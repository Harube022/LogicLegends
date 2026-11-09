using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Small static UI seal; no transparent particle layers or texture allocations.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ArchiveResultEmblemGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var rect=GetPixelAdjustedRect();var c=rect.center;
            float radius=Mathf.Min(rect.width,rect.height)*.46f;
            Ring(mesh,c,radius,2.5f);Ring(mesh,c,radius*.77f,1.7f);
            for(int i=0;i<10;i++)
            {
                float angle=(i*36+90)*Mathf.Deg2Rad;
                var axis=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));var side=new Vector2(-axis.y,axis.x);
                Segment(mesh,c+axis*radius*.84f-side*3,c+axis*radius*.96f,2);
                Segment(mesh,c+axis*radius*.96f,c+axis*radius*.84f+side*3,2);
            }
            // The arch and central rising spark echo the team's escape portal.
            float w=radius*.34f,h=radius*.47f;
            Segment(mesh,c+new Vector2(-w,-h),c+new Vector2(-w,h*.28f),4);
            Segment(mesh,c+new Vector2(-w,h*.28f),c+new Vector2(0,h),4);
            Segment(mesh,c+new Vector2(0,h),c+new Vector2(w,h*.28f),4);
            Segment(mesh,c+new Vector2(w,h*.28f),c+new Vector2(w,-h),4);
            Segment(mesh,c+new Vector2(-w*1.4f,-h),c+new Vector2(w*1.4f,-h),3);
        }
        void Ring(VertexHelper mesh,Vector2 c,float radius,float width)
        {
            for(int i=0;i<48;i++)
            {
                float a=i*Mathf.PI/24,b=(i+1)*Mathf.PI/24;
                Segment(mesh,c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,c+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,width);
            }
        }
        void Segment(VertexHelper mesh,Vector2 a,Vector2 b,float width)
        {
            Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;int i=mesh.currentVertCount;
            mesh.AddVert(a-n,color,Vector2.zero);mesh.AddVert(a+n,color,Vector2.zero);
            mesh.AddVert(b+n,color,Vector2.zero);mesh.AddVert(b-n,color,Vector2.zero);
            mesh.AddTriangle(i,i+1,i+2);mesh.AddTriangle(i,i+2,i+3);
        }
    }
}
