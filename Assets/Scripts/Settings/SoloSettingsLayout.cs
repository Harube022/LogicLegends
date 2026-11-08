using UnityEngine;

/// <summary>Fits the same authored settings surface to menu and gameplay safe areas.</summary>
public sealed class SoloSettingsLayout : MonoBehaviour
{
    RectTransform rect;
    Canvas canvas;
    Rect lastSafe;
    Vector2 lastScreen, lastCanvas;
    float lastScale = -1;
    void OnEnable() { rect = (RectTransform)transform; canvas = GetComponentInParent<Canvas>()?.rootCanvas; Fit(); }
    void LateUpdate() { Fit(); }
    public void Fit()
    {
        if (rect == null || canvas == null) return;
        var area = (RectTransform)canvas.transform;
        var safe = Application.isPlaying ? Screen.safeArea : new Rect(0, 0, Screen.width, Screen.height);
        safe = Rect.MinMaxRect(Mathf.Clamp(safe.xMin, 0, Screen.width), Mathf.Clamp(safe.yMin, 0, Screen.height),
            Mathf.Clamp(safe.xMax, 0, Screen.width), Mathf.Clamp(safe.yMax, 0, Screen.height));
        if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, Screen.width, Screen.height);
        var scale = Mathf.Max(.001f, canvas.scaleFactor);
        var screen = new Vector2(Screen.width, Screen.height);
        if (lastSafe == safe && lastScreen == screen && lastCanvas == area.rect.size && Mathf.Approximately(lastScale, scale)) return;
        lastSafe = safe; lastScreen = screen; lastCanvas = area.rect.size; lastScale = scale;
        var available = new Vector2(Mathf.Min(area.rect.width, safe.width / scale), Mathf.Min(area.rect.height, safe.height / scale));
        float fit = Mathf.Min(1, available.x / 1920, available.y / 1080);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
        rect.sizeDelta = new Vector2(1920, 1080); rect.localScale = Vector3.one * Mathf.Max(.1f, fit);
        rect.anchoredPosition = Application.isPlaying ? (safe.center - new Vector2(Screen.width, Screen.height) * .5f) / scale : Vector2.zero;
    }
}
