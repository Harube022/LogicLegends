#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Prepares and bakes visibility data for the three large solo environments.</summary>
public static class SoloTrialOcclusionSetup
{
    public static readonly string[] ScenePaths = {
        "Assets/Scenes/TruthTable.unity",
        "Assets/Scenes/RulesOfInference.unity",
        "Assets/Scenes/Setssss.unity"
    };
    const string Menu = "Logic Legends/Optimization/Occlusion/";
    const string AreaName = "Solo Trial Occlusion Area";
    public const string ReportPath = "Library/SoloTrialOcclusionBake.txt";
    const StaticEditorFlags OcclusionFlags = StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;
    static readonly string[] MovingNames = {
        "door", "gate", "book", "scroll", "crystal", "portal", "answer", "spawner", "pickup", "hologram"
    };
    static readonly string[] PlantNames = {
        "tree", "puno", "grass", "foliage", "fern", "plant", "vine", "ivy", "flower", "bush", "leaf", "leaves", "canopy"
    };
    static Queue<string> pending;
    static SceneSetup[] originalSetup;
    static Scene bakingScene;
    static double started;

    [MenuItem(Menu + "Bake All Three Trials")]
    public static void BakeAll() => Begin(ScenePaths);

    [MenuItem(Menu + "Bake Current Trial")]
    public static void BakeCurrent() => Begin(new[] { SceneManager.GetActiveScene().path });

    [MenuItem(Menu + "Prepare Current Trial")]
    public static void PrepareCurrent()
    {
        RequireIdle();
        var scene = SceneManager.GetActiveScene();
        RequireSupported(scene.path);
        Debug.Log(Prepare(scene));
        EditorSceneManager.MarkSceneDirty(scene);
    }

    static void Begin(IEnumerable<string> paths)
    {
        RequireIdle();
        var requested = paths.ToArray();
        foreach (string path in requested) RequireSupported(path);
        originalSetup = EditorSceneManager.GetSceneManagerSetup();
        pending = new Queue<string>(requested);
        File.WriteAllText(ReportPath, "Occlusion visibility bake for gameplay scenes.\n");
        EditorApplication.update += Tick;
        AssemblyReloadEvents.beforeAssemblyReload += CancelBake;
        StartNext();
    }

    static void RequireIdle()
    {
        if (pending != null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            BuildPipeline.isBuildingPlayer || StaticOcclusionCulling.isRunning || Lightmapping.isRunning)
            throw new InvalidOperationException("Finish the current Editor operation first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save your open scenes before preparing or baking occlusion.");
    }

    static void RequireSupported(string path)
    {
        if (!ScenePaths.Contains(path))
            throw new InvalidOperationException("Open TruthTable, RulesOfInference, or Setssss for this command.");
    }

    static void StartNext()
    {
        try
        {
            if (pending.Count == 0) { Finish("COMPLETE"); return; }
            string path = pending.Dequeue();
            bakingScene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            SceneManager.SetActiveScene(bakingScene);
            File.AppendAllText(ReportPath, Prepare(bakingScene) + "\n");
            StaticOcclusionCulling.smallestOccluder = 5f;
            StaticOcclusionCulling.smallestHole = .5f;
            StaticOcclusionCulling.backfaceThreshold = 100f;
            EditorSceneManager.SaveScene(bakingScene);
            started = EditorApplication.timeSinceStartup;
            if (!StaticOcclusionCulling.GenerateInBackground())
                throw new InvalidOperationException("Unity could not start the occlusion bake for " + path);
            File.AppendAllText(ReportPath, "BAKING " + path + "\n");
        }
        catch (Exception error) { Finish("FAILED " + error.Message); Debug.LogException(error); }
    }

