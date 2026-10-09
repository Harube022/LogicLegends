using UnityEngine;

/// <summary>Fits the authored pause card inside an Android device's safe area without changing its prefab hierarchy.</summary>
public sealed class PauseSettingsLayout : MonoBehaviour
{
    private RectTransform card;
    private Canvas canvas;
    private Rect lastSafeArea;
    private Vector2 lastScreen;
    private float lastCanvasScale;
    private void OnEnable()
    {
        card = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>().rootCanvas;
        Fit();
    }
    private void LateUpdate()
    {
        if (lastSafeArea != Screen.safeArea || lastScreen != new Vector2(Screen.width, Screen.height) || (canvas != null && !Mathf.Approximately(lastCanvasScale, canvas.scaleFactor))) Fit();
    }
    private void Fit()
    {
        if (canvas == null) return;
        lastSafeArea = Screen.safeArea; lastScreen = new Vector2(Screen.width, Screen.height);
        lastCanvasScale = canvas.scaleFactor;
        float canvasScale = Mathf.Max(0.001f, lastCanvasScale);
        // Device Simulator can retain physical safe-area coordinates while a regular Game view renders at another size.
        // Clamp to the current viewport; real Android safe areas already lie inside these bounds.
        var safe = Rect.MinMaxRect(Mathf.Clamp(lastSafeArea.xMin, 0, Screen.width), Mathf.Clamp(lastSafeArea.yMin, 0, Screen.height),
            Mathf.Clamp(lastSafeArea.xMax, 0, Screen.width), Mathf.Clamp(lastSafeArea.yMax, 0, Screen.height));
        if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, Screen.width, Screen.height);
        float width = safe.width / canvasScale - 48;
        float height = safe.height / canvasScale - 48;
        float fit = Mathf.Min(1f, width / card.rect.width, height / card.rect.height);
        card.localScale = Vector3.one * Mathf.Max(0.1f, fit);
        card.anchoredPosition = (safe.center - lastScreen * 0.5f) / canvasScale;
    }
}