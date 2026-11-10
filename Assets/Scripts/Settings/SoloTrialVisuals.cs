using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Preserves each trial's art direction with simple lit surfaces and optional directional shadows.</summary>
public sealed class SoloTrialVisuals : MonoBehaviour
{
    readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
    readonly Dictionary<Material, Material> litCopies = new Dictionary<Material, Material>();
    readonly Dictionary<Light, LightShadows> lightShadows = new Dictionary<Light, LightShadows>();
    readonly Dictionary<Light, float> shadowStrengths = new Dictionary<Light, float>();
    AmbientMode ambientMode;
    Color sky, equator, ground, fogColor;
    bool originalFog;
    bool authoredAmbient;
    FogMode fogMode;
    float fogDensity, fogStart, fogEnd;
    Volume volume;
    MotionBlur blur;
    UniversalAdditionalCameraData cameraData;
    bool previousPost, previousDepth, previousShadows;
    LayerMask previousVolumeMask;
    SoloSettingsData settings;
    float nextCameraScan;
    void Awake()
    {
        ambientMode = RenderSettings.ambientMode; sky = RenderSettings.ambientSkyColor;
        equator = RenderSettings.ambientEquatorColor; ground = RenderSettings.ambientGroundColor;
        originalFog = RenderSettings.fog; fogMode = RenderSettings.fogMode; fogColor = RenderSettings.fogColor;
        fogDensity = RenderSettings.fogDensity; fogStart = RenderSettings.fogStartDistance; fogEnd = RenderSettings.fogEndDistance;
        // Isolate our optional blur from authored post effects on the default
        // layer, so enabling blur does not also enable an unused color grade.
        gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<PropositionalChamberLighting>(true) != null || root.GetComponentInChildren<TruthTableDuskLighting>(true) != null) authoredAmbient = true;
            foreach (var light in root.GetComponentsInChildren<Light>(true))
                if (light.type == LightType.Directional) { lightShadows[light] = light.shadows; shadowStrengths[light] = light.shadowStrength; }
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true)) LightOpaqueSurface(renderer);
        }
    }
    void LightOpaqueSurface(Renderer renderer)
    {
        if (renderer.GetComponentInParent<Canvas>() != null || renderer.GetComponent<TMPro.TMP_Text>() != null) return;
        var materials = renderer.sharedMaterials; bool changed = false;
        for (int i = 0; i < materials.Length; i++)
        {
            var source = materials[i]; if (source == null) continue;
            string shader = source.shader.name;
            if (shader != "Universal Render Pipeline/Unlit" && shader != "Unlit/Color" && shader != "Unlit/Texture") continue;
            // Keep deliberately emissive effects and readable puzzle surfaces unlit.
            string name = source.name.ToLowerInvariant();
            bool preserve = false;
            foreach (string cue in new[] { "text", "rune", "glow", "holo", "crystal", "answer", "question", "sign", "board", "indicator", "portal", "screen", "flame", "fire", "torch", "lantern", "lamp", "candle" })
                if (name.Contains(cue)) { preserve = true; break; }
            if (preserve) continue;
            if (source.GetTag("RenderType", false, "Opaque") != "Opaque" || source.HasProperty("_Surface") && source.GetFloat("_Surface") != 0) continue;
            if (!litCopies.TryGetValue(source, out var lit))
            {
                var template = Resources.Load<Material>("Settings/SoloOpaqueLit");
                if (template == null) continue;
                lit = new Material(template) { name = source.name + " (Solo Lit Runtime)", hideFlags = HideFlags.DontSave };
                lit.SetColor("_BaseColor", source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white);
                string map = source.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                if (source.HasProperty(map)) { lit.SetTexture("_BaseMap", source.GetTexture(map)); lit.SetTextureScale("_BaseMap", source.GetTextureScale(map)); lit.SetTextureOffset("_BaseMap", source.GetTextureOffset(map)); }
                litCopies[source] = lit;
            }
            materials[i] = lit; changed = true;
        }
        if (changed) { originalMaterials[renderer] = renderer.sharedMaterials; renderer.sharedMaterials = materials; }
    }
    public void Apply(SoloSettingsData value)
    {
        settings = value;
        // Existing sunset and chamber lighting owns its own colors and local
        // render scopes. Only legacy flat ambient needs a directional gradient.
        if (ambientMode == AmbientMode.Flat && !authoredAmbient)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky * .98f; RenderSettings.ambientEquatorColor = sky * .85f; RenderSettings.ambientGroundColor = sky * .65f;
        }
        RenderSettings.fog = value.fog;
        if (!originalFog && value.fog)
        {
            RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = .0015f;
            RenderSettings.fogColor = Color.Lerp(sky, new Color(.45f, .5f, .56f), .3f);
        }
        else { RenderSettings.fogMode = fogMode; RenderSettings.fogDensity = fogDensity; RenderSettings.fogColor = fogColor; }
        foreach (var entry in lightShadows)
            if (entry.Key != null)
            {
                entry.Key.shadows = value.shadowQuality == 0 ? LightShadows.None : value.shadowQuality < 3 ? LightShadows.Hard : LightShadows.Soft;
                // Formerly unshadowed rooms still need their broad fill light.
                entry.Key.shadowStrength = entry.Value == LightShadows.None ? Mathf.Min(shadowStrengths[entry.Key], .5f) : shadowStrengths[entry.Key];
            }
        if (value.motionBlur && blur == null)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>(); profile.name = "Optional Solo Motion Blur (Runtime)";
            blur = profile.Add<MotionBlur>(true); blur.mode.Override(MotionBlurMode.CameraOnly); blur.quality.Override(MotionBlurQuality.Low);
            volume = gameObject.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 50; volume.sharedProfile = profile;
        }
        if (volume != null) volume.enabled = value.motionBlur;
        if (blur != null) { blur.intensity.Override(value.motionBlur ? .16f : 0); blur.clamp.Override(.035f); }
        ApplyCamera();
    }
    void LateUpdate()
    {
        if (Time.unscaledTime < nextCameraScan || settings == null) return;
        nextCameraScan = Time.unscaledTime + .5f; ApplyCamera();
    }
    void ApplyCamera()
    {
        var view = Camera.main;
        if (view == null || view.gameObject.scene != gameObject.scene || (view.cullingMask & ~(1 << LayerMask.NameToLayer("UI"))) == 0) return;
        var data = view.GetUniversalAdditionalCameraData();
        if (cameraData != data)
        {
            RestoreCamera(); cameraData = data; previousPost = data.renderPostProcessing;
            previousDepth = data.requiresDepthTexture; previousShadows = data.renderShadows;
            previousVolumeMask = data.volumeLayerMask;
        }
        data.renderPostProcessing = settings.motionBlur || previousPost || LogicLegends.Graph.GraphChamberAtmosphere.WantsBloom(gameObject.scene);
        data.volumeLayerMask = previousPost ? previousVolumeMask.value | (1 << gameObject.layer) : 1 << gameObject.layer;
        data.requiresDepthTexture = settings.motionBlur || previousDepth;
        data.renderShadows = settings.shadowQuality > 0;
    }
    void RestoreCamera()
    {
        if (cameraData == null) return;
        cameraData.renderPostProcessing = previousPost; cameraData.requiresDepthTexture = previousDepth; cameraData.renderShadows = previousShadows;
        cameraData.volumeLayerMask = previousVolumeMask;
    }
    void OnDestroy()
    {
        RestoreCamera();
        foreach (var pair in originalMaterials) if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
        foreach (var material in litCopies.Values) if (material != null) CoreUtils.Destroy(material);
        foreach (var entry in lightShadows) if (entry.Key != null) { entry.Key.shadows = entry.Value; entry.Key.shadowStrength = shadowStrengths[entry.Key]; }
        if (SceneManager.GetActiveScene() == gameObject.scene)
        {
            RenderSettings.ambientMode = ambientMode; RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = equator; RenderSettings.ambientGroundColor = ground;
            RenderSettings.fog = originalFog; RenderSettings.fogMode = fogMode; RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity; RenderSettings.fogStartDistance = fogStart; RenderSettings.fogEndDistance = fogEnd;
        }
        if (volume != null && volume.sharedProfile != null) CoreUtils.Destroy(volume.sharedProfile);
    }
}
