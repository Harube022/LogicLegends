using TMPro;
using UnityEngine;

/// <summary>Shows a confirmed receipt (or a pending save), without granting on UI reopen.</summary>
public sealed class TrialRewardDisplay : MonoBehaviour
{
    int trial;
    TMP_Text label;
    public static void Attach(StageResultLayout view, int trial)
    {
        var display = view.GetComponent<TrialRewardDisplay>();
        if (display == null) display = view.gameObject.AddComponent<TrialRewardDisplay>();
        display.trial = trial;
        if (display.label == null) display.label = StageUITheme.Text("TrialCoinReward", view.Card, "", 30, true);
        StageUITheme.Stretch(display.label.rectTransform, new Vector2(.08f,.285f), new Vector2(.92f,.345f));
        display.label.font = StageUITheme.HeadingFont; display.label.color = StageUITheme.ReadingInk;
        display.label.enableAutoSizing = true; display.label.fontSizeMin = 20; display.label.fontSizeMax = 30;
        TrialRewards.Complete(trial); display.Refresh();
    }
    void OnEnable() { TrialRewards.Changed += Refresh; Refresh(); }
    void OnDisable() { TrialRewards.Changed -= Refresh; }
    void Refresh() { if (label != null) label.text = TrialRewards.Status(trial); }
}
