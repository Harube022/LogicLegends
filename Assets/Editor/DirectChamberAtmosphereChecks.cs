#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Offline lighting checks using the real board camera and five challenge introductions.</summary>
public static class DirectChamberAtmosphereChecks
{
    const string Flag = "DirectAtmosphere.QA", Report = "Library/DirectChamberAtmosphereChecks.txt";
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags Statics = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    [MenuItem("Logic Legends/Direct Proofs/Check Chamber Atmosphere in Play Mode")]
    public static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty || SceneManager.GetActiveScene().path != DirectChamberAtmosphereSetup.ScenePath)
            throw new InvalidOperationException("Open and save Direct&Indirect before checking lighting.");
        SessionState.SetInt(Flag + ".Size", BookMapMobileChecks.GetSize());
        SessionState.SetInt(Flag + ".Trial", StageSelectionState.SelectedStage);
        SessionState.SetBool(Flag + ".Persistence", StageCompleteManager.SuppressPersistenceForValidation);
        SessionState.SetBool(Flag, true);
        File.WriteAllText(Report, "Offline Direct chamber cameras and fixture faults. No questions, progress, or rewards submitted.\n");
        EditorApplication.isPlaying = true;
    }
    [InitializeOnLoadMethod] static void Register()
    { EditorApplication.playModeStateChanged -= Restore; EditorApplication.playModeStateChanged += Restore; }
    static void Restore(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Flag, false)) return;
        SessionState.SetBool(Flag, false); SceneManager.sceneLoaded -= Loaded;
        foreach (string name in new[] { "ValidationTransport", "ValidationUid", "ValidationEmail" })
            typeof(TrialBackendClient).GetField(name, Statics)?.SetValue(null, null);
        StageCompleteManager.SuppressPersistenceForValidation = SessionState.GetBool(Flag + ".Persistence", false);
        StageSelectionState.SelectForEditorSpawn(SessionState.GetInt(Flag + ".Trial", 5));
        BookMapMobileChecks.SetSize(SessionState.GetInt(Flag + ".Size", 6));
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void Bootstrap()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        typeof(TrialBackendClient).GetField("ValidationTransport", Statics)?.SetValue(null,
            (Func<string, string, Task<string>>)((a, b) => Task.FromResult("{\"admin\":false}")));
        typeof(TrialBackendClient).GetField("ValidationUid", Statics)?.SetValue(null, "direct-atmosphere-qa");
        StageCompleteManager.SuppressPersistenceForValidation = true; StageSelectionState.SelectForEditorSpawn(5);
        SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded;
    }
    static void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.path != DirectChamberAtmosphereSetup.ScenePath) return;
        SceneManager.sceneLoaded -= Loaded;
        if (Player.LocalInstance == null)
        {
            var avatar = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Multiplayer_Male"));
            avatar.GetComponent<MultiplayerAvatarSync>().enabled = false;
            avatar.GetComponent<PlayerEquipmentLoader>().enabled = false;
            foreach (var skin in avatar.GetComponentsInChildren<PlayerAnimator>(true)) skin.gameObject.SetActive(skin.name == "UNIFORM_MALE");
            avatar.GetComponent<CharacterController>().enabled = true;
            var spawn = GameObject.Find("Trial Player Spawn").transform;
            avatar.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
        }
        DirectIndirectProofStageController.SuppressProgressionForValidation = true;
        new GameObject("Offline Direct Lighting Check").AddComponent<DirectGuideQaRunner>().StartCoroutine(Guard(Run()));
    }
    static IEnumerator Guard(IEnumerator work)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(work);
        while (stack.Count > 0)
        {
            object next;
            try { var top = stack.Peek(); if (!top.MoveNext()) { stack.Pop(); continue; } next = top.Current; }
            catch (Exception e) { File.AppendAllText(Report, "FAIL " + e + "\n"); Debug.LogException(e); EditorApplication.isPlaying = false; yield break; }
            if (next is IEnumerator nested) stack.Push(nested); else yield return next;
        }
        File.AppendAllText(Report, "ALL DIRECT CHAMBER ATMOSPHERE CHECKS PASSED\n"); EditorApplication.isPlaying = false;
    }
    static IEnumerator Run()
    {
        Application.runInBackground = true; BookMapMobileChecks.SetSize(6);
        var stage = UnityEngine.Object.FindFirstObjectByType<DirectIndirectProofStageController>();
        float until = Time.realtimeSinceStartup + 35;
        while ((stage.Guide == null || !stage.Guide.IsBusy) && Time.realtimeSinceStartup < until) yield return null;
        Check(stage.Guide != null && stage.Guide.IsBusy, "arrival Help available");
        stage.Guide.Cancel(); stage.CompleteOnboarding();
        var flicker = UnityEngine.Object.FindFirstObjectByType<ChamberLightFlicker>();
        Check(flicker != null && flicker.AffectedLights.Length == 3, "only three decorative fixtures have occasional faults");
        var lights = GameObject.Find("ChamberDecorations").GetComponentsInChildren<Light>(true).Where(l => l.isActiveAndEnabled).ToArray();
        var brightness = lights.ToDictionary(l => l, l => l.intensity);
        var stable = lights.Except(flicker.AffectedLights).ToArray();
        Check(lights.Length == 16 && lights.All(l => l.shadows == LightShadows.None), "16 short-range local lights, no real-time shadow maps");
        Check(stable.Any(l => l.name.Contains("Board")) && stable.Any(l => l.name.Contains("Book")), "reading lights stay steady");
        // Sample three simulated minutes of just the visual effect; no gameplay clock is advanced.
        var advance = typeof(ChamberLightFlicker).GetMethod("Advance", Hidden);
        var randomState = UnityEngine.Random.state;
        bool dip = false, outage = false; int altered = 0;
        for (int i = 0; i < 3600; i++)
        {
            advance.Invoke(flicker, new object[] { .05f });
            int changed = lights.Count(l => Mathf.Abs(l.intensity - brightness[l]) > .001f);
            altered = Mathf.Max(altered, changed);
            dip |= flicker.AffectedLights.Any(l => l.intensity > .01f && l.intensity < brightness[l] * .7f);
            outage |= flicker.AffectedLights.Any(l => l.intensity < .01f);
            CheckValue(lights.All(l => l.intensity >= 0 && l.intensity <= brightness[l] + .001f), "flicker exceeded original brightness");
        }
        Check(dip && outage && flicker.CompletedEvents >= 6, "irregular smooth dips and short outages occur between long steady intervals");
        Check(altered <= 1 && stable.All(l => l.intensity == brightness[l]), "only one faulty fixture changes at a time; key lights stay steady");
        Check(randomState.Equals(UnityEngine.Random.state), "private flicker randomness preserves gameplay random stream");
        flicker.enabled = false;
        Check(lights.All(l => l.intensity == brightness[l]), "disabling the effect restores every original intensity");
        flicker.enabled = true;
        var colliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToDictionary(c => c, c => c.enabled);
        var roofs = GameObject.Find("ChamberCeiling").GetComponentsInChildren<Renderer>(true);
        float seconds = stage.SecondsLeft; var guide = stage.Guide;
        for (int challenge = 1; challenge <= 5; challenge++)
        {
            typeof(DirectIndirectProofStageController).GetField("challengeIndex", Hidden).SetValue(stage, challenge - 1);
            until = Time.realtimeSinceStartup + 8;
            while (!guide.IsIntroduction && Time.realtimeSinceStartup < until) yield return null;
            Check(guide.IsIntroduction && Time.timeScale == 0 && roofs.All(r => !r.enabled), "challenge " + challenge + " overhead view hides ceiling and pauses gameplay");
            for (int page = 0; page < 3; page++)
            {
                yield return new WaitForSecondsRealtime(1.6f); yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot("Library/Direct_Atmosphere_Intro" + challenge + "_" + page + ".png");
                Check(guide.NextButton.interactable && stable.All(l => l.intensity == brightness[l]), "challenge " + challenge + " page " + (page + 1) + " readable with steady task lighting");
                if (challenge == 2 && page == 1) { guide.Skip(); break; }
                guide.Next(); yield return null;
            }
            yield return new WaitForSecondsRealtime(1.1f);
            Check(!guide.IsBusy && roofs.All(r => r.enabled) && colliders.All(p => p.Key.enabled == p.Value), "challenge " + challenge + " restores ceiling and preserves collisions");
            Check(stage.State == DirectIndirectProofStageController.TrialState.WaitingForActivation && stage.SecondsLeft == seconds && Player.LocalInstance.enabled &&
                !GameInput.Instance.GameplayInputBlocked, "challenge " + challenge + " leaves book and timer waiting, controls restored");
        }
        // Use the real proximity-gated View Board / Back flow at three mobile aspect ratios.
        var boardArea = (Transform)typeof(DirectIndirectProofStageController).GetField("boardInteractionPoint", Hidden).GetValue(stage);
        PrelimTeleportTransition.MovePlayerImmediately(Player.LocalInstance.gameObject, boardArea.position, Quaternion.Euler(0, 180, 0));
        foreach (int size in new[] { 6, 12, 3 })
        {
            BookMapMobileChecks.SetSize(size); yield return new WaitForSecondsRealtime(.4f);
            stage.WorldGuidance.OpenBoard(); yield return new WaitForSecondsRealtime(1.1f);
            Check(stage.IsViewingBoard && !stage.WorldGuidance.BoardView.Transitioning, "readable board framing settles at " + Screen.width + "x" + Screen.height);
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot("Library/Direct_Atmosphere_Board_"+Screen.width+"x"+Screen.height+".png");
            stage.WorldGuidance.BoardView.Back(); yield return new WaitForSecondsRealtime(1.1f);
            Check(!stage.IsViewingBoard && Player.LocalInstance.enabled && !GameInput.Instance.GameplayInputBlocked, "Back restores gameplay controls");
        }
        var tex = TreesChamberSetup.Capture("Library/Direct_Atmosphere_Gameplay.png", new Vector3(56.7f,4.8f,70.5f),new Vector3(58,4,48),65);
        var pixels = tex.GetPixels32();
        Check(pixels.Count(p => p.r > 248 && p.g > 248 && p.b > 248) < pixels.Length * .03f, "no large overexposed highlights in gameplay view");
        UnityEngine.Object.Destroy(tex);
        Check(stage.SecondsLeft == seconds && stage.State == DirectIndirectProofStageController.TrialState.WaitingForActivation, "camera and light checks changed no timer, question, or challenge progress");
    }
    static void CheckValue(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    static void Check(bool ok, string message) { CheckValue(ok, message); File.AppendAllText(Report, "PASS " + message + "\n"); }
}
#endif
