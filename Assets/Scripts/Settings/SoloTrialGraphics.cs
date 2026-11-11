using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Owns the graphics overrides for the active solo trial.</summary>
internal sealed class SoloTrialGraphics : IDisposable
{
    // The volume is scoped to display cameras. Minimap and capture cameras keep their own masks.
    const int VolumeLayer = 2; // Ignore Raycast
    const float RefreshInterval = .4f;
    readonly UniversalRenderPipelineAsset source;
    readonly UniversalRenderPipelineAsset pipeline;
    readonly RenderPipelineAsset previousPipeline;
    readonly VolumeProfile profile;
    readonly Bloom bloom;
    readonly int previousFrameRate = Application.targetFrameRate;
    readonly int previousVSync = QualitySettings.vSyncCount;
    readonly int previousMipLimit = QualitySettings.globalTextureMipmapLimit;
    readonly float previousLodBias = QualitySettings.lodBias;
    readonly List<Light> suns = new List<Light>();
    readonly List<LightShadows> sunShadows = new List<LightShadows>();
    readonly List<Light> fixtures = new List<Light>();
    readonly HashSet<Light> litFixtures = new HashSet<Light>();
    readonly List<(Light light, float score)> rankedFixtures = new List<(Light, float)>();
    readonly List<(Renderer renderer, Vector3 position, bool alternate)> grass = new List<(Renderer, Vector3, bool)>();
    readonly List<(ParticleSystem system, float rate)> particles = new List<(ParticleSystem, float)>();
    readonly Dictionary<Camera, CameraSettings> cameras = new Dictionary<Camera, CameraSettings>();
    Scene scene;
    Volume volume;
    SoloSettingsData settings;
    bool authoredFog;
    Color fogColor;
    FogMode fogMode;
    float fogDensity;
    int shortEdge;
    float nextRefresh;

    struct CameraSettings
    {
        public UniversalAdditionalCameraData data;
        public bool hdr, post, shadows;
        public LayerMask mask;
    }

    public SoloTrialGraphics(SoloGraphicsProfile assets)
    {
        source = assets.pipeline;
        previousPipeline = QualitySettings.renderPipeline;
        pipeline = Object.Instantiate(source);
        pipeline.name = "Open Archive Mobile (Solo Runtime)";
        pipeline.hideFlags = HideFlags.DontSave;
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.hideFlags = HideFlags.DontSave;
        foreach (var component in assets.atmosphere.components)
            profile.components.Add(Object.Instantiate(component));
        profile.TryGet(out bloom);
        RenderPipelineManager.beginContextRendering += ConfigureCameras;
    }

