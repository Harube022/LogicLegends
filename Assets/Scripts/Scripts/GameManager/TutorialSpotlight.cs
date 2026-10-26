using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A visual-only veil with cutouts for a tutorial's real control and its reading UI.
/// It never moves controls, changes their sorting, or receives pointer events.
/// </summary>
[DefaultExecutionOrder(32000)]
[DisallowMultipleComponent]
public sealed class TutorialSpotlight : MonoBehaviour
{
    RectTransform explanation, target, arrow, overlay;
    Canvas rootCanvas;
    TutorialSpotlightVeil veil;
    int refreshedAt = -100;
    readonly List<Rect> openings = new List<Rect>(6);
    readonly Vector3[] corners = new Vector3[4];
    readonly List<PrelimTutorialSkipControl> skipControls = new List<PrelimTutorialSkipControl>();
    readonly List<UnityEngine.UI.Button> buttons = new List<UnityEngine.UI.Button>();

    /// <summary>Refresh once per tutorial layout pass. A missing target removes the veil.</summary>
    public static void Follow(RectTransform panel, RectTransform subject, RectTransform pointer)
    {
        if (panel == null) return;
        var spotlight = panel.GetComponent<TutorialSpotlight>();
        if (subject == null || !subject.gameObject.activeInHierarchy)
        {
            if (spotlight != null) spotlight.Hide();
            return;
        }
        if (spotlight == null) spotlight = panel.gameObject.AddComponent<TutorialSpotlight>();
        spotlight.explanation = panel;
        spotlight.target = subject;
        spotlight.arrow = pointer;
        spotlight.refreshedAt = Time.frameCount;
        spotlight.Prepare();
    }

    public bool IsVisible => overlay != null && overlay.gameObject.activeInHierarchy;
    public Rect TargetScreenRect { get; private set; }

    void Prepare()
    {
        if (overlay != null) return;
        var canvas = explanation.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        rootCanvas = canvas.rootCanvas;
        // Use the full canvas, not its safe-area child, so the edges dim consistently.
        var go = new GameObject("Tutorial Spotlight (Visual Only)", typeof(RectTransform), typeof(CanvasRenderer), typeof(TutorialSpotlightVeil));
        overlay = go.GetComponent<RectTransform>();
        overlay.SetParent(rootCanvas.transform, false);
        overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one;
        overlay.offsetMin = overlay.offsetMax = Vector2.zero;
        // A visual-only canvas covers controls on other stage canvases as well.
        // Bright regions are holes, so the original controls remain in place.
        var canvasLayer = go.AddComponent<Canvas>();
        canvasLayer.overrideSorting = true; canvasLayer.sortingOrder = 5000;
        canvasLayer.targetDisplay = rootCanvas.targetDisplay;
        veil = go.GetComponent<TutorialSpotlightVeil>();
        veil.color = new Color(.015f, .025f, .045f, .46f);
        veil.raycastTarget = false;
        // The graphic's default material respects the canvas without adding a raycaster.
        go.SetActive(false);
    }

    void OnEnable() => Canvas.preWillRenderCanvases += BeforeRender;
    void OnDisable()
    {
        Canvas.preWillRenderCanvases -= BeforeRender;
        Hide();
    }
    void OnDestroy()
    {
        Canvas.preWillRenderCanvases -= BeforeRender;
        if (overlay != null) Destroy(overlay.gameObject);
    }

    void LateUpdate() => Refresh();
    void BeforeRender() => Refresh();

    void Refresh()
    {
        if (overlay == null) return;
        if (refreshedAt != Time.frameCount || explanation == null ||
            !explanation.gameObject.activeInHierarchy || target == null || !target.gameObject.activeInHierarchy)
        {
            Hide(); return;
        }
        openings.Clear();
        Rect subject;
        if (!ScreenBounds(target, out subject) || subject.width <= 1f || subject.height <= 1f)
        { Hide(); return; }
        TargetScreenRect = subject;
        AddOpening(subject, 18f);
        AddOpening(explanation, 8f);
        if (arrow != null) AddOpening(arrow, 10f);
        // Skip is shared by initial and per-challenge introductions and may be a
        // sibling of the instruction panel or belong to another nested canvas.
        skipControls.Clear();
        rootCanvas.GetComponentsInChildren(false, skipControls);
        foreach (var skip in skipControls) AddOpening(skip.transform as RectTransform, 10f);
        // Rules of Inference retains the same shared skip behaviour on a Button.
        rootCanvas.GetComponentsInChildren(false, buttons);
        foreach (var button in buttons)
            if (button.name == "Inference Shared Skip") AddOpening(button.transform as RectTransform, 10f);
        veil.SetOpenings(openings);
        overlay.gameObject.SetActive(true);
        overlay.SetAsLastSibling();
    }

