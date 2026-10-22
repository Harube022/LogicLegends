using System.Collections.Generic;
using System.Linq;
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
        var path = new List<string> { c.start };
        string answer = c.type == GraphChallengeType.Degree ? c.Degree(c.degreeVertex).ToString() : Route(c, path);
        pages.Add(new AdminAnswerPage("Challenge " + (stage.ChallengeIndex + 1) + " / " + c.title,
            c.objective + "\n\nCORRECT ANSWER / ONE VALID ROUTE: " + answer + "\n\n" + c.lesson));
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
        if (!AdminAuthorization.IsAuthorized) return;
        var stage = Object.FindFirstObjectByType<TreeStageManager>(); if (stage == null || !stage.Ready) return;
        var c = stage.Challenge; string answer = "";
        if (c.type == TreeChallengeType.Identify)
            answer = string.Join(", ", c.nodes.Where(n => TreeAnswerValidator.Validate(c, c.initial, n.id, null, out _)).Select(n => n.id));
        else if (c.type == TreeChallengeType.Depth || c.type == TreeChallengeType.Height)
            answer = string.Join(", ", Enumerable.Range(0, c.nodes.Count + 1).Where(n => TreeAnswerValidator.Validate(c, c.initial, null, n, out _)));
        else if (c.type == TreeChallengeType.Spanning)
            answer = "Keep a connected, cycle-free subset of the supplied edges with exactly " + (c.nodes.Count - 1) + " edges. Multiple answers are valid.";
        else answer = string.Join(", ", c.required.Select(e => e.source + " -> " + e.destination));
        pages.Add(new AdminAnswerPage("Challenge " + (stage.ChallengeIndex + 1) + " / " + c.title, c.objective + "\n\nCORRECT ANSWER: " + answer + "\n\n" + c.lesson));
    }
}
