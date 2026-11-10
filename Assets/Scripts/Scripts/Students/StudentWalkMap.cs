using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LogicLegends.Graph;
using UnityEngine.SceneManagement;

namespace LogicLegends.Students
{
    // A small, incremental collision-derived walking graph. No scene rebake,
    // renderer bounds, decorative rooftops or global NavMesh can supply a spawn.
    public sealed class StudentWalkMap
    {
        public sealed class Node
        {
            public Vector3 Point;
            public Vector2Int Cell;
            public int Component=-1;
            public readonly List<int> Links=new List<int>(8);
        }
        public readonly List<Node> Nodes=new List<Node>();
        public bool Ready { get; private set; }
        public int Rejected { get; private set; }
        readonly StudentNpcCatalog config;
        readonly float spacing;
        readonly int cellLimit;
        readonly List<Vector3> keepOut;
        readonly List<Bounds> water;
        readonly GraphVertex[] platforms;
        readonly HashSet<Collider> graphFloor;
        readonly Scene scene;
        readonly List<Vector3> entranceSeeds;
        readonly RaycastHit[] rays=new RaycastHit[48];
        readonly Collider[] overlaps=new Collider[48];
        readonly Dictionary<Vector2Int,int> lookup=new Dictionary<Vector2Int,int>();
        readonly HashSet<Vector2Int> tested=new HashSet<Vector2Int>();
        readonly Dictionary<int,float> temporarilyBlocked=new Dictionary<int,float>();
        Vector3 origin;
        static readonly Vector2Int[] offsets={new Vector2Int(1,0),new Vector2Int(-1,0),new Vector2Int(0,1),new Vector2Int(0,-1),
            new Vector2Int(1,1),new Vector2Int(1,-1),new Vector2Int(-1,1),new Vector2Int(-1,-1)};
        static readonly Vector3[] footprint={Vector3.right,Vector3.left,Vector3.forward,Vector3.back};
        public StudentWalkMap(StudentNpcCatalog settings,List<Vector3> interactionPoints,List<Bounds> hazards,GraphVertex[] graphPlatforms=null,Scene scene=default,IEnumerable<Vector3> entrances=null,float gridSize=0,int maximumCells=0)
        {
            config=settings;keepOut=interactionPoints;water=hazards;platforms=graphPlatforms;this.scene=scene;
            spacing=gridSize>0?gridSize:config.gridSpacing;cellLimit=maximumCells>0?maximumCells:config.maximumCells;
            entranceSeeds=entrances!=null?new List<Vector3>(entrances):new List<Vector3>();
            if(platforms!=null)
            {
                graphFloor=new HashSet<Collider>();
                foreach(var vertex in platforms)
                {
                    if(vertex==null)continue;
                    var floor=vertex.transform.Find("Bilog artwork");
                    if(floor!=null)foreach(var collider in floor.GetComponentsInChildren<Collider>(true))if(!collider.isTrigger)graphFloor.Add(collider);
                }
            }
        }
        bool Ignored(Collider c)=>c==null || c.isTrigger || scene.IsValid()&&c.gameObject.scene!=scene || c.GetComponentInParent<Player>()!=null || c.GetComponentInParent<StudentNpcAgent>()!=null;
        static bool WaterName(string name)
        {name=name.ToLowerInvariant();return name.Contains("water")||name.Contains("ocean")||name.Contains("river");}
        bool Ground(Vector3 guess,float rise,float depth,out Vector3 point)
        {
            point=default;float best=float.PositiveInfinity;
            int count=Physics.RaycastNonAlloc(guess+Vector3.up*rise,Vector3.down,rays,depth,~0,QueryTriggerInteraction.Ignore);
            if(count==rays.Length)return false; // Fail closed if the query saturated.
            for(int i=0;i<count;i++)
            {
                var hit=rays[i];if(Ignored(hit.collider)||hit.normal.y<.72f||WaterName(hit.collider.name))continue;
                if(graphFloor!=null&&!graphFloor.Contains(hit.collider))continue;
                if(hit.distance<best){best=hit.distance;point=hit.point;}
            }
            if(float.IsPositiveInfinity(best))return false;
            foreach(var b in water)
                if(point.x>=b.min.x&&point.x<=b.max.x&&point.z>=b.min.z&&point.z<=b.max.z&&point.y<=b.max.y+.2f)return false;
            return true;
        }
        public bool Clear(Vector3 foot)
        {
            // Reserve the same step clearance used by the route sweep. A lower
            // capsule otherwise collides with a tread before its centre reaches it.
            int count=Physics.OverlapCapsuleNonAlloc(foot+Vector3.up*(config.radius+config.stepHeight+.06f),
                foot+Vector3.up*(config.height-config.radius),config.radius,overlaps,~0,QueryTriggerInteraction.Ignore);
            if(count==overlaps.Length)return false;
            for(int i=0;i<count;i++)if(!Ignored(overlaps[i]))return false;
            return true;
        }
        public bool AwayFromInteractions(Vector3 point)
        {
            foreach(var p in keepOut)
                if(Mathf.Abs(p.y-point.y)<config.height+2&&PlanarSquare(p,point)<config.interactionClearance*config.interactionClearance)return false;
            return true;
        }
        bool Supported(Vector3 foot)
        {
            if(!Clear(foot)||!AwayFromInteractions(foot))return false;
            foreach(var offset in footprint)
                if(!Ground(foot+offset*config.radius,1.05f,2.1f,out var edge)||Mathf.Abs(edge.y-foot.y)>.7f)return false;
            return true;
        }
        public bool Segment(Vector3 from,Vector3 to)
        {
            var delta=to-from;float distance=delta.magnitude;
            if(distance<.001f)return Supported(to);
            // Validate support first, including a low stone between two floor
            // nodes. Sweep above the highest supported step, not into its riser.
            float sweepHeight=Mathf.Max(from.y,to.y),previousHeight=from.y;
            int steps=Mathf.Max(1,Mathf.CeilToInt(distance/(config.radius*.8f)));
            for(int i=1;i<=steps;i++)
            {
                var sample=Vector3.Lerp(from,to,(float)i/steps);
                if(!Ground(sample,1.05f,2.1f,out var ground)||Mathf.Abs(ground.y-sample.y)>.7f||Mathf.Abs(ground.y-previousHeight)>.7f||!AwayFromInteractions(ground))return false;
                sweepHeight=Mathf.Max(sweepHeight,ground.y);previousHeight=ground.y;
            }
            var sweepFrom=new Vector3(from.x,sweepHeight,from.z);
            var horizontal=new Vector3(delta.x,0,delta.z);
            if(horizontal.sqrMagnitude<.000001f)return Supported(to);
            int count=Physics.CapsuleCastNonAlloc(sweepFrom+Vector3.up*(config.radius+config.stepHeight+.06f),sweepFrom+Vector3.up*(config.height-config.radius),
                config.radius,horizontal.normalized,rays,horizontal.magnitude+.08f,~0,QueryTriggerInteraction.Ignore);
            if(count==rays.Length)return false;
            for(int i=0;i<count;i++)if(!Ignored(rays[i].collider)&&rays[i].normal.y<.72f)return false;
            return true;
        }
        public IEnumerator Build(Vector3 seed)
        {
            Ready=false;Nodes.Clear();lookup.Clear();tested.Clear();origin=seed;
            var queue=new Queue<int>();
            // Graph mode has an explicit whitelist of generated, active vertices.
            // Other trials flood only the floor reachable from this area's spawn.
            var seeds=new List<Vector3>();
            if(platforms!=null){foreach(var p in platforms)if(p!=null&&p.gameObject.activeInHierarchy)seeds.Add(p.transform.position);}
            else {seeds.Add(seed);seeds.AddRange(entranceSeeds);}
            int seedComponent=0;
            foreach(var start in seeds)
            {
                bool found=false;
                for(int ring=0;ring<=4&&!found;ring++)for(int side=0;side<8&&!found;side++)
                {
                    var guess=start+Quaternion.Euler(0,side*45,0)*Vector3.forward*(ring*spacing);
                    if(!Ground(guess,4,16,out var ground)||!Supported(ground))continue;
                    // Entrance seeds use the door's bottom, never its roof or a
                    // renderer's top bounds, as their expected ground reference.
                    if(entranceSeeds.Contains(start)&&(ground.y>start.y+1.25f||ground.y<start.y-config.height-2))continue;
                    var cell=Cell(ground);if(lookup.ContainsKey(cell)){found=true;continue;}
                    int index=Add(cell,ground);Nodes[index].Component=seedComponent++;queue.Enqueue(index);tested.Add(cell);found=true;
                }
            }
            int work=0;double slice=Time.realtimeSinceStartupAsDouble;
            while(queue.Count>0&&Nodes.Count<cellLimit)
            {
                int current=queue.Dequeue();var node=Nodes[current];
                foreach(var offset in offsets)
                {
                    var cell=node.Cell+offset;
                    bool added=false;
                    if(!lookup.TryGetValue(cell,out int next))
                    {
                        if(tested.Contains(cell))continue;
                        var guess=new Vector3(origin.x+cell.x*spacing,node.Point.y,origin.z+cell.y*spacing);
                        // A coarse grid edge can climb several units along a
                        // gentle ramp. Segment checks the small intermediate
                        // steps; an endpoint-only one-unit cap cuts off islands.
                        float climb=offset.magnitude*spacing*.95f;
                        if(!Ground(guess,climb+1,2*(climb+1),out var ground)||Mathf.Abs(ground.y-node.Point.y)>climb||!Supported(ground)||!Traversable(node.Point,ground)||!Traversable(ground,node.Point))
                        {Rejected++;continue;}
                        tested.Add(cell);next=Add(cell,ground);Nodes[next].Component=node.Component;queue.Enqueue(next);added=true;
                    }
                    if(!node.Links.Contains(next)&&(added||Traversable(node.Point,Nodes[next].Point)&&Traversable(Nodes[next].Point,node.Point)))
                    {node.Links.Add(next);Nodes[next].Links.Add(current);}
                    if(Nodes.Count>=cellLimit)break;
                }
                if(++work>=config.buildCellsPerFrame||Time.realtimeSinceStartupAsDouble-slice>SoloGraphicsBudget.NavigationSliceSeconds)
                {work=0;yield return null;slice=Time.realtimeSinceStartupAsDouble;}
            }
            queue.Clear();foreach(var node in Nodes)node.Component=-1;int component=0;
            for(int i=0;i<Nodes.Count;i++)
            {
                if(Nodes[i].Component>=0)continue;
                queue.Enqueue(i);Nodes[i].Component=component;
                while(queue.Count>0)
                {
                    int n=queue.Dequeue();foreach(int link in Nodes[n].Links)if(Nodes[link].Component<0){Nodes[link].Component=component;queue.Enqueue(link);}
                }
                component++;
            }
            Ready=true;
        }
        int Add(Vector2Int cell,Vector3 point){int i=Nodes.Count;lookup.Add(cell,i);Nodes.Add(new Node{Cell=cell,Point=point});return i;}
        Vector2Int Cell(Vector3 p)=>new Vector2Int(Mathf.RoundToInt((p.x-origin.x)/spacing),Mathf.RoundToInt((p.z-origin.z)/spacing));
        public int Nearest(Vector3 point)
        {
            int best=-1;float distance=float.PositiveInfinity;
            for(int i=0;i<Nodes.Count;i++){float d=(Nodes[i].Point-point).sqrMagnitude;if(d<distance){distance=d;best=i;}}
            return best;
        }
        public void Block(int node){if(node>=0)temporarilyBlocked[node]=Time.time+3;}
        bool Blocked(int node)=>temporarilyBlocked.TryGetValue(node,out float until)&&Time.time<until;
        public bool Path(int start,int goal,List<int> result)
        {
            result.Clear();if(start<0||goal<0||start==goal)return false;
            // A bounded A*; distances are world units, so diagonals cost correctly.
            var open=new MinHeap();var cost=new Dictionary<int,float>{{start,0}};var parent=new Dictionary<int,int>();
            var closed=new HashSet<int>();open.Push(start,0);int visits=0;
            while(open.Count>0&&visits++<cellLimit*3)
            {
                int current=open.Pop();if(!closed.Add(current))continue;
                if(current==goal)
                {
                    for(int n=goal;n!=start;n=parent[n])result.Add(n);
                    result.Reverse();return true;
                }
                foreach(int n in Nodes[current].Links)
                {
                    if(closed.Contains(n)||Blocked(n))continue;
                    float g=cost[current]+Vector3.Distance(Nodes[current].Point,Nodes[n].Point);
                    if(cost.TryGetValue(n,out float old)&&g>=old)continue;
                    cost[n]=g;parent[n]=current;open.Push(n,g+Vector3.Distance(Nodes[n].Point,Nodes[goal].Point));
                }
            }
            return false;
        }
        public bool GroundedStep(Vector3 from,Vector3 desired,out Vector3 actual)
        {
            actual=from;
            if(!Ground(desired,1.05f,2.1f,out var ground)||Mathf.Abs(ground.y-from.y)>1||!Supported(ground)||!Segment(from,ground))return false;
            actual=ground;return true;
        }
        bool Traversable(Vector3 from,Vector3 to)
        {
            if(!Segment(from,to))return false;
            if(Mathf.Abs(from.y-to.y)<.1f)return true;
            // A coarse edge can look clear while a character-sized intermediate
            // position catches a stone lip. Reject it before A* can choose it.
            int steps=Mathf.CeilToInt(Vector3.Distance(from,to)/.12f);var previous=from;
            for(int i=1;i<=steps;i++)
            {
                var desired=Vector3.Lerp(from,to,(float)i/steps);
                if(!GroundedStep(previous,desired,out var foot))return false;
                previous=foot;
            }
            return true;
        }
        public static float PlanarSquare(Vector3 a,Vector3 b){float x=a.x-b.x,z=a.z-b.z;return x*x+z*z;}
        sealed class MinHeap
        {
            readonly List<(int node,float score)> entries=new List<(int,float)>();public int Count=>entries.Count;
            public void Push(int node,float score){entries.Add((node,score));int i=entries.Count-1;while(i>0){int p=(i-1)/2;if(entries[p].score<=score)break;entries[i]=entries[p];i=p;}entries[i]=(node,score);}
            public int Pop(){int result=entries[0].node;var last=entries[entries.Count-1];entries.RemoveAt(entries.Count-1);if(entries.Count==0)return result;int i=0;while(i*2+1<entries.Count){int c=i*2+1;if(c+1<entries.Count&&entries[c+1].score<entries[c].score)c++;if(entries[c].score>=last.score)break;entries[i]=entries[c];i=c;}entries[i]=last;return result;}
        }
    }
}
