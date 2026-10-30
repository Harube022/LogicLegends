using System;
using System.Collections.Generic;
using LogicLegends.OpenArchive.Water;
using UnityEngine;
using UnityEngine.AI;

namespace LogicLegends.OpenArchive.Hunting
{
    /// <summary>Chooses complete dry routes alongside the real pond shoreline. Does not move, rotate or warp the agent.</summary>
    public sealed class KhezuWaterPursuit
    {
        sealed class Edge {
            public Vector3 a,b,outward;
            public float length;
            public int count, next=-1, previous=-1;
        }
        struct Candidate { public int edge; public float along, distance; public Vector3 bank; }
        readonly OpenArchiveWater water;
        readonly List<Edge> edges = new List<Edge>();
        readonly List<Candidate> candidates = new List<Candidate>();
        readonly List<Vector3> tested = new List<Vector3>();
        Transform target;
        Vector3 lastTarget, goal;
        Candidate anchor;
        bool hasGoal, pacing;
        int direction=1;
        public bool Active {get;private set;}
        public Vector3 Goal => goal;
        public KhezuWaterPursuit(OpenArchiveWater water,KhezuWaterPursuit source=null)
        {
            this.water=water;
            // Late-awakening actors reuse the immutable boundary built before static batching.
            // Each actor retains its own target, pacing direction and route candidates.
            if(source!=null&&source.water==water&&source.edges.Count>0)edges.AddRange(source.edges);
            else BuildShoreline();
        }
        public void Reset() {target=null; hasGoal=pacing=Active=false; direction=1;}
        static string Key(Vector3 p) => Mathf.RoundToInt(p.x*1000)+","+Mathf.RoundToInt(p.z*1000);
        void BuildShoreline()
        {
            foreach(var surface in water.surfaces)
            {
                if(surface==null || surface.sharedMesh==null || !surface.sharedMesh.isReadable)continue;
                var vertices=surface.sharedMesh.vertices; var triangles=surface.sharedMesh.triangles;
                var boundary=new Dictionary<string,Edge>();
                for(int i=0;i<triangles.Length;i+=3)
                {
                    var a=surface.transform.TransformPoint(vertices[triangles[i]]);
                    var b=surface.transform.TransformPoint(vertices[triangles[i+1]]);
                    var c=surface.transform.TransformPoint(vertices[triangles[i+2]]);
                    float winding=Vector3.Cross(b-a,c-a).y;
                    if(Mathf.Abs(winding)<.000001f)continue;
                    if(winding<0){var swap=b;b=c;c=swap;}
                    AddEdge(boundary,a,b);AddEdge(boundary,b,c);AddEdge(boundary,c,a);
                }
                var first=edges.Count;
                foreach(var e in boundary.Values)if(e.count==1){
                    e.length=Vector3.Distance(e.a,e.b); e.outward=Vector3.Cross(e.b-e.a,Vector3.up).normalized; edges.Add(e);
                }
                var starts=new Dictionary<string,int>();var ends=new Dictionary<string,int>();
                for(int i=first;i<edges.Count;i++){starts[Key(edges[i].a)]=i;ends[Key(edges[i].b)]=i;}
                for(int i=first;i<edges.Count;i++){
                    int n;if(starts.TryGetValue(Key(edges[i].b),out n))edges[i].next=n;
                    if(ends.TryGetValue(Key(edges[i].a),out n))edges[i].previous=n;
                }
            }
        }
        static void AddEdge(Dictionary<string,Edge> edges,Vector3 a,Vector3 b)
        {
            string ka=Key(a),kb=Key(b),key=string.CompareOrdinal(ka,kb)<0?ka+"|"+kb:kb+"|"+ka;
            Edge edge;if(!edges.TryGetValue(key,out edge))edges[key]=edge=new Edge{a=a,b=b};edge.count++;
        }
        public bool ContainsTarget(Transform transform,CharacterController capsule)
        {
            if(water==null || !water.isActiveAndEnabled)return false;
            Vector3 feet=capsule==null?transform.position:OpenArchiveWater.Feet(capsule);float height;
            return water.SampleSurface(feet,out height) &&
                (feet.y<=height+.12f || (capsule!=null && !capsule.isGrounded));
        }
        public bool TryRoute(Transform selected,NavMeshAgent agent,float paceDistance,float turnDistance,out NavMeshPath route)
        {
            route=null;
            bool changed=target!=selected;
            if(changed){Reset();target=selected;}
            Vector3 position=selected.position;
            bool targetMoved=FlatSquared(position-lastTarget)>9;
            bool nearGoal=hasGoal && (FlatSquared(agent.transform.position-goal)<=turnDistance*turnDistance ||
                (agent.hasPath && !agent.pathPending && agent.remainingDistance<=turnDistance));
            if(hasGoal && !changed && !targetMoved && !nearGoal && agent.hasPath && agent.pathStatus==NavMeshPathStatus.PathComplete)
            {Active=true;return true;}
            if(changed || targetMoved || !hasGoal)
            {
                Candidate found;NavMeshPath approach;
                if(!FindBank(position,agent,out found,out approach))return KeepCurrent(agent);
                anchor=found;lastTarget=position;
                if(FlatSquared(agent.transform.position-found.bank)>turnDistance*turnDistance)
                {goal=found.bank;hasGoal=true;pacing=false;Active=true;route=approach;return true;}
            }
            if(nearGoal && pacing)direction=-direction;
            foreach(float factor in new[]{1f,.65f,1.5f})
                for(int side=0;side<2;side++)
                {
                    int sign=side==0?direction:-direction;
                    Vector3 bank=Advance(anchor,sign*paceDistance*factor,agent.radius+1);
                    NavMeshPath path;Vector3 sampled;
                    if(TryBankPath(bank,agent,out sampled,out path) && FlatSquared(sampled-agent.transform.position)>turnDistance*turnDistance)
                    {goal=sampled;hasGoal=pacing=Active=true;direction=sign;route=path;return true;}
                }
            return KeepCurrent(agent);
        }
        // A fallen actor cannot run away: fetch the closest reachable bank rather than pace past it.
        public bool TryRescueApproach(Transform selected,NavMeshAgent agent,out NavMeshPath route)
        {
            route=null;
            if(!FindBank(selected.position,agent,out var found,out route))return false;
            target=selected;lastTarget=selected.position;anchor=found;goal=found.bank;
            hasGoal=Active=true;pacing=false;return true;
        }
        bool KeepCurrent(NavMeshAgent agent)
        {
            Active=agent.hasPath && agent.pathStatus==NavMeshPathStatus.PathComplete && agent.remainingDistance>1;
            return Active;
        }
        bool FindBank(Vector3 player,NavMeshAgent agent,out Candidate chosen,out NavMeshPath bestPath)
        {
            candidates.Clear();tested.Clear();
            for(int i=0;i<edges.Count;i++)
            {
                var edge=edges[i];var offset=player-edge.a;offset.y=0;
                var line=edge.b-edge.a;line.y=0;
                float along=Mathf.Clamp01(Vector3.Dot(offset,line)/Mathf.Max(.000001f,line.sqrMagnitude));
                var shore=Vector3.Lerp(edge.a,edge.b,along);var bank=shore+edge.outward*(agent.radius+1);bank.y=agent.transform.position.y;
                candidates.Add(new Candidate{edge=i,along=along,bank=bank,distance=FlatSquared(shore-player)});
            }
            candidates.Sort((a,b)=>a.distance.CompareTo(b.distance));
            float score=float.MaxValue;chosen=default(Candidate);bestPath=null;
            foreach(var candidate in candidates)
            {
                if(tested.Exists(p=>FlatSquared(p-candidate.bank)<agent.radius*agent.radius))continue;
                tested.Add(candidate.bank);
                Vector3 sampled;NavMeshPath path;
                if(TryBankPath(candidate.bank,agent,out sampled,out path))
                {
                    float length=0;var corners=path.corners;
                    for(int i=1;i<corners.Length;i++)length+=Vector3.Distance(corners[i-1],corners[i]);
                    float cost=Mathf.Sqrt(FlatSquared(sampled-player))+length*.08f;
                    if(cost<score){score=cost;chosen=candidate;chosen.bank=sampled;bestPath=path;}
                }
                if(tested.Count>=8)break;
            }
            return bestPath!=null;
        }
        bool TryBankPath(Vector3 point,NavMeshAgent agent,out Vector3 sampled,out NavMeshPath path)
        {
            point.y=agent.transform.position.y;
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
            NavMeshHit hit;float height;path=new NavMeshPath();sampled=point;
            if(!NavMesh.SamplePosition(point,out hit,Mathf.Max(3,agent.radius*.4f),filter) || water.SampleSurface(hit.position,out height))return false;
            if(!NavMesh.CalculatePath(agent.transform.position,hit.position,filter,path) || path.status!=NavMeshPathStatus.PathComplete)return false;
            sampled=hit.position;return true;
        }
        Vector3 Advance(Candidate start,float distance,float clearance)
        {
            int index=start.edge;float along=start.along;
            for(int count=0;count<edges.Count;count++)
            {
                var edge=edges[index];float available=(distance>=0?1-along:along)*edge.length;
                if(Mathf.Abs(distance)<=available)
                {along+=distance/Mathf.Max(.001f,edge.length);return Vector3.Lerp(edge.a,edge.b,along)+edge.outward*clearance;}
                int next=distance>=0?edge.next:edge.previous;
                if(next<0)return Vector3.Lerp(edge.a,edge.b,along)+(edge.b-edge.a).normalized*distance+edge.outward*clearance;
                distance-=Mathf.Sign(distance)*available;index=next;along=distance>=0?0:1;
            }
            return start.bank;
        }
        static float FlatSquared(Vector3 p)=>p.x*p.x+p.z*p.z;
    }
}
