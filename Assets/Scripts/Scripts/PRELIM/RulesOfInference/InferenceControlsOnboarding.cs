using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Cinemachine;

/// <summary>One-time control practice after the Rules of Inference field guide.</summary>
[DefaultExecutionOrder(1100)]
public sealed class InferenceControlsOnboarding : MonoBehaviour
{
    private enum Step { Movement, Jump, Interaction, Book, Timer, Minimap,
        BoardOverview, BoardApproach, BoardPremises, BoardDiamond, BoardConclusion,
        BoardBack, CrystalTower, Confirm }

    [SerializeField, Range(0.15f, 0.8f)] private float joystickDeadZone = 0.3f;
    [SerializeField, Min(0.1f)] private float movementDistance = 0.35f;
    [SerializeField, Min(1f)] private float practiceRange = 2.8f;
    [SerializeField, Min(0.1f)] private float cameraBlendSeconds = 1.2f;
    [SerializeField] private Vector3 bookCameraOffset = new Vector3(-5f, 2.5f, 10f);
    [SerializeField] private Vector3 boardCameraOffset = new Vector3(0f, 3f, 55f);
    [SerializeField] private Vector3 boardCameraFocusOffset = new Vector3(0f, 23f, 0f);
    [SerializeField] private Vector3 boardHighlightOffset = new Vector3(0f, 28f, 0f);
    [SerializeField] private Vector3 crystalCameraOffset = new Vector3(8f, 3f, 25f);
    [SerializeField] private Transform crystalTowerOverride;

    public static InferenceControlsOnboarding Active { get; private set; }
    public bool IsActive { get; private set; }
    public bool AllowsJump => IsActive && step == Step.Jump;
    public bool SuppressMovement => IsActive && step != Step.Movement && step != Step.BoardApproach;
    public bool IsBookStep => IsActive && step == Step.Book;
    public bool IsBoardApproach => IsActive && step == Step.BoardApproach;
    public bool IsPracticeBoardOpen => IsActive && boardPanel != null && boardPanel.gameObject.activeSelf;
    public string CurrentStep => step.ToString();

