using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One-time control practice between Truth Table Help and its first book start.</summary>
[DefaultExecutionOrder(1100)]
public sealed class TruthTableControlsOnboarding : MonoBehaviour
{
    private enum Step
    {
        Movement, Jump, Interaction, Book, Timer, Minimap, Progress,
        BoardSlots, BoardSubmit, House, Door, DoorQuiz, DoorQuizFeedback, DoorOpened, Blocks, Confirm
    }

    [SerializeField, Range(0.15f, 0.8f)] private float joystickDeadZone = 0.3f;
    [SerializeField, Min(0.1f)] private float movementDistance = 0.35f;
    [SerializeField, Min(1f)] private float practiceRange = 2.5f;
    [Header("Guided camera")]
    [SerializeField, Min(0.1f)] private float cameraTransitionSeconds = 0.7f;
    [SerializeField, Min(2f)] private float bookViewDistance = 8.5f;
    [SerializeField, Min(5f)] private float boardViewDistance = 22f;
    [SerializeField, Min(4f)] private float houseViewDistance = 22f;
    [SerializeField, Range(0, 5)] private int tutorialHouseIndex = 3;

    public static TruthTableControlsOnboarding Active { get; private set; }
    public bool IsActive { get; private set; }
    public bool AllowsJump => IsActive && step == Step.Jump;
    public bool SuppressMovement => IsActive && step >= Step.Book;
    public bool IsDemonstratingBook => IsActive && step == Step.Book;
    public bool WantsDedicatedInteractionPrompt => false;
    public bool IsPracticeQuizOpen => IsActive && (step == Step.DoorQuiz || step == Step.DoorQuizFeedback);

    private Step step;
    private TruthTableStageClock clock;
    private TruthTableDoorInteraction doors;
    private DynamicLogicPuzzle puzzle;
    private GameInput input;
    private Player player;
    private MobileInputUI joystickInput;
    private Button helpButton;
    private PrelimTutorialSkipControl skipControl;
    private RectTransform safeArea;
    private RectTransform joystick;
    private RectTransform jumpButton;
    private RectTransform handButton;
    private RectTransform highlight;
    private RectTransform arrow;
    private RectTransform worldHighlight;
    private readonly List<Renderer> worldTargets = new List<Renderer>();
    private readonly List<Transform> worldPoints = new List<Transform>();
    private readonly List<RectTransform> uiGroupTargets = new List<RectTransform>();
    private RectTransform instructionTarget;
    private bool instructionBottom;
    private RectTransform[] instructionBlockers;
    private RectTransform instructions;
    private TextMeshProUGUI instructionText;
    private RectTransform nextButton;
    private GameObject practiceQuiz;
    private RectTransform practiceQuizCard;
    private RectTransform practiceQuizTitle;
    private TextMeshProUGUI practiceQuizFeedback;
    private Button[] practiceAnswerButtons;
    private Button practiceQuizContinue;
    private RectTransform[] practiceOptionRects;
    private GameObject confirmation;
    private RectTransform confirmationCard;
    private RectTransform confirmationQuestion;
    private RectTransform yesButton;
    private RectTransform noButton;
    private Vector3 movementStart;
    private float jumpStartY;
    private bool jumpRequested;
    private bool previousInputBlocked;
    private RectTransform minimapPanel;
    private TextMeshProUGUI timerLabel;
    private TextMeshProUGUI challengeLabel;
    private TextMeshProUGUI columnLabel;
    private bool timerWasVisible;
    private bool challengeWasVisible;
    private bool columnWasVisible;
    private string timerOriginalText;
    private string challengeOriginalText;
    private string columnOriginalText;
    private TMP_Text[] boardHeaders;
    private string[] originalBoardHeaders;
    private Transform book;
    private Transform board;
    private Collider houseDoor;
    private CinemachineCamera demonstrationCamera;
    private Vector3 cameraGoal;
    private Quaternion cameraRotationGoal;
    private CinemachinePinchZoom pinchZoom;
    private ThirdPersonCameraController cameraController;
    private bool previousPinchEnabled;
    private bool previousCameraEnabled;
    private Renderer[] playerRenderers;
    private bool[] playerRendererStates;
    private bool originalJoystickVisible;
    private bool originalJumpVisible;
    private bool originalHandVisible;
    private bool demonstrationPrepared;

    public void Begin(Button existingHelpButton)
    {
        if (IsActive || StageSelectionState.SelectedStage != 2) return;
        clock = GetComponent<TruthTableStageClock>();
        doors = GetComponent<TruthTableDoorInteraction>();
        input = GameInput.Instance != null ? GameInput.Instance : FindFirstObjectByType<GameInput>();
        player = Player.LocalInstance;
        helpButton = existingHelpButton;
        if (clock == null || !clock.IsWaitingForBook || doors == null || input == null || player == null || !BuildUI())
        {
            Debug.LogError("Truth Table controls tutorial needs the waiting stage, player, GameInput, and PRELIM Canvas controls.", this);
            Cleanup();
            return;
        }

        previousInputBlocked = input.GameplayInputBlocked;
        if (!SnapshotHudAndBoard())
        {
            Debug.LogError("Truth Table tutorial could not find its book, board, house door, minimap, or progress HUD.", this);
            Cleanup();
            return;
        }
        Active = this;
        IsActive = true;
        clock.SetTutorialOpen(true);
        input.SetGameplayInputBlocked(false);
        if (helpButton != null) helpButton.gameObject.SetActive(false);
        BeginMovement();
    }

