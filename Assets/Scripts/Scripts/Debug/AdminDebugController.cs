using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Optional mobile Admin button and modal shared by the learning stages.</summary>
[DisallowMultipleComponent]
public sealed class AdminDebugController : MonoBehaviour
{
    [Header("Availability")]
    [SerializeField] private bool featureEnabled = true;
    [SerializeField] private bool allowInPlayerBuilds;
    [Header("UI")]
    [SerializeField] private Canvas uiCanvas;
    [SerializeField] private bool adminButtonEnabled;

    private GameInput input;
    private Player player;
    private TruthTableStageClock truthClock;
    private LevelTimerManager propositionalTimer;
    private RectTransform panelRoot;
    private RectTransform card;
    private Button adminButton;
    private Button clearChallengeButton;
    private Button clearConditionButton;
    private AdminAnswerViewer answerViewer;
    private bool panelOpen;
    private bool clearConsumed;
    private bool challengeEligibleAtOpen;
    private bool conditionEligibleAtOpen;
    private float previousTimeScale;
    private bool inputWasBlocked;
    private bool playerWasEnabled;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;

    private void Awake()
    {
        if (!featureEnabled || (!Application.isEditor && !allowInPlayerBuilds))
        {
            enabled = false;
            return;
        }
        if (SceneManager.GetActiveScene().name != "PRELIM" &&
            SceneManager.GetActiveScene().name != "TruthTable" &&
            SceneManager.GetActiveScene().name != "Setssss" &&
            SceneManager.GetActiveScene().name != "Formal vs. Informal Proofs" &&
            SceneManager.GetActiveScene().name != "RulesOfInference" &&
            SceneManager.GetActiveScene().name != "Functions and Relations" &&
            SceneManager.GetActiveScene().name != "Induction" &&
            SceneManager.GetActiveScene().name != "Direct&Indirect" &&
            SceneManager.GetActiveScene().name != "Graph" && SceneManager.GetActiveScene().name != "Trees")
            enabled = false;
    }

