#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>A scene-only walking surface sampled from the visible front stair treads.</summary>
public static class TruthTablePlatformStairsSetup
{
    public const string ObjectName = "Truth Table Platform Stair Ramp";
    public const string AssetPath = "Assets/Art/TruthTableCollision/TruthTablePlatformRamp.asset";
    struct Tread { public bool valid; public Vector3 inner, outer; }

    [MenuItem("Logic Legends/Truth Table/Fit Platform Stair Collision")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "TruthTable" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Open TruthTable in Edit Mode.");
        var table = GameObject.Find("TRUTIBOL").GetComponent<MeshCollider>();
        if (table.convex || table.sharedMesh != table.GetComponent<MeshFilter>().sharedMesh)
            throw new InvalidOperationException("Expected the original precise table mesh collision.");
        Physics.SyncTransforms();
        var bounds = table.bounds;
        float front = bounds.max.z + .1f, back = bounds.max.z - 5.5f;
        float ceiling = bounds.min.y + 6;
        var middle = new List<float>();
        for (float z = front; z >= back; z -= .025f)
        { RaycastHit hit; if (Sample(table, ceiling, bounds.center.x, z, out hit)) middle.Add(hit.point.y); }
        float lower = middle.Min(), upper = middle.Max();
        if (upper - lower < 1 || upper - lower > 2) throw new InvalidOperationException("Unexpected stair profile.");
        var vertices = new List<Vector3>(); var indices = new List<int>();
        Tread previous = default;
        for (float x = bounds.min.x; x <= bounds.max.x; x += .2f)
        {
            var tread = new Tread(); bool low = false, high = false;
            for (float z = front; z >= back && (!low || !high); z -= .025f)
            {
                RaycastHit hit; if (!Sample(table, ceiling, x, z, out hit)) continue;
                if (!low && Mathf.Abs(hit.point.y - lower) < .08f)
                { tread.outer = new Vector3(x, hit.point.y + .025f, z - .035f); low = true; }
                if (!high && Mathf.Abs(hit.point.y - upper) < .08f)
                { tread.inner = new Vector3(x, hit.point.y + .065f, z - .05f); high = true; }
            }
            tread.valid = low && high && tread.inner.z > front - 3 && tread.outer.z - tread.inner.z > 1;
            if (previous.valid && tread.valid && Covered(table, ceiling, previous, tread))
            {
                int n = vertices.Count;
                vertices.AddRange(new[] { previous.inner, tread.inner, previous.outer, tread.outer });
                indices.AddRange(new[] { n, n+2, n+1, n+1, n+2, n+3 });
            }
            previous = tread;
        }
        if (vertices.Count < 100) throw new InvalidOperationException("No continuous front stair surface found.");
        var footprint = new Bounds(vertices[0], Vector3.zero); foreach (var point in vertices) footprint.Encapsulate(point);
        var origin = new Vector3(footprint.center.x, 0, footprint.center.z);
        if (!AssetDatabase.IsValidFolder(TruthTableInteractionRepairs.Folder)) AssetDatabase.CreateFolder("Assets/Art", "TruthTableCollision");
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(AssetPath);
        if (mesh == null) { mesh = new Mesh { name = "Truth Table fitted front stair surface" }; AssetDatabase.CreateAsset(mesh, AssetPath); }
        Undo.RecordObject(mesh, "Fit table stair collision"); mesh.Clear();
        mesh.SetVertices(vertices.Select(p => p-origin).ToList()); mesh.SetTriangles(indices, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
        var parent = GameObject.Find("TruthTable Collision Repairs");
        if (parent == null) { parent = new GameObject("TruthTable Collision Repairs"); Undo.RegisterCreatedObjectUndo(parent, "Organize stair collision"); }
        var child = parent.transform.Find(ObjectName);
        if (child == null)
        { child = new GameObject(ObjectName).transform; child.SetParent(parent.transform, false); Undo.RegisterCreatedObjectUndo(child.gameObject, "Add table stair collision"); }
        Undo.RecordObject(child, "Align with stair footprint"); child.SetPositionAndRotation(origin, Quaternion.identity); child.localScale = Vector3.one;
        var collider = child.GetComponent<MeshCollider>(); if (collider == null) collider = Undo.AddComponent<MeshCollider>(child.gameObject);
        Undo.RecordObject(collider, "Use fitted stair mesh"); collider.sharedMesh = null; collider.sharedMesh = mesh;
        collider.convex = false; collider.isTrigger = false;
        // This shallow ramp works with the original 45-degree slope / 1-unit step settings.
        // No renderer, broad box collider, or gameplay reference is replaced.
        AssetDatabase.SaveAssets(); Physics.SyncTransforms(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
    }
    static bool Sample(MeshCollider table, float ceiling, float x, float z, out RaycastHit hit)
        => table.Raycast(new Ray(new Vector3(x, ceiling, z), Vector3.down), out hit, 10);
    static bool Covered(MeshCollider table, float ceiling, Tread a, Tread b)
    {
        // Keep each strip on real treads, including the irregular stone edges.
        for (int side = 0; side <= 4; side++)
            for (int depth = 0; depth <= 4; depth++)
            {
                var inner = Vector3.Lerp(a.inner, b.inner, side / 4f);
                var outer = Vector3.Lerp(a.outer, b.outer, side / 4f);
                var point = Vector3.Lerp(inner, outer, depth / 4f); RaycastHit hit;
                if (!Sample(table, ceiling, point.x, point.z, out hit) || hit.point.y > point.y + .01f) return false;
            }
        return true;
    }
}
#endif
