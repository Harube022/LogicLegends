using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Text;

/// <summary>Full-canvas result overlay, with bounded matching actions and slim GraphHUD artwork.</summary>
[DefaultExecutionOrder(3100)]
public sealed class StageResultLayout : MonoBehaviour
{
    public RectTransform Card;
    public TMP_Text Stage, Heading, Summary;
    public Button Primary, Menu, Extra;
    public bool LegacySummary;
    public bool HasCoinReward;
    TMP_Text accuracyLabel, answerList;
    Button answersButton, closeAnswersButton;
    GameObject answersPanel;
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
        Card.sizeDelta = new Vector2(Mathf.Min(1100, area.rect.width - 64), Mathf.Min(840, area.rect.height - 48));
        if (LegacySummary && Summary != null && Summary.text != previousSummary)
        {
            int end = Summary.text.IndexOf('\n');
            string first = end >= 0 ? Summary.text.Substring(0, end) : Summary.text;
            if (first.ToUpperInvariant().Contains("COMPLETE") || first.ToUpperInvariant().Contains("TIME"))
            { Heading.text = first.ToUpperInvariant().Contains("COMPLETE") ? "STAGE COMPLETE" : "TIME IS UP"; if (end >= 0) Summary.text = Summary.text.Substring(end + 1); }
            previousSummary = Summary.text;
        }
        Label(Stage, new Vector2(.07f,.87f), new Vector2(.93f,.94f), 24, 30, false);
        Label(Heading, new Vector2(.07f,.74f), new Vector2(.93f,.87f), 32, 58, false);
        Label(Summary, new Vector2(.09f,HasCoinReward ? .59f : .34f), new Vector2(.91f,.73f), 24, 30, true);
        bool extra = Extra != null && Extra.gameObject.activeSelf;
        bool primary = Primary != null && Primary.gameObject.activeSelf;
        bool menu = Menu != null && Menu.gameObject.activeSelf;
        float width = Mathf.Min(280, (Card.rect.width - 96) / 2);
        Action(Primary, primary && menu ? -width * .5f - 14 : 0, extra ? .21f : .14f, width);
        if (Menu != null) Menu.GetComponentInChildren<TMP_Text>(true).text = "MAIN MENU";
        Action(Menu, primary && menu ? width * .5f + 14 : 0, extra ? .21f : .14f, width);
        Action(Extra, 0, .085f, Mathf.Min(340, Card.rect.width - 96));
    }
    public void Fail(int trial, UnityEngine.Events.UnityAction retry)
    {
        HasCoinReward = true; navigating = false;
        if (Extra == null) Extra = StageUITheme.Button("TryAgainOrStudy", Card, "", null);
        Primary.gameObject.SetActive(true); Menu.gameObject.SetActive(true); Extra.gameObject.SetActive(true);
        Primary.GetComponentInChildren<TMP_Text>(true).text = "START OVER";
        Primary.onClick.RemoveAllListeners();
        Primary.onClick.AddListener(() => { if (navigating) return; navigating = true; TrialRunFlow.StartOver(trial); });
        Extra.onClick.RemoveAllListeners();
        bool canRetry = TrialRewards.CanRetry(trial);
        Extra.GetComponentInChildren<TMP_Text>(true).text = canRetry ? "TRY AGAIN" : "STUDY IN LOGIC GARDEN LIBRARY";
        Extra.onClick.AddListener(() => {
            if (navigating) return;
            if (canRetry && !TrialRewards.CanRetry(trial)) return;
            navigating = true;
            if (canRetry) retry?.Invoke(); else TrialRunFlow.Study();
        });
        // Timeout screens offer retry/navigation only; reward reductions are not
        // completion results and are intentionally omitted here.
        foreach (var display in GetComponentsInChildren<TrialRewardDisplay>(true)) display.gameObject.SetActive(false);
        Layout();
    }
    public void Complete(int trial, UnityEngine.Events.UnityAction next = null)
    {
        HasCoinReward = true;
        TrialRewardDisplay.Attach(this, trial);
        BuildAccuracyAndAnswers(trial);
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
    void BuildAccuracyAndAnswers(int trial)
    {
        accuracyLabel = StageUITheme.Text("TrialAccuracy", Card, "Accuracy: " + TrialRewards.AccuracyPercent(trial) + "%", 30);
        StageUITheme.Stretch(accuracyLabel.rectTransform, new Vector2(.08f,.30f), new Vector2(.92f,.35f));
        accuracyLabel.font = StageUITheme.HeadingFont;
        accuracyLabel.color = new Color32(62, 39, 20, 255);
        accuracyLabel.fontSizeMin = 26; accuracyLabel.fontSizeMax = 32;
        var answersLabel = StageUITheme.Text("TrialAnswersLabel", Card, "Answers:", 28);
        StageUITheme.Stretch(answersLabel.rectTransform, new Vector2(.31f,.51f), new Vector2(.50f,.58f));
        answersLabel.font = StageUITheme.HeadingFont;
        answersLabel.color = StageUITheme.ReadingInk;
        answersLabel.alignment = TextAlignmentOptions.MidlineRight;
        answersLabel.fontSizeMin = 24; answersLabel.fontSizeMax = 30;
        answersButton = StageUITheme.Button("TrialAnswersShow", Card, "SHOW", OpenAnswers);
        StageUITheme.Stretch((RectTransform)answersButton.transform, new Vector2(.51f,.51f), new Vector2(.65f,.59f));
        var sprite = Resources.LoadAll<Sprite>("StageUI/WoodButtonGreen");
        if (sprite.Length > 0) answersButton.image.sprite = sprite[0];
        answersButton.image.pixelsPerUnitMultiplier = 4.5f;
        BuildAnswersPanel(trial);
    }
    void BuildAnswersPanel(int trial)
    {
        if (answersPanel != null) Destroy(answersPanel);
        answersPanel = new GameObject("TrialAnswersPanel", typeof(RectTransform), typeof(Image));
        answersPanel.transform.SetParent(transform, false);
        StageUITheme.Stretch((RectTransform)answersPanel.transform, Vector2.zero, Vector2.one);
        answersPanel.GetComponent<Image>().color = new Color(.01f,.025f,.035f,.96f);
        var card = StageUITheme.Rect("AnswersCard", answersPanel.transform);
        StageUITheme.Stretch(card, new Vector2(.06f,.06f), new Vector2(.94f,.94f));
        StagePresentation.Frame(card.gameObject.AddComponent<Image>(), true);
        var title = StageUITheme.Text("AnswersTitle", card, "TRIAL ANSWERS", 42);
        StageUITheme.Stretch(title.rectTransform, new Vector2(.06f,.86f), new Vector2(.94f,.96f));
        title.font = StageUITheme.HeadingFont;
        title.color = StageUITheme.ReadingInk;
        title.fontSizeMin = 36; title.fontSizeMax = 46;
        var viewport = StageUITheme.Rect("AnswersViewport", card);
        StageUITheme.Stretch(viewport, new Vector2(.08f,.18f), new Vector2(.90f,.84f));
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 42f;
        var maskImage = viewport.gameObject.AddComponent<Image>(); maskImage.color = new Color(1,1,1,.01f);
        maskImage.raycastTarget = true;
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        scroll.verticalScrollbar = BuildAnswersScrollbar(card);
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scroll.verticalScrollbarSpacing = 6f;
        var content = StageUITheme.Rect("AnswersContent", viewport);
        content.anchorMin = new Vector2(0,1); content.anchorMax = new Vector2(1,1); content.pivot = new Vector2(.5f,1);
        var answers = TrialRewards.Answers(trial);
        if (trial == 2 && answers.Length > 0)
        {
            for (int i = 0; i < answers.Length; i++) AddTruthTableReview(content, answers[i], i * 370f);
            content.sizeDelta = new Vector2(0, answers.Length * 370f + 12f);
        }
        else
        {
            var lines = new StringBuilder();
            foreach (var answer in answers)
            {
                lines.AppendLine("<b>" + EscapeRichText(answer.question ?? ("Question " + answer.questionId)) + "</b>");
                lines.AppendLine("<color=#16803C><b>Correct answer:</b> " + EscapeRichText(answer.correctAnswer ?? "—") + "</color>");
                lines.AppendLine(answer.correct
                    ? "<color=#16803C><b>Your answer:</b> " + EscapeRichText(answer.selectedAnswer ?? "—") + "</color>"
                    : "<color=#B42318><b>Your wrong answer:</b> " + EscapeRichText(answer.selectedAnswer ?? "—") + "</color>");
                lines.AppendLine();
            }
            answerList = StageUITheme.Text("AnswersText", content,
                answers.Length == 0 ? "No answers were recorded for this trial." : lines.ToString(), 24);
            answerList.richText = true;
            answerList.color = StageUITheme.ReadingInk;
            answerList.alignment = TextAlignmentOptions.TopLeft; answerList.fontSizeMin = 20; answerList.fontSizeMax = 24;
            answerList.enableAutoSizing = false; answerList.raycastTarget = false;
            answerList.textWrappingMode = TextWrappingModes.Normal;
            var textRect = answerList.rectTransform;
            textRect.anchorMin = new Vector2(0,1); textRect.anchorMax = new Vector2(1,1);
            textRect.pivot = new Vector2(.5f,1); textRect.anchoredPosition = new Vector2(0,-12);
            Canvas.ForceUpdateCanvases();
            float textWidth = viewport.rect.width > 24f ? viewport.rect.width - 24f : Screen.width * .7f;
            float textHeight = answerList.GetPreferredValues(answerList.text, textWidth, 100000f).y;
            float contentHeight = Mathf.Max(viewport.rect.height + 1f, textHeight + 24f);
            textRect.sizeDelta = new Vector2(-24, contentHeight - 24f);
            content.sizeDelta = new Vector2(0, contentHeight);
        }
        scroll.viewport = (RectTransform)viewport; scroll.content = content;
        scroll.verticalNormalizedPosition = 1f;
        Canvas.ForceUpdateCanvases();
        closeAnswersButton = StageUITheme.Button("CloseAnswers", card, "CLOSE", CloseAnswers);
        StageUITheme.Stretch((RectTransform)closeAnswersButton.transform, new Vector2(.34f,.055f), new Vector2(.66f,.16f));
        answersPanel.SetActive(false);
    }
    void OpenAnswers() { if (answersPanel != null) answersPanel.SetActive(true); }
    void CloseAnswers() { if (answersPanel != null) answersPanel.SetActive(false); }
    static Scrollbar BuildAnswersScrollbar(Transform parent)
    {
        var track = StageUITheme.Rect("AnswersScrollbar", parent);
        track.anchorMin = new Vector2(.915f,.18f); track.anchorMax = new Vector2(.915f,.84f);
        track.pivot = new Vector2(.5f,1); track.sizeDelta = new Vector2(36,0);
        var trackImage = track.gameObject.AddComponent<Image>();
        trackImage.sprite = StagePresentation.Art("MenuScrollbarTrack");
        trackImage.type = Image.Type.Sliced; trackImage.color = Color.white;
        trackImage.pixelsPerUnitMultiplier = 2.5f;
        trackImage.raycastPadding = new Vector4(-6,-4,-6,-4);
        var slidingArea = StageUITheme.Rect("SlidingArea", track);
        StageUITheme.Stretch(slidingArea, Vector2.zero, Vector2.one);
        slidingArea.offsetMin = new Vector2(6,6); slidingArea.offsetMax = new Vector2(-6,-6);
        var handle = StageUITheme.Rect("Handle", slidingArea);
        StageUITheme.Stretch(handle, Vector2.zero, Vector2.one);
        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.sprite = StagePresentation.Art("MenuScrollbarThumb");
        handleImage.type = Image.Type.Sliced; handleImage.color = Color.white;
        handleImage.pixelsPerUnitMultiplier = 3f;
        handleImage.raycastPadding = new Vector4(-4,-4,-4,-4);
        var scrollbar = track.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.transition = Selectable.Transition.None;
        scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
        scrollbar.value = 1f;
        return scrollbar;
    }
    static void AddTruthTableReview(Transform parent, TrialBackendClient.QuizAnswer answer, float top)
    {
        string[] questionLines = (answer.question ?? "Truth-table column").Split('\n');
        string title = questionLines.Length > 0 ? questionLines[0] : "Truth-table column";
        int expressionStart = title.LastIndexOf(':');
        string expression = expressionStart >= 0 ? title.Substring(expressionStart + 1).Trim() : answer.questionId;
        string[] correct = (answer.correctAnswer ?? "").Split(',');
        string[] selected = (answer.selectedAnswer ?? "").Split(',');
        string[] inputs = { "T", "T", "F", "F" };
        string[] qInputs = { "T", "F", "T", "F" };
        Color ink = StageUITheme.ReadingInk;
        Color green = new Color32(22, 128, 60, 255);
        Color red = new Color32(180, 35, 24, 255);

        AddAnswerCell(parent, EscapeRichText(title), .06f, .94f, top, 34, 25, ink, true);
        AddAnswerCell(parent, "Correct table", .06f, .94f, top + 35, 26, 22, ink, true);
        AddTruthTableHeader(parent, expression, top + 61, ink);
        for (int row = 0; row < 4; row++)
        {
            string expected = row < correct.Length ? correct[row].Trim() : "—";
            AddTruthTableRow(parent, inputs[row], qInputs[row], expected, top + 86 + row * 25, green);
        }

        AddAnswerCell(parent, "Player table", .06f, .94f, top + 190, 26, 22, ink, true);
        AddTruthTableHeader(parent, expression, top + 216, ink);
        for (int row = 0; row < 4; row++)
        {
            string expected = row < correct.Length ? correct[row].Trim() : "—";
            string actual = row < selected.Length ? selected[row].Trim() : "—";
            Color resultColor = string.Equals(expected, actual, System.StringComparison.OrdinalIgnoreCase) ? green : red;
            AddTruthTableRow(parent, inputs[row], qInputs[row], actual, top + 241 + row * 25, resultColor);
        }
    }
    static void AddTruthTableHeader(Transform parent, string expression, float top, Color ink)
    {
        AddAnswerCell(parent, "p", .12f, .27f, top, 24, 21, ink, true);
        AddAnswerCell(parent, "q", .35f, .50f, top, 24, 21, ink, true);
        AddAnswerCell(parent, EscapeRichText(expression), .56f, .91f, top, 24, 21, ink, true);
    }
    static void AddTruthTableRow(Transform parent, string p, string q, string result, float top, Color resultColor)
    {
        AddAnswerCell(parent, p, .12f, .27f, top, 24, 22, StageUITheme.ReadingInk, false);
        AddAnswerCell(parent, q, .35f, .50f, top, 24, 22, StageUITheme.ReadingInk, false);
        AddAnswerCell(parent, result, .56f, .91f, top, 24, 22, resultColor, true);
    }
    static TMP_Text AddAnswerCell(Transform parent, string text, float xMin, float xMax,
        float top, float height, float fontSize, Color color, bool bold)
    {
        var label = StageUITheme.Text("AnswerCell", parent, text, fontSize);
        var rect = label.rectTransform;
        rect.anchorMin = new Vector2(xMin, 1); rect.anchorMax = new Vector2(xMax, 1);
        rect.pivot = new Vector2(.5f, 1); rect.anchoredPosition = new Vector2(0, -top);
        rect.sizeDelta = new Vector2(0, height);
        label.font = bold ? StageUITheme.HeadingFont : StageUITheme.ReadingFont;
        label.color = color; label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableAutoSizing = false; label.fontSize = fontSize;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.raycastTarget = false;
        return label;
    }
    static string EscapeRichText(string value)
    {
        // TextMeshPro does not decode HTML entities; preserve operators such as '&' verbatim.
        return value ?? "";
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
        if (label != null) { label.text = label.text.ToUpperInvariant(); label.fontSizeMin = 24; label.fontSizeMax = 30; label.enableAutoSizing = true; label.textWrappingMode = TextWrappingModes.Normal; StageUITheme.Stretch(label.rectTransform, Vector2.zero, Vector2.one); label.rectTransform.localScale = Vector3.one; label.rectTransform.offsetMin = new Vector2(24,10); label.rectTransform.offsetMax = new Vector2(-24,-10); }
    }
}