    private void Start()
    {
        if (!enabled) return;
        BindInput();
        if (uiCanvas == null)
        {
            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.name == "Canvas 1")
                { uiCanvas = canvas; break; }
        }
        if (uiCanvas == null)
        {
            Debug.LogError("Admin / Debug needs the existing Canvas 1 in this scene.", this);
            enabled = false;
            return;
        }
        BuildPanel();
        BuildAdminButton();
        adminButton.gameObject.SetActive(adminButtonEnabled);
    }

    private void BindInput()
    {
        input = GameInput.Instance != null ? GameInput.Instance :
            FindFirstObjectByType<GameInput>();
    }

    private void Update()
    {
        if (adminButton != null && (SceneManager.GetActiveScene().name == "Functions and Relations" ||
            SceneManager.GetActiveScene().name == "Induction" || SceneManager.GetActiveScene().name == "Graph" || SceneManager.GetActiveScene().name == "Trees" ||
            SceneManager.GetActiveScene().name == "RulesOfInference"))
            adminButton.gameObject.SetActive(adminButtonEnabled && (panelOpen || !IsAnotherModalBlocking()));
        if (adminButton != null && SceneManager.GetActiveScene().name == "Setssss")
        {
            bool visible = adminButtonEnabled && (SetssssControlsOnboarding.Active == null ||
                           !SetssssControlsOnboarding.Active.IsActive) &&
                           (SetssssChallengeOne.ActiveInstance == null || SetssssChallengeOne.ActiveInstance.Introductions == null ||
                            !SetssssChallengeOne.ActiveInstance.Introductions.IsOpen);
            if (adminButton.gameObject.activeSelf != visible) adminButton.gameObject.SetActive(visible);
        }
        if (panelOpen) UpdateLayout();
    }

    public void AdminButtonOn()
    {
        adminButtonEnabled = true;
        if (adminButton != null && enabled) adminButton.gameObject.SetActive(true);
    }

    public void AdminButtonOff()
    {
        adminButtonEnabled = false;
        if (panelOpen) ClosePanel();
        if (adminButton != null) adminButton.gameObject.SetActive(false);
    }

    private bool IsAnotherModalBlocking()
    {
        // An unanswered pop quiz is itself the current question. Its own input
        // lock remains in place when this nested Admin view closes.
        if ((SceneManager.GetActiveScene().name == "PRELIM" || SceneManager.GetActiveScene().name == "TruthTable") && StageSelectionState.SelectedStage == 2 && Time.timeScale > 0f)
        {
            var quiz = FindFirstObjectByType<TruthTableDoorInteraction>();
            var clock = quiz != null ? quiz.GetComponent<TruthTableStageClock>() : null;
            if (quiz != null && quiz.AdminQuestionOpen && clock != null && !clock.IsTutorialOpen &&
                !clock.IsGameOver && !clock.IsCompleted) return false;
        }
        if (input == null || input.GameplayInputBlocked || Time.timeScale == 0f) return true;
        if (SceneManager.GetActiveScene().name == "Functions and Relations")
        {
            var stage = FindFirstObjectByType<FunctionsRelationsStageManager>();
            return stage == null || stage.GameplayPaused || stage.IsBoardOpen ||
                stage.State == FunctionsRelationsState.VALIDATING || stage.State == FunctionsRelationsState.CHALLENGE_COMPLETE ||
                stage.State == FunctionsRelationsState.STAGE_COMPLETE || stage.State == FunctionsRelationsState.GAME_OVER;
        }
        if (SceneManager.GetActiveScene().name == "Induction")
        {
            var stage = FindFirstObjectByType<LogicLegends.Induction.InductionStageManager>();
            return stage == null || !stage.Ready || stage.Reading || stage.help == null || stage.help.OnboardingPending ||
                stage.onboarding != null && stage.onboarding.IsActive ||
                stage.State == LogicLegends.Induction.InductionState.StageComplete || stage.State == LogicLegends.Induction.InductionState.GameOver;
        }
        if (SceneManager.GetActiveScene().name == "RulesOfInference")
        {
            var inference = FindFirstObjectByType<LogicLegends.Inference.InferenceChallenge>();
            return inference == null || inference.IsBoardOpen || inference.PuzzleCompleted ||
                (StageJourneyUI.Instance != null && StageJourneyUI.Instance.InferenceEnded) ||
                (InferenceControlsOnboarding.Active != null &&
                 InferenceControlsOnboarding.Active.IsActive);
        }
        if (SceneManager.GetActiveScene().name == "Setssss")
        {
            if (SetssssControlsOnboarding.Active != null &&
                SetssssControlsOnboarding.Active.IsActive) return true;
            SetssssBoardView board = FindFirstObjectByType<SetssssBoardView>();
            return board != null && board.IsViewingBoard;
        }
        if (SceneManager.GetActiveScene().name == "Formal vs. Informal Proofs")
        {
            ProofsLibraryStageController proofs = FindFirstObjectByType<ProofsLibraryStageController>();
            return proofs == null || proofs.Finished || proofs.InputSuppressed;
        }
        if (SceneManager.GetActiveScene().name == "Direct&Indirect")
        {
            DirectIndirectProofStageController proofs = FindFirstObjectByType<DirectIndirectProofStageController>();
            return proofs == null || proofs.IsReaderOpen ||
                proofs.State == DirectIndirectProofStageController.TrialState.Validating ||
                proofs.State == DirectIndirectProofStageController.TrialState.StageComplete ||
                proofs.State == DirectIndirectProofStageController.TrialState.GameOver;
        }
        if (SceneManager.GetActiveScene().name == "Graph")
        {
            var stage = FindFirstObjectByType<LogicLegends.Graph.GraphStageManager>();
            return stage == null || !stage.Ready || stage.HelpOpen || stage.TutorialActive || stage.Moving ||
                stage.State == LogicLegends.Graph.GraphStageState.STAGE_COMPLETE || stage.State == LogicLegends.Graph.GraphStageState.GAME_OVER;
        }
        if (SceneManager.GetActiveScene().name == "Trees")
        {
            var stage = FindFirstObjectByType<LogicLegends.Trees.TreeStageManager>();
            return stage == null || !stage.Ready || stage.LessonOpen || stage.Moving || stage.BoardOpen ||
                stage.State == LogicLegends.Trees.TreeStageState.STAGE_COMPLETE || stage.State == LogicLegends.Trees.TreeStageState.GAME_OVER;
        }
        if (SceneManager.GetActiveScene().name != "PRELIM" && SceneManager.GetActiveScene().name != "TruthTable") return true;
        if (StageSelectionState.SelectedStage == 1)
        {
            PropositionalLogicTutorial tutorial = FindFirstObjectByType<PropositionalLogicTutorial>();
            return tutorial != null && tutorial.IsOpen;
        }
        if (StageSelectionState.SelectedStage == 2)
        {
            TruthTableDoorInteraction doors = FindFirstObjectByType<TruthTableDoorInteraction>();
            TruthTableStageClock clock = doors != null ? doors.GetComponent<TruthTableStageClock>() : null;
            return doors != null && doors.IsQuizOpen || clock != null &&
                (clock.IsTutorialOpen || clock.IsGameOver || clock.IsCompleted);
        }
        return true;
    }

    private bool CanClearChallenge()
    {
        if (SceneManager.GetActiveScene().name == "Graph")
            return FindFirstObjectByType<LogicLegends.Graph.GraphStageManager>()?.CanDebugClearChallenge == true;
        if (SceneManager.GetActiveScene().name == "Trees")
            return FindFirstObjectByType<LogicLegends.Trees.TreeStageManager>()?.CanDebugClearChallenge == true;
        if (SceneManager.GetActiveScene().name == "Functions and Relations")
        {
            var stage = FindFirstObjectByType<FunctionsRelationsStageManager>();
            return stage != null && stage.CanDebugClearChallenge;
        }
        if (SceneManager.GetActiveScene().name == "Induction")
        {
            var stage = FindFirstObjectByType<LogicLegends.Induction.InductionStageManager>();
            return stage != null && stage.CanDebugClearChallenge;
        }
        if (SceneManager.GetActiveScene().name == "RulesOfInference")
        {
            var stage = FindFirstObjectByType<LogicLegends.Inference.InferenceChallenge>();
            return stage != null && stage.HasStarted && !stage.PuzzleCompleted;
        }
        if (SceneManager.GetActiveScene().name == "Setssss")
        {
            SetssssChallengeOne sets = FindFirstObjectByType<SetssssChallengeOne>();
            return sets != null && sets.CanDebugClearChallenge;
        }
        if (SceneManager.GetActiveScene().name == "Formal vs. Informal Proofs")
        {
            ProofsLibraryStageController proofs = FindFirstObjectByType<ProofsLibraryStageController>();
            return proofs != null && proofs.CanDebugClearChallenge;
        }
        if (SceneManager.GetActiveScene().name == "Direct&Indirect")
        {
            DirectIndirectProofStageController proofs = FindFirstObjectByType<DirectIndirectProofStageController>();
            return proofs != null && proofs.CanDebugClearChallenge;
        }
        if (StageSelectionState.SelectedStage == 1)
        {
            QuizManager quiz = FindFirstObjectByType<QuizManager>();
            return quiz != null && quiz.CanDebugClearChallenge;
        }
        if (StageSelectionState.SelectedStage == 2)
        {
            var doors = FindFirstObjectByType<TruthTableDoorInteraction>();
            if (doors != null && doors.IsQuizOpen) return false;
            foreach (DynamicLogicPuzzle puzzle in FindObjectsByType<DynamicLogicPuzzle>(
                         FindObjectsSortMode.None))
                if (puzzle.CanDebugClearChallenge) return true;
        }
        return false;
    }

    private bool CanClearCondition()
    {
        if (SceneManager.GetActiveScene().name != "Setssss") return false;
        SetssssChallengeOne sets = FindFirstObjectByType<SetssssChallengeOne>();
        return sets != null && sets.CanDebugClearCondition;
    }

    private void OpenPanel()
    {
        BindInput();
        if (panelOpen || panelRoot == null || IsAnotherModalBlocking()) return;
        clearConsumed = false;
        // Capture eligibility before this modal pauses gameplay. The pause must
        // not make an otherwise active challenge's clear buttons unavailable.
        challengeEligibleAtOpen = CanClearChallenge();
        conditionEligibleAtOpen = CanClearCondition();
        panelOpen = true;
        card.gameObject.SetActive(true);
        if (answerViewer != null) answerViewer.Hide();
        player = Player.LocalInstance != null ? Player.LocalInstance : FindFirstObjectByType<Player>();
        inputWasBlocked = input != null && input.GameplayInputBlocked;
        playerWasEnabled = player != null && player.enabled;
        previousTimeScale = Time.timeScale;
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        if (SceneManager.GetActiveScene().name == "PRELIM" || SceneManager.GetActiveScene().name == "TruthTable")
        {
            if (StageSelectionState.SelectedStage == 1)
            {
                propositionalTimer = FindFirstObjectByType<LevelTimerManager>();
                if (propositionalTimer != null) propositionalTimer.SetTutorialPaused(true);
            }
            else if (StageSelectionState.SelectedStage == 2)
            {
                truthClock = FindFirstObjectByType<TruthTableStageClock>();
                if (truthClock != null) truthClock.SetTutorialOpen(true);
            }
        }
        Time.timeScale = 0f;
        if (input != null) input.SetGameplayInputBlocked(true);
        if (player != null) player.ToggleControl(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        clearChallengeButton.interactable = challengeEligibleAtOpen;
        clearConditionButton.interactable = conditionEligibleAtOpen;
        UpdateLayout();
        panelRoot.gameObject.SetActive(true);
        panelRoot.SetAsLastSibling();
    }

    public void ClosePanel()
    {
        if (!panelOpen) return;
        panelOpen = false;
        panelRoot.gameObject.SetActive(false);
        if (answerViewer != null) answerViewer.Hide();
        if (propositionalTimer != null) propositionalTimer.SetTutorialPaused(false);
        if (truthClock != null) truthClock.SetTutorialOpen(false);
        propositionalTimer = null;
        truthClock = null;
        Time.timeScale = previousTimeScale;
        if (input != null) input.SetGameplayInputBlocked(inputWasBlocked);
        if (player != null && playerWasEnabled && !IsTerminalState()) player.ToggleControl(true);
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
        player = null;
        challengeEligibleAtOpen = false;
        conditionEligibleAtOpen = false;
    }

    private bool IsTerminalState()
    {
        if (SceneManager.GetActiveScene().name == "Graph")
        {
            var stage = FindFirstObjectByType<LogicLegends.Graph.GraphStageManager>();
            return stage == null || stage.State == LogicLegends.Graph.GraphStageState.STAGE_COMPLETE || stage.State == LogicLegends.Graph.GraphStageState.GAME_OVER;
        }
        if (SceneManager.GetActiveScene().name == "Trees")
        {
            var stage = FindFirstObjectByType<LogicLegends.Trees.TreeStageManager>();
            return stage == null || stage.State == LogicLegends.Trees.TreeStageState.STAGE_COMPLETE || stage.State == LogicLegends.Trees.TreeStageState.GAME_OVER;
        }
        if (SceneManager.GetActiveScene().name == "Functions and Relations")
        {
            var stage = FindFirstObjectByType<FunctionsRelationsStageManager>();
            return stage == null || stage.State == FunctionsRelationsState.STAGE_COMPLETE || stage.State == FunctionsRelationsState.GAME_OVER;
        }
        if (SceneManager.GetActiveScene().name == "Induction")
        {
            var stage = FindFirstObjectByType<LogicLegends.Induction.InductionStageManager>();
            return stage == null || stage.State == LogicLegends.Induction.InductionState.StageComplete || stage.State == LogicLegends.Induction.InductionState.GameOver;
        }
        if (SceneManager.GetActiveScene().name == "RulesOfInference")
            return StageJourneyUI.Instance != null && StageJourneyUI.Instance.InferenceEnded;
        if (SceneManager.GetActiveScene().name == "Setssss")
        {
            SetssssChallengeOne sets = FindFirstObjectByType<SetssssChallengeOne>();
            return sets == null || sets.Complete || sets.IsGameOver;
        }
        if (SceneManager.GetActiveScene().name == "Formal vs. Informal Proofs")
        {
            ProofsLibraryStageController proofs = FindFirstObjectByType<ProofsLibraryStageController>();
            return proofs == null || proofs.Finished;
        }
        if (SceneManager.GetActiveScene().name == "Direct&Indirect")
        {
            DirectIndirectProofStageController proofs = FindFirstObjectByType<DirectIndirectProofStageController>();
            return proofs == null || proofs.State == DirectIndirectProofStageController.TrialState.StageComplete ||
                proofs.State == DirectIndirectProofStageController.TrialState.GameOver;
        }
        if (StageSelectionState.SelectedStage == 1)
        {
            QuizManager quiz = FindFirstObjectByType<QuizManager>();
            return quiz == null || quiz.IsSequenceComplete;
        }
        if (StageSelectionState.SelectedStage == 2)
        {
            TruthTableStageClock clock = FindFirstObjectByType<TruthTableStageClock>();
            return clock == null || clock.IsCompleted || clock.IsGameOver;
        }
        return true;
    }

    private void ClearCurrentChallenge()
    {
        if (!panelOpen || clearConsumed || !challengeEligibleAtOpen) return;
        clearConsumed = true;
        clearChallengeButton.interactable = false;
        clearConditionButton.interactable = false;
        // Restore the exact pre-panel pause/input state before the existing
        // completion flow starts. That flow owns the next challenge's state.
        ClosePanel();
        if (SceneManager.GetActiveScene().name == "Graph")
            FindFirstObjectByType<LogicLegends.Graph.GraphStageManager>()?.DebugClearCurrentChallenge();
        else if (SceneManager.GetActiveScene().name == "Trees")
            FindFirstObjectByType<LogicLegends.Trees.TreeStageManager>()?.DebugClearCurrentChallenge();
        else if (SceneManager.GetActiveScene().name == "Functions and Relations")
        {
            var stage = FindFirstObjectByType<FunctionsRelationsStageManager>();
            if (stage != null) stage.DebugClearCurrentChallenge();
        }
        else if (SceneManager.GetActiveScene().name == "Induction")
        {
            var stage = FindFirstObjectByType<LogicLegends.Induction.InductionStageManager>();
            if (stage != null) stage.DebugClearCurrentChallenge();
        }
        else if (SceneManager.GetActiveScene().name == "RulesOfInference")
        {
            var stage = FindFirstObjectByType<LogicLegends.Inference.InferenceChallenge>();
            if (stage != null) stage.DebugClearCurrentChallenge();
        }
        else if (SceneManager.GetActiveScene().name == "Setssss")
        {
            SetssssChallengeOne sets = FindFirstObjectByType<SetssssChallengeOne>();
            if (sets != null) sets.DebugClearCurrentChallenge();
        }
        else if (SceneManager.GetActiveScene().name == "Formal vs. Informal Proofs")
        {
            ProofsLibraryStageController proofs = FindFirstObjectByType<ProofsLibraryStageController>();
            if (proofs != null) proofs.DebugClearCurrentChallenge();
        }
        else if (SceneManager.GetActiveScene().name == "Direct&Indirect")
        {
            DirectIndirectProofStageController proofs = FindFirstObjectByType<DirectIndirectProofStageController>();
            if (proofs != null) proofs.DebugClearCurrentChallenge();
        }
        else if (StageSelectionState.SelectedStage == 1)
        {
            QuizManager quiz = FindFirstObjectByType<QuizManager>();
            if (quiz != null) quiz.DebugClearCurrentChallenge();
        }
        else if (StageSelectionState.SelectedStage == 2)
        {
            foreach (DynamicLogicPuzzle puzzle in FindObjectsByType<DynamicLogicPuzzle>(
                         FindObjectsSortMode.None))
                if (puzzle.DebugClearCurrentChallenge()) break;
        }
    }

    private void ClearCurrentCondition()
    {
        if (!panelOpen || clearConsumed || !conditionEligibleAtOpen) return;
        clearConsumed = true;
        clearChallengeButton.interactable = false;
        clearConditionButton.interactable = false;
        ClosePanel();
        SetssssChallengeOne sets = FindFirstObjectByType<SetssssChallengeOne>();
        if (sets != null) sets.DebugClearCurrentCondition();
    }

    private void BuildPanel()
    {
        TMP_FontAsset font = uiCanvas.GetComponentInChildren<TMP_Text>(true)?.font;
        GameObject overlay = new GameObject("AdminDebugOverlay", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image));
        panelRoot = (RectTransform)overlay.transform;
        panelRoot.SetParent(uiCanvas.transform, false);
        Stretch(panelRoot);
        Image shade = overlay.GetComponent<Image>();
        shade.color = new Color(0.015f, 0.025f, 0.03f, 0.82f);
        shade.raycastTarget = true;
        var modalCanvas = overlay.AddComponent<Canvas>();
        modalCanvas.overrideSorting = true;
        modalCanvas.sortingOrder = 1000;
        overlay.AddComponent<GraphicRaycaster>();

        GameObject cardObject = new GameObject("AdminDebugCard", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image));
        card = (RectTransform)cardObject.transform;
        card.SetParent(panelRoot, false);
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
        card.pivot = new Vector2(0.5f, 0.5f);
        StagePresentation.Frame(cardObject.GetComponent<Image>(), true);
        var border = cardObject.AddComponent<Outline>();
        border.effectColor = new Color(0.72f, 0.58f, 0.28f, 0.9f);
        border.effectDistance = new Vector2(3f, -3f); border.enabled = false;

        TextMeshProUGUI title = CreateText("AdminDebugTitle", card, font, 52f);
        title.text = "Admin / Debug";
        Place(title.rectTransform, new Vector2(0f, 235f), new Vector2(650f, 78f));
        clearChallengeButton = CreateButton("ClearCurrentChallenge", card, font,
            "CLEAR CHALLENGE", new Vector2(0f, 120f), ClearCurrentChallenge);
        clearConditionButton = CreateButton("ClearCurrentCondition", card, font,
            "CLEAR QUESTION", new Vector2(0f, 15f), ClearCurrentCondition);
        CreateButton("ViewQuestionsAndAnswers", card, font, "QUESTIONS & ANSWERS",
            new Vector2(0f, -90f), OpenAnswerViewer);
        CreateButton("CloseAdminDebug", card, font, "CLOSE",
            new Vector2(0f, -195f), ClosePanel);
        answerViewer = overlay.AddComponent<AdminAnswerViewer>();
        answerViewer.Build(uiCanvas, panelRoot, font, () =>
        {
            answerViewer.Hide();
            card.gameObject.SetActive(true);
        });
        panelRoot.gameObject.SetActive(false);
        UpdateLayout();
    }

    private void BuildAdminButton()
    {
        RectTransform safeArea = uiCanvas.transform.Find("SafeArea") as RectTransform;
        Transform parent = safeArea != null ? safeArea : uiCanvas.transform;
        TMP_FontAsset font = uiCanvas.GetComponentInChildren<TMP_Text>(true)?.font;
        GameObject go = new GameObject("AdminButton", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-40f, 40f);
        rect.sizeDelta = new Vector2(220f, 88f);
        Image image = go.GetComponent<Image>();
        image.color = new Color(0.08f, 0.23f, 0.21f, 0.95f);
        adminButton = go.GetComponent<Button>();
        adminButton.targetGraphic = image;
        adminButton.onClick.AddListener(OpenPanel);
        TextMeshProUGUI label = CreateText("Label", rect, font, 34f);
        label.text = "Admin";
        Stretch(label.rectTransform);
        StageUITheme.SkinButton(adminButton); adminButton.image.pixelsPerUnitMultiplier = 4.5f;
    }

    private void UpdateLayout()
    {
        if (card == null || uiCanvas == null) return;
        Canvas.ForceUpdateCanvases();
        Rect safe = Screen.safeArea;
        float left = Mathf.Clamp(safe.xMin, 0f, Screen.width);
        float right = Mathf.Clamp(safe.xMax, 0f, Screen.width);
        float bottom = Mathf.Clamp(safe.yMin, 0f, Screen.height);
        float top = Mathf.Clamp(safe.yMax, 0f, Screen.height);
        if (right <= left || top <= bottom) { left = bottom = 0; right = Screen.width; top = Screen.height; }
        Vector2 safeCenter = right > left && top > bottom
            ? new Vector2((left + right) * 0.5f, (bottom + top) * 0.5f)
            : new Vector2(Screen.width, Screen.height) * 0.5f;
        Vector2 pixelOffset = safeCenter - new Vector2(Screen.width, Screen.height) * 0.5f;
        float scale = Mathf.Max(0.001f, uiCanvas.scaleFactor);
        card.sizeDelta = new Vector2(Mathf.Min(760f, (right - left) / scale - 48f),
            Mathf.Min(620f, (top - bottom) / scale - 48f));
        card.anchoredPosition = pixelOffset / scale;
    }

    private void OpenAnswerViewer()
    {
        // Only the already-authorized Admin modal exposes this read-only viewer.
        if (!panelOpen || answerViewer == null) return;
        answerViewer.Show(AdminQuestionAnswers.Capture());
        card.gameObject.SetActive(false);
    }

    private static Button CreateButton(string name, Transform parent, TMP_FontAsset font,
        string caption, Vector2 position, UnityEngine.Events.UnityAction callback)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(Button));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f);
        rect.anchoredPosition = position; rect.sizeDelta = new Vector2(340,88);
        Image image = go.GetComponent<Image>();
        image.color = new Color(0.09f, 0.34f, 0.29f, 1f);
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(callback);
        TextMeshProUGUI text = CreateText("Label", rect, font, 34f);
        text.text = caption;
        Stretch(text.rectTransform);
        StageUITheme.SkinButton(button); button.image.pixelsPerUnitMultiplier = 4.5f;
        text.rectTransform.offsetMin = new Vector2(24,10); text.rectTransform.offsetMax = new Vector2(-24,-10);
        text.fontSize = 26; text.fontSizeMin = 22; text.fontSizeMax = 26;
        return button;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent,
        TMP_FontAsset font, float fontSize)
    {
        GameObject go = new GameObject(name, typeof(RectTransform),
            typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.font = StageUITheme.HeadingFont; label.color = StageUITheme.ReadingInk;
        label.raycastTarget = false;
        return label;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(-80f, size.y);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void OnDisable()
    {
        if (panelOpen) ClosePanel();
    }
}
