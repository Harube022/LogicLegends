using UnityEngine;
using UnityEngine.UI;

/// <summary>Capsule stencil clips the original painted slider fill without replacing its artwork.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class RoundedSliderMask : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vertices)
    {
        vertices.Clear();
        var rect = rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f) return;
        float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
        const int arcSteps = 16;
        vertices.AddVert(rect.center, color, Vector2.zero);
        for (int side = 0; side < 2; side++)
        {
            Vector2 center = new Vector2(side == 0 ? rect.xMax - radius : rect.xMin + radius, rect.center.y);
            for (int step = 0; step <= arcSteps; step++)
            {
                float angle = (-Mathf.PI * 0.5f + side * Mathf.PI) + step * Mathf.PI / arcSteps;
                Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                vertices.AddVert(point, color, Vector2.zero);
            }
        }
        int count = 2 * (arcSteps + 1);
        for (int index = 1; index <= count; index++) vertices.AddTriangle(0, index, index == count ? 1 : index + 1);
    }
}