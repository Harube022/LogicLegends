using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Lighting for the Truth_Table area only. Its parent is enabled by AreaVisibilityManager.
/// The yellow lantern submesh supplies the fixture position, independent of the curved post.
/// </summary>
public sealed class TruthTableDuskLighting : MonoBehaviour
{
    [Header("Truth Table Sunset Environment")]
    [SerializeField] private Light sunlight;
    [SerializeField, Range(0f, 2f)] private float sunlightIntensity = 0.85f;
    [SerializeField] private Color sunlightColor = new Color(1f, 0.74f, 0.5f);
    [SerializeField] private Color ambientSky = new Color(0.36f, 0.29f, 0.36f);
    [SerializeField] private Color ambientEquator = new Color(0.3f, 0.24f, 0.22f);
    [SerializeField] private Color ambientGround = new Color(0.15f, 0.125f, 0.15f);
    [SerializeField, Range(0f, 2f)] private float skyExposure = 1.05f;
    [SerializeField] private Color skyTint = new Color(0.6f, 0.52f, 0.48f, 1f);

    [Header("Streetlamp Fixture")]
    [SerializeField] private Color lampColor = new Color(1f, 0.71f, 0.4f);
    [SerializeField, Min(0f)] private float lampIntensity = 22f;
    [SerializeField, Min(0.1f)] private float lampRange = 17f;
    [SerializeField, Min(0f)] private float fixtureGlow = 1.2f;
    [SerializeField, Range(-1f, 1f)] private float fixtureHeightOffset = 0.32f;
    [SerializeField, Range(-1f, 1f)] private float fixtureSideOffset = -0.52f;
    [SerializeField, Min(0f)] private float lightBelowFixture = 1f;
    [SerializeField, Range(20f, 120f)] private float poolOuterAngle = 68f;
    [SerializeField, Range(0f, 120f)] private float poolInnerAngle = 48f;

    [Header("Occasional Flicker")]
    [SerializeField, Min(0)] private int flickerEveryNthLamp = 4;
    [SerializeField, Min(0.1f)] private float minimumFlickerInterval = 4f;
    [SerializeField, Min(0.1f)] private float maximumFlickerInterval = 9f;
    [SerializeField, Range(0.5f, 1f)] private float lowestFlickerBrightness = 0.88f;
    [SerializeField, Min(0.1f)] private float flickerDuration = 0.48f;

    private sealed class LampState
    {
        public Light light;
        public Renderer renderer;
        public Material[] originalMaterials;
        public int glowMaterialIndex = -1;
        public MaterialPropertyBlock properties;
        public GameObject fallbackGlow;
        public bool flickers;
        public float nextFlicker;
        public float flickerStarted = -1f;
        public float flickerLength;
    }

    public readonly List<Light> LampLights = new List<Light>();
    public int FixtureCount => lamps.Count;
    public int FlickeringCount { get { int count=0;foreach(var lamp in lamps)if(lamp.flickers)count++;return count; } }
    System.Random flickerRandom;
    float lastIntensity, lastGlow;
    float NextFloat(float min,float max)=>min+(float)flickerRandom.NextDouble()*(max-min);

    private readonly List<LampState> lamps = new List<LampState>();
    private readonly Dictionary<Material, Material> glowingMaterials = new Dictionary<Material, Material>();
    private Material originalSkybox;
    private Material duskSkybox;
    private Material fallbackGlowMaterial;
    private AmbientMode originalAmbientMode;
    private Color originalAmbientSky;
    private Color originalAmbientEquator;
    private Color originalAmbientGround;
    private float originalSunIntensity;
    private Color originalSunColor;
    private bool lightingApplied;

    private void OnEnable()
    {
        ApplyDusk();
        ConfigureStreetlamps();
    }

    private void ApplyDusk()
    {
        if (lightingApplied) return;
        originalSkybox = RenderSettings.skybox;
        originalAmbientMode = RenderSettings.ambientMode;
        originalAmbientSky = RenderSettings.ambientSkyColor;
        originalAmbientEquator = RenderSettings.ambientEquatorColor;
        originalAmbientGround = RenderSettings.ambientGroundColor;
        if (sunlight == null) sunlight = RenderSettings.sun;
        if (sunlight != null)
        {
            originalSunIntensity = sunlight.intensity;
            originalSunColor = sunlight.color;
            sunlight.intensity = sunlightIntensity;
            sunlight.color = sunlightColor;
        }

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = ambientSky;
        RenderSettings.ambientEquatorColor = ambientEquator;
        RenderSettings.ambientGroundColor = ambientGround;
        if (originalSkybox != null)
        {
            duskSkybox = new Material(originalSkybox) { name = "TruthTableSunsetSky (Runtime)" };
            if (duskSkybox.HasProperty("_Exposure")) duskSkybox.SetFloat("_Exposure", skyExposure);
            if (duskSkybox.HasProperty("_Tint")) duskSkybox.SetColor("_Tint", skyTint);
            if (duskSkybox.HasProperty("_SkyTint")) duskSkybox.SetColor("_SkyTint", skyTint);
            RenderSettings.skybox = duskSkybox;
        }
        lightingApplied = true;
    }

