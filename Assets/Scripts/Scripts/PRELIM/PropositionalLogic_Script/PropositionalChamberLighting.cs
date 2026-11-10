using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Dim ambient light only while a camera views one of the five proof-door chambers.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class PropositionalChamberLighting : MonoBehaviour
{
    public Light sunlight;
    public Light[] torches = new Light[0];
    public Bounds[] chamberBounds = new Bounds[0];
    public string[] chamberNames = new string[0];
    [ColorUsage(false)] public Color ambientColor = new Color(.135f, .145f, .17f);
    [Range(0, 1)] public float sunlightIntensity = .16f;
    [Range(0, 1)] public float reflectionIntensity = .5f;
    public int ScopedRenderCount { get; private set; }

    struct Environment
    {
        public Scene scene;
        public AmbientMode mode;
        public Color sky, equator, ground;
        public float ambientIntensity, reflectionIntensity, sunIntensity;
        public SphericalHarmonicsL2 probe;
        public Light sun;
        public static Environment Capture(Light light) => new Environment {
            scene = SceneManager.GetActiveScene(), mode = RenderSettings.ambientMode,
            sky = RenderSettings.ambientSkyColor, equator = RenderSettings.ambientEquatorColor,
            ground = RenderSettings.ambientGroundColor, ambientIntensity = RenderSettings.ambientIntensity,
            reflectionIntensity = RenderSettings.reflectionIntensity, probe = RenderSettings.ambientProbe,
            sun = light, sunIntensity = light != null ? light.intensity : 0
        };
        public void Restore()
        {
            // An unloading scene must never overwrite the next trial's environment.
            if (SceneManager.GetActiveScene() == scene)
            {
                RenderSettings.ambientMode = mode;
                RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = equator;
                RenderSettings.ambientGroundColor = ground; RenderSettings.ambientIntensity = ambientIntensity;
                RenderSettings.reflectionIntensity = reflectionIntensity; RenderSettings.ambientProbe = probe;
            }
            if (sun != null) sun.intensity = sunIntensity;
        }
    }
    readonly Dictionary<Camera, Environment> scopes = new Dictionary<Camera, Environment>();

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCamera;
        RenderPipelineManager.endCameraRendering -= EndCamera;
        RenderPipelineManager.beginCameraRendering += BeginCamera;
        RenderPipelineManager.endCameraRendering += EndCamera;
    }
    public bool Contains(Vector3 position)
    {
        foreach (var bounds in chamberBounds) if (bounds.Contains(position)) return true;
        return false;
    }
    public bool AppliesTo(Camera camera)
    {
        if (camera == null || !isActiveAndEnabled || gameObject.scene.name != "PRELIM" ||
            SceneManager.GetActiveScene() != gameObject.scene ||
            camera.cameraType == CameraType.Reflection || camera.cameraType == CameraType.Preview) return false;
        if (Application.isPlaying && StageSelectionState.SelectedStage != 1) return false;
        if (Contains(camera.transform.position)) return true;
        // Close-ups and transitional tutorial views can stand outside the room.
        var view = new Ray(camera.transform.position, camera.transform.forward);
        foreach (var bounds in chamberBounds)
            if (bounds.IntersectRay(view, out float distance) && distance < 240) return true;
        return Application.isPlaying && camera == Camera.main && Player.LocalInstance != null && Contains(Player.LocalInstance.transform.position);
    }
    void BeginCamera(ScriptableRenderContext context, Camera camera)
    {
        if (!AppliesTo(camera) || scopes.ContainsKey(camera)) return;
        var original = Environment.Capture(sunlight); scopes.Add(camera, original);
        Color fill = Application.platform == RuntimePlatform.Android
            ? Color.Lerp(ambientColor, new Color(.36f, .325f, .28f), .4f) : ambientColor;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = fill; RenderSettings.ambientIntensity = 1;
        RenderSettings.reflectionIntensity = Mathf.Min(original.reflectionIntensity, reflectionIntensity);
        // Scale the scene's existing SH rather than rebuilding or baking global probes.
        var probe = original.probe; Color source = original.sky.linear, target = fill.linear;
        for (int channel = 0; channel < 3; channel++)
        {
            float scale = Mathf.Clamp01(target[channel] / Mathf.Max(.001f, source[channel]));
            for (int coefficient = 0; coefficient < 9; coefficient++) probe[channel, coefficient] *= scale;
        }
        RenderSettings.ambientProbe = probe;
        if (sunlight != null) sunlight.intensity = Mathf.Min(original.sunIntensity, sunlightIntensity);
        ScopedRenderCount++;
    }
    void EndCamera(ScriptableRenderContext context, Camera camera)
    {
        if (!scopes.TryGetValue(camera, out var original)) return;
        original.Restore(); scopes.Remove(camera);
    }
    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCamera;
        RenderPipelineManager.endCameraRendering -= EndCamera;
        foreach (var original in scopes.Values) original.Restore();
        scopes.Clear();
    }
}
