using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Scene-scoped URP presentation. The existing world progression owns the time of day.</summary>
    [DefaultExecutionOrder(12000), DisallowMultipleComponent]
    public sealed class ArchiveSceneLighting : MonoBehaviour
    {
        public const string QualityProfile = "Open Archive Mobile";
        public ArchiveTopicSession session;
        public UniversalRenderPipelineAsset pipeline;
        public Volume atmosphere;
        public Light[] fireLights = Array.Empty<Light>();
        public AudioSource[] fireVoices = Array.Empty<AudioSource>();
        [Header("Bloom - quarter resolution")]
        [Range(0, 1)] public float daylightBloom = .20f, nightBloom = .48f;
        [Header("Stationary warm torch pools")]
        [Range(0, 8)] public int maximumFireLights = 6;
        [Range(8, 30)] public float fireRange = 20;
        [Range(0, 250)] public float dayFireIntensity = 10, nightFireIntensity = 120;
        [Range(0, .15f)] public float fireVariation = .07f;
        [Range(25, 100)] public float fireVisibilityDistance = 65;
        [Range(0, 2)] public int maximumFireVoices = 2;
        [Range(0, .3f)] public float fireVolume = .13f;
        public int ActiveFireLights { get; private set; }
        public int ActiveFireVoices { get; private set; }
        public float NightBlend => session != null && session.worldProgression != null ? session.worldProgression.NightFogBlend : 0;
        public float BloomIntensity => bloom != null ? bloom.intensity.value : 0;
        public bool UsesArchivePipeline => GraphicsSettings.currentRenderPipeline == pipeline;
        sealed class CameraState
        {
            public Camera camera; public UniversalAdditionalCameraData data;
            public bool hdr, post, shadows; public LayerMask mask;
        }
        readonly List<CameraState> cameras = new List<CameraState>();
        readonly List<int> nearby = new List<int>();
        readonly Dictionary<Light, float> distances = new Dictionary<Light, float>();
        RenderPipelineAsset oldOverride;
        int oldQuality;
        string oldQualityName;
        VolumeProfile originalProfile, localProfile;
        Bloom bloom;
        bool initialized;
        float nextScan;

        void OnEnable()
        {
            if (!Application.isPlaying || initialized) return;
            SoloSettingsRuntime.ReleaseGraphics();
            oldQuality = QualitySettings.GetQualityLevel(); oldQualityName = QualitySettings.names[oldQuality];
            oldOverride = QualitySettings.renderPipeline;
            int quality = Array.IndexOf(QualitySettings.names, QualityProfile);
            if (quality >= 0) QualitySettings.SetQualityLevel(quality, true);
            if (pipeline != null) QualitySettings.renderPipeline = pipeline;
            if (atmosphere != null && atmosphere.sharedProfile != null)
            {
                originalProfile = atmosphere.sharedProfile;
                localProfile = ScriptableObject.CreateInstance<VolumeProfile>(); localProfile.name = "Archive_Lighting_Local_Profile";
                foreach (var component in originalProfile.components) localProfile.components.Add(Instantiate(component));
                atmosphere.sharedProfile = localProfile; localProfile.TryGet(out bloom);
            }
            initialized = true; nextScan = 0;
            foreach (var voice in fireVoices) if (voice != null) { voice.playOnAwake = false; voice.Stop(); AudioVolumeSettings.Route(voice, GameAudioChannel.SoundFX); }
        }
        void LateUpdate()
        {
            if (!initialized) return;
            float night = NightBlend;
            if (bloom != null) bloom.intensity.value = Mathf.Lerp(daylightBloom, nightBloom, night);
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + .4f; ConfigureCameras(); ChooseFires();
            }
            ActiveFireLights = ActiveFireVoices = 0;
            var eye = Observer();
            for (int i = 0; i < fireLights.Length; i++)
            {
                var light = fireLights[i]; if (light == null) continue;
                float distance = Vector3.Distance(eye, light.transform.position);
                float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(fireVisibilityDistance * .75f, fireVisibilityDistance, distance));
                bool show = nearby.Contains(i) && fade > .001f && light.transform.parent.gameObject.activeInHierarchy;
                light.enabled = show; light.range = fireRange;
                if (show)
                {
                    float slow = Mathf.PerlinNoise(i * 3.71f, Time.unscaledTime * .8f);
                    float pulse = (slow - .5f) * 2 * fireVariation;
                    light.intensity = Mathf.Lerp(dayFireIntensity, nightFireIntensity, night) * (1 + pulse) * fade;
                    ActiveFireLights++;
                }
                var voice = i < fireVoices.Length ? fireVoices[i] : null;
                if (voice == null) continue;
                bool audible = show && nearby.IndexOf(i) < maximumFireVoices && distance < 22 && session != null && session.Ready;
                if (audible && voice.clip != null)
                {
                    voice.volume = fireVolume * Mathf.Lerp(.60f, 1, night);
                    if (!voice.isPlaying) voice.Play(); ActiveFireVoices++;
                }
                else if (voice.isPlaying) voice.Stop();
            }
        }
        Vector3 Observer()
        {
            var camera = Camera.main;
            if (camera != null) return camera.transform.position;
            return Player.LocalInstance != null ? Player.LocalInstance.transform.position : transform.position;
        }
        void ChooseFires()
        {
            nearby.Clear(); distances.Clear(); Vector3 eye = Observer(); float radius = fireVisibilityDistance * fireVisibilityDistance;
            for (int i = 0; i < fireLights.Length; i++)
            {
                var light = fireLights[i]; if (light == null || !light.transform.parent.gameObject.activeInHierarchy) continue;
                float distance = (light.transform.position - eye).sqrMagnitude; distances[light] = distance;
                if (distance < radius) nearby.Add(i);
            }
            nearby.Sort((a, b) => distances[fireLights[a]].CompareTo(distances[fireLights[b]]));
            int count = Mathf.Clamp(maximumFireLights, 0, 8); if (nearby.Count > count) nearby.RemoveRange(count, nearby.Count - count);
        }
        void ConfigureCameras()
        {
            foreach (var camera in Camera.allCameras)
            {
                if (camera.gameObject.scene != gameObject.scene || camera.targetTexture != null || camera.cameraType != CameraType.Game || cameras.Any(c => c.camera == camera)) continue;
                var data = camera.GetUniversalAdditionalCameraData();
                cameras.Add(new CameraState { camera = camera, data = data, hdr = camera.allowHDR, post = data.renderPostProcessing, shadows = data.renderShadows, mask = data.volumeLayerMask });
                camera.allowHDR = true; data.renderPostProcessing = true; data.renderShadows = true;
                if (atmosphere != null) data.volumeLayerMask |= 1 << atmosphere.gameObject.layer;
            }
        }
        void OnDisable()
        {
            foreach (var light in fireLights) if (light != null) light.enabled = false;
            foreach (var voice in fireVoices) if (voice != null) voice.Stop();
            ActiveFireLights = ActiveFireVoices = 0; nearby.Clear();
            if (!initialized) return;
            foreach (var state in cameras)
            {
                if (state.camera == null || state.data == null) continue;
                state.camera.allowHDR = state.hdr; state.data.renderPostProcessing = state.post; state.data.renderShadows = state.shadows; state.data.volumeLayerMask = state.mask;
            }
            cameras.Clear();
            if (atmosphere != null && atmosphere.sharedProfile == localProfile) atmosphere.sharedProfile = originalProfile;
            if (localProfile != null)
            {
                foreach (var component in localProfile.components) Destroy(component);
                Destroy(localProfile);
            }
            localProfile = null; bloom = null;
            if (QualitySettings.renderPipeline == pipeline)
            {
                int restore = Array.IndexOf(QualitySettings.names, oldQualityName);
                QualitySettings.SetQualityLevel(restore >= 0 ? restore : oldQuality, true); QualitySettings.renderPipeline = oldOverride;
            }
            initialized = false;
        }
    }
}
