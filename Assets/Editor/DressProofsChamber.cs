#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Scene-only staging; original gameplay objects and references stay intact.</summary>
public static class DressProofsChamber
{
    const string Dungeon = "Assets/CHEATCODE/DUNGEON/LowPolyDungeon/";
    [MenuItem("Logic Legends/Proofs Library/Enlarge and Decorate Chamber")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.name != "Formal vs. Informal Proofs")
            throw new InvalidOperationException("Open the proof scene outside Play Mode.");
        var chandelier = Load(Dungeon + "Prefabs/Lamps/Chandelier.prefab");
        var torch = Load(Dungeon + "Prefabs/Lamps/Torch_Wall.prefab");
        var banner = Load(Dungeon + "Prefabs/Furniture/Banner.prefab");
        var rib = Load(Dungeon + "Prefabs/Tiles/Pillar.prefab");
        var ring = Load("Assets/FINAL_ASSETS/MAPS/CINEMATIC/RING.fbx").GetComponentsInChildren<MeshFilter>().First(m => m.name == "Text.001").sharedMesh;
        var iron = AssetDatabase.LoadAssetAtPath<Material>(Dungeon + "Materials/Metal_Rusty.mat");
        var stage = UnityEngine.Object.FindFirstObjectByType<ProofsLibraryStageController>();
        var library = GameObject.Find("ProofsLibrary").transform;
        Undo.RegisterFullObjectHierarchyUndo(library.gameObject, "Dress proof chamber");
        var floors = library.Find("Induction Chamber").GetComponents<MeshCollider>();
        foreach (var extra in floors.Skip(1))
            if (extra.sharedMesh == floors[0].sharedMesh && extra.isTrigger == floors[0].isTrigger && extra.convex == floors[0].convex)
                Undo.DestroyObjectImmediate(extra);

        stage.BookTarget.localScale = Vector3.one * .825f;
        stage.BookTarget.localPosition = new Vector3(0, 0, -9);
        stage.BoardTarget.localScale = Vector3.one * 1.5f;
        stage.BoardTarget.localPosition = new Vector3(0, 0, 13.3f);
        for (int i = 0; i < stage.AltarTargets.Length; i++)
        {
            stage.AltarTargets[i].localScale = Vector3.one * 1.5f;
            stage.AltarTargets[i].localPosition = new Vector3(i == 0 ? -5.8f : 5.8f, 0, 4.4f);
        }
        // Keep the existing perimeter arrangement, but clear the bookshelf
        // which occupied the saved gameplay camera and central entrance aisle.
        var shelves = library.Find("Garden Bookshelves");
        var entranceShelf = shelves.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Garden Bookshelf Model (2)");
        if (entranceShelf != null)
        {
            entranceShelf.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            entranceShelf.localScale = Vector3.one * .38f;
            var bounds = entranceShelf.GetComponent<Renderer>().bounds;
            entranceShelf.position += new Vector3(-4.8f - bounds.center.x, -bounds.min.y, -14.5f - bounds.center.z);
        }
        foreach (var box in shelves.GetComponentsInChildren<BoxCollider>())
            if (box.GetComponentsInChildren<MeshCollider>().Length > 0) box.enabled = false;

        var previous = library.Find("ChamberDecorations");
        if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);
        var decorations = new GameObject("ChamberDecorations").transform;
        decorations.SetParent(library, false);
        Undo.RegisterCreatedObjectUndo(decorations.gameObject, "Add chamber decorations");
        foreach (float z in new[] { -5.5f, 6f })
        {
            var beam = Place(rib, decorations, "Overhead Stone Rib", Vector3.zero, Quaternion.Euler(0, 0, 90), new Vector3(.55f, 8f, .55f));
            Center(beam, new Vector3(0, 16.8f, z));
            foreach (float x in new[] { -10.5f, 10.5f })
            {
                var lamp = Place(chandelier, decorations, "Hanging Chandelier", new Vector3(x, 12f, z), Quaternion.identity, Vector3.one * 2.2f);
                var light = new GameObject("Warm Chandelier Light").AddComponent<Light>();
                light.transform.SetParent(lamp, false); light.transform.position = new Vector3(x, 8.9f, z);
                ConfigureLight(light, 12f, 15f);
                var chain = new GameObject("Suspension Chain").transform; chain.SetParent(decorations, false);
                for (int link = 0; link < 16; link++)
                {
                    var go = new GameObject("Iron Link " + (link + 1), typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(chain, false); go.GetComponent<MeshFilter>().sharedMesh = ring;
                    go.GetComponent<MeshRenderer>().sharedMaterial = iron;
                    go.transform.SetPositionAndRotation(new Vector3(x, 16.65f - link * .35f, z), Quaternion.Euler(0, link % 2 == 0 ? 0 : 90, 0));
                    go.transform.localScale = new Vector3(.22f, .40f, .85f);
                    go.transform.position -= go.transform.TransformVector(ring.bounds.center);
                    GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
                }
            }
        }
        var wall = library.Find("Induction Chamber").GetComponent<MeshCollider>();
        Physics.SyncTransforms();
        foreach (float angle in new[] { 45f, -45f, 135f, -135f })
        {
            var direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            RaycastHit hit;
            if (!wall.Raycast(new Ray(new Vector3(0, 5.8f, 0), direction), out hit, 40))
                throw new InvalidOperationException("No wall for chamber torch at " + angle);
            var sconce = Place(torch, decorations, "Wall Torch", hit.point - direction * .3f, Quaternion.LookRotation(-direction), Vector3.one * 2.5f);
            var light = new GameObject("Warm Wall Light").AddComponent<Light>(); light.transform.SetParent(sconce, false);
            light.transform.position = sconce.position - direction * .8f + Vector3.up * .6f;
            ConfigureLight(light, 4f, 9f);
        }
        foreach (float angle in new[] { 32f, -32f, 148f, -148f })
        {
            var direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            RaycastHit hit;
            if (wall.Raycast(new Ray(new Vector3(0, 14.5f, 0), direction), out hit, 40))
                Place(banner, decorations, "Hanging Banner", hit.point - direction * .25f, Quaternion.LookRotation(-direction), Vector3.one * 1.3f);
        }
        var sun = GameObject.Find("Directional Light").GetComponent<Light>();
        Undo.RecordObject(sun, "Balance chamber lighting"); sun.intensity = .75f; sun.color = new Color(.80f, .87f, 1f);
        RenderSettings.ambientLight = new Color(.40f, .43f, .52f);
        Physics.SyncTransforms(); EditorSceneManager.MarkSceneDirty(scene);
    }
    static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new InvalidOperationException("Missing decoration: " + path);
    static Transform Place(GameObject asset, Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent); go.name = name;
        go.transform.SetPositionAndRotation(position, rotation); go.transform.localScale = scale;
        foreach (var collider in go.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var light in go.GetComponentsInChildren<Light>(true)) light.enabled = false;
        return go.transform;
    }
    static void Center(Transform obj, Vector3 center)
    {
        var renderers = obj.GetComponentsInChildren<Renderer>(); var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        obj.position += center - bounds.center;
    }
    static void ConfigureLight(Light light, float intensity, float range)
    {
        light.type = LightType.Point; light.color = new Color(1f, .73f, .40f);
        light.intensity = intensity; light.range = range; light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.ForcePixel;
    }
}
#endif
