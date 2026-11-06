using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

/// <summary>Uses the existing amber artwork with rounded, antialiased moving ends.</summary>
[AddComponentMenu("UI/Logic Legends/Rounded Loading Fill")]
public sealed class RoundedLoadingFill : Image
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect full = GetPixelAdjustedRect();
        if (sprite == null || fillAmount <= 0f || full.height <= 0f) return;
        Rect filled = full;
        filled.width *= fillAmount;
        float radius = Mathf.Min(filled.height, filled.width) * .5f;
        float feather = Mathf.Min(radius, 1f / Mathf.Max(.1f, rectTransform.lossyScale.x));
        Vector4 uv = DataUtility.GetOuterUV(sprite);
        Add(mesh, filled.center, full, uv, color);
        const int segments = 12, points = 4 * (segments + 1);
        for (int corner = 0; corner < 4; corner++)
        {
            Vector2 center = new Vector2(corner == 0 || corner == 3 ? filled.xMax - radius : filled.xMin + radius,
                corner < 2 ? filled.yMax - radius : filled.yMin + radius);
            for (int step = 0; step <= segments; step++)
            {
                float angle = (corner * 90f + step * 90f / segments) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Add(mesh, center + direction * (radius - feather), full, uv, color);
                Color transparent = color; transparent.a = 0f;
                Add(mesh, center + direction * radius, full, uv, transparent);
            }
        }
        for (int i = 0; i < points; i++)
        {
            int next = (i + 1) % points;
            int inner = 1 + 2 * i, outer = inner + 1, nextInner = 1 + 2 * next, nextOuter = nextInner + 1;
            mesh.AddTriangle(0, inner, nextInner);
            mesh.AddTriangle(inner, outer, nextOuter);
            mesh.AddTriangle(inner, nextOuter, nextInner);
        }
    }

    static void Add(VertexHelper mesh, Vector2 point, Rect full, Vector4 uv, Color tint)
    {
        var vertex = UIVertex.simpleVert; vertex.position = point; vertex.color = tint;
        vertex.uv0 = new Vector2(Mathf.Lerp(uv.x, uv.z, Mathf.InverseLerp(full.xMin, full.xMax, point.x)),
            Mathf.Lerp(uv.y, uv.w, Mathf.InverseLerp(full.yMin, full.yMax, point.y)));
        mesh.AddVert(vertex);
    }
}
