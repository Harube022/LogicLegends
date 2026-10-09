using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The existing Propositional Logic HUD's stage-specific Help overlay.</summary>
public sealed class PropositionalLogicTutorial : MonoBehaviour
{
    [Header("Existing PRELIM Canvas UI")]
    [SerializeField] private GameObject tutorialOverlay;
    [SerializeField] private Button helpButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button beginButton;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private TextMeshProUGUI pageText;
    [SerializeField] private LevelTimerManager timerManager;

    private readonly (string title, string body)[] pages =
    {
        ("Start the challenge",
            "Move to the book statue in your current room. Tap the on-screen READ button to reveal the question and start the shared countdown. Closing this guide does not start the timer."),
        ("Read the question",
            "Read the description at the top of the screen. Decide which logical operation it describes, then compare the four answer labels shown above the doors."),
        ("Choose a door",
            "Use the on-screen joystick to walk to the selection pad in front of your answer. Stay on the pad for about 2 seconds. Its ring fills, the door clears, and your character moves through it. On a keyboard, use WASD to reach the pad."),
        ("Use the hints",
            "The truth-table board helps you check the operation. While the challenge is active, it reveals one answer row every 20 seconds, up to four rows. The reveal pauses with this guide."),
        ("Watch your time",
            "A fresh run begins with 6 minutes shared across the five challenges. A wrong door takes 10 seconds away. A hammer knocks you back and stuns you for about 5 seconds; then you can try the same challenge again."),
        ("Complete all five",
            "A correct door sends you to the next challenge room and pauses the countdown. Find that room’s book statue and tap READ again to reveal the next question and resume your remaining time. The challenge order is randomized."),
        ("Finish or retry",
            "Finish all five challenges to continue to Truth Table. If time reaches zero, Game Over offers Start Over or Try Again. Try Again resumes at your checkpoint with a shorter timer (up to two retries); after that, you can study in Logic Garden or return to the Main Menu.")
    };

    private int pageIndex;
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
    private bool isOpen;
    private bool startControlsAfterClose;

    public bool IsOpen => isOpen;
    public int PageIndex => pageIndex;
    public int PageCount => pages.Length;

    private void Awake()
    {
        if (tutorialOverlay != null) tutorialOverlay.SetActive(false);
        if (helpButton != null) helpButton.gameObject.SetActive(false);

        if (helpButton != null) helpButton.onClick.AddListener(OpenTutorial);
        if (backButton != null) backButton.onClick.AddListener(PreviousPage);
        if (nextButton != null) nextButton.onClick.AddListener(NextPage);
        if (beginButton != null) beginButton.onClick.AddListener(CloseTutorial);
    }

    private void Start()
    {
        if (StageSelectionState.SelectedStage != 1) return;
        if (timerManager == null) timerManager = FindFirstObjectByType<LevelTimerManager>();
        StartCoroutine(OpenAfterInitialRoomSpawn());
    }

    private IEnumerator OpenAfterInitialRoomSpawn()
    {
        // QuizManager teleports the new player on a FixedUpdate. Let that
        // existing room setup finish before pausing scaled gameplay time.
        while (Player.LocalInstance == null && StageSelectionState.SelectedStage == 1)
            yield return null;

        if (StageSelectionState.SelectedStage != 1) yield break;
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return null;
        if (StageSelectionState.SelectedStage == 1)
        {
            startControlsAfterClose = true;
            OpenTutorial();
        }
    }

    public void OpenTutorial()
    {
        if (isOpen || StageSelectionState.SelectedStage != 1 || tutorialOverlay == null ||
            Time.timeScale <= 0f) return;

        if (timerManager == null) timerManager = FindFirstObjectByType<LevelTimerManager>();
        gameInput = FindFirstObjectByType<GameInput>();
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

        timerManager?.SetTutorialPaused(true);
        gameInput?.SetGameplayInputBlocked(true);
        if (pausedPlayer != null) pausedPlayer.enabled = false;
        if (pinchZoom != null) pinchZoom.enabled = false;
        if (cameraController != null) cameraController.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
        isOpen = true;
        pageIndex = 0;
        if (helpButton != null) helpButton.gameObject.SetActive(false);
        tutorialOverlay.SetActive(true);
        tutorialOverlay.transform.SetAsLastSibling();
        RefreshPage();
    }

    public void CloseTutorial()
    {
        if (!isOpen) return;
        isOpen = false;
        if (tutorialOverlay != null) tutorialOverlay.SetActive(false);
        if (helpButton != null && StageSelectionState.SelectedStage == 1)
            helpButton.gameObject.SetActive(true);

        timerManager?.SetTutorialPaused(false);
        if (gameInput != null) gameInput.SetGameplayInputBlocked(previousInputBlocked);
        if (pausedPlayer != null) pausedPlayer.enabled = previousPlayerEnabled;
        if (pinchZoom != null) pinchZoom.enabled = previousPinchEnabled;
        if (cameraController != null) cameraController.enabled = previousCameraEnabled;
        Time.timeScale = previousTimeScale;
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
        if (startControlsAfterClose && StageSelectionState.SelectedStage == 1)
        {
            startControlsAfterClose = false;
            PropositionalControlsOnboarding controls = GetComponent<PropositionalControlsOnboarding>();
            if (controls == null) controls = gameObject.AddComponent<PropositionalControlsOnboarding>();
            controls.Begin(helpButton);
        }
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
        startControlsAfterClose = false;
        CloseTutorial();
        if (helpButton != null) helpButton.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (helpButton != null) helpButton.onClick.RemoveListener(OpenTutorial);
        if (backButton != null) backButton.onClick.RemoveListener(PreviousPage);
        if (nextButton != null) nextButton.onClick.RemoveListener(NextPage);
        if (beginButton != null) beginButton.onClick.RemoveListener(CloseTutorial);
    }
}
