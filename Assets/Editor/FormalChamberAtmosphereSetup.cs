#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Scene overrides only: shared decorations and imported source materials stay unchanged.</summary>
public static class FormalChamberAtmosphereSetup
{
    public const string CeilingAsset = "Assets/FINAL_ASSETS/MAPS/REMADE/ChamberCeiling.fbx";
    public const string ScenePath = "Assets/Scenes/Formal vs. Informal Proofs.unity";

    [MenuItem("Logic Legends/Proofs Library/Apply Chamber Atmosphere")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
            throw new InvalidOperationException("Open Formal vs. Informal Proofs in Edit Mode.");
        var stage = UnityEngine.Object.FindFirstObjectByType<ProofsLibraryStageController>(FindObjectsInactive.Include);
        var decorations = stage.BookTarget.root.GetComponentsInChildren<Transform>(true).Single(t => t.name == "ChamberDecorations");
        var chamber = stage.BookTarget.root.GetComponentsInChildren<Renderer>(true).Single(r => r.name == "Induction Chamber");
        var ceiling = decorations.Find("Chamber Ceiling");
        if (ceiling == null)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(CeilingAsset);
            if (asset == null) throw new InvalidOperationException("Missing ChamberCeiling asset.");
            ceiling = ((GameObject)PrefabUtility.InstantiatePrefab(asset, decorations)).transform;
            ceiling.name = "Chamber Ceiling";
            Undo.RegisterCreatedObjectUndo(ceiling.gameObject, "Fit chamber ceiling");
        }
        Undo.RecordObject(ceiling, "Fit chamber ceiling");
        ceiling.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        ceiling.localScale = Vector3.one;
        var roofBounds = BoundsOf(ceiling);
        float diameter = Mathf.Max(chamber.bounds.size.x, chamber.bounds.size.z) + .6f;
        float scale = diameter / Mathf.Min(roofBounds.size.x, roofBounds.size.z);
        ceiling.localScale = Vector3.one * scale;
        roofBounds = BoundsOf(ceiling);
        // Slight overlap with the wall crown closes the perimeter without touching the walkable chamber.
        ceiling.position += new Vector3(chamber.bounds.center.x - roofBounds.center.x,
            chamber.bounds.max.y - .22f - roofBounds.min.y, chamber.bounds.center.z - roofBounds.center.z);
        foreach (var collider in ceiling.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        PrefabUtility.RecordPrefabInstancePropertyModifications(ceiling);
        var serialized = new SerializedObject(stage);
        var roofs = serialized.FindProperty("tutorialCeilingRenderers");
        var renderers = ceiling.GetComponentsInChildren<Renderer>(true);
        roofs.arraySize = renderers.Length;
        for (int i = 0; i < renderers.Length; i++) roofs.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
        serialized.ApplyModifiedProperties();

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.16f, .18f, .24f);
        RenderSettings.ambientIntensity = .65f;
        RenderSettings.reflectionIntensity = .35f;
        var sun = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>(true))
            .FirstOrDefault(l => l.type == LightType.Directional);
        if (sun != null)
        {
            Undo.RecordObject(sun, "Dim chamber fill");
            sun.intensity = .18f; sun.color = new Color(.67f, .74f, 1f);
        }
        foreach (var light in decorations.GetComponentsInChildren<Light>(true))
        {
            if (light.name != "Warm Chandelier Light" && light.name != "Warm Wall Light") continue;
            Undo.RecordObject(light, "Strengthen existing practical light");
            bool chandelier = light.name == "Warm Chandelier Light";
            Configure(light, chandelier ? 25f : 11f, chandelier ? 17f : 11f, new Color(1f, .72f, .40f));
            PrefabUtility.RecordPrefabInstancePropertyModifications(light);
        }
        var accents = decorations.Find("AtmosphereLighting");
        if (accents == null)
        {
            accents = new GameObject("AtmosphereLighting").transform; accents.SetParent(decorations, false);
            Undo.RegisterCreatedObjectUndo(accents.gameObject, "Add chamber accent lights");
        }
        Accent(accents, "Board Reading Light", new Vector3(0, 7.5f, 8), 9, 12, new Color(1f, .83f, .61f), stage.BoardTarget.position + Vector3.up * 5);
        Accent(accents, "Book Reading Light", new Vector3(0, 4.5f, -7), 6, 9, new Color(1f, .8f, .55f), stage.BookTarget.position + Vector3.up * 1.8f);
        Accent(accents, "Azure Architecture Accent", new Vector3(-15, 4, 0), 4, 7, new Color(.3f, .5f, 1f), null);
        Accent(accents, "Violet Architecture Accent", new Vector3(15, 4, 0), 4, 7, new Color(.61f, .32f, 1f), null);
        Physics.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
    static Bounds BoundsOf(Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }
    static void Configure(Light light, float intensity, float range, Color color)
    {
        light.enabled = true; light.intensity = intensity; light.range = range; light.color = color;
        light.shadows = LightShadows.None; light.bounceIntensity = 0;
        light.renderMode = LightRenderMode.Auto;
    }
    static void Accent(Transform parent, string name, Vector3 position, float intensity, float range, Color color, Vector3? target)
    {
        var child = parent.Find(name);
        var light = child != null ? child.GetComponent<Light>() : null;
        if (light == null)
        {
            light = new GameObject(name).AddComponent<Light>(); light.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(light.gameObject, "Add chamber accent light");
        }
        Undo.RecordObject(light, "Balance chamber accent"); Undo.RecordObject(light.transform, "Aim chamber accent");
        light.transform.position = position;
        light.type = target.HasValue ? LightType.Spot : LightType.Point;
        if (target.HasValue) { light.transform.LookAt(target.Value); light.spotAngle = 90; light.innerSpotAngle = 65; }
        Configure(light, intensity, range, color);
    }
}
#endif
