using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Initializes the graphics, audio, touch and accessibility settings for solo trials.</summary>
[DefaultExecutionOrder(31900)]
public sealed class SoloSettingsRuntime : MonoBehaviour
{
    static SoloSettingsRuntime instance;
    Coroutine pending;
    SoloTrialGraphics graphics;
    SoloGraphicsProfile graphicsAssets;
    int appliedSceneHandle = -1;

    public static bool IsReady(Scene scene) => instance != null && instance.appliedSceneHandle == scene.handle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Create() { new GameObject("Solo Settings").AddComponent<SoloSettingsRuntime>(); }

    void Awake()
    {
        if (instance != null) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += SceneLoaded;
        SoloGameSettings.Changed += SettingsChanged;
        SceneManager.activeSceneChanged += ActiveSceneChanged;
    }

    // The first scene may already be loaded when this component subscribes.
    void Start() { QueueScene(SceneManager.GetActiveScene()); }

    void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single || scene == SceneManager.GetActiveScene()) QueueScene(scene);
    }

    void ActiveSceneChanged(Scene previous, Scene next) { QueueScene(next); }

    void QueueScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        if (!SoloGameSettings.IsTrial(scene.name)) ReleaseGraphics();
        if (SoloGameSettings.IsTrial(scene.name))
            foreach (var root in scene.GetRootGameObjects())
                DisableUnusedTrialCounters(root);

        if (pending != null) StopCoroutine(pending);
        pending = StartCoroutine(AfterSceneReady(scene));
    }

    public static void DisableUnusedTrialCounters(GameObject root)
    {
        if (root == null || !SoloGameSettings.IsTrial(SceneManager.GetActiveScene().name)) return;
        foreach (var counter in root.GetComponentsInChildren<Photon.Pun.Demo.Cockpit.CountOfPlayersProperty>(true))
            if (counter.Text == null) counter.enabled = false;
    }

    IEnumerator AfterSceneReady(Scene scene)
    {
        yield return null;
        yield return null;
        if (scene != SceneManager.GetActiveScene()) yield break;
        pending = null;

        bool trial = SoloGameSettings.IsTrial(scene.name);
        if (appliedSceneHandle != scene.handle && trial)
        {
            var settings = new GameObject("Solo Trial Touch and Accessibility");
            SceneManager.MoveGameObjectToScene(settings, scene);
            settings.AddComponent<SoloTouchSettings>();
            settings.AddComponent<SoloAccessibility>();
        }

        if (trial)
        {
            if (graphicsAssets == null) graphicsAssets = Resources.Load<SoloGraphicsProfile>("Settings/SoloGraphicsProfile");
            if (graphicsAssets != null && graphicsAssets.pipeline != null && graphicsAssets.atmosphere != null)
            {
                if (graphics == null) graphics = new SoloTrialGraphics(graphicsAssets);
                graphics.Enter(scene, SoloGameSettings.Current);
            }
            else Debug.LogError("Missing solo graphics profile for " + scene.name);
        }

        if (trial || scene.name == "Main Menu")
            AudioListener.volume = SoloGameSettings.Current.mute ? 0 : 1;
        appliedSceneHandle = scene.handle;
    }

    void SettingsChanged()
    {
        if (SoloGameSettings.InTrial) graphics?.Apply(SoloGameSettings.Current);
    }

    void LateUpdate() => graphics?.Refresh();

    // Multiplayer acquires its own profile in OnEnable, which can run before
    // sceneLoaded. Restore the app defaults before it captures them.
    public static void ReleaseGraphics()
    {
        if (instance == null) return;
        instance.graphics?.Dispose();
        instance.graphics = null;
    }

    void OnDestroy()
    {
        if (instance != this) return;
        ReleaseGraphics();
        SoloGameSettings.Changed -= SettingsChanged;
        SceneManager.sceneLoaded -= SceneLoaded;
        SceneManager.activeSceneChanged -= ActiveSceneChanged;
        instance = null;
    }
}
