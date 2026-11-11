#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Rendering-only comparison: actual saved TruthTable art, no player/account/gameplay controllers.
public static class SoloGraphicsPlayChecks
{
    const string Flag = "SoloGraphics.QA", Fixture = "Assets/Tests/AndroidGraphics/TruthTable.unity";
    static readonly string[] PreferenceKeys = { SoloGameSettings.PreferenceKey, AudioVolumeSettings.MusicPreference, AudioVolumeSettings.SoundFXPreference };
    [MenuItem("Logic Legends/Graphics/Profile isolated TruthTable rendering")]
    public static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Finish Play Mode first.");
        SessionState.SetString(Flag + ".PreviousStart", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(Flag + ".Settings", JsonUtility.ToJson(SoloGameSettings.Current));
        SessionState.SetInt(Flag + ".Size", BookMapMobileChecks.GetSize());
        SessionState.SetInt(Flag + ".SafeArea", (int)Crystal.SafeArea.Sim);
        SessionState.SetBool(Flag + ".Background", Application.runInBackground);
        SessionState.SetInt(Flag + ".FPS", Application.targetFrameRate);
        SessionState.SetInt(Flag + ".Vsync", QualitySettings.vSyncCount);
        foreach (string key in PreferenceKeys)
        {
            SessionState.SetBool(Flag + key, PlayerPrefs.HasKey(key));
            SessionState.SetString(Flag + key + ".Value", key == SoloGameSettings.PreferenceKey ? PlayerPrefs.GetString(key) : PlayerPrefs.GetFloat(key).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        Directory.CreateDirectory("Assets/Tests/AndroidGraphics");
        File.Copy("Assets/Scenes/TruthTable.unity", Fixture, true);
        AssetDatabase.ImportAsset(Fixture, ImportAssetOptions.ForceSynchronousImport);
        var original = SceneManager.GetActiveScene();
        var preview = EditorSceneManager.OpenScene(Fixture, OpenSceneMode.Additive);
        try
        {
            var roots = preview.GetRootGameObjects();
            var spawn = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "Truth_Table_SpawnPoint");
            if (spawn == null) throw new Exception("TruthTable's actual player spawn is missing.");
            Vector3 position = spawn != null ? spawn.position : Vector3.zero;
            foreach (var root in roots)
            {
                // Remove scripts, not only enabled flags: disabled components still receive Awake.
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true).Reverse())
                    if (behaviour != null && !(behaviour is TruthTableDuskLighting)) UnityEngine.Object.DestroyImmediate(behaviour);
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true).Reverse())
                    if (behaviour != null && !(behaviour is TruthTableDuskLighting)) UnityEngine.Object.DestroyImmediate(behaviour);
                foreach (var camera in root.GetComponentsInChildren<Camera>(true)) { camera.enabled = false; camera.tag = "Untagged"; }
                foreach (var audio in root.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
                foreach (var body in root.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true).Reverse())
                    if (canvas != null) UnityEngine.Object.DestroyImmediate(canvas.gameObject);
            }
            var view = new GameObject("Graphics QA camera", typeof(Camera)).GetComponent<Camera>();
            SceneManager.MoveGameObjectToScene(view.gameObject, preview); view.tag = "MainCamera";
            view.transform.position = position + new Vector3(-9, 10, -13); view.transform.LookAt(position + Vector3.forward * 6);
            view.nearClipPlane = .1f; view.farClipPlane = 240; view.fieldOfView = 55;
            view.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            if (!EditorSceneManager.SaveScene(preview, Fixture)) throw new Exception("Could not save isolated graphics fixture; Play Mode was not started.");
        }
        finally { EditorSceneManager.CloseScene(preview, true); SceneManager.SetActiveScene(original); }
        SessionState.SetBool(Flag, true);
        Directory.CreateDirectory(SoloGraphicsChecks.Folder);
        File.WriteAllText(SoloGraphicsChecks.Folder + "/EditorProfile.txt", "Rendering-only isolated fixture. Real TruthTable art and lighting; no NPCs, gameplay, account or reward controllers. Editor measurements are not Android frame rates.\n");
        var fixture = AssetDatabase.LoadAssetAtPath<SceneAsset>(Fixture);
        if (fixture == null) throw new Exception("Isolated fixture was not imported; Play Mode was not started.");
        EditorSceneManager.playModeStartScene = fixture;
        EditorApplication.isPlaying = true;
    }
    [InitializeOnLoadMethod]
    static void Hook() { EditorApplication.playModeStateChanged -= Restore; EditorApplication.playModeStateChanged += Restore; }
    static void Restore(PlayModeStateChange mode)
    {
        if (mode != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Flag, false)) return;
        SessionState.SetBool(Flag, false);
        SoloGameSettings.Apply(JsonUtility.FromJson<SoloSettingsData>(SessionState.GetString(Flag + ".Settings", "{}")));
        foreach (string key in PreferenceKeys)
        {
            if (!SessionState.GetBool(Flag + key, false)) PlayerPrefs.DeleteKey(key);
            else if (key == SoloGameSettings.PreferenceKey) PlayerPrefs.SetString(key, SessionState.GetString(Flag + key + ".Value", ""));
            else PlayerPrefs.SetFloat(key, float.Parse(SessionState.GetString(Flag + key + ".Value", "1"), System.Globalization.CultureInfo.InvariantCulture));
        }
        PlayerPrefs.Save(); BookMapMobileChecks.SetSize(SessionState.GetInt(Flag + ".Size", 0));
        Crystal.SafeArea.Sim = (Crystal.SafeArea.SimDevice)SessionState.GetInt(Flag + ".SafeArea", 0);
        Application.runInBackground = SessionState.GetBool(Flag + ".Background", false);
        Application.targetFrameRate = SessionState.GetInt(Flag + ".FPS", -1);
        QualitySettings.vSyncCount = SessionState.GetInt(Flag + ".Vsync", 0);
        string path = SessionState.GetString(Flag + ".PreviousStart", "");
        EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Isolate()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        // Defense in depth: even an accidentally retained component cannot contact commerce.
        var type = typeof(TrialBackendClient); var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        type.GetField("ValidationTransport", flags)?.SetValue(null, (Func<string, string, System.Threading.Tasks.Task<string>>)((a, b) => System.Threading.Tasks.Task.FromResult("{}")));
        type.GetField("ValidationUid", flags)?.SetValue(null, null);
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (SessionState.GetBool(Flag, false) && SceneManager.GetActiveScene().path == Fixture)
        {
            var runner = new GameObject("Graphics comparison").AddComponent<SoloGraphicsQaRunner>();
            UnityEngine.Object.DontDestroyOnLoad(runner); runner.StartCoroutine(Guard(Run()));
        }
    }
    static IEnumerator Guard(IEnumerator work)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(work);
        while (stack.Count > 0)
        {
            object next = null; bool more;
            try { more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current; }
            catch (Exception error)
            {
                File.AppendAllText(SoloGraphicsChecks.Folder + "/EditorProfile.txt", "FAIL " + error + "\n");
                Debug.LogException(error); EditorApplication.isPlaying = false; yield break;
            }
            if (!more) { stack.Pop(); continue; }
            if (next is IEnumerator nested) stack.Push(nested); else yield return next;
        }
    }
    static IEnumerator Run()
    {
        Application.runInBackground = true;
        BookMapMobileChecks.SetSize(2);
        var data = SoloGameSettings.Current.Copy(); data.graphicsQuality = data.lightingQuality = data.shadowQuality = 0; data.motionBlur = false;
        SoloGameSettings.Apply(data);
        double ready = Time.realtimeSinceStartupAsDouble + 4;
        while (UnityEngine.Object.FindFirstObjectByType<SoloTrialVisuals>() == null && Time.realtimeSinceStartupAsDouble < ready) yield return null;
        var host = UnityEngine.Object.FindFirstObjectByType<SoloTrialVisuals>();
        if (host == null) { File.AppendAllText(SoloGraphicsChecks.Folder + "/EditorProfile.txt", "FAIL settings host not installed; scene=" + SceneManager.GetActiveScene().name + "; settings=" + UnityEngine.Object.FindObjectsByType<SoloSettingsRuntime>(FindObjectsSortMode.None).Length + "\n"); EditorApplication.isPlaying = false; yield break; }
        var gameObject = host.gameObject;
        UnityEngine.Object.Destroy(host); UnityEngine.Object.Destroy(gameObject.GetComponent<SoloSceneGraphics>());
        yield return null;
        yield return Sample("Before_Low_legacy");
        var gate = gameObject.AddComponent<SoloSceneGraphics>(); var visuals = gameObject.AddComponent<SoloTrialVisuals>();
        // Explicit Android mesh policy in this Editor-only rendering fixture.
        // Normal desktop gameplay still uses the untouched source models.
        var mobileCatalog = Resources.Load<SoloMobileMeshes>("Settings/SoloMobileMeshes");
        var originalMeshes = UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
            .Where(f => f.sharedMesh != null).ToDictionary(f => f, f => f.sharedMesh);
        for (int tier = 0; tier < 3; tier++)
        {
            data.graphicsQuality = data.lightingQuality = tier; data.shadowQuality = tier == 0 ? 0 : tier;
            foreach (var pair in originalMeshes) if (pair.Key != null) pair.Key.sharedMesh = mobileCatalog.Resolve(pair.Value, tier);
            SoloGameSettings.Apply(data); visuals.Apply(data); gate.Apply(data);
            yield return Sample("After_" + new[] { "Low", "Medium", "High" }[tier]);
            if (gate.ActiveLocalLights > SoloGraphicsBudget.LightLimit(data)) File.AppendAllText(SoloGraphicsChecks.Folder + "/EditorProfile.txt", "FAIL local light cap\n");
            if (Camera.main.GetUniversalAdditionalCameraData().renderPostProcessing && tier == 0) File.AppendAllText(SoloGraphicsChecks.Folder + "/EditorProfile.txt", "FAIL Low post processing\n");
        }
        // A real scene reload exercises the shared install/reapply path used by retries and navigation.
        int previousHandle = SceneManager.GetActiveScene().handle;
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(Fixture, new LoadSceneParameters(LoadSceneMode.Single));
        yield return null; yield return null; yield return null; yield return new WaitForSecondsRealtime(.6f);
        var reloaded = UnityEngine.Object.FindFirstObjectByType<SoloSceneGraphics>();
        if (SceneManager.GetActiveScene().handle == previousHandle || reloaded == null || SoloGameSettings.Current.graphicsQuality != 2 || reloaded.ActiveLocalLights > SoloGraphicsBudget.LightLimit(SoloGameSettings.Current))
            File.AppendAllText(SoloGraphicsChecks.Folder + "/EditorProfile.txt", "FAIL quality reapply after scene reload\n");
        else File.AppendAllText(SoloGraphicsChecks.Folder + "/EditorProfile.txt", "PASS quality reapply after scene reload\n");
        typeof(SoloGameSettings).GetField("current", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).SetValue(null, null);
        File.AppendAllText(SoloGraphicsChecks.Folder + "/EditorProfile.txt", (SoloGameSettings.Current.graphicsQuality == 2 ? "PASS" : "FAIL") + " saved quality read after cache reset\n");
        yield return RoutingChecks();
        File.AppendAllText(SoloGraphicsChecks.Folder + "/EditorProfile.txt", "COMPLETE; gameplay/NPC CPU and physical Android remain separate tests.\n");
        EditorApplication.isPlaying = false;
    }
    static IEnumerator RoutingChecks()
    {
        // Named empty fixtures test shared scene-routing/quality ownership, not ten gameplay walkthroughs.
        for (int trial = 1; trial <= 10; trial++)
        {
            var previous = SceneManager.GetActiveScene();
            foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) camera.enabled = false;
            var scene = SceneManager.CreateScene(TrialCatalog.Get(trial).Scene);
            var canvas = new GameObject("Canvas 1", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(canvas, scene);
            var view = new GameObject("Quality route camera", typeof(Camera)).GetComponent<Camera>();
            SceneManager.MoveGameObjectToScene(view.gameObject, scene); view.tag = "MainCamera";
            for (int i = 0; i < 30; i++)
            {
                var fixture = new GameObject("Quality fixture " + i, typeof(Light));
                SceneManager.MoveGameObjectToScene(fixture, scene); fixture.transform.position = new Vector3(i % 5, 2, i / 5);
                var light = fixture.GetComponent<Light>(); light.type = LightType.Point; light.range = 20;
                fixture.AddComponent<SoloGraphicsQaLightOverride>();
            }
            SceneManager.SetActiveScene(scene); yield return SceneManager.UnloadSceneAsync(previous);
            yield return null; yield return null; yield return new WaitForSecondsRealtime(.4f);
            var gate = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SoloSceneGraphics>()).FirstOrDefault();
            if (gate == null) throw new Exception("Trial " + trial + " graphics not installed after active-scene transition.");
            for (int tier = 0; tier < 3; tier++)
            {
                var data = SoloGameSettings.Current.Copy(); data.graphicsQuality = data.lightingQuality = tier;
                data.shadowQuality = 3; data.motionBlur = true;
                SoloGameSettings.Apply(data); yield return new WaitForSecondsRealtime(.35f);
                if (gate.ActiveLocalLights != new[] { 4, 6, 8 }[tier]) throw new Exception("Trial " + trial + " tier " + tier + " light budget mismatch.");
                if (tier == 0 && view.GetUniversalAdditionalCameraData().renderPostProcessing) throw new Exception("Trial " + trial + " Low post-processing override.");
            }
            File.AppendAllText(SoloGraphicsChecks.Folder + "/EditorProfile.txt", "PASS Trial " + trial + " named fixture: Low/Medium/High, active-scene reapply, scene-script light override\n");
        }
    }
    static IEnumerator Sample(string name)
    {
        foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)) canvas.enabled = false;
        // Fixed view and resolution, warmed shader variants; no user input drives this comparison.
        double until = Time.realtimeSinceStartupAsDouble + 2;
        while (Time.realtimeSinceStartupAsDouble < until) yield return null;
        using (var draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1))
        using (var passes = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 1))
        using (var triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 1))
        using (var main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1))
        {
            double draw = 0, pass = 0, tri = 0, cpu = 0;
            int frames = 90;
            for (int i = 0; i < frames; i++) { yield return null; draw += draws.LastValue; pass += passes.LastValue; tri += triangles.LastValue; cpu += main.LastValue / 1000000.0; }
            int lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Count(l => l.isActiveAndEnabled && l.type != LightType.Directional);
            var gate = UnityEngine.Object.FindFirstObjectByType<SoloSceneGraphics>();
            string row = name + " | " + Screen.width + "x" + Screen.height + " | draw/frame=" + (draw / frames).ToString("F1") + " | setpass/frame=" + (pass / frames).ToString("F1") + " | triangles/frame=" + (tri / frames).ToString("F0") + " | main-thread-ms(includes wait/editor)=" + (cpu / frames).ToString("F2") + " | localLights=" + lights + " | grass=" + (gate != null ? gate.VisibleGrass + "/" + gate.ManagedGrass : "ungated");
            File.AppendAllText(SoloGraphicsChecks.Folder + "/EditorProfile.txt", row + "\n");
        }
        Directory.CreateDirectory("Library/AndroidGraphicsAudit");
        ScreenCapture.CaptureScreenshot("Library/AndroidGraphicsAudit/" + name + ".png");
        yield return null; yield return null;
    }
}
public sealed class SoloGraphicsQaRunner : MonoBehaviour { }
public sealed class SoloGraphicsQaLightOverride : MonoBehaviour { void Update() { GetComponent<Light>().enabled = true; } }
#endif