    private void ConfigureStreetlamps()
    {
        lamps.Clear();LampLights.Clear();flickerRandom=new System.Random(21428);
        lastIntensity=lampIntensity;lastGlow=fixtureGlow;
        var found = new List<Transform>();
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
            if (child.name == "STREETLAMP2" || child.name.StartsWith("STREETLAMP2 (", StringComparison.Ordinal))
                found.Add(child);
        found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        for (int index = 0; index < found.Count; index++)
        {
            Transform lamp = found[index];
            Renderer fixture = FindFixtureRenderer(lamp);
            if (fixture == null) continue;
            Bounds bounds = fixture.bounds;
            Vector3 fixturePosition = bounds.center + Vector3.up * (bounds.extents.y * fixtureHeightOffset)
                + lamp.right * (Mathf.Max(bounds.extents.x, bounds.extents.z) * fixtureSideOffset);
            // Use the actual yellow lantern submesh, not the bounds of the entire curved post.
            var filter=fixture.GetComponent<MeshFilter>();var materials=fixture.sharedMaterials;
            if(filter!=null&&filter.sharedMesh!=null)
                for(int slot=0;slot<materials.Length&&slot<filter.sharedMesh.subMeshCount;slot++)
                    if(materials[slot]!=null&&materials[slot].name.IndexOf("Celestial Gold",StringComparison.OrdinalIgnoreCase)>=0)
                    {fixturePosition=filter.transform.TransformPoint(filter.sharedMesh.GetSubMesh(slot).bounds.center);break;}

            Transform lightTransform = lamp.Find("TruthTableFixtureLight");
            if (lightTransform == null)
            {
                var lightObject = new GameObject("TruthTableFixtureLight");
                lightTransform = lightObject.transform;
                lightTransform.SetParent(lamp, false);
            }
            // Unshadowed light in the translucent-looking yellow lantern reaches the ground
            // without the imported opaque glass blocking its entire pool.
            lightTransform.position = fixturePosition;
            lightTransform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            Light poolLight = lightTransform.GetComponent<Light>();
            if (poolLight == null) poolLight = lightTransform.gameObject.AddComponent<Light>();
            poolLight.type = LightType.Point;
            poolLight.color = lampColor;
            poolLight.intensity = lampIntensity;
            poolLight.range = lampRange;
            poolLight.spotAngle = poolOuterAngle;
            poolLight.innerSpotAngle = Mathf.Min(poolInnerAngle, poolOuterAngle);
            poolLight.shadows = LightShadows.None;
#if UNITY_EDITOR
            // Bake mode is editor-only; lights created at runtime are realtime.
            poolLight.lightmapBakeType = LightmapBakeType.Realtime;
#endif
            poolLight.enabled = true;

            var state = new LampState
            {
                light = poolLight,
                renderer = fixture,
                flickers = flickerEveryNthLamp > 0 && index % flickerEveryNthLamp == 1,
                nextFlicker = Time.unscaledTime + NextFloat(minimumFlickerInterval, maximumFlickerInterval)
                    + index * 0.13f
            };
            ConfigureFixtureGlow(state);
            if (state.glowMaterialIndex < 0) ConfigureFallbackGlow(state, lightTransform, fixturePosition);
            SetBrightness(state, 1f);
            lamps.Add(state);LampLights.Add(poolLight);
        }
        if (found.Count == 0)
            Debug.LogWarning("Truth_Table has no STREETLAMP2 fixtures under its hierarchy.", this);
        else if (lamps.Count != found.Count)
            Debug.LogWarning($"Truth_Table configured {lamps.Count} of {found.Count} STREETLAMP2 fixtures.", this);
    }

    private static Renderer FindFixtureRenderer(Transform lamp)
    {
        Renderer fallback = null;
        foreach (Renderer renderer in lamp.GetComponentsInChildren<Renderer>(true))
        {
            if (fallback == null || renderer.bounds.size.sqrMagnitude > fallback.bounds.size.sqrMagnitude)
                fallback = renderer;
            foreach (Material material in renderer.sharedMaterials)
                if (material != null && material.name.IndexOf("Celestial Gold", StringComparison.OrdinalIgnoreCase) >= 0)
                    return renderer;
        }
        return fallback;
    }