    private bool SnapshotHudAndBoard()
    {
        book = doors.TutorialBook;
        board = transform.root.Find("TRUTIBOL");
        houseDoor = doors.GetTutorialDoor(tutorialHouseIndex);
        minimapPanel = doors.MinimapPanel;
        RectTransform hud = doors.UiHost != null ? doors.UiHost.Find("TruthTableHud") as RectTransform : null;
        timerLabel = hud != null ? hud.Find("TruthTableCountdown")?.GetComponent<TextMeshProUGUI>() : null;
        challengeLabel = hud != null ? hud.Find("TruthTableChallengeProgress")?.GetComponent<TextMeshProUGUI>() : null;
        columnLabel = hud != null ? hud.Find("TruthTableColumnProgress")?.GetComponent<TextMeshProUGUI>() : null;
        if (book == null || board == null || houseDoor == null || minimapPanel == null ||
            timerLabel == null || challengeLabel == null || columnLabel == null) return false;

        timerWasVisible = timerLabel.gameObject.activeSelf;
        challengeWasVisible = challengeLabel.gameObject.activeSelf;
        columnWasVisible = columnLabel.gameObject.activeSelf;
        timerOriginalText = timerLabel.text;
        challengeOriginalText = challengeLabel.text;
        columnOriginalText = columnLabel.text;
        timerLabel.gameObject.SetActive(false);
        challengeLabel.gameObject.SetActive(false);
        columnLabel.gameObject.SetActive(false);

        boardHeaders = board.GetComponentsInChildren<TMP_Text>(true);
        originalBoardHeaders = new string[boardHeaders.Length];
        for (int i = 0; i < boardHeaders.Length; i++)
            originalBoardHeaders[i] = boardHeaders[i].text;
        foreach (DynamicLogicPuzzle candidate in transform.root.GetComponentsInChildren<DynamicLogicPuzzle>(true))
        {
            Transform[] slots = candidate.GetColumnSnapPoints(0);
            if (slots != null && slots.Length == 4) { puzzle = candidate; break; }
        }
        return puzzle != null;
    }

    private void RestoreHudAndBoard()
    {
        if (timerLabel != null)
        {
            timerLabel.text = timerOriginalText;
            timerLabel.gameObject.SetActive(timerWasVisible);
        }
        if (challengeLabel != null)
        {
            challengeLabel.text = challengeOriginalText;
            challengeLabel.gameObject.SetActive(challengeWasVisible);
        }
        if (columnLabel != null)
        {
            columnLabel.text = columnOriginalText;
            columnLabel.gameObject.SetActive(columnWasVisible);
        }
        RestoreBoardHeaders();
    }

    private void RestoreBoardHeaders()
    {
        if (boardHeaders == null || originalBoardHeaders == null) return;
        for (int i = 0; i < Mathf.Min(boardHeaders.Length, originalBoardHeaders.Length); i++)
            if (boardHeaders[i] != null) boardHeaders[i].text = originalBoardHeaders[i];
    }

