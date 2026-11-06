#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Local lighting overrides; keeps the shared chamber prefab and all gameplay intact.</summary>
public static class DirectChamberAtmosphereSetup
{
    public const string ScenePath = "Assets/Scenes/Direct&Indirect.unity";
    [MenuItem("Logic Legends/Direct Proofs/Apply Chamber Atmosphere")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Open Direct&Indirect in Edit Mode.");
        var stage = UnityEngine.Object.FindFirstObjectByType<DirectIndirectProofStageController>(FindObjectsInactive.Include);
        string gameplay = EditorJsonUtility.ToJson(stage);
        var decorations = scene.GetRootGameObjects().Single(g => g.name == "ChamberDecorations").transform;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.135f, .15f, .20f);
        RenderSettings.ambientIntensity = .65f;
        RenderSettings.reflectionIntensity = .35f;
        foreach (var sun in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>(true)).Where(l => l.type == LightType.Directional))
        {
            Undo.RecordObject(sun, "Dim chamber fill"); sun.intensity = .18f; sun.color = new Color(.67f, .74f, 1f);
        }
        foreach (var light in decorations.GetComponentsInChildren<Light>(true))
        {
            bool chandelier = light.name == "Warm Chandelier Light", torch = light.name == "Warm Wall Light";
            if (!chandelier && !torch && light.name != "Soft Crystal Accent Light") continue;
            Undo.RecordObject(light, "Strengthen chamber fixtures");
            Configure(light, chandelier ? 32 : torch ? 16 : 3.2f, chandelier ? 21 : torch ? 15 : 6.5f);
            if (chandelier || torch) light.color = new Color(1, .72f, .4f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(light);
        }
        var accents = decorations.Find("AtmosphereLighting");
        if (accents == null)
        {
            accents = new GameObject("AtmosphereLighting").transform; accents.SetParent(decorations, false);
            Undo.RegisterCreatedObjectUndo(accents.gameObject, "Add chamber accent lighting");
        }
        // Mesh coordinates in the imported board are offset from its transform: use the actual parchment bounds.
        var parchment = stage.InformationBoardDisplay.GetComponentsInChildren<Renderer>(true).Single(r => r.name == "Info_ParchmentSurface").bounds;
        Accent(accents, "Steady Board Reading Light", parchment.center + new Vector3(0, 1.5f, 7), parchment.center, 18, 13, new Color(1, .84f, .64f));
        var book = stage.Book.position;
        Accent(accents, "Steady Book Reading Light", book + new Vector3(0, 6, 2.5f), book + Vector3.up * 3, 8, 11, new Color(1, .81f, .57f));
        for (int i = 0; i < 2; i++)
        {
            var altar = stage.Altars[i];
            Accent(accents, "Steady " + (i == 0 ? "Direct" : "Indirect") + " Altar Light", altar.position + new Vector3(0, 6, 2),
                altar.position + Vector3.up * 1.2f, 5, 11, i == 0 ? new Color(.55f, .74f, 1) : new Color(1, .64f, .45f));
        }
        var flicker = accents.GetComponent<ChamberLightFlicker>();
        if (flicker == null) flicker = Undo.AddComponent<ChamberLightFlicker>(accents.gameObject);
        var walls = decorations.GetComponentsInChildren<Light>(true).Where(l => l.name == "Warm Wall Light").OrderBy(l => l.transform.position.x).ThenBy(l => l.transform.position.z).ToArray();
        var chandeliers = decorations.GetComponentsInChildren<Light>(true).Where(l => l.name == "Warm Chandelier Light").OrderBy(l => l.transform.position.x).ThenBy(l => l.transform.position.z).ToArray();
        var affected = new[] { walls.First(), walls.Last(), chandeliers.First() };
        var serialized = new SerializedObject(flicker); var targets = serialized.FindProperty("flickeringLights"); targets.arraySize = affected.Length;
        for (int i = 0; i < affected.Length; i++) targets.GetArrayElementAtIndex(i).objectReferenceValue = affected[i];
        serialized.ApplyModifiedProperties();
        if (gameplay != EditorJsonUtility.ToJson(stage)) throw new InvalidOperationException("Gameplay references changed.");
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
    }
    static void Configure(Light light, float intensity, float range)
    {
        light.enabled = true; light.intensity = intensity; light.range = range;
        light.shadows = LightShadows.None; light.bounceIntensity = 0; light.renderMode = LightRenderMode.Auto;
    }
    static void Accent(Transform parent, string name, Vector3 position, Vector3 target, float intensity, float range, Color color)
    {
        var child = parent.Find(name); var light = child != null ? child.GetComponent<Light>() : null;
        if (light == null)
        {
            light = new GameObject(name).AddComponent<Light>(); light.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(light.gameObject, "Add steady reading light");
        }
        Undo.RecordObject(light, "Balance accent light"); Undo.RecordObject(light.transform, "Aim accent light");
        light.transform.position = position; light.transform.LookAt(target);
        light.type = LightType.Spot; light.spotAngle = 100; light.innerSpotAngle = 75; light.color = color;
        Configure(light, intensity, range);
    }
}
#endif
