using System.Collections;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Cinemachine;

/// <summary>PRELIM's paged parchment guide; pauses and restores the existing stage.</summary>
[DisallowMultipleComponent]
public sealed class ProofsHelpUI : MonoBehaviour
{
    [SerializeField] private bool showOnFirstEntry = true;

    private struct Page
    {
        public readonly string title;
        public readonly string body;
        public Page(string title, string body) { this.title = title; this.body = body; }
    }

    private Page[] Pages;

    private RectTransform safeArea, card;
    private Button helpButton, previousButton, nextButton, beginButton;
    private TextMeshProUGUI titleText, bodyText, pageText;
    private ScrollRect bodyScroll;
    private GameObject overlay;
    private ProofsLibraryStageController stage;
    private ProofsStageUI ui;
    private ProofsBoardView boardView;
    private GameInput input;
    private Player player;
    private readonly List<Behaviour> pausedCameraInputs = new List<Behaviour>();
    private bool open, shownOnEntry, inputWasBlocked, playerWasEnabled, cursorWasVisible;
    public bool IsOpen => open;
    public Button HelpButton => helpButton;
    public int PageCount => Pages.Length;
    public void Initialize(ProofsLibraryStageController owner, ProofsStageUI presentation) { stage = owner; ui = presentation; }
    private CursorLockMode previousCursorLock;
    private float previousTimeScale;
    private int pageIndex;

