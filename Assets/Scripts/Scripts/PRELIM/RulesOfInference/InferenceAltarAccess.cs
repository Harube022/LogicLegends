using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LogicLegends.Inference
{
    /// <summary>Invisible continuous stair support, derived from the existing altar mesh.</summary>
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
            if (source == null || source.sharedMesh == null) return;
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
            Bounds platform = bounds[level]; Center = platform.center; TopHeight = level + .2f;
            float inner = Mathf.Max(platform.extents.x, platform.extents.z) + 4f;
            OuterRadius = inner + 10f;
            float bottom = source.GetComponent<Renderer>().bounds.min.y + .15f;
            var root = new GameObject("Altar Invisible Stair Ramp"); root.transform.SetParent(altar.transform, false);
            const int sides = 96;
            var points = new Vector3[1 + sides * 2]; var indices = new List<int>();
            points[0] = root.transform.InverseTransformPoint(new Vector3(Center.x, TopHeight, Center.z));
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides; var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                points[1 + i] = root.transform.InverseTransformPoint(new Vector3(Center.x, TopHeight, Center.z) + direction * inner);
                points[1 + sides + i] = root.transform.InverseTransformPoint(new Vector3(Center.x, bottom, Center.z) + direction * OuterRadius);
                int a = 1 + i, b = 1 + (i + 1) % sides, c = a + sides, d = b + sides;
                indices.AddRange(new[] { 0, b, a, a, b, c, b, d, c });
            }
            support = new Mesh { name = "Altar Stair Support" }; support.vertices = points; support.triangles = indices.ToArray(); support.RecalculateNormals(); support.RecalculateBounds();
            root.AddComponent<MeshCollider>().sharedMesh = support;
        }
        void OnDestroy() { if (support != null) Destroy(support); }
    }
}
