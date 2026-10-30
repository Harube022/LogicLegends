using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One-time control practice between the Setssss field guide and the first book start.</summary>
[DisallowMultipleComponent]
public sealed class SetssssControlsOnboarding : MonoBehaviour
{
    private enum Step { Movement, Jump, Interaction, Book, Timer, ChallengeIndicator, Spawner, ViewBoard, Board, Placement, PlacementSubmit, Differences, Complement, Confirm }

    [SerializeField, Range(0.15f, 0.8f)] private float joystickDeadZone = 0.3f;
    [SerializeField, Min(0.1f)] private float movementDistance = 0.35f;
    [SerializeField, Min(1f)] private float practiceRange = 3.5f;
    [Header("Guided camera")]
    [SerializeField, Min(0.1f)] private float cameraTransitionSeconds = 0.7f;
    [SerializeField] private Vector3 bookCameraOffset = new Vector3(0f, 2f, -6.5f);
    [SerializeField] private Vector3 spawnerCameraOffset = new Vector3(0f, 19f, -1.5f);
    [SerializeField] private Vector3 boardCameraOffset = new Vector3(0f, 0f, -19.4f);
    [SerializeField] private Vector3 diagramCameraOffset = new Vector3(0f, 55f, 0f);

    public static SetssssControlsOnboarding Active { get; private set; }
    public bool IsActive { get; private set; }
    public bool AllowsJump => IsActive && step == Step.Jump;
    public bool SuppressMovement => IsActive && step >= Step.Book;
    public bool IsBookPractice => IsActive && step == Step.Book;
    public bool WantsPracticeBoardButton => IsActive && step == Step.ViewBoard;

    private Step step;
    private RectTransform safeArea, joystick, jumpButton, handButton, timerRect, viewBoardRect;
    private RectTransform challengeRect;
    private SetssssHudView hud;
    private RectTransform highlight, arrow, instructions, worldHighlight;
    private TextMeshProUGUI instructionText;
    private Button nextButton;
    private GameObject confirmation;
    private MobileInputUI mobileControls;
    private Player player;
    private GameInput input;
    private Button helpButton;
    private Vector3 movementStart;
    private float jumpStartY;
    private bool jumpRequested, originalSettingsInteractable, previousInputBlocked;
    private Button settingsButton;
    private GameObject joystickRoot, jumpRoot, handRoot;
    private bool joystickWasVisible, jumpWasVisible, handWasVisible;
    private Renderer[] playerRenderers;
    private bool[] playerRendererStates;
    private TextMeshPro bookPrompt;
    private bool bookPromptWasVisible;
    private Transform bookTarget, spawnerTarget, boardTarget, diagramTarget, universalTarget;
    private TextMeshPro boardText;
    private string originalBoardText;
    private TMP_Text timerText;
    private string originalTimerText;
    private bool originalTimerVisible;
    private readonly List<Renderer> worldRenderers = new List<Renderer>();
    private readonly List<GameObject> sampleBlocks = new List<GameObject>();
    private readonly List<Behaviour> pausedCameraInputs = new List<Behaviour>();
    private readonly RectTransform[] regionMarkers = new RectTransform[4];
    private CinemachineCamera tutorialCamera;
    private CinemachineBrain cameraBrain;
    private CinemachineBlendDefinition previousBlend;
    private Vector3 cameraGoal;
    private Quaternion cameraRotationGoal;

