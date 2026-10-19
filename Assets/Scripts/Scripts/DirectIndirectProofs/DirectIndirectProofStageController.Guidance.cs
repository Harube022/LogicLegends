using UnityEngine;
using TMPro;
using UnityEngine.UI;

public sealed partial class DirectIndirectProofStageController
{
    public const int QuestionsPerChallenge = 2;
    readonly int[,] selectedVariants = new int[5, QuestionsPerChallenge];
    int questionIndex;
    RectTransform problemViewport;
    public int QuestionNumber => questionIndex + 1;
    public DirectProofsGuide Guide { get; private set; }
    public bool OnboardingPending { get; private set; } = true;
    public bool GuidanceBlocksGameplay => OnboardingPending || Guide != null && (Guide.IsBusy || Guide.NeedsIntroduction);
    public Transform Book => bookStatue;
    public Transform InformationBoard => boardText.transform;
    public Transform[] Altars => new[] { directAltar, indirectAltar, neutralAltar };
    public ProofWorldPedestal PracticeTemplate => methodPedestals[0];
    public TMP_Text Clock => timerText;
    public TMP_Text Progress => progressText;
    public TMP_Text Prompt => promptText;
    public TMP_Text Selection => selectionText;
    public ProofAltarOrderingBoard OrderingBoard => altarBoard;
    public float StartingSeconds => startingSeconds;
    public float WrongPenalty => wrongPenaltySeconds;
    public bool Finished => state == TrialState.GameOver || state == TrialState.StageComplete;
    public int[] SelectedVariantIndices(int challenge) => new[] { selectedVariants[challenge, 0], selectedVariants[challenge, 1] };
    public void CompleteOnboarding() { OnboardingPending = false; }
    public void ConfigureGuidePresentation(RectTransform safe)
    {
        timerText.font = progressText.font = StageUITheme.HeadingFont;
        promptText.transform.SetParent(safe, false); selectionText.transform.SetParent(safe, false);
        foreach (var text in new[] { promptText, selectionText })
        {
            text.font = StageUITheme.HeadingFont; text.enableAutoSizing = true; text.fontSizeMin = 24; text.fontSizeMax = 30;
            text.textWrappingMode = TextWrappingModes.Normal; text.raycastTarget = false;
        }
        readerPanel.transform.SetParent(safe, false); StageUITheme.CompactSurface(readerPanel.GetComponent<Image>());
        readerTitle.font = StageUITheme.HeadingFont; readerTitle.enableAutoSizing = true; readerTitle.fontSizeMin = 26; readerTitle.fontSizeMax = 40;
        readerTitle.color = StageUITheme.Gold;
        problemViewport = StageUITheme.Rect("Full Problem Viewport", readerPanel.transform); StageUITheme.Stretch(problemViewport, Vector2.zero, Vector2.one);
        problemViewport.gameObject.AddComponent<Image>().color = Color.clear; problemViewport.gameObject.AddComponent<RectMask2D>();
        var scroll = readerPanel.AddComponent<ScrollRect>(); scroll.viewport = problemViewport; scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 40;
        readerBody.transform.SetParent(problemViewport, false); readerBody.font = StageUITheme.ReadingFont;
        readerBody.fontSize = 32; readerBody.enableAutoSizing = false; readerBody.color = StageUITheme.Ink;
        readerBody.alignment = TextAlignmentOptions.TopLeft; readerBody.textWrappingMode = TextWrappingModes.Normal; readerBody.raycastTarget = false;
        readerBody.rectTransform.anchorMin = new Vector2(0, 1); readerBody.rectTransform.anchorMax = Vector2.one;
        readerBody.rectTransform.pivot = new Vector2(.5f, 1); readerBody.rectTransform.sizeDelta = Vector2.zero; readerBody.rectTransform.anchoredPosition = Vector2.zero;
        readerBody.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize; scroll.content = readerBody.rectTransform;
        foreach (var button in new[] { selectTabletButton, closeReaderButton }) { StageUITheme.SkinButton(button); button.image.pixelsPerUnitMultiplier = 4.5f; }
    }
    public void LayoutGuidePresentation(RectTransform safe)
    {
        var size = safe.rect.size;
        foreach (var text in new[] { promptText, selectionText })
        {
            var r = text.rectTransform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, 1);
            r.sizeDelta = new Vector2(Mathf.Min(1040, size.x - 48), text == promptText ? 84 : 68);
            r.anchoredPosition = new Vector2(0, text == promptText ? -210 : -310);
        }
        var panel = (RectTransform)readerPanel.transform; panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
        panel.anchoredPosition = Vector2.zero; panel.sizeDelta = new Vector2(Mathf.Min(1140, size.x - 48), Mathf.Min(850, size.y * .86f));
        readerTitle.rectTransform.anchorMin = new Vector2(0, 1); readerTitle.rectTransform.anchorMax = Vector2.one;
        readerTitle.rectTransform.pivot = new Vector2(.5f, 1); readerTitle.rectTransform.anchoredPosition = new Vector2(0, -18); readerTitle.rectTransform.sizeDelta = new Vector2(-64, 76);
        bool stacked = panel.sizeDelta.x < 680 && selectTabletButton.gameObject.activeSelf;
        problemViewport.offsetMin = new Vector2(44, stacked ? 240 : 140); problemViewport.offsetMax = new Vector2(-44, -112);
        ProofsStageUI.Bottom((RectTransform)closeReaderButton.transform, new Vector2(selectTabletButton.gameObject.activeSelf && !stacked ? 152 : 0, 24), new Vector2(280, 88));
        ProofsStageUI.Bottom((RectTransform)selectTabletButton.transform, new Vector2(stacked ? 0 : -152, stacked ? 130 : 24), new Vector2(280, 88));
    }
    void BuildQuestionSchedule()
    {
        for (int c = 0; c < 5; c++)
        {
            int count = catalog.variantSets[c].variants.Length;
            int first = Random.Range(0, count), second = Random.Range(0, count - 1);
            if (second >= first) second++;
            selectedVariants[c, 0] = first; selectedVariants[c, 1] = second;
        }
    }
    void RefreshQuestionCounter()
    { progressText.text = "Challenge " + Mathf.Clamp(ChallengeNumber, 1, 5) + " of 5\nQuestion " + QuestionNumber + " of 2"; }
}
