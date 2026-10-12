using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Cinemachine;

/// <summary>Scene-only, paged field guide for the five Setssss challenges.</summary>
[DisallowMultipleComponent]
public sealed class SetssssHelpUI : MonoBehaviour
{
    [SerializeField] private bool showOnFirstEntry = true;

    private struct Page
    {
        public readonly string title;
        public readonly string body;
        public Page(string title, string body) { this.title = title; this.body = body; }
    }

    private static readonly Page[] Pages =
    {
        new Page("Begin at the book",
            "Approach the book statue and tap the hand button or Interact with Book to begin Challenge 1. Closing this guide does not start the challenge or its timer. After a challenge is complete, return to the book to start the next one."),
        new Page("Move and interact",
            "Use the joystick to move and the jump button to jump. Face a nearby object and tap the hand button to interact; the E key also works. Interact with Book and Pick Up Block use that same interaction. Near the spawner, tap View Board to read the condition, then Back to return."),
        new Page("Collect numbered blocks",
            "In Challenges 1–4, the spawner offers 10 numbered blocks from 1–99. The numbers needed for the board are included; the others are distractors. Face a block and tap the hand button or Pick Up Block. Carry it to the diagram, face the region, and tap the hand button to place it. Return Block sends a held block back."),
        new Page("Challenge 1 — Single set",
            "Read the number on Board 1 and place it in the named set region. Most conditions show A; one condition shows B. Complete all 10 conditions. Leave the other numbered blocks at the spawner."),
        new Page("Challenge 2 — Sets A and B",
            "Read both sets on the board. Place every A element in the A-only region and every B element in the B-only region. These questions have no shared elements, so leave A ∩ B empty. Complete all 10 conditions."),
        new Page("Challenge 3 — Intersection",
            "Compare A and B. Place values found only in A in A, values found only in B in B, and shared values once in A ∩ B. Unused blocks stay at the spawner. Complete all 10 conditions."),
        new Page("Challenge 4 — Set differences",
            "Fill all five answer areas. A and B each need their full listed set. Put shared values in A ∩ B, values only in A in A − B, and values only in B in B − A. A value can belong in several areas. Every pickup from the spawner leaves a replacement with the same number, so you can collect another copy. Complete all 10 conditions."),
        new Page("Challenge 5 — Complement",
            "BORDERLINES marks the universal set U. Only the current A or B circle is active. Reposition the preplaced numbers: members of the named set go inside its circle; every other U element stays outside that circle but inside U. Each number appears once, with no replacement copies. Complete all 5 conditions."),
        new Page("Submit, fix, and try again",
            "Near the diagram, tap Submit to check every required placement. A wrong submission costs 10 seconds and keeps the same condition. Placed blocks stay put; red marks show misplaced blocks, and a message warns about missing elements without giving away their locations. Move only what needs fixing, then submit again."),
        new Page("Time and progress",
            "Each challenge has its own 18-minute countdown. It starts only when you activate the book and waits at 18:00 before then. Conditions are shuffled at challenge start: 10 each in Challenges 1–4, and 5 in Challenge 5. Correct submissions advance; finish every condition to complete the challenge. Time reaching zero ends the attempt.")
    };

    private RectTransform safeArea, card;
    private Button helpButton, previousButton, nextButton, beginButton;
    private TextMeshProUGUI titleText, bodyText, pageText;
    private ScrollRect bodyScroll;
    private GameObject overlay;
    private SetssssChallengeOne challenge;
    private SetssssBoardView boardView;
    private GameInput input;
    private Player player;
    private readonly List<Behaviour> pausedCameraInputs = new List<Behaviour>();
    private bool open, shownOnEntry, onboardingPending, inputWasBlocked, playerWasEnabled, cursorWasVisible;
    private CursorLockMode previousCursorLock;
    private float previousTimeScale;
    private int pageIndex;

    private void Awake()
    {
        if (SceneManager.GetActiveScene().name != "Setssss") enabled = false;
    }

    private IEnumerator Start()
    {
        if (!enabled) yield break;
        safeArea = transform.Find("SafeArea") as RectTransform;
        if (safeArea == null) { Debug.LogError("Setssss How to Play needs Canvas 1/SafeArea.", this); yield break; }
        challenge = FindFirstObjectByType<SetssssChallengeOne>(FindObjectsInactive.Include);
        boardView = FindFirstObjectByType<SetssssBoardView>(FindObjectsInactive.Include);
        BuildUI();
        if (!showOnFirstEntry || shownOnEntry) yield break;
        while (Player.LocalInstance == null || GameInput.Instance == null ||
               Time.timeScale <= 0f || GameInput.Instance.GameplayInputBlocked)
            yield return null;
        shownOnEntry = true;
        if (challenge != null && !challenge.Started && !challenge.Complete && !challenge.IsGameOver)
        {
            onboardingPending = true;
            OpenHelp();
        }
    }

    private void BuildUI()
    {
        Transform settings = safeArea.Find("Gameplay_Interface/SettingsButton");
        Transform buttonParent = settings != null ? settings.parent : safeArea;
        helpButton = StageUITheme.Button("SetssssHelpButton", buttonParent, "HELP", OpenHelp);
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

        RectTransform root = StageUITheme.Rect("Setssss How To Play Overlay", safeArea);
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
            "SETS  /  HOW TO PLAY", 32);
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
            bool visible = !open && (challenge == null || !challenge.Complete && !challenge.IsGameOver) &&
                           (boardView == null || !boardView.IsViewingBoard) &&
                           (SetssssControlsOnboarding.Active == null || !SetssssControlsOnboarding.Active.IsActive);
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
        if (open || overlay == null || Time.timeScale <= 0f ||
            SetssssControlsOnboarding.Active != null && SetssssControlsOnboarding.Active.IsActive ||
            challenge != null && (challenge.Complete || challenge.IsGameOver) ||
            boardView != null && boardView.IsViewingBoard) return;
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
        if (!onboardingPending) return;
        onboardingPending = false;
        SetssssControlsOnboarding onboarding = GetComponent<SetssssControlsOnboarding>();
        if (onboarding == null) onboarding = gameObject.AddComponent<SetssssControlsOnboarding>();
        onboarding.Begin(helpButton);
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

    private void OnDisable()
    {
        CloseHelp();
    }
}
