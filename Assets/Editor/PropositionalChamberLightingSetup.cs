#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scene-instance lighting changes; retains the user's fixtures, models, and collisions.</summary>
public static class PropositionalChamberLightingSetup
{
    public const string Folder = "Assets/Art/PropositionalChamberLighting";
    [Serializable] public sealed class Report
    {
        public string[] chambers;
        public int torches, boardSurfaces, pageSurfaces, newLights;
        public float torchIntensity, torchRange, sunlightIntensity;
        public Color ambient;
        public bool transformsCollidersGameplayAndOriginalEnvironmentPreserved;
    }
    public static List<TopicChallenge> Rooms() => (List<TopicChallenge>)typeof(QuizManager).GetField("challenges", BindingFlags.Instance | BindingFlags.NonPublic)
        .GetValue(UnityEngine.Object.FindFirstObjectByType<QuizManager>(FindObjectsInactive.Include));
    [MenuItem("Logic Legends/Propositional Logic/Apply Dim Chamber Lighting")]
    public static void ApplyMenu() { Apply(); }
    public static Report Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.name != "PRELIM" || scene.isDirty || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Open the saved PRELIM scene in Edit Mode.");
        Directory.CreateDirectory(Folder);
        if (!File.Exists("Library/PrelimChambersBefore.unity")) EditorSceneManager.SaveScene(scene, "Library/PrelimChambersBefore.unity", true);
        var group = GameObject.Find("Propositional Logic"); var rooms = Rooms();
        if (rooms.Count != 5) throw new InvalidOperationException("Five original chambers are required.");
        string before = Invariants(scene);
        var profile = group.GetComponent<PropositionalChamberLighting>();
        if (profile == null) profile = Undo.AddComponent<PropositionalChamberLighting>(group);
        Undo.RecordObject(profile, "Dim five chambers");
        profile.sunlight = RenderSettings.sun;
        profile.chamberBounds = rooms.Select(room => {
            var bounds = room.roomChoiceCanvas.transform.parent.Find("S1L1_FLOOR").GetComponent<Renderer>().bounds;
            bounds.Expand(new Vector3(12, 22, 12)); return bounds;
        }).ToArray();
        profile.chamberNames = rooms.Select(room => room.roomChoiceCanvas.transform.parent.name).ToArray();
        profile.torches = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(light => light.type == LightType.Point && light.name == "TorchLight" &&
                rooms.Any(room => Vector2.Distance(XZ(light.transform.position), XZ(room.topicSpawnPoint.position)) < 40)).ToArray();
        if (profile.torches.Length != 25) throw new InvalidOperationException("Expected five existing torch lights per chamber.");
        foreach (var light in profile.torches)
        {
            Undo.RecordObject(light, "Balance existing torch light");
            light.intensity = 23; light.range = 28; light.color = new Color(1, .76f, .49f);
            light.shadows = LightShadows.None;
            PrefabUtility.RecordPrefabInstancePropertyModifications(light); EditorUtility.SetDirty(light);
        }
        int boardSurfaces = 0, pageSurfaces = 0;
        var materials = new Dictionary<string, Material>();
        foreach (var room in rooms)
        {
            var board = room.hintBoard.GetComponent<Renderer>(); var slots = board.sharedMaterials;
            // Lift only the two neutral board surfaces; its dark lettering stays dark.
            for (int slot = 0; slot < 2; slot++) { slots[slot] = ReadingMaterial(slots[slot], "HintBoard", .6f, materials); boardSurfaces++; }
            SetMaterials(board, slots);
            foreach (var page in room.roomChoiceCanvas.transform.parent.GetComponentsInChildren<MeshRenderer>(true)
                .Where(renderer => renderer.name.ToLowerInvariant().Contains("page")))
            {
                var pages = page.sharedMaterials;
                for (int slot = 0; slot < pages.Length; slot++) { pages[slot] = ReadingMaterial(pages[slot], "BookPage", .22f, materials); pageSurfaces++; }
                SetMaterials(page, pages);
            }
        }
        EditorUtility.SetDirty(profile);
        bool preserved = before == Invariants(scene);
        if (!preserved) throw new InvalidOperationException("A transform, collider, gameplay reference, or original environment setting changed.");
        var report = new Report { chambers = profile.chamberNames, torches = profile.torches.Length, boardSurfaces = boardSurfaces,
            pageSurfaces = pageSurfaces, newLights = 0, torchIntensity = 23, torchRange = 28,
            sunlightIntensity = profile.sunlightIntensity, ambient = profile.ambientColor,
            transformsCollidersGameplayAndOriginalEnvironmentPreserved = preserved };
        File.WriteAllText(Folder + "/ChamberLightingReport.json", JsonUtility.ToJson(report, true));
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        return report;
    }
    static Vector2 XZ(Vector3 p) => new Vector2(p.x, p.z);
    static void SetMaterials(Renderer renderer, Material[] materials)
    {
        Undo.RecordObject(renderer, "Preserve reading contrast"); renderer.sharedMaterials = materials;
        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer); EditorUtility.SetDirty(renderer);
    }
    static Material ReadingMaterial(Material source, string role, float strength, Dictionary<string, Material> cache)
    {
        if (source == null || !source.HasProperty("_EmissionColor") || !source.HasProperty("_BaseColor")) return source;
        // Reapplying authoring must reuse the scoped derivative.
        if (AssetDatabase.GetAssetPath(source).StartsWith(Folder + "/", StringComparison.Ordinal)) return source;
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long id);
        string key = role + "_" + guid + "_" + id;
        if (cache.TryGetValue(key, out var existing)) return existing;
        string name = role + "_" + source.name.Replace(' ', '_'); string path = Folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(source) { name = name }; AssetDatabase.CreateAsset(material, path); }
        material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", source.GetColor("_BaseColor").linear * strength);
        // URP derives the emission keyword from this flag when assets reload.
        // No realtime GI or additional lights are required for this readability lift.
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        material.enableInstancing = source.enableInstancing; EditorUtility.SetDirty(material); cache.Add(key, material); return material;
    }
    public static string Invariants(Scene scene)
    {
        var parts = new List<string> { "environment:" + RenderSettings.ambientMode + "|" + RenderSettings.ambientSkyColor.ToString("F8") +
            "|" + RenderSettings.ambientEquatorColor.ToString("F8") + "|" + RenderSettings.ambientGroundColor.ToString("F8") + "|" +
            RenderSettings.ambientIntensity + "|" + RenderSettings.reflectionIntensity + "|" + (RenderSettings.skybox != null ? RenderSettings.skybox.GetInstanceID() : 0) +
            "|sun:" + (RenderSettings.sun != null ? RenderSettings.sun.intensity.ToString("F8") : "none") };
        for (int channel = 0; channel < 3; channel++) for (int coefficient = 0; coefficient < 9; coefficient++)
            parts.Add("ambientSH:" + RenderSettings.ambientProbe[channel, coefficient].ToString("R"));
        foreach (var root in scene.GetRootGameObjects()) foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            parts.Add(transform.GetInstanceID() + ":" + EditorJsonUtility.ToJson(transform));
            foreach (var component in transform.GetComponents<Component>())
                if (component != null && !(component is Transform) && !(component is Renderer) && !(component is Light) && !(component is PropositionalChamberLighting))
                    parts.Add(component.GetInstanceID() + ":" + EditorJsonUtility.ToJson(component));
        }
        return string.Join("\n", parts);
    }
}
#endif
