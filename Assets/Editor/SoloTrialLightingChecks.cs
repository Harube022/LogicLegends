#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Editor-only light selection checks; never builds a player or bakes lighting.</summary>
public static class SoloTrialLightingChecks
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    [MenuItem("Logic Legends/Optimization/Lighting/Check ten trials (no bake)")]
    public static void Run() => Debug.Log(Validate());

    public static string Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            BuildPipeline.isBuildingPlayer || Lightmapping.isRunning)
            throw new InvalidOperationException("Finish the current Editor operation first.");
        var log = new StringBuilder("Editor checks only; Android GPU rendering and frame times remain unverified.\n");
        log.AppendLine(AndroidSoloBuildChecks.Validate());
        CheckSelection(log);
        CheckPools(log);
        for (int trial = 1; trial <= TrialCatalog.Count; trial++)
        {
            string path = "Assets/Scenes/" + TrialCatalog.Get(trial).Scene + ".unity";
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                var lights = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(true))
                    .Where(l => l.type == LightType.Point || l.type == LightType.Spot).ToArray();
                var root = new GameObject("Temporary lighting check");
                SceneManager.MoveGameObjectToScene(root, scene);
                var camera = root.AddComponent<Camera>(); camera.enabled = false;
                var gate = root.AddComponent<SoloSceneGraphics>();
                gate.Apply(new SoloSettingsData());
                Invoke(gate, "DiscoverLights");
                int checkedFixtures = 0;
                foreach (var light in lights.Where(l => l.isActiveAndEnabled && l.intensity > 0))
                {
                    var baked = light.bakingOutput;
                    if (baked.isBaked && baked.lightmapBakeType == LightmapBakeType.Baked) continue;
                    camera.cullingMask = light.cullingMask;
                    Vector3 focus = light.transform.position + Vector3.down * Mathf.Min(1f, light.range * .1f);
                    camera.transform.position = focus + Vector3.back * 3f;
                    camera.transform.LookAt(focus);
                    // Reset the previous pose's hysteresis for an independent fixture check.
                    foreach (var entry in Fixtures(gate)) SetSelected(entry, false);
                    Invoke(gate, "RankLights", camera, focus);
                    var selected = Selected(gate);
                    Check(selected.Length > 0 && selected.Length <= 4, scene.name + " has no nearby Low lighting or exceeds its four-light cap.");
                    Check(selected.Contains(light), scene.name + ": nearby fixture not selected on Low: " + light.name);
                    checkedFixtures++;
                }
                Check(checkedFixtures > 0, scene.name + " has no working enabled local fixture to check.");
                log.AppendLine("PASS " + scene.name + ": " + checkedFixtures + " enabled realtime fixtures selectable nearby on Low; four-light cap.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        Directory.CreateDirectory("output/mobile-performance");
        File.WriteAllText("output/mobile-performance/TrialLightingChecks.txt", log.ToString());
        return log.ToString();
    }

    static void CheckSelection(StringBuilder log)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("Temporary selection regression"); SceneManager.MoveGameObjectToScene(root, scene);
            var view = new GameObject("Independent camera"); view.transform.SetParent(root.transform);
            var camera = view.AddComponent<Camera>(); camera.enabled = false;
            var gate = root.AddComponent<SoloSceneGraphics>();
            var behind = Enumerable.Range(0, 8).Select(i => Lamp(root.transform, "Behind " + i, new Vector3(i * .05f, 0, -3), 1)).ToArray();
            var ahead = Enumerable.Range(0, 8).Select(i => Lamp(root.transform, "Ahead " + i, new Vector3((i - 4) * .5f, 0, 10), 4)).ToArray();
            foreach (int tier in new[] { 0, 1, 2 })
            {
                gate.Apply(new SoloSettingsData { graphicsQuality = 0, lightingQuality = tier });
                Invoke(gate, "DiscoverLights"); Invoke(gate, "RankLights", camera, new Vector3(0, 0, 10));
                var selected = Selected(gate);
                Check(selected.Length == new[] { 4, 6, 8 }[tier] && selected.All(ahead.Contains), "Offscreen lamps displaced the player's visible lights.");
                var ids = selected.Select(l => l.GetInstanceID()).OrderBy(id => id).ToArray();
                Invoke(gate, "RankLights", camera, new Vector3(0, 0, 10));
                Check(ids.SequenceEqual(Selected(gate).Select(l => l.GetInstanceID()).OrderBy(id => id)), "Standing still changes selected fixtures.");
                for (int angle = 0; angle < 360; angle += 15)
                {
                    camera.transform.rotation = Quaternion.Euler(0, angle, 0);
                    Invoke(gate, "RankLights", camera, new Vector3(0, 0, 10));
                    Check(ids.SequenceEqual(Selected(gate).Select(l => l.GetInstanceID()).OrderBy(id => id)), "Swiping the camera changes the lights affecting a stationary player.");
                }
                camera.transform.rotation = Quaternion.identity;
            }
            gate.Apply(new SoloSettingsData());
            Invoke(gate, "RankLights", camera, new Vector3(0, 0, 10)); Invoke(gate, "ApplyLights", .02f);
            Check(gate.ActiveLocalLights == 4, "Low did not apply four local lights.");
            var departing = Selected(gate);
            var nextRoom = Enumerable.Range(0, 8).Select(i => Lamp(root.transform, "Next room " + i, new Vector3((i - 4) * .5f, 0, 100), 4)).ToArray();
            camera.transform.position = new Vector3(0, 0, 90);
            Invoke(gate, "DiscoverLights"); Invoke(gate, "RankLights", camera, new Vector3(0, 0, 100));
            Invoke(gate, "ApplyLights", .02f);
            Check(departing.All(l => l.enabled && l.intensity > 0 && l.intensity < 10), "Outgoing light slots switch off abruptly instead of fading.");
            for (int frame = 0; frame < 30; frame++)
            {
                Invoke(gate, "ApplyLights", .02f);
                Check(gate.ActiveLocalLights <= 4, "A light transition exceeded the Low per-object budget.");
            }
            Check(nextRoom.Count(l => l.enabled) == 4 && departing.All(l => !l.enabled && Mathf.Approximately(l.intensity, 10)), "Light slots did not transfer or preserve authored brightness.");
            foreach (var light in nextRoom) light.gameObject.SetActive(false);
            camera.transform.position = Vector3.zero;
            foreach (var light in behind.Concat(ahead)) light.gameObject.SetActive(false);
            // The bulb is outside the picture, but its volume reaches the player.
            var side = Lamp(root.transform, "Offscreen bulb, visible pool", new Vector3(8, 0, 10), 9);
            Invoke(gate, "DiscoverLights"); Invoke(gate, "RankLights", camera, new Vector3(0, 0, 10));
            Check(Selected(gate).Contains(side), "A visible pool was dropped because its bulb is outside the camera view.");
            var late = Lamp(root.transform, "Late gameplay fixture", new Vector3(0, 1, 10), 4); late.enabled = false;
            Invoke(gate, "DiscoverLights"); late.enabled = true;
            Invoke(gate, "LateUpdate"); Invoke(gate, "RankLights", camera, new Vector3(0, 0, 10)); Invoke(gate, "ApplyLights", .02f);
            Check(late.enabled && late.intensity > 0, "A gameplay fixture activated after discovery stayed dark.");
            late.enabled = false;
            Invoke(gate, "LateUpdate"); Invoke(gate, "RankLights", camera, new Vector3(0, 0, 10)); Invoke(gate, "ApplyLights", .02f);
            Check(!late.enabled, "An intentionally disabled gameplay fixture was reactivated by the budget.");
            log.AppendLine("PASS visible/player-area priority, full camera rotation without player-light switching, offscreen bulb with visible pool, smooth bounded slot handover, independent 4/6/8 light caps.");
            log.AppendLine("PASS gameplay fixture activation/deactivation after discovery.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    static Light Lamp(Transform parent, string name, Vector3 position, float range)
    {
        var go = new GameObject(name); go.transform.SetParent(parent); go.transform.position = position;
        var light = go.AddComponent<Light>(); light.type = LightType.Point; light.range = range; light.intensity = 10;
        return light;
    }

    static void CheckPools(StringBuilder log)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var previousPipeline = QualitySettings.renderPipeline;
        var previousProbe = RenderSettings.ambientProbe;
        RenderTexture target = null; Texture2D image = null; Material material = null;
        try
        {
            var root = new GameObject("Temporary Low lighting pixel check"); SceneManager.MoveGameObjectToScene(root, scene);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.SetParent(root.transform);
            floor.transform.localScale = new Vector3(45, .05f, 45);
            var view = new GameObject("Preview camera"); view.transform.SetParent(root.transform);
            var camera = view.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
            camera.transform.position = new Vector3(0, 18, -22); camera.transform.LookAt(Vector3.zero);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.aspect = 1.5f;
            target = new RenderTexture(192, 128, 24); target.Create(); camera.targetTexture = target;
            image = new Texture2D(192, 128, TextureFormat.RGB24, false);
            var points = new[] { new Vector3(-5, 2, -4), new Vector3(5, 2, -4), new Vector3(-5, 2, 4), new Vector3(5, 2, 4) };
            var lights = points.Select((p, i) => Lamp(root.transform, "Pool " + i, p, 5)).ToArray();
            RenderSettings.ambientProbe = new UnityEngine.Rendering.SphericalHarmonicsL2();
            QualitySettings.renderPipeline = Resources.Load<UniversalRenderPipelineAsset>("Settings/SoloLighting0");
            foreach (string name in new[] { "Universal Render Pipeline/Lit", "Universal Render Pipeline/Simple Lit", "LogicLegends/TruthTable/GrassAndPaths", "LogicLegends/Inference/StylizedMeadowAndPaths" })
            {
                var shader = Shader.Find(name); Check(shader != null && shader.isSupported, "Missing supported trial shader " + name);
                material = new Material(shader); floor.GetComponent<Renderer>().sharedMaterial = material;
                foreach (var light in lights) light.enabled = true;
                var bright = ReadPools(camera, image, points);
                for (int i = 0; i < lights.Length; i++)
                {
                    lights[i].enabled = false;
                    float dark = ReadPools(camera, image, points)[i];
                    Check(bright[i] - dark > .015f, "Low lighting failed to render pool " + i + " on " + name + ": " + dark + " -> " + bright[i]);
                    lights[i].enabled = true;
                }
                UnityEngine.Object.DestroyImmediate(material); material = null;
                log.AppendLine("PASS Editor GPU: four independent local light pools on one large mesh using " + name + ", Low lighting.");
            }
        }
        finally
        {
            QualitySettings.renderPipeline = previousPipeline; RenderSettings.ambientProbe = previousProbe;
            EditorSceneManager.ClosePreviewScene(scene);
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
            if (material != null) UnityEngine.Object.DestroyImmediate(material);
        }
    }
    static float[] ReadPools(Camera camera, Texture2D image, Vector3[] points)
    {
        camera.Render(); var previous = RenderTexture.active;
        try { RenderTexture.active = camera.targetTexture; image.ReadPixels(new Rect(0, 0, image.width, image.height), 0, 0); image.Apply(); }
        finally { RenderTexture.active = previous; }
        return points.Select(point =>
        {
            point.y = .025f; Vector3 p = camera.WorldToViewportPoint(point);
            Check(p.z > 0 && p.x > 0 && p.x < 1 && p.y > 0 && p.y < 1, "Light pool is outside the pixel-check camera.");
            int x = Mathf.Clamp(Mathf.RoundToInt(p.x * image.width), 2, image.width - 3);
            int y = Mathf.Clamp(Mathf.RoundToInt(p.y * image.height), 2, image.height - 3);
            return image.GetPixels(x - 2, y - 2, 5, 5).Average(c => (c.r + c.g + c.b) / 3f);
        }).ToArray();
    }
    static IEnumerable Fixtures(SoloSceneGraphics gate) => (IEnumerable)typeof(SoloSceneGraphics).GetField("fixtures", Private).GetValue(gate);
    static void SetSelected(object entry, bool selected) => entry.GetType().GetField("selected").SetValue(entry, selected);
    static Light[] Selected(SoloSceneGraphics gate) => Fixtures(gate).Cast<object>()
        .Where(f => (bool)f.GetType().GetField("selected").GetValue(f))
        .Select(f => (Light)f.GetType().GetField("light").GetValue(f)).ToArray();
    static void Invoke(SoloSceneGraphics gate, string name, params object[] args) => typeof(SoloSceneGraphics).GetMethod(name, Private).Invoke(gate, args);
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
