using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Filled outer boundary of an existing circular placement mesh.</summary>
internal sealed class SetssssPlacementFootprint
{
    private readonly List<Vector2> boundary = new List<Vector2>();
    private Bounds bounds;

    public SetssssPlacementFootprint(Mesh mesh, Transform target)
    {
        if (mesh == null || target == null) return;
        Vector3[] vertices = mesh.vertices;
        int[] indices = mesh.triangles;
        if (indices.Length == 0) return;
        // These meshes contain tiles and border strips with small gaps. Those
        // gaps are still inside the set: use the outer perimeter, not tile faces.
        Vector2[] points = indices.Distinct().Select(i => target.TransformPoint(vertices[i]))
            .Select(XZ).Distinct().OrderBy(p => p.x).ThenBy(p => p.y).ToArray();
        if (points.Length < 3) return;
        bounds = new Bounds(new Vector3(points[0].x, 0f, points[0].y), Vector3.zero);
        foreach (Vector2 point in points) bounds.Encapsulate(new Vector3(point.x, 0f, point.y));
        foreach (Vector2 point in points)
        {
            while (boundary.Count >= 2 && Cross(boundary[boundary.Count - 1] - boundary[boundary.Count - 2],
                point - boundary[boundary.Count - 1]) <= 0f) boundary.RemoveAt(boundary.Count - 1);
            boundary.Add(point);
        }
        int lowerCount = boundary.Count;
        for (int i = points.Length - 2; i >= 0; i--)
        {
            Vector2 point = points[i];
            while (boundary.Count > lowerCount && Cross(boundary[boundary.Count - 1] - boundary[boundary.Count - 2],
                point - boundary[boundary.Count - 1]) <= 0f) boundary.RemoveAt(boundary.Count - 1);
            boundary.Add(point);
        }
        boundary.RemoveAt(boundary.Count - 1);
    }

    public bool Contains(Vector3 point)
    {
        if (boundary.Count < 3 || point.x < bounds.min.x || point.x > bounds.max.x ||
            point.z < bounds.min.z || point.z > bounds.max.z) return false;
        Vector2 p = XZ(point);
        for (int i = 0; i < boundary.Count; i++)
            if (Cross(boundary[(i + 1) % boundary.Count] - boundary[i], p - boundary[i]) < -.00001f) return false;
        return true;
    }

    private static Vector2 XZ(Vector3 point) => new Vector2(point.x, point.z);
    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
}
