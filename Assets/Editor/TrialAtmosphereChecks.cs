#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using LogicLegends.Induction;

/// <summary>Offline lighting and real tutorial-camera checks; no answers, rewards, or recording imports.</summary>
public static class TrialAtmosphereChecks
{
    const string Flag = "FiveAtmospheres.QA", Report = "Library/TrialAtmosphereChecks.txt";
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly string[] History = { "FunctionsRelations.LastVariant.", "LogicLegends.Induction.LastVariant." };
    [MenuItem("Logic Legends/Lighting/Check Five Trial Atmospheres in Play Mode")]
    public static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save the scene and leave Play Mode first.");
        SessionState.SetString(Flag + ".Scene", SceneManager.GetActiveScene().path);
        SessionState.SetInt(Flag + ".Size", BookMapMobileChecks.GetSize());
        SessionState.SetInt(Flag + ".Trial", StageSelectionState.SelectedStage);
        SessionState.SetBool(Flag + ".Persistence", StageCompleteManager.SuppressPersistenceForValidation);
        foreach (string prefix in History) for (int i = 0; i < 5; i++)
        { string key = prefix + i; SessionState.SetBool(Flag + key, PlayerPrefs.HasKey(key)); SessionState.SetInt(Flag + key + ".Value", PlayerPrefs.GetInt(key)); }
        SessionState.SetBool(Flag, true);
        File.WriteAllText(Report, "Offline saved scenes: gameplay, actual tutorial cameras, board framing, sunlight and fixture budgets. No submissions or rewards.\n");
        EditorSceneManager.OpenScene("Assets/Scenes/Induction.unity"); EditorApplication.isPlaying = true;
    }
    [InitializeOnLoadMethod] static void Register()
    { EditorApplication.playModeStateChanged -= Restore; EditorApplication.playModeStateChanged += Restore; }
    static void Restore(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Flag, false)) return;
        SessionState.SetBool(Flag, false); SceneManager.sceneLoaded -= Loaded;
        foreach (string name in new[] { "ValidationTransport", "ValidationUid", "ValidationEmail" }) typeof(TrialBackendClient).GetField(name, Fields)?.SetValue(null, null);
        StageCompleteManager.SuppressPersistenceForValidation = SessionState.GetBool(Flag + ".Persistence", false);
        StageSelectionState.SelectForEditorSpawn(SessionState.GetInt(Flag + ".Trial", 5)); BookMapMobileChecks.SetSize(SessionState.GetInt(Flag + ".Size", 6));
        foreach (string prefix in History) for (int i = 0; i < 5; i++)
        { string key = prefix + i; if (SessionState.GetBool(Flag + key, false)) PlayerPrefs.SetInt(key, SessionState.GetInt(Flag + key + ".Value", 0)); else PlayerPrefs.DeleteKey(key); }
        PlayerPrefs.Save(); string scene = SessionState.GetString(Flag + ".Scene", "Assets/Scenes/Induction.unity");
        EditorApplication.delayCall += () => EditorSceneManager.OpenScene(scene);
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void Bootstrap()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        typeof(TrialBackendClient).GetField("ValidationTransport", Fields)?.SetValue(null,
            (Func<string, string, Task<string>>)((a, b) => Task.FromResult("{\"admin\":false}")));
        typeof(TrialBackendClient).GetField("ValidationUid", Fields)?.SetValue(null, null);
        StageCompleteManager.SuppressPersistenceForValidation = true; StageSelectionState.SelectForEditorSpawn(6);
        SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded;
    }
    static TrialAccountQaRunner runner;
    static void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (!SessionState.GetBool(Flag, false)) return;
        // Vendor multiplayer demo counters require an online room; this fixture is deliberately offline.
        foreach (var component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (component != null && (component.GetType().Namespace ?? "").StartsWith("Photon.Pun.Demo.Cockpit", StringComparison.Ordinal)) component.enabled = false;
        if (Player.LocalInstance == null)
        {
            var avatar = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Multiplayer_Male"));
            avatar.GetComponent<MultiplayerAvatarSync>().enabled = false; avatar.GetComponent<PlayerEquipmentLoader>().enabled = false;
            foreach (var skin in avatar.GetComponentsInChildren<PlayerAnimator>(true)) skin.gameObject.SetActive(skin.name == "UNIFORM_MALE");
            avatar.GetComponent<CharacterController>().enabled = true;
            var spawner = UnityEngine.Object.FindFirstObjectByType<LevelSpawner>();
            var spawn = spawner != null ? (Transform)spawner.GetType().GetField("spawnPoint", Fields).GetValue(spawner) : null;
            if (spawn != null) avatar.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
        }
        if (runner == null)
        { runner = new GameObject("Offline Five Atmospheres QA").AddComponent<TrialAccountQaRunner>(); UnityEngine.Object.DontDestroyOnLoad(runner); runner.StartCoroutine(Guard(Run())); }
    }
    static IEnumerator Guard(IEnumerator work)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(work);
        while (stack.Count > 0)
        {
            object next;
            try { var top = stack.Peek(); if (!top.MoveNext()) { stack.Pop(); continue; } next = top.Current; }
            catch (Exception error) { File.AppendAllText(Report, "FAIL " + error + "\n"); Debug.LogException(error); EditorApplication.isPlaying = false; yield break; }
            if (next is IEnumerator nested) stack.Push(nested); else yield return next;
        }
        File.AppendAllText(Report, "ALL FIVE TRIAL ATMOSPHERE CHECKS PASSED\n"); SceneManager.sceneLoaded -= Loaded; EditorApplication.isPlaying = false;
    }
    static IEnumerator Run()
    {
        Application.runInBackground = true;
        foreach (string name in TrialAtmosphereSetup.Scenes)
        {
            Time.timeScale = 1; GameInput.Instance?.SetGameplayInputBlocked(false);
            StageSelectionState.SelectForEditorSpawn(TrialCatalog.ForScene(name).Number);
            if (SceneManager.GetActiveScene().name != name)
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/" + name + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return new WaitForSecondsRealtime(3.5f);
            float until = Time.realtimeSinceStartup + 30;
            while ((Player.LocalInstance == null || UnityEngine.Object.FindObjectsByType<StageHelpPage>(FindObjectsSortMode.None).Length == 0 ||
                TeleportManager.Instance != null && TeleportManager.Instance.IsTransitioning) && Time.realtimeSinceStartup < until) yield return null;
            Check(Player.LocalInstance != null, name + ": offline player ready");
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.isActiveAndEnabled).ToArray();
            Check(lights.Where(l => l.type != LightType.Directional).All(l => l.shadows == LightShadows.None), name + ": local accents use no shadow maps");
            Check(lights.Count(l => l.type == LightType.Directional && l.shadows != LightShadows.None) <= 1, name + ": at most one shadow-casting sun");
            Check(lights.Count(l => l.type != LightType.Directional) <= (name == "TruthTable" ? 25 : 16), name + ": bounded local-light budget");
            var sun = lights.First(l => l.type == LightType.Directional);
            if (name == "Induction")
            {
                Check(RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Flat && sun.intensity == .18f, "Induction: dim chamber fill is active");
                var flicker = UnityEngine.Object.FindFirstObjectByType<ChamberLightFlicker>();
                Check(flicker.AffectedLights.Length == 3 && flicker.AffectedLights.All(l => !l.name.Contains("Reading")), "Induction: decorative-light references remain intact");
                flicker.enabled = false; flicker.enabled = true;
                var original = flicker.AffectedLights.Select(l => l.intensity).ToArray();
                bool outage = false; var advance = flicker.GetType().GetMethod("Advance", Fields);
                for (int i = 0; i < 3000; i++) { advance.Invoke(flicker, new object[] { .05f }); outage |= flicker.AffectedLights.Any(l => l.intensity < .01f); }
                Check(!outage && flicker.CompletedEvents == 0 && flicker.AffectedLights.Select((l, i) => Mathf.Approximately(l.intensity, original[i])).All(v => v), "Induction: all fixtures stay fully lit without dimming or outages");
                flicker.enabled = false;
                Check(flicker.AffectedLights.Select((l, i) => Mathf.Abs(l.intensity - original[i]) < .001f).All(v => v), "Induction: disabling flicker restores lamp intensities"); flicker.enabled = true;
            }
            else if (name == "TruthTable")
                Check(Mathf.Abs(sun.transform.eulerAngles.x - 14) < .1f && Mathf.Abs(sun.intensity - .82f) < .01f && sun.color.r > sun.color.b && RenderSettings.skybox.name.Contains("Sunset"), "TruthTable: runtime controller preserves sunset and updated warm/cool balance");
            else Check(sun.transform.eulerAngles.x > 30 && sun.intensity >= 1, name + ": daylight retained");
            var helper = typeof(MobileTutorialInteractionChecks);
            var coach = (MonoBehaviour)helper.GetMethod("Prepare", Fields).Invoke(null, new object[] { name });
            yield return null;
            if (name == "Induction") Call(coach, "StartGuide");
            else if (name == "TruthTable") Call(coach, "BeginBookDemo");
            else if (name == "Functions and Relations") { Call(coach, "StartCameraGuide"); Call(coach, "Focus", UnityEngine.Object.FindFirstObjectByType<FunctionsRelationsStageManager>().BookTarget); }
            else Call(coach, "BeginBook");
            yield return new WaitForSecondsRealtime(1.8f); yield return Shot(name, "TutorialBook");
            if (name == "Induction") Call(coach, "StandStep");
            else if (name == "TruthTable") Call(coach, "BeginBoardSlotsDemo");
            else if (name == "RulesOfInference") Call(coach, "BeginBoardOverview");
            else if (name == "Setssss") Call(coach, "BeginBoard");
            else Call(coach, "BeginDomain");
            yield return new WaitForSecondsRealtime(1.8f); yield return Shot(name, "TutorialTask");
            yield return (IEnumerator)helper.GetMethod("Cleanup", Fields).Invoke(null, new object[] { coach, name });
            Time.timeScale = 1; GameInput.Instance.SetGameplayInputBlocked(false); Player.LocalInstance.ToggleControl(true);
            yield return new WaitForSecondsRealtime(1.5f);
            Check(Camera.main != null && !GameInput.Instance.GameplayInputBlocked, name + ": tutorial cleanup restores camera and input");
            foreach (int size in new[] { 6, 12, 3 })
            { BookMapMobileChecks.SetSize(size); yield return new WaitForSecondsRealtime(.35f); yield return Shot(name, "Gameplay_" + Screen.width + "x" + Screen.height); }
            if (name == "Induction")
            {
                var s = UnityEngine.Object.FindFirstObjectByType<InductionStageManager>();
                PrelimTeleportTransition.MovePlayerImmediately(Player.LocalInstance.gameObject, s.stations[0].position + Vector3.back * 2, Quaternion.identity);
                yield return null; s.boardView.ViewBoard(); yield return new WaitForSecondsRealtime(1.2f);
                Check(s.boardView.Viewing, "Induction: real board view opens"); yield return Shot(name, "Board");
                s.boardView.Back(); yield return new WaitForSecondsRealtime(1.2f);
                Check(!s.boardView.Viewing && s.State == InductionState.WaitingForBook, "Induction: board Back leaves challenge waiting for book");
            }
            else if (name == "Functions and Relations")
            {
                var s = UnityEngine.Object.FindFirstObjectByType<FunctionsRelationsStageManager>();
                Check(s.BoardView.OpenView(null), "Functions: board view opens without starting a question");
                yield return new WaitForSecondsRealtime(1.2f); yield return Shot(name, "Board");
                s.BoardView.Back(); yield return new WaitForSecondsRealtime(1.2f);
            }
            File.AppendAllText(Report, "SCENE PASSED " + name + "\n");
        }
    }
    static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Fields).Invoke(target, args);
    static IEnumerator Shot(string scene, string view)
    { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot("Library/AtmospherePlay_" + scene + "_" + view + ".png"); Check(Camera.main.nearClipPlane > 0, scene + ": " + view + " captured"); }
    static void Check(bool ok, string text) { if (!ok) throw new InvalidOperationException(text); File.AppendAllText(Report, "PASS " + text + "\n"); }
}
#endif
