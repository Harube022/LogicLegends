#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using LogicLegends.Induction;
using LogicLegends.Inference;

/// <summary>Scene-instance lighting only. No shared models, materials, colliders or gameplay data are edited.</summary>
public static class TrialAtmosphereSetup
{
    public static readonly string[] Scenes = { "Induction", "Setssss", "Functions and Relations", "TruthTable", "RulesOfInference" };
    [MenuItem("Logic Legends/Lighting/Balance Five Trial Atmospheres")]
    public static void ApplyAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save the open scene and leave Play Mode first.");
        string previous = SceneManager.GetActiveScene().path;
        foreach (string name in Scenes)
        { EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity"); ApplyCurrent(); }
        EditorSceneManager.OpenScene(previous);
    }
    public static void ApplyCurrent()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || !Scenes.Contains(scene.name))
            throw new InvalidOperationException("Open one of the five requested trials in Edit Mode.");
        var sun = All<Light>().First(l => l.type == LightType.Directional);
        if (scene.name == "Induction") Induction(sun);
        else
        {
            Undo.RecordObject(sun, "Balance trial sunlight");
            Undo.RecordObject(sun.transform, "Shape sunlight direction");
            sun.shadows = LightShadows.Soft; sun.shadowStrength = .65f;
            sun.shadowBias = .05f; sun.shadowNormalBias = .25f;
            RenderSettings.reflectionIntensity = .6f;
            if (scene.name == "Setssss")
            {
                Daylight(sun, new Vector3(44, 320, 0), 1.05f, new Color(1, .95f, .86f),
                    new Color(.42f, .52f, .65f), new Color(.34f, .39f, .36f), new Color(.20f, .23f, .24f));
                AddLampPools(LightingGroup("StageBorderDecorations"), 10, 16, 16);
            }
            else if (scene.name == "Functions and Relations")
            {
                Daylight(sun, new Vector3(43, 310, 0), 1, new Color(1, .95f, .86f),
                    new Color(.40f, .49f, .57f), new Color(.32f, .38f, .34f), new Color(.18f, .22f, .23f));
                foreach (var light in All<Light>().Where(l => l.name == "Warm Ground Pool"))
                { Undo.RecordObject(light, "Balance existing trail lamps"); Local(light, 16, 16); }
                var stage = UnityEngine.Object.FindFirstObjectByType<FunctionsRelationsStageManager>(FindObjectsInactive.Include);
                var group = LightingGroup("FunctionsRelationsEnvironment");
                Point(group, "Domain Crystal Accent", stage.DomainTarget.position + Vector3.up * 2, 1.8f, 4.5f, new Color(.28f, .66f, 1));
                Point(group, "Range Crystal Accent", stage.RangeTarget.position + Vector3.up * 2, 1.8f, 4.5f, new Color(.35f, .8f, 1));
            }
            else if (scene.name == "RulesOfInference")
            {
                Daylight(sun, new Vector3(38, 315, 0), 1.05f, new Color(.99f, .96f, .90f),
                    new Color(.38f, .47f, .58f), new Color(.32f, .37f, .37f), new Color(.16f, .20f, .23f));
                var group = LightingGroup(null); AddLampPools(group, 6, 15, 17);
                var challenge = UnityEngine.Object.FindFirstObjectByType<InferenceChallenge>(FindObjectsInactive.Include);
                for (int i = 0; i < challenge.crystalSpawns.Length; i++)
                    Point(group, "Rule Tower Accent " + (i + 1).ToString("00"), challenge.crystalSpawns[i].position + Vector3.down * .3f,
                        1.6f, 4.5f, new Color(.30f, .61f, 1));
                var book = All<Transform>().First(t => t.name == "Book_Statue");
                Spot(group, "Steady Book Accent", book.position + new Vector3(0, 5, 2), book.position + Vector3.up * 2, 3, 10, new Color(1, .83f, .61f));
            }
            else Sunset(sun);
        }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
    }
    static void Daylight(Light sun, Vector3 angles, float intensity, Color sunlight, Color sky, Color equator, Color ground)
    {
        sun.transform.rotation = Quaternion.Euler(angles); sun.intensity = intensity; sun.color = sunlight;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = equator; RenderSettings.ambientGroundColor = ground;
        RenderSettings.ambientIntensity = .9f;
    }
    static void Sunset(Light sun)
    {
        // Keep the existing sunset sky and low sun elevation. Save matching runtime overrides, too.
        var sky = new Color(.38f, .32f, .41f); var equator = new Color(.30f, .25f, .24f); var ground = new Color(.17f, .145f, .17f);
        var warm = new Color(1, .78f, .56f);
        Daylight(sun, new Vector3(14, 310, 0), .82f, warm, sky, equator, ground);
        var profile = UnityEngine.Object.FindFirstObjectByType<TruthTableDuskLighting>(FindObjectsInactive.Include);
        var settings = new SerializedObject(profile);
        settings.FindProperty("sunlightIntensity").floatValue = .82f; settings.FindProperty("sunlightColor").colorValue = warm;
        settings.FindProperty("ambientSky").colorValue = sky; settings.FindProperty("ambientEquator").colorValue = equator;
        settings.FindProperty("ambientGround").colorValue = ground;
        settings.FindProperty("lampIntensity").floatValue = 18; settings.FindProperty("lampRange").floatValue = 18;
        settings.ApplyModifiedProperties();
        foreach (var light in All<Light>().Where(l => l.name == "TruthTableFixtureLight"))
        { Undo.RecordObject(light, "Balance sunset fixtures"); Local(light, 18, 18); }
    }
    static void Induction(Light sun)
    {
        var stage = UnityEngine.Object.FindFirstObjectByType<InductionStageManager>(FindObjectsInactive.Include);
        var decorations = All<Transform>().First(t => t.name == "ChamberDecorations");
        RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.15f, .17f, .225f);
        RenderSettings.ambientIntensity = .65f; RenderSettings.reflectionIntensity = .35f;
        Undo.RecordObject(sun, "Dim chamber fill"); sun.intensity = .18f; sun.color = new Color(.67f, .74f, 1);
        foreach (var light in decorations.GetComponentsInChildren<Light>(true))
        {
            bool hanging = light.name == "Warm Chandelier Light", wall = light.name == "Warm Wall Light";
            if (!hanging && !wall && light.name != "Soft Crystal Light") continue;
            Undo.RecordObject(light, "Balance chamber fixture");
            Local(light, hanging ? 40 : wall ? 14 : 2.8f, hanging ? 24 : wall ? 13 : 6.5f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(light);
        }
        var group = decorations.Find("AtmosphereLighting");
        if (group == null) { group = new GameObject("AtmosphereLighting").transform; group.SetParent(decorations, false); Undo.RegisterCreatedObjectUndo(group.gameObject, "Add lighting group"); }
        var parchment = stage.informationBoard.GetComponentsInChildren<Renderer>(true).First(r => r.name == "Info_ParchmentSurface").bounds;
        var normal = stage.informationBoard.forward;
        Spot(group, "Steady Board Reading Light", parchment.center + normal * 7 + Vector3.up * 1.5f, parchment.center, 18, 13, new Color(1, .84f, .65f));
        Spot(group, "Steady Book Reading Light", stage.book.position + new Vector3(0, 6, -2), stage.book.position + Vector3.up * 2.5f, 8, 11, new Color(1, .81f, .57f));
        Spot(group, "Steady Scroll Reading Light", stage.scrollStand.position + new Vector3(0, 6, -2), stage.scrollStand.position + Vector3.up * 2, 7, 11, new Color(1, .83f, .61f));
        Vector3 center = stage.stations.Select(t => t.position).Aggregate(Vector3.zero, (a, b) => a + b) / stage.stations.Length;
        Spot(group, "Steady Proof Stations Light", center + Vector3.up * 7, center, 7, 16, new Color(.68f, .78f, 1));
        var effect = group.GetComponent<ChamberLightFlicker>();
        if (effect == null) effect = Undo.AddComponent<ChamberLightFlicker>(group.gameObject);
        var walls = decorations.GetComponentsInChildren<Light>(true).Where(l => l.name == "Warm Wall Light").OrderBy(l => l.transform.position.x).ToArray();
        var hangingLights = decorations.GetComponentsInChildren<Light>(true).Where(l => l.name == "Warm Chandelier Light").OrderBy(l => l.transform.position.z).ToArray();
        var settings = new SerializedObject(effect); var affected = settings.FindProperty("flickeringLights"); affected.arraySize = 3;
        var targets = new[] { walls.First(), walls.Last(), hangingLights.First() };
        for (int i = 0; i < targets.Length; i++) affected.GetArrayElementAtIndex(i).objectReferenceValue = targets[i];
        settings.FindProperty("seed").intValue = 61027; settings.ApplyModifiedProperties();
    }
    static T[] All<T>() where T : UnityEngine.Object => UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    static Transform LightingGroup(string preferred)
    {
        var host = string.IsNullOrEmpty(preferred) ? null : All<Transform>().FirstOrDefault(t => t.name == preferred);
        var found = host != null ? host.Find("StageLighting") : SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "StageLighting")?.transform;
        if (found != null) return found;
        var group = new GameObject("StageLighting").transform; if (host != null) group.SetParent(host, false);
        Undo.RegisterCreatedObjectUndo(group.gameObject, "Organize trial accents"); return group;
    }
    static void AddLampPools(Transform parent, int count, float intensity, float range)
    {
        var book = All<Transform>().First(t => t.name == "Book_Statue");
        var lamps = All<Transform>().Where(t => System.Text.RegularExpressions.Regex.IsMatch(t.name, @"^STREETLAMP2(?: \(\d+\))?$"))
            .OrderBy(t => (t.position - book.position).sqrMagnitude).Take(count).ToArray();
        foreach (var lamp in lamps)
        {
            var models = lamp.GetComponentsInChildren<MeshFilter>(true);
            Vector3 position = lamp.position + Vector3.up * 8;
            foreach (var model in models)
            {
                var renderer = model.GetComponent<Renderer>(); if (renderer == null || model.sharedMesh == null) continue;
                for (int i = 0; i < renderer.sharedMaterials.Length && i < model.sharedMesh.subMeshCount; i++)
                    if (renderer.sharedMaterials[i] != null && renderer.sharedMaterials[i].name.IndexOf("Celestial Gold", StringComparison.OrdinalIgnoreCase) >= 0)
                        position = model.transform.TransformPoint(model.sharedMesh.GetSubMesh(i).bounds.center);
            }
            // One grouped, shadowless pool per existing lantern. Imported geometry/colliders remain untouched.
            Spot(parent, lamp.name + " Warm Pool", position, position + Vector3.down * 6, intensity, range, new Color(1, .77f, .47f));
        }
    }
    static Light Get(Transform parent, string name)
    {
        var child = parent.Find(name); var light = child != null ? child.GetComponent<Light>() : null;
        if (light == null) { light = new GameObject(name).AddComponent<Light>(); light.transform.SetParent(parent, false); Undo.RegisterCreatedObjectUndo(light.gameObject, "Add trial accent"); }
        Undo.RecordObject(light, "Balance accent"); Undo.RecordObject(light.transform, "Place accent"); return light;
    }
    static void Local(Light light, float intensity, float range)
    { light.enabled = true; light.intensity = intensity; light.range = range; light.shadows = LightShadows.None; light.bounceIntensity = 0; light.renderMode = LightRenderMode.Auto; }
    static void Point(Transform parent, string name, Vector3 position, float intensity, float range, Color color)
    { var light = Get(parent, name); light.type = LightType.Point; light.transform.position = position; light.color = color; Local(light, intensity, range); }
    static void Spot(Transform parent, string name, Vector3 position, Vector3 target, float intensity, float range, Color color)
    {
        var light = Get(parent, name); light.type = LightType.Spot; light.transform.position = position; light.transform.LookAt(target);
        light.spotAngle = 90; light.innerSpotAngle = 60; light.color = color; Local(light, intensity, range);
    }
}
#endif
