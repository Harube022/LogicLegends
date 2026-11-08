using UnityEngine;
using UnityEngine.UI;

/// <summary>Crops the artwork inside a canvas-sized quad without resizing its UI children.</summary>
[ExecuteAlways, DisallowMultipleComponent]
[AddComponentMenu("UI/Logic Legends/Canvas Background Cover")]
public sealed class CanvasBackgroundCover : BaseMeshEffect
{
    public Rect NormalizedCrop
    {
        get
        {
            var image = graphic as Image;
            var sprite = image != null ? image.overrideSprite : null;
            if (sprite == null && image != null) sprite = image.sprite;
            var texture = graphic.mainTexture;
            float sourceAspect = sprite != null ? sprite.rect.width / sprite.rect.height :
                (float)texture.width / texture.height;
            var size = graphic.rectTransform.rect.size;
            float destinationAspect = size.x / Mathf.Max(1f, size.y);
            float u = Mathf.Min(1f, destinationAspect / sourceAspect);
            float v = Mathf.Min(1f, sourceAspect / destinationAspect);
            return new Rect((1f - u) * .5f, (1f - v) * .5f, u, v);
        }
    }

    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive() || vertices.currentVertCount == 0) return;
        var minimum = new Vector2(float.MaxValue, float.MaxValue);
        var maximum = new Vector2(float.MinValue, float.MinValue);
        var vertex = UIVertex.simpleVert;
        for (int i = 0; i < vertices.currentVertCount; i++)
        {
            vertices.PopulateUIVertex(ref vertex, i);
            minimum = Vector2.Min(minimum, vertex.uv0);
            maximum = Vector2.Max(maximum, vertex.uv0);
        }
        Rect crop = NormalizedCrop;
        for (int i = 0; i < vertices.currentVertCount; i++)
        {
            vertices.PopulateUIVertex(ref vertex, i);
            var uv = vertex.uv0;
            float x = Mathf.InverseLerp(minimum.x, maximum.x, uv.x);
            float y = Mathf.InverseLerp(minimum.y, maximum.y, uv.y);
            uv.x = Mathf.Lerp(minimum.x, maximum.x, crop.x + x * crop.width);
            uv.y = Mathf.Lerp(minimum.y, maximum.y, crop.y + y * crop.height);
            vertex.uv0 = uv;
            vertices.SetUIVertex(vertex, i);
        }
    }
}
