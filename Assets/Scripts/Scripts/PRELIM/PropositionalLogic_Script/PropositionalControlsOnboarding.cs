using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One-time, Propositional Logic-only practice after How to Play.</summary>
public sealed class PropositionalControlsOnboarding : MonoBehaviour
{
    private enum Step { Movement, Jump, Interaction, Book, Timer, Question, HintBoard, Doors, Confirm }

    [SerializeField, Range(0.15f, 0.8f)] private float joystickDeadZone = 0.3f;
    [SerializeField, Min(0.1f)] private float movementDistance = 0.35f;
    [SerializeField, Min(1f)] private float practiceRange = 2.5f;
    [Header("Guided camera")]
    [SerializeField, Min(0.1f)] private float cameraTransitionSeconds = 0.7f;
    [SerializeField, Min(2f)] private float bookViewDistance = 5.5f;
    [SerializeField, Min(3f)] private float hintBoardViewDistance = 10f;

    public static PropositionalControlsOnboarding Active { get; private set; }
    public bool IsActive { get; private set; }
    public bool AllowsJump => IsActive && step == Step.Jump;
    public bool SuppressMovement => IsActive && step >= Step.Book;
    public bool IsDemonstratingBook => IsActive && step == Step.Book;

    private Step step;
    private GameInput input;
    private LevelTimerManager timer;
    private Player player;
    private MobileInputUI mobileControls;
    private Button helpButton;
    private RectTransform joystick;
    private RectTransform jumpButton;
    private RectTransform handButton;
    private RectTransform highlight;
    private RectTransform arrow;
    private RectTransform instructions;
    private TextMeshProUGUI instructionText;
    private Button nextButton;
    private RectTransform safeArea;
    private RectTransform worldHighlight;
    private readonly List<Renderer> worldTargets = new List<Renderer>();
    private GameObject confirmation;
    private GameObject practiceTarget;
    private TextMeshPro practiceLabel;
    private Vector3 movementStart;
    private float jumpStartY;
    private bool jumpRequested;
    private QuizManager quiz;
    private TopicChallenge room;
    private BookInteract roomBook;
    private Button bookButton;
    private bool originalBookButtonVisible;
    private bool originalJoystickVisible;
    private bool originalJumpVisible;
    private bool originalHandVisible;
    private bool demonstrationPrepared;
    private TextMeshProUGUI timerText;
    private bool originalTimerVisible;
    private string originalTimerText;
    private GameObject quizPanel;
    private bool originalQuizPanelVisible;
    private GameObject quizOptions;
    private bool originalQuizOptionsVisible;
    private TextMeshProUGUI questionText;
    private string originalQuestionText;
    private TextMeshProUGUI[] doorTexts;
    private string[] originalDoorTexts;
    private bool originalDoorCanvasVisible;
    private CinemachineCamera demonstrationCamera;
    private Vector3 cameraGoal;
    private Quaternion cameraRotationGoal;
    private Renderer[] playerRenderers;
    private bool[] playerRendererStates;

    public void Begin(Button existingHelpButton)
    {
        if (IsActive || StageSelectionState.SelectedStage != 1) return;
        input = GameInput.Instance != null ? GameInput.Instance : FindFirstObjectByType<GameInput>();
        timer = FindFirstObjectByType<LevelTimerManager>();
        quiz = FindFirstObjectByType<QuizManager>();
        player = Player.LocalInstance;
        mobileControls = FindFirstObjectByType<MobileInputUI>();
        helpButton = existingHelpButton;
        if (input == null || player == null || !BuildUI())
        {
            Debug.LogError("Propositional controls tutorial needs the player, GameInput, and PRELIM Canvas controls.");
            Cleanup();
            return;
        }

        Active = this;
        IsActive = true;
        timer?.SetTutorialPaused(true);
        input.SetGameplayInputBlocked(false);
        if (helpButton != null) helpButton.gameObject.SetActive(false);
        BeginMovement();
    }

