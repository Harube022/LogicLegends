using TMPro;
using UnityEngine;

/// <summary>Keep editable trial text readable on the shared cream status frame.</summary>
[DefaultExecutionOrder(3000)]
public sealed class TrialHudStyle : MonoBehaviour
{
    public TMP_Text Title, Clock, Progress;
    public void Apply()
    {
        Style(Title); Style(Clock); Style(Progress);
    }
    static void Style(TMP_Text label)
    { if (label != null) { label.font = StageUITheme.HeadingFont; label.color = StageUITheme.ReadingInk; label.outlineWidth = 0; label.raycastTarget = false; } }
    void LateUpdate() { Apply(); }
}
