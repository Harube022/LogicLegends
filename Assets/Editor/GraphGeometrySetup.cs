using System;
using System.Linq;
using LogicLegends.Graph;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Updates only Graph world dimensions; shared UI and source models stay intact.</summary>
public static class GraphGeometrySetup
{
    [MenuItem("Tools/Logic Legends/Apply Graph Object Sizes")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Graph.unity") throw new InvalidOperationException("Open Graph first.");
        var stage = UnityEngine.Object.FindFirstObjectByType<GraphStageManager>();
        ResizePrefab(stage.vertexPrefab.gameObject, Vector3.one * GraphGeometry.VertexScale);
        ResizePrefab(stage.edgePrefab.gameObject, GraphGeometry.EdgeScale);
        var book = stage.bookStatue;
        var renderers = book.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        book.localScale *= (1.9f * GraphGeometry.VertexScale) / bounds.size.y;
        book.position = stage.arena.TransformPoint(GraphGeometry.BookOffset);
        PrefabUtility.RecordPrefabInstancePropertyModifications(book);
        var chamber = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Graph Chamber");
        if (chamber != null)
        {
            chamber.transform.localScale = new Vector3(GraphGeometry.LayoutScale, GraphGeometry.VertexScale, GraphGeometry.LayoutScale);
            foreach (var light in chamber.GetComponentsInChildren<Light>()) light.range = 12f * GraphGeometry.VertexScale;
        }
        var camera = scene.GetRootGameObjects().Select(g => g.GetComponent<Unity.Cinemachine.CinemachineThirdPersonFollow>()).FirstOrDefault(c => c != null);
        if (camera != null) { camera.CameraDistance = 18f; EditorUtility.SetDirty(camera); }
        foreach (Transform child in stage.arena.Cast<Transform>().ToArray())
            if (child.GetComponent<GraphVertex>() != null || child.GetComponent<GraphEdge>() != null)
                Undo.DestroyObjectImmediate(child.gameObject);
        var challenge = GraphGeometry.Layout(stage.catalog.challenges[0]);
        foreach (var vertex in challenge.vertices)
        {
            var view = (GraphVertex)PrefabUtility.InstantiatePrefab(stage.vertexPrefab, stage.arena);
            view.Configure(vertex); PrefabUtility.RecordPrefabInstancePropertyModifications(view.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(view.label);
        }
        foreach (var edge in challenge.edges)
        {
            var view = (GraphEdge)PrefabUtility.InstantiatePrefab(stage.edgePrefab, stage.arena);
            view.Configure(edge, challenge.Vertex(edge.source).position, challenge.Vertex(edge.destination).position);
            foreach (var transform in view.GetComponentsInChildren<Transform>(true))
                PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
        }
        stage.spawnPoint.position = stage.arena.TransformPoint(challenge.Vertex(challenge.start).position) + Vector3.up * .15f;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Graph object sizes saved: vertex 2.28 uniform, edge 2.5 / 2 / 4, matching book and connected walk geometry.");
    }
    private static void ResizePrefab(GameObject prefab, Vector3 scale)
    {
        string path = AssetDatabase.GetAssetPath(prefab);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.transform.localScale = scale;
            var vertex = root.GetComponent<GraphVertex>();
            if (vertex != null && vertex.label != null)
            {
                vertex.label.transform.localPosition = Vector3.up * (GraphGeometry.LabelHeight / GraphGeometry.VertexScale);
                vertex.label.transform.localScale = Vector3.one * (GraphGeometry.LabelScale / GraphGeometry.VertexScale);
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