    private bool BuildUI()
    {
        Canvas canvas = GameObject.Find("Canvas 1")?.GetComponent<Canvas>();
        safeArea = canvas != null ? canvas.transform.Find("SafeArea") as RectTransform : null;
        if (safeArea == null) return false;
        joystick = mobileControls != null ? mobileControls.transform as RectTransform : null;
        jumpButton = safeArea.Find("Gameplay_Interface/JumpButton") as RectTransform;
        handButton = safeArea.Find("Gameplay_Interface/InteractButton") as RectTransform;
        if (joystick == null || jumpButton == null || handButton == null) return false;

        instructions = MakeRect("Controls Tutorial Instructions", safeArea, new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -108f), new Vector2(1200f, 180f));
        Image backing = instructions.gameObject.AddComponent<Image>();
        backing.color = new Color(0.06f, 0.13f, 0.19f, 0.93f);
        backing.raycastTarget = false;
        instructionText = MakeText("Instruction", instructions, 38, TextAlignmentOptions.Center);
        Stretch(instructionText.rectTransform, 24f);
        nextButton = MakeButton(instructions, "Next", new Vector2(455f, 0f), AdvanceExplanation);
        nextButton.gameObject.SetActive(false);

        RectTransform marker = MakeRect("Control Highlight", safeArea, new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        highlight = marker;
        AddBorder(marker);
        TextMeshProUGUI arrowText = MakeText("Arrow", marker, 72, TextAlignmentOptions.Center);
        arrowText.text = "▼";
        arrowText.color = new Color(1f, 0.9f, 0.25f);
        arrowText.raycastTarget = false;
        arrow = arrowText.rectTransform;
        arrow.anchorMin = arrow.anchorMax = new Vector2(0.5f, 1f);
        arrow.pivot = new Vector2(0.5f, 0f);
        arrow.sizeDelta = new Vector2(110f, 90f);
        arrow.anchoredPosition = new Vector2(0f, 16f);

        worldHighlight = MakeRect("World Tutorial Highlight", safeArea, new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 100f));
        AddBorder(worldHighlight);
        worldTargets.Clear();
        worldHighlight.gameObject.SetActive(false);

