using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Scene-local presentation around the existing Solo trial spawn and result flow.</summary>
public sealed class SoloTrialCinematic : MonoBehaviour
{
    const float ArrivalSeconds = 2.8f, ExitSeconds = 3.5f, EndingSeconds = 11f;
    const int SortingOrder = 24000;
    static SoloTrialCinematic current;
    static readonly float[] PortalAngles = { 0, 45, -45, 90, -90, 135, -135, 180 };
    static readonly int Walking = Animator.StringToHash("IsWalking"), Running = Animator.StringToHash("IsRunning"),
        Jumping = Animator.StringToHash("IsJumping"), Playback = Animator.StringToHash(PlayerAnimator.WALK_PLAYBACK);
    readonly List<Canvas> hiddenCanvases = new List<Canvas>();
    readonly List<Renderer> hiddenPlayer = new List<Renderer>();
    readonly RaycastHit[] hits = new RaycastHit[24];
    SoloTrialCinematicAssets assets;
    SoloTrialCinematicVisuals effects;
    Player player;
    GameInput input;
    Camera cameraView;
    CinemachineBrain brain;
    ThirdPersonCameraController legacyCamera;
    bool restoreBrain, restoreLegacy, lockedCamera, skip, completed;
    Vector3 cameraPosition;
    Quaternion cameraRotation;
    float fieldOfView, farPlane;
    CameraClearFlags clearFlags;
    Color clearColor;
    Canvas filmCanvas;
    Image shade;
    TMP_Text title, subtitle;
    RectTransform safe;
    AudioSource sound, music;
    Animator animator;
    PlayerAnimator animatorDriver;
    AnimatorUpdateMode animatorMode;
    bool restoreDriver;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState() { current = null; SceneManager.sceneLoaded -= Install; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() { SceneManager.sceneLoaded += Install; }

    static void Install(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || PhotonNetwork.InRoom || !TeleportManager.IsTrialScene(scene.name)) return;
        var root = new GameObject("Solo Trial Cinematics");
        SceneManager.MoveGameObjectToScene(root, scene);
        current = root.AddComponent<SoloTrialCinematic>();
    }

    public static void PlayClear(int trial)
    {
        if (current == null || current.completed || PhotonNetwork.InRoom || trial < 1 || trial > TrialCatalog.Count) return;
        var scene = current.gameObject.scene;
        if (TrialCatalog.ForScene(scene.name, StageSelectionState.SelectedStage).Number != trial) return;
        var local = Player.LocalInstance;
        var main = Camera.main;
        if (local == null || main == null || local.gameObject.scene != scene || !current.Prepare()) return;
        current.completed = true;
        current.StopAllCoroutines();
        current.RestoreShot();
        current.StartCoroutine(current.ClearTrial(local, main, trial));
    }

    IEnumerator Start()
    {
        if (!Prepare()) yield break;
        float readyBy = Time.unscaledTime + 15f;
        while (!completed && !TryBindPlayer())
        {
            if (Time.unscaledTime >= readyBy) yield break;
            yield return null;
        }
        if (completed) yield break;
        SetFilmVisible(true);
        shade.color = Color.black;
        while (TeleportManager.Instance != null && TeleportManager.Instance.IsTransitioning)
        {
            if (Time.unscaledTime >= readyBy || player == null) { RestoreShot(); yield break; }
            yield return null;
        }
        TakeShot("Trial summoning");
        var trial = TrialCatalog.ForScene(gameObject.scene.name, StageSelectionState.SelectedStage);
        title.text = trial.Title;
        subtitle.text = "The Logic Guardian has summoned you to this trial.";
        var feet = Feet(player);
        float groundProbeRise = Mathf.Max(.55f, player.transform.position.y - feet.y + .15f);
        if (TryGround(feet, out var ground, groundProbeRise)) feet = ground;
        var start = cameraPosition;
        cameraView.fieldOfView = Mathf.Clamp(fieldOfView + 12, 48, 75);
        HideAvatar();
        sound.PlayOneShot(assets.summonSound, .65f);
        float time = 0;
        while (!skip && player != null && time < ArrivalSeconds)
        {
            float progress = time / ArrivalSeconds;
            shade.color = new Color(0, 0, 0, 1 - Ease(0, .55f, time));
            effects.Summon(feet, progress);
            if (time >= 1.05f) RevealAvatar();
            if (cameraView != null)
                cameraView.transform.position = start + cameraRotation * Vector3.forward * (.32f * Ease(.35f, 2.5f, time));
            yield return null;
            time += Time.unscaledDeltaTime;
        }
        RestoreShot();
    }

