using UnityEngine;

/// <summary>Safe bounds for the multiplayer reading views, including a stale Editor Simulator surface.</summary>
public static class MultiplayerSafeArea
{
    public static Rect ScreenRect => Bounds(Screen.width, Screen.height);
    static Rect Bounds(float width, float height)
    {
            var area = Screen.safeArea;
            var full = new Rect(0, 0, width, height);
            if (area.width <= 0 || area.height <= 0 || area.xMin < 0 || area.yMin < 0 || area.xMax > width + 1 || area.yMax > height + 1) return full;
#if UNITY_EDITOR
            // Device Simulator can retain the old render surface after Game View
            // switches to a larger preset. Real device insets remain untouched.
            if (area.width < width * .8f && area.height < height * .8f) return full;
#endif
            return area;
    }
    public static void Apply(RectTransform rect)
    {
        var canvas = rect.GetComponentInParent<Canvas>();
        var pixels = canvas != null ? canvas.rootCanvas.pixelRect : new Rect(0, 0, Screen.width, Screen.height);
        var area = Bounds(pixels.width, pixels.height);
        rect.anchorMin = new Vector2(area.xMin / Mathf.Max(1, pixels.width), area.yMin / Mathf.Max(1, pixels.height));
        rect.anchorMax = new Vector2(area.xMax / Mathf.Max(1, pixels.width), area.yMax / Mathf.Max(1, pixels.height));
    }
}