    private bool BuildUI()
    {
        Canvas canvas = GameObject.Find("Canvas 1")?.GetComponent<Canvas>();
        safeArea = canvas != null ? canvas.transform.Find("SafeArea") as RectTransform : null;
        // Bind the actual Canvas joystick; the root button handler has the same type.
        Transform joystickHost = safeArea != null ? safeArea.Find("Gameplay_Interface/Joystick") : null;
        joystickInput = joystickHost != null ? joystickHost.GetComponentInChildren<MobileInputUI>(true) : null;
        joystick = joystickInput != null ? joystickInput.transform as RectTransform : null;
        jumpButton = safeArea != null ? safeArea.Find("Gameplay_Interface/JumpButton") as RectTransform : null;
        handButton = safeArea != null ? safeArea.Find("Gameplay_Interface/InteractButton") as RectTransform : null;
        if (safeArea == null || joystick == null || jumpButton == null || handButton == null) return false;

        instructions = MakeRect("Truth Table Controls Instructions", safeArea);
        instructions.anchorMin = new Vector2(0.08f, 1f);
        instructions.anchorMax = new Vector2(0.92f, 1f);
        instructions.pivot = new Vector2(0.5f, 1f);
        instructions.anchoredPosition = new Vector2(0f, -28f);
        instructions.sizeDelta = new Vector2(0f, 150f);
        Image backing = instructions.gameObject.AddComponent<Image>();
        backing.color = new Color(0.06f, 0.13f, 0.19f, 0.94f);
        backing.raycastTarget = false;
        instructionText = MakeText("Instruction", instructions, 38);
        Stretch(instructionText.rectTransform, 20f);
        nextButton = MakeButton(instructions, "Next", 0f, AdvanceExplanation);
        nextButton.anchorMin = nextButton.anchorMax = Vector2.zero;
        nextButton.pivot = Vector2.zero;
        nextButton.sizeDelta = new Vector2(210f, 70f);
        nextButton.gameObject.SetActive(false);

        highlight = MakeRect("Truth Table Control Highlight", safeArea);
        AddBorder(highlight);
        TextMeshProUGUI arrowText = MakeText("Arrow", highlight, 72);
        arrowText.text = "▼";
        arrowText.color = new Color(1f, 0.9f, 0.25f);
        arrow = arrowText.rectTransform;
        arrow.anchorMin = arrow.anchorMax = new Vector2(0.5f, 1f);
        arrow.pivot = new Vector2(0.5f, 0f);
        arrow.anchoredPosition = new Vector2(0f, 15f);
        arrow.sizeDelta = new Vector2(110f, 90f);

        worldHighlight = MakeRect("Truth Table World Highlight", safeArea);
        AddBorder(worldHighlight);
        TextMeshProUGUI worldArrow = MakeText("World Arrow", worldHighlight, 72f);
        worldArrow.text = "▼";
        worldArrow.color = new Color(1f, 0.9f, 0.25f);
        worldArrow.rectTransform.anchorMin = worldArrow.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        worldArrow.rectTransform.pivot = new Vector2(0.5f, 0f);
        worldArrow.rectTransform.anchoredPosition = new Vector2(0f, 12f);
        worldArrow.rectTransform.sizeDelta = new Vector2(110f, 90f);
        worldHighlight.gameObject.SetActive(false);

        RectTransform shade = MakeRect("Truth Table Controls Confirm", safeArea);
        Stretch(shade, 0f);
        Image shadeImage = shade.gameObject.AddComponent<Image>();
        shadeImage.color = new Color(0f, 0f, 0f, 0.6f);
        confirmation = shade.gameObject;
        RectTransform card = MakeRect("Confirmation Card", shade);
        confirmationCard = card;
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(660f, 280f);
        Image cardImage = card.gameObject.AddComponent<Image>();
        cardImage.color = new Color(0.06f, 0.16f, 0.22f, 0.98f);
        TextMeshProUGUI question = MakeText("Ready Question", card, 48);
        confirmationQuestion = question.rectTransform;
        question.text = "Let's start?";
        question.rectTransform.anchorMin = question.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        question.rectTransform.anchoredPosition = new Vector2(0f, -62f);
        question.rectTransform.sizeDelta = new Vector2(590f, 84f);
        yesButton = MakeButton(card, "Yes", -155f, ConfirmYes);
        noButton = MakeButton(card, "No", 155f, ConfirmNo);
        confirmation.SetActive(false);

        BuildPracticeQuiz();
        skipControl = PrelimTutorialSkipControl.Create(safeArea, SkipTutorial);
        return true;
    }

