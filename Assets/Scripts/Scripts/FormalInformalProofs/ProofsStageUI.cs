using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>PRELIM chrome bound to the existing proof timer, reader and book interactions.</summary>
[DisallowMultipleComponent]
public sealed class ProofsStageUI : MonoBehaviour
{
    public ProofsLibraryStageController Stage { get; private set; }
    public ProofsHelpUI Help { get; private set; }
    public ProofsBoardView BoardView { get; private set; }
    public RectTransform SafeArea { get; private set; }
    public RectTransform Hud { get; private set; }
    public TMP_Text Progress { get; private set; }
    public Button ViewButton { get; private set; }
    public Button BackButton { get; private set; }
    public Button InteractionButton { get; private set; }
    public Button ReturnScrollButton { get; private set; }
    bool boardInputBlocked, boardPlayerEnabled, boardInputHeld;
    GameObject boardControls;
    bool boardControlsVisible;
    Button adminButton;
    bool adminHidden, adminWasVisible;
    RectTransform readerOverlay, readerPaper, readerViewport;
    ScrollRect readerScroll;
    TMP_Text readerScrollHint;
    readonly Dictionary<RectTransform, TouchLayout> portraitControls = new Dictionary<RectTransform, TouchLayout>();
    Transform touchControls;
    struct TouchLayout
    {
        public Vector2 min, max, pivot, position;
        public Vector3 scale;
        public TouchLayout(RectTransform rect)
        { min = rect.anchorMin; max = rect.anchorMax; pivot = rect.pivot; position = rect.anchoredPosition; scale = rect.localScale; }
        public void Restore(RectTransform rect)
        { rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot; rect.anchoredPosition = position; rect.localScale = scale; }
    }

