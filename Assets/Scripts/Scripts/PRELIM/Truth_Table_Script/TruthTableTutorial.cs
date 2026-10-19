using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Stage-specific Help overlay on the existing PRELIM Canvas.</summary>
public sealed class TruthTableTutorial : MonoBehaviour
{
    [Header("Existing PRELIM Canvas UI")]
    [SerializeField] private GameObject tutorialOverlay;
    [SerializeField] private Button helpButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button gotItButton;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private TextMeshProUGUI pageText;
    [SerializeField] private TruthTableStageClock stageClock;
    [SerializeField] private TruthTableDoorInteraction doors;

    private readonly (string title, string body)[] pages =
    {
        ("What is a Truth Table?",
            "A Truth Table lists every combination of truth values for statements and shows whether a logical expression is True or False. P and Q represent statements. In this stage, evaluate each expression, collect the matching True/False element blocks, and complete the required column."),
        ("Read the operators",
            "AND (∧) is True only when both inputs are True. Inclusive OR (∨) is True when at least one input is True. Exclusive OR (⊕) is True when exactly one input is True. NOT (~ or ¬) reverses a truth value; it also appears in house quizzes."),
        ("Conditionals and brackets",
            "Implication (P → Q) is False only when P is True and Q is False. Biconditional (P ↔ Q) is True when both truth values match. Evaluate expressions inside brackets first. Medium and Hard combine these operators, so work through the expression one part at a time."),
        ("Rows, inputs and answers",
            "The four numbered rows organize the P/Q combinations: True/True, True/False, False/True, and False/False. Row numbers identify positions, not separate challenges. A True or False element block represents the expression's output for that row. Read the P/Q values and expression on the Truth Table board."),
        ("Start and explore",
            "Approach the Book Statue and tap the hand button to start a challenge. Then explore the six houses. A small white bubble marks an available interaction and expands to show the hand icon when you are nearby. The doors stay locked before book activation; closing Help never starts the timer."),
        ("Answer the house quizzes",
            "At a closed house door, tap the hand button and choose one of four quiz answers. These quizzes check your understanding of logic; they are separate from the board's rows. Either answer opens that door. An incorrect answer costs 10 seconds and shows the correct answer before the quiz closes. Correct answers give no extra time."),
        ("Collect element blocks",
            "Once a house door is open, enter and look for True/False element blocks. Their interaction bubbles appear only while that house is open and the blocks can be collected. Approach a block and tap the hand button. The existing inventory groups True and False blocks; tap a stack to select the type you need."),
        ("Fill and submit a column",
            "Read the highlighted row's P/Q values. Face and line up with the required Truth Table column, then use the hand button while holding the selected element block to drop it into the highlighted slot. Fill all four rows in order, then tap Submit Column. Submit checks the entire column, not just the last block."),
        ("Correct mistakes",
            "An incorrect column costs 20 seconds and returns its four element blocks to your inventory. Retry the same expression; the doors stay as they are. An invalid placement returns that element block to a house and resets the doors and their quizzes. It does not itself deduct time. Select the needed stack and try again."),
        ("Three challenges, nine columns",
            "Complete Easy, Medium, and Hard in order. Each challenge selects three distinct expressions from its question bank: three columns of four rows. After a correct column, the houses receive new blocks and all doors close with new quizzes. After three correct columns, return to the book to start the next challenge. Completing Hard finishes the stage."),
        ("Time and retries",
            "Each challenge starts with 18:00. Time pauses during Help, tutorial practice, door quizzes, and while you stand in the marked board area. Penalties still apply while paused: −10 seconds for an incorrect door quiz and −20 for an incorrect column. Red time deductions appear beside the timer and fade out. At 00:00, Restart begins again at Easy."),
        ("Move and read",
            "Use the joystick to move, the jump button to jump, and the hand button for nearby interactions. Hold a movement direction for 3 seconds to run at 1.5 times walking speed. On desktop, WASD, jump, and E remain available. Tap the minimap to inspect the whole stage; Close restores the compact view. The challenge and timer wait for the book between challenges.")
    };

    private int pageIndex;
    readonly Dictionary<RectTransform, GuidePose> guidePoses=new Dictionary<RectTransform, GuidePose>();
    RectTransform guideCard;
    struct GuidePose
    {
        public Vector2 min,max,pivot,size,position;
        public Vector3 scale;
        public GuidePose(RectTransform rect){min=rect.anchorMin;max=rect.anchorMax;pivot=rect.pivot;size=rect.sizeDelta;position=rect.anchoredPosition;scale=rect.localScale;}
        public void Restore(RectTransform rect){rect.anchorMin=min;rect.anchorMax=max;rect.pivot=pivot;rect.sizeDelta=size;rect.anchoredPosition=position;rect.localScale=scale;}
    }
    private bool hasShownAutomatically;
    private bool isOpen;
    private bool startControlsOnClose;
    private TextMeshProUGUI gotItLabel;
    private string originalGotItLabel;
    private GameInput gameInput;
    private Player pausedPlayer;
    private CinemachinePinchZoom pinchZoom;
    private ThirdPersonCameraController cameraController;
    private bool previousInputBlocked;
    private bool previousPlayerEnabled;
    private bool previousPinchEnabled;
    private bool previousCameraEnabled;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLock;
    private float previousTimeScale;

