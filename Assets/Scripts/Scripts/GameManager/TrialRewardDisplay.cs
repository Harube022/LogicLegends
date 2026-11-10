using TMPro;
using UnityEngine;

/// <summary>Shows a confirmed receipt (or a pending save), without granting on UI reopen.</summary>
public sealed class TrialRewardDisplay : MonoBehaviour
{
    int trial;
    TMP_Text label;
    TMP_Text rewardHeading;
    bool completed;
    RectTransform coinRow;
    public static void Attach(StageResultLayout view, int trial, bool completed = true)
    {
        var display = view.GetComponent<TrialRewardDisplay>();
        if (display == null) display = view.gameObject.AddComponent<TrialRewardDisplay>();
        display.trial = trial; display.completed = completed;
        if (display.label == null) display.label = StageUITheme.Text("TrialCoinReward", view.Card, "", 30, true);
        if (completed)
        {
            if (display.rewardHeading == null)
                display.rewardHeading = StageUITheme.Text("CompletionRewardHeading", view.Card, "REWARDS EARNED", 28);
            display.rewardHeading.gameObject.SetActive(true);
            StageUITheme.Stretch(display.rewardHeading.rectTransform, new Vector2(.08f, .45f), new Vector2(.92f, .49f));
            display.rewardHeading.color = StageUITheme.ReadingInk;
            display.rewardHeading.enableAutoSizing = true;
            display.rewardHeading.fontSizeMin = 22;
            display.rewardHeading.fontSizeMax = 28;
            display.rewardHeading.alignment = TextAlignmentOptions.Center;
            display.rewardHeading.textWrappingMode = TextWrappingModes.NoWrap;
            display.rewardHeading.raycastTarget = false;
            if (display.coinRow == null)
            {
                display.coinRow = StageUITheme.Rect("Completion Coin Reward", view.Card);
                var icon = StageUITheme.Rect("Coin Icon", display.coinRow);
                var image = icon.gameObject.AddComponent<UnityEngine.UI.Image>();
                image.sprite = TrialRewardArt.Coin; image.preserveAspect = true; image.raycastTarget = false;
                icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(0, .5f);
                icon.sizeDelta = new Vector2(64, 64); icon.anchoredPosition = Vector2.zero;
            }
            display.coinRow.gameObject.SetActive(true);
            display.coinRow.anchorMin = display.coinRow.anchorMax = display.coinRow.pivot = new Vector2(.5f, .385f);
            display.coinRow.sizeDelta = new Vector2(300, 84); display.coinRow.anchoredPosition = Vector2.zero;
            display.label.transform.SetParent(display.coinRow, false);
            StageUITheme.Stretch(display.label.rectTransform, Vector2.zero, Vector2.one);
            display.label.rectTransform.offsetMin = new Vector2(86, 0);
        }
        else
        {
            if (display.rewardHeading != null) display.rewardHeading.gameObject.SetActive(false);
            if (display.coinRow != null) display.coinRow.gameObject.SetActive(false);
            display.label.transform.SetParent(view.Card, false);
            StageUITheme.Stretch(display.label.rectTransform, new Vector2(.08f,.30f), new Vector2(.92f,.49f));
        }
        display.label.font = StageUITheme.ReadingFont; display.label.color = StageUITheme.ReadingInk;
        display.label.enableAutoSizing = true; display.label.fontSizeMin = 24; display.label.fontSizeMax = completed ? 44 : 28;
        display.label.alignment = completed ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center;
        display.label.textWrappingMode = TextWrappingModes.Normal; display.label.raycastTarget = false;
        if (completed) TrialRewards.Complete(trial); display.Refresh();
    }
    void OnEnable() { TrialRewards.Changed += Refresh; Refresh(); }
    void OnDisable() { TrialRewards.Changed -= Refresh; }
    void Refresh() { if (label != null) label.text = completed ? TrialRewards.CompletionCoins(trial).ToString("N0") : TrialRewards.Breakdown(trial, false); }
}
