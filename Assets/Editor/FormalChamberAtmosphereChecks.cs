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

/// <summary>Offline camera/roof checks. Never starts a question, submits work, or awards rewards.</summary>
public static class FormalChamberAtmosphereChecks
{
    const string Flag = "FormalAtmosphere.QA", Report = "Library/FormalChamberAtmosphereChecks.txt";
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags Statics = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    [MenuItem("Logic Legends/Proofs Library/Check Chamber Atmosphere in Play Mode")]
    public static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty ||
            SceneManager.GetActiveScene().path != FormalChamberAtmosphereSetup.ScenePath)
            throw new InvalidOperationException("Open and save the proof chamber before checking it.");
        SessionState.SetInt(Flag + ".Size", BookMapMobileChecks.GetSize());
        SessionState.SetInt(Flag + ".Trial", StageSelectionState.SelectedStage);
        SessionState.SetBool(Flag + ".Persistence", StageCompleteManager.SuppressPersistenceForValidation);
        SessionState.SetBool(Flag, true);
        File.WriteAllText(Report, "Offline chamber cameras, all five introductions, and roof/input restoration. No question activation or rewards.\n");
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
        StageSelectionState.SelectForEditorSpawn(SessionState.GetInt(Flag + ".Trial", 4));
        BookMapMobileChecks.SetSize(SessionState.GetInt(Flag + ".Size", 6));
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void Bootstrap()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        typeof(TrialBackendClient).GetField("ValidationTransport", Statics)?.SetValue(null,
            (Func<string, string, Task<string>>)((a, b) => Task.FromResult("{\"admin\":false}")));
        typeof(TrialBackendClient).GetField("ValidationUid", Statics)?.SetValue(null, "formal-atmosphere-qa");
        StageCompleteManager.SuppressPersistenceForValidation = true;
        StageSelectionState.SelectForEditorSpawn(4);
        SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded;
    }
    static void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.path != FormalChamberAtmosphereSetup.ScenePath) return;
        SceneManager.sceneLoaded -= Loaded;
        if (Player.LocalInstance == null)
        {
            var avatar = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Multiplayer_Male"));
            avatar.GetComponent<MultiplayerAvatarSync>().enabled = false;
            avatar.GetComponent<PlayerEquipmentLoader>().enabled = false;
            foreach (var skin in avatar.GetComponentsInChildren<PlayerAnimator>(true)) skin.gameObject.SetActive(skin.name == "UNIFORM_MALE");
            avatar.GetComponent<CharacterController>().enabled = true;
            var spawn = GameObject.Find("Proofs Player Spawn").transform;
            avatar.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
        }
        ProofsLibraryStageController.SuppressProgressionForValidation = true;
        new GameObject("Offline Chamber Atmosphere Check").AddComponent<ProofsQaRunner>().StartCoroutine(Guard(Run()));
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
        File.AppendAllText(Report, "ALL CHAMBER ATMOSPHERE CHECKS PASSED\n"); EditorApplication.isPlaying = false;
    }
    static IEnumerator Run()
    {
        Application.runInBackground = true;
        BookMapMobileChecks.SetSize(6);
        float until = Time.realtimeSinceStartup + 35;
        var stage = UnityEngine.Object.FindFirstObjectByType<ProofsLibraryStageController>();
        while ((stage.Presentation == null || stage.Presentation.Help == null || !stage.Presentation.Help.IsOpen) && Time.realtimeSinceStartup < until) yield return null;
        Check(stage.Presentation.Help.IsOpen, "arrival Help available");
        stage.Presentation.Help.CloseHelp(); stage.FinishOnboarding();
        float seconds = stage.SecondsLeft;
        var roofs = stage.TutorialCeilingRenderers;
        Check(roofs.Length > 0 && roofs.All(r => r != null && r.enabled), "fitted ceiling has explicit live renderer references");
        var colliders = stage.BookTarget.root.GetComponentsInChildren<Collider>(true).ToDictionary(c => c, c => c.enabled);
        var chamber = stage.BookTarget.root.Find("Induction Chamber").GetComponent<Renderer>();
        var intro = stage.Introductions;
        for (int challenge = 1; challenge <= 5; challenge++)
        {
            typeof(ProofsLibraryStageController).GetField("challengeIndex", Hidden).SetValue(stage, challenge - 1);
            until = Time.realtimeSinceStartup + 6;
            while (!intro.IsOpen && Time.realtimeSinceStartup < until) yield return null;
            Check(intro.IsOpen && Time.timeScale == 0, "challenge " + challenge + " introduction pauses time");
            Check(roofs.All(r => !r.enabled) && chamber.enabled, "challenge " + challenge + " hides roof only; floor and walls render");
            int pageCount = intro.PageCount;
            for (int page = 0; page < pageCount; page++)
            {
                until = Time.realtimeSinceStartup + 12;
                while (!intro.NextButton.interactable && Time.realtimeSinceStartup < until) yield return null;
                Check(intro.NextButton.interactable, "challenge " + challenge + " page " + (page + 1) + " frames target");
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot("Library/Formal_Atmosphere_Intro" + challenge + "_" + page + ".png");
                if (challenge == 2 && page == 1) { intro.Skip(); break; }
                intro.Next(); yield return null;
            }
            Check(!intro.IsOpen && roofs.All(r => r.enabled), "challenge " + challenge + " restores ceiling after Next/Skip");
            Check(colliders.All(p => p.Key.enabled == p.Value), "challenge " + challenge + " preserves all collider states");
            Check(stage.WaitingForBook && !stage.Started && stage.SecondsLeft == seconds && Player.LocalInstance.enabled &&
                !GameInput.Instance.GameplayInputBlocked, "challenge " + challenge + " restores input; book and timer still wait");
            yield return new WaitForSecondsRealtime(1);
        }
        var lights = stage.BookTarget.root.GetComponentsInChildren<Light>().Where(l => l.isActiveAndEnabled).ToArray();
        Check(lights.All(l => l.shadows == LightShadows.None) && lights.Length <= 16, "at most 16 local lights, all shadowless");
        foreach (int size in new[] { 6, 12, 3 })
        {
            BookMapMobileChecks.SetSize(size); yield return new WaitForSecondsRealtime(.5f);
            Check(stage.Presentation.BoardView.OpenView(null), "board camera opens at " + Screen.width + "x" + Screen.height);
            yield return new WaitForSecondsRealtime(1.1f); yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("Library/Formal_Atmosphere_Board_" + Screen.width + "x" + Screen.height + ".png");
            Check(roofs.All(r => r.enabled) && !stage.Presentation.BoardView.Transitioning, "board framing settles with roof restored");
            stage.Presentation.BoardView.Back(); yield return new WaitForSecondsRealtime(1.1f);
            Check(!stage.Presentation.BoardView.Viewing && stage.WaitingForBook, "Back restores gameplay without activating book");
        }
        BookMapMobileChecks.SetSize(6);
        var views = new[] {
            new {name="Gameplay",at=new Vector3(0,5.9f,-17.1f),look=new Vector3(0,3.6f,4),fov=65f},
            new {name="Scrolls",at=new Vector3(-9,4,-6),look=new Vector3(-9,2,0),fov=60f},
            new {name="Ceiling",at=new Vector3(0,8,-6),look=new Vector3(0,18,2),fov=70f}
        };
        foreach (var shot in views)
        {
            var tex = TreesChamberSetup.Capture("Library/Formal_Atmosphere_"+shot.name+".png",shot.at,shot.look,shot.fov);
            var pixels = tex.GetPixels32();
            Check(pixels.Count(p => p.r > 248 && p.g > 248 && p.b > 248) < pixels.Length * .03f, shot.name + " avoids blown white highlights");
            UnityEngine.Object.Destroy(tex); yield return null;
        }
    }
    static void Check(bool ok, string message)
    { if (!ok) throw new InvalidOperationException(message); File.AppendAllText(Report, "PASS " + message + "\n"); }
}
#endif
