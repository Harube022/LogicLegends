using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LogicLegends.Inference
{
    /// <summary>Supports only the existing stairs, never a broad invisible disc over the pond.</summary>
    public sealed class InferenceAltarAccess : MonoBehaviour
    {
        Mesh support;
        public Vector3 Center { get; private set; }
        public float OuterRadius { get; private set; }
        public float TopHeight { get; private set; }

        public void Build()
        {
            if (support != null) return;
            var altar = GameObject.Find("ALTAR");
            var source = altar != null ? altar.transform.Find("Cylinder.077")?.GetComponent<MeshFilter>() : null;
            var collider = source != null ? source.GetComponent<MeshCollider>() : null;
            if (source == null || source.sharedMesh == null || collider == null) return;
            var vertices = source.sharedMesh.vertices; var triangles = source.sharedMesh.triangles;
            var area = new Dictionary<float, float>(); var bounds = new Dictionary<float, Bounds>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = source.transform.TransformPoint(vertices[triangles[i]]), b = source.transform.TransformPoint(vertices[triangles[i + 1]]), c = source.transform.TransformPoint(vertices[triangles[i + 2]]);
                Vector3 cross = Vector3.Cross(b - a, c - a);
                if (cross.normalized.y < .99f) continue;
                float y = Mathf.Round((a.y + b.y + c.y) / 3f * 100f) / 100f;
                if (!area.ContainsKey(y)) { area[y] = 0f; bounds[y] = new Bounds(a, Vector3.zero); }
                area[y] += cross.magnitude * .5f;
                Bounds box = bounds[y]; box.Encapsulate(a); box.Encapsulate(b); box.Encapsulate(c); bounds[y] = box;
            }
            if (area.Count == 0) return;
            float level = area.OrderByDescending(x => x.Value).First().Key;
            Bounds platform = bounds[level]; Center = platform.center; TopHeight = level + .035f;
            float inner = Mathf.Min(platform.extents.x, platform.extents.z) - .5f;
            var floor = GameObject.Find("FLOOR")?.GetComponent<Collider>();
            var water = GameObject.Find("Ocean50x50W750H750")?.GetComponent<Renderer>();
            var root = new GameObject("Altar Invisible Stair Ramp"); root.transform.SetParent(altar.transform, false);
            const int sides = 128, rings = 36;
            const float spacing = .5f;
            var points = new Vector3[sides * rings]; var valid = new bool[points.Length];
            var indices = new List<int>();
            for (int angle = 0; angle < sides; angle++)
            {
                float radians = angle * Mathf.PI * 2f / sides;
                Vector3 direction = new Vector3(Mathf.Cos(radians), 0, Mathf.Sin(radians));
                var profile = new List<Vector2>();
                for (int r = 0; r < rings; r++)
                {
                    float radius = inner + r * spacing;
                    Vector3 origin = new Vector3(Center.x, level + .45f, Center.z) + direction * radius;
                    RaycastHit hit;
                    if (!collider.Raycast(new Ray(origin, Vector3.down), out hit, 12f) || hit.normal.y < .45f) break;
                    profile.Add(new Vector2(radius, hit.point.y + .035f));
                }
                if (profile.Count < 2) continue;
                // A short landing is allowed only on visible dry ground at the stair's foot.
                // The submerged floor beneath the pond must never extend the ramp.
                float end = profile[profile.Count - 1].x;
                for (int r = profile.Count; r < rings && inner + r * spacing <= end + 1.5f; r++)
                {
                    float radius = inner + r * spacing;
                    Vector3 origin = new Vector3(Center.x, level + .45f, Center.z) + direction * radius;
                    RaycastHit hit;
                    if (floor == null || !floor.Raycast(new Ray(origin, Vector3.down), out hit, 12f) ||
                        (water != null && hit.point.y <= water.bounds.max.y + .1f)) break;
                    profile.Add(new Vector2(radius, hit.point.y + .035f));
                }
                // Upper concave envelope connects tread edges, removing vertical risers
                // without raising an entire flat cap above the visible stair surfaces.
                var hull = new List<Vector2>();
                foreach (Vector2 p in profile)
                {
                    while (hull.Count >= 2)
                    {
                        Vector2 a = hull[hull.Count - 2], b = hull[hull.Count - 1];
                        if ((b.y - a.y) / (b.x - a.x) >= (p.y - b.y) / (p.x - b.x)) break;
                        hull.RemoveAt(hull.Count - 1);
                    }
                    hull.Add(p);
                }
                int segment = 0;
                for (int r = 0; r < profile.Count; r++)
                {
                    float radius = profile[r].x;
                    while (segment + 2 < hull.Count && radius > hull[segment + 1].x) segment++;
                    Vector2 a = hull[segment], b = hull[segment + 1];
                    float height = Mathf.Lerp(a.y, b.y, Mathf.InverseLerp(a.x, b.x, radius));
                    int id = angle * rings + r;
                    points[id] = root.transform.InverseTransformPoint(new Vector3(Center.x, height, Center.z) + direction * radius);
                    valid[id] = true; OuterRadius = Mathf.Max(OuterRadius, radius);
                }
            }
            for (int angle = 0; angle < sides; angle++)
                for (int r = 0; r < rings - 1; r++)
                {
                    int a = angle * rings + r, b = ((angle + 1) % sides) * rings + r;
                    int c = a + 1, d = b + 1;
                    if (valid[a] && valid[b] && valid[c] && valid[d]) indices.AddRange(new[] { a, b, c, b, d, c });
                }
            support = new Mesh { name = "Altar Stair Support" };
            support.vertices = points; support.triangles = indices.ToArray(); support.RecalculateNormals(); support.RecalculateBounds();
            root.AddComponent<MeshCollider>().sharedMesh = support;
        }
        void OnDestroy() { if (support != null) Destroy(support); }
    }
}
