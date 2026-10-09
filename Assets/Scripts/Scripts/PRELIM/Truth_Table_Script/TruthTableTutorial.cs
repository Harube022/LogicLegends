using System.Collections;
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
        ("Start at the book",
            "Walk to Book_Statue and tap Interact to start each challenge. The house doors stay locked until you start. Closing this guide does not start the challenge."),
        ("Three challenges",
            "Finish Easy, then Medium, then Hard. Each challenge has three randomly chosen truth-table expressions, one for each TRUTIBOL column. Read the expression on the board and fill its four P/Q rows."),
        ("Open the houses",
            "Approach a closed house door and tap INTERACT WITH DOOR, or press E on a keyboard. Choose one of four quiz answers. Either answer opens that door. A wrong answer costs 10 seconds; a correct answer gives no extra time."),
        ("Collect the blocks",
            "Use the on-screen joystick to search the houses. Near a True or False block, tap Interact to collect it. Tap its inventory slot to select it. On a keyboard, use WASD to move and E to interact."),
        ("Place each row",
            "Check the P and Q values for the highlighted row. Face and line up with the current TRUTIBOL column, then tap Interact while holding the selected block to drop it into the highlighted slot. Place the four rows in board order."),
        ("Submit your column",
            "After placing all four blocks, tap Submit Column. A correct column moves you forward. A wrong column costs 20 seconds, returns that attempt's blocks to houses, and lets you retry the same expression."),
        ("New houses, new quizzes",
            "Returned blocks appear immediately at fresh house spawn points. After a column submission, all six doors close, their quizzes change, and you must answer again to enter. An invalid placement also returns that block and resets the doors. Use the minimap and each door's open/closed state to plan your search."),
        ("Watch the clock",
            "Each challenge starts with 18:00. The clock pauses while a door quiz is open or while you stand in the marked area in front of TRUTIBOL. Wrong-answer penalties still count while paused. At 00:00, the stage ends in Game Over."),
        ("Start the next challenge",
            "After three correct columns, the next challenge waits at 18:00. Return to Book_Statue and tap Interact to begin it. After Hard's third column, choose NEXT STAGE or MAIN MENU on the completion screen."),
        ("Move and retry",
            "Keep holding the joystick in a direction for 3 seconds to run at 1.5 times walking speed. Releasing it resets the run. On a keyboard, hold WASD. If time runs out, tap Restart to begin again at Easy, then use the book to start the clock.")
    };

    private int pageIndex;
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