    private IEnumerator Start()
    {
        if (stage == null) yield break;
        safeArea = ui.SafeArea;
        if (safeArea == null) { Debug.LogError("Proofs Library Help needs the existing Proofs HUD safe area.", this); yield break; }
        boardView = ui.BoardView;
        Pages = new[] {
            new Page("Formal and informal proofs", "A proof explains why a conclusion follows from accepted premises or established facts. Formal proofs use explicit mathematical statements and justified logical steps. Informal proofs communicate valid reasoning through explanatory prose. Informal does not mean incorrect: both styles can be valid."),
            new Page("Begin at the book", "Complete five challenges, with one question selected from each bank of ten variants. Activate the Book Statue to start a challenge. Read the Information Board, examine the available scrolls or tablets, build your answer, then return to the book to confirm the entire arrangement. Finishing Help or practice never starts a real challenge."),
            new Page("Move, interact, and read", "Use the joystick to move and the jump button to jump. Use the hand or nearby Interact button to interact. View Board brings the Information Board closer; Back restores the gameplay camera. At a scroll stand, interact to read its full proof. Carry Scroll selects and collects that scroll; Close leaves it at the stand."),
            new Page("Challenges 1 and 2 — Proof styles", "Read every supplied proof and decide how its reasoning is presented. Place formal statements with explicit justified steps on the Formal Proof altar. Place valid explanatory prose on the Informal Proof altar. Challenge 1 uses two scrolls; Challenge 2 uses four. Classify every scroll before confirming at the book."),
            new Page("Place and correct scrolls", "Carry one scroll at a time. Interact with an altar to place it; placement alone does not grade your answer. With empty hands, interact with an occupied altar to open Take Back. Choose a scroll to carry it again, or Cancel to leave the arrangement unchanged. You can move a scroll to the other altar before confirming."),
            new Page("Challenge 3 — Arrange proof steps", "Premises are the accepted starting statements. A conclusion is what you must establish. Select a statement tablet, then interact with a numbered STEP pedestal to place it. Put supporting facts before the steps that use them, and finish with the conclusion. Interact with a filled slot to remove a tablet; placing a new tablet can replace its contents. Confirm at the book."),
            new Page("Challenge 4 — Missing justification", "A justification explains why a proof step is allowed, such as a premise or a valid rule of inference. Read the proof and choose the RULE tablet that correctly supports the missing step. Place it on RULE SLOT 1, then confirm at the Book Statue. The available rules are choices, not hints about the correct answer."),
            new Page("Challenge 5 — Formalize reasoning", "Read the informal explanation and translate it into an ordered formal proof. Place each statement on the appropriate numbered STEP pedestal and put its matching justification on the corresponding RULE pedestal. Complete both sequences, including the conclusion, before confirming the whole proof at the book."),
            new Page("Shared timer and feedback", "The book starts or resumes one shared " + StageJourneyUI.FormatTime(stage.StartingSeconds) + " countdown. It does not reset between challenges. Reading scrolls or using View Board during an active challenge keeps the countdown running. Help and guided practice pause time. Incorrect or incomplete confirmations show error feedback, subtract " + stage.WrongPenalty.ToString("0.##") + " seconds, and preserve your work. Correct answers show success and pause time; activate the book again to continue."),
            new Page("Ready to practice?", "Complete all five challenges to reach the stage results. Time up offers a stage retry or Menu. Let's Begin teaches the controls, book, altars, reading, collection, placement, and board view with clearly labeled practice content. Yes at the final confirmation prepares Challenge 1; it still waits for the real book interaction.")
        };
        BuildUI();
        if (!showOnFirstEntry || shownOnEntry) yield break;
        while (Player.LocalInstance == null || GameInput.Instance == null || Time.timeScale <= 0f || GameInput.Instance.GameplayInputBlocked ||
               !Player.LocalInstance.GetComponent<CharacterController>().enabled)
            yield return null;
        shownOnEntry = true;
        if (stage.OnboardingPending) OpenHelp();
    }
    private void BuildUI()
    {
        var controls = GameObject.Find("Canvas 1");
        Transform settings = controls != null ? controls.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == "SettingsButton") : null;
        Transform buttonParent = settings != null ? settings.parent : safeArea;
        helpButton = StageUITheme.Button("ProofsHelpButton", buttonParent, "HELP", OpenHelp);
        RectTransform helpRect = (RectTransform)helpButton.transform;
        if (settings is RectTransform settingsRect)
        {
            helpRect.anchorMin = settingsRect.anchorMin;
            helpRect.anchorMax = settingsRect.anchorMax;
            helpRect.pivot = settingsRect.pivot;
            helpRect.anchoredPosition = settingsRect.anchoredPosition + new Vector2(settingsRect.rect.width + 24f, 0f);
            helpRect.sizeDelta = new Vector2(160f, settingsRect.rect.height);
        }
        else
        {
            helpRect.anchorMin = helpRect.anchorMax = new Vector2(0f, 1f);
            helpRect.pivot = new Vector2(0f, 1f);
            helpRect.anchoredPosition = new Vector2(200f, -25f);
            helpRect.sizeDelta = new Vector2(160f, 110f);
        }

        RectTransform root = StageUITheme.Rect("Proofs How To Play Overlay", safeArea);
        StageUITheme.Stretch(root, Vector2.zero, Vector2.one);
        Image dimmer = root.gameObject.AddComponent<Image>();
        dimmer.color = new Color(0.012f, 0.025f, 0.03f, 0.88f);
        dimmer.raycastTarget = true;
        overlay = root.gameObject;