    void AddOpening(RectTransform rect, float padding)
    {
        Rect bounds;
        if (rect != null && rect.gameObject.activeInHierarchy && ScreenBounds(rect, out bounds))
            AddOpening(bounds, padding);
    }

    void AddOpening(Rect screen, float padding)
    {
        Vector2 min, max;
        var camera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, screen.min, camera, out min) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, screen.max, camera, out max)) return;
        var bounds = Rect.MinMaxRect(min.x - padding, min.y - padding, max.x + padding, max.y + padding);
        var area = overlay.rect;
        bounds = Rect.MinMaxRect(Mathf.Max(bounds.xMin, area.xMin), Mathf.Max(bounds.yMin, area.yMin),
            Mathf.Min(bounds.xMax, area.xMax), Mathf.Min(bounds.yMax, area.yMax));
        if (bounds.width > 0f && bounds.height > 0f) openings.Add(bounds);
    }

    bool ScreenBounds(RectTransform rect, out Rect result)
    {
        var canvas = rect.GetComponentInParent<Canvas>();
        var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        rect.GetWorldCorners(corners);
        Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
        foreach (var corner in corners)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner);
            min = Vector2.Min(min, point); max = Vector2.Max(max, point);
        }
        result = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return min.x < Screen.width && max.x > 0f && min.y < Screen.height && max.y > 0f;
    }

    void Hide()
    {
        refreshedAt = -100;
        if (overlay != null) overlay.gameObject.SetActive(false);
        TargetScreenRect = Rect.zero;
    }
}

/// <summary>Draws only the union's exterior; overlapping cutouts never darken each other.</summary>
internal sealed class TutorialSpotlightVeil : UnityEngine.UI.MaskableGraphic
{
    readonly List<Rect> openings = new List<Rect>(6);
    readonly List<float> xs = new List<float>(14), ys = new List<float>(14);

    public void SetOpenings(List<Rect> values)
    {
        bool changed = openings.Count != values.Count;
        if (!changed)
            for (int i = 0; i < values.Count; i++)
                if (openings[i] != values[i]) { changed = true; break; }
        if (!changed) return;
        openings.Clear(); openings.AddRange(values);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
    {
        vh.Clear(); xs.Clear(); ys.Clear();
        Rect area = rectTransform.rect;
        xs.Add(area.xMin); xs.Add(area.xMax); ys.Add(area.yMin); ys.Add(area.yMax);
        foreach (Rect hole in openings)
        { xs.Add(hole.xMin); xs.Add(hole.xMax); ys.Add(hole.yMin); ys.Add(hole.yMax); }
        xs.Sort(); ys.Sort();
        Color32 tint = color;
        for (int x = 0; x < xs.Count - 1; x++)
        for (int y = 0; y < ys.Count - 1; y++)
        {
            float left = xs[x], right = xs[x + 1], bottom = ys[y], top = ys[y + 1];
            if (right - left < .1f || top - bottom < .1f) continue;
            Vector2 midpoint = new Vector2((left + right) * .5f, (bottom + top) * .5f);
            bool transparent = false;
            foreach (Rect hole in openings) if (hole.Contains(midpoint)) { transparent = true; break; }
            if (transparent) continue;
            int start = vh.currentVertCount;
            vh.AddVert(new Vector3(left, bottom, 0f), tint, Vector2.zero);
            vh.AddVert(new Vector3(left, top, 0f), tint, Vector2.zero);
            vh.AddVert(new Vector3(right, top, 0f), tint, Vector2.zero);
            vh.AddVert(new Vector3(right, bottom, 0f), tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
