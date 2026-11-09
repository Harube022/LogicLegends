using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Lightweight shared HUD frame with clear content insets, without parchment artwork.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ArchiveHudCardGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var rect=GetPixelAdjustedRect();
            Polygon(mesh,rect,7,new Color(.13f,.09f,.055f,.98f));
            Polygon(mesh,Inset(rect,1.2f),6,new Color(.62f,.49f,.28f,.96f));
            Polygon(mesh,Inset(rect,2.4f),5,new Color(.025f,.105f,.12f,.97f));
        }
        static Rect Inset(Rect r,float n)=>new Rect(r.x+n,r.y+n,Mathf.Max(0,r.width-2*n),Mathf.Max(0,r.height-2*n));
        void Polygon(VertexHelper mesh,Rect r,float cut,Color tint)
        {
            cut=Mathf.Min(cut,Mathf.Min(r.width,r.height)*.25f);int first=mesh.currentVertCount;
            Color32 c=tint*color;
            mesh.AddVert(r.center,c,Vector2.zero);
            mesh.AddVert(new Vector2(r.xMin+cut,r.yMin),c,Vector2.zero);
            mesh.AddVert(new Vector2(r.xMax-cut,r.yMin),c,Vector2.zero);
            mesh.AddVert(new Vector2(r.xMax,r.yMin+cut),c,Vector2.zero);
            mesh.AddVert(new Vector2(r.xMax,r.yMax-cut),c,Vector2.zero);
            mesh.AddVert(new Vector2(r.xMax-cut,r.yMax),c,Vector2.zero);
            mesh.AddVert(new Vector2(r.xMin+cut,r.yMax),c,Vector2.zero);
            mesh.AddVert(new Vector2(r.xMin,r.yMax-cut),c,Vector2.zero);
            mesh.AddVert(new Vector2(r.xMin,r.yMin+cut),c,Vector2.zero);
            for(int i=0;i<8;i++)mesh.AddTriangle(first,first+i+1,first+(i+1)%8+1);
        }
    }
}
