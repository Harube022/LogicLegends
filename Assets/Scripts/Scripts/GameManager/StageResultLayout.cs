using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Full-canvas result overlay, with bounded matching actions and slim GraphHUD artwork.</summary>
[DefaultExecutionOrder(3100)]
public sealed class StageResultLayout : MonoBehaviour
{
    public RectTransform Card;
    public TMP_Text Stage, Heading, Summary;
    public Button Primary, Menu, Extra;
    public bool LegacySummary;
    public bool HasCoinReward;
    string previousSummary;
    bool navigating;
    void OnEnable() { Layout(); }
    void LateUpdate() { Layout(); }
    public void Layout()
    {
        if (Card == null) return;
        Card.GetComponent<Image>().pixelsPerUnitMultiplier = 3f;
        var canvas = GetComponentInParent<Canvas>().rootCanvas;
        var area = (RectTransform)canvas.transform;
        Card.anchorMin = Card.anchorMax = Card.pivot = new Vector2(.5f, .5f);
        Card.anchoredPosition = Vector2.zero;
        Card.sizeDelta = new Vector2(Mathf.Min(1100, area.rect.width - 64), Mathf.Min(740, area.rect.height - 48));
        if (LegacySummary && Summary != null && Summary.text != previousSummary)
        {
            int end = Summary.text.IndexOf('\n');
            string first = end >= 0 ? Summary.text.Substring(0, end) : Summary.text;
            if (first.ToUpperInvariant().Contains("COMPLETE") || first.ToUpperInvariant().Contains("TIME"))
            { Heading.text = first.ToUpperInvariant().Contains("COMPLETE") ? "STAGE COMPLETE" : "TIME IS UP"; if (end >= 0) Summary.text = Summary.text.Substring(end + 1); }
            previousSummary = Summary.text;
        }
        Label(Stage, new Vector2(.07f,.79f), new Vector2(.93f,.90f), 24, 30, false);
        Label(Heading, new Vector2(.07f,.62f), new Vector2(.93f,.79f), 38, 64, false);
        Label(Summary, new Vector2(.09f,HasCoinReward ? .37f : .34f), new Vector2(.91f,.60f), 24, 32, true);
        bool extra = Extra != null && Extra.gameObject.activeSelf;
        bool primary = Primary != null && Primary.gameObject.activeSelf;
        bool menu = Menu != null && Menu.gameObject.activeSelf;
        float width = Mathf.Min(280, (Card.rect.width - 96) / 2);
        Action(Primary, primary && menu ? -width * .5f - 14 : 0, extra ? .24f : .18f, width);
        if (Menu != null) Menu.GetComponentInChildren<TMP_Text>(true).text = "MAIN MENU";
        Action(Menu, primary && menu ? width * .5f + 14 : 0, extra ? .24f : .18f, width);
        Action(Extra, 0, .085f, width);
    }
    public void Complete(int trial, UnityEngine.Events.UnityAction next = null)
    {
        HasCoinReward = true;
        TrialRewardDisplay.Attach(this, trial);
        navigating = false;
        Menu.gameObject.SetActive(true);
        if (Extra != null) Extra.gameObject.SetActive(false);
        if (Primary != null)
        {
            Primary.onClick.RemoveAllListeners();
            Primary.gameObject.SetActive(trial < TrialCatalog.Count);
            if (trial < TrialCatalog.Count)
            {
                Primary.GetComponentInChildren<TMP_Text>(true).text = "NEXT STAGE";
                Primary.onClick.AddListener(() =>
                {
                    if (navigating) return;
                    navigating = true;
                    if (next != null) next(); else StagePresentation.NextTrial(trial);
                });
            }
        }
        Layout();
    }
    static void Label(TMP_Text label, Vector2 min, Vector2 max, float low, float high, bool reading)
    {
        if (label == null) return;
        StageUITheme.Stretch(label.rectTransform, min, max);
        label.font = reading ? StageUITheme.ReadingFont : StageUITheme.HeadingFont;
        label.color = StageUITheme.ReadingInk; label.outlineWidth = 0; label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true; label.fontSizeMin = low; label.fontSizeMax = high;
        label.textWrappingMode = TextWrappingModes.Normal; label.raycastTarget = false;
    }
    static void Action(Button button, float x, float y, float width)
    {
        if (button == null) return;
        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, y);
        rect.anchoredPosition = new Vector2(x, 0); rect.sizeDelta = new Vector2(width, 88); rect.localScale = Vector3.one;
        var image = button.GetComponent<Image>(); if (image != null) image.pixelsPerUnitMultiplier = 4.5f;
        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) { label.text = label.text.ToUpperInvariant(); if (label.text == "RETRY" || label.text == "TRY AGAIN") label.text = "PLAY AGAIN"; StageUITheme.Stretch(label.rectTransform, Vector2.zero, Vector2.one); label.rectTransform.localScale = Vector3.one; label.rectTransform.offsetMin = new Vector2(24,10); label.rectTransform.offsetMax = new Vector2(-24,-10); }
    }
}
