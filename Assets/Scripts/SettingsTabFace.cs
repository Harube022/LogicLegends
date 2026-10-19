using UnityEngine;
using UnityEngine.UI;

/// <summary>Fills the clipped inner face of the existing wooden tab frame.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class SettingsTabFace : MaskableGraphic
{
    [SerializeField, Min(0)] private float cornerSize = 5.8f;
    [SerializeField, Range(0, 2)] private float edgeSoftness = 1f;
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect r = rectTransform.rect;
        if (r.width <= 0 || r.height <= 0) return;
        float corner = Mathf.Clamp(cornerSize, .001f, Mathf.Max(.001f, Mathf.Min(r.width, r.height) * .499f));
        var points = new[]
        {
            new Vector2(r.xMin + corner, r.yMax), new Vector2(r.xMax - corner, r.yMax),
            new Vector2(r.xMax, r.yMax - corner), new Vector2(r.xMax, r.yMin + corner),
            new Vector2(r.xMax - corner, r.yMin), new Vector2(r.xMin + corner, r.yMin),
            new Vector2(r.xMin, r.yMin + corner), new Vector2(r.xMin, r.yMax - corner)
        };
        mesh.AddVert(r.center, color, Vector2.zero);
        foreach (var point in points) mesh.AddVert(point, color, Vector2.zero);
        float feather = edgeSoftness / Mathf.Max(.1f, canvas ? canvas.scaleFactor : 1f);
        Color clear = color; clear.a = 0;
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 before = (points[i] - points[(i + 7) % 8]).normalized;
            Vector2 after = (points[(i + 1) % 8] - points[i]).normalized;
            Vector2 n1 = new Vector2(-before.y, before.x), n2 = new Vector2(-after.y, after.x);
            Vector2 outward = (n1 + n2).normalized;
            mesh.AddVert(points[i] + outward * (feather / Vector2.Dot(outward, n1)), clear, Vector2.zero);
        }
        for (int i = 1; i <= 8; i++) mesh.AddTriangle(0, i, i == 8 ? 1 : i + 1);
        for (int i = 1; i <= 8; i++)
        {
            int next = i == 8 ? 1 : i + 1;
            mesh.AddTriangle(i, i + 8, next + 8); mesh.AddTriangle(i, next + 8, next);
        }
    }
}
