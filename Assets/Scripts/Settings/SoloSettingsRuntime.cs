using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Applies runtime copies of the solo pipeline; other scenes retain their own presentation.</summary>
[DefaultExecutionOrder(31900)]
public sealed class SoloSettingsRuntime : MonoBehaviour
{
    static SoloSettingsRuntime instance;
    UniversalRenderPipelineAsset runtimePipeline;
    RenderPipelineAsset originalPipeline;
    int originalFPS, originalVsync, originalMips;
    float originalLod, originalMeshLodThreshold;
    bool scoped;
    Coroutine pending;
    SoloTrialVisuals visuals;
    int pipelineKey = -1;
    static readonly float[] LodBias = { .4f, .7f, 1f };
    static readonly float[] MeshLodThreshold = { 2f, 1.5f, 1f };
    static readonly float[] RenderScale = { .8f, .9f, 1f };
    static readonly float[] ShadowDistance = { 0f, 20f, 35f, 55f };
    static readonly int[] ShadowResolution = { 512, 512, 1024, 1024 };
    static readonly int[] LightCount = { 4, 6, 8 };
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { instance = null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Create() { new GameObject("Solo Settings").AddComponent<SoloSettingsRuntime>(); }
    void Awake()
    {
        if (instance != null) { Destroy(gameObject); return; }
        instance = this; DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += SceneLoaded; SoloGameSettings.Changed += Apply;
    }
    void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        if (pending != null) StopCoroutine(pending);
        pending = StartCoroutine(AfterSceneReady(scene));
    }
    IEnumerator AfterSceneReady(Scene scene)
    {
        yield return null; yield return null;
        if (scene != SceneManager.GetActiveScene()) yield break;
        bool supported = scene.name == "Main Menu" || SoloGameSettings.IsTrial(scene.name);
        if (!supported) { Restore(); yield break; }
        if (!scoped)
        {
            originalPipeline = QualitySettings.renderPipeline; originalFPS = Application.targetFrameRate;
            originalVsync = QualitySettings.vSyncCount; originalMips = QualitySettings.globalTextureMipmapLimit; originalLod = QualitySettings.lodBias;
            originalMeshLodThreshold = QualitySettings.meshLodThreshold;
            scoped = true;
        }
        visuals = null;
        if (SoloGameSettings.IsTrial(scene.name))
        {
            var go = new GameObject("Solo Trial Lighting and Accessibility"); SceneManager.MoveGameObjectToScene(go, scene);
            visuals = go.AddComponent<SoloTrialVisuals>();
            go.AddComponent<SoloTouchSettings>(); go.AddComponent<SoloAccessibility>();
        }
        Apply();
    }
    void Apply()
    {
        if (!scoped) return;
        var data = SoloGameSettings.Current;
        Application.targetFrameRate = data.frameRate; QualitySettings.vSyncCount = 0;
        QualitySettings.globalTextureMipmapLimit = data.graphicsQuality == 0 ? 1 : 0;
        QualitySettings.lodBias = LodBias[data.graphicsQuality];
        if (Application.platform == RuntimePlatform.Android)
            QualitySettings.meshLodThreshold = MeshLodThreshold[data.graphicsQuality];
        AudioListener.volume = data.mute ? 0 : 1;
        int key = data.graphicsQuality * 100 + data.lightingQuality * 10 + data.shadowQuality;
        if (runtimePipeline == null || pipelineKey != key)
        {
            var source = Resources.Load<UniversalRenderPipelineAsset>("Settings/SoloLighting" + data.lightingQuality);
            if (source != null)
            {
                QualitySettings.renderPipeline = originalPipeline;
                if (runtimePipeline != null) Destroy(runtimePipeline);
                runtimePipeline = Instantiate(source); runtimePipeline.name = "Solo Pipeline (Runtime)";
                runtimePipeline.hideFlags = HideFlags.DontSave;
                runtimePipeline.shadowDistance = ShadowDistance[data.shadowQuality];
                runtimePipeline.mainLightShadowmapResolution = ShadowResolution[data.shadowQuality];
                runtimePipeline.shadowCascadeCount = 1; runtimePipeline.cascadeBorder = .25f;
                runtimePipeline.maxAdditionalLightsCount = LightCount[data.lightingQuality];
                runtimePipeline.renderScale = Application.platform == RuntimePlatform.Android && data.graphicsQuality == 0
                    ? .7f : RenderScale[data.graphicsQuality];
                runtimePipeline.msaaSampleCount = data.graphicsQuality == 2 ? 2 : 1;
                QualitySettings.renderPipeline = runtimePipeline; pipelineKey = key;
            }
        }
        if (visuals != null) visuals.Apply(data);
    }
    void Restore()
    {
        if (!scoped) return;
        QualitySettings.renderPipeline = originalPipeline;
        if (runtimePipeline != null) Destroy(runtimePipeline); runtimePipeline = null; pipelineKey = -1;
        Application.targetFrameRate = originalFPS; QualitySettings.vSyncCount = originalVsync;
        QualitySettings.globalTextureMipmapLimit = originalMips; QualitySettings.lodBias = originalLod; scoped = false;
        QualitySettings.meshLodThreshold = originalMeshLodThreshold;
    }
    void OnDestroy()
    {
        if (instance != this) return;
        Restore(); SceneManager.sceneLoaded -= SceneLoaded; SoloGameSettings.Changed -= Apply; instance = null;
    }
}
