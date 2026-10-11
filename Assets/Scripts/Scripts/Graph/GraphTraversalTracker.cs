using System.Collections.Generic;
using UnityEngine;

namespace LogicLegends.Graph
{
    /// <summary>One continuous bridge transaction at a time, independent of trigger jitter.</summary>
    public sealed class GraphTraversalTracker
    {
        private readonly List<string> route = new List<string>();
        private GraphChallengeData graph;
        private GraphEdgeData pending;
        private string next;
        private Vector3 previous;
        private bool sampled, crossedMiddle;
        // Match the 7-unit vertex collider and 4.4-unit bridge deck. A character
        // can still be supported when its capsule centre overhangs an edge.
        private float vertexRadius, bridgeHalfWidth;
        private float footprintRadius;
        public IReadOnlyList<string> Route => route;
        public GraphEdgeData PendingEdge => pending;
        public bool IsContinuous { get; private set; } = true;
        public string InterruptionReason { get; private set; }
        public string Current => route.Count == 0 ? null : route[route.Count - 1];
        public bool AtVertex { get; private set; }
        public void Reset(GraphChallengeData data, Vector3 position, float characterFootprint = .78f,
            float platformRadius = 3.5f, float pathHalfWidth = 2.2f)
        {
            graph = data; route.Clear(); route.Add(data.start);
            footprintRadius = Mathf.Max(0, characterFootprint);
            vertexRadius = platformRadius; bridgeHalfWidth = pathHalfWidth;
            pending = null; next = null; sampled = true; previous = position;
            crossedMiddle = false; IsContinuous = true; AtVertex = true;
            InterruptionReason = null;
        }
        public void Invalidate(string reason = "Route interrupted")
        { IsContinuous = false; pending = null; AtVertex = false; InterruptionReason = reason; }
        public void Sample(Vector3 position, float deltaTime, bool teleporting = false)
        {
            if (graph == null || !IsContinuous) return;
            // CharacterController depenetration may lift the root on spawn/steps; judge
            // graph travel in the horizontal plane, retaining a separate height limit.
            float displacement = new Vector2(previous.x-position.x,previous.z-position.z).magnitude;
            if (teleporting) { Invalidate("Teleport during traversal"); return; }
            if (sampled && displacement > Mathf.Max(.85f, deltaTime * 22f))
            { Invalidate("Discontinuous movement: " + displacement + " in " + deltaTime + "s"); return; }
            Vector3 lastPosition = previous;
            sampled = true; previous = position;
            var current = graph.Vertex(Current);
            AtVertex = Near(position, current.position);
            if (AtVertex) { pending = null; crossedMiddle = false; return; }
            // Do not bind a bridge while still supported by the source platform.
            // At a junction its adjacent bridge corridors overlap; turning on
            // the rim must not accidentally lock onto a different connection.
            if (SupportedAtVertex(position, current.position))
            { pending = null; crossedMiddle = false; return; }
            if (pending == null)
            {
                float closest = float.PositiveInfinity;
                foreach (var edge in graph.edges)
                {
                    string target = edge.source == Current ? edge.destination : edge.source;
                    if (!edge.Allows(Current, target)) continue;
                    Vector3 end = graph.Vertex(target).position;
                    if (OnBridge(position, current.position, end, out float fraction) &&
                        (fraction < .5f || SupportedAtVertex(lastPosition, current.position)))
                    {
                        Vector3 offset = position - Vector3.Lerp(current.position, end, fraction); offset.y = 0;
                        if (offset.sqrMagnitude < closest)
                        { closest = offset.sqrMagnitude; pending = edge; next = target; }
                    }
                }
                if (pending == null)
                {
                    Invalidate("Left vertex without an allowed connecting edge"); return;
                }
            }
            Vector3 destination = graph.Vertex(next).position;
            // The middle may lie between two samples at a lower frame rate.
            // Both endpoints must be on this same bridge, with the existing
            // movement/teleport checks still applied before accepting it.
            if (OnBridge(lastPosition, current.position, destination, out float previousT) &&
                OnBridge(position, current.position, destination, out float currentT) &&
                previousT <= .5f && currentT >= .5f) crossedMiddle = true;
            if (Near(position, destination))
            {
                if (!crossedMiddle) { Invalidate("Arrived without crossing bridge midpoint"); return; }
                route.Add(next); pending = null; crossedMiddle = false; AtVertex = true;
                return;
            }
            if (!OnBridge(position, current.position, destination, out float t))
            {
                if (SupportedAtVertex(position, current.position) || SupportedAtVertex(position, destination)) return;
                Invalidate("Left connecting bridge"); return;
            }
            if (t >= .4f && t <= .65f) crossedMiddle = true;
        }
        private bool Near(Vector3 p, Vector3 v) => Mathf.Abs(p.y - v.y) <= 6f &&
            new Vector2(p.x - v.x, p.z - v.z).sqrMagnitude <= vertexRadius * vertexRadius;
        private bool SupportedAtVertex(Vector3 p, Vector3 v) => Mathf.Abs(p.y - v.y) <= 6f &&
            new Vector2(p.x - v.x, p.z - v.z).sqrMagnitude <= (vertexRadius + footprintRadius) * (vertexRadius + footprintRadius);
        private bool OnBridge(Vector3 p, Vector3 a, Vector3 b, out float t)
        {
            Vector3 ab = b - a; ab.y = 0;
            Vector3 ap = p - a; ap.y = 0;
            t = Vector3.Dot(ap, ab) / ab.sqrMagnitude;
            return t >= 0 && t <= 1 && Mathf.Abs(p.y - a.y) <= 6f &&
                (ap - ab * t).sqrMagnitude <= (bridgeHalfWidth + footprintRadius) * (bridgeHalfWidth + footprintRadius);
        }
    }
}
