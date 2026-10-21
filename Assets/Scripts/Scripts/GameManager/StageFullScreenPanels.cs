using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Full-screen modal shades belong to the Canvas; edge HUDs stay in SafeArea.</summary>
[DefaultExecutionOrder(32000)]
public sealed class StageFullScreenPanels : MonoBehaviour
{
    private bool watcher;
    private bool dirty = true;

    public static Canvas RootCanvas(Transform content)
    {
        // Canvas.rootCanvas can report an inactive nested Canvas as its own root.
        // Walk the hierarchy explicitly so authored hidden panels use the screen Canvas.
        Canvas root = null;
        for (var current = content; current != null; current = current.parent)
        {
            var canvas = current.GetComponent<Canvas>();
            if (canvas != null) root = canvas;
        }
        return root;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= Install;
        SceneManager.sceneLoaded += Install;
    }

    private static void Install(Scene scene, LoadSceneMode mode)
    {
        bool trialScene = false;
        for (int i = 1; i <= TrialCatalog.Count; i++)
            if (TrialCatalog.Get(i).Scene == scene.name) { trialScene = true; break; }
        if (!trialScene) return;
        var setup = new GameObject("Stage Full Screen Panel Setup");
        SceneManager.MoveGameObjectToScene(setup, scene);
        setup.AddComponent<StageFullScreenPanels>();
    }

    private IEnumerator Start()
    {
        // Let the stage's Awake/Start methods finish creating their UI and references.
        yield return null;
        if (watcher) { dirty = true; yield break; }
        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                if (canvas.isRootCanvas && canvas.renderMode != RenderMode.WorldSpace)
                    Watch(canvas.gameObject);
        Destroy(gameObject);
    }

    private static void Watch(GameObject owner)
    {
        if (owner.GetComponent<StageFullScreenPanels>() != null) return;
        owner.AddComponent<StageFullScreenPanels>().watcher = true;
    }

    private void OnEnable() { dirty = true; }
    private void OnTransformChildrenChanged() { dirty = true; }

    private void LateUpdate()
    {
        if (!watcher || !dirty) return;
        dirty = false;
        // Watch transparent full-screen containers too: some stages create their
        // quizzes later inside a HUD container, rather than directly in SafeArea.
        foreach (var rect in GetComponentsInChildren<RectTransform>(true))
        {
            var image = rect.GetComponent<Image>();
            if (rect.GetComponent<Crystal.SafeArea>() != null ||
                (FullStretch(rect) && SafeAncestor(rect) != null &&
                 (image == null || image.color.a < .01f))) Watch(rect.gameObject);
        }
        foreach (var image in GetComponentsInChildren<Image>(true))
        {
            var rect = image.rectTransform;
            var safe = SafeAncestor(rect);
            if (safe == null || !FullStretch(rect) || !DarkShade(image) || rect.childCount == 0) continue;
            if (rect.parent != null && !rect.parent.gameObject.activeInHierarchy && rect.gameObject.activeSelf) continue;
            // Exclude small, stretched card interiors and reading viewports.
            if (rect.rect.width * Mathf.Abs(rect.lossyScale.x) < safe.rect.width * Mathf.Abs(safe.lossyScale.x) * .9f ||
                rect.rect.height * Mathf.Abs(rect.lossyScale.y) < safe.rect.height * Mathf.Abs(safe.lossyScale.y) * .9f) continue;
            var canvas = RootCanvas(rect);
            if (canvas.renderMode == RenderMode.WorldSpace) continue;
            rect.SetParent(canvas.transform, false);
            StageUITheme.Stretch(rect, Vector2.zero, Vector2.one);
            rect.localScale = Vector3.one;
            rect.SetAsLastSibling();
        }
    }

    private static bool FullStretch(RectTransform rect)
    {
        return rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one &&
               rect.offsetMin.sqrMagnitude < 1f && rect.offsetMax.sqrMagnitude < 1f;
    }

    private static bool DarkShade(Image image)
    {
        var color = image.color;
        return color.a > .1f && Mathf.Max(color.r, Mathf.Max(color.g, color.b)) < .25f;
    }

    private static RectTransform SafeAncestor(Transform item)
    {
        for (var parent = item.parent; parent != null; parent = parent.parent)
            if (parent.GetComponent<Crystal.SafeArea>() != null) return parent as RectTransform;
        return null;
    }
}
