using System;
using System.Collections.Generic;
using System.Linq;
using LogicLegends.Graph;
using UnityEditor;
using UnityEngine;

public static class GraphStageChecks
{
    public static void Require(bool condition,string message) { if(!condition)throw new Exception("Graph check failed: "+message); }
    [MenuItem("Tools/Logic Legends/Validate Graph Rules")]
    public static void Menu()=>Debug.Log(Run());
    public static string Run()
    {
        var catalog=AssetDatabase.LoadAssetAtPath<GraphChallengeCatalog>("Assets/Prefabs/Graph/GraphChallenges.asset");
        Require(catalog.challenges.Count==10,"ten challenge configurations");
        foreach(var c in catalog.challenges)
        {
            Require(c.vertices.Select(v=>v.id).Distinct().Count()==c.vertices.Count,"unique vertex IDs");
            Require(c.Vertex(c.start)!=null,"start exists");
            foreach(var e in c.edges) Require(c.Vertex(e.source)!=null && c.Vertex(e.destination)!=null,"edge endpoints exist");
        }
        Require(catalog.variantSets!=null&&catalog.variantSets.Count==5,"five ordered Graph challenge sets");
        var expectedTypes=new[]{GraphChallengeType.VerticesAndEdges,GraphChallengeType.Degree,
            GraphChallengeType.Path,GraphChallengeType.DirectedPath,GraphChallengeType.Cycle};
        for(int round=0;round<5;round++)
        {
            var set=catalog.variantSets[round];
            Require(set!=null&&set.variants!=null&&set.variants.Count==10,"ten variants in round "+round);
            var signatures=new HashSet<string>();
            for(int index=0;index<10;index++)
            {
                var variant=set.variants[index];
                string label="round "+round+" variant "+index;
                Require(variant!=null&&variant.type==expectedTypes[round],label+" correct difficulty step");
                Require(variant.vertices.Select(v=>v.id).Distinct().Count()==variant.vertices.Count,label+" unique IDs");
                Require(variant.Vertex(variant.start)!=null,label+" start exists");
                Require(variant.edges.All(e=>variant.Vertex(e.source)!=null&&variant.Vertex(e.destination)!=null),label+" valid endpoints");
                Require(variant.vertices.All(a=>variant.vertices.All(b=>a==b||Vector3.Distance(a.position,b.position)>=8.9f)),label+" separate platforms");
                Require(variant.edges.All(e=>
                {
                    float length=Vector3.Distance(variant.Vertex(e.source).position,variant.Vertex(e.destination).position);
                    return length>=8.9f&&length*GraphGeometry.LayoutScale<=140f;
                }),label+" walkable bridge length");
                string signature=variant.objective+"|"+string.Join(",",variant.edges.Select(e=>e.source+">"+e.destination+(e.directed?"!":"")).OrderBy(x=>x).ToArray());
                Require(signatures.Add(signature),label+" distinct problem");
                if(index<5)Require(variant.vertices.All(v=>char.IsLetter(v.id[0])),label+" letter labels");
                else Require(variant.vertices.All(v=>char.IsDigit(v.id[0])),label+" numbered labels");
                if(round==0)Require(variant.vertices.Count==2&&variant.edges.Count==1&&
                    GraphAnswerValidator.Validate(variant,new[]{variant.start,variant.destination},null,true,out _),label+" one-bridge introduction");
                else if(round==1)
                {
                    int degree=variant.Degree(variant.degreeVertex);
                    Require(variant.vertices.Count==5&&(degree==3||degree==4),label+" incident-edge count");
                    Require(GraphAnswerValidator.Validate(variant,null,degree,true,out _),label+" correct degree");
                    Require(!GraphAnswerValidator.Validate(variant,null,degree-1,true,out _),label+" incorrect degree");
                }
                else
                {
                    var solution=FindRoute(variant,new List<string>{variant.start});
                    Require(solution!=null&&GraphAnswerValidator.Validate(variant,solution,null,true,out _),label+" solvable route");
                    if(round==2)Require(variant.vertices.Count==6&&solution.Count>=4&&variant.edges.Count>=6,label+" branching simple path");
                    if(round==3)Require(variant.vertices.Count==8&&solution.Count>=5&&variant.edges.All(e=>e.directed)&&
                        variant.edges.Count>=6&&variant.edges.Any(e=>!variant.Connected(e.destination,e.source)),label+" one-way choices");
                    if(round==4)Require(variant.vertices.Count==10&&variant.edges.Count>=12&&solution.Count>=7&&
                        variant.Degree(variant.vertices.Last().id)==1,label+" five-edge cycle and dead end");
                    Require(!GraphAnswerValidator.Validate(variant,new[]{variant.start},null,true,out _),label+" incomplete route rejected");
                }
            }
        }
        string feedback;
        Func<int,string[],bool> valid=(i,r)=>GraphAnswerValidator.Validate(catalog.challenges[i],r,null,true,out feedback);
        Require(valid(0,new[]{"A","B"}),"first edge accepted");
        Require(!valid(0,new[]{"A"}),"empty movement rejected");
        Require(catalog.challenges[1].Degree("C")==4,"degree computed from data");
        Require(GraphAnswerValidator.Validate(catalog.challenges[1],null,4,true,out feedback),"degree four accepted");
        Require(!GraphAnswerValidator.Validate(catalog.challenges[1],null,3,true,out feedback),"wrong degree rejected");
        var loop=new GraphChallengeData{edges=new List<GraphEdgeData>{new GraphEdgeData("A","A")}};
        Require(loop.Degree("A")==2,"loop contributes two");
        Require(valid(2,new[]{"A","B","D","F"}) && valid(2,new[]{"A","C","E","F"}),"both simple paths accepted");
        Require(!valid(2,new[]{"A","F"}),"nonexistent edge rejected");
        Require(!valid(2,new[]{"A","B","A","C","E","F"}),"repeated vertex rejected");
        Require(valid(3,new[]{"A","B","D"}) && valid(3,new[]{"A","C","D"}),"both directed paths accepted");
        Require(!catalog.challenges[3].Connected("B","A"),"reverse direction rejected");
        Require(valid(4,new[]{"A","B","C","D","A"}) && valid(4,new[]{"A","D","C","B","A"}),"both cycles accepted");
        Require(!valid(4,new[]{"A","B","A"}),"two-vertex return is not a cycle");
        Require(!valid(4,new[]{"A","B","C","B","A"}),"repeated middle vertex rejected");
        Require(valid(5,new[]{"1","2"}) && !valid(5,new[]{"1"}),"numbered edge objective");
        Require(catalog.challenges[6].Degree("3")==3,"numbered degree computed from edges");
        Require(GraphAnswerValidator.Validate(catalog.challenges[6],null,3,true,out feedback),"numbered degree accepted");
        Require(!GraphAnswerValidator.Validate(catalog.challenges[6],null,4,true,out feedback),"wrong numbered degree rejected");
        Require(valid(7,new[]{"1","2","3","5"}) && valid(7,new[]{"1","4","5"}),"both numbered paths of different lengths accepted");
        Require(!valid(7,new[]{"1","5"}) && !valid(7,new[]{"1","2","1","4","5"}),"numbered invalid and repeated path rejected");
        Require(valid(8,new[]{"1","2","4","5"}) && valid(8,new[]{"1","3","5"}),"both numbered directed paths accepted");
        Require(!catalog.challenges[8].Connected("2","1"),"numbered reverse direction rejected");
        Require(valid(9,new[]{"1","2","3","1"}) && valid(9,new[]{"1","3","2","1"}),"both triangular cycles accepted");
        Require(!valid(9,new[]{"1","2","1"}) && !valid(9,new[]{"1","2","3","2","1"}),"numbered invalid cycles rejected");
        var original=catalog.challenges.ToArray();
        var sampled=Enumerable.Range(0,5).Select(_=>new HashSet<int>()).ToArray();var orders=new HashSet<string>();
        for(int seed=0;seed<200;seed++)
        {
            var selection=catalog.SelectRun(new System.Random(seed));
            Require(selection.Count==10,"exactly five ordered challenge picks");
            Require(selection.All(i=>i>=0 && i<10),"selection stays within each variant set");
            Require(selection.SequenceEqual(catalog.SelectRun(new System.Random(seed))),"seeded selection reproducible for validation");
            var next=catalog.SelectRun(new System.Random(seed),Enumerable.Range(0,5).Select(i=>selection[i*2]).ToArray());
            Require(Enumerable.Range(0,5).All(i=>selection[i*2]!=next[i*2]),"no immediate repeat on replay");
            for(int round=0;round<5;round++){ sampled[round].Add(selection[round*2]); sampled[round].Add(selection[round*2+1]);Require(selection[round*2]!=selection[round*2+1],"distinct pair per challenge"); }
            orders.Add(string.Join(",",selection));
        }
        Require(sampled.All(s=>s.Count==10)&&orders.Count>1,"every variant eligible in every round");
        Require(catalog.challenges.SequenceEqual(original),"selection never reorders the shared catalog");
        var t=new GraphTraversalTracker();var first=catalog.challenges[0];
        t.Reset(first,Vector3.up);for(int i=0;i<=120;i++)t.Sample(new Vector3(0,1,i*.1f),.02f);
        Require(t.IsContinuous && t.Route.SequenceEqual(new[]{"A","B"}),"continuous bridge records once");
        for(int i=0;i<30;i++)t.Sample(new Vector3(0,1,12),.02f);
        Require(t.Route.Count==2,"vertex jitter cannot add visits");
        t.Reset(first,Vector3.up);t.Sample(new Vector3(0,1,12),.02f);
        Require(!t.IsContinuous && t.Route.Count==1,"teleport to vertex rejected");
        t.Reset(first,Vector3.up);for(int i=0;i<60;i++)t.Sample(new Vector3(i*.1f,1,0),.02f);
        Require(!t.IsContinuous,"off-graph motion rejected");
        t.Reset(first,Vector3.up);for(int i=0;i<=60;i++)t.Sample(new Vector3(0,1,i*.1f),.02f);
        t.Sample(new Vector3(0,1,6),.02f,true);Require(!t.IsContinuous && t.Route.Count==1,"teleport during crossing rejected");
        t.Reset(catalog.challenges[5],Vector3.up);for(int i=0;i<=120;i++)t.Sample(new Vector3(0,1,i*.1f),.02f);
        Require(t.IsContinuous && t.Route.SequenceEqual(new[]{"1","2"}),"numeric IDs traverse and record normally");
        var directed=catalog.challenges[3];var a=directed.Vertex("A").position;var b=directed.Vertex("B").position;
        t.Reset(directed,a+Vector3.up);for(int i=0;i<=160;i++)t.Sample(Vector3.Lerp(a,b,i/160f)+Vector3.up,.02f);
        var back=(a-b).normalized;
        for(int i=0;i<=40;i++)t.Sample(b+back*(i*.1f)+Vector3.up,.02f);
        Require(t.IsContinuous && !t.AtVertex && t.Route.SequenceEqual(new[]{"A","B"}),"blocked reverse entry preserves route without recording or allowing confirmation");
        for(int i=40;i>=0;i--)t.Sample(b+back*(i*.1f)+Vector3.up,.02f);
        Require(t.IsContinuous && t.AtVertex && t.Route.Count==2,"return from blocked entry restores vertex status");
        for(int i=0;i<=60;i++)t.Sample(b+back*(i*.1f)+Vector3.up,.02f);
        Require(!t.IsContinuous && t.Route.Count==2,"bypassing a closed reverse gate still invalidates traversal");
        // Reproduce ordinary rim movement after the A -> B directed crossing.
        t.Reset(directed,a+Vector3.up);
        for(int i=0;i<=160;i++)t.Sample(Vector3.Lerp(a,b,i/160f)+Vector3.up,.02f);
        for(int i=0;i<=35;i++)t.Sample(b+new Vector3(-i*.1f,1,0),.02f);
        Require(t.IsContinuous && t.AtVertex && t.Route.SequenceEqual(new[]{"A","B"}),"walking the actual platform rim keeps the route");
        for(int i=35;i<=39;i++)t.Sample(b+new Vector3(-i*.1f,1,0),.02f);
        Require(t.IsContinuous && !t.AtVertex && t.Route.Count==2,"capsule support margin preserves continuity without accepting confirmation");
        for(int i=39;i>=0;i--)t.Sample(b+new Vector3(-i*.1f,1,0),.02f);
        var d=directed.Vertex("D").position;
        for(int i=0;i<=160;i++)t.Sample(Vector3.Lerp(b,d,i/160f)+Vector3.up,.02f);
        Require(t.IsContinuous && t.Route.SequenceEqual(new[]{"A","B","D"}),"platform turn continues onto the next directed bridge");
        foreach(var challenge in catalog.challenges)
        foreach(var edge in challenge.edges.Where(e=>e.traversalEnabled))
        {
            var from=challenge.Vertex(edge.source).position;var to=challenge.Vertex(edge.destination).position;
            var sideways=Vector3.Cross(Vector3.up,(to-from).normalized);
            // Begin at this edge's source without changing the shared challenge.
            var segment=new GraphChallengeData{start=edge.source,vertices=challenge.vertices,edges=challenge.edges};
            foreach(float side in new[]{-1f,1f})
            {
                t.Reset(segment,from+Vector3.up);
                for(int i=0;i<=240;i++)
                {
                    float fraction=i/240f;
                    t.Sample(Vector3.Lerp(from,to,fraction)+sideways*(side*2.8f*Mathf.Sin(Mathf.PI*fraction))+Vector3.up,.02f);
                }
                Require(t.IsContinuous && t.Current==edge.destination && t.Route.Count==2,"capsule-supported walking near either bridge edge: "+edge.source+" -> "+edge.destination);
            }
        }
        t.Reset(first,Vector3.up);
        for(int i=0;i<=37;i++)t.Sample(new Vector3(0,1,i*.1f),.02f);
        t.Sample(new Vector3(0,1,8.1f),.75f); // Six units/second; no sample in the old midpoint window.
        t.Sample(new Vector3(0,1,8.6f),.1f);
        Require(t.IsContinuous && t.Current=="B","continuous low-frame-rate crossing recognizes the segment through the middle");
        t.Reset(first,Vector3.up);
        for(int i=0;i<=60;i++)t.Sample(new Vector3(0,1,i*.1f),.02f);
        for(int i=0;i<=32;i++)t.Sample(new Vector3(i*.1f,1,6),.02f);
        Require(!t.IsContinuous && t.Route.Count==1,"leaving the deck beyond capsule support still interrupts without recording arrival");
        var gateObject=new GameObject("Gate validation");var gate=gateObject.AddComponent<GraphEdge>();
        gate.data=new GraphEdgeData("A","B",true);var gateCollider=gateObject.AddComponent<BoxCollider>();
        gateCollider.size=new Vector3(5.8f,10,.25f);gate.reverseGate=gateCollider;
        gate.UpdateGate("A",Vector3.zero,.78f);
        gate.UpdateGate("B",new Vector3(0,0,.3f),.78f);
        Require(!gateCollider.enabled,"directed arrival does not close a gate through the player's capsule");
        gate.UpdateGate("B",new Vector3(0,0,1),.78f);
        Require(gateCollider.enabled,"directed gate closes once the player's capsule clears it");
        gate.UpdateGate("B",new Vector3(0,0,.3f),.78f);
        Require(gateCollider.enabled,"closed directed gate remains closed against reverse travel");
        UnityEngine.Object.DestroyImmediate(gateObject);
        foreach(var challenge in catalog.challenges)
        {
            var enlarged=GraphGeometry.Layout(challenge);
            Require(enlarged.vertices.Count==challenge.vertices.Count && enlarged.edges==challenge.edges,"sizing preserves challenge topology");
            foreach(var edge in enlarged.edges.Where(e=>e.traversalEnabled))
            {
                var from=enlarged.Vertex(edge.source).position;var to=enlarged.Vertex(edge.destination).position;
                var sideways=Vector3.Cross(Vector3.up,(to-from).normalized);
                var segment=new GraphChallengeData{start=edge.source,vertices=enlarged.vertices,edges=enlarged.edges};
                foreach(float side in new[]{-1f,1f})
                {
                    t.Reset(segment,from+Vector3.up,.78f,GraphGeometry.VertexRadius,GraphGeometry.BridgeHalfWidth);
                    for(int i=0;i<=400;i++)
                    {
                        float fraction=i/400f;
                        t.Sample(Vector3.Lerp(from,to,fraction)+sideways*(side*(GraphGeometry.BridgeHalfWidth+.4f)*Mathf.Sin(Mathf.PI*fraction))+Vector3.up,.03f);
                        if(t.Current==edge.source)Require(t.Route.Count==1,"resized bridge does not record before vertex arrival");
                    }
                    Require(t.IsContinuous && t.Current==edge.destination && t.Route.Count==2,"resized bridge corridor accepts both sides: "+edge.source+" -> "+edge.destination);
                }
            }
        }
        var go=new GameObject("Clock validation");var clock=go.AddComponent<GraphStageClock>();int timeout=0;
        clock.Initialize(30,()=>timeout++);clock.Tick(5);Require(clock.Remaining==30,"paused before activation");
        clock.Resume();clock.Tick(2);clock.Penalize(10);Require(clock.Remaining==18 && clock.ActiveSeconds==2,"exact penalty separate from duration");
        clock.Pause();clock.Tick(5);Require(clock.Remaining==18,"correct pause");clock.Resume();clock.Tick(30);clock.Tick(30);
        Require(clock.Remaining==0 && timeout==1 && clock.ActiveSeconds==20,"single timeout and clamped active duration");
        UnityEngine.Object.DestroyImmediate(go);
        return "PASS: fifty progressive Graph variants, alternate routes and invalid answers, 200 seeded five-round selections without immediate repeats, platform rims and turns, both sides of every bridge, low-frame-rate midpoint crossing, off-path/teleport/reverse protection, and independent clock semantics.";
    }
    static List<string> FindRoute(GraphChallengeData graph,List<string> route)
    { return GraphAnswerValidator.FindSolution(graph); }
}
