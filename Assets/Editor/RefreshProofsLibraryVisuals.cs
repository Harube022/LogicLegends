#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Replaces scene artwork without rebuilding any proof-gameplay objects.</summary>
public static class RefreshProofsLibraryVisuals
{
    const string Chamber = "Assets/FINAL_ASSETS/MAPS/REMADE/Induction/Stage.fbx";
    const string Board = "Assets/FINAL_ASSETS/MAPS/REMADE/Induction/InductionBoards/InformationBoard.fbx";
    const string Shelf = "Assets/FINAL_ASSETS/MAPS/REMADE/REMAKE TUTS/GARDEN/BOOKSHELVES_DESIGN.fbx";

    [MenuItem("Logic Legends/Proofs Library/Replace Environment Artwork")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.name != "Formal vs. Informal Proofs")
            throw new InvalidOperationException("Open Formal vs. Informal Proofs outside Play Mode.");
        var chamberAsset = Load(Chamber); var boardAsset = Load(Board); var shelfAsset = Load(Shelf);
        var library = GameObject.Find("ProofsLibrary").transform;
        var stage = UnityEngine.Object.FindFirstObjectByType<ProofsLibraryStageController>();
        var board = stage.BoardTarget;
        var headerTransform = library.Find("Board Header");
        if (headerTransform == null) headerTransform = board.Find("Board Header");
        var header = headerTransform.GetComponent<TMP_Text>();
        var instructions = stage.BoardInstructions;
        // These components remain the same objects, including their serialized references.
        Undo.RegisterFullObjectHierarchyUndo(library.gameObject, "Replace proof library artwork");

        Remove(library.Find("Induction Chamber"));
        var chamber = Instantiate(chamberAsset, library, "Induction Chamber");
        chamber.localPosition = Vector3.zero; chamber.localRotation = Quaternion.identity;
        chamber.localScale = Vector3.one * .8f;
        var floor = chamber.gameObject.AddComponent<MeshCollider>();
        floor.sharedMesh = chamber.GetComponent<MeshFilter>().sharedMesh;
        Physics.SyncTransforms();
        RaycastHit hit;
        if (!floor.Raycast(new Ray(new Vector3(0, 30, 0), Vector3.down), out hit, 60))
            throw new InvalidOperationException("The replacement chamber has no walkable center floor.");
        chamber.position -= Vector3.up * hit.point.y;

        // The old group contains only floor/wall/decoration artwork, not game stations.
        Remove(library.Find("Library Architecture"));
        foreach (var name in new[] { "Library Sign", "Sign Gold Rail", "Archive Crystal" }) Remove(library.Find(name));

        // Keep the board holder/collider and both original text components intact.
        foreach (Transform child in board.Cast<Transform>().ToArray())
            if (child != header.transform && child != instructions.transform) Remove(child);
        board.localScale = Vector3.one;
        var model = Instantiate(boardAsset, board, "Induction Information Board Model");
        model.localPosition = Vector3.zero; model.localRotation = Quaternion.identity;
        model.localScale = new Vector3(4.4f, 2.5f, 1.8f);
        var surface = model.Find("Info_ParchmentSurface").GetComponent<Renderer>().bounds;
        header.transform.SetParent(board, true); instructions.transform.SetParent(board, true);
        float front = surface.min.z - .045f;
        header.transform.SetPositionAndRotation(new Vector3(surface.center.x, surface.max.y - .60f, front), Quaternion.identity);
        header.rectTransform.sizeDelta = new Vector2(surface.size.x - .6f, .68f);
        header.fontSize = header.fontSizeMax = 4.2f; header.fontSizeMin = 2.8f;
        instructions.transform.SetPositionAndRotation(new Vector3(surface.center.x, surface.center.y - .48f, front - .01f), Quaternion.identity);
        instructions.rectTransform.sizeDelta = new Vector2(surface.size.x - .7f, surface.size.y - 1.65f);
        instructions.enableAutoSizing = true; instructions.fontSize = instructions.fontSizeMax = 3f; instructions.fontSizeMin = 1.8f;
        instructions.textWrappingMode = TextWrappingModes.Normal;
        FitCollider(board.gameObject, BoundsOf(model));

        Remove(library.Find("Garden Bookshelves"));
        var shelves = new GameObject("Garden Bookshelves").transform; shelves.SetParent(library, false);
        Undo.RegisterCreatedObjectUndo(shelves.gameObject, "Add garden bookshelves");
        AddShelf(shelfAsset, shelves, "West Bookshelf", new Vector3(-15.5f, 0, -4), 90);
        AddShelf(shelfAsset, shelves, "East Bookshelf", new Vector3(15.5f, 0, -4), -90);
        AddShelf(shelfAsset, shelves, "West Rear Bookshelf", new Vector3(-11.5f, 0, 11.5f), 180);
        AddShelf(shelfAsset, shelves, "East Rear Bookshelf", new Vector3(11.5f, 0, 11.5f), 180);
        Physics.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Proof artwork replaced; original book, altars, scrolls, board text and gameplay references retained.");
    }

    static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new InvalidOperationException("Missing artwork: " + path);
    static Transform Instantiate(GameObject asset, Transform parent, string name)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        Undo.RegisterCreatedObjectUndo(go, "Add " + name); go.name = name; return go.transform;
    }
    static void Remove(Transform obj) { if (obj != null) Undo.DestroyObjectImmediate(obj.gameObject); }
    static Bounds BoundsOf(Transform model)
    {
        var renderers = model.GetComponentsInChildren<Renderer>(); var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }
    static void FitCollider(GameObject obj, Bounds bounds)
    {
        var collider = obj.GetComponent<BoxCollider>();
        if (collider == null) collider = obj.AddComponent<BoxCollider>();
        collider.center = obj.transform.InverseTransformPoint(bounds.center);
        // Board/shelf holders have unit scale and only rotations about Y.
        var size = obj.transform.InverseTransformVector(bounds.size);
        collider.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
    }
    static void AddShelf(GameObject asset, Transform parent, string name, Vector3 point, float yaw)
    {
        var holder = new GameObject(name).transform; holder.SetParent(parent, false);
        holder.SetPositionAndRotation(point, Quaternion.Euler(0, yaw, 0));
        var model = Instantiate(asset, holder, "Garden Bookshelf Model");
        model.localPosition = Vector3.zero; model.localRotation = Quaternion.identity;
        model.localScale = Vector3.one * .38f;
        var bounds = BoundsOf(model);
        model.position += new Vector3(point.x - bounds.center.x, point.y - bounds.min.y, point.z - bounds.center.z);
        FitCollider(holder.gameObject, BoundsOf(model));
    }
}
#endif
