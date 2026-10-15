using System;
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
    bool boardInputBlocked, boardPlayerEnabled, boardInputHeld;
    GameObject boardControls;
    bool boardControlsVisible;
    Button adminButton;
    bool adminHidden, adminWasVisible;

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
        Hud.anchoredPosition = new Vector2(-24, -24); Hud.sizeDelta = new Vector2(500, 130);
        StageUITheme.CompactSurface(Hud.gameObject.AddComponent<Image>());
        var clock = Stage.ClockText;
        clock.transform.SetParent(Hud, false);
        StageUITheme.Stretch(clock.rectTransform, new Vector2(.06f, .16f), new Vector2(.39f, .84f));
        clock.font = StageUITheme.HeadingFont; clock.fontSize = 56; clock.enableAutoSizing = false; clock.color = StageUITheme.Gold;
        Progress = StageUITheme.Text("Proof Challenge Progress", Hud, "", 29);
        StageUITheme.Stretch(Progress.rectTransform, new Vector2(.4f, .16f), new Vector2(.95f, .84f));
        Progress.color = StageUITheme.Ink;
        ViewButton = StageUITheme.Button("Proofs View Board", SafeArea, "View Board", RequestBoard);
        Bottom((RectTransform)ViewButton.transform, new Vector2(0, 190), new Vector2(280, 88));
        InteractionButton = StageUITheme.Button("Proofs Interact", SafeArea, "Interact", Stage.InteractNearby);
        Bottom((RectTransform)InteractionButton.transform, new Vector2(0, 55), new Vector2(310, 88));
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
        bool practice = Stage.Tutorial != null;
        bool ordinary = !Stage.OnboardingPending && !practice && !Stage.Finished && !Stage.HasReaderOpen &&
            !BoardView.Viewing && !Help.IsOpen && GameInput.Instance != null && !GameInput.Instance.GameplayInputBlocked;
        ViewButton.gameObject.SetActive(ordinary || practice && Stage.Tutorial.WantsViewBoard);
        InteractionButton.gameObject.SetActive(ordinary && Stage.HasNearbyInteraction || practice && Stage.Tutorial.WantsInteraction);
        if (ordinary) InteractionButton.GetComponentInChildren<TMP_Text>().text = Stage.Started && Stage.PromptText.text.Contains("Confirm") ? "Confirm at Book" : "Interact";
        Hud.gameObject.SetActive(!Help.IsOpen && !Stage.Finished && !Stage.HasReaderOpen && !BoardView.Viewing);
        Progress.text = "Challenge " + Mathf.Clamp(Stage.ChallengeNumber, 1, Stage.ChallengeCount) + " of " + Stage.ChallengeCount;
        // Keep the existing Admin preference; temporarily hide its entry point
        // behind Help, practice and reading modals instead of drawing it over them.
        if (adminButton == null) adminButton = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(b => b.name == "AdminButton");
        bool hideAdmin = Stage.GameplayPaused || BoardView.Viewing || Stage.HasReaderOpen;
        if (adminButton != null && hideAdmin && !adminHidden)
        { adminWasVisible = adminButton.gameObject.activeSelf; adminHidden = true; adminButton.gameObject.SetActive(false); }
        else if (adminButton != null && !hideAdmin && adminHidden)
        { adminHidden = false; adminButton.gameObject.SetActive(adminWasVisible); }
    }
    public void RequestBoard()
    {
        if (Stage.Tutorial != null) { Stage.Tutorial.OpenPracticeBoard(); return; }
        if (Stage.OnboardingPending || Help.IsOpen || Stage.HasReaderOpen || Stage.Finished || BoardView.Viewing ||
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
        if (Stage != null && Stage.Tutorial != null) Stage.Tutorial.Cancel();
        if (BoardView != null) BoardView.RestoreImmediately(false);
        ReleaseBoardInput();
    }
    void SkinReader()
    {
        var panel = Stage.ReaderPanel.GetComponent<RectTransform>();
        StageUITheme.Stretch(panel, new Vector2(.12f, .10f), new Vector2(.88f, .90f));
        StageUITheme.CompactSurface(panel.GetComponent<Image>());
        var paper = StageUITheme.Rect("Proof Reading Parchment", panel);
        StageUITheme.Stretch(paper, new Vector2(.055f, .225f), new Vector2(.945f, .83f));
        var image = paper.gameObject.AddComponent<Image>(); image.sprite = StageUITheme.Paper; image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 1.5f; image.raycastTarget = false; paper.SetSiblingIndex(1);
        var title = Stage.ReaderTitle; title.font = StageUITheme.HeadingFont; title.fontSize = 42; title.color = StageUITheme.Gold;
        StageUITheme.Stretch(title.rectTransform, new Vector2(.06f, .85f), new Vector2(.94f, .95f));
        var viewport = StageUITheme.Rect("Proof Reading Viewport", panel);
        StageUITheme.Stretch(viewport, new Vector2(.12f, .26f), new Vector2(.88f, .72f));
        viewport.gameObject.AddComponent<Image>().color = Color.clear;
        viewport.gameObject.AddComponent<RectMask2D>();
        var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.scrollSensitivity = 40; scroll.viewport = viewport;
        var body = Stage.ReaderBody; body.transform.SetParent(viewport, false);
        body.rectTransform.anchorMin = new Vector2(0, 1); body.rectTransform.anchorMax = Vector2.one; body.rectTransform.pivot = new Vector2(.5f, 1);
        body.rectTransform.sizeDelta = Vector2.zero; body.rectTransform.anchoredPosition = Vector2.zero;
        body.font = StageUITheme.ReadingFont; body.enableAutoSizing = false; body.fontSize = 33; body.color = StageUITheme.ReadingInk;
        body.textWrappingMode = TextWrappingModes.Normal; body.alignment = TextAlignmentOptions.TopLeft;
        var fitter = body.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = body.rectTransform;
        foreach (var button in new[] { Stage.CollectButton, Stage.ReaderCloseButton }) StageUITheme.SkinButton(button);
        StageUITheme.Stretch((RectTransform)Stage.CollectButton.transform, new Vector2(.09f, .06f), new Vector2(.55f, .19f));
        StageUITheme.Stretch((RectTransform)Stage.ReaderCloseButton.transform, new Vector2(.63f, .06f), new Vector2(.91f, .19f));
    }
    void SkinTakeBack()
    {
        var panel = Stage.TakeBackPanel.GetComponent<RectTransform>();
        StageUITheme.CompactSurface(panel.GetComponent<Image>());
        foreach (var button in Stage.TakeBackPanel.GetComponentsInChildren<Button>(true)) StageUITheme.SkinButton(button);
    }
    void OnDisable() { CloseForResult(); }
}
