using System;
using LogicLegends.Graph;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Solo's bridge-transaction rules fitted to the open arena; all mutable work stays in the session snapshot.</summary>
    public static class ArchiveGraphTraversal
    {
        public const float VertexRadius=.76f,PathHalfWidth=.48f,HeightLimit=1.9f;
        public static Vector3 Vertex(ArchiveQuestion q,int index)=>new Vector3(q.nodes[index].x,0,q.nodes[index].y)+new Vector3(ArchiveTopicStation.GraphOffset.x,0,ArchiveTopicStation.GraphOffset.y);
        public static int StartIndex(ArchiveQuestion q)=>Array.IndexOf(q.nodeIds,q.graph.start);
        public static bool AtVertex(Vector3 p,Vector3 v,float extra=0)=>Mathf.Abs(p.y-v.y)<=HeightLimit&&new Vector2(p.x-v.x,p.z-v.z).sqrMagnitude<=(VertexRadius+extra)*(VertexRadius+extra);
        public static void Begin(ArchiveStationState s,ArchiveQuestion q,int actor,Vector3 position,double now){s.route.Clear();s.route.Add(StartIndex(q));s.routeActor=actor;s.continuous=true;s.routeReason="";s.routeEdge=-1;s.routeCrossedMiddle=false;s.routeAtVertex=true;s.lastRoutePosition=position;s.routeSampleAt=now;s.routeSampleValid=true;}
        public static void Invalidate(ArchiveStationState s,string reason){s.continuous=false;s.routeAtVertex=false;s.routeEdge=-1;s.routeCrossedMiddle=false;s.routeReason=reason;}
        public static bool OnBridge(Vector3 p,Vector3 a,Vector3 b,float footprint,out float fraction){var ab=b-a;ab.y=0;var ap=p-a;ap.y=0;fraction=Vector3.Dot(ap,ab)/Mathf.Max(.00001f,ab.sqrMagnitude);return fraction>=0&&fraction<=1&&Mathf.Abs(p.y-a.y)<=HeightLimit&&(ap-ab*fraction).sqrMagnitude<=(PathHalfWidth+footprint)*(PathHalfWidth+footprint);}
        public static void Sample(ArchiveStationState s,ArchiveQuestion q,Vector3 position,double now,float footprint=.18f,float speedLimit=16f,bool teleporting=false){
            if(q.graph==null||q.graph.type==GraphChallengeType.Degree||!s.continuous||s.route.Count==0)return;
            if(s.route.AnyIndexOutside(q.nodes.Length)){Invalidate(s,"Invalid route checkpoint. Retry at the start.");return;}
            Vector3 previous=s.lastRoutePosition;float elapsed=Mathf.Clamp((float)(now-s.routeSampleAt),.001f,.6f),distance=Vector2.Distance(new Vector2(previous.x,previous.z),new Vector2(position.x,position.z));
            if(teleporting||s.routeSampleValid&&distance>Mathf.Max(.85f,elapsed*speedLimit)){Invalidate(s,"Movement interrupted. Return to the start to retry.");return;}
            s.routeSampleValid=true;s.lastRoutePosition=position;s.routeSampleAt=now;int current=s.route[s.route.Count-1];var from=Vertex(q,current);
            s.routeAtVertex=AtVertex(position,from);if(s.routeAtVertex){s.routeEdge=-1;s.routeCrossedMiddle=false;return;}
            if(AtVertex(position,from,footprint)){s.routeEdge=-1;s.routeCrossedMiddle=false;return;}
            if(s.routeEdge<0){float closest=float.PositiveInfinity;for(int i=0;i<q.graph.edges.Count;i++){var edge=q.graph.edges[i];string target=edge.source==q.nodeIds[current]?edge.destination:edge.source;if(!edge.Allows(q.nodeIds[current],target))continue;int next=Array.IndexOf(q.nodeIds,target);if(next<0)continue;var to=Vertex(q,next);float fraction;if(!OnBridge(position,from,to,footprint,out fraction)||fraction>=.5f&&!AtVertex(previous,from,footprint))continue;var offset=position-Vector3.Lerp(from,to,fraction);offset.y=0;if(offset.sqrMagnitude<closest){closest=offset.sqrMagnitude;s.routeEdge=i;}}
                if(s.routeEdge<0){Invalidate(s,"Left the vertex without an allowed edge. Follow the arrows and retry at the start.");return;}
            }
            if(s.routeEdge>=q.graph.edges.Count){Invalidate(s,"Missing bridge checkpoint. Retry at the start.");return;}
            var pending=q.graph.edges[s.routeEdge];string nextId=pending.source==q.nodeIds[current]?pending.destination:pending.source;int destination=Array.IndexOf(q.nodeIds,nextId);
            if(destination<0||!pending.Allows(q.nodeIds[current],nextId)){Invalidate(s,"This bridge only permits the arrow direction.");return;}
            var end=Vertex(q,destination);float previousT,currentT;
            if(OnBridge(previous,from,end,footprint,out previousT)&&OnBridge(position,from,end,footprint,out currentT)&&previousT<=.5f&&currentT>=.5f)s.routeCrossedMiddle=true;
            if(AtVertex(position,end)){
                if(!s.routeCrossedMiddle){Invalidate(s,"Cross the connecting path's middle before arriving.");return;}
                if(s.route.Count>=32){Invalidate(s,"Route is too long. Retry without repeating vertices.");return;}
                s.route.Add(destination);s.routeEdge=-1;s.routeCrossedMiddle=false;s.routeAtVertex=true;return;
            }
            float t;if(!OnBridge(position,from,end,footprint,out t)){if(AtVertex(position,from,footprint)||AtVertex(position,end,footprint))return;Invalidate(s,"Left the connecting stone path. Return to the start to retry.");return;}
            if(t>=.4f&&t<=.65f)s.routeCrossedMiddle=true;
        }
        static bool AnyIndexOutside(this System.Collections.Generic.List<int> route,int count){foreach(int index in route)if(index<0||index>=count)return true;return false;}
    }
}
