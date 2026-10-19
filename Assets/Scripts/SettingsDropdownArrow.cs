using UnityEngine;
using UnityEngine.UI;

/// <summary>Small editable UI chevron, independent of a font glyph.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class SettingsDropdownArrow : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect r = rectTransform.rect;
        mesh.AddVert(new Vector3(r.xMin, r.yMax), color, Vector2.zero);
        mesh.AddVert(new Vector3(r.xMax, r.yMax), color, Vector2.zero);
        mesh.AddVert(new Vector3(r.center.x, r.yMin), color, Vector2.zero);
        mesh.AddTriangle(0, 1, 2);
    }
}