    public void Begin(Button existingHelpButton)
    {
        if (IsActive || SetssssChallengeOne.ActiveInstance == null ||
            SetssssChallengeOne.ActiveInstance.Started) return;
        player = Player.LocalInstance;
        input = GameInput.Instance;
        helpButton = existingHelpButton;
        safeArea = transform.Find("SafeArea") as RectTransform;
        Transform joystickHost = safeArea != null ? safeArea.Find("Gameplay_Interface/Joystick") : null;
        mobileControls = joystickHost != null ? joystickHost.GetComponentInChildren<MobileInputUI>(true) : null;
        // The prefab's Joystick and Background rectangles include large transparent
        // touch padding. Track the visible ring at the Background's center instead.
        if (mobileControls != null && mobileControls.transform is RectTransform joystickBackground)
        {
            joystick = StageUITheme.Rect("Setssss Visible Joystick Target", joystickBackground);
            joystick.anchorMin = joystick.anchorMax = joystick.pivot = new Vector2(0.5f, 0.5f);
            joystick.anchoredPosition = Vector2.zero;
            float diameter = Mathf.Min(joystickBackground.rect.width, joystickBackground.rect.height) * 0.9f;
            joystick.sizeDelta = new Vector2(diameter, diameter);
        }
        jumpButton = safeArea != null ? safeArea.Find("Gameplay_Interface/JumpButton") as RectTransform : null;
        handButton = safeArea != null ? safeArea.Find("Gameplay_Interface/InteractButton") as RectTransform : null;
        joystickRoot = joystickHost != null ? joystickHost.gameObject : null;
        jumpRoot = jumpButton != null ? jumpButton.gameObject : null;
        handRoot = handButton != null ? handButton.gameObject : null;
        hud = SetssssChallengeOne.ActiveInstance.Hud;
        timerRect = hud != null ? hud.Timer.rectTransform : null;
        challengeRect = hud != null ? hud.Challenge.rectTransform : null;
        viewBoardRect = safeArea != null ? safeArea.Find("ViewBoardButton") as RectTransform : null;
        bookTarget = GameObject.Find("Book_Statue")?.transform;
        bookPrompt = bookTarget != null ? bookTarget.Find("Challenge Start Prompt")?.GetComponent<TextMeshPro>() : null;
        spawnerTarget = GameObject.Find("Spawner")?.transform;
        boardTarget = GameObject.Find("Board 1")?.transform;
        diagramTarget = GameObject.Find("AB")?.transform;
        universalTarget = GameObject.Find("BORDERLINES")?.transform;
        boardText = boardTarget != null ? boardTarget.GetComponentInChildren<TextMeshPro>(true) : null;
        timerText = timerRect != null ? timerRect.GetComponent<TMP_Text>() : null;
        if (player == null || input == null || safeArea == null || mobileControls == null ||
            jumpButton == null || handButton == null || bookTarget == null || spawnerTarget == null ||
            boardTarget == null || diagramTarget == null || universalTarget == null ||
            boardText == null || timerText == null || viewBoardRect == null ||
            challengeRect == null)
        {
            Debug.LogError("Setssss tutorial needs the player, Canvas 1 controls, timer, book, spawner, board, and diagram.", this);
            return;
        }

        previousInputBlocked = input.GameplayInputBlocked;
        originalBoardText = boardText.text;
        originalTimerText = timerText.text;
        originalTimerVisible = timerText.gameObject.activeSelf;
        joystickWasVisible = joystickRoot.activeSelf;
        jumpWasVisible = jumpRoot.activeSelf;
        handWasVisible = handRoot.activeSelf;
        bookPromptWasVisible = bookPrompt != null && bookPrompt.gameObject.activeSelf;
        settingsButton = safeArea.Find("Gameplay_Interface/SettingsButton")?.GetComponent<Button>();
        if (settingsButton != null)
        {
            originalSettingsInteractable = settingsButton.interactable;
            settingsButton.interactable = false;
        }
        if (helpButton != null) helpButton.gameObject.SetActive(false);
        BuildUI();
        Active = this;
        IsActive = true;
        input.SetGameplayInputBlocked(false);
        BeginMovement();
    }