    public bool IsOpen => isOpen;
    public int PageIndex => pageIndex;
    public int PageCount => pages.Length;

    private void Awake()
    {
        if (stageClock == null) stageClock = GetComponent<TruthTableStageClock>();
        if (doors == null) doors = GetComponent<TruthTableDoorInteraction>();
        if (tutorialOverlay != null) tutorialOverlay.SetActive(false);
        if (helpButton != null) helpButton.gameObject.SetActive(false);

        if (helpButton != null) helpButton.onClick.AddListener(OpenTutorial);
        if (backButton != null) backButton.onClick.AddListener(PreviousPage);
        if (nextButton != null) nextButton.onClick.AddListener(NextPage);
        if (gotItButton != null) gotItButton.onClick.AddListener(CloseTutorial);
        if (gotItButton != null)
        {
            gotItLabel = gotItButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (gotItLabel != null) originalGotItLabel = gotItLabel.text;
        }
    }

    private void Start()
    {
        if (StageSelectionState.SelectedStage == 2)
            StartCoroutine(OpenAfterStageArrival());
    }

    private IEnumerator OpenAfterStageArrival()
    {
        // PRELIM's existing stage transition teleports the player after a
        // FixedUpdate. Let it finish before pausing scaled gameplay time.
        while (Player.LocalInstance == null && StageSelectionState.SelectedStage == 2)
            yield return null;
        if (StageSelectionState.SelectedStage != 2) yield break;
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return null;
        while (TeleportManager.Instance != null && TeleportManager.Instance.IsTransitioning)
            yield return null;
        if (!hasShownAutomatically && StageSelectionState.SelectedStage == 2)
            OpenTutorial();
    }

    private void Update()
    {
        if (helpButton == null || isOpen || !hasShownAutomatically) return;
        bool available = StageSelectionState.SelectedStage == 2 &&
                         stageClock != null && !stageClock.IsGameOver &&
                         !stageClock.IsCompleted && !stageClock.IsPlayerAtBoard && (doors == null || !doors.IsQuizOpen) &&
                         (TruthTableControlsOnboarding.Active == null ||
                          !TruthTableControlsOnboarding.Active.IsActive);
        if (helpButton.gameObject.activeSelf != available)
            helpButton.gameObject.SetActive(available);
    }

    public void OpenTutorial()
    {
        if (TeleportManager.Instance != null && TeleportManager.Instance.IsTransitioning) return;
        if (isOpen || StageSelectionState.SelectedStage != 2 || tutorialOverlay == null ||
            stageClock == null || stageClock.IsGameOver || stageClock.IsCompleted ||
            (doors != null && doors.IsQuizOpen) || Time.timeScale <= 0f) return;

        gameInput = GameInput.Instance;
        if (gameInput == null) gameInput = FindFirstObjectByType<GameInput>();
        pausedPlayer = Player.LocalInstance;
        pinchZoom = FindFirstObjectByType<CinemachinePinchZoom>();
        cameraController = FindFirstObjectByType<ThirdPersonCameraController>();

        previousTimeScale = Time.timeScale;
        previousInputBlocked = gameInput != null && gameInput.GameplayInputBlocked;
        previousPlayerEnabled = pausedPlayer != null && pausedPlayer.enabled;
        previousPinchEnabled = pinchZoom != null && pinchZoom.enabled;
        previousCameraEnabled = cameraController != null && cameraController.enabled;
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;

        stageClock.SetTutorialOpen(true);
        gameInput?.SetGameplayInputBlocked(true);
        if (pausedPlayer != null) pausedPlayer.enabled = false;
        if (pinchZoom != null) pinchZoom.enabled = false;
        if (cameraController != null) cameraController.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;

        startControlsOnClose = !hasShownAutomatically;
        if (gotItLabel != null) gotItLabel.text = startControlsOnClose ? "Let’s Begin" : originalGotItLabel;
        isOpen = true;
        hasShownAutomatically = true;
        pageIndex = 0;
        if (helpButton != null) helpButton.gameObject.SetActive(false);
        tutorialOverlay.SetActive(true);
        tutorialOverlay.transform.SetAsLastSibling();
        RefreshPage();
    }

