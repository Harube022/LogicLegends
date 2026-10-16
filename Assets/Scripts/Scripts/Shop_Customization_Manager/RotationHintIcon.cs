using UnityEngine;
using UnityEngine.UI;

/// <summary>A small, font-free circular arrow used beside rotation instructions.</summary>
public sealed class RotationHintIcon : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        float size = Mathf.Min(rectTransform.rect.width,rectTransform.rect.height);
        if (size <= 0f) return;
        float outer = size*.43f;
        float inner = size*.27f;
        const int segments = 32;
        const float start = 42f;
        const float end = 318f;
        int first = 0;
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Lerp(start,end,i/(float)segments)*Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
            vh.AddVert(direction*outer,color,Vector2.zero);
            vh.AddVert(direction*inner,color,Vector2.zero);
            if (i > 0)
            {
                int current = first+i*2;
                int previous = current-2;
                vh.AddTriangle(previous,current,previous+1);
                vh.AddTriangle(current,current+1,previous+1);
            }
        }

        float endAngle = end*Mathf.Deg2Rad;
        Vector2 radial = new Vector2(Mathf.Cos(endAngle),Mathf.Sin(endAngle));
        Vector2 tangent = new Vector2(-radial.y,radial.x);
        Vector2 baseOuter = radial*outer;
        Vector2 baseInner = radial*inner;
        Vector2 tip = radial*((outer+inner)*.5f)+tangent*(outer-inner)*1.6f;
        int arrow = vh.currentVertCount;
        vh.AddVert(baseOuter,color,Vector2.zero);
        vh.AddVert(tip,color,Vector2.zero);
        vh.AddVert(baseInner,color,Vector2.zero);
        vh.AddTriangle(arrow,arrow+1,arrow+2);
    }
}
