using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Preserves each trial's art direction with simple lit surfaces and optional directional shadows.</summary>
[DefaultExecutionOrder(31940)]
public sealed class SoloTrialVisuals : MonoBehaviour
{
    readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
    readonly Dictionary<Material, Material> litCopies = new Dictionary<Material, Material>();
    readonly Dictionary<Material, Material> meshLodMaterials = new Dictionary<Material, Material>();
    readonly Dictionary<Material, Material> foliageCopies = new Dictionary<Material, Material>();
    readonly Dictionary<MeshFilter, Mesh> mobileMeshes = new Dictionary<MeshFilter, Mesh>();
    readonly Dictionary<MeshCollider, Mesh> mobileColliders = new Dictionary<MeshCollider, Mesh>();
    SoloMobileMeshes mobileMeshCatalog;
    readonly Dictionary<Light, LightShadows> lightShadows = new Dictionary<Light, LightShadows>();
    readonly Dictionary<Light, float> shadowStrengths = new Dictionary<Light, float>();
    AmbientMode ambientMode;
    SphericalHarmonicsL2 ambientProbe;
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
    public bool SurfacesReady { get; private set; }
    static readonly string[] UnlitCues = { "text", "rune", "glow", "holo", "crystal", "answer", "question", "sign", "board", "indicator", "portal", "screen", "flame", "fire", "torch", "lantern", "lamp", "candle" };
    void Awake()
    {
        ambientMode = RenderSettings.ambientMode; sky = RenderSettings.ambientSkyColor;
        ambientProbe = RenderSettings.ambientProbe;
        equator = RenderSettings.ambientEquatorColor; ground = RenderSettings.ambientGroundColor;
        originalFog = RenderSettings.fog; fogMode = RenderSettings.fogMode; fogColor = RenderSettings.fogColor;
        fogDensity = RenderSettings.fogDensity; fogStart = RenderSettings.fogStartDistance; fogEnd = RenderSettings.fogEndDistance;
        if (Application.platform == RuntimePlatform.Android) mobileMeshCatalog = Resources.Load<SoloMobileMeshes>("Settings/SoloMobileMeshes");
        // Isolate our optional blur from authored post effects on the default
        // layer, so enabling blur does not also enable an unused color grade.
        gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<PropositionalChamberLighting>(true) != null || root.GetComponentInChildren<TruthTableDuskLighting>(true) != null) authoredAmbient = true;
            foreach (var light in root.GetComponentsInChildren<Light>(true))
                if (light.type == LightType.Directional) { lightShadows[light] = light.shadows; shadowStrengths[light] = light.shadowStrength; }
        }
    }
    IEnumerator Start()
    {
        // Material preparation used to process thousands of plants in one Awake.
        // Keep the loading artwork responsive while preparing the same surfaces.
        double until = Time.realtimeSinceStartupAsDouble + .002;
        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer != null) LightOpaqueSurface(renderer);
                if (Time.realtimeSinceStartupAsDouble < until) continue;
                yield return null;
                until = Time.realtimeSinceStartupAsDouble + .002;
            }
        SurfacesReady = true;
    }
    void LightOpaqueSurface(Renderer renderer)
    {
        if (renderer.GetComponentInParent<Canvas>() != null || renderer.GetComponent<TMPro.TMP_Text>() != null ||
            renderer.GetComponentInParent<GuidanceArrowGlow>() != null) return;
        var filter = renderer.GetComponent<MeshFilter>();
        if (mobileMeshCatalog != null && filter != null && filter.sharedMesh != null)
        {
            var sourceMesh = filter.sharedMesh;
            var replacement = mobileMeshCatalog.Resolve(sourceMesh, SoloGameSettings.Current.graphicsQuality);
            if (replacement != sourceMesh)
            {
                mobileMeshes[filter] = sourceMesh; filter.sharedMesh = replacement;
                // The decorative lamps also used the full 296k-triangle render
                // mesh for collision. Retain a mesh-fitted solid shape, using the
                // existing source LOD; never replace stairs/floors/puzzle colliders.
                var collider = filter.GetComponent<MeshCollider>();
                if (collider != null && collider.sharedMesh == sourceMesh && !collider.isTrigger)
                {
                    mobileColliders[collider] = sourceMesh;
                    collider.sharedMesh = mobileMeshCatalog.Resolve(sourceMesh, 0);
                }
            }
        }
        bool useMeshLods = filter != null && filter.sharedMesh != null && filter.sharedMesh.lodCount > 1 &&
            renderer.GetComponentInParent<LODGroup>() == null;
        var materials = renderer.sharedMaterials; bool changed = false;
        bool foliage = false;
        for (int i = 0; i < materials.Length; i++)
        {
            var source = materials[i]; if (source == null) continue;
            if (SoloFoliageMaterials.IsLegacy(source))
            {
                if (!foliageCopies.TryGetValue(source, out var cutout))
                {
                    cutout = SoloFoliageMaterials.Create(source);
                    if (cutout != null) foliageCopies[source] = cutout;
                }
                if (cutout != null) { materials[i] = cutout; changed = true; foliage = true; }
                continue;
            }
            // GPU-instanced draws always select LOD0. Share copies that allow automatic Mesh LOD selection.
            if (useMeshLods && source.enableInstancing)
            {
                if (!meshLodMaterials.TryGetValue(source, out var copy))
                {
                    copy = new Material(source) { name = source.name + " (Solo Mesh LOD)",
                        hideFlags = HideFlags.DontSave, enableInstancing = false };
                    meshLodMaterials[source] = copy;
                }
                source = copy; materials[i] = copy; changed = true;
            }
            string shader = source.shader.name;
            if (shader != "Universal Render Pipeline/Unlit" && shader != "Unlit/Color" && shader != "Unlit/Texture") continue;
            // Keep deliberately emissive effects and readable puzzle surfaces unlit.
            string name = source.name.ToLowerInvariant();
            bool preserve = false;
            foreach (string cue in UnlitCues)
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
        if (foliage) GetComponent<SoloSceneGraphics>()?.AddFoliage(renderer);
    }
    public void Apply(SoloSettingsData value)
    {
        settings = value;
        foreach (var pair in mobileMeshes) if (pair.Key != null)
            pair.Key.sharedMesh = mobileMeshCatalog.Resolve(pair.Value, value.graphicsQuality);
        // Existing sunset and chamber lighting owns its own colors and render scopes.
        bool flatAmbient = ambientMode == AmbientMode.Flat;
        bool mobile = Application.platform == RuntimePlatform.Android;
        if (!authoredAmbient && (flatAmbient || (mobile && ambientMode == AmbientMode.Trilight)))
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            Color skyFill = flatAmbient ? sky * .98f : sky;
            Color equatorFill = flatAmbient ? sky * .85f : equator;
            Color groundFill = flatAmbient ? sky * .65f : ground;
            RenderSettings.ambientSkyColor = mobile ? LiftDimAmbient(skyFill) : skyFill;
            RenderSettings.ambientEquatorColor = mobile ? LiftDimAmbient(equatorFill) : equatorFill;
            RenderSettings.ambientGroundColor = mobile ? LiftDimAmbient(groundFill) : groundFill;
            if (mobile)
            {
                Color originalFill = flatAmbient ? sky : (sky + equator + ground) / 3f;
                Color updatedFill = (RenderSettings.ambientSkyColor + RenderSettings.ambientEquatorColor +
                    RenderSettings.ambientGroundColor) / 3f;
                Color lift = updatedFill.linear - originalFill.linear;
                var probe = ambientProbe;
                probe.AddAmbientLight(new Color(Mathf.Max(0f, lift.r), Mathf.Max(0f, lift.g), Mathf.Max(0f, lift.b)));
                RenderSettings.ambientProbe = probe;
            }
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
                int quality = SoloGraphicsBudget.Shadows(value);
                entry.Key.shadows = quality == 0 ? LightShadows.None : quality < 3 ? LightShadows.Hard : LightShadows.Soft;
                // Formerly unshadowed rooms still need their broad fill light.
                entry.Key.shadowStrength = entry.Value == LightShadows.None ? Mathf.Min(shadowStrengths[entry.Key], .5f) : shadowStrengths[entry.Key];
            }
        bool enableBlur = value.motionBlur && SoloGraphicsBudget.PostProcessing(value);
        if (enableBlur && blur == null)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>(); profile.name = "Optional Solo Motion Blur (Runtime)";
            blur = profile.Add<MotionBlur>(true); blur.mode.Override(MotionBlurMode.CameraOnly); blur.quality.Override(MotionBlurQuality.Low);
            volume = gameObject.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 50; volume.sharedProfile = profile;
        }
        if (volume != null) volume.enabled = enableBlur;
        if (blur != null) { blur.intensity.Override(enableBlur ? .16f : 0); blur.clamp.Override(.035f); }
        ApplyCamera();
    }
    static Color LiftDimAmbient(Color color)
    {
        float lift = Mathf.Max(0f, .3f - color.grayscale) * .5f;
        return new Color(color.r + lift, color.g + lift, color.b + lift, color.a);
    }
    void LateUpdate()
    {
        if (settings != null) ApplyCamera();
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
        bool post = SoloGraphicsBudget.PostProcessing(settings);
        data.renderPostProcessing = post && (settings.motionBlur || previousPost || LogicLegends.Graph.GraphChamberAtmosphere.WantsBloom(gameObject.scene));
        data.volumeLayerMask = previousPost ? previousVolumeMask.value | (1 << gameObject.layer) : 1 << gameObject.layer;
        data.requiresDepthTexture = post && (settings.motionBlur || previousDepth);
        data.renderShadows = SoloGraphicsBudget.Shadows(settings) > 0;
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
        foreach (var pair in mobileMeshes) if (pair.Key != null) pair.Key.sharedMesh = pair.Value;
        if (gameObject.scene.isLoaded)
            foreach (var pair in mobileColliders) if (pair.Key != null) pair.Key.sharedMesh = pair.Value;
        foreach (var pair in originalMaterials) if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
        foreach (var material in litCopies.Values) if (material != null) CoreUtils.Destroy(material);
        foreach (var material in meshLodMaterials.Values) if (material != null) CoreUtils.Destroy(material);
        foreach (var material in foliageCopies.Values) if (material != null) CoreUtils.Destroy(material);
        foreach (var entry in lightShadows) if (entry.Key != null) { entry.Key.shadows = entry.Value; entry.Key.shadowStrength = shadowStrengths[entry.Key]; }
        if (SceneManager.GetActiveScene() == gameObject.scene)
        {
            RenderSettings.ambientMode = ambientMode; RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = equator; RenderSettings.ambientGroundColor = ground;
            RenderSettings.ambientProbe = ambientProbe;
            RenderSettings.fog = originalFog; RenderSettings.fogMode = fogMode; RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity; RenderSettings.fogStartDistance = fogStart; RenderSettings.fogEndDistance = fogEnd;
        }
        if (volume != null && volume.sharedProfile != null) CoreUtils.Destroy(volume.sharedProfile);
    }
}