    IEnumerator ClearTrial(Player local, Camera main, int trial)
    {
        player = local;
        cameraView = main;
        SetFilmVisible(true);
        shade.color = Color.black;
        // Result controllers finish disabling their own input and pausing their timers in this frame.
        yield return null;
        if (player == null || cameraView == null) { RestoreShot(); yield break; }
        TakeShot("Trial exit portal");
        var original = player.transform.position;
        var feet = Feet(player);
        var capsule = player.GetComponent<CharacterController>();
        float height = capsule != null ? capsule.height * Mathf.Abs(capsule.transform.lossyScale.y) : 2;
        height = Mathf.Clamp(height, 1.7f, 4.8f);
        FindRoute(feet, height, out var portal, out var direction);
        var end = portal + direction * .42f + (original - feet);
        var right = Vector3.Cross(Vector3.up, direction);
        var focus = Vector3.Lerp(feet, portal, .45f) + Vector3.up * height * .63f;
        var cameraGoal = focus - direction * (height * 2.15f + 1.9f) + right * (height * 1.1f) + Vector3.up * (height * .56f);
        Vector3 cameraOffset = cameraGoal - focus;
        float safeDistance = ClearDistance(focus, cameraOffset.normalized, .25f, cameraOffset.magnitude);
        var goal = focus + cameraOffset.normalized * Mathf.Max(.6f, safeDistance - .18f);
        var goalRotation = Quaternion.LookRotation(focus - goal, Vector3.up);
        title.text = "TRIAL COMPLETE";
        subtitle.text = "The passage opens.";
        sound.PlayOneShot(assets.summonSound, .6f);
        float time = 0;
        while (!skip && player != null && cameraView != null && time < ExitSeconds)
        {
            float walk = Ease(.88f, 2.85f, time);
            float blend = Ease(0, .75f, time);
            var facing = Quaternion.LookRotation(direction, Vector3.up);
            player.transform.rotation = Quaternion.RotateTowards(player.transform.rotation, facing, 350 * Time.unscaledDeltaTime);
            if (walk > 0 && walk < 1) SetWalk(true);
            player.AcquireMovementLock(this, "Trial exit portal", Vector3.Lerp(original, end, walk), 100);
            if (walk >= .93f) HideAvatar();
            cameraView.transform.SetPositionAndRotation(Vector3.Lerp(cameraPosition, goal, blend),
                Quaternion.Slerp(cameraRotation, goalRotation, blend));
            effects.Portal(portal, direction, height, Ease(0, .9f, time), time);
            shade.color = new Color(0, 0, 0, Mathf.Max(1 - Ease(0, .28f, time), Ease(2.72f, ExitSeconds, time)));
            yield return null;
            time += Time.unscaledDeltaTime;
        }
        SetWalk(false);
        effects.Hide();
        if (!skip && player != null && cameraView != null && trial == TrialCatalog.Count)
            yield return Ending();
        RestoreShot();
    }

    IEnumerator Ending()
    {
        var center = player.transform.position + Vector3.up * 5000;
        effects.BuildSanctuary(center);
        cameraView.clearFlags = CameraClearFlags.SolidColor;
        cameraView.backgroundColor = new Color(.025f, .047f, .085f);
        cameraView.farClipPlane = 65;
        cameraView.fieldOfView = 40;
        music = gameObject.AddComponent<AudioSource>();
        music.playOnAwake = false;
        AudioVolumeSettings.Route(music, GameAudioChannel.Music);
        music.clip = assets.restorationMusic;
        music.volume = .38f;
        music.Play();
        float time = 0;
        while (!skip && time < EndingSeconds)
        {
            float repaired = Ease(2.4f, 5.5f, time);
            effects.RestorePillar(time, repaired);
            float sweep = Mathf.Lerp(-24, 31, Ease(0, EndingSeconds, time));
            float distance = Mathf.Lerp(15f, 11.7f, Ease(0, EndingSeconds, time));
            var position = center + Quaternion.Euler(0, sweep, 0) * new Vector3(0, 4.6f, -distance);
            cameraView.transform.SetPositionAndRotation(position, Quaternion.LookRotation(center + Vector3.up * 3.5f - position));
            title.text = time < 6.4f ? "" : "BALANCE RESTORED";
            subtitle.text = time < 2.5f ? "The final trial is complete." :
                time < 6.4f ? "The Pillar of Logic is whole again." :
                "What you learned here goes with you.";
            shade.color = new Color(0, 0, 0, Mathf.Max(1 - Ease(0, .85f, time), Ease(10.2f, EndingSeconds, time)));
            yield return null;
            time += Time.unscaledDeltaTime;
        }
    }

