using System;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ArchiveTreeMapGraphic:MaskableGraphic
    {
        public ArchiveTreeMapGraphic(){useLegacyMeshGeneration=false;}
        ArchiveQuestion question;ArchiveStationState state;
        public void Show(ArchiveQuestion q,ArchiveStationState s){question=q;state=s;SetVerticesDirty();}
        public Vector2 MapPoint(Vector2 p){var r=rectTransform.rect;return r.center+p*(Mathf.Min(r.width,r.height)/12.5f);}
        protected override void OnPopulateMesh(VertexHelper vh){vh.Clear();if(question==null||state==null)return;var q=question;foreach(int link in ArchiveTreePresentation.VisibleLinks(q,state)){if(link<0||link/100>=q.nodes.Length||link%100>=q.nodes.Length)continue;var a=MapPoint(q.nodes[link/100]);var b=MapPoint(q.nodes[link%100]);var d=(b-a).normalized;var side=new Vector2(-d.y,d.x)*2;int at=vh.currentVertCount;vh.AddVert(a-side,ArchiveTreePresentation.Green,Vector2.zero);vh.AddVert(a+side,ArchiveTreePresentation.Green,Vector2.zero);vh.AddVert(b+side,ArchiveTreePresentation.Green,Vector2.zero);vh.AddVert(b-side,ArchiveTreePresentation.Green,Vector2.zero);vh.AddTriangle(at,at+1,at+2);vh.AddTriangle(at,at+2,at+3);if(q.tree.Directed){var center=Vector2.Lerp(a,b,.56f);Triangle(vh,center+d*8,center-d*6+new Vector2(-d.y,d.x)*6,center-d*6-new Vector2(-d.y,d.x)*6,ArchiveTreePresentation.Gold);}}}
        static void Triangle(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Color color){int n=vh.currentVertCount;vh.AddVert(a,color,Vector2.zero);vh.AddVert(b,color,Vector2.zero);vh.AddVert(c,color,Vector2.zero);vh.AddTriangle(n,n+1,n+2);}
    }
}