    private Step step;
    private GameInput input;
    private Player player;
    private MobileInputUI mobileControls;
    private Button helpButton, settingsButton, skipButton, nextButton;
    private bool previousInputBlocked, previousSettingsInteractable;
    private RectTransform safeArea, joystick, jumpButton, handButton, worldAnchor;
    private RectTransform highlight, arrow, instructions, boardPanel, boardSectionTarget;
    private RectTransform[] instructionBlockers;
    private TextMeshProUGUI instructionText;
    private RectTransform instructionTarget;
    private GameObject confirmation, practiceTarget;
    private TextMeshPro practiceLabel;
    private GameObject practiceCrystal;
    private TextMeshProUGUI crystalScreenLabel;
    private Transform worldTarget, bookTarget, altarTarget, crystalTarget;
    private Transform boardCameraFocus, boardHighlightTarget;
    private RectTransform minimapTarget;
    private Button boardBackButton, boardNextButton;
    private RectTransform[] boardTabs;
    private TextMeshProUGUI boardExplanation;
    private CinemachineCamera tutorialCamera;
    private CinemachineBrain cameraBrain;
    private CinemachineBlendDefinition previousBlend;
    private readonly System.Collections.Generic.List<Behaviour> pausedCameraInputs =
        new System.Collections.Generic.List<Behaviour>();
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;
    private Vector3 movementStart;
    private float jumpStartY;
    private bool jumpRequested;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Active = null; }

    public void Begin(Button existingHelpButton)
    {
        if (IsActive || SceneManager.GetActiveScene().name != "RulesOfInference") return;
        input = GameInput.Instance;
        player = Player.LocalInstance;
        helpButton = existingHelpButton;
        var challenge = FindFirstObjectByType<LogicLegends.Inference.InferenceChallenge>();
        bookTarget = FindFirstObjectByType<LogicLegends.Inference.InferenceBookStart>()?.transform;
        altarTarget = GameObject.Find("ALTAR")?.transform.Find("Cube.101");
        crystalTarget = crystalTowerOverride != null ? crystalTowerOverride :
            GameObject.Find("PILLARS (2)")?.transform;
        if (input == null || player == null || !player.enabled || challenge == null ||
            challenge.HasStarted || bookTarget == null || altarTarget == null ||
            crystalTarget == null || !BuildUI())
        {
            Debug.LogError("Rules of Inference controls tutorial needs a waiting challenge, player, and mobile controls.");
            Cleanup();
            return;
        }

        previousInputBlocked = input.GameplayInputBlocked;
        boardCameraFocus = new GameObject("Inference Tutorial Board Camera Focus").transform;
        boardHighlightTarget = new GameObject("Inference Tutorial Board Highlight").transform;
        boardCameraFocus.position = altarTarget.position + boardCameraFocusOffset;
        boardHighlightTarget.position = altarTarget.position + boardHighlightOffset;
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Active = this;
        IsActive = true;
        input.SetGameplayInputBlocked(false);
        if (helpButton != null) helpButton.gameObject.SetActive(false);
        settingsButton = safeArea.Find("Gameplay_Interface/SettingsButton")?.GetComponent<Button>();
        if (settingsButton != null)
        {
            previousSettingsInteractable = settingsButton.interactable;
            settingsButton.interactable = false;
        }
        BeginMovement();
    }

    private bool BuildUI()
    {
        GameObject canvas = GameObject.Find("Canvas 1");
        safeArea = canvas != null ? canvas.transform.Find("SafeArea") as RectTransform : null;
        Transform joystickHost = safeArea != null ? safeArea.Find("Gameplay_Interface/Joystick") : null;
        mobileControls = joystickHost != null ? joystickHost.GetComponentInChildren<MobileInputUI>(true) : null;
        joystick = mobileControls != null ? mobileControls.transform as RectTransform : null;
        jumpButton = safeArea != null ? safeArea.Find("Gameplay_Interface/JumpButton") as RectTransform : null;
        handButton = safeArea != null ? safeArea.Find("Gameplay_Interface/InteractButton") as RectTransform : null;
        if (safeArea == null || joystick == null || jumpButton == null || handButton == null) return false;

        instructions = StageUITheme.Rect("Inference Controls Instructions", safeArea);
        var backing = instructions.gameObject.AddComponent<Image>();
        backing.color = new Color(0.06f, 0.13f, 0.19f, 0.94f);
        backing.raycastTarget = false;
        instructionText = (TextMeshProUGUI)StageUITheme.Text("Instruction", instructions, "", 36, true);
        instructionText.color = Color.white;
        nextButton = StageUITheme.Button("Inference Tutorial Next", instructions, "Next", NextStep);
        skipButton = StageUITheme.Button("Inference Shared Skip", safeArea, "Skip Tutorial", SkipTutorial);
        var skipRect = (RectTransform)skipButton.transform;
        skipRect.anchorMin = skipRect.anchorMax = new Vector2(0f, 1f);
        skipRect.pivot = new Vector2(0f, 1f);
        skipRect.anchoredPosition = new Vector2(380f, -30f);
        skipRect.sizeDelta = new Vector2(230f, 80f);
        skipButton.transform.SetAsLastSibling();

        worldAnchor = StageUITheme.Rect("Inference World Highlight Target", safeArea);
        worldAnchor.anchorMin = worldAnchor.anchorMax = worldAnchor.pivot = new Vector2(0.5f, 0.5f);
        worldAnchor.sizeDelta = new Vector2(150f, 150f);
        crystalScreenLabel = (TextMeshProUGUI)StageUITheme.Text("Practice Crystal Rule Label",
            worldAnchor, "PRACTICE RULE CRYSTAL", 32, true);
        crystalScreenLabel.color = StageUITheme.Gold;
        crystalScreenLabel.outlineColor = Color.black;
        crystalScreenLabel.outlineWidth = 0.25f;
        crystalScreenLabel.rectTransform.anchorMin = crystalScreenLabel.rectTransform.anchorMax =
            new Vector2(0.5f, 0.5f);
        crystalScreenLabel.rectTransform.anchoredPosition = new Vector2(0f, 120f);
        crystalScreenLabel.rectTransform.sizeDelta = new Vector2(390f, 70f);
        crystalScreenLabel.gameObject.SetActive(false);
        worldAnchor.gameObject.SetActive(false);
        minimapTarget = FindRect("TruthTableMinimapPanel");

        highlight = StageUITheme.Rect("Inference Control Highlight", safeArea);
        AddBorder(highlight);
        var arrowText = (TextMeshProUGUI)StageUITheme.Text("Arrow", highlight, "▼", 60);
        arrowText.color = new Color(1f, 0.9f, 0.25f);
        arrow = arrowText.rectTransform;

        RectTransform shade = StageUITheme.Rect("Inference Controls Confirm", safeArea);
        StageUITheme.Stretch(shade, Vector2.zero, Vector2.one);
        var shadeImage = shade.gameObject.AddComponent<Image>();
        shadeImage.color = new Color(0f, 0f, 0f, 0.65f);
        confirmation = shade.gameObject;
        RectTransform card = StageUITheme.Rect("Confirmation Card", shade);
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
        card.pivot = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(660f, 280f);
        var cardImage = card.gameObject.AddComponent<Image>();
        cardImage.color = new Color(0.06f, 0.16f, 0.22f, 0.98f);
        var question = (TextMeshProUGUI)StageUITheme.Text("Ready Question", card, "Let's start?", 48);
        question.color = Color.white;
        question.rectTransform.anchorMin = question.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        question.rectTransform.anchoredPosition = new Vector2(0f, -66f);
        question.rectTransform.sizeDelta = new Vector2(590f, 86f);
        MakeConfirmButton(card, "Yes", -155f, ConfirmYes);
        MakeConfirmButton(card, "No", 155f, ConfirmNo);
        confirmation.SetActive(false);
        BuildPracticeBoard();
        return true;
    }

    private static void MakeConfirmButton(Transform parent, string label, float x,
        UnityEngine.Events.UnityAction action)
    {
        Button button = StageUITheme.Button(label + " Button", parent, label, action);
        RectTransform rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, -72f);
        rect.sizeDelta = new Vector2(250f, 92f);
    }

    private static void AddBorder(RectTransform target)
    {
        Color color = new Color(1f, 0.86f, 0.22f, 0.95f);
        BorderEdge(target, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 7f), color);
        BorderEdge(target, "Bottom", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 7f), color);
        BorderEdge(target, "Left", Vector2.zero, new Vector2(0f, 1f), new Vector2(7f, 0f), color);
        BorderEdge(target, "Right", new Vector2(1f, 0f), Vector2.one, new Vector2(7f, 0f), color);
    }

    private static void BorderEdge(RectTransform parent, string name, Vector2 min,
        Vector2 max, Vector2 size, Color color)
    {
        RectTransform edge = StageUITheme.Rect(name, parent);
        edge.anchorMin = min;
        edge.anchorMax = max;
        edge.sizeDelta = size;
        var image = edge.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private void SetInstruction(Step nextStep, string message, RectTransform target, bool canAdvance = false)
    {
        step = nextStep;
        instructionTarget = target;
        instructionText.text = message;
        arrow.gameObject.SetActive(target != null);
        nextButton.gameObject.SetActive(canAdvance);
        if (target != null)
        {
            highlight.SetParent(target, false);
            highlight.anchorMin = Vector2.zero;
            highlight.anchorMax = Vector2.one;
            highlight.offsetMin = new Vector2(-10f, -10f);
            highlight.offsetMax = new Vector2(10f, 10f);
            highlight.SetAsLastSibling();
            highlight.gameObject.SetActive(true);
        }
        else highlight.gameObject.SetActive(false);
        instructions.gameObject.SetActive(true);
        skipButton.gameObject.SetActive(true);
        confirmation.SetActive(false);
        if (boardPanel != null) boardPanel.gameObject.SetActive(false);
        instructionBlockers = ControlTutorialLayout.FindBlockers();
        LayoutInstruction();
    }

    private void BeginMovement()
    {
        DestroyPracticeTarget();
        input.SetGameplayInputBlocked(false);
        mobileControls?.ResetJoystick();
        movementStart = player.transform.position;
        SetInstruction(Step.Movement, "Use this joystick to move.", joystick);
    }

    private void BeginJump()
    {
        jumpRequested = false;
        jumpStartY = player.transform.position.y;
        SetInstruction(Step.Jump, "Tap this button to jump.", jumpButton);
    }

    private void BeginInteraction()
    {
        CreatePracticeTarget();
        SetInstruction(Step.Interaction, "Use this button to interact with nearby objects.", handButton);
    }

    private void Update()
    {
        if (!IsActive || player == null || input == null) return;
        if (step == Step.Movement && mobileControls != null &&
            mobileControls.CurrentInput.sqrMagnitude > joystickDeadZone * joystickDeadZone)
        {
            Vector3 moved = player.transform.position - movementStart;
            moved.y = 0f;
            if (moved.magnitude >= movementDistance) BeginJump();
        }
        else if (step == Step.Jump && jumpRequested && player.IsJumping() &&
                 player.transform.position.y > jumpStartY + 0.1f)
            BeginInteraction();
        if (step == Step.BoardApproach) TrackViewBoardButton();
        if (practiceLabel != null && Camera.main != null)
            practiceLabel.transform.rotation = Camera.main.transform.rotation;
    }

    private void LateUpdate()
    {
        if (IsActive && step != Step.Confirm)
        {
            UpdateWorldAnchor();
            if (instructions.gameObject.activeSelf) LayoutInstruction();
            if (skipButton != null) skipButton.transform.SetAsLastSibling();
        }
    }

    private void LayoutInstruction()
    {
        ControlTutorialLayout.Place(instructions, instructionText,
            (RectTransform)nextButton.transform, safeArea, instructionTarget,
            false, instructionBlockers, arrow);
        if (step == Step.CrystalTower)
            instructions.anchoredPosition = new Vector2(-safeArea.rect.width * 0.3f,
                safeArea.rect.height * 0.12f);
        else if (step == Step.BoardOverview)
        {
            instructions.sizeDelta = new Vector2(Mathf.Min(500f, safeArea.rect.width - 40f),
                instructions.sizeDelta.y);
            instructions.anchoredPosition = new Vector2(-safeArea.rect.width * 0.31f,
                safeArea.rect.height * 0.05f);
            arrow.gameObject.SetActive(false);
        }
    }

    private static RectTransform FindRect(string name)
    {
        foreach (var rect in FindObjectsByType<RectTransform>(FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
            if (rect.name == name) return rect;
        return null;
    }

    private void SetWorldStep(Step nextStep, string message, Transform target, bool canAdvance)
    {
        worldTarget = target;
        worldAnchor.gameObject.SetActive(target != null);
        crystalScreenLabel.gameObject.SetActive(nextStep == Step.CrystalTower);
        UpdateWorldAnchor();
        SetInstruction(nextStep, message, target != null ? worldAnchor : null, canAdvance);
    }

    private void UpdateWorldAnchor()
    {
        if (worldTarget == null || worldAnchor == null || Camera.main == null) return;
        Vector3 point = worldTarget.position;
        var renderers = worldTarget.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            point = bounds.center;
        }
        if (step == Step.Book) point += Vector3.up * 1.3f;
        worldAnchor.sizeDelta = step == Step.Book ? new Vector2(480f, 210f) :
            step == Step.BoardOverview ? new Vector2(700f, 320f) :
            new Vector2(150f, 150f);
        Vector3 screen = Camera.main.WorldToScreenPoint(point);
        worldAnchor.gameObject.SetActive(screen.z > 0f);
        if (screen.z <= 0f) return;
        Vector2 local;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(safeArea,
                screen, null, out local)) worldAnchor.anchoredPosition = local;
    }

    private void FocusCamera(Transform target, Vector3 offset)
    {
        if (target == null || Camera.main == null) return;
        PauseCameraInput();
        if (tutorialCamera == null)
        {
            var cameraObject = new GameObject("Inference Tutorial Camera");
            tutorialCamera = cameraObject.AddComponent<CinemachineCamera>();
            var challenge = FindFirstObjectByType<LogicLegends.Inference.InferenceChallenge>();
            var source = challenge != null ? challenge.focusCamera.GetComponent<CinemachineCamera>() : null;
            if (source != null) tutorialCamera.Lens = source.Lens;
            tutorialCamera.Priority = new PrioritySettings { Enabled = true, Value = 150 };
            cameraBrain = Camera.main.GetComponent<CinemachineBrain>();
            if (cameraBrain != null)
            {
                previousBlend = cameraBrain.DefaultBlend;
                cameraBrain.DefaultBlend = new CinemachineBlendDefinition(
                    CinemachineBlendDefinition.Styles.EaseInOut, cameraBlendSeconds);
            }
        }
        Vector3 focusPoint = target.position;
        if (target == bookTarget || target == altarTarget)
        {
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                focusPoint = bounds.center;
            }
        }
        Vector3 cameraPosition = focusPoint + offset;
        tutorialCamera.transform.SetPositionAndRotation(cameraPosition,
            Quaternion.LookRotation(focusPoint - cameraPosition, Vector3.up));
        tutorialCamera.gameObject.SetActive(true);
    }

    private void RestoreGameplayCamera()
    {
        if (tutorialCamera != null) tutorialCamera.gameObject.SetActive(false);
        foreach (var cameraInput in pausedCameraInputs)
            if (cameraInput != null) cameraInput.enabled = true;
        pausedCameraInputs.Clear();
    }

    private void PauseCameraInput()
    {
        foreach (var component in FindObjectsByType<CinemachineInputAxisController>(
                     FindObjectsSortMode.None))
            if (component.enabled) { component.enabled = false; pausedCameraInputs.Add(component); }
        foreach (var component in FindObjectsByType<CinemachinePinchZoom>(
                     FindObjectsSortMode.None))
            if (component.enabled) { component.enabled = false; pausedCameraInputs.Add(component); }
    }

    private void BeginBook()
    {
        worldTarget = null;
        FocusCamera(bookTarget, bookCameraOffset);
        SetWorldStep(Step.Book, "Interact with this book statue to start the game.", bookTarget, false);
    }

    public void TryTutorialBookInteraction()
    {
        if (!IsBookStep) return;
        BeginTimer();
    }

    private void BeginTimer()
    {
        worldTarget = null;
        worldAnchor.gameObject.SetActive(false);
        SetInstruction(Step.Timer,
            "This timer shows how much time you have left to complete the challenges.",
            ControlTutorialLayout.JourneyElement("StageCountdown"), true);
    }

    private void BeginMinimap()
    {
        SetInstruction(Step.Minimap,
            "Use the minimap to navigate the stage and find important locations.",
            minimapTarget, true);
    }

    private void BeginBoardOverview()
    {
        FocusCamera(boardCameraFocus, boardCameraOffset);
        SetWorldStep(Step.BoardOverview, "Your current challenge appears on this board.",
            boardHighlightTarget, true);
    }

    private void BeginBoardApproach()
    {
        worldTarget = null;
        worldAnchor.gameObject.SetActive(false);
        RestoreGameplayCamera();
        SetInstruction(Step.BoardApproach,
            "Approach the altar, then tap View Board to open the challenge.", null);
    }

    private void TrackViewBoardButton()
    {
        RectTransform view = FindRect("InferenceViewBoardButton");
        RectTransform availableView = view != null && view.gameObject.activeInHierarchy ? view : null;
        if (availableView == instructionTarget) return;
        SetInstruction(Step.BoardApproach,
            "Approach the altar, then tap View Board to open the challenge.",
            availableView);
    }

    public void TryTutorialBoardView()
    {
        if (!IsBoardApproach) return;
        input.SetGameplayInputBlocked(true);
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        FocusCamera(boardCameraFocus, boardCameraOffset);
        ShowBoardSection(Step.BoardPremises);
    }

    public void NextStep()
    {
        if (!IsActive) return;
        switch (step)
        {
            case Step.Timer: BeginMinimap(); break;
            case Step.Minimap: BeginBoardOverview(); break;
            case Step.BoardOverview: BeginBoardApproach(); break;
            case Step.BoardPremises: ShowBoardSection(Step.BoardDiamond); break;
            case Step.BoardDiamond: ShowBoardSection(Step.BoardConclusion); break;
            case Step.BoardConclusion: ShowBoardSection(Step.BoardBack); break;
            case Step.CrystalTower: BeginConfirmation(); break;
        }
    }

    private void BuildPracticeBoard()
    {
        boardPanel = StageUITheme.Rect("Inference Practice Board", safeArea);
        StageUITheme.Stretch(boardPanel, Vector2.zero, Vector2.one);
        var shade = boardPanel.gameObject.AddComponent<Image>();
        shade.color = new Color(0.015f, 0.025f, 0.035f, 0.97f);
        RectTransform card = StageUITheme.Rect("Practice Board Card", boardPanel);
        StageUITheme.Stretch(card, new Vector2(0.07f, 0.07f), new Vector2(0.93f, 0.93f));
        var cardImage = card.gameObject.AddComponent<Image>();
        cardImage.color = new Color(0.06f, 0.11f, 0.14f, 1f);
        AddBorder(card);
        var heading = (TextMeshProUGUI)StageUITheme.Text("Practice Board Heading", card,
            "RULES OF INFERENCE — PRACTICE", 42, true);
        heading.color = StageUITheme.Gold;
        StageUITheme.Stretch(heading.rectTransform, new Vector2(0.04f, 0.84f),
            new Vector2(0.96f, 0.96f));
        boardTabs = new RectTransform[3];
        string[] labels = { "1  COMPLETE PREMISES", "2  FIND A DIAMOND", "3  REVEAL CONCLUSION" };
        for (int i = 0; i < boardTabs.Length; i++)
        {
            boardTabs[i] = StageUITheme.Rect("Practice Board Step " + (i + 1), card);
            StageUITheme.Stretch(boardTabs[i], new Vector2(0.04f + i * 0.31f, 0.71f),
                new Vector2(0.33f + i * 0.31f, 0.81f));
            boardTabs[i].gameObject.AddComponent<Image>().color =
                new Color(0.1f, 0.2f, 0.23f, 1f);
            var tabText = (TextMeshProUGUI)StageUITheme.Text("Step Label", boardTabs[i],
                labels[i], 25, true);
            tabText.color = Color.white;
            StageUITheme.Stretch(tabText.rectTransform, new Vector2(0.02f, 0.06f),
                new Vector2(0.98f, 0.94f));
        }
        RectTransform sample = StageUITheme.Rect("Practice Premises", card);
        StageUITheme.Stretch(sample, new Vector2(0.04f, 0.28f), new Vector2(0.96f, 0.68f));
        sample.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.15f, 0.19f, 1f);
        var sampleText = (TextMeshProUGUI)StageUITheme.Text("Placeholder Argument", sample,
            "PRACTICE EXAMPLE\nPremise:  [choose a word]  ...\nTherefore,  [conclusion hidden]",
            32, true);
        sampleText.color = Color.white;
        StageUITheme.Stretch(sampleText.rectTransform, new Vector2(0.05f, 0.05f),
            new Vector2(0.95f, 0.95f));
        boardExplanation = (TextMeshProUGUI)StageUITheme.Text("Board Explanation", card,
            "", 29, true);
        boardExplanation.color = Color.white;
        StageUITheme.Stretch(boardExplanation.rectTransform, new Vector2(0.05f, 0.14f),
            new Vector2(0.95f, 0.27f));
        boardNextButton = StageUITheme.Button("Practice Board Next", card, "Next", NextStep);
        StageUITheme.Stretch((RectTransform)boardNextButton.transform,
            new Vector2(0.58f, 0.025f), new Vector2(0.8f, 0.13f));
        boardBackButton = StageUITheme.Button("Practice Board Back", card, "Back", BackFromPracticeBoard);
        StageUITheme.Stretch((RectTransform)boardBackButton.transform,
            new Vector2(0.22f, 0.025f), new Vector2(0.44f, 0.13f));
        boardPanel.gameObject.SetActive(false);
    }

    private void ShowBoardSection(Step boardStep)
    {
        step = boardStep;
        worldTarget = null;
        worldAnchor.gameObject.SetActive(false);
        instructions.gameObject.SetActive(false);
        skipButton.gameObject.SetActive(true);
        boardPanel.gameObject.SetActive(true);
        boardPanel.SetAsLastSibling();
        int section = boardStep == Step.BoardPremises ? 0 :
            boardStep == Step.BoardDiamond ? 1 : 2;
        highlight.SetParent(boardStep == Step.BoardBack ?
            (RectTransform)boardBackButton.transform : boardTabs[section], false);
        highlight.anchorMin = Vector2.zero;
        highlight.anchorMax = Vector2.one;
        highlight.offsetMin = new Vector2(-5f, -5f);
        highlight.offsetMax = new Vector2(5f, 5f);
        highlight.gameObject.SetActive(true);
        boardNextButton.gameObject.SetActive(boardStep != Step.BoardBack);
        boardBackButton.interactable = boardStep == Step.BoardBack;
        switch (boardStep)
        {
            case Step.BoardPremises:
                boardExplanation.text = "Complete the premises using the available choices. " +
                    "Drag words into the blanks, then tap Check argument.";
                break;
            case Step.BoardDiamond:
                boardExplanation.text = "Find a rule diamond that matches the completed premises. " +
                    "Collect it at a crystal tower and bring it back to the altar.";
                break;
            case Step.BoardConclusion:
                boardExplanation.text = "Place the matching rule diamond at the altar. " +
                    "The correct rule reveals the conclusion and completes the round.";
                break;
            case Step.BoardBack:
                boardExplanation.text = "Tap Back to return to the stage and learn where to find crystals.";
                break;
        }
    }

    private void BackFromPracticeBoard()
    {
        if (!IsActive || step != Step.BoardBack) return;
        boardPanel.gameObject.SetActive(false);
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
        input.SetGameplayInputBlocked(false);
        BeginCrystalTower();
    }

    private void BeginCrystalTower()
    {
        CreatePracticeCrystal();
        FocusCamera(practiceCrystal.transform, crystalCameraOffset);
        SetWorldStep(Step.CrystalTower,
            "Search crystal towers for rule crystals. Approach a crystal and use the hand " +
            "button to collect it. Tap the hand button to try this practice crystal.",
            practiceCrystal.transform, false);
    }

    private void CreatePracticeCrystal()
    {
        DestroyPracticeCrystal();
        practiceCrystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        practiceCrystal.name = "Inference Tutorial Crystal";
        var renderers = crystalTarget.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = renderers.Length > 0 ? renderers[0].bounds :
            new Bounds(crystalTarget.position, Vector3.one);
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        practiceCrystal.transform.position = bounds.center +
            new Vector3(2f, 2f, bounds.extents.z + 0.8f);
        practiceCrystal.transform.localScale = Vector3.one * 0.65f;
        practiceCrystal.transform.rotation = Quaternion.Euler(35f, 45f, 35f);
        var collider = practiceCrystal.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        var renderer = practiceCrystal.GetComponent<Renderer>();
        if (renderer != null) renderer.material.color = new Color(0.45f, 0.9f, 1f);
    }

    private void DestroyPracticeCrystal()
    {
        if (practiceCrystal != null) Destroy(practiceCrystal);
        practiceCrystal = null;
    }

    private void CreatePracticeTarget()
    {
        DestroyPracticeTarget();
        practiceTarget = GameObject.CreatePrimitive(PrimitiveType.Cube);
        practiceTarget.name = "Inference Practice Interaction";
        Vector3 practicePosition = player.transform.position + player.transform.forward * 1.3f -
                                   player.transform.right * 2.3f;
        practicePosition.y = jumpStartY + 1.3f;
        practiceTarget.transform.position = practicePosition;
        practiceTarget.transform.localScale = Vector3.one * 0.7f;
        var collider = practiceTarget.GetComponent<Collider>();
        if (collider != null) collider.isTrigger = true;
        var renderer = practiceTarget.GetComponent<Renderer>();
        if (renderer != null) renderer.material.color = new Color(1f, 0.75f, 0.18f);
        var label = new GameObject("Practice Label", typeof(TextMeshPro));
        label.transform.SetParent(practiceTarget.transform, false);
        label.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        label.transform.localScale = Vector3.one * 1.2f;
        practiceLabel = label.GetComponent<TextMeshPro>();
        practiceLabel.text = "PRACTICE";
        practiceLabel.fontSize = 6f;
        practiceLabel.alignment = TextAlignmentOptions.Center;
        practiceLabel.color = Color.white;
    }

    private void DestroyPracticeTarget()
    {
        if (practiceTarget != null) Destroy(practiceTarget);
        practiceTarget = null;
        practiceLabel = null;
    }

    public void NoteJumpInput()
    {
        if (IsActive && step == Step.Jump) jumpRequested = true;
    }

    public void TryPracticeInteraction()
    {
        if (!IsActive) return;
        if (step == Step.Book) { TryTutorialBookInteraction(); return; }
        if (step == Step.CrystalTower)
        {
            DestroyPracticeCrystal();
            BeginConfirmation();
            return;
        }
        if (step != Step.Interaction || practiceTarget == null || player == null) return;
        Vector3 toward = practiceTarget.transform.position - player.transform.position;
        toward.y = 0f;
        Vector3 facing = player.transform.forward;
        facing.y = 0f;
        if (toward.magnitude > practiceRange || toward.sqrMagnitude < 0.001f ||
            facing.sqrMagnitude < 0.001f ||
            Vector3.Dot(facing.normalized, toward.normalized) < 0.25f) return;
        DestroyPracticeTarget();
        BeginBook();
    }

    public void SkipTutorial()
    {
        if (!IsActive) return;
        DestroyPracticeTarget();
        DestroyPracticeCrystal();
        BeginConfirmation();
    }

    private void BeginConfirmation()
    {
        step = Step.Confirm;
        worldTarget = null;
        if (worldAnchor != null) worldAnchor.gameObject.SetActive(false);
        highlight.gameObject.SetActive(false);
        instructions.gameObject.SetActive(false);
        skipButton.gameObject.SetActive(false);
        if (boardPanel != null) boardPanel.gameObject.SetActive(false);
        RestoreGameplayCamera();
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
        confirmation.SetActive(true);
        confirmation.transform.SetAsLastSibling();
        mobileControls?.ResetJoystick();
        input.SetGameplayInputBlocked(true);
    }

    public void ConfirmNo()
    {
        if (!IsActive || step != Step.Confirm) return;
        DestroyPracticeCrystal();
        RestoreGameplayCamera();
        BeginMovement();
    }

    public void ConfirmYes()
    {
        if (!IsActive || step != Step.Confirm) return;
        Cleanup();
    }

    private void Cleanup()
    {
        DestroyPracticeTarget();
        DestroyPracticeCrystal();
        RestoreGameplayCamera();
        if (tutorialCamera != null) Destroy(tutorialCamera.gameObject);
        tutorialCamera = null;
        if (boardCameraFocus != null) Destroy(boardCameraFocus.gameObject);
        if (boardHighlightTarget != null) Destroy(boardHighlightTarget.gameObject);
        boardCameraFocus = null;
        boardHighlightTarget = null;
        if (cameraBrain != null) cameraBrain.DefaultBlend = previousBlend;
        cameraBrain = null;
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
        mobileControls?.ResetJoystick();
        if (input != null && IsActive) input.SetGameplayInputBlocked(previousInputBlocked);
        if (settingsButton != null) settingsButton.interactable = previousSettingsInteractable;
        if (highlight != null) Destroy(highlight.gameObject);
        if (instructions != null) Destroy(instructions.gameObject);
        if (skipButton != null) Destroy(skipButton.gameObject);
        if (worldAnchor != null) Destroy(worldAnchor.gameObject);
        if (boardPanel != null) Destroy(boardPanel.gameObject);
        if (confirmation != null) Destroy(confirmation);
        highlight = null;
        instructions = null;
        confirmation = null;
        skipButton = null;
        boardPanel = null;
        worldAnchor = null;
        IsActive = false;
        if (Active == this) Active = null;
        if (helpButton != null && SceneManager.GetActiveScene().name == "RulesOfInference")
            helpButton.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (IsActive) Cleanup();
    }
}
