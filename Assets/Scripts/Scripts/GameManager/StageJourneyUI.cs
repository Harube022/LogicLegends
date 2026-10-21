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
    public RectTransform HudPanel => hud;
    private RectTransform propositionalHelp, truthHelp, settingsDock, inferenceAdmin;
    private const float HudWidth = 580f, HudHeight = 164f;
    private const float PropositionalHudWidth = 500f, PropositionalHudHeight = 136f;
    private readonly Vector3[] dockCorners = new Vector3[4];
    private TMP_Text stageLabel, clockLabel, progressLabel, resultTitle, resultBody, resultStage;
    private Button primaryButton, secondaryButton, extraButton, inferenceHelp;
    private GameObject inferenceTutorial;
    private TMP_Text tutorialTitle, tutorialBody, tutorialPage;
    private Button tutorialBack, tutorialNext;
    private LevelTimerManager propositionalClock;
    private TruthTableStageClock truthClock;
    private TruthTableMinimap truthMap;
    private PropositionalLogicTutorial propositionalTutorial;
    private TruthTableTutorial truthTutorial;
    private readonly System.Collections.Generic.List<Behaviour> pausedCameraInputs = new System.Collections.Generic.List<Behaviour>();
    private InferenceChallenge inference;
    private InferenceControlsOnboarding inferenceOnboarding;
    private PrelimChallengeCompletionNotice inferenceNotice;
    private bool inferenceFirstGuidePending = true;
    private float inferenceRemaining;
    private bool inferenceQuizOpen, inferenceCompletionPending;
    private TMP_Text inferenceDeduction;
    private TMP_Text inferenceModalClock;
    private float inferenceDeductionAt = -10;
    public float InferenceRemainingSeconds => inferenceRemaining;
    public bool InferenceQuizOpen => inferenceQuizOpen;
    private bool inferenceFinished, resultOpen, tutorialOpen, navigating;
    public bool InferenceEnded => inferenceFinished || resultOpen || inferenceRemaining <= 0f;
    private float previousTimeScale;
    private bool previousInputBlocked, previousPlayerEnabled;
    private int tutorialIndex;
    private readonly string[] tutorialTitles = { "Start and explore", "Premises", "Rules of inference", "Rule Crystals", "Crystal pickup quiz", "Conclusions", "Five challenges", "Time and feedback" };
    private readonly string[] tutorialBodies = {
        "Use the joystick to move and the jump button to jump. Approach the Book Statue and tap the hand button to start each challenge. Walk up the altar stairs and tap View Board. Complete the premises, identify their reasoning pattern, collect its Rule Crystal, and return to validate the answer.",
        "Premises are statements accepted as true and used to support a conclusion. Read the whole argument. Drag the supplied words into its blanks, then tap Check argument. Incorrect blanks stay editable; correct premises unlock the crystal search.",
        "A rule of inference is a valid reasoning pattern used to derive a conclusion from premises. Look at how the statements connect, including and, or, if/then, and negation. Choose the rule that fits that structure, rather than guessing from a crystal's appearance.",
        "A Rule Crystal is a collectible representing a specific inference rule. Its label identifies the rule. Choose the one that fits the completed premises. Near an available crystal, tap the hand button. Its white thought bubble expands to show the hand icon. You can carry one crystal at a time.",
        "Before collecting a crystal, answer one multiple-choice question about logical equivalence. Choose A–D, read Correct or Incorrect feedback, then tap Collect Rule Crystal. Either answer allows collection. An incorrect answer subtracts 10 seconds once. These quiz topics are separate from the nine inference rules on the crystals.",
        "A conclusion is the statement that logically follows from the premises using the selected rule. Carry your Rule Crystal back and tap Submit to Altar to open the board. Then tap Place Rule Crystal to check it. A correct match reveals the conclusion. Read it, then press Back or Continue. A wrong crystal is rejected and subtracts 10 seconds.",
        "Complete five questions total: one randomly selected from each challenge's bank of ten. Tasks progress from direct facts to conditionals, negation, reasoning chains, and compound dilemmas. Correct rules never repeat in one playthrough. Return to the book to start each new challenge.",
        "The shared 18-minute timer starts through the book. It pauses during Help, practice, board viewing, pickup quizzes, and between challenges. An incorrect pickup quiz subtracts 10 seconds even while paused; incorrect crystal submissions also subtract 10 seconds. Incorrect premises show feedback for correction. The book resumes the remaining time for the next challenge. Five successes complete the stage." };

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
        if (scene.name != "PRELIM" && scene.name != "TruthTable" && scene.name != "RulesOfInference") return;
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
        truthMap = FindFirstObjectByType<TruthTableMinimap>(FindObjectsInactive.Include);
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
                    if (t.name == "PropositionalHelpButton") propositionalHelp = rt;
                    else truthHelp = rt;
                    PlaceHelp(rt);
                    StagePresentation.Help(t.GetComponent<Button>(), settingsDock);
                }
            }
        }
        var gameplayCanvas = GameObject.Find("Canvas 1");
        if (gameplayCanvas != null)
        {
            settingsDock = gameplayCanvas.transform.Find("SafeArea/Gameplay_Interface/SettingsButton") as RectTransform;
        }
        LayoutHudDock();
        if (StageSelectionState.SelectedStage == 1)
            FindFirstObjectByType<QuizManager>()?.DockCompletionNotice(hud);
        else if (StageSelectionState.SelectedStage == 2) truthClock?.DockCompletionNotice(hud);
        if (SceneManager.GetActiveScene().name == "RulesOfInference")
        {
            StageSelectionState.Select(3);
            inference = FindFirstObjectByType<InferenceChallenge>(FindObjectsInactive.Include);
            inferenceNotice = PrelimChallengeCompletionNotice.GetOrCreate(safeArea, progressLabel);
            inferenceNotice.FollowHud(hud);inferenceNotice.SetHudTextSize(16,18);
            if (inference != null)
            {
                inference.onChallengeCompleted.AddListener(OnInferenceComplete);
                inference.onRoundCompleted += OnInferenceRoundCompleted;
                inference.onBookStarted += OnInferenceBookStarted;
                inference.onBoardClosed += OnInferenceBoardClosed;
            }
            BuildInferenceTutorial();
            inferenceOnboarding = FindFirstObjectByType<InferenceControlsOnboarding>(FindObjectsInactive.Include);
            if (inferenceOnboarding == null) inferenceOnboarding = gameObject.AddComponent<InferenceControlsOnboarding>();
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
        hud.gameObject.SetActive(!resultOpen && !tutorialOpen && ExpandableMinimap.OpenMap == null &&
            !(stage==3&&inferenceQuizOpen) &&
            !(stage == 2 && truthClock != null && truthClock.IsQuizOpen) &&
            !(stage == 2 && TruthTableControlsOnboarding.Active != null && TruthTableControlsOnboarding.Active.IsPracticeQuizOpen) &&
            !(stage == 2 && truthClock != null && truthClock.IsPlayerAtBoard) &&
            !(inference != null && inference.IsBoardOpen) &&
            !(inferenceOnboarding != null && inferenceOnboarding.IsPracticeBoardOpen) &&
            !(propositionalTutorial != null && propositionalTutorial.IsOpen) &&
            !(truthTutorial != null && truthTutorial.IsOpen));
        stageLabel.text = TrialCatalog.Get(stage).Title;
        if (stage == 1 && propositionalClock != null)
        {
            clockLabel.text = FormatTime(propositionalClock.RemainingTime);
            progressLabel.text = "CHALLENGE " + Mathf.Clamp(LevelTimerManager.savedTopicIndex + 1, 1, 5) + " OF 5";
        }
        else if (stage == 2 && truthClock != null)
        {
            clockLabel.text = FormatTime(truthClock.RemainingSeconds);
            progressLabel.text = "CHALLENGE " + truthClock.CurrentChallengeNumber + " OF 3\n" + truthClock.ProgressSummary;
        }
        else if (stage == 3)
        {
            if (inference != null && inference.HasStarted && !inferenceFinished && !resultOpen && !tutorialOpen &&
                (inferenceOnboarding == null || !inferenceOnboarding.IsActive) &&
                !inference.IsBoardOpen && !inferenceQuizOpen && Time.timeScale > 0)
            {
                inferenceRemaining = Mathf.Max(0, inferenceRemaining - Time.deltaTime);
                if (inferenceRemaining == 0) ShowFailure(3, "The exploration timer ran out.", RestartInference);
            }
            clockLabel.text = FormatTime(inferenceRemaining);
            progressLabel.text = inference == null ? "EXPLORE THE BOARD" : "CHALLENGE " + Mathf.Min(inference.SolvedRounds + 1, inference.requiredRounds) + " OF " + inference.requiredRounds;
            if (inferenceHelp != null) inferenceHelp.gameObject.SetActive(!resultOpen && !tutorialOpen &&
                !inferenceQuizOpen &&
                (inferenceOnboarding == null || !inferenceOnboarding.IsActive) &&
                !(inference != null && inference.IsBoardOpen));
        }
        if (inferenceDeduction != null)
        {
            float age = Time.unscaledTime - inferenceDeductionAt;
            inferenceDeduction.gameObject.SetActive(age < 2.2f && !resultOpen && !tutorialOpen);
            var color = inferenceDeduction.color; color.a = 1f - Mathf.Clamp01((age - 1.2f) / 1f); inferenceDeduction.color = color;
        }
        if(inferenceModalClock!=null)
        {
            inferenceModalClock.gameObject.SetActive(!resultOpen && !tutorialOpen && inferenceQuizOpen && !(inference != null && inference.IsBoardOpen));
            inferenceModalClock.text=FormatTime(inferenceRemaining);
        }
    }

    public void SetInferenceQuizOpen(bool open)
    {
        inferenceQuizOpen = open;
        if (!open && inferenceRemaining <= 0 && !resultOpen && !inferenceFinished)
            ShowFailure(3, "The exploration timer ran out.", RestartInference);
    }

    public void DeductInferenceSeconds(float seconds)
    {
        if (seconds <= 0 || inference == null || !inference.HasStarted || resultOpen || inferenceFinished) return;
        inferenceRemaining = Mathf.Max(0, inferenceRemaining - seconds);
        if (inferenceDeduction == null)
        {
            var overlay=StageUITheme.Rect("Inference Time Feedback",safeArea);
            StageUITheme.Stretch(overlay,Vector2.zero,Vector2.one);
            var canvas=overlay.gameObject.AddComponent<Canvas>();canvas.overrideSorting=true;canvas.sortingOrder=350;
            inferenceModalClock=StageUITheme.Text("Inference Paused Countdown",overlay,"",26);
            inferenceModalClock.outlineColor=Color.black;inferenceModalClock.outlineWidth=.2f;
            inferenceDeduction = StageUITheme.Text("InferenceTimeDeduction", overlay, "", 30, true);
            inferenceDeduction.outlineColor=Color.black;inferenceDeduction.outlineWidth=.2f;
            var rect = inferenceDeduction.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.18f, .07f); rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(0, -4); rect.sizeDelta = new Vector2(140, 40);
            inferenceDeduction.color = new Color(1, .35f, .3f); inferenceDeduction.raycastTarget = false;
        }
        inferenceDeduction.text = "−" + seconds.ToString("0.#") + "s"; inferenceDeductionAt = Time.unscaledTime;
        inferenceDeduction.color = new Color(1f, .15f, .12f, 1f);
        inferenceDeduction.gameObject.SetActive(true);
        // Keep the penalty above the quiz shade, without intercepting input.
        var penaltyCanvas = inferenceDeduction.GetComponent<Canvas>();
        if (penaltyCanvas == null) penaltyCanvas = inferenceDeduction.gameObject.AddComponent<Canvas>();
        penaltyCanvas.overrideSorting = true; penaltyCanvas.sortingOrder = 305;
        clockLabel.text = FormatTime(inferenceRemaining);
        LayoutInferenceTimeFeedback();
    }
    void LayoutInferenceTimeFeedback()
    {
        if(inferenceDeduction==null)return;
        var rect=inferenceDeduction.rectTransform;
        rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one;rect.sizeDelta=new Vector2(140,42);
        if(inferenceQuizOpen||(inference!=null&&inference.IsBoardOpen))
        {
            var clock=inferenceModalClock.rectTransform;
            clock.anchorMin=clock.anchorMax=clock.pivot=Vector2.one;clock.sizeDelta=new Vector2(200,46);clock.anchoredPosition=new Vector2(-24,-24);
            rect.anchoredPosition=new Vector2(-24,-76);
        }
        else
        {
            clockLabel.rectTransform.GetWorldCorners(dockCorners);
            rect.position=new Vector3(dockCorners[0].x-8,dockCorners[2].y,dockCorners[0].z);
        }
    }

    public static string StageName(int stage) => stage == 1 ? "Propositional Logic" : stage == 2 ? "Truth Table" : "Rules of Inference";
    public static string FormatTime(float seconds) { int total = Mathf.CeilToInt(Mathf.Max(0, seconds)); return (total / 60).ToString("00") + ":" + (total % 60).ToString("00"); }

    private void BuildHud()
    {
        hud = StageUITheme.Rect("CurrentStageHud", safeArea);
        hud.anchorMin = hud.anchorMax = Vector2.one; hud.pivot = Vector2.one;
        hud.anchoredPosition = new Vector2(-24, -24); hud.sizeDelta = new Vector2(HudWidth, HudHeight);
        StageUITheme.CompactSurface(hud.gameObject.AddComponent<Image>()); hud.GetComponent<Image>().raycastTarget = false;
        stageLabel = StageUITheme.Text("CurrentStageLabel", hud, "", 30);
        StageUITheme.Stretch(stageLabel.rectTransform, new Vector2(0.06f, 0.62f), new Vector2(0.94f, 0.86f));
        stageLabel.enableAutoSizing = true;
        stageLabel.fontSizeMin = 25f; stageLabel.fontSizeMax = 30f;
        stageLabel.textWrappingMode = TextWrappingModes.NoWrap;
        clockLabel = StageUITheme.Text("StageCountdown", hud, "", 48);
        StageUITheme.Stretch(clockLabel.rectTransform, new Vector2(0.06f, 0.18f), new Vector2(0.31f, 0.57f));
        progressLabel = StageUITheme.Text("StageProgress", hud, "", 28, true);
        StageUITheme.Stretch(progressLabel.rectTransform, new Vector2(0.32f, 0.08f), new Vector2(0.94f, 0.6f));
        progressLabel.enableAutoSizing = true;
        progressLabel.fontSizeMin = 23f; progressLabel.fontSizeMax = 28f;
        StagePresentation.Hud(hud, stageLabel, clockLabel, progressLabel);
    }

    private void LateUpdate() { LayoutHudDock(); }

    private void LayoutHudDock()
    {
        if (safeArea == null || hud == null || safeArea.rect.height <= 0f) return;
        int stage = SceneManager.GetActiveScene().name == "RulesOfInference" ? 3 : StageSelectionState.SelectedStage;
        // Keep the larger HUD readable while reserving the left settings/help pair.
        bool rightDock = stage <= 3 || Screen.height > Screen.width;
        bool compact = rightDock;
        float width = compact ? PropositionalHudWidth : HudWidth;
        hud.sizeDelta = new Vector2(width, compact ? PropositionalHudHeight : HudHeight);
        stageLabel.fontSizeMin = compact ? 22f : 25f; stageLabel.fontSizeMax = compact ? 26f : 30f;
        clockLabel.fontSize = compact ? 40f : 48f;
        progressLabel.fontSizeMin = compact ? 22f : 23f; progressLabel.fontSizeMax = compact ? 24f : 28f;
        hud.anchorMin = hud.anchorMax = new Vector2(rightDock ? 1f : 0.5f, 1f);
        hud.pivot = new Vector2(rightDock ? 1f : 0.5f, 1f);
        hud.localScale = Vector3.one * Mathf.Min(1f, (safeArea.rect.width - 48f) / width);
        hud.anchoredPosition = new Vector2(rightDock ? -24f : 0f, -24f);
        if ((stage == 2||stage==3) && truthMap != null && ExpandableMinimap.OpenMap == null)
        {
            if(stage==2)
            {
                if (truthClock == null) truthClock = FindFirstObjectByType<TruthTableStageClock>(FindObjectsInactive.Include);
                truthClock?.DockCompletionNotice(hud);
            }
            truthMap.Panel.GetWorldCorners(dockCorners);
            // Match the minimap's physical width even though the canvases scale differently.
            float mapWidth=Vector3.Distance(dockCorners[0],dockCorners[3]);
            hud.localScale=Vector3.one;
            float dockWidth=mapWidth/hud.parent.lossyScale.x;
            stageLabel.fontSizeMin=stageLabel.fontSizeMax=stageLabel.fontSize=18;
            stageLabel.textWrappingMode=stage==3?TextWrappingModes.Normal:TextWrappingModes.NoWrap;
            clockLabel.fontSize=26;progressLabel.fontSizeMin=progressLabel.fontSizeMax=progressLabel.fontSize=18;
            float contentWidth=dockWidth-24,top=12;
            foreach(var label in new[]{stageLabel,clockLabel,progressLabel})
            {
                float height=label.GetPreferredValues(label.text,contentWidth,0).y;
                var rect=label.rectTransform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,1);
                rect.anchoredPosition=new Vector2(0,-top);rect.sizeDelta=new Vector2(contentWidth,height+1);top+=height+1;
            }
            hud.sizeDelta=new Vector2(dockWidth,Mathf.Ceil(top+12));
            hud.position = dockCorners[3] - Vector3.up * (8f * hud.lossyScale.y);
            if(stage==2)truthClock?.LayoutTimeFeedback(hud);
        }
        else
        {
            StageUITheme.Stretch(stageLabel.rectTransform,new Vector2(.06f,.62f),new Vector2(.94f,.86f));
            StageUITheme.Stretch(clockLabel.rectTransform,new Vector2(.06f,.18f),new Vector2(.31f,.57f));
            StageUITheme.Stretch(progressLabel.rectTransform,new Vector2(.32f,.08f),new Vector2(.94f,.6f));
        }
        if (stage == 1) PrelimUIPresentation.CompactHud(hud, safeArea, stageLabel, clockLabel, progressLabel);
        if(stage==3)LayoutInferenceTimeFeedback();
        PlaceHelp(propositionalHelp);
        PlaceHelp(truthHelp);
        if (inferenceHelp != null) PlaceHelp((RectTransform)inferenceHelp.transform);
    }
    private void PlaceHelp(RectTransform rect)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(220f, 88f);
        if (settingsDock == null)
        {
            rect.anchoredPosition = new Vector2(230f, -43f);
            return;
        }
        float uiScale = settingsDock.parent.lossyScale.x;
        rect.localScale = Vector3.one * uiScale / Mathf.Max(0.001f, rect.parent.lossyScale.x);
        settingsDock.GetWorldCorners(dockCorners);
        if (Screen.height > Screen.width)
        {
            rect.sizeDelta = new Vector2(220f, 74f);
            rect.position = dockCorners[0] - Vector3.up * (18f * uiScale);
            return;
        }
        rect.position = new Vector3(dockCorners[2].x + 35f * uiScale,
            (dockCorners[0].y + dockCorners[1].y) * 0.5f + 44f * uiScale, dockCorners[1].z);
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
        var result = StageResultCard.Build(safeArea, ReturnToMenu);
        modal = result.Root; card = result.Card;
        resultStage = result.Stage; resultTitle = result.Heading; resultBody = result.Summary;
        primaryButton = result.Primary; secondaryButton = result.Menu; extraButton = result.Extra;
    }

    public void ShowCompletion(int stage, string summary)
    {
        if (resultOpen) return;
        ShowResult(stage, "STAGE COMPLETE", summary, stage < 3 ? "NEXT STAGE" : "PLAY AGAIN", stage < 3 ? NextStage : RestartInference);
        StagePresentation.Completed(modal.gameObject, stage, stage < 3 ? (UnityEngine.Events.UnityAction)NextStage : null);
        WorldStageAudio.PlayStageCompletion();
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
        resultStage.text = TrialCatalog.Get(stage).Title;
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
        TeleportManager.Navigate("Main Menu");
    }

    private void RestartInference()
    {
        if (navigating) return; navigating = true;
        Time.timeScale = 1; GameInput.Instance?.SetGameplayInputBlocked(false);
        StageSelectionState.Select(3);
        TeleportManager.Navigate("RulesOfInference");
    }

    private void OnInferenceComplete()
    {
        if (resultOpen) return;
        if (inference != null && inference.IsBoardOpen) { inferenceCompletionPending = true; return; }
        inferenceFinished = true;
        if (inference != null) inference.CloseBoard();
        inferenceNotice?.Hide();
        ShowResult(3, "All challenges completed!", "Argument mastered!\nRules solved: " + inference.SolvedRounds +
            "\nTime remaining: " + FormatTime(inferenceRemaining), "NEXT STAGE", () => StagePresentation.NextTrial(3));
        StagePresentation.Completed(modal.gameObject, 3);
        WorldStageAudio.PlayStageCompletion();
    }

    private void OnInferenceRoundCompleted(int completedChallenge)
    {
        inference?.ShowBookWaitingPrompt();
    }

    private void OnInferenceBoardClosed()
    {
        if(inferenceRemaining<=0&&!inferenceFinished&&!resultOpen)
        {ShowFailure(3,"The exploration timer ran out.",RestartInference);return;}
        if (inferenceCompletionPending) { inferenceCompletionPending = false; OnInferenceComplete(); }
    }

    private void OnInferenceBookStarted(int challengeNumber)
    {
        inferenceNotice?.Hide();
        inferenceNotice?.ShowStart();
    }

    private void BuildInferenceTutorial()
    {
        RectTransform inner;
        inferenceTutorial = MakeModal("InferenceTutorialOverlay", out inner).gameObject;
        var section = StageUITheme.Text("SectionLabel", inner, "Trial 3 / Field Guide", 25);
        StageUITheme.Stretch(section.rectTransform, new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.88f));
        tutorialTitle = StageUITheme.Text("PageTitle", inner, "", 52);
        StageUITheme.Stretch(tutorialTitle.rectTransform, new Vector2(0.08f, 0.63f), new Vector2(0.92f, 0.78f));
        tutorialBody = StageUITheme.Text("PageBody", inner, "", 32, true);
        StageUITheme.Stretch(tutorialBody.rectTransform, new Vector2(0.1f, 0.34f), new Vector2(0.9f, 0.61f));
        tutorialBody.alignment = TextAlignmentOptions.TopLeft;
        tutorialPage = StageUITheme.Text("PageIndicator", inner, "", 24);
        StageUITheme.Stretch(tutorialPage.rectTransform, new Vector2(0.42f, 0.27f), new Vector2(0.58f, 0.34f));
        tutorialBack = StageUITheme.Button("PreviousPage", inner, "PREVIOUS", () => ChangePage(-1));
        tutorialNext = StageUITheme.Button("NextPage", inner, "NEXT", () => ChangePage(1));
        StageUITheme.Stretch((RectTransform)tutorialBack.transform, new Vector2(0.1f, 0.12f), new Vector2(0.32f, 0.25f));
        StageUITheme.Stretch((RectTransform)tutorialNext.transform, new Vector2(0.35f, 0.12f), new Vector2(0.57f, 0.25f));
        var close = StageUITheme.Button("BeginStage", inner, "LET'S BEGIN", CloseInferenceTutorial);
        StageUITheme.Stretch((RectTransform)close.transform, new Vector2(0.6f, 0.12f), new Vector2(0.9f, 0.25f));
        StageUITheme.SkinTutorial(inferenceTutorial);
        inferenceHelp = StageUITheme.Button("InferenceHelpButton", safeArea, "HELP", OpenInferenceTutorial);
        var rt = (RectTransform)inferenceHelp.transform;
        rt.sizeDelta = new Vector2(220, 88);
        PlaceHelp(rt);
        StagePresentation.Help(inferenceHelp, settingsDock);
    }

    public void OpenInferenceTutorial()
    {
        if (TeleportManager.Instance != null && TeleportManager.Instance.IsTransitioning) return;
        if (tutorialOpen || resultOpen || inferenceQuizOpen || inferenceTutorial == null ||
            (inferenceOnboarding != null && inferenceOnboarding.IsActive)) return;
        Pause(); tutorialOpen = true; tutorialIndex = 0; ChangePage(0);
        inferenceTutorial.SetActive(true); inferenceTutorial.transform.SetAsLastSibling();
    }

    public void CloseInferenceTutorial()
    {
        if (!tutorialOpen) return;
        tutorialOpen = false; inferenceTutorial.SetActive(false); Resume();
        if (inferenceFirstGuidePending)
        {
            inferenceFirstGuidePending = false;
            inferenceOnboarding?.Begin(inferenceHelp);
        }
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
        if (inference != null)
        {
            inference.onChallengeCompleted.RemoveListener(OnInferenceComplete);
            inference.onRoundCompleted -= OnInferenceRoundCompleted;
            inference.onBookStarted -= OnInferenceBookStarted;
            inference.onBoardClosed -= OnInferenceBoardClosed;
        }
        if (Instance == this) Instance = null;
        if (resultOpen || tutorialOpen) Time.timeScale = 1;
    }
}