        RectTransform shade = MakeRect("Controls Tutorial Confirm", safeArea, Vector2.zero,
            Vector2.one, Vector2.zero, Vector2.zero);
        Stretch(shade, 0f);
        Image shadeImage = shade.gameObject.AddComponent<Image>();
        shadeImage.color = new Color(0f, 0f, 0f, 0.55f);
        confirmation = shade.gameObject;
        RectTransform card = MakeRect("Confirmation Card", shade, new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(660f, 280f));
        Image cardImage = card.gameObject.AddComponent<Image>();
        cardImage.color = new Color(0.06f, 0.16f, 0.22f, 0.98f);
        TextMeshProUGUI question = MakeText("Ready Question", card, 48, TextAlignmentOptions.Center);
        question.text = "Let's start?";
        question.rectTransform.anchorMin = question.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        question.rectTransform.anchoredPosition = new Vector2(0f, -62f);
        question.rectTransform.sizeDelta = new Vector2(590f, 84f);
        MakeButton(card, "Yes", new Vector2(-155f, -71f), ConfirmYes);
        MakeButton(card, "No", new Vector2(155f, -71f), ConfirmNo);
        confirmation.SetActive(false);
        return true;
    }

    private static RectTransform MakeRect(string name, Transform parent, Vector2 min, Vector2 max,
        Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static void AddBorder(RectTransform target)
    {
        Color color = new Color(1f, 0.86f, 0.22f, 0.95f);
        CreateBorderEdge("Top", target, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, 7f), color);
        CreateBorderEdge("Bottom", target, Vector2.zero, new Vector2(1f, 0f),
            new Vector2(0f, 7f), color);
        CreateBorderEdge("Left", target, Vector2.zero, new Vector2(0f, 1f),
            new Vector2(7f, 0f), color);
        CreateBorderEdge("Right", target, new Vector2(1f, 0f), Vector2.one,
            new Vector2(7f, 0f), color);
    }

    private static void CreateBorderEdge(string name, RectTransform parent, Vector2 min,
        Vector2 max, Vector2 size, Color color)
    {
        RectTransform rect = MakeRect(name, parent, min, max, Vector2.zero, size);
        Image edge = rect.gameObject.AddComponent<Image>();
        edge.color = color;
        edge.raycastTarget = false;
    }

    private static TextMeshProUGUI MakeText(string name, Transform parent, float size, TextAlignmentOptions alignment)
    {
        RectTransform rect = MakeRect(name, parent, new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = 30;
        text.fontSizeMax = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static Button MakeButton(Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = MakeRect(label + " Button", parent, new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), position, new Vector2(250f, 92f));
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.12f, 0.48f, 0.43f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.onClick.AddListener(action);
        TextMeshProUGUI text = MakeText(label, rect, 40, TextAlignmentOptions.Center);
        text.text = label;
        Stretch(text.rectTransform, 8f);
        return button;
    }

    private void BeginMovement()
    {
        DestroyPracticeTarget();
        mobileControls?.ResetJoystick();
        input.SetGameplayInputBlocked(false);
        step = Step.Movement;
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
        CreatePracticeTarget();
        SetInstruction("Use this button to interact with nearby objects.", handButton);
    }

    private void SetInstruction(string message, RectTransform target, bool bottom = false, bool showNext = false)
    {
        instructionText.text = message;
        instructions.anchorMin = instructions.anchorMax = new Vector2(0.5f, bottom ? 0f : 1f);
        instructions.anchoredPosition = new Vector2(0f, bottom ? 200f : -108f);
        nextButton.gameObject.SetActive(showNext);
        instructionText.rectTransform.offsetMax = new Vector2(showNext ? -290f : -24f, -24f);
        if (target != null)
        {
            highlight.SetParent(target, false);
            Stretch(highlight, -10f);
            highlight.SetAsLastSibling();
            highlight.gameObject.SetActive(true);
        }
        else highlight.gameObject.SetActive(false);
        worldTargets.Clear();
        worldHighlight.gameObject.SetActive(false);
        instructions.gameObject.SetActive(true);
        confirmation.SetActive(false);
    }

    private void Update()
    {
        if (!IsActive || player == null || input == null) return;
        if (step == Step.Movement && mobileControls != null &&
            mobileControls.CurrentInput.sqrMagnitude > joystickDeadZone * joystickDeadZone &&
            Vector3.Distance(new Vector3(movementStart.x, 0f, movementStart.z),
                new Vector3(player.transform.position.x, 0f, player.transform.position.z)) >= movementDistance)
            BeginJump();
        else if (step == Step.Jump && jumpRequested && player.IsJumping() &&
                 player.transform.position.y > jumpStartY + 0.1f)
            BeginInteraction();

        if (practiceLabel != null && Camera.main != null)
            practiceLabel.transform.rotation = Camera.main.transform.rotation;
        if (demonstrationCamera != null)
        {
            float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 4f / cameraTransitionSeconds);
            demonstrationCamera.transform.position = Vector3.Lerp(demonstrationCamera.transform.position, cameraGoal, blend);
            demonstrationCamera.transform.rotation = Quaternion.Slerp(demonstrationCamera.transform.rotation, cameraRotationGoal, blend);
        }
    }

    private void LateUpdate()
    {
        if (!IsActive || worldHighlight == null || worldTargets.Count == 0 ||
            Camera.main == null || safeArea == null) return;
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
            {
                Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                Vector3 screen = Camera.main.WorldToScreenPoint(point);
                if (screen.z <= 0f) continue;
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
        worldHighlight.sizeDelta = max - min +
            (step == Step.Book ? new Vector2(130f, 28f) : new Vector2(28f, 28f));
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
        if (step == Step.Book)
        {
            BeginTimerDemo();
            return;
        }
        if (step != Step.Interaction || practiceTarget == null || player == null) return;
        Vector3 toward = practiceTarget.transform.position - player.transform.position;
        toward.y = 0f;
        Vector3 facing = player.transform.forward;
        facing.y = 0f;
        if (toward.magnitude > practiceRange || toward.sqrMagnitude < 0.001f ||
            Vector3.Dot(facing.normalized, toward.normalized) < 0.25f) return;
        DestroyPracticeTarget();
        BeginBookDemo();
    }

    private void BeginBookDemo()
    {
        room = quiz != null ? quiz.CurrentChallengeForTutorial : null;
        roomBook = room != null && room.roomChoiceCanvas != null
            ? room.roomChoiceCanvas.transform.parent.GetComponentInChildren<BookInteract>(true) : null;
        if (roomBook == null || room.hintBoard == null)
        {
            Debug.LogError("The current Propositional room needs its book and hint board for onboarding.");
            Cleanup();
            return;
        }
        step = Step.Book;
        mobileControls?.ResetJoystick();
        SnapshotDemonstrationUI();
        playerRenderers = player.GetComponentsInChildren<Renderer>(true);
        playerRendererStates = new bool[playerRenderers.Length];
        for (int i = 0; i < playerRenderers.Length; i++)
        {
            playerRendererStates[i] = playerRenderers[i].enabled;
            playerRenderers[i].enabled = false;
        }
        CreateDemonstrationCamera();
        AimAtBookAndDoors(false);
        SetWorldInstruction("Interact with this book statue to start the game.", false,
            roomBook.GetComponentsInChildren<Renderer>(true));
        if (bookButton != null)
        {
            bookButton.gameObject.SetActive(true);
            bookButton.onClick.AddListener(OnBookButtonClicked);
        }
    }

    private void SnapshotDemonstrationUI()
    {
        demonstrationPrepared = true;
        Transform canvas = safeArea.root.Find("Canvas 1");
        if (canvas == null) canvas = GameObject.Find("Canvas 1").transform;
        bookButton = canvas.Find("ReadBook_BTN")?.GetComponent<Button>();
        originalBookButtonVisible = bookButton != null && bookButton.gameObject.activeSelf;
        originalJoystickVisible = joystick.gameObject.activeSelf;
        originalJumpVisible = jumpButton.gameObject.activeSelf;
        originalHandVisible = handButton.gameObject.activeSelf;
        timerText = canvas.Find("TimerText")?.GetComponent<TextMeshProUGUI>();
        if (timerText != null)
        {
            originalTimerVisible = timerText.gameObject.activeSelf;
            originalTimerText = timerText.text;
        }
        quizPanel = canvas.Find("QuizPanel")?.gameObject;
        originalQuizPanelVisible = quizPanel != null && quizPanel.activeSelf;
        quizOptions = canvas.Find("QuizPanel/Options")?.gameObject;
        originalQuizOptionsVisible = quizOptions != null && quizOptions.activeSelf;
        questionText = canvas.Find("QuizPanel/QuestionText")?.GetComponent<TextMeshProUGUI>();
        originalQuestionText = questionText != null ? questionText.text : null;
        originalDoorCanvasVisible = room.roomChoiceCanvas.gameObject.activeSelf;
        doorTexts = room.roomChoiceCanvas.GetComponentsInChildren<TextMeshProUGUI>(true);
        originalDoorTexts = new string[doorTexts.Length];
        for (int i = 0; i < doorTexts.Length; i++) originalDoorTexts[i] = doorTexts[i].text;
    }

    private void OnBookButtonClicked()
    {
        TryTutorialInteraction();
    }

    private void BeginTimerDemo()
    {
        if (step != Step.Book) return;
        step = Step.Timer;
        if (bookButton != null) bookButton.gameObject.SetActive(false);
        joystick.gameObject.SetActive(false);
        jumpButton.gameObject.SetActive(false);
        handButton.gameObject.SetActive(false);
        if (timerText != null)
        {
            int seconds = Mathf.CeilToInt(Mathf.Max(0f, timer != null ? timer.RemainingTime : 360f));
            timerText.text = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
            timerText.gameObject.SetActive(true);
            SetInstruction("This timer shows how much time you have left to complete the challenges.",
                timerText.rectTransform, true, true);
        }
        else SetInstruction("This timer shows how much time you have left to complete the challenges.",
            null, true, true);
    }

    private void BeginQuestionDemo()
    {
        step = Step.Question;
        if (timerText != null) timerText.gameObject.SetActive(false);
        if (quizOptions != null) quizOptions.SetActive(false);
        if (quizPanel != null) quizPanel.SetActive(true);
        if (questionText != null)
        {
            questionText.text = "Question related to this challenge";
            SetInstruction("Read the question and identify the logical operation it describes.",
                questionText.rectTransform, true, true);
            // The existing question RectTransform spans the entire HUD. Outline
            // only its visible text area, leaving the control buttons unobscured.
            highlight.anchorMin = highlight.anchorMax = new Vector2(0.5f, 1f);
            highlight.sizeDelta = new Vector2(1200f, 170f);
            highlight.anchoredPosition = new Vector2(0f, -260f);
        }
        else SetInstruction("Read the question and identify the logical operation it describes.",
            null, true, true);
    }

    private void BeginHintBoardDemo()
    {
        step = Step.HintBoard;
        if (quizPanel != null) quizPanel.SetActive(false);
        AimAtHintBoard();
        SetWorldInstruction("This hint board reveals a new truth-table hint every 20 seconds while the challenge timer is running.",
            true, room.hintBoard.GetComponentsInChildren<Renderer>(true));
    }

    private void BeginDoorsDemo()
    {
        step = Step.Doors;
        AimAtBookAndDoors(true);
        if (room.roomChoiceCanvas != null)
        {
            room.roomChoiceCanvas.gameObject.SetActive(true);
            for (int i = 0; i < Mathf.Min(4, doorTexts.Length); i++)
                doorTexts[i].text = "Answer " + (i + 1);
        }
        var renderers = new List<Renderer>();
        foreach (GameObject door in room.choiceDoors)
            if (door != null) renderers.AddRange(door.GetComponentsInChildren<Renderer>(true));
        SetWorldInstruction("Each door represents an answer. Read the choices and select the door that matches the question.\nAn incorrect choice costs 10 seconds and triggers the hammer knockback and stun.",
            true, renderers.ToArray());
    }

    private void AdvanceExplanation()
    {
        if (!IsActive) return;
        if (step == Step.Timer) BeginQuestionDemo();
        else if (step == Step.Question) BeginHintBoardDemo();
        else if (step == Step.HintBoard) BeginDoorsDemo();
        else if (step == Step.Doors) BeginConfirmation();
    }

    private void BeginConfirmation()
    {
        if (room != null && room.roomChoiceCanvas != null)
            room.roomChoiceCanvas.gameObject.SetActive(false);
        if (doorTexts != null && originalDoorTexts != null)
            for (int i = 0; i < Mathf.Min(doorTexts.Length, originalDoorTexts.Length); i++)
                if (doorTexts[i] != null) doorTexts[i].text = originalDoorTexts[i];
        step = Step.Confirm;
        worldTargets.Clear();
        worldHighlight.gameObject.SetActive(false);
        highlight.gameObject.SetActive(false);
        instructions.gameObject.SetActive(false);
        confirmation.SetActive(true);
        confirmation.transform.SetAsLastSibling();
        input.SetGameplayInputBlocked(true);
    }

    private void SetWorldInstruction(string message, bool next, Renderer[] targets)
    {
        SetInstruction(message, null, false, next);
        worldTargets.AddRange(targets);
        worldHighlight.gameObject.SetActive(worldTargets.Count > 0);
    }

    private void CreateDemonstrationCamera()
    {
        Camera rendered = Camera.main;
        GameObject objectForCamera = new GameObject("Propositional Tutorial Camera");
        demonstrationCamera = objectForCamera.AddComponent<CinemachineCamera>();
        if (rendered != null)
        {
            demonstrationCamera.transform.SetPositionAndRotation(rendered.transform.position, rendered.transform.rotation);
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

    private void AimAtBookAndDoors(bool focusDoors)
    {
        if (demonstrationCamera == null || roomBook == null) return;
        Vector3 bookPosition = roomBook.transform.position;
        Vector3 doorCenter = Vector3.zero;
        int count = 0;
        foreach (GameObject door in room.choiceDoors)
            if (door != null) { doorCenter += door.transform.position; count++; }
        doorCenter = count > 0 ? doorCenter / count : bookPosition + Vector3.forward * 8f;
        Vector3 roomForward = doorCenter - bookPosition;
        roomForward.y = 0f;
        roomForward = roomForward.sqrMagnitude > 0.01f ? roomForward.normalized : Vector3.forward;
        cameraGoal = bookPosition - roomForward * (bookViewDistance + (focusDoors ? 1.5f : 0f))
            + Vector3.up * 5f;
        Vector3 lookAt = focusDoors ? doorCenter + Vector3.up * 0.2f
            : bookPosition + roomForward * 4f + Vector3.up * 3.8f;
        cameraRotationGoal = Quaternion.LookRotation(lookAt - cameraGoal, Vector3.up);
    }

    private void AimAtHintBoard()
    {
        if (demonstrationCamera == null || room.hintBoard == null) return;
        Renderer boardRenderer = room.hintBoard.GetComponentInChildren<Renderer>(true);
        Vector3 center = boardRenderer != null ? boardRenderer.bounds.center : room.hintBoard.transform.position;
        Vector3 towardBoard = center - roomBook.transform.position;
        towardBoard.y = 0f;
        towardBoard = towardBoard.sqrMagnitude > 0.01f ? towardBoard.normalized : Vector3.forward;
        cameraGoal = center - towardBoard * hintBoardViewDistance;
        cameraRotationGoal = Quaternion.LookRotation(center - cameraGoal, Vector3.up);
    }

    private void RestoreDemonstrationUI()
    {
        if (demonstrationPrepared)
        {
            if (joystick != null) joystick.gameObject.SetActive(originalJoystickVisible);
            if (jumpButton != null) jumpButton.gameObject.SetActive(originalJumpVisible);
            if (handButton != null) handButton.gameObject.SetActive(originalHandVisible);
            demonstrationPrepared = false;
        }
        if (playerRenderers != null && playerRendererStates != null)
        {
            for (int i = 0; i < Mathf.Min(playerRenderers.Length, playerRendererStates.Length); i++)
                if (playerRenderers[i] != null) playerRenderers[i].enabled = playerRendererStates[i];
            playerRenderers = null;
            playerRendererStates = null;
        }
        if (bookButton != null)
        {
            bookButton.onClick.RemoveListener(OnBookButtonClicked);
            bookButton.gameObject.SetActive(originalBookButtonVisible);
            bookButton = null;
        }
        if (timerText != null)
        {
            timerText.text = originalTimerText;
            timerText.gameObject.SetActive(originalTimerVisible);
            timerText = null;
        }
        if (questionText != null)
        {
            questionText.text = originalQuestionText;
            questionText = null;
        }
        if (quizPanel != null)
        {
            quizPanel.SetActive(originalQuizPanelVisible);
            quizPanel = null;
        }
        if (quizOptions != null)
        {
            quizOptions.SetActive(originalQuizOptionsVisible);
            quizOptions = null;
        }
        if (doorTexts != null && originalDoorTexts != null)
        {
            for (int i = 0; i < Mathf.Min(doorTexts.Length, originalDoorTexts.Length); i++)
                if (doorTexts[i] != null) doorTexts[i].text = originalDoorTexts[i];
            doorTexts = null;
            originalDoorTexts = null;
        }
        if (room != null && room.roomChoiceCanvas != null)
            room.roomChoiceCanvas.gameObject.SetActive(originalDoorCanvasVisible);
        room = null;
        roomBook = null;
        if (demonstrationCamera != null)
        {
            var priority = demonstrationCamera.Priority;
            priority.Value = -1000;
            demonstrationCamera.Priority = priority;
            Destroy(demonstrationCamera.gameObject, cameraTransitionSeconds + 0.2f);
            demonstrationCamera = null;
        }
        worldTargets.Clear();
        if (worldHighlight != null) worldHighlight.gameObject.SetActive(false);
    }

    private void CreatePracticeTarget()
    {
        practiceTarget = GameObject.CreatePrimitive(PrimitiveType.Cube);
        practiceTarget.name = "Propositional Practice Interaction";
        Vector3 practicePosition = player.transform.position + player.transform.forward * 1.5f;
        practicePosition.y = jumpStartY + 0.8f;
        practiceTarget.transform.position = practicePosition;
        practiceTarget.transform.localScale = Vector3.one * 0.45f;
        Collider collider = practiceTarget.GetComponent<Collider>();
        if (collider != null) collider.isTrigger = true;
        Renderer renderer = practiceTarget.GetComponent<Renderer>();
        if (renderer != null) renderer.material.color = new Color(1f, 0.75f, 0.18f);
        GameObject label = new GameObject("Practice Label", typeof(TextMeshPro));
        label.transform.SetParent(practiceTarget.transform, false);
        label.transform.localPosition = new Vector3(0f, 1.3f, 0f);
        label.transform.localScale = Vector3.one * 0.7f;
        practiceLabel = label.GetComponent<TextMeshPro>();
        practiceLabel.text = "PRACTICE";
        practiceLabel.fontSize = 5f;
        practiceLabel.alignment = TextAlignmentOptions.Center;
        practiceLabel.color = Color.white;
    }

    private void DestroyPracticeTarget()
    {
        if (practiceTarget != null) Destroy(practiceTarget);
        practiceTarget = null;
        practiceLabel = null;
    }

    private void ConfirmNo()
    {
        if (!IsActive || step != Step.Confirm) return;
        RestoreDemonstrationUI();
        BeginMovement();
    }

    private void ConfirmYes()
    {
        if (!IsActive || step != Step.Confirm) return;
        Cleanup();
    }

    private void Cleanup()
    {
        RestoreDemonstrationUI();
        DestroyPracticeTarget();
        timer?.SetTutorialPaused(false);
        if (input != null) input.SetGameplayInputBlocked(false);
        mobileControls?.ResetJoystick();
        if (highlight != null) Destroy(highlight.gameObject);
        if (worldHighlight != null) Destroy(worldHighlight.gameObject);
        if (instructions != null) Destroy(instructions.gameObject);
        if (confirmation != null) Destroy(confirmation);
        highlight = null;
        worldHighlight = null;
        instructions = null;
        confirmation = null;
        IsActive = false;
        if (Active == this) Active = null;
        if (helpButton != null && StageSelectionState.SelectedStage == 1)
            helpButton.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (IsActive) Cleanup();
    }
}
