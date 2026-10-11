using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LogicLegends.Graph
{
    public enum GraphChallengeType { VerticesAndEdges, Degree, Path, DirectedPath, Cycle }

    [Serializable]
    public sealed class GraphVertexData
    {
        public string id;
        public string label;
        public Vector3 position;
        public GraphVertexData(string id, float x, float z) { this.id = label = id; position = new Vector3(x, 0, z); }
    }

    [Serializable]
    public sealed class GraphEdgeData
    {
        public string source, destination;
        public bool directed, traversalEnabled = true;
        public GraphEdgeData(string source, string destination, bool directed = false)
        { this.source = source; this.destination = destination; this.directed = directed; }
        public bool Allows(string from, string to) => traversalEnabled &&
            ((source == from && destination == to) || (!directed && source == to && destination == from));
    }

    [Serializable]
    public sealed class GraphChallengeData
    {
        public string title;
        public GraphChallengeType type;
        [TextArea] public string objective;
        [TextArea] public string lesson;
        public string start = "A", destination, degreeVertex = "C";
        public List<GraphVertexData> vertices = new List<GraphVertexData>();
        public List<GraphEdgeData> edges = new List<GraphEdgeData>();
        public GraphVertexData Vertex(string id) => vertices.Find(v => v.id == id);
        public bool Connected(string from, string to) => edges.Any(e => e.Allows(from, to));
        public int Degree(string id) => edges.Where(e => e.traversalEnabled).Sum(e =>
            (e.source == id ? 1 : 0) + (e.destination == id ? 1 : 0));
    }

    public static class GraphAnswerValidator
    {
        public static bool Validate(GraphChallengeData c, IReadOnlyList<string> route, int? answer, bool continuous, out string feedback)
        {
            feedback = "";
            if (c.type == GraphChallengeType.Degree)
            {
                if (answer.HasValue && answer.Value == c.Degree(c.degreeVertex)) return true;
                feedback = "Count the edges incident to " + c.degreeVertex + ". Your selection is preserved.";
                return false;
            }
            if (!continuous) { feedback = "The route left a connecting bridge. Use RETRY ROUTE to begin again."; return false; }
            if (route == null || route.Count < 2 || route[0] != c.start)
            { feedback = "Begin at " + c.start + " and cross a connecting bridge first."; return false; }
            for (int i = 1; i < route.Count; i++)
                if (!c.Connected(route[i - 1], route[i]))
                { feedback = "Every move must follow a connecting edge and its direction."; return false; }
            if (c.type == GraphChallengeType.Cycle)
            {
                var middle = route.Take(route.Count - 1).ToList();
                if (route[route.Count - 1] == c.start && middle.Count >= 3 && middle.Distinct().Count() == middle.Count) return true;
                feedback = "Return to " + c.start + " after at least three distinct vertices, without repeating an intermediate vertex.";
                return false;
            }
            if (route[route.Count - 1] != c.destination)
            { feedback = "Reach " + c.destination + " before confirming."; return false; }
            if (route.Distinct().Count() != route.Count)
            { feedback = "A simple path cannot repeat a vertex. Use RETRY ROUTE."; return false; }
            return true;
        }
    }
}
