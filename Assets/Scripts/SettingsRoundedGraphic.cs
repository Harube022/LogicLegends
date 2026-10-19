using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

/// <summary>Settings-local capsule with a one-screen-pixel antialiased edge.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class SettingsRoundedGraphic : MaskableGraphic
{
    public Sprite surface;
    public bool roundLeft = true;
    public bool roundRight = true;
    public bool pillShape = true;
    [Min(0)] public float cornerRadius = 12f;
    public override Texture mainTexture => surface ? surface.texture : base.mainTexture;

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect r = rectTransform.rect;
        if (r.width <= 0 || r.height <= 0) return;
        float maximumRadius = Mathf.Min(r.width, r.height) * .5f;
        float radius = pillShape ? maximumRadius : Mathf.Min(cornerRadius, maximumRadius);
        float feather = 1f / Mathf.Max(.1f, canvas ? canvas.scaleFactor : 1f);
        int steps = Mathf.Clamp(Mathf.CeilToInt(radius / feather * .5f), 12, 32);
        var points = new List<Vector2>();
        var normals = new List<Vector2>();
        for (int corner = 0; corner < 4; corner++)
        {
            bool right = corner < 2;
            bool top = corner == 1 || corner == 2;
            float cornerSize = (right ? roundRight : roundLeft) ? radius : 0;
            if (cornerSize <= 0)
            {
                points.Add(new Vector2(right ? r.xMax : r.xMin, top ? r.yMax : r.yMin));
                normals.Add(new Vector2(right ? 1 : -1, top ? 1 : -1));
                continue;
            }
            var center = new Vector2(right ? r.xMax - cornerSize : r.xMin + cornerSize,
                top ? r.yMax - cornerSize : r.yMin + cornerSize);
            for (int step = 0; step <= steps; step++)
            {
                float angle = -Mathf.PI * .5f + corner * Mathf.PI * .5f + step * Mathf.PI * .5f / steps;
                var normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                points.Add(center + normal * cornerSize); normals.Add(normal);
            }
        }
        Vector4 uv = surface ? DataUtility.GetOuterUV(surface) : new Vector4(0, 0, 1, 1);
        AddVertex(mesh, r.center, r, uv, color);
        foreach (var point in points) AddVertex(mesh, point, r, uv, color);
        Color clear = color; clear.a = 0;
        for (int i = 0; i < points.Count; i++) AddVertex(mesh, points[i] + normals[i] * feather, r, uv, clear);
        for (int i = 0; i < points.Count; i++)
        {
            int a = i + 1, b = (i + 1) % points.Count + 1;
            mesh.AddTriangle(0, a, b);
            mesh.AddTriangle(a, a + points.Count, b + points.Count);
            mesh.AddTriangle(a, b + points.Count, b);
        }
    }

    private static void AddVertex(VertexHelper mesh, Vector2 point, Rect rect, Vector4 uv, Color tint)
    {
        mesh.AddVert(point, tint, new Vector2(
            Mathf.Lerp(uv.x, uv.z, Mathf.InverseLerp(rect.xMin, rect.xMax, point.x)),
            Mathf.Lerp(uv.y, uv.w, Mathf.InverseLerp(rect.yMin, rect.yMax, point.y))));
    }
}