    public void Initialize(ProofsLibraryStageController owner)
    {
        Stage = owner;
        var canvas = owner.ClockText.GetComponentInParent<Canvas>();
        canvas.sortingOrder = 210;
        SafeArea = StageUITheme.Rect("Proofs SafeArea", canvas.transform);
        StageUITheme.Stretch(SafeArea, Vector2.zero, Vector2.one);
        SafeArea.gameObject.AddComponent<Crystal.SafeArea>();
        Hud = StageUITheme.Rect("Proofs Compact HUD", SafeArea);
        Hud.anchorMin = Hud.anchorMax = Hud.pivot = Vector2.one;
        Hud.anchoredPosition = new Vector2(-24, -24); Hud.sizeDelta = new Vector2(500, 136);
        StageUITheme.CompactSurface(Hud.gameObject.AddComponent<Image>());
        var title=StageUITheme.Text("CurrentStageLabel",Hud,TrialCatalog.ForScene("Formal vs. Informal Proofs").Title,26);
        StageUITheme.Stretch(title.rectTransform,new Vector2(.06f,.62f),new Vector2(.94f,.86f));
        title.enableAutoSizing=true;title.fontSizeMin=22;title.fontSizeMax=26;
        var clock = Stage.ClockText;
        clock.transform.SetParent(Hud, false);
        StageUITheme.Stretch(clock.rectTransform, new Vector2(.06f, .18f), new Vector2(.31f, .57f));
        clock.font = StageUITheme.HeadingFont; clock.fontSize = 40; clock.enableAutoSizing = false; clock.color = StageUITheme.Gold;
        Progress = StageUITheme.Text("Proof Challenge Progress", Hud, "", 29);
        StageUITheme.Stretch(Progress.rectTransform, new Vector2(.32f, .08f), new Vector2(.94f, .6f));
        Progress.enableAutoSizing=true;Progress.fontSizeMin=22;Progress.fontSizeMax=24;
        Progress.color = StageUITheme.Ink;
        StagePresentation.Hud(Hud, title, clock, Progress);
        ViewButton = StageUITheme.Button("Proofs View Board", SafeArea, "View Board", RequestBoard);
        ViewButton.image.pixelsPerUnitMultiplier = 4.5f;
        Bottom((RectTransform)ViewButton.transform, new Vector2(0, 190), new Vector2(280, 88));
        InteractionButton = StageUITheme.Button("Proofs Interact", SafeArea, "Interact", Stage.InteractNearby);
        InteractionButton.image.pixelsPerUnitMultiplier = 4.5f;
        Bottom((RectTransform)InteractionButton.transform, new Vector2(0, 55), new Vector2(310, 88));
        ReturnScrollButton = StageUITheme.Button("Proofs Return Scroll", SafeArea, "Return Scroll", Stage.ReturnScroll);
        ReturnScrollButton.image.pixelsPerUnitMultiplier = 4.5f;
        Bottom((RectTransform)ReturnScrollButton.transform, new Vector2(0, 55), new Vector2(280, 88));
        ReturnScrollButton.gameObject.SetActive(false);
        BackButton = StageUITheme.Button("Proofs Board Back", SafeArea, "Back", null);
        Bottom((RectTransform)BackButton.transform, new Vector2(0, 28), new Vector2(240, 88));
        BackButton.gameObject.SetActive(false);
        BoardView = gameObject.AddComponent<ProofsBoardView>();
        BoardView.informationBoard = Stage.BoardTarget; BoardView.boardInstructions = Stage.BoardInstructions;
        BoardView.viewSafeArea = SafeArea; BoardView.backButton = BackButton;
        var text = Stage.BoardInstructions;
        text.textWrappingMode = TextWrappingModes.Normal; text.enableAutoSizing = true; text.fontSizeMin = 2.2f; text.fontSizeMax = 3;
        Stage.PromptText.font = StageUITheme.HeadingFont; Stage.PromptText.fontSize = 30;
        Stage.PromptText.enableAutoSizing = true; Stage.PromptText.fontSizeMin = 25; Stage.PromptText.fontSizeMax = 30;
        Stage.PromptText.transform.SetParent(SafeArea, false);
        Bottom(Stage.PromptText.rectTransform, new Vector2(0, 310), new Vector2(1000, 72));
        SkinReader();
        SkinTakeBack();
        Help = gameObject.AddComponent<ProofsHelpUI>(); Help.Initialize(owner, this);
    }
    public static void Bottom(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 0);
        rect.anchoredPosition = position; rect.sizeDelta = size;
    }
    void Update()
    {
        if (Stage == null || SafeArea == null) return;
        LayoutPortraitControls();
        RefreshReader();
        bool practice = Stage.Tutorial != null;
        bool ordinary = !Stage.GameplayPaused && !practice && !Stage.Finished && !Stage.HasReaderOpen &&
            !BoardView.Viewing && !Help.IsOpen && GameInput.Instance != null && !GameInput.Instance.GameplayInputBlocked;
        ViewButton.gameObject.SetActive(ordinary || practice && Stage.Tutorial.WantsViewBoard);
        bool carrying = ordinary && Stage.Started && Stage.HasCarriedScroll;
        ReturnScrollButton.gameObject.SetActive(carrying);
        bool place = carrying && Stage.HasNearbyPlacement;
        InteractionButton.gameObject.SetActive(ordinary && (!carrying || place) && Stage.HasNearbyInteraction && !Stage.IsBookTargeted || practice && Stage.Tutorial.WantsInteraction);
        if (ordinary) InteractionButton.GetComponentInChildren<TMP_Text>().text =
            place ? "Place Scroll" : Stage.Started && Stage.PromptText.text.Contains("Confirm") ? "Confirm at Book" : "Interact";
        if (!practice)
        {
            // Use the central gap between touch controls; retain the hand and dedicated placement action.
            Bottom((RectTransform)ViewButton.transform, new Vector2(0, place ? 300 : 190), new Vector2(280, 88));
            Bottom((RectTransform)InteractionButton.transform, new Vector2(0, place ? 190 : 55), new Vector2(280, 88));
            float promptWidth = Screen.width < Screen.height ? SafeArea.rect.width * .55f : Mathf.Min(1000, SafeArea.rect.width - 48);
            Bottom(Stage.PromptText.rectTransform, new Vector2(0, place ? 430 : 310), new Vector2(promptWidth, 72));
        }
        Hud.gameObject.SetActive(!Help.IsOpen && !Stage.Finished && !Stage.HasReaderOpen && !BoardView.Viewing && Stage.Introductions?.IsOpen != true);
        Progress.text = "Challenge " + Mathf.Clamp(Stage.ChallengeNumber, 1, Stage.ChallengeCount) + " of " + Stage.ChallengeCount +
            "\nQuestion " + Stage.QuestionNumber + " of " + ProofsLibraryStageController.QuestionsPerChallenge;
        // Keep the existing Admin preference; temporarily hide its entry point
        // behind Help, practice and reading modals instead of drawing it over them.
        if (adminButton == null) adminButton = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(b => b.name == "AdminButton");
        bool hideAdmin = Stage.GameplayPaused || BoardView.Viewing || Stage.HasReaderOpen;
        if (adminButton != null && hideAdmin && !adminHidden)
        { adminWasVisible = adminButton.gameObject.activeSelf; adminHidden = true; adminButton.gameObject.SetActive(false); }
        else if (adminButton != null && !hideAdmin && adminHidden)
        { adminHidden = false; adminButton.gameObject.SetActive(adminWasVisible); }
    }
    void LayoutPortraitControls()
    {
        if (Screen.width >= Screen.height) { RestorePortraitControls(); return; }
        if (touchControls == null)
        {
            var canvas = GameObject.Find("Canvas 1");
            touchControls = canvas != null ? canvas.transform.Find("SafeArea/Gameplay_Interface") : null;
        }
        if (touchControls == null) return;
        // The shared controls use landscape offsets. Fit this stage's portrait controls
        // in screen space, leaving the central action column clear; restore on exit.
        var corners = new Vector3[4]; SafeArea.GetWorldCorners(corners);
        var lower = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
        var upper = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
        float width = upper.x - lower.x;
        FitTouch((RectTransform)touchControls.Find("Joystick"), lower + new Vector2(width * .17f, width * .15f), width * .28f);
        FitTouch((RectTransform)touchControls.Find("InteractButton"), new Vector2(upper.x - width * .12f, lower.y + width * .14f), width * .18f);
        FitTouch((RectTransform)touchControls.Find("JumpButton"), new Vector2(upper.x - width * .12f, lower.y + width * .35f), width * .18f);
        FitTouch((RectTransform)touchControls.Find("SettingsButton"), new Vector2(lower.x + width * .075f, upper.y - width * .075f), width * .095f);
        if (Help != null && Help.HelpButton != null)
            FitTouch((RectTransform)Help.HelpButton.transform, new Vector2(lower.x + width * .24f, upper.y - width * .075f), width * .16f);
    }
    void FitTouch(RectTransform rect, Vector2 center, float diameter)
    {
        if (rect == null || !(rect.parent is RectTransform parent)) return;
        if (!portraitControls.ContainsKey(rect)) portraitControls.Add(rect, new TouchLayout(rect));
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        float scale = diameter / Mathf.Max(rect.rect.width * parent.lossyScale.x, rect.rect.height * parent.lossyScale.y);
        rect.localScale = new Vector3(scale, scale, 1);
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, center, null, out Vector3 position)) rect.position = position;
    }
    void RestorePortraitControls()
    {
        foreach (var pair in portraitControls) if (pair.Key != null) pair.Value.Restore(pair.Key);
        portraitControls.Clear();
    }
    public void RequestBoard()
    {
        if (Stage.Tutorial != null) { Stage.Tutorial.OpenPracticeBoard(); return; }
        if (Stage.GameplayPaused || Help.IsOpen || Stage.HasReaderOpen || Stage.Finished || BoardView.Viewing ||
            GameInput.Instance == null || GameInput.Instance.GameplayInputBlocked) return;
        if (!BoardView.OpenView(ReleaseBoardInput)) return;
        HoldBoardInput();
    }
    void HoldBoardInput()
    {
        boardInputHeld = true; boardInputBlocked = GameInput.Instance.GameplayInputBlocked;
        boardPlayerEnabled = Player.LocalInstance != null && Player.LocalInstance.enabled;
        var controlsCanvas = GameObject.Find("Canvas 1");
        boardControls = controlsCanvas != null ? controlsCanvas.transform.Find("SafeArea/Gameplay_Interface")?.gameObject : null;
        if (boardControls != null) { boardControlsVisible = boardControls.activeSelf; boardControls.SetActive(false); }
        GameInput.Instance.SetGameplayInputBlocked(true); Player.LocalInstance?.ToggleControl(false);
    }
    void ReleaseBoardInput()
    {
        if (!boardInputHeld) return; boardInputHeld = false;
        if (boardControls != null) boardControls.SetActive(boardControlsVisible); boardControls = null;
        if (GameInput.Instance != null) GameInput.Instance.SetGameplayInputBlocked(boardInputBlocked);
        if (Player.LocalInstance != null && Stage != null && !Stage.Finished) Player.LocalInstance.ToggleControl(boardPlayerEnabled);
    }
    public void CloseForResult()
    {
        if (Help != null) Help.CloseHelp();
        Stage?.Introductions?.Cancel();
        if (Stage != null && Stage.Tutorial != null) Stage.Tutorial.Cancel();
        if (BoardView != null) BoardView.RestoreImmediately(false);
        ReleaseBoardInput();
    }
    void SkinReader()
    {
        readerOverlay = StageUITheme.Rect("Proof Scroll Reading Overlay", SafeArea);
        StageUITheme.Stretch(readerOverlay, Vector2.zero, Vector2.one);
        var dimmer = readerOverlay.gameObject.AddComponent<Image>(); dimmer.color = new Color(0, .015f, .025f, .65f);
        dimmer.raycastTarget = true;
        var panel = Stage.ReaderPanel.GetComponent<RectTransform>();
        panel.SetParent(readerOverlay, false);
        StageUITheme.CompactSurface(panel.GetComponent<Image>());
        readerPaper = StageUITheme.Rect("Proof Reading Parchment", panel);
        var image = readerPaper.gameObject.AddComponent<Image>(); image.sprite = StageUITheme.Paper; image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 1.5f; image.raycastTarget = false; readerPaper.SetSiblingIndex(1);
        var title = Stage.ReaderTitle; title.font = StageUITheme.HeadingFont; title.fontSize = 42; title.color = StageUITheme.Gold;
        title.enableAutoSizing = true; title.fontSizeMin = 26; title.fontSizeMax = 40;
        title.textWrappingMode = TextWrappingModes.Normal; title.raycastTarget = false;
        title.rectTransform.anchorMin = new Vector2(0, 1); title.rectTransform.anchorMax = Vector2.one;
        title.rectTransform.pivot = new Vector2(.5f, 1); title.rectTransform.sizeDelta = new Vector2(-64, 68);
        title.rectTransform.anchoredPosition = new Vector2(0, -16);
        readerViewport = StageUITheme.Rect("Proof Reading Viewport", panel);
        readerViewport.gameObject.AddComponent<Image>().color = Color.clear;
        readerViewport.gameObject.AddComponent<RectMask2D>();
        readerScroll = readerViewport.gameObject.AddComponent<ScrollRect>(); readerScroll.horizontal = false;
        readerScroll.scrollSensitivity = 40; readerScroll.viewport = readerViewport; readerScroll.movementType = ScrollRect.MovementType.Clamped;
        var body = Stage.ReaderBody; body.transform.SetParent(readerViewport, false);
        body.rectTransform.anchorMin = new Vector2(0, 1); body.rectTransform.anchorMax = Vector2.one; body.rectTransform.pivot = new Vector2(.5f, 1);
        body.rectTransform.sizeDelta = Vector2.zero; body.rectTransform.anchoredPosition = Vector2.zero;
        body.font = StageUITheme.ReadingFont; body.enableAutoSizing = false; body.fontSize = 33; body.color = StageUITheme.ReadingInk;
        body.textWrappingMode = TextWrappingModes.Normal; body.alignment = TextAlignmentOptions.TopLeft;
        body.raycastTarget = false; body.margin = new Vector4(0, 0, 12, 16);
        var fitter = body.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        readerScroll.content = body.rectTransform;
        readerScrollHint = StageUITheme.Text("Proof Scroll Hint", panel, "Swipe or scroll to read more", 22, true);
        readerScrollHint.color = StageUITheme.ReadingInk; readerScrollHint.raycastTarget = false;
        foreach (var button in new[] { Stage.CollectButton, Stage.ReaderCloseButton })
        {
            StageUITheme.SkinButton(button); button.image.pixelsPerUnitMultiplier = 4.5f;
            button.GetComponentInChildren<TMP_Text>(true).text = button == Stage.CollectButton ? "Get This Scroll" : "Back";
        }
        LayoutReader(false); readerOverlay.gameObject.SetActive(false);
    }
    public void LayoutReader(bool practice)
    {
        var panel = (RectTransform)Stage.ReaderPanel.transform;
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f); panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(Mathf.Min(1100, SafeArea.rect.width * .9f), Mathf.Min(850, SafeArea.rect.height * (practice ? .58f : .86f)));
        bool stacked = panel.sizeDelta.x < 660;
        float footer = stacked ? 220 : 116;
        StageUITheme.Stretch(readerPaper, Vector2.zero, Vector2.one);
        readerPaper.offsetMin = new Vector2(24, footer + 8); readerPaper.offsetMax = new Vector2(-24, -92);
        StageUITheme.Stretch(readerViewport, Vector2.zero, Vector2.one);
        readerViewport.offsetMin = new Vector2(58, footer + 58); readerViewport.offsetMax = new Vector2(-58, -184);
        readerScrollHint.rectTransform.anchorMin = Vector2.zero; readerScrollHint.rectTransform.anchorMax = new Vector2(1, 0);
        readerScrollHint.rectTransform.pivot = new Vector2(.5f, 0);
        readerScrollHint.rectTransform.sizeDelta = new Vector2(-116, 28);
        readerScrollHint.rectTransform.anchoredPosition = new Vector2(0, footer + 18);
        float width = Mathf.Min(280, panel.sizeDelta.x - 48);
        Bottom((RectTransform)Stage.CollectButton.transform, new Vector2(stacked ? 0 : -152, stacked ? 120 : 20), new Vector2(width, 88));
        Bottom((RectTransform)Stage.ReaderCloseButton.transform, new Vector2(stacked ? 0 : 152, 20), new Vector2(width, 88));
    }
    public void RefreshReader(bool resetScroll = false)
    {
        if (readerOverlay == null) return;
        bool visible = Stage.ReaderPanel.activeSelf;
        if (readerOverlay.gameObject.activeSelf != visible)
        {
            readerOverlay.gameObject.SetActive(visible);
            if (visible) readerOverlay.SetAsLastSibling();
        }
        if (!visible) return;
        LayoutReader(Stage.Tutorial?.CurrentStep == ProofsTutorialStep.Reader);
        if (resetScroll)
        {
            Stage.CollectButton.interactable = true;
            Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(readerScroll.content);
            readerScroll.StopMovement(); readerScroll.verticalNormalizedPosition = 1;
            readerScroll.content.anchoredPosition = Vector2.zero;
        }
        readerScrollHint.gameObject.SetActive(readerScroll.content.rect.height > readerViewport.rect.height + 1);
    }
    void SkinTakeBack()
    {
        var panel = Stage.TakeBackPanel.GetComponent<RectTransform>();
        StageUITheme.CompactSurface(panel.GetComponent<Image>());
        foreach (var button in Stage.TakeBackPanel.GetComponentsInChildren<Button>(true)) StageUITheme.SkinButton(button);
    }
    void OnDisable() { CloseForResult(); RestorePortraitControls(); }
}
