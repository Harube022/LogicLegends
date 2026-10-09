using System.Collections;
using LogicLegends.Inference;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Presentation and explicit navigation for the three PRELIM learning stages.</summary>
[DefaultExecutionOrder(1000)]
public sealed class StageJourneyUI : MonoBehaviour
{
    public static StageJourneyUI Instance { get; private set; }
    [SerializeField, Min(60)] private float inferenceTimeLimit = 18 * 60;
    private RectTransform safeArea, hud, modal, card;
    private TMP_Text stageLabel, clockLabel, progressLabel, resultTitle, resultBody, resultStage;
    private Button primaryButton, secondaryButton, extraButton, inferenceHelp;
    private GameObject inferenceTutorial;
    private TMP_Text tutorialTitle, tutorialBody, tutorialPage;
    private Button tutorialBack, tutorialNext;
    private LevelTimerManager propositionalClock;
    private TruthTableStageClock truthClock;
    private PropositionalLogicTutorial propositionalTutorial;
    private TruthTableTutorial truthTutorial;
    private readonly System.Collections.Generic.List<Behaviour> pausedCameraInputs = new System.Collections.Generic.List<Behaviour>();
    private InferenceChallenge inference;
    private float inferenceRemaining;
    private bool inferenceStarted, inferenceFinished, resultOpen, tutorialOpen, navigating;
    private float previousTimeScale;
    private bool previousInputBlocked, previousPlayerEnabled;
    private int tutorialIndex;
    private readonly string[] tutorialTitles = { "Build the argument", "Find the matching crystal", "Place and validate", "Finish the stage" };
    private readonly string[] tutorialBodies = {
        "Approach the inference board and interact to open it. Read the premises. Drag words from the word bank into the blanks, then tap CHECK ARGUMENT. The timer starts when you first open the board and pauses while the board or this guide is open.",
        "Once the premises are correct, explore the pillars. Collect a rule crystal using Interact. Read the rule names carefully: every crystal looks equally important, so choose from the argument rather than its appearance.",
        "Return to the board with your crystal. Tap PLACE RULE, then validate the argument. A wrong crystal is removed; explore again and find another. The conclusion is revealed only after a correct validation.",
        "Solve the required rounds to finish Rules of Inference. The result screen stays open until you choose your next action. If the 18-minute exploration timer reaches zero, restart the stage or return to the Main Menu." };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "PRELIM" && scene.name != "RulesOfInference") return;
        if (FindFirstObjectByType<StageJourneyUI>() != null) return;
        var go = new GameObject("StageJourneyUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(go, scene);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<StageJourneyUI>();
    }

    private void Awake()
    {
        Instance = this;
        inferenceRemaining = inferenceTimeLimit;
        safeArea = StageUITheme.Rect("SafeArea", transform);
        StageUITheme.Stretch(safeArea, Vector2.zero, Vector2.one);
        safeArea.gameObject.AddComponent<Crystal.SafeArea>();
        BuildHud(); BuildResults();
    }

    private IEnumerator Start()
    {
        propositionalClock = FindFirstObjectByType<LevelTimerManager>(FindObjectsInactive.Include);
        truthClock = FindFirstObjectByType<TruthTableStageClock>(FindObjectsInactive.Include);
        propositionalTutorial = FindFirstObjectByType<PropositionalLogicTutorial>(FindObjectsInactive.Include);
        truthTutorial = FindFirstObjectByType<TruthTableTutorial>(FindObjectsInactive.Include);
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas == GetComponent<Canvas>()) continue;
            StageUITheme.SkinGameplay(canvas);
            foreach (var t in canvas.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "PropositionalTutorialOverlay" || t.name == "TruthTableTutorialOverlay") StageUITheme.SkinTutorial(t.gameObject);
                if (t.name == "TimerText" || t.name == "ChallengenumberText")
                {
                    var label = t.GetComponent<TMP_Text>();
                    if (label != null) label.enabled = false;
                }
                if (t.name == "PropositionalHelpButton" || t.name == "TruthTableHelpButton")
                {
                    StageUITheme.SkinButton(t.GetComponent<Button>());
                    var rt = (RectTransform)t;
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1);
                    rt.pivot = new Vector2(0.5f, 1); rt.anchoredPosition = new Vector2(0, -24);
                }
            }
        }
        if (SceneManager.GetActiveScene().name == "RulesOfInference")
        {
            StageSelectionState.Select(3);
            inference = FindFirstObjectByType<InferenceChallenge>(FindObjectsInactive.Include);
            if (inference != null) inference.onChallengeCompleted.AddListener(OnInferenceComplete);
            BuildInferenceTutorial();
            yield return null;
            while (Player.LocalInstance == null) yield return null;
            yield return new WaitForSecondsRealtime(0.8f);
            while (TeleportManager.Instance != null && TeleportManager.Instance.IsTransitioning)
                yield return null;
            OpenInferenceTutorial();
        }
    }

    private void Update()
    {
        int stage = SceneManager.GetActiveScene().name == "RulesOfInference" ? 3 : StageSelectionState.SelectedStage;
        hud.gameObject.SetActive(!resultOpen && !tutorialOpen &&
            !(stage == 2 && truthClock != null && truthClock.IsPlayerAtBoard) &&
            !(inference != null && inference.IsBoardOpen) &&
            !(propositionalTutorial != null && propositionalTutorial.IsOpen) &&
            !(truthTutorial != null && truthTutorial.IsOpen));
        stageLabel.text = "STAGE " + stage + "  /  " + StageName(stage).ToUpperInvariant();
        if (stage == 1 && propositionalClock != null)
        {
            clockLabel.text = FormatTime(propositionalClock.RemainingTime);
            progressLabel.text = "CHALLENGE " + Mathf.Clamp(LevelTimerManager.savedTopicIndex + 1, 1, 5) + " OF 5";
        }
        else if (stage == 2 && truthClock != null)
        {
            clockLabel.text = FormatTime(truthClock.RemainingSeconds);
            progressLabel.text = "CHALLENGE " + truthClock.CurrentChallengeNumber + "  /  " + truthClock.ProgressSummary;
        }
        else if (stage == 3)
        {
            if (inference != null && inference.IsBoardOpen) inferenceStarted = true;
            if (inference != null && inferenceStarted && !inferenceFinished && !resultOpen && !tutorialOpen && !inference.IsBoardOpen && Time.timeScale > 0)
            {
                inferenceRemaining = Mathf.Max(0, inferenceRemaining - Time.deltaTime);
                if (inferenceRemaining == 0) ShowFailure(3, "The exploration timer ran out.", RestartInference);
            }
            clockLabel.text = FormatTime(inferenceRemaining);
            progressLabel.text = inference == null ? "EXPLORE THE BOARD" : "RULES SOLVED  " + inference.SolvedRounds + " / " + inference.requiredRounds;
            if (inferenceHelp != null) inferenceHelp.gameObject.SetActive(!resultOpen && !tutorialOpen && !(inference != null && inference.IsBoardOpen));
        }
        if (stage == 2 && truthClock != null && truthClock.TimeAdjustmentVisible)
            progressLabel.text += "\n" + truthClock.TimeAdjustmentText;
    }

    public static string StageName(int stage) => stage == 1 ? "Propositional Logic" : stage == 2 ? "Truth Table" : "Rules of Inference";
    public static string FormatTime(float seconds) { int total = Mathf.CeilToInt(Mathf.Max(0, seconds)); return (total / 60).ToString("00") + ":" + (total % 60).ToString("00"); }

    private void BuildHud()
    {
        hud = StageUITheme.Rect("CurrentStageHud", safeArea);
        hud.anchorMin = hud.anchorMax = new Vector2(0, 1); hud.pivot = new Vector2(0, 1);
        hud.anchoredPosition = new Vector2(180, -24); hud.sizeDelta = new Vector2(520, 148);
        StageUITheme.CompactSurface(hud.gameObject.AddComponent<Image>()); hud.GetComponent<Image>().raycastTarget = false;
        stageLabel = StageUITheme.Text("CurrentStageLabel", hud, "", 25);
        StageUITheme.Stretch(stageLabel.rectTransform, new Vector2(0.06f, 0.62f), new Vector2(0.94f, 0.84f));
        clockLabel = StageUITheme.Text("StageCountdown", hud, "", 42);
        StageUITheme.Stretch(clockLabel.rectTransform, new Vector2(0.06f, 0.18f), new Vector2(0.31f, 0.57f));
        progressLabel = StageUITheme.Text("StageProgress", hud, "", 22, true);
        StageUITheme.Stretch(progressLabel.rectTransform, new Vector2(0.32f, 0.18f), new Vector2(0.94f, 0.57f));
    }

    private RectTransform MakeModal(string name, out RectTransform inner)
    {
        var root = StageUITheme.Rect(name, safeArea);
        StageUITheme.Stretch(root, Vector2.zero, Vector2.one);
        root.gameObject.AddComponent<Image>().color = new Color(0.01f, 0.035f, 0.045f, 0.88f);
        inner = StageUITheme.Rect("FramedCard", root);
        StageUITheme.Stretch(inner, new Vector2(0.14f, 0.12f), new Vector2(0.86f, 0.88f));
        StageUITheme.Surface(inner.gameObject.AddComponent<Image>());
        root.gameObject.SetActive(false);
        return root;
    }

    private void BuildResults()
    {
        modal = MakeModal("StageResultPanel", out card);
        resultStage = StageUITheme.Text("ResultStage", card, "", 27);
        StageUITheme.Stretch(resultStage.rectTransform, new Vector2(0.08f, 0.77f), new Vector2(0.92f, 0.86f));
        resultTitle = StageUITheme.Text("ResultHeading", card, "", 72);
        StageUITheme.Stretch(resultTitle.rectTransform, new Vector2(0.08f, 0.57f), new Vector2(0.92f, 0.76f));
        resultBody = StageUITheme.Text("ResultSummary", card, "", 31, true);
        StageUITheme.Stretch(resultBody.rectTransform, new Vector2(0.1f, 0.34f), new Vector2(0.9f, 0.56f));
        primaryButton = StageUITheme.Button("NextOrRetryButton", card, "NEXT STAGE", null);
        secondaryButton = StageUITheme.Button("MainMenuButton", card, "MAIN MENU", ReturnToMenu);
        extraButton = StageUITheme.Button("CheckpointRetryButton", card, "TRY AGAIN", null);
        StageUITheme.Stretch((RectTransform)primaryButton.transform, new Vector2(0.12f, 0.17f), new Vector2(0.48f, 0.29f));
        StageUITheme.Stretch((RectTransform)secondaryButton.transform, new Vector2(0.52f, 0.17f), new Vector2(0.88f, 0.29f));
        StageUITheme.Stretch((RectTransform)extraButton.transform, new Vector2(0.25f, 0.04f), new Vector2(0.75f, 0.15f));
    }

    public void ShowCompletion(int stage, string summary)
    {
        if (resultOpen) return;
        ShowResult(stage, "STAGE COMPLETE", summary, stage < 3 ? "NEXT STAGE" : "PLAY AGAIN", stage < 3 ? NextStage : RestartInference);
    }

    public void ShowFailure(int stage, string summary, UnityEngine.Events.UnityAction retry)
    {
        if (resultOpen) return;
        ShowResult(stage, "TIME TO TRY AGAIN", summary, stage == 1 ? "START OVER" : "RESTART STAGE", retry);
        if (stage == 1 && propositionalClock != null)
        {
            extraButton.gameObject.SetActive(true);
            StageUITheme.Stretch((RectTransform)primaryButton.transform, new Vector2(0.12f, 0.23f), new Vector2(0.48f, 0.35f));
            StageUITheme.Stretch((RectTransform)secondaryButton.transform, new Vector2(0.52f, 0.23f), new Vector2(0.88f, 0.35f));
            StageUITheme.Stretch((RectTransform)extraButton.transform, new Vector2(0.25f, 0.1f), new Vector2(0.75f, 0.22f));
            extraButton.GetComponentInChildren<TMP_Text>().text = propositionalClock.IsStudyOptionAvailable ? "STUDY IN LOGIC GARDEN" : "TRY FROM CHECKPOINT";
            extraButton.onClick.RemoveAllListeners(); extraButton.onClick.AddListener(propositionalClock.OnMiddleActionClicked);
        }
    }

    private void ShowResult(int stage, string title, string summary, string action, UnityEngine.Events.UnityAction callback)
    {
        Pause(); resultOpen = true; navigating = false;
        resultStage.text = "STAGE " + stage + "  /  " + StageName(stage).ToUpperInvariant();
        resultTitle.text = title; resultBody.text = summary;
        primaryButton.GetComponentInChildren<TMP_Text>().text = action;
        StageUITheme.Stretch((RectTransform)primaryButton.transform, new Vector2(0.12f, 0.17f), new Vector2(0.48f, 0.29f));
        StageUITheme.Stretch((RectTransform)secondaryButton.transform, new Vector2(0.52f, 0.17f), new Vector2(0.88f, 0.29f));
        primaryButton.onClick.RemoveAllListeners(); primaryButton.onClick.AddListener(callback);
        extraButton.gameObject.SetActive(false);
        modal.gameObject.SetActive(true); modal.SetAsLastSibling();
    }

    private void Pause()
    {
        previousTimeScale = Time.timeScale;
        previousInputBlocked = GameInput.Instance != null && GameInput.Instance.GameplayInputBlocked;
        previousPlayerEnabled = Player.LocalInstance != null && Player.LocalInstance.enabled;
        GameInput.Instance?.SetGameplayInputBlocked(true);
        if (Player.LocalInstance != null) Player.LocalInstance.ToggleControl(false);
        foreach (var input in FindObjectsByType<Unity.Cinemachine.CinemachineInputAxisController>(FindObjectsSortMode.None))
            if (input.enabled) { pausedCameraInputs.Add(input); input.enabled = false; }
        foreach (var input in FindObjectsByType<CinemachinePinchZoom>(FindObjectsSortMode.None))
            if (input.enabled) { pausedCameraInputs.Add(input); input.enabled = false; }
        foreach (var input in FindObjectsByType<ThirdPersonCameraController>(FindObjectsSortMode.None))
            if (input.enabled) { pausedCameraInputs.Add(input); input.enabled = false; }
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        Time.timeScale = 0;
    }

    private void Resume()
    {
        Time.timeScale = previousTimeScale > 0 ? previousTimeScale : 1;
        GameInput.Instance?.SetGameplayInputBlocked(previousInputBlocked);
        if (Player.LocalInstance != null) Player.LocalInstance.ToggleControl(previousPlayerEnabled);
        foreach (var input in pausedCameraInputs) if (input != null) input.enabled = true;
        pausedCameraInputs.Clear();
    }

    private void NextStage()
    {
        if (navigating) return; navigating = true;
        modal.gameObject.SetActive(false); resultOpen = false; Resume();
        GameInput.Instance?.SetGameplayInputBlocked(false);
        if (Player.LocalInstance != null) Player.LocalInstance.ToggleControl(true);
        if (StageSelectionState.SelectedStage == 1)
        {
            AreaVisibilityManager.Instance.TransitionToTruthTable();
        }
        else if (StageSelectionState.SelectedStage == 2) AreaVisibilityManager.Instance.TransitionToRulesOfInference();
    }

    private void ReturnToMenu()
    {
        if (navigating) return; navigating = true;
        Time.timeScale = 1; GameInput.Instance?.SetGameplayInputBlocked(false);
        LevelTimerManager.ResetSession();
        SceneManager.LoadScene("Main Menu");
    }

    private void RestartInference()
    {
        if (navigating) return; navigating = true;
        Time.timeScale = 1; GameInput.Instance?.SetGameplayInputBlocked(false);
        StageSelectionState.Select(3);
        SceneManager.LoadScene("RulesOfInference");
    }

    private void OnInferenceComplete()
    {
        inferenceFinished = true;
        if (inference != null) inference.CloseBoard();
        ShowCompletion(3, "Argument mastered!\nRules solved: " + inference.SolvedRounds + "\nTime remaining: " + FormatTime(inferenceRemaining));
    }

    private void BuildInferenceTutorial()
    {
        RectTransform inner;
        inferenceTutorial = MakeModal("InferenceTutorialOverlay", out inner).gameObject;
        var section = StageUITheme.Text("SectionLabel", inner, "STAGE 3  /  FIELD GUIDE", 25);
        StageUITheme.Stretch(section.rectTransform, new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.88f));
        tutorialTitle = StageUITheme.Text("PageTitle", inner, "", 52);
        StageUITheme.Stretch(tutorialTitle.rectTransform, new Vector2(0.08f, 0.63f), new Vector2(0.92f, 0.78f));
        tutorialBody = StageUITheme.Text("PageBody", inner, "", 32, true);
        StageUITheme.Stretch(tutorialBody.rectTransform, new Vector2(0.1f, 0.34f), new Vector2(0.9f, 0.61f));
        tutorialBody.alignment = TextAlignmentOptions.TopLeft;
        tutorialPage = StageUITheme.Text("PageIndicator", inner, "", 24);
        StageUITheme.Stretch(tutorialPage.rectTransform, new Vector2(0.42f, 0.27f), new Vector2(0.58f, 0.34f));
        tutorialBack = StageUITheme.Button("PreviousPage", inner, "BACK", () => ChangePage(-1));
        tutorialNext = StageUITheme.Button("NextPage", inner, "NEXT", () => ChangePage(1));
        StageUITheme.Stretch((RectTransform)tutorialBack.transform, new Vector2(0.1f, 0.12f), new Vector2(0.32f, 0.25f));
        StageUITheme.Stretch((RectTransform)tutorialNext.transform, new Vector2(0.35f, 0.12f), new Vector2(0.57f, 0.25f));
        var close = StageUITheme.Button("BeginStage", inner, "LET'S EXPLORE", CloseInferenceTutorial);
        StageUITheme.Stretch((RectTransform)close.transform, new Vector2(0.6f, 0.12f), new Vector2(0.9f, 0.25f));
        StageUITheme.SkinTutorial(inferenceTutorial);
        inferenceHelp = StageUITheme.Button("InferenceHelpButton", safeArea, "HELP", OpenInferenceTutorial);
        var rt = (RectTransform)inferenceHelp.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1); rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, -24); rt.sizeDelta = new Vector2(220, 80);
    }

    public void OpenInferenceTutorial()
    {
        if (TeleportManager.Instance != null && TeleportManager.Instance.IsTransitioning) return;
        if (tutorialOpen || resultOpen || inferenceTutorial == null) return;
        Pause(); tutorialOpen = true; tutorialIndex = 0; ChangePage(0);
        inferenceTutorial.SetActive(true); inferenceTutorial.transform.SetAsLastSibling();
    }

    public void CloseInferenceTutorial()
    {
        if (!tutorialOpen) return;
        tutorialOpen = false; inferenceTutorial.SetActive(false); Resume();
    }

    private void ChangePage(int direction)
    {
        tutorialIndex = Mathf.Clamp(tutorialIndex + direction, 0, tutorialBodies.Length - 1);
        tutorialTitle.text = tutorialTitles[tutorialIndex]; tutorialBody.text = tutorialBodies[tutorialIndex];
        tutorialPage.text = (tutorialIndex + 1) + " / " + tutorialBodies.Length;
        tutorialBack.interactable = tutorialIndex > 0; tutorialNext.interactable = tutorialIndex < tutorialBodies.Length - 1;
    }

    private void OnDestroy()
    {
        if (inference != null) inference.onChallengeCompleted.RemoveListener(OnInferenceComplete);
        if (Instance == this) Instance = null;
        if (resultOpen || tutorialOpen) Time.timeScale = 1;
    }
}