    private void BuildUI()
    {
        instructions = StageUITheme.Rect("Setssss Control Instructions", safeArea);
        Image instructionPanel = instructions.gameObject.AddComponent<Image>();
        instructionPanel.color = new Color(0.06f, 0.13f, 0.19f, 0.95f);
        instructionPanel.raycastTarget = false;
        instructionText = (TextMeshProUGUI)StageUITheme.Text("Control Instruction", instructions, "", 36);
        instructionText.alignment = TextAlignmentOptions.Center;
        instructionText.color = Color.white;
        instructionText.enableAutoSizing = true;
        instructionText.fontSizeMin = 28f;
        instructionText.fontSizeMax = 38f;
        instructionText.raycastTarget = false;
        nextButton = MakePrelimButton("Next", instructions, NextStep);
        nextButton.gameObject.SetActive(false);

        highlight = StageUITheme.Rect("Setssss Control Highlight", safeArea);
        MakeBorder(highlight);
        TextMeshProUGUI arrowText = (TextMeshProUGUI)StageUITheme.Text("Tutorial Arrow", highlight, "▼", 70);
        arrowText.color = new Color(1f, 0.9f, 0.25f);
        arrowText.alignment = TextAlignmentOptions.Center;
        arrowText.raycastTarget = false;
        arrow = arrowText.rectTransform;
        arrow.anchorMin = arrow.anchorMax = new Vector2(0.5f, 1f);
        arrow.pivot = new Vector2(0.5f, 0f);
        arrow.sizeDelta = new Vector2(100f, 85f);
        arrow.anchoredPosition = new Vector2(0f, 12f);

        worldHighlight = StageUITheme.Rect("Setssss World Highlight", safeArea);
        worldHighlight.anchorMin = worldHighlight.anchorMax = worldHighlight.pivot = new Vector2(0.5f, 0.5f);
        MakeBorder(worldHighlight);
        worldHighlight.gameObject.SetActive(false);

        RectTransform shade = StageUITheme.Rect("Setssss Tutorial Confirmation", safeArea);
        StageUITheme.Stretch(shade, Vector2.zero, Vector2.one);
        shade.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        confirmation = shade.gameObject;
        RectTransform card = StageUITheme.Rect("Ready Card", shade);
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(660f, 280f);
        Image cardImage = card.gameObject.AddComponent<Image>();
        cardImage.color = new Color(0.06f, 0.16f, 0.22f, 0.98f);
        TextMeshProUGUI question = (TextMeshProUGUI)StageUITheme.Text("Ready Question", card, "Let's start?", 48);
        StageUITheme.Stretch(question.rectTransform, new Vector2(0.08f, 0.54f), new Vector2(0.92f, 0.84f));
        question.alignment = TextAlignmentOptions.Center;
        question.color = Color.white;
        Button yes = MakePrelimButton("Yes", card, ConfirmYes);
        Button no = MakePrelimButton("No", card, ConfirmNo);
        RectTransform yesRect = (RectTransform)yes.transform;
        RectTransform noRect = (RectTransform)no.transform;
        yesRect.anchorMin = yesRect.anchorMax = noRect.anchorMin = noRect.anchorMax = new Vector2(0.5f, 0.5f);
        yesRect.anchoredPosition = new Vector2(-155f, -71f);
        noRect.anchoredPosition = new Vector2(155f, -71f);
        confirmation.SetActive(false);

        string[] labels = { "A", "B", "A ∩ B", "U" };
        for (int i = 0; i < regionMarkers.Length; i++)
        {
            RectTransform marker = StageUITheme.Rect("Sets Region " + labels[i], safeArea);
            marker.anchorMin = marker.anchorMax = marker.pivot = new Vector2(0.5f, 0.5f);
            marker.sizeDelta = new Vector2(i == 2 ? 124f : 94f, 64f);
            Image background = marker.gameObject.AddComponent<Image>();
            background.color = new Color(0.03f, 0.1f, 0.14f, 0.78f);
            background.raycastTarget = false;
            MakeBorder(marker);
            TextMeshProUGUI label = (TextMeshProUGUI)StageUITheme.Text("Region Name", marker, labels[i], 34);
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            StageUITheme.Stretch(label.rectTransform, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f));
            regionMarkers[i] = marker;
            marker.gameObject.SetActive(false);
        }
        PrelimTutorialSkipControl.Create(safeArea, SkipTutorial);
    }

    private static Button MakePrelimButton(string text, Transform parent, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = StageUITheme.Rect(text + " Button", parent);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(250f, 92f);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.12f, 0.48f, 0.43f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.onClick.AddListener(action);
        TextMeshProUGUI label = (TextMeshProUGUI)StageUITheme.Text("Button Label", rect, text, 40);
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        StageUITheme.Stretch(label.rectTransform, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.96f));
        return button;
    }

    private static void MakeBorder(RectTransform target)
    {
        Color gold = StageUITheme.Gold;
        for (int i = 0; i < 4; i++)
        {
            RectTransform edge = StageUITheme.Rect("Highlight Edge", target);
            Image image = edge.gameObject.AddComponent<Image>();
            image.color = gold;
            image.raycastTarget = false;
            if (i == 0) { edge.anchorMin = new Vector2(0f, 1f); edge.anchorMax = Vector2.one; edge.sizeDelta = new Vector2(0f, 7f); }
            if (i == 1) { edge.anchorMin = Vector2.zero; edge.anchorMax = new Vector2(1f, 0f); edge.sizeDelta = new Vector2(0f, 7f); }
            if (i == 2) { edge.anchorMin = Vector2.zero; edge.anchorMax = new Vector2(0f, 1f); edge.sizeDelta = new Vector2(7f, 0f); }
            if (i == 3) { edge.anchorMin = new Vector2(1f, 0f); edge.anchorMax = Vector2.one; edge.sizeDelta = new Vector2(7f, 0f); }
            edge.anchoredPosition = Vector2.zero;
        }
    }

    private void ShowStep(Step next, string message, RectTransform target,
        Transform worldTarget = null, bool showNext = false)
    {
        step = next;
        instructionText.text = message;
        nextButton.gameObject.SetActive(showNext);
        instructions.gameObject.SetActive(true);
        confirmation.SetActive(false);
        if (target != null)
        {
            highlight.SetParent(target, false);
            highlight.anchorMin = Vector2.zero;
            highlight.anchorMax = Vector2.one;
            bool hudTarget = target == timerRect || target == challengeRect;
            float padding = hudTarget ? 3f : 10f;
            highlight.offsetMin = new Vector2(-padding, -padding);
            highlight.offsetMax = new Vector2(padding, padding);
            foreach (Image edge in highlight.GetComponentsInChildren<Image>())
            {
                RectTransform edgeRect = edge.rectTransform;
                Vector2 size = edgeRect.sizeDelta;
                if (edgeRect.anchorMin.x == edgeRect.anchorMax.x) size.x = hudTarget ? 3f : 7f;
                else size.y = hudTarget ? 3f : 7f;
                edgeRect.sizeDelta = size;
            }
            highlight.SetAsLastSibling();
            highlight.gameObject.SetActive(true);
        }
        else highlight.gameObject.SetActive(false);
        arrow.gameObject.SetActive(target != null);
        worldRenderers.Clear();
        if (worldTarget != null) worldRenderers.AddRange(worldTarget.GetComponentsInChildren<Renderer>(true));
        worldHighlight.gameObject.SetActive(false);
        foreach (RectTransform marker in regionMarkers) marker.gameObject.SetActive(false);
        instructions.SetAsLastSibling();
        LayoutInstructions();
    }

    private void BeginMovement()
    {
        // Idempotently restore the same shared control on every replay.
        PrelimTutorialSkipControl.Create(safeArea, SkipTutorial);
        DestroySampleBlocks();
        RestoreBoardAndTimer();
        RestoreGameplayCamera();
        RestoreGameplayControls();
        mobileControls.ResetJoystick();
        input.SetGameplayInputBlocked(false);
        movementStart = player.transform.position;
        ShowStep(Step.Movement, "Use this joystick to move.", joystick);
    }

    private void BeginJump()
    {
        jumpRequested = false;
        jumpStartY = player.transform.position.y;
        ShowStep(Step.Jump, "Tap this button to jump.", jumpButton);
    }

    private void BeginInteraction()
    {
        ShowStep(Step.Interaction, "Tap the hand button to interact with nearby objects.", handButton);
    }

    private void BeginBook()
    {
        if (bookPrompt != null) bookPrompt.gameObject.SetActive(false);
        SetGuidedControls(true);
        FocusCamera(bookTarget, bookCameraOffset, false);
        ShowStep(Step.Book, "Interact with this book statue to start each challenge.", null, bookTarget);
    }

    private void BeginTimer()
    {
        SetGuidedControls(false);
        timerText.gameObject.SetActive(true);
        timerText.text = "18:00";
        ShowStep(Step.Timer,
            "Each challenge gives you 18 minutes. The timer starts when you activate the book statue and resets for the next challenge. An incorrect submission subtracts 10 seconds.",
            timerRect, null, true);
    }

    private void BeginChallengeIndicator()
    {
        ShowStep(Step.ChallengeIndicator,
            "Five challenges. Two questions each.\nUse the book to start the next challenge. Read each question on the board.",
            challengeRect, null, true);
    }

    private void BeginSpawner()
    {
        CreateSampleBlocks();
        FocusCamera(spawnerTarget, spawnerCameraOffset, true);
        ShowStep(Step.Spawner,
            "Numbered blocks represent elements.\nChallenges 1–4 offer ten blocks, numbered 1–99. They include every needed element.\nCollect the elements listed on the board.",
            null, spawnerTarget, true);
    }

    private void BeginViewBoard()
    {
        ShowStep(Step.ViewBoard,
            "When you are near the spawner, tap View Board to read the current question and its required sets on the Information Board.",
            viewBoardRect);
    }

    public void OnPracticeViewBoardPressed()
    {
        if (WantsPracticeBoardButton) BeginBoard();
    }

    private void BeginBoard()
    {
        DestroySampleBlocks();
        boardText.text = "<color=#F4D794>PRACTICE QUESTION</color>\nA = {12}     B = {27}";
        FocusCamera(boardTarget, boardCameraOffset, false);
        ShowStep(Step.Board,
            "The board shows the question, sets and placement rules.\nThis preview is practice only. Use the book to reveal the real question.",
            null, boardTarget, true);
    }

    private void BeginPlacement()
    {
        FocusCamera(universalTarget, diagramCameraOffset, true);
        ShowStep(Step.Placement,
            "A and B are sets represented by circles.\nPlace each element where the question requires.\nA ∩ B is their shared overlap.",
            null, universalTarget, true);
        foreach (RectTransform marker in regionMarkers) marker.gameObject.SetActive(true);
    }

    private void BeginPlacementSubmit()
    {
        ShowStep(Step.PlacementSubmit,
            "Submit checks settled positions. Each block counts once.\nGreen: correct. Red: fix. Wrong: −10s; blocks stay.",
            null, universalTarget, true);
        foreach (RectTransform marker in regionMarkers) marker.gameObject.SetActive(true);
    }

    private void BeginDifferences()
    {
        ShowStep(Step.Differences,
            "A − B: in A, not B. B − A: in B, not A.\nChallenge 4 also needs full A, full B and A ∩ B placements.\nCollect separate copies from the replenishing spawner.",
            null, universalTarget, true);
    }

    private void BeginComplement()
    {
        ShowStep(Step.Complement,
            "U is the universal set.\nA complement is inside U but outside the named set.\nMove each supplied element to the area required on the board.",
            null, universalTarget, true);
    }

    public void NextStep()
    {
        if (!IsActive) return;
        switch (step)
        {
            case Step.Timer: BeginChallengeIndicator(); break;
            case Step.ChallengeIndicator: BeginSpawner(); break;
            case Step.Spawner: BeginViewBoard(); break;
            case Step.Board: BeginPlacement(); break;
            case Step.Placement: BeginPlacementSubmit(); break;
            case Step.PlacementSubmit: BeginDifferences(); break;
            case Step.Differences: BeginComplement(); break;
            case Step.Complement: BeginConfirmation(); break;
        }
    }

    public void SkipTutorial()
    {
        if (IsActive && step != Step.Confirm) BeginConfirmation();
    }

    private void Update()
    {
        if (!IsActive || player == null) return;
        if (step == Step.Movement && mobileControls.CurrentInput.sqrMagnitude > joystickDeadZone * joystickDeadZone &&
            Vector2.Distance(new Vector2(movementStart.x, movementStart.z),
                new Vector2(player.transform.position.x, player.transform.position.z)) >= movementDistance)
            BeginJump();
        else if (step == Step.Jump && jumpRequested && player.IsJumping() &&
                 player.transform.position.y > jumpStartY + 0.1f)
            BeginInteraction();

        if (tutorialCamera != null)
        {
            float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 4f / cameraTransitionSeconds);
            tutorialCamera.transform.position = Vector3.Lerp(tutorialCamera.transform.position, cameraGoal, blend);
            tutorialCamera.transform.rotation = Quaternion.Slerp(tutorialCamera.transform.rotation, cameraRotationGoal, blend);
        }
        foreach (GameObject block in sampleBlocks)
            if (block != null && Camera.main != null)
            {
                TextMeshPro label = block.GetComponentInChildren<TextMeshPro>();
                if (label != null) label.transform.rotation = Camera.main.transform.rotation;
            }
    }

    private void LateUpdate()
    {
        if (!IsActive || step == Step.Confirm) return;
        UpdateWorldHighlight();
        if (step == Step.Placement || step == Step.PlacementSubmit) UpdateRegionMarkers();
        LayoutInstructions();
    }

    private void LayoutInstructions()
    {
        if (instructions == null || safeArea == null || !instructions.gameObject.activeSelf) return;
        if (step == Step.Placement || step == Step.PlacementSubmit)
        {
            instructions.anchorMin = instructions.anchorMax = new Vector2(0.5f, 0f);
            instructions.pivot = new Vector2(0.5f, 0f);
            instructions.anchoredPosition = new Vector2(0f, 20f);
            instructions.sizeDelta = new Vector2(Mathf.Min(970f, safeArea.rect.width - 40f), 245f);
            instructionText.enableAutoSizing = true;
            instructionText.fontSizeMin = 28f;
            instructionText.fontSizeMax = 36f;
            instructionText.rectTransform.anchorMin = Vector2.zero;
            instructionText.rectTransform.anchorMax = Vector2.one;
            instructionText.rectTransform.offsetMin = new Vector2(22f, 110f);
            instructionText.rectTransform.offsetMax = new Vector2(-22f, -16f);
            RectTransform next = (RectTransform)nextButton.transform;
            next.anchorMin = next.anchorMax = next.pivot = new Vector2(0.5f, 0f);
            next.sizeDelta = new Vector2(210f, 80f);
            next.anchoredPosition = new Vector2(0f, 16f);
            return;
        }
        RectTransform target = step == Step.Movement ? joystick :
            step == Step.Jump ? jumpButton : step == Step.Interaction ? handButton :
            step == Step.Timer ? timerRect : step == Step.ChallengeIndicator ? challengeRect :
            step == Step.ViewBoard ? viewBoardRect :
            worldHighlight.gameObject.activeSelf ? worldHighlight : null;
        bool uiStep = step == Step.Movement || step == Step.Jump || step == Step.Interaction ||
                      step == Step.Timer || step == Step.ChallengeIndicator || step == Step.ViewBoard;
        bool hudStep = step == Step.Timer || step == Step.ChallengeIndicator;
        var blockers = new List<RectTransform>(ControlTutorialLayout.FindBlockers());
        if (hudStep)
        {
            // Keep HUD arrows outside the compact panel, aligned with the actual
            // target's center, so the timer and progress stay clear.
            Rect panelBounds = SetssssHudView.RelativeBounds(hud.Hud, highlight);
            arrow.anchorMin = arrow.anchorMax = new Vector2(0f, .5f);
            arrow.pivot = new Vector2(.5f, .5f);
            arrow.anchoredPosition = new Vector2(panelBounds.xMin - highlight.rect.xMin - 24f, 0f);
            arrow.sizeDelta = new Vector2(36f, 32f);
            arrow.localRotation = Quaternion.Euler(0f, 0f, 90f);
            arrow.GetComponent<TMP_Text>().fontSize = 32f;
            blockers.Add(arrow);
        }
        ControlTutorialLayout.Place(instructions, instructionText, (RectTransform)nextButton.transform,
            safeArea, target, false, blockers.ToArray(), uiStep && !hudStep ? arrow : null);
    }

    private void SetGuidedControls(bool showHand)
    {
        if (joystickRoot != null) joystickRoot.SetActive(false);
        if (jumpRoot != null) jumpRoot.SetActive(false);
        if (handRoot != null) handRoot.SetActive(showHand && handWasVisible);
    }

    private void RestoreGameplayControls()
    {
        if (joystickRoot != null) joystickRoot.SetActive(joystickWasVisible);
        if (jumpRoot != null) jumpRoot.SetActive(jumpWasVisible);
        if (handRoot != null) handRoot.SetActive(handWasVisible);
    }

    private void UpdateWorldHighlight()
    {
        if (worldRenderers.Count == 0 || Camera.main == null)
        { worldHighlight.gameObject.SetActive(false); return; }
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        bool found = false;
        foreach (Renderer renderer in worldRenderers)
        {
            if (renderer == null || !renderer.gameObject.activeInHierarchy) continue;
            Bounds bounds = renderer.bounds;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                Vector3 screen = Camera.main.WorldToScreenPoint(point);
                if (screen.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        safeArea, screen, null, out Vector2 local)) continue;
                min = Vector2.Min(min, local); max = Vector2.Max(max, local); found = true;
            }
        }
        worldHighlight.gameObject.SetActive(found);
        if (!found) return;
        worldHighlight.anchoredPosition = (min + max) * 0.5f;
        worldHighlight.sizeDelta = max - min + new Vector2(28f, 28f);
    }

    private void UpdateRegionMarkers()
    {
        if (Camera.main == null) return;
        Bounds diagram = BoundsOf(diagramTarget);
        Bounds universe = BoundsOf(universalTarget);
        Vector3[] positions =
        {
            new Vector3(diagram.center.x - diagram.extents.x * 0.47f, diagram.max.y + 0.2f, diagram.center.z),
            new Vector3(diagram.center.x + diagram.extents.x * 0.47f, diagram.max.y + 0.2f, diagram.center.z),
            new Vector3(diagram.center.x, diagram.max.y + 0.2f, diagram.center.z),
            new Vector3(universe.max.x - 4f, universe.max.y + 0.2f, universe.min.z + 4f)
        };
        for (int i = 0; i < regionMarkers.Length; i++)
        {
            Vector3 screen = Camera.main.WorldToScreenPoint(positions[i]);
            Vector2 local = Vector2.zero;
            bool visible = screen.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                safeArea, screen, null, out local);
            regionMarkers[i].gameObject.SetActive(visible);
            if (visible) regionMarkers[i].anchoredPosition = local + Vector2.up * 45f;
        }
    }

    private static Bounds BoundsOf(Transform target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(target.position, Vector3.one);
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private void FocusCamera(Transform target, Vector3 offset, bool overhead)
    {
        if (target == null || Camera.main == null) return;
        if (tutorialCamera == null)
        {
            PauseCameraInput();
            playerRenderers = player.GetComponentsInChildren<Renderer>(true);
            playerRendererStates = new bool[playerRenderers.Length];
            for (int i = 0; i < playerRenderers.Length; i++)
            {
                playerRendererStates[i] = playerRenderers[i].enabled;
                playerRenderers[i].enabled = false;
            }
            GameObject cameraObject = new GameObject("Sets Tutorial Camera");
            tutorialCamera = cameraObject.AddComponent<CinemachineCamera>();
            CinemachineCamera source = GameObject.Find("CM_ThirdPersonCam")?.GetComponent<CinemachineCamera>();
            if (source != null) tutorialCamera.Lens = source.Lens;
            tutorialCamera.transform.SetPositionAndRotation(Camera.main.transform.position, Camera.main.transform.rotation);
            tutorialCamera.Priority = new PrioritySettings { Enabled = true, Value = 150 };
            cameraBrain = Camera.main.GetComponent<CinemachineBrain>();
            if (cameraBrain != null)
            {
                previousBlend = cameraBrain.DefaultBlend;
                cameraBrain.DefaultBlend = new CinemachineBlendDefinition(
                    CinemachineBlendDefinition.Styles.EaseInOut, cameraTransitionSeconds);
            }
        }
        Vector3 focus = BoundsOf(target).center;
        cameraGoal = focus + offset;
        cameraRotationGoal = Quaternion.LookRotation(focus - cameraGoal,
            overhead ? Vector3.forward : Vector3.up);
    }

    private void PauseCameraInput()
    {
        foreach (CinemachineInputAxisController behaviour in FindObjectsByType<CinemachineInputAxisController>(FindObjectsSortMode.None))
            PauseBehaviour(behaviour);
        foreach (CinemachinePinchZoom behaviour in FindObjectsByType<CinemachinePinchZoom>(FindObjectsSortMode.None))
            PauseBehaviour(behaviour);
        foreach (ThirdPersonCameraController behaviour in FindObjectsByType<ThirdPersonCameraController>(FindObjectsSortMode.None))
            PauseBehaviour(behaviour);
    }

    private void PauseBehaviour(Behaviour behaviour)
    {
        if (behaviour == null || !behaviour.enabled) return;
        behaviour.enabled = false;
        pausedCameraInputs.Add(behaviour);
    }

    private void RestoreGameplayCamera()
    {
        if (tutorialCamera != null) PrelimTutorialSkipControl.CancelCamera(tutorialCamera);
        tutorialCamera = null;
        if (cameraBrain != null) cameraBrain.DefaultBlend = previousBlend;
        cameraBrain = null;
        foreach (Behaviour behaviour in pausedCameraInputs)
            if (behaviour != null) behaviour.enabled = true;
        pausedCameraInputs.Clear();
        if (playerRenderers != null && playerRendererStates != null)
            for (int i = 0; i < playerRenderers.Length; i++)
                if (playerRenderers[i] != null) playerRenderers[i].enabled = playerRendererStates[i];
        playerRenderers = null;
        playerRendererStates = null;
    }

    private void CreateSampleBlocks()
    {
        DestroySampleBlocks();
        int[] values = { 12, 27, 45, 68, 31, 54, 76, 89, 5, 99 };
        Vector3[] offsets =
        {
            new Vector3(-4.95f, 1.84f, -2.85f), new Vector3(-1.65f, 1.84f, -2.85f),
            new Vector3(1.65f, 1.84f, -2.85f), new Vector3(4.95f, 1.84f, -2.85f),
            new Vector3(-4.95f, 1.84f, 0.75f), new Vector3(-1.65f, 1.84f, 0.75f),
            new Vector3(1.65f, 1.84f, 0.75f), new Vector3(4.95f, 1.84f, 0.75f),
            new Vector3(-1.65f, 1.84f, 4.35f), new Vector3(1.65f, 1.84f, 4.35f)
        };
        for (int i = 0; i < values.Length; i++)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "Sets Tutorial Sample " + values[i];
            block.transform.SetParent(spawnerTarget, false);
            block.transform.localPosition = offsets[i];
            block.transform.localScale = Vector3.one * 1.15f;
            Collider collider = block.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            Renderer renderer = block.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = Color.Lerp(
                new Color(0.05f, 0.55f, 0.66f), new Color(0.9f, 0.58f, 0.18f), (i % 4) / 3f);
            GameObject labelObject = new GameObject("Sample Number", typeof(TextMeshPro));
            labelObject.transform.SetParent(block.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            TextMeshPro label = labelObject.GetComponent<TextMeshPro>();
            label.text = values[i].ToString();
            label.fontSize = 5f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.outlineWidth = 0.25f;
            label.outlineColor = Color.black;
            label.rectTransform.sizeDelta = new Vector2(4f, 2f);
            sampleBlocks.Add(block);
        }
    }

    private void DestroySampleBlocks()
    {
        foreach (GameObject block in sampleBlocks) if (block != null) Destroy(block);
        sampleBlocks.Clear();
    }

    public void NoteJumpInput() { if (IsActive && step == Step.Jump) jumpRequested = true; }

    public void TryPracticeInteraction()
    {
        if (!IsActive) return;
        if (step == Step.Book) { BeginTimer(); return; }
        if (step != Step.Interaction) return;
        BeginBook();
    }

    private void BeginConfirmation()
    {
        step = Step.Confirm;
        DestroySampleBlocks();
        RestoreBoardAndTimer();
        RestoreGameplayCamera();
        RestoreGameplayControls();
        highlight.gameObject.SetActive(false);
        worldHighlight.gameObject.SetActive(false);
        foreach (RectTransform marker in regionMarkers) marker.gameObject.SetActive(false);
        instructions.gameObject.SetActive(false);
        Transform skip = safeArea.Find("PRELIM Shared Skip Tutorial");
        if (skip != null) skip.gameObject.SetActive(false);
        confirmation.SetActive(true);
        confirmation.transform.SetAsLastSibling();
        mobileControls.ResetJoystick();
        input.SetGameplayInputBlocked(true);
    }

    public void ConfirmNo()
    {
        if (!IsActive || step != Step.Confirm) return;
        Transform skip = safeArea.Find("PRELIM Shared Skip Tutorial");
        if (skip != null) skip.gameObject.SetActive(true);
        BeginMovement();
    }

    public void ConfirmYes()
    {
        if (IsActive && step == Step.Confirm) Cleanup();
    }

    private void Cleanup()
    {
        DestroySampleBlocks();
        RestoreBoardAndTimer();
        RestoreGameplayCamera();
        RestoreGameplayControls();
        mobileControls?.ResetJoystick();
        if (input != null) input.SetGameplayInputBlocked(previousInputBlocked);
        if (settingsButton != null) settingsButton.interactable = originalSettingsInteractable;
        PrelimTutorialSkipControl skip = safeArea != null ?
            safeArea.Find("PRELIM Shared Skip Tutorial")?.GetComponent<PrelimTutorialSkipControl>() : null;
        if (skip != null) skip.Release();
        if (highlight != null) Destroy(highlight.gameObject);
        if (worldHighlight != null) Destroy(worldHighlight.gameObject);
        if (joystick != null) Destroy(joystick.gameObject);
        if (instructions != null) Destroy(instructions.gameObject);
        if (confirmation != null) Destroy(confirmation);
        foreach (RectTransform marker in regionMarkers) if (marker != null) Destroy(marker.gameObject);
        highlight = null;
        worldHighlight = null;
        instructions = null;
        confirmation = null;
        IsActive = false;
        if (Active == this) Active = null;
        if (helpButton != null) helpButton.gameObject.SetActive(true);
    }

    private void RestoreBoardAndTimer()
    {
        if (boardText != null) boardText.text = originalBoardText;
        if (bookPrompt != null) bookPrompt.gameObject.SetActive(bookPromptWasVisible);
        if (timerText != null)
        {
            timerText.text = originalTimerText;
            timerText.gameObject.SetActive(originalTimerVisible);
        }
    }

    private void OnDisable() { if (IsActive) Cleanup(); }
}
