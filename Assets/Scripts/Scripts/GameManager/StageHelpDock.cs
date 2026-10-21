using UnityEngine;
using UnityEngine.UI;

/// <summary>One physical Help size beside the existing Settings control.</summary>
[DefaultExecutionOrder(3000)]
public sealed class StageHelpDock : MonoBehaviour
{
    public RectTransform Settings;
    readonly Vector3[] corners = new Vector3[4];
    public void Layout()
    {
        if (Settings == null)
            foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (button.name == "SettingsButton") { Settings = button.transform as RectTransform; break; }
        var rect = (RectTransform)transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = new Vector2(220, 88);
        if (Settings == null) { rect.anchoredPosition = new Vector2(230, -43); return; }
        float scale = Settings.parent.lossyScale.x;
        rect.localScale = Vector3.one * scale / Mathf.Max(.001f, rect.parent.lossyScale.x);
        Settings.GetWorldCorners(corners);
        rect.position = new Vector3(corners[2].x + 35 * scale, (corners[0].y + corners[1].y) * .5f + 44 * scale, corners[1].z);
    }
    void LateUpdate() { Layout(); }
}