    private void ConfigureFixtureGlow(LampState state)
    {
        Material[] materials = state.renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            Material original = materials[i];
            if (original == null || original.name.IndexOf("Celestial Gold", StringComparison.OrdinalIgnoreCase) < 0 ||
                !original.HasProperty("_EmissionColor")) continue;

            state.originalMaterials = materials;
            state.glowMaterialIndex = i;
            state.properties = new MaterialPropertyBlock();
            if (!glowingMaterials.TryGetValue(original, out Material glowing))
            {
                glowing = new Material(original) { name = original.name + " (Truth Table Glow)" };
                glowing.SetColor("_EmissionColor",lampColor*fixtureGlow);
                glowing.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
                glowing.EnableKeyword("_EMISSION");
                glowingMaterials.Add(original, glowing);
            }
            Material[] replacements = (Material[])materials.Clone();
            replacements[i] = glowing;
            state.renderer.sharedMaterials = replacements;
            return;
        }
    }

    private void ConfigureFallbackGlow(LampState state, Transform lightTransform, Vector3 fixturePosition)
    {
        Transform existing = lightTransform.Find("TruthTableFixtureGlow");
        if (existing == null)
        {
            GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "TruthTableFixtureGlow";
            orb.transform.SetParent(lightTransform, false);
            orb.transform.localScale = Vector3.one * 0.38f;
            Collider collider = orb.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            MeshRenderer mesh = orb.GetComponent<MeshRenderer>();
            mesh.shadowCastingMode = ShadowCastingMode.Off;
            mesh.receiveShadows = false;
            if (fallbackGlowMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader != null)
                {
                    fallbackGlowMaterial = new Material(shader) { name = "Truth Table Fixture Glow (Runtime)" };
                    string colorProperty = fallbackGlowMaterial.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
                    fallbackGlowMaterial.SetColor(colorProperty, lampColor * fixtureGlow);
                }
            }
            if (fallbackGlowMaterial != null) mesh.sharedMaterial = fallbackGlowMaterial;
            existing = orb.transform;
        }
        existing.position = fixturePosition;
        state.fallbackGlow = existing.gameObject;
        state.fallbackGlow.SetActive(true);
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        bool changed=!Mathf.Approximately(lastIntensity,lampIntensity)||!Mathf.Approximately(lastGlow,fixtureGlow);
        lastIntensity=lampIntensity;lastGlow=fixtureGlow;
        foreach (LampState state in lamps)
        {
            if (!state.flickers) {if(changed)SetBrightness(state,1);continue;}
            if(changed&&state.flickerStarted<0)SetBrightness(state,1);
            if (state.flickerStarted < 0f && now >= state.nextFlicker)
            {
                state.flickerStarted = now;
                state.flickerLength = flickerDuration * NextFloat(0.8f, 1.2f);
            }
            if (state.flickerStarted < 0f) continue;
            float progress = (now - state.flickerStarted) / state.flickerLength;
            if (progress >= 1f)
            {
                SetBrightness(state, 1f);
                state.flickerStarted = -1f;
                state.nextFlicker = now + NextFloat(minimumFlickerInterval, maximumFlickerInterval);
                continue;
            }
            float dip = Mathf.Sin(Mathf.PI * Mathf.Clamp01(progress));
            SetBrightness(state, 1f - (1f - lowestFlickerBrightness) * dip);
        }
    }

    private void SetBrightness(LampState state, float factor)
    {
        state.light.intensity = lampIntensity * factor;
        if (state.fallbackGlow != null) state.fallbackGlow.transform.localScale = Vector3.one * (0.38f * factor);
        if (state.glowMaterialIndex < 0) return;
        state.renderer.GetPropertyBlock(state.properties, state.glowMaterialIndex);
        state.properties.SetColor("_EmissionColor", lampColor * (fixtureGlow * factor));
        state.renderer.SetPropertyBlock(state.properties, state.glowMaterialIndex);
    }

    private void OnDisable()
    {
        foreach (LampState state in lamps)
        {
            if (state.light != null) state.light.enabled = false;
            if (state.fallbackGlow != null) state.fallbackGlow.SetActive(false);
            if (state.glowMaterialIndex >= 0)
            {
                state.renderer.SetPropertyBlock(null, state.glowMaterialIndex);
                state.renderer.sharedMaterials = state.originalMaterials;
            }
        }
        lamps.Clear();LampLights.Clear();
        if (!lightingApplied) return;
        if (sunlight != null)
        {
            sunlight.intensity = originalSunIntensity;
            sunlight.color = originalSunColor;
        }
        // A newly loaded scene owns its own environment; never overwrite it on unload.
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="TruthTable")
        {
        RenderSettings.ambientMode = originalAmbientMode;
        RenderSettings.ambientSkyColor = originalAmbientSky;
        RenderSettings.ambientEquatorColor = originalAmbientEquator;
        RenderSettings.ambientGroundColor = originalAmbientGround;
        RenderSettings.skybox = originalSkybox;
        }
        if (duskSkybox != null) Destroy(duskSkybox);
        duskSkybox = null;
        lightingApplied = false;
    }

    private void OnDestroy()
    {
        foreach (Material material in glowingMaterials.Values)
            if (material != null) Destroy(material);
        glowingMaterials.Clear();
        if (fallbackGlowMaterial != null) Destroy(fallbackGlowMaterial);
    }
}