    public void Enter(Scene target, SoloSettingsData value)
    {
        if (scene != target)
        {
            ClearScene();
            scene = target;
            authoredFog = RenderSettings.fog;
            fogColor = RenderSettings.fogColor;
            fogMode = RenderSettings.fogMode;
            fogDensity = RenderSettings.fogDensity;
            var host = new GameObject("Solo Graphics Volume");
            host.layer = VolumeLayer;
            SceneManager.MoveGameObjectToScene(host, target);
            volume = host.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 8;
            volume.sharedProfile = profile;
            volume.enabled = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var light in root.GetComponentsInChildren<Light>(true))
                {
                    if (light.type == LightType.Directional)
                    {
                        suns.Add(light);
                        sunShadows.Add(light.shadows);
                    }
                    else if (!light.enabled && (light.type == LightType.Point || light.type == LightType.Spot) && IsDecorativeFixture(light.name))
                        fixtures.Add(light);
                }
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (renderer.forceRenderingOff || !renderer.name.Contains("grass", StringComparison.OrdinalIgnoreCase)) continue;
                    var material = renderer.sharedMaterial;
                    if (material == null || !material.name.StartsWith("PT_Grass_Mat", StringComparison.Ordinal)) continue;
                    var position = renderer.bounds.center;
                    int hash = unchecked(Mathf.RoundToInt(position.x * 13) * 73856093 ^ Mathf.RoundToInt(position.z * 13) * 19349663);
                    grass.Add((renderer, position, (hash & 1) == 0));
                }
                foreach (var particle in root.GetComponentsInChildren<ParticleSystem>(true))
                    if (particle.name.StartsWith("CLOUDS_PS", StringComparison.Ordinal) || particle.name == "Book Motes")
                        particles.Add((particle, particle.emission.rateOverTimeMultiplier));
            }
        }
        Apply(value);
    }

    public void Apply(SoloSettingsData value)
    {
        var previous = settings;
        settings = value;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = value.frameRate;
        // The Open Archive Mobile preset uses one mip reduction and a 0.3 LOD bias.
        QualitySettings.globalTextureMipmapLimit = 3 - value.textureQuality;
        QualitySettings.lodBias = value.graphicsQuality == 2 ? .3f : value.graphicsQuality == 1 ? .23f : .16f;
        pipeline.msaaSampleCount = value.graphicsQuality == 2 ? source.msaaSampleCount : 1;
        pipeline.supportsHDR = value.specialEffects == 2 || value.bloom;
        pipeline.shadowDistance = value.shadowQuality == 3 ? source.shadowDistance : value.shadowQuality * 15f;
        pipeline.mainLightShadowmapResolution = value.shadowQuality == 3 ? source.mainLightShadowmapResolution : 512 * Mathf.Max(1, value.shadowQuality);
        pipeline.shadowCascadeCount = value.shadowQuality == 3 ? source.shadowCascadeCount : 1;
        QualitySettings.renderPipeline = pipeline;
        ApplyResolution();

        foreach (var sun in suns)
            if (sun != null) sun.shadows = value.shadowQuality == 0 ? LightShadows.None :
                value.shadowQuality == 3 ? LightShadows.Soft : LightShadows.Hard;
        // Fog is an outdoor effect and preserves the scene's own sky.
        bool outdoor = scene.name == "TruthTable" || scene.name == "RulesOfInference" ||
            scene.name == "Setssss" || scene.name == "Functions and Relations";
        RenderSettings.fog = value.fog && (outdoor || authoredFog);
        if (RenderSettings.fog && !authoredFog && scene.name != "Functions and Relations")
        {
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = .004f;
            RenderSettings.fogColor = Color.Lerp(RenderSettings.ambientSkyColor, new Color(.49f, .56f, .65f), .4f);
        }

        foreach (var component in profile.components)
            component.active = component == bloom ? value.bloom : value.specialEffects == 2;
        if (bloom != null)
        {
            bloom.maxIterations.Override(value.specialEffects == 0 ? 2 : value.specialEffects == 1 ? 3 : 4);
            bloom.intensity.Override(value.specialEffects == 0 ? .12f : .20f);
        }
        if (volume != null) volume.enabled = pipeline.supportsHDR;
        if (previous == null || previous.specialEffects != value.specialEffects) UpdateParticles();
        nextRefresh = 0;
        Refresh();
    }

    internal static float RenderScale(int height, int width, int displayHeight)
    {
        // Render against the display's short edge. URP scales both axes together,
        // keeping the actual display, canvas, safe area and touch coordinates native.
        return Mathf.Clamp((float)height / Mathf.Max(1, Mathf.Min(width, displayHeight)), .1f, 1f);
    }

    void ApplyResolution()
    {
        shortEdge = Mathf.Min(Screen.width, Screen.height);
        pipeline.renderScale = RenderScale(settings.resolution, Screen.width, Screen.height);
    }

    public void Refresh()
    {
        if (settings == null || scene != SceneManager.GetActiveScene()) return;
        if (shortEdge != Mathf.Min(Screen.width, Screen.height)) ApplyResolution();
        if (Time.unscaledTime < nextRefresh) return;
        var camera = Camera.main;
        if (camera == null) return;
        nextRefresh = Time.unscaledTime + RefreshInterval;
        Vector3 eye = camera.transform.position;
        foreach (var plant in grass)
        {
            if (plant.renderer == null) continue;
            float distance = (plant.position - eye).sqrMagnitude;
            plant.renderer.forceRenderingOff = settings.specialEffects == 0
                ? distance > 50f * 50f || (plant.alternate && distance > 18f * 18f)
                : settings.specialEffects == 1 && distance > 90f * 90f;
        }
        UpdateFixtures(eye);
    }

    void UpdateParticles()
    {
        foreach (var particle in particles)
        {
            var system = particle.system;
            if (system == null) continue;
            var emission = system.emission;
            bool enabled = settings.specialEffects > 0;
            emission.enabled = enabled;
            emission.rateOverTimeMultiplier = settings.specialEffects == 1 ? particle.rate * .45f : particle.rate;
            if (!enabled && system.isPlaying) system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            else if (enabled && !system.isPlaying && system.gameObject.activeInHierarchy) system.Play(false);
        }
    }

    void UpdateFixtures(Vector3 eye)
    {
        // Match multiplayer's nearest-fixture selection cadence and six-light
        // ceiling. Keep the authored colors and ranges and skip local shadows.
        float radius = settings.specialEffects == 2 ? 65 : settings.specialEffects == 1 ? 45 : 30;
        int count = 2 + settings.specialEffects * 2;
        rankedFixtures.Clear();
        foreach (var light in fixtures)
        {
            if (light == null || !light.gameObject.activeInHierarchy) continue;
            float distance = (light.transform.position - eye).sqrMagnitude;
            if (distance > radius * radius) continue;
            rankedFixtures.Add((light, distance - (litFixtures.Contains(light) ? 16 : 0)));
        }
        rankedFixtures.Sort(CompareFixtures);
        count = Mathf.Min(count, rankedFixtures.Count);
        foreach (var light in litFixtures)
        {
            if (light == null) continue;
            bool stillSelected = false;
            for (int i = 0; i < count; i++)
                if (rankedFixtures[i].light == light) { stillSelected = true; break; }
            if (!stillSelected) light.enabled = false;
        }
        litFixtures.Clear();
        for (int i = 0; i < count; i++)
        {
            var light = rankedFixtures[i].light;
            if (light.shadows != LightShadows.None) light.shadows = LightShadows.None;
            if (!light.enabled) light.enabled = true;
            litFixtures.Add(light);
        }
    }

    static int CompareFixtures((Light light, float score) a, (Light light, float score) b) => a.score.CompareTo(b.score);

    static bool IsDecorativeFixture(string name) =>
        name.StartsWith("TorchLight", StringComparison.Ordinal) || name.StartsWith("CandleLight", StringComparison.Ordinal) ||
        name == "TruthTableFixtureLight" || name == "Warm Lamp Light" || name == "Warm Ground Pool" ||
        name == "Torch warm light" || name.EndsWith(" Warm Pool", StringComparison.Ordinal) ||
        name.Contains("Reading Light", StringComparison.Ordinal) || name.StartsWith("Blue Crystal Pool", StringComparison.Ordinal);

    void ConfigureCameras(ScriptableRenderContext context, List<Camera> activeCameras)
    {
        if (settings == null || scene != SceneManager.GetActiveScene()) return;
        foreach (var camera in activeCameras)
        {
            if (camera.cameraType != CameraType.Game || camera.targetTexture != null ||
                (camera.gameObject.scene != scene && camera != Camera.main)) continue;
            if (!camera.TryGetComponent<UniversalAdditionalCameraData>(out var data))
                data = camera.GetUniversalAdditionalCameraData();
            if (data.renderType != CameraRenderType.Base) continue;
            if (!cameras.ContainsKey(camera))
                cameras.Add(camera, new CameraSettings { data = data, hdr = camera.allowHDR, post = data.renderPostProcessing,
                    shadows = data.renderShadows, mask = data.volumeLayerMask });
            camera.allowHDR = pipeline.supportsHDR;
            data.renderPostProcessing = pipeline.supportsHDR;
            data.renderShadows = settings.shadowQuality > 0;
            data.volumeLayerMask = 1 << VolumeLayer;
        }
    }

    void ClearScene()
    {
        foreach (var entry in cameras)
        {
            if (entry.Key == null || entry.Value.data == null) continue;
            entry.Key.allowHDR = entry.Value.hdr;
            entry.Value.data.renderPostProcessing = entry.Value.post;
            entry.Value.data.renderShadows = entry.Value.shadows;
            entry.Value.data.volumeLayerMask = entry.Value.mask;
        }
        cameras.Clear();
        for (int i = 0; i < suns.Count; i++) if (suns[i] != null) suns[i].shadows = sunShadows[i];
        suns.Clear();
        sunShadows.Clear();
        foreach (var light in litFixtures) if (light != null) light.enabled = false;
        litFixtures.Clear();
        fixtures.Clear();
        rankedFixtures.Clear();
        foreach (var plant in grass) if (plant.renderer != null) plant.renderer.forceRenderingOff = false;
        grass.Clear();
        foreach (var particle in particles)
        {
            if (particle.system == null) continue;
            var emission = particle.system.emission;
            emission.enabled = false;
            emission.rateOverTimeMultiplier = particle.rate;
            if (particle.system.isPlaying) particle.system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        particles.Clear();
        if (volume != null)
        {
            volume.enabled = false;
            Object.Destroy(volume.gameObject);
        }
        volume = null;
        if (scene.IsValid() && scene == SceneManager.GetActiveScene())
        {
            RenderSettings.fog = authoredFog;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogDensity = fogDensity;
        }
        settings = null;
    }

    public void Dispose()
    {
        RenderPipelineManager.beginContextRendering -= ConfigureCameras;
        ClearScene();
        if (QualitySettings.renderPipeline == pipeline) QualitySettings.renderPipeline = previousPipeline;
        QualitySettings.vSyncCount = previousVSync;
        QualitySettings.globalTextureMipmapLimit = previousMipLimit;
        QualitySettings.lodBias = previousLodBias;
        Application.targetFrameRate = previousFrameRate;
        Object.Destroy(pipeline);
        foreach (var component in profile.components) Object.Destroy(component);
        Object.Destroy(profile);
    }
}
