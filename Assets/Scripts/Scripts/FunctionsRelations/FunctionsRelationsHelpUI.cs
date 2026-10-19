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
public sealed class FunctionsRelationsHelpUI : MonoBehaviour
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
    private FunctionsRelationsStageManager stage;
    private FunctionsRelationsBoardView boardView;
    private GameInput input;
    private Player player;
    private readonly List<Behaviour> pausedCameraInputs = new List<Behaviour>();
    private bool open, shownOnEntry, inputWasBlocked, playerWasEnabled, cursorWasVisible;
    public bool IsOpen => open;
    public Button HelpButton => helpButton;
    public int PageCount => Pages.Length;
    public void Initialize(FunctionsRelationsStageManager owner) { stage = owner; }
    private CursorLockMode previousCursorLock;
    private float previousTimeScale;
    private int pageIndex;

    private IEnumerator Start()
    {
        if (stage == null) yield break;
        safeArea = transform.Find("SafeArea") as RectTransform;
        if (safeArea == null) { Debug.LogError("Functions and Relations Help needs FR_GameplayCanvas/SafeArea.", this); yield break; }
        boardView = stage.BoardView;
        Pages = new[] {
            new Page("Functions and relations", "A relation is a set of input–output pairs. A function gives every input exactly one output; different inputs can still share an output. The domain contains the inputs, the codomain contains all allowed outputs, and the range contains the outputs actually used."),
            new Page("Begin at the book", "Complete five challenges. Approach Book_Statue and use the hand button to start each one. Use View Board to read, then Back to return. Closing Help or finishing practice does not start a challenge. Each challenge selects one of ten variants; these are alternatives, not ten rounds you must complete."),
            new Page("Move, select, and connect", "Use the joystick to move and the jump button to jump. At Domain, use the hand or nearby interaction button to open the input choices. Select an input, go to the Range/output station, and choose an output. A blue laser shows the connection. Repeat for every pair required on the InformationBoard."),
            new Page("Correct and submit", "The dedicated Submit button checks the entire current arrangement. At the output station, Undo removes the last editable connection; fixed connections cannot be removed. Select an input and output to rebuild it. Incorrect submissions preserve your connections for correction."),
            new Page("Challenges 1 and 2 — Relations", "Challenge 1 asks you to build the relation described on the board. Challenge 2 gives a set of ordered pairs. Match every required pair, with no extra connections. A relation can give one input more than one output; do not impose the function rule on every relation."),
            new Page("Challenge 3 — Complete a function", "One preset connection is fixed. Read the board's order clue to complete a one-to-one and onto function. Every input must have exactly one output, different inputs use different outputs, and every available output is used. Submit checks both the connections and these requirements."),
            new Page("Challenge 4 — Identify the diagram", "Trace the preset lasers from each input. Every valid diagram here is a relation. If each input has exactly one output, use the game's Function Only pedestal; otherwise use Relation Only. Select the matching answer pedestal, then press Submit."),
            new Page("Challenge 5 — Properties", "Inspect the preset diagram and toggle every applicable property pedestal, then Submit. Injective means one-to-one: different inputs do not share an output. Surjective means onto: every codomain value is reached. Bijective means both. Tapping a property again deselects it."),
            new Page("Shared timer and feedback", "You have one shared " + StageJourneyUI.FormatTime(stage.StartingSeconds) + " countdown. The book starts or resumes it. A correct submission shows green success feedback, pauses time, and opens the next challenge; return to the book to continue. The timer does not reset between challenges. Incorrect submissions show red/error feedback and subtract " + stage.WrongAnswerPenalty.ToString("0.##") + " seconds. Time up offers a stage retry; finishing all five opens the results/menu UI."),
            new Page("Ready to practice?", "Help pauses gameplay input and time, then restores them when it closes. Let's Begin leads through controls and a clearly labeled practice mapping before Challenge 1. Practice uses separate data, so it cannot change real connections, progress, penalties, or the timer.")
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
        helpButton = StageUITheme.Button("FunctionsRelationsHelpButton", buttonParent, "HELP", OpenHelp);
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

        RectTransform root = StageUITheme.Rect("FunctionsRelations How To Play Overlay", safeArea);
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
            "FUNCTIONS & RELATIONS  /  HOW TO PLAY", 32);
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
            bool visible = !open && stage != null && stage.Tutorial == null && !stage.IsBoardOpen &&
                (boardView == null || !boardView.Viewing) && stage.State != FunctionsRelationsState.STAGE_COMPLETE && stage.State != FunctionsRelationsState.GAME_OVER;
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
            stage.IsBoardOpen || boardView != null && boardView.Viewing ||
            stage.State == FunctionsRelationsState.STAGE_COMPLETE || stage.State == FunctionsRelationsState.GAME_OVER) return;
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
        FunctionsRelationsControlsOnboarding onboarding = GetComponent<FunctionsRelationsControlsOnboarding>();
        if (onboarding == null) onboarding = gameObject.AddComponent<FunctionsRelationsControlsOnboarding>();
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

