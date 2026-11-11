#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

// Bakes an isolated copy of real chamber art. Never bakes or saves an open gameplay scene.
public static class AndroidChamberExperiment
{
    const string Folder = "Assets/Tests/AndroidOptimization";
    static Scene original, sample;
    static SceneSetup[] originalSetup;
    static double started;
    static int phase;
    static string report;
    public static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning || StaticOcclusionCulling.isRunning)
            throw new InvalidOperationException("Finish the current operation first.");
        original = SceneManager.GetActiveScene();
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("A scene has unsaved changes; leave it open and do not run the bake pilot.");
        originalSetup = EditorSceneManager.GetSceneManagerSetup();
        Directory.CreateDirectory(Folder);
        const string copy = Folder + "/ChamberExperiment.unity";
        if (File.Exists(copy)) throw new InvalidOperationException("A retained pilot already exists. Inspect it or use MeasureRetainedPilot; do not duplicate its large generated UV mesh.");
        File.Copy("Assets/Scenes/Formal vs. Informal Proofs.unity", copy, true);
        AssetDatabase.ImportAsset(copy);
        sample = EditorSceneManager.OpenScene(copy, OpenSceneMode.Single);
        SceneManager.SetActiveScene(sample);
        report = "Isolated Formal vs. Informal chamber art; Editor D3D12. No gameplay/accounts/rewards.\n";
        try
        {
            // Disable all code before removing dependencies; the source scene remains untouched.
            var behaviours = new List<MonoBehaviour>();
            foreach (var root in sample.GetRootGameObjects()) behaviours.AddRange(root.GetComponentsInChildren<MonoBehaviour>(true));
            foreach (var behaviour in behaviours) if (behaviour != null) behaviour.enabled = false;
            foreach (var root in sample.GetRootGameObjects())
            {
                foreach (var camera in root.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;
                foreach (var audio in root.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
                foreach (var listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
                foreach (var light in root.GetComponentsInChildren<Light>(true))
                    if (light.isActiveAndEnabled && light.type != LightType.Directional) light.lightmapBakeType = LightmapBakeType.Baked;
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer = filter.GetComponent<MeshRenderer>(); var mesh = filter.sharedMesh;
                    if (mesh == null || renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || filter.GetComponentInParent<TMPro.TMP_Text>() != null) continue;
                    // Only stable architecture/decorations. Book pages, scrolls and puzzle targets stay dynamic.
                    bool architecture = renderer.bounds.size.magnitude > 20 || filter.name.Contains("Bookshelf");
                    if (!architecture) continue;
                    if (mesh.uv2.Length == 0)
                    {
                        mesh = UnityEngine.Object.Instantiate(mesh);
                        mesh.name += " Bake pilot UV2";
                        Unwrapping.GenerateSecondaryUVSet(mesh);
                        string path = Folder + "/Mesh_" + filter.GetInstanceID() + ".asset";
                        AssetDatabase.CreateAsset(mesh, path); filter.sharedMesh = mesh;
                    }
                    GameObjectUtility.SetStaticEditorFlags(filter.gameObject, StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
                }
            }
            var settings = new LightingSettings { name = "Mobile Chamber Bake Pilot", bakedGI = true, realtimeGI = false,
                lightmapper = LightingSettings.Lightmapper.ProgressiveCPU, lightmapResolution = 1,
                lightmapMaxSize = 512, directSampleCount = 16, indirectSampleCount = 32,
                environmentSampleCount = 16, maxBounces = 1, directionalityMode = LightmapsMode.NonDirectional };
            AssetDatabase.CreateAsset(settings, Folder + "/PilotLighting.asset");
            Lightmapping.lightingSettings = settings;
            EditorSceneManager.SaveScene(sample);
            phase = 0; started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            StaticOcclusionCulling.smallestOccluder = 3; StaticOcclusionCulling.smallestHole = .5f;
            StaticOcclusionCulling.backfaceThreshold = 100;
            StaticOcclusionCulling.GenerateInBackground();
            Write("Occlusion bake started.");
        }
        catch { Restore(); throw; }
    }
    static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 150)
            {
                if (Lightmapping.isRunning) Lightmapping.Cancel();
                if (StaticOcclusionCulling.isRunning) StaticOcclusionCulling.Cancel();
                Write("Pilot exceeded 150 seconds; cancelled without modifying gameplay scenes."); Restore(); return;
            }
            if (phase == 0)
            {
                if (StaticOcclusionCulling.isRunning) return;
                report += "Occlusion data bytes=" + StaticOcclusionCulling.umbraDataSize + "\n";
                MeasureViews();
                phase = 1; started = EditorApplication.timeSinceStartup;
                if (!Lightmapping.BakeAsync()) { Write("Light bake could not start."); Restore(); }
                else Write("Light bake started; source has no usable board/floor lightmaps before this pilot.");
            }
            else if (!Lightmapping.isRunning)
            {
                long bytes = 0;
                foreach (var map in LightmapSettings.lightmaps)
                    if (map.lightmapColor != null) bytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(map.lightmapColor);
                report += "Baked maps=" + LightmapSettings.lightmaps.Length + " runtime color-map bytes=" + bytes + "\n";
                EditorSceneManager.SaveScene(sample); Write("COMPLETE. Pilot retained for inspection; not assigned to gameplay scenes."); Restore();
            }
        }
        catch (Exception ex) { Write("FAIL " + ex); Restore(); }
    }
    static void MeasureViews()
    {
        var go = new GameObject("Pilot camera"); SceneManager.MoveGameObjectToScene(go, sample);
        var camera = go.AddComponent<Camera>(); camera.enabled = false; camera.farClipPlane = 150; camera.nearClipPlane = .1f;
        camera.targetTexture = new RenderTexture(640, 360, 24);
        var poses = new[] { new Vector3(0, 4, -17), new Vector3(-14, 4, -5), new Vector3(0, 4, 12) };
        foreach (var pose in poses)
        {
            camera.transform.position = pose; camera.transform.LookAt(new Vector3(0, 4, 0));
            foreach (bool enabled in new[] { false, true })
            {
                camera.useOcclusionCulling = enabled;
                for (int i = 0; i < 3; i++) camera.Render();
                var watch = Stopwatch.StartNew();
                for (int i = 0; i < 12; i++) camera.Render();
                watch.Stop();
                report += "pose=" + pose + " occlusion=" + enabled + " Camera.Render CPU submission/wait ms=" + (watch.Elapsed.TotalMilliseconds / 12).ToString("F3") +
                    " triangles(last Editor frame; may include other cameras)=" + UnityStats.triangles + "\n";
            }
        }
        var target=camera.targetTexture;camera.targetTexture=null;target.Release();
        UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(go);
    }
    public static void MeasureRetainedPilot()
    {
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Preserve unsaved scene first.");
        originalSetup = EditorSceneManager.GetSceneManagerSetup();
        sample = EditorSceneManager.OpenScene(Folder + "/ChamberExperiment.unity", OpenSceneMode.Single);
        try
        {
            report = "Retained baked chamber reopened so its saved occlusion data is loaded. Editor D3D12, 640x360.\n";
            report += "Loaded occlusion bytes=" + StaticOcclusionCulling.umbraDataSize + " asset bytes=" + new FileInfo(Folder + "/ChamberExperiment/OcclusionCullingData.asset").Length + "\n";
            MeasureViews();
            File.WriteAllText("output/mobile-performance/OcclusionRetest.txt", report);
        }
        finally { Restore(); }
    }
    static void Write(string message)
    {
        Directory.CreateDirectory("output/mobile-performance");
        File.WriteAllText("output/mobile-performance/ChamberBakePilot.txt", report + message + "\n");
    }
    static void Restore()
    {
        EditorApplication.update -= Tick;
        if (sample.IsValid() && sample.isLoaded) EditorSceneManager.CloseScene(sample, true);
        if (originalSetup != null) { EditorSceneManager.RestoreSceneManagerSetup(originalSetup); originalSetup = null; }
    }
}
#endif
