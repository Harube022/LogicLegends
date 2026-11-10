#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Actual saved scenes and the production loader; inert backend, no answering or reward events.
public static class AndroidLoadingChecks
{
    const string Flag = "AndroidLoadingChecks.Active", Report = "output/mobile-performance/LoadingEditorResults.txt";
    static readonly string[] Histories={"FunctionsRelations.LastVariant.","LogicLegends.Induction.LastVariant.","LogicLegends.ProofsLibrary.LastVariant.","LogicLegends.Graph.LastVariant.","LogicLegends.Trees.LastVariant."};
    static readonly string[] Scenes = { "PRELIM", "TruthTable", "RulesOfInference", "Formal vs. Informal Proofs", "Direct&Indirect", "Induction", "Setssss", "Functions and Relations", "Graph", "Trees", "TruthTable", "RulesOfInference" };
    public static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        SessionState.SetString(Flag + ".Start", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Flag + ".Background", Application.runInBackground);
        SessionState.SetBool(Flag, true);
        foreach(var prefix in Histories) for(int i=0;i<5;i++)
        {
            string key=prefix+i;SessionState.SetBool(Flag+key,PlayerPrefs.HasKey(key));SessionState.SetInt(Flag+key+".Value",PlayerPrefs.GetInt(key));
        }
        Directory.CreateDirectory("output/mobile-performance");
        File.WriteAllText(Report, "EDITOR ONLY. Actual saved scenes, offline backend, no puzzle answers or rewards. Times include Editor overhead; not Android metrics.\n");
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/PRELIM.unity");
        EditorApplication.isPlaying = true;
    }
    [InitializeOnLoadMethod] static void Hook()
    {
        EditorApplication.playModeStateChanged -= Restore;
        EditorApplication.playModeStateChanged += Restore;
    }
    static void Restore(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Flag, false)) return;
        SessionState.SetBool(Flag, false); SceneManager.sceneLoaded -= Loaded;
        foreach(var prefix in Histories)for(int i=0;i<5;i++)
        {
            string key=prefix+i;if(SessionState.GetBool(Flag+key,false))PlayerPrefs.SetInt(key,SessionState.GetInt(Flag+key+".Value",0));else PlayerPrefs.DeleteKey(key);
        }
        PlayerPrefs.Save();
        Application.runInBackground = SessionState.GetBool(Flag + ".Background", false);
        var path = SessionState.GetString(Flag + ".Start", "");
        EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void Isolate()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        typeof(TrialBackendClient).GetField("ValidationTransport", flags).SetValue(null,
            (Func<string,string,System.Threading.Tasks.Task<string>>)((a,b) => System.Threading.Tasks.Task.FromResult("{}")));
        typeof(TrialBackendClient).GetField("ValidationUid", flags).SetValue(null, null);
        typeof(TrialBackendClient).GetField("ValidationEmail", flags).SetValue(null, null);
        Application.runInBackground = true;
        SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded;
        StageSelectionState.SelectForEditorSpawn(1);
    }
    static void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (!SessionState.GetBool(Flag, false)) return;
        if (Player.LocalInstance == null)
        {
            var staging = new GameObject("Offline test avatar staging"); staging.SetActive(false);
            var avatar = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Multiplayer_Male"), staging.transform);
            avatar.GetComponent<MultiplayerAvatarSync>().enabled = false;
            avatar.GetComponent<PlayerEquipmentLoader>().enabled = false;
            foreach (var skin in avatar.GetComponentsInChildren<PlayerAnimator>(true)) skin.gameObject.SetActive(skin.name == "UNIFORM_MALE");
            avatar.transform.SetParent(null); avatar.SetActive(true); UnityEngine.Object.Destroy(staging);
            avatar.GetComponent<CharacterController>().enabled = true;
            var spawn = GameObject.Find("Proofs Player Spawn") ?? GameObject.Find("Trial Player Spawn") ?? GameObject.Find("Truth_Table_SpawnPoint");
            if (spawn != null) avatar.transform.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);
        }
        if (UnityEngine.Object.FindFirstObjectByType<AndroidLoadingQaRunner>() != null) return;
        var runner = new GameObject("Offline loading checks").AddComponent<AndroidLoadingQaRunner>();
        UnityEngine.Object.DontDestroyOnLoad(runner); runner.StartCoroutine(Guard(Run()));
    }
    static IEnumerator Guard(IEnumerator work)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(work);
        while (stack.Count > 0)
        {
            bool more; object next = null;
            try { more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current; }
            catch (Exception error)
            { File.AppendAllText(Report, "FAIL " + error + "\n"); EditorApplication.isPlaying = false; yield break; }
            if (!more) { stack.Pop(); continue; }
            if (next is IEnumerator) stack.Push((IEnumerator)next); else yield return next;
        }
    }
    static IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(2);
        foreach (var destination in Scenes)
        {
            StageSelectionState.SelectForEditorSpawn(TrialCatalog.ForScene(destination).Number);
            var watch = Stopwatch.StartNew(); double deadline = Time.realtimeSinceStartupAsDouble + 60;
            if (!SoloStageLoader.Load(destination)) throw new Exception("Load rejected: " + destination);
            if (SoloStageLoader.Load(destination)) throw new Exception("Duplicate load accepted.");
            bool coveredActivation = false;
            while (SoloStageLoader.IsLoading && Time.realtimeSinceStartupAsDouble < deadline)
            {
                var scene = SceneManager.GetActiveScene();
                if (scene.name == destination)
                {
                    foreach (var screen in UnityEngine.Object.FindObjectsByType<SharedLoadingScreen>(FindObjectsSortMode.None))
                        if (screen.IsVisible && screen.gameObject.scene.name == "DontDestroyOnLoad") coveredActivation = true;
                }
                yield return null;
            }
            if (SoloStageLoader.IsLoading) throw new Exception("Readiness timed out: " + destination);
            watch.Stop();
            if (!SoloSettingsRuntime.IsReady(SceneManager.GetActiveScene())) throw new Exception("Revealed before materials ready.");
            if (!coveredActivation) throw new Exception("Artwork did not survive activation.");
            File.AppendAllText(Report, "PASS " + destination + " ready+reveal=" + watch.Elapsed.TotalSeconds.ToString("F2") +
                "s; total allocated MiB=" + (UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576.0).ToString("F1") +
                "; duplicate request rejected; activation covered; material/settings readiness confirmed.\n");
            yield return new WaitForSecondsRealtime(.3f);
        }
        File.AppendAllText(Report, "COMPLETE. All ten trial routes + TruthTable/Inference re-entry. Gameplay solving/device touch not tested here.\n");
        EditorApplication.isPlaying = false;
    }
}
public sealed class AndroidLoadingQaRunner : MonoBehaviour { }
#endif
