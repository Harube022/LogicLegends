using System.Linq;
using UnityEngine;

namespace LogicLegends.Graph
{
    /// <summary>Graph-only dimensions from the requested vertex/edge Inspector scales.</summary>
    public static class GraphGeometry
    {
        public const float VertexScale = 2.28f;
        public static readonly Vector3 EdgeScale = new Vector3(2.5f, 2f, 4f);
        public const float VertexRadius = 3.5f * VertexScale;
        public const float BridgeHalfWidth = 2.2f * 2.5f;
        public const float PlatformOverlap = .6f * VertexScale;
        public const float LabelHeight = 6.95f, LabelScale = 1.5f;
        // Preserve the reference 12-unit connection's 6.2-unit deck, enlarged
        // four times in length, with its ends still overlapping the platforms.
        public const float LayoutScale = ((12f - 5.8f) * 4f + 2f * (VertexRadius - PlatformOverlap)) / 12f;
        public const float DirectedDepthOffset = -24f;
        public static readonly Vector3 BookOffset = new Vector3(-1.7f, 0, -.7f) * VertexScale;

        /// <summary>Fit static compound collision to the imported statue, without blocking its animated page.</summary>
        public static void EnsureBookCollision(Transform book)
        {
            if (book == null) return;
            string[] solidParts = { "Square foundation", "Lower octagonal step", "Middle octagonal step",
                "Upper octagonal step", "Pedestal foot", "Tapered square pillar", "Lectern capital",
                "Central book spine", "Left thick cover", "Right thick cover" };
            foreach (var mesh in book.GetComponentsInChildren<MeshFilter>(true))
            {
                if (System.Array.IndexOf(solidParts, mesh.name) < 0 || mesh.sharedMesh == null) continue;
                var collider = mesh.GetComponent<BoxCollider>();
                if (collider == null) collider = mesh.gameObject.AddComponent<BoxCollider>();
                collider.center = mesh.sharedMesh.bounds.center;
                collider.size = mesh.sharedMesh.bounds.size;
                collider.isTrigger = false; collider.enabled = true;
            }
        }

        public static bool TryGetSurfaceHeight(Transform artwork, Vector3 position, out float height)
        {
            height = float.NegativeInfinity;
            if (artwork == null) return false;
            foreach (var surface in artwork.GetComponentsInChildren<Collider>())
            {
                if (!surface.enabled || surface.isTrigger) continue;
                var bounds = surface.bounds;
                var ray = new Ray(new Vector3(position.x, bounds.max.y + 1, position.z), Vector3.down);
                if (surface.Raycast(ray, out RaycastHit hit, bounds.size.y + 2) && hit.normal.y > .5f)
                    height = Mathf.Max(height, hit.point.y);
            }
            return !float.IsNegativeInfinity(height);
        }

        // Translate all eight directed platforms inside the chamber, retaining junction angles and spacing.
        // This changes positions only: IDs, edges, directions and answers are shared unchanged.
        public static GraphChallengeData Layout(GraphChallengeData source) => new GraphChallengeData
        {
            title = source.title, type = source.type, objective = source.objective, lesson = source.lesson,
            start = source.start, destination = source.destination, degreeVertex = source.degreeVertex,
            requiredVertices = source.requiredVertices, minimumCycleVertices = source.minimumCycleVertices,
            edges = source.edges,
            vertices = source.vertices.Select(v => new GraphVertexData(v.id, 0, 0)
            { label = v.label, position = new Vector3(v.position.x * LayoutScale, v.position.y, v.position.z * LayoutScale + (source.type == GraphChallengeType.DirectedPath && source.vertices.Count >= 8 ? DirectedDepthOffset : 0f)) }).ToList()
        };
    }
}
