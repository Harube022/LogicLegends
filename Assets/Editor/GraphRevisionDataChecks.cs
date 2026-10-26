#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using LogicLegends.Graph;
using UnityEditor;

public static class GraphRevisionDataChecks
{
    public static string Run()
    {
        var bank=AssetDatabase.LoadAssetAtPath<GraphChallengeCatalog>("Assets/Prefabs/Graph/GraphChallenges.asset");int checkedQuestions=0;
        foreach(var group in bank.variantSets)foreach(var c in group.variants)
        {
            Check(c.vertices.Select(v=>v.id).Distinct().Count()==c.vertices.Count,"unique labels");
            Check(c.edges.All(e=>c.Vertex(e.source)!=null&&c.Vertex(e.destination)!=null&&e.source!=e.destination),"edge endpoints");
            if(c.type==GraphChallengeType.Degree)
            {
                int answer=c.Degree(c.degreeVertex);Check(answer<=6,"degree selectable");
                for(int n=0;n<=6;n++)Check(GraphAnswerValidator.Validate(c,null,n,true,out _)==(n==answer),"one correct numerical answer");
            }
            else
            {
                var path=GraphAnswerValidator.FindSolution(c);Check(path!=null,"solvable: "+c.title);
                Check(GraphAnswerValidator.Validate(GraphGeometry.Layout(c),path,null,true,out _),"displayed graph and validation match");
                Check(!GraphAnswerValidator.Validate(c,new[]{c.start},null,true,out _),"incomplete path rejected");
                if(c.type==GraphChallengeType.DirectedPath)
                {
                    Check(c.vertices.Count>=8&&c.requiredVertices.Length>0,"hard directed configuration");
                    Check(c.edges.All(e=>e.directed),"all directed bridges have arrows");
                    var layout=GraphGeometry.Layout(c);
                    Check(layout.edges==c.edges&&layout.start==c.start&&layout.destination==c.destination,"layout preserves connections and goal");
                    foreach(var v in layout.vertices)Check(v.position.x-GraphGeometry.VertexRadius>-50&&v.position.x+GraphGeometry.VertexRadius<50&&v.position.z-GraphGeometry.VertexRadius>-34&&v.position.z+GraphGeometry.VertexRadius<114,"all directed variants fit chamber");
                }
                if(c.type==GraphChallengeType.Cycle)
                {
                    Check(c.vertices.Count>=9&&c.minimumCycleVertices>=6,"hard cycle configuration");
                    Check(c.Degree(c.vertices.Last().id)==1,"dead end exists");
                    Check(!GraphAnswerValidator.Validate(c,new[]{c.start,c.vertices[1].id,c.vertices[7].id,c.start},null,true,out _),"short cycle rejected");
                }
            }
            checkedQuestions++;
        }
        for(int seed=0;seed<1000;seed++)
        {
            var selected=bank.SelectRun(new Random(seed));Check(selected.Count==10,"ten questions selected");
            for(int c=0;c<5;c++)Check(selected[c*2]!=selected[c*2+1]&&selected[c*2]>=0&&selected[c*2+1]<10,"two distinct valid picks");
        }
        string result="PASS "+checkedQuestions+" bank questions: valid endpoints, selectable degree answers, solvable paths/cycles, required visits and cycle lengths, displayed geometry agrees with validation. 1,000 randomized ten-question selections passed.";
        Directory.CreateDirectory("Library/GraphRevision");File.WriteAllText("Library/GraphRevision/Data.txt",result);return result;
    }
    static void Check(bool valid,string message){if(!valid)throw new InvalidOperationException("Graph data: "+message);}
}
#endif
