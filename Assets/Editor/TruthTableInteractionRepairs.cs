#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Scene-only collision changes. Imported stones, house meshes and door references stay intact.</summary>
public static class TruthTableInteractionRepairs
{
    public const string Folder = "Assets/Art/TruthTableCollision";
    [MenuItem("Logic Legends/Truth Table/Repair Paths and House Stairs")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "TruthTable" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Open TruthTable in Edit Mode.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Art", "TruthTableCollision");
        foreach (var path in UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(c => c.name.StartsWith("Pathway", StringComparison.Ordinal) || c.name == "PATH1"))
        {
            if (path.attachedRigidbody != null) throw new InvalidOperationException("Expected static path geometry.");
            Undo.RecordObject(path, "Match paver collision to visible stones");
            path.convex = false; path.sharedMesh = path.GetComponent<MeshFilter>().sharedMesh;
            PrefabUtility.RecordPrefabInstancePropertyModifications(path);
        }
        var interaction = UnityEngine.Object.FindFirstObjectByType<TruthTableDoorInteraction>(FindObjectsInactive.Include);
        var doors = (Collider[])typeof(TruthTableDoorInteraction).GetField("doors", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(interaction);
        var group = GameObject.Find("TruthTable Collision Repairs");
        if (group == null) { group = new GameObject("TruthTable Collision Repairs"); Undo.RegisterCreatedObjectUndo(group, "Fit house entrance ramps"); }
        var access = group.GetComponent<TruthTableStairAccess>(); if (access == null) access = Undo.AddComponent<TruthTableStairAccess>(group);
        var ramps = new List<MeshCollider>();
        Physics.SyncTransforms();
        for (int i = 0; i < doors.Length; i++)
        {
            var door = doors[i]; var house = door.transform.parent;
            var body = house.GetComponentsInChildren<Renderer>(true).First(r => r.name == "Cylinder.087");
            var stairs = house.GetComponentsInChildren<MeshCollider>(true).First(c => c.name == "Cube.183");
            var outward = door.bounds.center - body.bounds.center; outward.y = 0; outward.Normalize();
            var right = Vector3.Cross(Vector3.up, outward);
            // Survey the existing step surfaces along the middle of this entrance.
            var samples = new List<Vector2>();
            for (float distance = 0; distance <= 22; distance += .1f)
            {
                var point = door.bounds.center + outward * distance; RaycastHit hit;
                if (stairs.Raycast(new Ray(new Vector3(point.x, door.bounds.max.y + 3, point.z), Vector3.down), out hit, 30))
                    samples.Add(new Vector2(distance, hit.point.y));
            }
            float top = samples.First(p => p.x >= 2).y;
            float start = samples.Where(p => p.y >= top - .02f).Max(p => p.x) - .08f;
            float end = samples.Max(p => p.x) + .06f;
            float bottom = samples.Last().y;
            float middle = (start + end) * .5f;
            float width = 0;
            var center = door.bounds.center + outward * middle;
            RaycastHit centerHit; stairs.Raycast(new Ray(new Vector3(center.x, door.bounds.max.y + 3, center.z), Vector3.down), out centerHit, 30);
            // Stay within the actual tread width, rather than the bounds of the whole house base.
            for (float side = 0; side <= 6; side += .1f)
            {
                bool valid = true;
                foreach (float sign in new[] { -1f, 1f })
                {
                    var p = center + right * side * sign; RaycastHit h;
                    valid &= stairs.Raycast(new Ray(new Vector3(p.x, door.bounds.max.y + 3, p.z), Vector3.down), out h, 30) && Mathf.Abs(h.point.y - centerHit.point.y) < .35f;
                }
                if (!valid) break; width = side;
            }
            width = Mathf.Max(1, width - .08f);
            var existing = group.transform.Find("House " + (i + 1) + " Fitted Stair Ramp");
            var ramp = existing != null ? existing.GetComponent<MeshCollider>() : null;
            if (ramp == null) { ramp = new GameObject("House " + (i + 1) + " Fitted Stair Ramp").AddComponent<MeshCollider>(); ramp.transform.SetParent(group.transform, false); Undo.RegisterCreatedObjectUndo(ramp.gameObject, "Add fitted stair collider"); }
            Undo.RecordObject(ramp.transform, "Align with existing entrance");
            ramp.transform.SetPositionAndRotation(new Vector3(door.bounds.center.x, 0, door.bounds.center.z), Quaternion.LookRotation(outward));
            ramp.transform.localScale = Vector3.one;
            // An upper envelope follows the unequal risers. A single diagonal
            // intersects the taller middle tread and leaves an invisible wall.
            var profile = new List<Vector2> { new Vector2(start, top + .035f) };
            profile.AddRange(samples.Where(p => p.x >= start && p.x + .08f < end)
                .Select(p => new Vector2(p.x + .08f, p.y + .035f)));
            profile.Add(new Vector2(end, bottom + .025f));
            var hull = new List<Vector2>();
            foreach (var point in profile.OrderBy(p => p.x))
            {
                while (hull.Count >= 2)
                {
                    Vector2 a = hull[hull.Count - 1] - hull[hull.Count - 2], b = point - hull[hull.Count - 1];
                    if (a.x * b.y - a.y * b.x < 0) break;
                    hull.RemoveAt(hull.Count - 1);
                }
                hull.Add(point);
            }
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int section = 0; section < hull.Count - 1; section++)
            {
                var a = hull[section]; var b = hull[section + 1]; int n = vertices.Count;
                vertices.AddRange(new[] { new Vector3(-width,a.y,a.x), new Vector3(width,a.y,a.x),
                    new Vector3(-width,b.y,b.x), new Vector3(width,b.y,b.x),
                    new Vector3(-width,bottom-.15f,a.x), new Vector3(width,bottom-.15f,a.x),
                    new Vector3(-width,bottom-.15f,b.x), new Vector3(width,bottom-.15f,b.x) });
                triangles.AddRange(new[] { 0,2,1, 1,2,3, 4,5,6, 5,7,6, 0,1,4, 1,5,4, 2,6,3, 3,6,7, 0,4,2, 2,4,6, 1,3,5, 3,7,5 }.Select(t=>t+n));
            }
            string path = Folder + "/HouseEntranceRamp_" + (i + 1) + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh { name = "House " + (i + 1) + " fitted entrance" }; AssetDatabase.CreateAsset(mesh, path); }
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
            Undo.RecordObject(ramp, "Fit stair collision"); ramp.sharedMesh = null; ramp.sharedMesh = mesh; ramp.convex = false; ramps.Add(ramp);
        }
        Undo.RecordObject(access, "Bind six entrance slopes"); access.ramps = ramps.ToArray();
        AssetDatabase.SaveAssets(); Physics.SyncTransforms(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
    }
}
#endif