    bool Prepare()
    {
        if (effects != null) return true;
        assets = Resources.Load<SoloTrialCinematicAssets>("StageUI/SoloTrialCinematicAssets");
        if (assets == null || assets.summonEnergy == null || assets.portalShader == null || assets.pillarCrystal == null ||
            assets.pillarHoop == null || assets.pillarShader == null || assets.templeStone == null || assets.antiqueGold == null ||
            assets.summonSound == null || assets.restorationMusic == null) return false;
        effects = new SoloTrialCinematicVisuals(transform, assets);
        BuildFilmCanvas();
        sound = gameObject.AddComponent<AudioSource>();
        sound.playOnAwake = false;
        AudioVolumeSettings.Route(sound, GameAudioChannel.SoundFX);
        return true;
    }

    bool TryBindPlayer()
    {
        player = Player.LocalInstance;
        cameraView = Camera.main;
        return player != null && player.gameObject.scene == gameObject.scene && cameraView != null;
    }

    void TakeShot(string reason)
    {
        input = GameInput.Instance;
        input?.AcquireGameplayActionBlock(this);
        player.AcquireMovementLock(this, reason, player.transform.position, 100);
        cameraPosition = cameraView.transform.position;
        cameraRotation = cameraView.transform.rotation;
        clearFlags = cameraView.clearFlags;
        clearColor = cameraView.backgroundColor;
        fieldOfView = cameraView.fieldOfView;
        farPlane = cameraView.farClipPlane;
        brain = cameraView.GetComponent<CinemachineBrain>();
        restoreBrain = brain != null && brain.enabled;
        if (restoreBrain) brain.enabled = false;
        legacyCamera = cameraView.GetComponent<ThirdPersonCameraController>();
        restoreLegacy = legacyCamera != null && legacyCamera.enabled;
        if (restoreLegacy) legacyCamera.enabled = false;
        lockedCamera = true;
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!canvas.enabled || canvas == filmCanvas || canvas.renderMode == RenderMode.WorldSpace || canvas.sortingOrder >= SortingOrder) continue;
            hiddenCanvases.Add(canvas);
            canvas.enabled = false;
        }
    }

    void BuildFilmCanvas()
    {
        var root = StageUITheme.Rect("Cinematic Overlay", transform);
        filmCanvas = root.gameObject.AddComponent<Canvas>();
        filmCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        filmCanvas.sortingOrder = SortingOrder;
        var scaler = root.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        root.gameObject.AddComponent<GraphicRaycaster>();
        shade = StageUITheme.Rect("Cinematic Fade", root).gameObject.AddComponent<Image>();
        StageUITheme.Stretch(shade.rectTransform, Vector2.zero, Vector2.one);
        var lower = StageUITheme.Rect("Caption Backing", root);
        StageUITheme.Stretch(lower, Vector2.zero, new Vector2(1, .17f));
        var backing = lower.gameObject.AddComponent<Image>();
        backing.color = new Color(.012f, .025f, .05f, .93f);
        backing.raycastTarget = false;
        safe = StageUITheme.Rect("Cinematic Safe Area", root);
        MultiplayerSafeArea.Apply(safe);
        safe.offsetMin = safe.offsetMax = Vector2.zero;
        title = MakeCaption("Chapter Title", 42, .09f, .152f);
        subtitle = MakeCaption("Story Subtitle", 29, .029f, .091f);
        var button = StageUITheme.Button("Skip Cinematic", safe, "SKIP", () => skip = true);
        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-32, -25);
        rect.sizeDelta = new Vector2(155, 62);
        SetFilmVisible(false);
    }

    TMP_Text MakeCaption(string name, int size, float bottom, float top)
    {
        var label = StageUITheme.Text(name, safe, "", size, name == "Story Subtitle");
        StageUITheme.Stretch(label.rectTransform, new Vector2(.06f, bottom), new Vector2(.94f, top));
        label.color = new Color(1, .94f, .80f);
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = size * .7f;
        label.fontSizeMax = size;
        label.raycastTarget = false;
        return label;
    }

    void SetFilmVisible(bool visible)
    {
        if (filmCanvas == null) return;
        skip = false;
        title.text = subtitle.text = "";
        filmCanvas.gameObject.SetActive(visible);
        if (visible) MultiplayerSafeArea.Apply(safe);
    }

    void HideAvatar()
    {
        if (player == null || hiddenPlayer.Count > 0) return;
        foreach (var renderer in player.GetComponentsInChildren<Renderer>())
        {
            if (renderer.forceRenderingOff) continue;
            renderer.forceRenderingOff = true;
            hiddenPlayer.Add(renderer);
        }
    }

    void RevealAvatar()
    {
        foreach (var renderer in hiddenPlayer) if (renderer != null) renderer.forceRenderingOff = false;
        hiddenPlayer.Clear();
    }

    void SetWalk(bool walking)
    {
        if (walking)
        {
            if (animator != null || player == null) return;
            animatorDriver = player.GetComponentInChildren<PlayerAnimator>();
            if (animatorDriver == null) return;
            animator = animatorDriver.GetComponent<Animator>();
            if (animator == null) return;
            restoreDriver = animatorDriver.enabled;
            animatorDriver.enabled = false;
            animatorMode = animator.updateMode;
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            foreach (var parameter in animator.parameters)
            {
                if (parameter.nameHash == Walking) animator.SetBool(Walking, true);
                if (parameter.nameHash == Running || parameter.nameHash == Jumping) animator.SetBool(parameter.nameHash, false);
                if (parameter.nameHash == Playback) animator.SetFloat(Playback, 1);
            }
        }
        else if (animator != null)
        {
            foreach (var parameter in animator.parameters)
                if (parameter.nameHash == Walking) animator.SetBool(Walking, false);
            animator.updateMode = animatorMode;
            if (restoreDriver && animatorDriver != null) animatorDriver.enabled = true;
            animator = null;
        }
    }

    void FindRoute(Vector3 feet, float height, out Vector3 portal, out Vector3 direction)
    {
        var forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < .5f) forward = Vector3.forward;
        foreach (float distance in new[] { 3.5f, 2.25f, 1.35f })
        {
            foreach (float angle in PortalAngles)
            {
                var axis = Quaternion.Euler(0, angle, 0) * forward;
                if (ClearDistance(feet + Vector3.up * height * .57f, axis, Mathf.Clamp(height * .2f, .25f, .75f), distance + .55f) < distance + .5f) continue;
                if (!TryGround(feet + axis * (distance * .5f), out _) || !TryGround(feet + axis * (distance + .45f), out var ground)) continue;
                portal = ground - axis * .45f;
                direction = axis;
                return;
            }
        }
        // At a cramped ledge, close the effect around the avatar's safe position.
        direction = forward;
        portal = feet - forward * .42f;
    }

    float ClearDistance(Vector3 origin, Vector3 direction, float radius, float distance)
    {
        int count = Physics.SphereCastNonAlloc(origin, radius, direction, hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (hits[i].collider != null && !hits[i].collider.transform.IsChildOf(player.transform))
                distance = Mathf.Min(distance, hits[i].distance);
        return distance;
    }

    bool TryGround(Vector3 point, out Vector3 ground, float probeRise = .55f)
    {
        ground = point;
        int count = Physics.RaycastNonAlloc(point + Vector3.up * probeRise, Vector3.down, hits, probeRise + .55f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var hit = hits[i];
            if (hit.collider == null || hit.collider.transform.IsChildOf(player.transform) || hit.normal.y < .65f || hit.distance >= nearest) continue;
            nearest = hit.distance;
            ground = hit.point;
        }
        return nearest < float.MaxValue;
    }

    static Vector3 Feet(Player avatar)
    {
        var capsule = avatar.GetComponent<CharacterController>();
        return capsule != null ? capsule.transform.TransformPoint(capsule.center - Vector3.up * capsule.height * .5f) : avatar.transform.position;
    }

    static float Ease(float from, float to, float value) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(from, to, value));

    void RestoreShot()
    {
        SetWalk(false);
        RevealAvatar();
        player?.ReleaseMovementLock(this);
        input?.ReleaseGameplayActionBlock(this);
        if (lockedCamera && cameraView != null)
        {
            cameraView.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            cameraView.fieldOfView = fieldOfView;
            cameraView.farClipPlane = farPlane;
            cameraView.clearFlags = clearFlags;
            cameraView.backgroundColor = clearColor;
            if (restoreBrain && brain != null) brain.enabled = true;
            if (restoreLegacy && legacyCamera != null) legacyCamera.enabled = true;
        }
        lockedCamera = false;
        foreach (var canvas in hiddenCanvases) if (canvas != null) canvas.enabled = true;
        hiddenCanvases.Clear();
        effects?.Hide();
        if (sound != null) sound.Stop();
        if (music != null) music.Stop();
        SetFilmVisible(false);
    }

    void OnDestroy()
    {
        RestoreShot();
        effects?.Dispose();
        if (current == this) current = null;
    }
}
