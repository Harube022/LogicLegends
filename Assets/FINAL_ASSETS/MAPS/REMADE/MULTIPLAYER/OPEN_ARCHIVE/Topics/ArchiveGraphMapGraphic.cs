using System;
using LogicLegends.Graph;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>One lightweight, read-only UI mesh. It cannot submit or edit route progress.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ArchiveGraphMapGraphic:MaskableGraphic
    {
        public ArchiveGraphMapGraphic(){useLegacyMeshGeneration=false;}
        ArchiveQuestion question;ArchiveStationState state;Vector2 runner;bool hasRunner;
        public void Show(ArchiveQuestion q,ArchiveStationState s,Vector3 position,bool showRunner){question=q;state=s;runner=new Vector2(position.x,position.z)-ArchiveTopicStation.GraphOffset;hasRunner=showRunner;SetVerticesDirty();}
        public Vector2 MapPoint(Vector2 p){var r=rectTransform.rect;float scale=Mathf.Min(r.width,r.height)/13f;return r.center+p*scale;}
        protected override void OnPopulateMesh(VertexHelper vh){vh.Clear();if(question==null||state==null)return;var q=question;
            for(int i=0;i<q.graph.edges.Count;i++){var e=q.graph.edges[i];if(!e.traversalEnabled)continue;int a=Array.IndexOf(q.nodeIds,e.source),b=Array.IndexOf(q.nodeIds,e.destination);if(a<0||b<0)continue;var from=MapPoint(q.nodes[a]);var to=MapPoint(q.nodes[b]);var c=ArchiveGraphPresentation.Traversed(q,state,i)?ArchiveGraphPresentation.Walked:ArchiveGraphPresentation.Available;Line(vh,from,to,5,c);if(e.directed){var dir=(to-from).normalized;var center=Vector2.Lerp(from,to,.60f);var side=new Vector2(-dir.y,dir.x)*7;Triangle(vh,center+dir*9,center-dir*7+side,center-dir*7-side,c);}}
            for(int i=0;i<q.nodes.Length;i++){string id=q.nodeIds[i];Color c=q.graph.type==GraphChallengeType.Degree?(id==q.graph.degreeVertex?ArchiveGraphPresentation.FocusColor:ArchiveGraphPresentation.Available):id==q.graph.start?ArchiveGraphPresentation.StartColor:id==q.graph.destination?ArchiveGraphPresentation.GoalColor:ArchiveGraphPresentation.Available;if(state.route.Contains(i))c=state.continuous?ArchiveGraphPresentation.Walked:ArchiveGraphPresentation.Invalid;Disk(vh,MapPoint(q.nodes[i]),14,c);Disk(vh,MapPoint(q.nodes[i]),10,new Color(.035f,.12f,.14f));}
            if(hasRunner){var at=MapPoint(runner);Disk(vh,at,6,Color.white);Disk(vh,at,3,ArchiveGraphPresentation.Walked);}}
        static void Line(VertexHelper v,Vector2 a,Vector2 b,float width,Color c){var dir=(b-a).normalized;var side=new Vector2(-dir.y,dir.x)*width*.5f;int start=v.currentVertCount;v.AddVert(a-side,c,Vector2.zero);v.AddVert(a+side,c,Vector2.zero);v.AddVert(b+side,c,Vector2.zero);v.AddVert(b-side,c,Vector2.zero);v.AddTriangle(start,start+1,start+2);v.AddTriangle(start,start+2,start+3);}
        static void Triangle(VertexHelper v,Vector2 a,Vector2 b,Vector2 d,Color c){int start=v.currentVertCount;v.AddVert(a,c,Vector2.zero);v.AddVert(b,c,Vector2.zero);v.AddVert(d,c,Vector2.zero);v.AddTriangle(start,start+1,start+2);}
        static void Disk(VertexHelper v,Vector2 at,float radius,Color c){for(int i=0;i<12;i++){float a=i*Mathf.PI/6,b=(i+1)*Mathf.PI/6;Triangle(v,at,at+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,at+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,c);}}
    }
}
