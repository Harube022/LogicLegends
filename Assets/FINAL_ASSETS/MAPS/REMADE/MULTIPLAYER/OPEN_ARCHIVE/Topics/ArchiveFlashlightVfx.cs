using UnityEngine;
using UnityEngine.Rendering;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Presentation for the existing supply spotlight. Owns no inventory, charge or network state.</summary>
    [DefaultExecutionOrder(10043), DisallowMultipleComponent]
    public sealed class ArchiveFlashlightVfx : MonoBehaviour
    {
        [Range(2, 20)] public float maximumVisibleLength = 14;
        [Range(.005f, .08f)] public float surfaceOffset = .025f;
        [Range(.2f, 2)] public float maximumImpactRadius = 1.1f;

        public MeshRenderer BeamRenderer => beam;
        public MeshRenderer ImpactRenderer => impact;
        public Collider HitSurface { get; private set; }
        public Vector3 HitPoint { get; private set; }
        public float CurrentHitDistance { get; private set; }
        public float VisualLength { get; private set; }

        const int Segments = 20;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int ModeId = Shader.PropertyToID("_Mode");
        readonly RaycastHit[] hits = new RaycastHit[64];
        MaterialPropertyBlock beamProperties, impactProperties;
        void Awake()
        {
            // Unity native allocations belong on the main thread, not MonoBehaviour construction.
            beamProperties=new MaterialPropertyBlock();impactProperties=new MaterialPropertyBlock();
        }
        Light source;
        Player owner;
        Material material;
        float maximumRange = 22, maximumIntensity, beamOpacity = .05f, impactOpacity = .16f;
        MeshRenderer beam, impact;
        Mesh beamMesh, impactMesh;

        /// <summary>maximumRange is the configured unoccluded range, never the previous frame's clipped light range.</summary>
        public void Configure(Light spotlight, Player player, Material sharedMaterial, float maximumRange,
            float beamOpacity, float impactOpacity)
        {
            source = spotlight; owner = player;
            maximumIntensity = spotlight.intensity;
            this.maximumRange = Mathf.Max(.1f, maximumRange);
            this.beamOpacity = Mathf.Clamp(beamOpacity, 0, .2f);
            this.impactOpacity = Mathf.Clamp(impactOpacity, 0, .5f);
            if (sharedMaterial == null) { material = null; Hide(); return; }
            if (beam == null) BuildRenderers();
            if (material != sharedMaterial)
            {
                material = sharedMaterial;
                beam.sharedMaterial = impact.sharedMaterial = material;
            }
        }

        void LateUpdate()
        {
            if (source == null || !source.enabled || !source.gameObject.activeInHierarchy || material == null)
            { Hide(); return; }

            Vector3 origin = source.transform.position, direction = source.transform.forward;
            float nearest = maximumRange; RaycastHit surface = default; bool found = false;
            // Ignore the player's capsule and water/gameplay triggers. The fixed buffer avoids per-frame allocations.
            int mask = Physics.DefaultRaycastLayers & ~(1 << 5); // UI is a built-in layer.
            int count = Physics.RaycastNonAlloc(origin, direction, hits, maximumRange, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var candidate = hits[i];
                if (candidate.collider == null || candidate.distance < .015f || candidate.distance > nearest) continue;
                if (candidate.collider.GetComponentInParent<Player>() != null ||
                    owner != null && candidate.collider.transform.IsChildOf(owner.transform)) continue;
                nearest = candidate.distance; surface = candidate; found = true;
            }
            HitSurface = found ? surface.collider : null;
            HitPoint = found ? surface.point : origin + direction * maximumRange;
            CurrentHitDistance = nearest;
            // Preserve the real light's falloff range. Clipping it at a wall would also attenuate
            // that wall to darkness. Only the visible fog cone and surface glow stop at the hit.
            source.range = maximumRange;
            // Strong enough for night navigation, with a softer hotspot against close walls.
            // Configure receives the authored intensity each frame, so this never compounds.
            source.intensity = Mathf.Lerp(Mathf.Min(55, maximumIntensity), maximumIntensity,
                Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1, 3, nearest)));

            VisualLength = Mathf.Min(nearest, maximumVisibleLength);
            float radius = Mathf.Tan(source.spotAngle * .5f * Mathf.Deg2Rad) * VisualLength;
            beam.transform.SetPositionAndRotation(origin, source.transform.rotation);
            SetWorldScale(beam.transform, new Vector3(Mathf.Max(.025f, radius), Mathf.Max(.025f, radius), Mathf.Max(.015f, VisualLength)));
            var color = source.color; color.a = beamOpacity;
            beamProperties.SetColor(ColorId, color); beamProperties.SetFloat(ModeId, 0);
            beam.SetPropertyBlock(beamProperties);
            beam.enabled = VisualLength > .08f && beamOpacity > 0;

            float facing = found ? Mathf.Clamp01(Vector3.Dot(surface.normal, -direction)) : 0;
            impact.enabled = found && facing > .04f && impactOpacity > 0;
            if (impact.enabled)
            {
                float size = Mathf.Clamp(.15f + nearest * .045f, .18f, maximumImpactRadius) * 2;
                impact.transform.SetPositionAndRotation(surface.point + surface.normal * surfaceOffset, Quaternion.LookRotation(surface.normal));
                SetWorldScale(impact.transform, new Vector3(size, size, 1));
                color.a = impactOpacity * Mathf.Clamp01((facing - .04f) / .45f);
                impactProperties.SetColor(ColorId, color); impactProperties.SetFloat(ModeId, 1);
                impact.SetPropertyBlock(impactProperties);
            }
        }

        static void SetWorldScale(Transform target, Vector3 scale)
        {
            var inherited = target.parent != null ? target.parent.lossyScale : Vector3.one;
            target.localScale = new Vector3(scale.x / Mathf.Max(.0001f, Mathf.Abs(inherited.x)),
                scale.y / Mathf.Max(.0001f, Mathf.Abs(inherited.y)), scale.z / Mathf.Max(.0001f, Mathf.Abs(inherited.z)));
        }

        void BuildRenderers()
        {
            beamMesh = new Mesh { name = "Archive_Flashlight_Cone_20" };
            var vertices = new Vector3[(Segments + 1) * 2];
            var normals = new Vector3[vertices.Length]; var uv = new Vector2[vertices.Length];
            var triangles = new int[Segments * 6];
            for (int i = 0; i <= Segments; i++)
            {
                float angle = i * Mathf.PI * 2 / Segments; var radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                vertices[i * 2] = radial * .008f; vertices[i * 2 + 1] = radial + Vector3.forward;
                normals[i * 2] = normals[i * 2 + 1] = (radial - Vector3.forward * .992f).normalized;
                uv[i * 2] = new Vector2((float)i / Segments, 0); uv[i * 2 + 1] = new Vector2((float)i / Segments, 1);
                if (i == Segments) continue;
                int start = i * 6, a = i * 2;
                triangles[start] = a; triangles[start + 1] = a + 2; triangles[start + 2] = a + 1;
                triangles[start + 3] = a + 1; triangles[start + 4] = a + 2; triangles[start + 5] = a + 3;
            }
            beamMesh.vertices = vertices; beamMesh.normals = normals; beamMesh.uv = uv; beamMesh.triangles = triangles; beamMesh.RecalculateBounds();
            impactMesh = new Mesh { name = "Archive_Flashlight_Soft_Impact" };
            impactMesh.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(-.5f, .5f, 0), new Vector3(.5f, .5f, 0) };
            impactMesh.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            impactMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            impactMesh.triangles = new[] { 0, 2, 1, 1, 2, 3 }; impactMesh.RecalculateBounds();
            beam = Renderer("Flashlight_Soft_Beam", beamMesh); impact = Renderer("Flashlight_Surface_Glow", impactMesh);
        }

        MeshRenderer Renderer(string name, Mesh mesh)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>(); renderer.enabled = false;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            return renderer;
        }

        public void Hide()
        {
            if (beam != null) beam.enabled = false;
            if (impact != null) impact.enabled = false;
            HitSurface = null; CurrentHitDistance = VisualLength = 0;
        }
        void OnDisable() { Hide(); if (source != null) { source.range = maximumRange; source.intensity = maximumIntensity; } }
        void OnDestroy()
        {
            if (source != null) source.range = maximumRange;
            if (beam != null) Destroy(beam.gameObject);
            if (impact != null) Destroy(impact.gameObject);
            if (beamMesh != null) Destroy(beamMesh);
            if (impactMesh != null) Destroy(impactMesh);
        }
    }
}
