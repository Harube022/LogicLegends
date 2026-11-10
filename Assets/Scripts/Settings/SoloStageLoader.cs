using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One asynchronous solo load, with the existing artwork surviving activation.</summary>
public sealed class SoloStageLoader : MonoBehaviour
{
    static SoloStageLoader instance;
    public static bool IsLoading => instance != null;
    SharedLoadingScreen screen;
    GameInput blockedInput;
    ThreadPriority previousPriority;
    bool ownsPriority;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { instance = null; }

    public static bool Load(string destination)
    {
        if (IsLoading || string.IsNullOrEmpty(destination)) return false;
        if (!Application.CanStreamedLevelBeLoaded(destination))
        { Debug.LogError("Scene missing from build: " + destination); return false; }
        // Navigation ends the outgoing pause/menu. Incoming Help/tutorials own
        // their own pause; they must not snapshot a stale zero time scale.
        Time.timeScale = 1f;
        instance = new GameObject("Solo Stage Loading").AddComponent<SoloStageLoader>();
        DontDestroyOnLoad(instance.gameObject);
        instance.StartCoroutine(instance.LoadScene(destination));
        return true;
    }

    IEnumerator LoadScene(string destination)
    {
        previousPriority = Application.backgroundLoadingPriority;
        ownsPriority = true;
        // Avoid the default large background integration allowance on Android.
        if (Application.platform == RuntimePlatform.Android) Application.backgroundLoadingPriority = ThreadPriority.Low;
        BlockInput();
        var resources = Resources.LoadAsync<SoloLoadingResources>("Settings/SoloLoadingResources");
        yield return resources;
        var setup = resources.asset as SoloLoadingResources;
        if (setup == null || setup.screen == null)
        {
            Debug.LogError("Solo loading screen reference missing; destination was not loaded.");
            Destroy(gameObject); yield break;
        }
        screen = Instantiate(setup.screen, transform);
        screen.ShowSceneLoading();
        yield return null; // Present the artwork before beginning asset integration.
        var operation = SceneManager.LoadSceneAsync(destination, LoadSceneMode.Single);
        if (operation == null) { Destroy(gameObject); yield break; }
        operation.allowSceneActivation = false;
        while (operation.progress < .9f)
        {
            screen.SetProgress(operation.progress / .9f * .85f);
            yield return null;
        }
        screen.SetProgress(-1f); // Activation/Start have no measurable percentage.
        operation.allowSceneActivation = true;
        while (!operation.isDone) yield return null;
        // Awake/Start, spawning, material conversion and settings must complete
        // under the persistent artwork. No duplicated gameplay managers or assets.
        yield return null;
        yield return null;
        BlockInput();
        var scene = SceneManager.GetActiveScene();
        if (SoloGameSettings.IsTrial(scene.name))
        {
            while (Player.LocalInstance == null || !SoloSettingsRuntime.IsReady(scene)) yield return null;
            var graph = FindFirstObjectByType<LogicLegends.Graph.GraphStageManager>();
            var trees = FindFirstObjectByType<LogicLegends.Trees.TreeStageManager>();
            var induction = FindFirstObjectByType<LogicLegends.Induction.InductionStageManager>();
            var students = FindFirstObjectByType<LogicLegends.Students.StudentNpcDirector>();
            while (graph != null && !graph.Ready || trees != null && !trees.Ready ||
                   induction != null && !induction.Ready || students != null && !students.CatalogReady) yield return null;
            TeleportCameraSync.ResetAfterMove(Player.LocalInstance.transform, Vector3.zero);
        }
        yield return null;
        yield return screen.CompleteSceneLoading();
        yield return screen.FadeOut();
        Destroy(gameObject);
    }

    void BlockInput()
    {
        if (blockedInput != null) blockedInput.ReleaseLoadingInputBlock(this);
        blockedInput = GameInput.Instance;
        if (blockedInput != null) blockedInput.AcquireLoadingInputBlock(this);
    }
    void OnDestroy()
    {
        if (blockedInput != null) blockedInput.ReleaseLoadingInputBlock(this);
        if (ownsPriority) Application.backgroundLoadingPriority = previousPriority;
        if (instance == this) instance = null;
    }
}