    static void Tick()
    {
        if (pending == null || StaticOcclusionCulling.isRunning) return;
        try
        {
            EditorSceneManager.SaveScene(bakingScene);
            string result = Validate(bakingScene);
            File.AppendAllText(ReportPath, result + " elapsed seconds " +
                (EditorApplication.timeSinceStartup - started).ToString("F1") + "\n");
            StartNext();
        }
        catch (Exception error) { Finish("FAILED " + error.Message); Debug.LogException(error); }
    }

    [MenuItem(Menu + "Cancel Bake")]
    public static void CancelBake()
    {
        if (pending == null) return;
        if (StaticOcclusionCulling.isRunning) StaticOcclusionCulling.Cancel();
        Finish("CANCELLED");
    }

    static void Finish(string result)
    {
        EditorApplication.update -= Tick;
        AssemblyReloadEvents.beforeAssemblyReload -= CancelBake;
        pending = null;
        File.AppendAllText(ReportPath, result + "\n");
        if (originalSetup != null) EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
        originalSetup = null;
        Debug.Log(result + " solo trial occlusion. Report saved at " + ReportPath);
    }

    public static string Prepare(Scene scene)
    {
        RequireSupported(scene.path);
        var movers = MovingDoors(scene);
        var catalog = Resources.Load<SoloMobileMeshes>("Settings/SoloMobileMeshes");
        var environment = new List<Renderer>();
        int occluders = 0, occludees = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) & ~OcclusionFlags;
                if (IsEnvironment(renderer, root, movers))
                {
                    flags |= StaticEditorFlags.OccludeeStatic;
                    occludees++;
                    environment.Add(renderer);
                    if (IsOccluder(renderer, filter.sharedMesh, catalog))
                    {
                        flags |= StaticEditorFlags.OccluderStatic;
                        occluders++;
                    }
                    renderer.allowOcclusionWhenDynamic = true;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, flags);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer.gameObject);
            }
            foreach (var camera in root.GetComponentsInChildren<Camera>(true))
            {
                camera.useOcclusionCulling = !camera.name.Contains("Minimap");
                PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
            }
        }
        if (occluders == 0) throw new InvalidOperationException("No solid static occluders found in " + scene.name);
        CreateViewArea(scene, environment);
        return scene.name + " prepared with " + occluders + " occluders and " + occludees + " static occludees";
    }

    static HashSet<Transform> MovingDoors(Scene scene)
    {
        var result = new HashSet<Transform>();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var interaction in root.GetComponentsInChildren<TruthTableDoorInteraction>(true))
            {
                var doors = new SerializedObject(interaction).FindProperty("doors");
                for (int i = 0; doors != null && i < doors.arraySize; i++)
                    if (doors.GetArrayElementAtIndex(i).objectReferenceValue is Collider door) result.Add(door.transform);
            }
        return result;
    }

    static bool IsEnvironment(MeshRenderer renderer, GameObject root, HashSet<Transform> movers)
    {
        if (renderer.GetComponentInParent<Canvas>(true) != null || renderer.GetComponentInParent<TMPro.TMP_Text>(true) != null ||
            renderer.GetComponentInParent<Animator>(true) != null || renderer.GetComponentInParent<GrabbableObject>(true) != null ||
            renderer.GetComponentInParent<BookStatuePresentation>(true) != null) return false;
        // These diagrams and boards change between questions and must remain dynamic.
        if (renderer.gameObject.scene.name == "Setssss" &&
            new[] { "AB", "A-B", "B-A", "BORDERLINES", "Board 1", "Spawner", "Book_Statue" }.Contains(root.name)) return false;
        for (var node = renderer.transform; node != null; node = node.parent)
        {
            if (movers.Contains(node)) return false;
            string name = node.name.ToLowerInvariant();
            if (MovingNames.Any(name.Contains)) return false;
            if (node.GetComponent<TruthBlockSpawner>() != null) return false;
        }
        return renderer.enabled && renderer.gameObject.activeInHierarchy;
    }

    static bool IsOccluder(MeshRenderer renderer, Mesh mesh, SoloMobileMeshes catalog)
    {
        // Occluder silhouettes must match what Android actually renders.
        if (mesh.lodCount > 1 || renderer.GetComponentInParent<LODGroup>() != null ||
            catalog != null && catalog.Resolve(mesh, 0) != mesh) return false;
        string description = (renderer.name + " " + AssetDatabase.GetAssetPath(mesh)).ToLowerInvariant();
        if (PlantNames.Any(description.Contains)) return false;
        var size = renderer.bounds.size;
        if (Mathf.Max(size.x, size.z) < 5f && size.y < 5f) return false;
        return renderer.sharedMaterials.Length > 0 && renderer.sharedMaterials.All(material => material != null &&
            material.renderQueue < 3000 && material.GetTag("RenderType", false, "Opaque") == "Opaque" &&
            (!material.HasProperty("_Surface") || material.GetFloat("_Surface") == 0) &&
            (!material.HasProperty("_AlphaClip") || material.GetFloat("_AlphaClip") == 0));
    }

    static void CreateViewArea(Scene scene, List<Renderer> environment)
    {
        var floor = environment.Where(renderer =>
        {
            string name = renderer.name.ToLowerInvariant();
            return name.Contains("floor") || name.Contains("ground") || name == "stage";
        }).ToArray();
        var selected = floor.Length > 0 ? floor : environment.ToArray();
        var bounds = selected[0].bounds;
        foreach (var renderer in selected) bounds.Encapsulate(renderer.bounds);
        var spawn = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LevelSpawner>(true)).FirstOrDefault();
        var point = spawn != null ? new SerializedObject(spawn).FindProperty("spawnPoint")?.objectReferenceValue as Transform : null;
        float baseY = point != null ? point.position.y : bounds.min.y;
        var areaObject = scene.GetRootGameObjects().FirstOrDefault(root => root.name == AreaName);
        if (areaObject == null)
        {
            areaObject = new GameObject(AreaName);
            SceneManager.MoveGameObjectToScene(areaObject, scene);
        }
        areaObject.transform.SetPositionAndRotation(new Vector3(bounds.center.x, baseY + 25f, bounds.center.z), Quaternion.identity);
        areaObject.transform.localScale = Vector3.one;
        var area = areaObject.GetComponent<OcclusionArea>();
        if (area == null) area = areaObject.AddComponent<OcclusionArea>();
        area.center = Vector3.zero;
        area.size = new Vector3(bounds.size.x + 32f, 80f, bounds.size.z + 32f);
        var settings = new SerializedObject(area);
        settings.FindProperty("m_IsViewVolume").boolValue = true;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    public static string Validate(Scene scene)
    {
        string dataPath = Path.ChangeExtension(scene.path, null) + "/OcclusionCullingData.asset";
        var data = AssetDatabase.LoadMainAssetAtPath(dataPath);
        if (data == null || !File.ReadAllText(scene.path).Contains(AssetDatabase.AssetPathToGUID(dataPath)))
            throw new InvalidOperationException("Saved occlusion data is missing or unassigned in " + scene.name);
        var movers = MovingDoors(scene);
        int occluders = 0, occludees = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                if ((flags & StaticEditorFlags.OccluderStatic) != 0)
                {
                    if (!IsEnvironment(renderer, root, movers))
                        throw new InvalidOperationException("A moving gameplay object was marked as an occluder in " + scene.name);
                    occluders++;
                }
                if ((flags & StaticEditorFlags.OccludeeStatic) != 0) occludees++;
            }
            foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                if (camera.useOcclusionCulling == camera.name.Contains("Minimap"))
                    throw new InvalidOperationException("Incorrect camera occlusion setting in " + scene.name);
        }
        if (occluders == 0) throw new InvalidOperationException("No static occluders in " + scene.name);
        return "PASS " + scene.name + " occluders " + occluders + " static occludees " + occludees +
            " saved visibility bytes " + new FileInfo(dataPath).Length;
    }
}
#endif