    private void BuildPracticeQuiz()
    {
        RectTransform shade = MakeRect("Truth Table Practice Quiz", safeArea);
        Stretch(shade, 0f);
        Image shadeImage = shade.gameObject.AddComponent<Image>();
        shadeImage.color = new Color(0f, 0f, 0f, 0.72f);
        practiceQuiz = shade.gameObject;
        practiceQuizCard = MakeRect("Practice Quiz Card", shade);
        practiceQuizCard.sizeDelta = new Vector2(850f, 560f);
        Image cardImage = practiceQuizCard.gameObject.AddComponent<Image>();
        cardImage.color = new Color(0.06f, 0.16f, 0.22f, 0.98f);
        TextMeshProUGUI title = MakeText("Practice Quiz Title", practiceQuizCard, 46f);
        practiceQuizTitle = title.rectTransform;
        title.text = "Practice: If P is True, what is ~P?";
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -62f);
        title.rectTransform.sizeDelta = new Vector2(760f, 84f);
        practiceQuizFeedback = MakeText("Practice Quiz Prompt", practiceQuizCard, 34f);
        practiceQuizFeedback.text = "Choose an answer. Practice does not use real time.";
        practiceQuizFeedback.rectTransform.anchorMin = practiceQuizFeedback.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        practiceQuizFeedback.rectTransform.anchoredPosition = new Vector2(0f, -155f);
        practiceQuizFeedback.rectTransform.sizeDelta = new Vector2(760f, 110f);
        practiceAnswerButtons = new Button[4];
        practiceOptionRects = new RectTransform[4];
        for (int i = 0; i < practiceAnswerButtons.Length; i++)
        {
            int answerIndex = i;
            RectTransform option = MakeButton(practiceQuizCard, "Choice " + (char)('A' + i), 0f,
                () => AnswerPracticeQuiz(answerIndex));
            option.sizeDelta = new Vector2(315f, 94f);
            option.anchoredPosition = new Vector2(i % 2 == 0 ? -175f : 175f,
                i < 2 ? -10f : -120f);
            practiceAnswerButtons[i] = option.GetComponent<Button>();
            practiceAnswerButtons[i].GetComponentInChildren<TMP_Text>().text = new[]{"A. True","B. False","C. Both True and False","D. Cannot be determined"}[i];
            practiceOptionRects[i] = option;
        }
        practiceQuizContinue = MakeButton(practiceQuizCard, "Continue", 0, FinishPracticeQuiz).GetComponent<Button>();
        practiceQuizContinue.gameObject.SetActive(false);
        practiceQuiz.SetActive(false);
    }

    private static RectTransform MakeRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        return rect;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static TextMeshProUGUI MakeText(string name, Transform parent, float size)
    {
        TextMeshProUGUI text = MakeRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = 28f;
        text.fontSizeMax = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static void AddBorder(RectTransform parent)
    {
        Color color = new Color(1f, 0.86f, 0.22f, 0.96f);
        AddEdge("Top", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 7f), color);
        AddEdge("Bottom", parent, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 7f), color);
        AddEdge("Left", parent, Vector2.zero, new Vector2(0f, 1f), new Vector2(7f, 0f), color);
        AddEdge("Right", parent, new Vector2(1f, 0f), Vector2.one, new Vector2(7f, 0f), color);
    }

    private static void AddEdge(string name, RectTransform parent, Vector2 min, Vector2 max, Vector2 size, Color color)
    {
        RectTransform edge = MakeRect(name, parent);
        edge.anchorMin = min;
        edge.anchorMax = max;
        edge.sizeDelta = size;
        Image image = edge.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private static RectTransform MakeButton(Transform parent, string label, float x, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = MakeRect(label + " Button", parent);
        rect.anchoredPosition = new Vector2(x, -71f);
        rect.sizeDelta = new Vector2(250f, 92f);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.12f, 0.48f, 0.43f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.onClick.AddListener(action);
        TextMeshProUGUI caption = MakeText(label, rect, 40);
        caption.text = label;
        Stretch(caption.rectTransform, 8f);
        return rect;
    }

    private void SetInstruction(string message, RectTransform target, bool bottom = false, bool showNext = false)
    {
        instructionTarget = target; instructionBottom = bottom;
        instructionText.text = message;
        nextButton.gameObject.SetActive(showNext);
        if (target != null)
        {
            highlight.SetParent(target, false);
            Stretch(highlight, -10f);
            highlight.SetAsLastSibling();
            highlight.gameObject.SetActive(true);
            TextMeshProUGUI arrowLabel = arrow.GetComponent<TextMeshProUGUI>();
            arrowLabel.text = bottom ? "▲" : "▼";
            arrow.anchorMin = arrow.anchorMax = new Vector2(0.5f, bottom ? 0f : 1f);
            arrow.pivot = new Vector2(0.5f, bottom ? 1f : 0f);
            arrow.anchoredPosition = new Vector2(0f, bottom ? -12f : 15f);
        }
        else highlight.gameObject.SetActive(false);
        worldTargets.Clear();
        worldPoints.Clear();
        uiGroupTargets.Clear();
        worldHighlight.gameObject.SetActive(false);
        instructions.gameObject.SetActive(true);
        instructionBlockers = ControlTutorialLayout.FindBlockers();
        confirmation.SetActive(false);
        LayoutInstruction();
    }

    private void SetWorldInstruction(string message, bool showNext, Renderer[] targets,
        Transform[] points = null)
    {
        SetInstruction(message, null, false, showNext);
        if (targets != null) worldTargets.AddRange(targets);
        if (points != null) worldPoints.AddRange(points);
    }

    private void BeginMovement()
    {
        joystickInput?.ResetJoystick();
        input.SetGameplayInputBlocked(false);
        step = Step.Movement;
        skipControl?.Show(true);
        movementStart = player.transform.position;
        SetInstruction("Use this joystick to move.", joystick);
    }

    private void BeginJump()
    {
        step = Step.Jump;
        jumpRequested = false;
        jumpStartY = player.transform.position.y;
        SetInstruction("Tap this button to jump.", jumpButton);
    }

    private void BeginInteraction()
    {
        step = Step.Interaction;
        SetInstruction("Tap the hand button to interact with nearby objects.", handButton);
    }

    private void PrepareDemonstration()
    {
        if (demonstrationPrepared) return;
        demonstrationPrepared = true;
        originalJoystickVisible = joystick.gameObject.activeSelf;
        originalJumpVisible = jumpButton.gameObject.activeSelf;
        originalHandVisible = handButton.gameObject.activeSelf;
        joystickInput?.ResetJoystick();
        joystick.gameObject.SetActive(false);
        jumpButton.gameObject.SetActive(false);

        playerRenderers = player.GetComponentsInChildren<Renderer>(true);
        playerRendererStates = new bool[playerRenderers.Length];
        for (int i = 0; i < playerRenderers.Length; i++)
        {
            playerRendererStates[i] = playerRenderers[i].enabled;
            playerRenderers[i].enabled = false;
        }
        pinchZoom = FindFirstObjectByType<CinemachinePinchZoom>();
        cameraController = FindFirstObjectByType<ThirdPersonCameraController>();
        previousPinchEnabled = pinchZoom != null && pinchZoom.enabled;
        previousCameraEnabled = cameraController != null && cameraController.enabled;
        if (pinchZoom != null) pinchZoom.enabled = false;
        if (cameraController != null) cameraController.enabled = false;

        Camera rendered = Camera.main;
        GameObject cameraObject = new GameObject("Truth Table Tutorial Camera");
        demonstrationCamera = cameraObject.AddComponent<CinemachineCamera>();
        if (rendered != null)
        {
            demonstrationCamera.transform.SetPositionAndRotation(rendered.transform.position,
                rendered.transform.rotation);
            var lens = demonstrationCamera.Lens;
            lens.FieldOfView = rendered.fieldOfView;
            demonstrationCamera.Lens = lens;
        }
        cameraGoal = demonstrationCamera.transform.position;
        cameraRotationGoal = demonstrationCamera.transform.rotation;
        var priority = demonstrationCamera.Priority;
        priority.Value = 1000;
        demonstrationCamera.Priority = priority;
    }

    private void BeginBookDemo()
    {
        step = Step.Book;
        PrepareDemonstration();
        handButton.gameObject.SetActive(true);
        AimAtBook();
        SetWorldInstruction("Tap the hand button to interact with this book statue and start each challenge.", false,
            StableRenderers(book));
    }

    private void BeginTimerDemo()
    {
        step = Step.Timer;
        handButton.gameObject.SetActive(false);
        timerLabel.text = "18:00";
        RectTransform currentTimer = ControlTutorialLayout.JourneyElement("StageCountdown");
        timerLabel.gameObject.SetActive(currentTimer == null);
        SetInstruction("This is your challenge timer. Each challenge starts with 18 minutes. Complete it before time runs out.",
            currentTimer != null ? currentTimer : timerLabel.rectTransform, true, true);
    }

    private void BeginMinimapDemo()
    {
        step = Step.Minimap;
        SetInstruction("Use the minimap to find your way around the stage. Tap it for a centered overview of the whole playable map; Close returns to this view.",
            minimapPanel, true, true);
    }

    private void BeginProgressDemo()
    {
        step = Step.Progress;
        challengeLabel.text = "Challenge 1 — Easy";
        columnLabel.text = "Column 1 of 3";
        RectTransform currentProgress = ControlTutorialLayout.JourneyElement("StageProgress");
        challengeLabel.gameObject.SetActive(currentProgress == null);
        columnLabel.gameObject.SetActive(currentProgress == null);
        SetInstruction("These labels show your current challenge and column progress.",
            currentProgress, true, true);
        if (currentProgress == null)
        {
            uiGroupTargets.Add(challengeLabel.rectTransform);
            uiGroupTargets.Add(columnLabel.rectTransform);
        }
    }

    private void BeginBoardSlotsDemo()
    {
        step = Step.BoardSlots;
        for (int i = 0; i < boardHeaders.Length; i++)
            if (boardHeaders[i] != null) boardHeaders[i].text = "P ? Q";
        AimAtBoard();
        SetWorldInstruction("Numbered rows identify the four P/Q combinations: TT, TF, FT, FF. True/False element blocks are the expression outputs. Place each output in the highlighted row of the current Truth Table column.",
            true, null, puzzle.GetColumnSnapPoints(0));
    }

    private void BeginBoardSubmitDemo()
    {
        step = Step.BoardSubmit;
        SetWorldInstruction("Press Submit Column when you are ready. Incorrect submissions cost 20 seconds.",
            true, null, puzzle.GetColumnSnapPoints(0));
    }

    private void BeginHouseDemo()
    {
        step = Step.House;
        RestoreBoardHeaders();
        AimAtHouse();
        SetWorldInstruction("There are six houses in this stage. Search them to find True and False blocks.",
            true, null);
    }

    private void BeginDoorDemo()
    {
        step = Step.Door;
        handButton.gameObject.SetActive(true);
        SetInstruction("A door bubble expands when you are nearby. Tap the hand button to open its multiple-choice quiz.", handButton);
        worldTargets.AddRange(houseDoor.GetComponentsInChildren<Renderer>(true));
    }

    private void OpenPracticeQuiz()
    {
        if (step != Step.Door) return;
        step = Step.DoorQuiz;
        handButton.gameObject.SetActive(false);
        instructions.gameObject.SetActive(false);
        worldHighlight.gameObject.SetActive(false);
        practiceQuizFeedback.text = "Choose an answer. Practice does not use real time.";
        practiceQuizContinue.gameObject.SetActive(false);
        foreach (Button answer in practiceAnswerButtons) answer.interactable = true;
        practiceQuiz.SetActive(true);
        practiceQuiz.transform.SetAsLastSibling();
    }

    private void AnswerPracticeQuiz(int answerIndex)
    {
        if (!IsActive || step != Step.DoorQuiz || answerIndex < 0 || answerIndex >= 4) return;
        step = Step.DoorQuizFeedback;
        foreach (Button answer in practiceAnswerButtons) answer.interactable = false;
        practiceQuizFeedback.text = (answerIndex == 1 ? "Correct." : "Incorrect. Correct answer: False.") + "\nPractice only — no time is deducted.";
        practiceQuizFeedback.color = answerIndex == 1 ? new Color(.25f,.95f,.35f) : new Color(1,.4f,.4f);
        practiceQuizContinue.gameObject.SetActive(true);
    }

    private void FinishPracticeQuiz()
    {
        if (step != Step.DoorQuizFeedback) return;
        step = Step.DoorOpened;
        practiceQuiz.SetActive(false);
        doors.SetTutorialDoorOpen(tutorialHouseIndex, true);
        SetWorldInstruction("Either answer opens the door. In gameplay, an incorrect answer shows the correct answer and costs 10 seconds. The countdown stays paused while you read the quiz feedback; this practice never deducts time.",
            true, houseDoor.GetComponentsInChildren<Renderer>(true));
    }

    private void AdvanceExplanation()
    {
        if (!IsActive) return;
        switch (step)
        {
            case Step.Timer: BeginMinimapDemo(); break;
            case Step.Minimap: BeginProgressDemo(); break;
            case Step.Progress: BeginBoardSlotsDemo(); break;
            case Step.BoardSlots: BeginBoardSubmitDemo(); break;
            case Step.BoardSubmit: BeginHouseDemo(); break;
            case Step.House: BeginDoorDemo(); break;
            case Step.DoorOpened:
                step = Step.Blocks;
                handButton.gameObject.SetActive(true);
                SetInstruction("True/False element blocks represent the output for a row. Their bubbles appear only after their house opens. Approach an available block and tap the hand button to collect it. Choose a True or False inventory stack, fill the board rows, and tap Submit Column.", handButton, false, true);
                break;
            case Step.Blocks: BeginConfirmation(); break;
        }
    }

    public void SkipTutorial()
    {
        if (!IsActive || step == Step.Confirm) return;
        // Change state first so interaction callbacks cannot advance the old step.
        step = Step.Confirm;
        StopAllCoroutines();
        joystickInput?.ResetJoystick();
        PrelimTutorialSkipControl.CancelCamera(demonstrationCamera);
        demonstrationCamera = null;
        RestoreDemonstration();
        if (skipControl != null) skipControl.Release();
        skipControl = null;
        RestoreHudAndBoard();
        BeginConfirmation();
    }

    private void BeginConfirmation()
    {
        skipControl?.Show(false);
        step = Step.Confirm;
        input.SetGameplayInputBlocked(true);
        instructions.gameObject.SetActive(false);
        highlight.gameObject.SetActive(false);
        worldHighlight.gameObject.SetActive(false);
        confirmation.SetActive(true);
        confirmation.transform.SetAsLastSibling();
    }

    private void Update()
    {
        if (!IsActive) return;
        if (StageSelectionState.SelectedStage != 2 || player == null) { Cleanup(); return; }
        if (step == Step.Movement && joystickInput != null &&
            joystickInput.CurrentInput.sqrMagnitude > joystickDeadZone * joystickDeadZone &&
            Vector2.Distance(new Vector2(movementStart.x, movementStart.z),
                new Vector2(player.transform.position.x, player.transform.position.z)) >= movementDistance)
            BeginJump();
        else if (step == Step.Jump && jumpRequested && player.IsJumping() &&
                 player.transform.position.y > jumpStartY + 0.1f)
            BeginInteraction();

        if (demonstrationCamera != null)
        {
            float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 4f / cameraTransitionSeconds);
            demonstrationCamera.transform.position = Vector3.Lerp(
                demonstrationCamera.transform.position, cameraGoal, blend);
            demonstrationCamera.transform.rotation = Quaternion.Slerp(
                demonstrationCamera.transform.rotation, cameraRotationGoal, blend);
        }
    }

    private void LateUpdate()
    {
        if (!IsActive || safeArea == null || confirmationCard == null) return;
        float width = Mathf.Min(660f, Mathf.Max(300f, safeArea.rect.width - 40f));
        confirmationCard.sizeDelta = new Vector2(width, 280f);
        if (confirmationQuestion != null)
            confirmationQuestion.sizeDelta = new Vector2(width - 40f, 84f);
        if (practiceQuizCard != null)
        {
            float quizWidth = Mathf.Min(850f, Mathf.Max(360f, safeArea.rect.width - 40f));
            bool stackAnswers = quizWidth < 700f;
            practiceQuizCard.sizeDelta = new Vector2(quizWidth, stackAnswers ? 700f : 560f);
            practiceQuizCard.anchoredPosition = Vector2.zero;
            if (practiceQuizTitle != null)
            {
                practiceQuizTitle.pivot = new Vector2(.5f,1);practiceQuizTitle.anchoredPosition = new Vector2(0,-24);practiceQuizTitle.sizeDelta = new Vector2(quizWidth-48,96);
                var label = practiceQuizTitle.GetComponent<TMP_Text>();label.fontSize=32;label.enableAutoSizing=true;label.fontSizeMin=24;label.fontSizeMax=32;
            }
            if (practiceQuizFeedback != null)
            {
                var rect=practiceQuizFeedback.rectTransform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(0,-128);rect.sizeDelta=new Vector2(quizWidth-48,110);practiceQuizFeedback.fontSize=28;
            }
            if (practiceOptionRects != null)
                for (int i = 0; i < practiceOptionRects.Length; i++)
                {
                    var option=practiceOptionRects[i];if(option==null)continue;
                    option.anchorMin=option.anchorMax=option.pivot=new Vector2(.5f,1);
                    option.sizeDelta=new Vector2(stackAnswers?quizWidth-60:315,stackAnswers?68:84);
                    option.anchoredPosition=new Vector2(stackAnswers?0:i%2==0?-175:175,-252-(stackAnswers?i*78:(i/2)*98));
                }
            if(practiceQuizContinue!=null)
            {
                var rect=(RectTransform)practiceQuizContinue.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,1);
                rect.sizeDelta=new Vector2(240,64);rect.anchoredPosition=new Vector2(0,stackAnswers?-590:-468);
            }
        }
        float buttonWidth = Mathf.Min(250f, width * 0.42f);
        if (yesButton != null)
        {
            yesButton.sizeDelta = new Vector2(buttonWidth, 92f);
            yesButton.anchoredPosition = new Vector2(-width * 0.25f, -71f);
        }
        if (noButton != null)
        {
            noButton.sizeDelta = new Vector2(buttonWidth, 92f);
            noButton.anchoredPosition = new Vector2(width * 0.25f, -71f);
        }
        UpdateWorldHighlight();
        LayoutInstruction();
    }

    private void UpdateWorldHighlight()
    {
        if (worldHighlight == null || safeArea == null || Camera.main == null) return;
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        bool found = false;
        foreach (Renderer renderer in worldTargets)
        {
            if (renderer == null || !renderer.gameObject.activeInHierarchy) continue;
            Bounds bounds = renderer.bounds;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
                AddProjectedPoint(bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3(x, y, z)), ref min, ref max, ref found);
        }
        foreach (Transform point in worldPoints)
            if (point != null) AddProjectedPoint(point.position, ref min, ref max, ref found);
        foreach (RectTransform target in uiGroupTargets)
        {
            if (target == null || !target.gameObject.activeInHierarchy) continue;
            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, corner);
                Vector2 local;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(safeArea, screen,
                    null, out local)) continue;
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
                found = true;
            }
        }
        worldHighlight.gameObject.SetActive(found);
        if (!found) return;
        worldHighlight.anchoredPosition = (min + max) * 0.5f;
        worldHighlight.sizeDelta = max - min + new Vector2(75f, 75f);
    }

    private void AddProjectedPoint(Vector3 worldPosition, ref Vector2 min, ref Vector2 max, ref bool found)
    {
        Vector3 screen = Camera.main.WorldToScreenPoint(worldPosition);
        if (screen.z <= 0f) return;
        if (screen.x < -Screen.width * 0.25f || screen.x > Screen.width * 1.25f ||
            screen.y < -Screen.height * 0.25f || screen.y > Screen.height * 1.25f) return;
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(safeArea, screen, null, out local)) return;
        min = Vector2.Min(min, local);
        max = Vector2.Max(max, local);
        found = true;
    }

    private void LayoutInstruction()
    {
        RectTransform target = instructionTarget;
        if (target == null && worldHighlight != null && worldHighlight.gameObject.activeSelf)
            target = worldHighlight;
        ControlTutorialLayout.Place(instructions, instructionText, nextButton, safeArea,
            target, instructionBottom, instructionBlockers, instructionTarget != null ? arrow : null);
    }

    public void NoteJumpInput()
    {
        if (IsActive && step == Step.Jump) jumpRequested = true;
    }

    public void TryPracticeInteraction()
    {
        TryTutorialInteraction();
    }

    public void TryTutorialInteraction()
    {
        if (!IsActive) return;
        if (step == Step.Book) { BeginTimerDemo(); return; }
        if (step == Step.Door) { OpenPracticeQuiz(); return; }
        if (step != Step.Interaction) return;
        BeginBookDemo();
    }

    private static Bounds RendererBounds(Transform target)
    {
        Renderer[] renderers = StableRenderers(target);
        Bounds bounds = new Bounds(target.position, Vector3.zero);
        bool found = false;
        foreach (Renderer renderer in renderers)
        {
            if (!renderer.gameObject.activeInHierarchy) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }

    private static Renderer[] StableRenderers(Transform target)
    {
        Renderer[] all = target.GetComponentsInChildren<Renderer>(true);
        List<Renderer> stable = new List<Renderer>(all.Length);
        foreach (Renderer renderer in all)
            if (renderer != null && !renderer.name.ToLowerInvariant().Contains("turning page"))
                stable.Add(renderer);
        return stable.ToArray();
    }

    private Transform TutorialHouse
    {
        get
        {
            Transform parent = houseDoor != null ? houseDoor.transform.parent : null;
            return parent != null && parent.name.Contains("QuizDoorHinge") ? parent.parent : parent;
        }
    }

    private float FitViewDistance(Bounds bounds)
    {
        float fov = demonstrationCamera != null ? demonstrationCamera.Lens.FieldOfView : 60f;
        float tangent = Mathf.Tan(Mathf.Deg2Rad * Mathf.Max(20f, fov) * 0.5f);
        float aspect = Camera.main != null ? Mathf.Max(0.5f, Camera.main.aspect) : 1.6f;
        return Mathf.Max(bounds.size.y / (2f * tangent),
            bounds.size.x / (2f * tangent * aspect)) * 1.25f;
    }

    private void SetCameraGoal(Vector3 position, Vector3 lookAt)
    {
        cameraGoal = position;
        Vector3 forward = lookAt - position;
        if (forward.sqrMagnitude > 0.01f)
            cameraRotationGoal = Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    private void AimAtBook()
    {
        Bounds bounds = RendererBounds(book);
        Vector3 outward = player.transform.position - bounds.center;
        outward.y = 0f;
        if (outward.sqrMagnitude < 0.01f) outward = Vector3.forward;
        outward.Normalize();
        float distance = Mathf.Max(bookViewDistance, FitViewDistance(bounds));
        SetCameraGoal(bounds.center + outward * distance + Vector3.up * 0.8f,
            bounds.center);
    }

    private void AimAtBoard()
    {
        Bounds bounds = RendererBounds(board);
        Transform preset = board.Find("VCam_TruthTable");
        Vector3 outward = preset != null ? preset.position - bounds.center : book.position - bounds.center;
        outward.y = 0f;
        if (outward.sqrMagnitude < 0.01f) outward = Vector3.forward;
        outward.Normalize();
        float distance = Mathf.Max(boardViewDistance, FitViewDistance(bounds));
        SetCameraGoal(bounds.center + outward * distance + Vector3.up * 0.8f,
            bounds.center);
    }

    private void AimAtHouse()
    {
        Transform house = TutorialHouse;
        Bounds houseBounds = RendererBounds(house);
        Vector3 outward = -houseDoor.transform.forward;
        outward.y = 0f;
        if (outward.sqrMagnitude < 0.01f) outward = houseDoor.bounds.center - houseBounds.center;
        outward.Normalize();
        float distance = Mathf.Max(houseViewDistance, FitViewDistance(houseBounds) * 0.75f);
        Vector3 focus = houseDoor.bounds.center + Vector3.up * 3f;
        SetCameraGoal(houseDoor.bounds.center + outward * distance + Vector3.up * 2f,
            focus);
    }

    private void RestoreDemonstration()
    {
        if (doors != null) doors.SetTutorialDoorOpen(tutorialHouseIndex, false);
        if (practiceQuiz != null) practiceQuiz.SetActive(false);
        if (demonstrationCamera != null)
        {
            var priority = demonstrationCamera.Priority;
            priority.Value = -1000;
            demonstrationCamera.Priority = priority;
            Destroy(demonstrationCamera.gameObject, cameraTransitionSeconds + 0.2f);
            demonstrationCamera = null;
        }
        if (pinchZoom != null) pinchZoom.enabled = previousPinchEnabled;
        if (cameraController != null) cameraController.enabled = previousCameraEnabled;
        pinchZoom = null;
        cameraController = null;
        if (playerRenderers != null && playerRendererStates != null)
            for (int i = 0; i < Mathf.Min(playerRenderers.Length, playerRendererStates.Length); i++)
                if (playerRenderers[i] != null) playerRenderers[i].enabled = playerRendererStates[i];
        playerRenderers = null;
        playerRendererStates = null;
        if (demonstrationPrepared)
        {
            if (joystick != null) joystick.gameObject.SetActive(originalJoystickVisible);
            if (jumpButton != null) jumpButton.gameObject.SetActive(originalJumpVisible);
            if (handButton != null) handButton.gameObject.SetActive(originalHandVisible);
            demonstrationPrepared = false;
        }
        RestoreBoardHeaders();
        worldTargets.Clear();
        worldPoints.Clear();
        uiGroupTargets.Clear();
        if (worldHighlight != null) worldHighlight.gameObject.SetActive(false);
    }

    private void ConfirmNo()
    {
        if (!IsActive || step != Step.Confirm) return;
        RestoreDemonstration();
        if (timerLabel != null) timerLabel.gameObject.SetActive(false);
        if (challengeLabel != null) challengeLabel.gameObject.SetActive(false);
        if (columnLabel != null) columnLabel.gameObject.SetActive(false);
        BeginMovement();
    }

    private void ConfirmYes()
    {
        if (IsActive && step == Step.Confirm) Cleanup();
    }

    private void Cleanup()
    {
        RestoreDemonstration();
        if (skipControl != null) skipControl.Release();
        skipControl = null;
        RestoreHudAndBoard();
        clock?.SetTutorialOpen(false);
        if (input != null) input.SetGameplayInputBlocked(previousInputBlocked);
        joystickInput?.ResetJoystick();
        if (highlight != null) Destroy(highlight.gameObject);
        if (worldHighlight != null) Destroy(worldHighlight.gameObject);
        if (instructions != null) Destroy(instructions.gameObject);
        if (confirmation != null) Destroy(confirmation);
        if (practiceQuiz != null) Destroy(practiceQuiz);
        highlight = null;
        worldHighlight = null;
        instructions = null;
        confirmation = null;
        practiceQuiz = null;
        practiceQuizCard = null;
        practiceQuizTitle = null;
        practiceQuizFeedback = null;
        practiceOptionRects = null;
        practiceAnswerButtons = null;
        confirmationCard = null;
        confirmationQuestion = null;
        yesButton = null;
        noButton = null;
        IsActive = false;
        if (Active == this) Active = null;
        if (helpButton != null && StageSelectionState.SelectedStage == 2)
            helpButton.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (IsActive) Cleanup();
    }
}