        card = StageUITheme.Rect("TutorialCard", root);
        StageUITheme.Stretch(card, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f));
        Image frame = card.gameObject.AddComponent<Image>();
        frame.sprite = StageUITheme.Panel;
        frame.type = Image.Type.Sliced;
        frame.pixelsPerUnitMultiplier = 1.7f;
        frame.color = Color.white;

        RectTransform parchment = StageUITheme.Rect("ReadingPage", card);
        StageUITheme.Stretch(parchment, new Vector2(0.055f, 0.225f), new Vector2(0.945f, 0.835f));
        Image paper = parchment.gameObject.AddComponent<Image>();
        paper.sprite = StageUITheme.Paper;
        paper.type = Image.Type.Sliced;
        paper.pixelsPerUnitMultiplier = 1.5f;
        paper.color = Color.white;
        paper.raycastTarget = false;

        TextMeshProUGUI header = (TextMeshProUGUI)StageUITheme.Text("SectionLabel", card,
            "PROOFS LIBRARY  /  HOW TO PLAY", 32);
        StageUITheme.Stretch(header.rectTransform, new Vector2(0.08f, 0.865f), new Vector2(0.92f, 0.93f));
        header.font = StageUITheme.HeadingFont;
        header.color = StageUITheme.Gold;

        titleText = (TextMeshProUGUI)StageUITheme.Text("PageTitle", card, "", 46);
        StageUITheme.Stretch(titleText.rectTransform, new Vector2(0.105f, 0.65f), new Vector2(0.895f, 0.72f));
        titleText.font = StageUITheme.HeadingFont;
        titleText.color = StageUITheme.ReadingInk;
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 31f;
        titleText.fontSizeMax = 46f;

        RectTransform viewport = StageUITheme.Rect("PageViewport", card);
        StageUITheme.Stretch(viewport, new Vector2(0.12f, 0.255f), new Vector2(0.88f, 0.60f));
        Image scrollHitArea = viewport.gameObject.AddComponent<Image>();
        scrollHitArea.color = Color.clear;
        scrollHitArea.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();
        bodyScroll = viewport.gameObject.AddComponent<ScrollRect>();
        bodyScroll.viewport = viewport;
        bodyScroll.horizontal = false;
        bodyScroll.vertical = true;
        bodyScroll.movementType = ScrollRect.MovementType.Clamped;
        RectTransform content = StageUITheme.Rect("PageContent", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = content.offsetMax = Vector2.zero;
        bodyText = content.gameObject.AddComponent<TextMeshProUGUI>();
        bodyText.font = StageUITheme.ReadingFont;
        bodyText.fontSize = 34f;
        bodyText.color = StageUITheme.ReadingInk;
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        bodyText.raycastTarget = false;
        bodyText.margin = new Vector4(0f, 0f, 14f, 10f);
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        bodyScroll.content = content;

        previousButton = StageUITheme.Button("PreviousHelpPage", card, "PREVIOUS", Previous);
        nextButton = StageUITheme.Button("NextHelpPage", card, "NEXT", Next);
        beginButton = StageUITheme.Button("BeginButton", card, "LET'S BEGIN", BeginFromHelp);
        StageUITheme.Stretch((RectTransform)previousButton.transform, new Vector2(0.08f, 0.075f), new Vector2(0.29f, 0.185f));
        StageUITheme.Stretch((RectTransform)nextButton.transform, new Vector2(0.31f, 0.075f), new Vector2(0.49f, 0.185f));
        StageUITheme.Stretch((RectTransform)beginButton.transform, new Vector2(0.65f, 0.075f), new Vector2(0.92f, 0.185f));
        pageText = (TextMeshProUGUI)StageUITheme.Text("PageIndicator", card, "", 29);
        StageUITheme.Stretch(pageText.rectTransform, new Vector2(0.51f, 0.075f), new Vector2(0.63f, 0.185f));
        pageText.font = StageUITheme.HeadingFont;
        pageText.color = StageUITheme.Gold;
        overlay.SetActive(false);
    }

    private void Update()
    {
        if (helpButton != null)
        {
            bool visible = !open && stage != null && stage.Tutorial == null && !stage.HasReaderOpen && !ui.BoardView.Viewing &&
                (boardView == null || !boardView.Viewing) && !stage.Finished;
            if (helpButton.gameObject.activeSelf != visible) helpButton.gameObject.SetActive(visible);
        }
        if (card != null && safeArea != null)
        {
            float aspect = safeArea.rect.width / Mathf.Max(1f, safeArea.rect.height);
            Vector2 min = aspect < 1.2f ? new Vector2(0.035f, 0.045f) : new Vector2(0.08f, 0.08f);
            Vector2 max = aspect < 1.2f ? new Vector2(0.965f, 0.955f) : new Vector2(0.92f, 0.92f);
            if (card.anchorMin != min || card.anchorMax != max) StageUITheme.Stretch(card, min, max);
        }
    }

    public void OpenHelp()
    {
        if (open || overlay == null || stage == null || Time.timeScale <= 0f || stage.Tutorial != null ||
            ui.BoardView.Viewing || boardView != null && boardView.Viewing ||
            stage.Finished) return;
        input = GameInput.Instance != null ? GameInput.Instance : FindFirstObjectByType<GameInput>();
        if (input == null || input.GameplayInputBlocked) return;
        player = Player.LocalInstance != null ? Player.LocalInstance : FindFirstObjectByType<Player>();
        inputWasBlocked = input.GameplayInputBlocked;
        playerWasEnabled = player != null && player.enabled;
        previousTimeScale = Time.timeScale;
        previousCursorLock = Cursor.lockState;
        cursorWasVisible = Cursor.visible;
        pausedCameraInputs.Clear();
        foreach (CinemachineInputAxisController cameraInput in FindObjectsByType<CinemachineInputAxisController>(FindObjectsSortMode.None))
            PauseCameraInput(cameraInput);
        foreach (CinemachinePinchZoom pinch in FindObjectsByType<CinemachinePinchZoom>(FindObjectsSortMode.None))
            PauseCameraInput(pinch);
        foreach (ThirdPersonCameraController cameraInput in FindObjectsByType<ThirdPersonCameraController>(FindObjectsSortMode.None))
            PauseCameraInput(cameraInput);
        input.SetGameplayInputBlocked(true);
        if (player != null) player.enabled = false;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        open = true;
        pageIndex = 0;
        ShowPage();
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
    }

    private void PauseCameraInput(Behaviour behaviour)
    {
        if (behaviour == null || !behaviour.enabled) return;
        pausedCameraInputs.Add(behaviour);
        behaviour.enabled = false;
    }

    public void CloseHelp()
    {
        if (!open) return;
        open = false;
        if (overlay != null) overlay.SetActive(false);
        Time.timeScale = previousTimeScale;
        if (input != null) input.SetGameplayInputBlocked(inputWasBlocked);
        if (player != null) player.enabled = playerWasEnabled;
        foreach (Behaviour cameraInput in pausedCameraInputs)
            if (cameraInput != null) cameraInput.enabled = true;
        pausedCameraInputs.Clear();
        Cursor.lockState = previousCursorLock;
        Cursor.visible = cursorWasVisible;
    }

    private void BeginFromHelp()
    {
        if (!open) return;
        CloseHelp();
        if (!stage.OnboardingPending) return;
        ProofsControlsOnboarding onboarding = GetComponent<ProofsControlsOnboarding>();
        if (onboarding == null) onboarding = gameObject.AddComponent<ProofsControlsOnboarding>();
        onboarding.Begin(stage, this);
    }

    private void Previous() { if (pageIndex > 0) { pageIndex--; ShowPage(); } }
    private void Next() { if (pageIndex < Pages.Length - 1) { pageIndex++; ShowPage(); } }

    private void ShowPage()
    {
        titleText.text = Pages[pageIndex].title;
        bodyText.text = Pages[pageIndex].body;
        pageText.text = (pageIndex + 1) + " / " + Pages.Length;
        previousButton.interactable = pageIndex > 0;
        nextButton.interactable = pageIndex < Pages.Length - 1;
        SetDisabledOpacity(previousButton);
        SetDisabledOpacity(nextButton);
        Canvas.ForceUpdateCanvases();
        bodyScroll.verticalNormalizedPosition = 1f;
    }

    private static void SetDisabledOpacity(Button button)
    {
        CanvasGroup group = button.GetComponent<CanvasGroup>();
        if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();
        group.alpha = button.interactable ? 1f : 0.55f;
    }

    private void OnDisable() { CloseHelp(); }
    private void OnDestroy()
    {
        CloseHelp();
        if (overlay != null) Destroy(overlay);
        if (helpButton != null) Destroy(helpButton.gameObject);
    }
}