    public void CloseTutorial()
    {
        if (!isOpen) return;
        bool beginControls = startControlsOnClose;
        startControlsOnClose = false;
        isOpen = false;
        if (tutorialOverlay != null) tutorialOverlay.SetActive(false);
        stageClock?.SetTutorialOpen(false);
        if (gameInput != null) gameInput.SetGameplayInputBlocked(previousInputBlocked);
        if (pausedPlayer != null) pausedPlayer.enabled = previousPlayerEnabled;
        if (pinchZoom != null) pinchZoom.enabled = previousPinchEnabled;
        if (cameraController != null) cameraController.enabled = previousCameraEnabled;
        Time.timeScale = previousTimeScale;
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
        if (gotItLabel != null) gotItLabel.text = originalGotItLabel;
        if (beginControls && StageSelectionState.SelectedStage == 2)
        {
            TruthTableControlsOnboarding onboarding = GetComponent<TruthTableControlsOnboarding>();
            if (onboarding == null) onboarding = gameObject.AddComponent<TruthTableControlsOnboarding>();
            onboarding.Begin(helpButton);
        }
        else if (helpButton != null && StageSelectionState.SelectedStage == 2 &&
            stageClock != null && !stageClock.IsGameOver && !stageClock.IsCompleted && !stageClock.IsPlayerAtBoard)
            helpButton.gameObject.SetActive(true);
    }

    public void NextPage()
    {
        if (!isOpen || pageIndex >= pages.Length - 1) return;
        pageIndex++;
        RefreshPage();
    }

    public void PreviousPage()
    {
        if (!isOpen || pageIndex <= 0) return;
        pageIndex--;
        RefreshPage();
    }

    private void RefreshPage()
    {
        if (titleText != null) titleText.text = pages[pageIndex].title;
        if (bodyText != null) bodyText.text = pages[pageIndex].body;
        if (pageText != null) pageText.text = $"{pageIndex + 1} / {pages.Length}";
        if (backButton != null) backButton.interactable = pageIndex > 0;
        if (nextButton != null) nextButton.interactable = pageIndex < pages.Length - 1;
        LayoutGuide();
    }

    private void LateUpdate(){if(isOpen)LayoutGuide();}
    private void LayoutGuide()
    {
        if(titleText==null||bodyText==null)return;
        if(guideCard==null)
        {
            guideCard=(RectTransform)titleText.transform.parent;
            guidePoses[guideCard]=new GuidePose(guideCard);
            foreach(Transform child in guideCard)if(child is RectTransform rect)guidePoses[rect]=new GuidePose(rect);
        }
        foreach(var saved in guidePoses)if(saved.Key!=null)saved.Value.Restore(saved.Key);
        bool portrait=Screen.height>Screen.width;
        foreach(var name in new[]{"RightReadingPage","BookSpine","FieldNotesHeading","FieldNotesBody"})
        {var child=guideCard.Find(name);if(child!=null)child.gameObject.SetActive(!portrait);}
        if(!portrait)
        {
            bodyText.fontSizeMin=24;bodyText.fontSizeMax=31;titleText.fontSizeMin=32;titleText.fontSizeMax=44;return;
        }
        StageUITheme.Stretch(guideCard,new Vector2(.02f,.04f),new Vector2(.98f,.96f));
        var paper=guideCard.Find("LeftReadingPage") as RectTransform;
        if(paper!=null)StageUITheme.Stretch(paper,new Vector2(.04f,.24f),new Vector2(.96f,.87f));
        var section=guideCard.Find("SectionLabel") as RectTransform;
        if(section!=null)StageUITheme.Stretch(section,new Vector2(.08f,.89f),new Vector2(.92f,.96f));
        StageUITheme.Stretch(titleText.rectTransform,new Vector2(.09f,.75f),new Vector2(.91f,.84f));
        StageUITheme.Stretch(bodyText.rectTransform,new Vector2(.09f,.28f),new Vector2(.91f,.73f));
        bodyText.enableAutoSizing=true;bodyText.fontSizeMin=22;bodyText.fontSizeMax=27;
        titleText.enableAutoSizing=true;titleText.fontSizeMin=26;titleText.fontSizeMax=36;
        StageUITheme.Stretch((RectTransform)backButton.transform,new Vector2(.06f,.12f),new Vector2(.36f,.21f));
        StageUITheme.Stretch(pageText.rectTransform,new Vector2(.39f,.12f),new Vector2(.61f,.21f));
        StageUITheme.Stretch((RectTransform)nextButton.transform,new Vector2(.64f,.12f),new Vector2(.94f,.21f));
        StageUITheme.Stretch((RectTransform)gotItButton.transform,new Vector2(.22f,.025f),new Vector2(.78f,.11f));
    }

    private void OnDisable()
    {
        startControlsOnClose = false;
        CloseTutorial();
        if (helpButton != null) helpButton.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (helpButton != null) helpButton.onClick.RemoveListener(OpenTutorial);
        if (backButton != null) backButton.onClick.RemoveListener(PreviousPage);
        if (nextButton != null) nextButton.onClick.RemoveListener(NextPage);
        if (gotItButton != null) gotItButton.onClick.RemoveListener(CloseTutorial);
    }
}
