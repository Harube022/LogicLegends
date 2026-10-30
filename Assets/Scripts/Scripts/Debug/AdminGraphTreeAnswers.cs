using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using LogicLegends.Graph;
using LogicLegends.Trees;

/// <summary>Read-only answer snapshots for the two stages using the shared Admin modal.</summary>
public static class AdminGraphTreeAnswers
{
    public static void Graph(List<AdminAnswerPage> pages)
    {
        if (!AdminAuthorization.IsAuthorized) return;
        var stage = Object.FindFirstObjectByType<GraphStageManager>(); if (stage == null || !stage.Ready) return;
        var c = stage.Challenge;
        pages.Add(new AdminAnswerPage("Current — Challenge " + (stage.ChallengeIndex + 1) + ", Question " + (stage.QuestionIndex+1) + " of 2",GraphSolution(c)));
        for(int challenge=0;challenge<stage.catalog.variantSets.Count;challenge++)
        {
            var body=new StringBuilder("Two questions are selected per challenge. Route examples are validated; other valid routes are accepted.\n\n");
            var bank=stage.catalog.variantSets[challenge].variants;
            for(int i=0;i<bank.Count;i++)
            {
                bool selected=stage.SelectedVariant(challenge,0)==i||stage.SelectedVariant(challenge,1)==i;
                body.AppendLine("BANK QUESTION "+(i+1)+AdminQuestionAnswers.Mark(selected,challenge==stage.ChallengeIndex&&i==stage.ActiveVariantIndex)+"\n"+GraphSolution(bank[i])+"\n");
            }
            pages.Add(new AdminAnswerPage("Challenge "+(challenge+1)+" — "+bank[0].type,body.ToString()));
        }
    }
    static string GraphSolution(GraphChallengeData c)
    {
        var path=GraphAnswerValidator.FindSolution(c);
        string answer=c.type==GraphChallengeType.Degree?c.Degree(c.degreeVertex).ToString():path==null?"No valid route (catalog error)":string.Join(" → ",path);
        return c.objective+"\n\nCORRECT ANSWER / VALID ROUTE: "+answer+"\nVertices: "+string.Join(", ",c.vertices.Select(v=>v.id))+
            "\nEdges: "+string.Join(", ",c.edges.Where(e=>e.traversalEnabled).Select(e=>e.source+(e.directed?" → ":" — ")+e.destination))+
            (c.type==GraphChallengeType.Cycle?"\nMinimum distinct vertices: "+c.minimumCycleVertices:"")+
            (c.requiredVertices.Length>0?"\nRequired visits: "+string.Join(", ",c.requiredVertices):"");
    }
    static string Route(GraphChallengeData c, List<string> path)
    {
        if (GraphAnswerValidator.Validate(c, path, null, true, out _)) return string.Join(" -> ", path);
        if (path.Count > c.vertices.Count + 1) return null;
        string current = path[path.Count - 1];
        foreach (var v in c.vertices)
        {
            if (!c.Connected(current, v.id) || path.Contains(v.id) && !(c.type == GraphChallengeType.Cycle && v.id == c.start && path.Count > 2)) continue;
            path.Add(v.id); var found = Route(c, path); path.RemoveAt(path.Count - 1); if (found != null) return found;
        }
        return null;
    }
    public static void Trees(List<AdminAnswerPage> pages)
    {
        if(!AdminAuthorization.IsAuthorized)return;
        var stage=Object.FindFirstObjectByType<TreeStageManager>();if(stage==null||!stage.Ready)return;
        pages.Add(new AdminAnswerPage("Current — Challenge "+(stage.ChallengeIndex+1)+", Question "+(stage.QuestionIndex+1)+" of 2",TreeSolution(stage.Challenge)));
        for(int c=0;c<stage.catalog.variantSets.Count;c++)
        {
            var body=new StringBuilder("Two selected questions: foundation, then harder structure.\n\n");
            var bank=stage.catalog.variantSets[c].variants;
            for(int i=0;i<bank.Count;i++)
            {
                int selectedQuestion=stage.SelectedVariant(c,0)==i?1:stage.SelectedVariant(c,1)==i?2:0;
                body.AppendLine("BANK QUESTION "+(i+1)+" — "+(bank[i].difficulty==2?"HARDER":"FOUNDATION")+
                    AdminQuestionAnswers.Mark(selectedQuestion>0,c==stage.ChallengeIndex&&i==stage.ActiveVariantIndex)+
                    (selectedQuestion>0?" — Question "+selectedQuestion+" of 2":"")+"\n"+TreeSolution(bank[i])+"\n");
            }
            pages.Add(new AdminAnswerPage("Challenge "+(c+1)+" — "+bank[0].title,body.ToString()));
        }
    }
    public static string TreeSolution(TreeChallengeData c)
    {
        string answer;
        if(c.type==TreeChallengeType.Identify)
            answer=string.Join(", ",c.nodes.Where(n=>TreeAnswerValidator.Validate(c,c.initial,n.id,null,out _)).Select(n=>n.id));
        else if(c.type==TreeChallengeType.Depth||c.type==TreeChallengeType.Height)
            answer=string.Join(", ",Enumerable.Range(0,c.nodes.Count+1).Where(n=>TreeAnswerValidator.Validate(c,c.initial,null,n,out _)));
        else if(c.type==TreeChallengeType.Spanning)
            answer="One valid solution: "+string.Join(", ",TreeSpanningSolution(c).Select(e=>e.source+" — "+e.destination))+". Keep "+(c.nodes.Count-1)+" edges. Other connected, cycle-free subsets are accepted.";
        else answer=string.Join(", ",c.required.Select(e=>e.source+" → "+e.destination));
        return c.objective+"\n\nCORRECT ANSWER: "+answer+"\nRoot: "+c.root+"\nInitial links: "+string.Join(", ",c.initial.Select(e=>e.source+(c.Directed?" → ":" — ")+e.destination))+"\n\n"+c.lesson;
    }
    public static List<TreeEdgeData> TreeSpanningSolution(TreeChallengeData c)
    {
        var result=new List<TreeEdgeData>();
        foreach(var e in c.initial)if(TreeAnswerValidator.Acyclic(c,result.Concat(new[]{e}).ToList()))result.Add(e);
        return result;
    }
}
