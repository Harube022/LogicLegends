using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>Account-scoped, local first-entry film. Does not create or join a room.</summary>
public sealed class ArchiveFirstMultiplayerIntro : MonoBehaviour
{
    const string PreferencePrefix = "LogicLegends.OpenArchive.IntroSeen.v1.";
    static ArchiveFirstMultiplayerIntro active;
    VideoPlayer player;
    RenderTexture texture;
    Action continueToMenu;
    string watchedKey;
    bool finished;
    GameObject previousSelection;
    readonly System.Collections.Generic.List<AudioSource> pausedMusic = new System.Collections.Generic.List<AudioSource>();

    public static bool IsShowing => active != null;
    public static string KeyFor(string uid) => PreferencePrefix + (string.IsNullOrEmpty(uid) ? "local-device" : uid);
    public static bool HasSeen(string uid) => PlayerPrefs.GetInt(KeyFor(uid), 0) == 1;

    public static bool TryPlay(MonoBehaviour owner, VideoClip clip, Action next)
    {
        if (active != null) return true; // Repeated taps cannot stack playback or menu callbacks.
        if (clip == null || HasSeen(TrialBackendClient.UserId)) return false;
        var root = new GameObject("Open Archive First Multiplayer Story", typeof(RectTransform));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, owner.gameObject.scene);
        var intro = root.AddComponent<ArchiveFirstMultiplayerIntro>();
        active = intro;
        intro.continueToMenu = next;
        intro.watchedKey = KeyFor(TrialBackendClient.UserId);
        try { intro.Begin(clip); return true; }
        catch (Exception error)
        {
            Debug.LogWarning("Open Archive story setup failed; opening multiplayer menu. " + error.Message);
            intro.Cleanup(); Destroy(root); return false;
        }
    }

    void Begin(VideoClip clip)
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        gameObject.AddComponent<GraphicRaycaster>();
        var black = MultiplayerUIArt.Rect("Cinema Backdrop", transform).gameObject.AddComponent<Image>();
        black.color = Color.black; // Full-screen raycast shield leaves the menu untouched underneath.
        var image = MultiplayerUIArt.Rect("Story Film", transform).gameObject.AddComponent<RawImage>();
        image.color = Color.white;
        var fit = image.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fit.aspectRatio = clip.width / (float)Mathf.Max(1, (int)clip.height);
        texture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);
        texture.Create();
        var old = RenderTexture.active;
        RenderTexture.active = texture; GL.Clear(true, true, Color.black); RenderTexture.active = old;
        image.texture = texture;
        var loading = MultiplayerUIArt.Label("Preparing Story", transform, "ENTERING THE OPEN ARCHIVE…", .15f, .44f, .85f, .56f, 32, false, new Color(.9f, .82f, .61f));
        loading.alignment = TextAlignmentOptions.Center;
        var skip = MultiplayerUIArt.Action("Skip Story", transform, "SKIP", .88f, .9f, .88f, .9f);
        MultiplayerUIArt.StandardAction(skip, 210);
        skip.onClick.AddListener(() => Complete(true));
        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            previousSelection = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }
        // Pause only playing menu music, then resume its existing position on exit.
        foreach (var source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            if (source.isPlaying && source.loop && (source.gameObject.scene == gameObject.scene || source.outputAudioMixerGroup != null && source.outputAudioMixerGroup.name == "Music"))
            { pausedMusic.Add(source); source.Pause(); }
        var audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false; audioSource.spatialBlend = 0; audioSource.ignoreListenerPause = true;
        AudioVolumeSettings.Route(audioSource, GameAudioChannel.Music);
        player = gameObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false; player.isLooping = false;
        player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        player.source = VideoSource.VideoClip; player.clip = clip;
        player.renderMode = VideoRenderMode.RenderTexture; player.targetTexture = texture;
        player.aspectRatio = VideoAspectRatio.FitInside; player.waitForFirstFrame = true;
        player.audioOutputMode = VideoAudioOutputMode.AudioSource;
        player.controlledAudioTrackCount = 1;
        player.EnableAudioTrack(0, true);
        player.SetTargetAudioSource(0, audioSource);
        player.loopPointReached += OnEnded; player.errorReceived += OnError;
        player.prepareCompleted += OnPrepared;
        player.Prepare();
        StartCoroutine(PrepareTimeout());
    }

    IEnumerator PrepareTimeout()
    {
        float limit = Time.realtimeSinceStartup + 15;
        while (!finished && player != null && !player.isPrepared && Time.realtimeSinceStartup < limit) yield return null;
        if (!finished && (player == null || !player.isPrepared)) Complete(false);
    }
    void OnPrepared(VideoPlayer source)
    {
        if (finished) return;
        var loading = transform.Find("Preparing Story");
        if (loading != null) loading.gameObject.SetActive(false);
        source.Play();
    }
    void OnEnded(VideoPlayer source) => Complete(true);
    void OnError(VideoPlayer source, string message)
    {
        Debug.LogWarning("Open Archive intro could not play; opening multiplayer menu. " + message);
        Complete(false); // Playback failure must never prevent entry or mark the story watched.
    }
    public void Skip() => Complete(true);
    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame) Complete(true);
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape)) Complete(true);
#endif
    }
    void Complete(bool watched)
    {
        if (finished) return;
        finished = true;
        if (watched) { PlayerPrefs.SetInt(watchedKey, 1); PlayerPrefs.Save(); }
        var next = continueToMenu; continueToMenu = null;
        Cleanup();
        Destroy(gameObject);
        next?.Invoke();
    }
    void Cleanup()
    {
        if (active == this) active = null;
        if (player != null)
        {
            player.loopPointReached -= OnEnded; player.errorReceived -= OnError; player.prepareCompleted -= OnPrepared;
            player.Stop(); player.targetTexture = null;
        }
        if (texture != null) { texture.Release(); Destroy(texture); texture = null; }
        foreach (var music in pausedMusic) if (music != null) music.UnPause();
        pausedMusic.Clear();
        if (previousSelection != null && UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(previousSelection);
        previousSelection = null;
    }
    void OnDestroy() { finished = true; continueToMenu = null; Cleanup(); }
}
