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
        ("Propositions and truth values",
            "A proposition is a statement that is true (T) or false (F). P and Q stand for propositions. Logical operators combine them into a compound proposition. This stage asks you to identify five operators from descriptions and truth-table patterns."),
        ("AND, OR, and XOR",
            "AND (P ∧ Q) is true only when both propositions are true. OR (P ∨ Q) is inclusive: it is true when at least one is true, including both. Exclusive OR (XOR) is true when exactly one is true."),
        ("If–then and if and only if",
            "A conditional P → Q means 'if P, then Q'. P is the antecedent and Q the consequent; it is false only when P is true and Q is false. A biconditional P ↔ Q means 'P if and only if Q'; it is true when both have the same truth value."),
        ("Activate, read, and decide",
            "Approach the current room's Book Statue and tap the hand button. This reveals a question and starts or resumes the countdown. Read the question at the top, use the hint board, compare the four answer doors, and choose the matching operator."),
        ("Use the hint board",
            "The board shows the operator's truth table: each row pairs truth values for P and Q with a result. During an active challenge, one result appears every 20 seconds, up to four rows. Help pauses both the countdown and hint reveals."),
        ("Choose an answer door",
            "Walk onto the selection pad in front of your answer using the joystick. Stay there for about 2 seconds as its ring fills. The door opens and your character moves through it. Leaving the pad cancels the selection."),
        ("Time and incorrect answers",
            "A fresh run has 6 minutes shared across all five challenges. A wrong door subtracts 10 seconds, knocks you back, and stuns you for about 5 seconds. After recovery, activate the book for a new question in the same challenge. The remaining countdown is preserved; it does not reset."),
        ("Complete each challenge",
            "A correct door completes the challenge, moves you to the next room, and pauses the countdown. The message below the HUD tells you to activate that room's book. The hand button reveals the next question and resumes your remaining time. Challenge order is randomized."),
        ("Game Over and retries",
            "At zero time, Start Over begins a fresh six-minute run. Try Again keeps your challenge progress with 4 minutes for Challenges 1–3 or 3 minutes for Challenges 4–5. You have two checkpoint retries; afterward, the middle option becomes Study in Logic Garden. Main Menu is also available."),
        ("Finish the stage",
            "Complete all five challenges. Play Next Stage opens Truth Table; Go to Menu returns to Main Menu. Reopening Help pauses gameplay and preserves your previous state. Closing Help never activates a book or starts a challenge.")
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
    private int helpLayoutMode = -1;

    public bool IsOpen => isOpen;
    public int PageIndex => pageIndex;
    public int PageCount => 1;

    // Keep the same parchment artwork, but use one full-width page on narrow screens.
    private void LateUpdate()
    {
        if (!isOpen || titleText == null || bodyText == null || titleText.GetComponentInParent<StageHelpPage>() != null) return;
        bool portrait = Screen.height > Screen.width;
        int mode = portrait ? 1 : 0;
        if (helpLayoutMode == mode) return;
        helpLayoutMode = mode;
        var card = titleText.transform.parent;
        var left = card.Find("LeftReadingPage") as RectTransform;
        var right = card.Find("RightReadingPage");
        var spine = card.Find("BookSpine");
        var referenceTitle = card.Find("FieldNotesHeading");
        var referenceBody = card.Find("FieldNotesBody");
        if (left != null) StageUITheme.Stretch(left, new Vector2(.045f, .25f), new Vector2(portrait ? .955f : .49f, .85f));
        if (right != null) right.gameObject.SetActive(!portrait);
        if (spine != null) spine.gameObject.SetActive(!portrait);
        if (referenceTitle != null) referenceTitle.gameObject.SetActive(!portrait);
        if (referenceBody != null) referenceBody.gameObject.SetActive(!portrait);
        StageUITheme.Stretch(titleText.rectTransform, new Vector2(portrait ? .13f : .08f, portrait ? .66f : .67f), new Vector2(portrait ? .87f : .46f, portrait ? .80f : .78f));
        StageUITheme.Stretch(bodyText.rectTransform, new Vector2(portrait ? .13f : .08f, .30f), new Vector2(portrait ? .87f : .46f, portrait ? .64f : .65f));
        titleText.fontSizeMax = portrait ? 36 : 44;
        bodyText.fontSizeMax = portrait ? 27 : 31;
        if (backButton != null) StageUITheme.Stretch((RectTransform)backButton.transform, new Vector2(.09f, portrait ? .16f : .10f), new Vector2(portrait ? .39f : .28f, portrait ? .23f : .21f));
        if (nextButton != null) StageUITheme.Stretch((RectTransform)nextButton.transform, new Vector2(portrait ? .61f : .30f, portrait ? .16f : .10f), new Vector2(portrait ? .91f : .49f, portrait ? .23f : .21f));
        if (pageText != null) StageUITheme.Stretch(pageText.rectTransform, new Vector2(portrait ? .40f : .50f, portrait ? .16f : .10f), new Vector2(portrait ? .60f : .62f, portrait ? .23f : .21f));
        if (beginButton != null) StageUITheme.Stretch((RectTransform)beginButton.transform, new Vector2(portrait ? .20f : .64f, portrait ? .05f : .10f), new Vector2(portrait ? .80f : .91f, portrait ? .12f : .21f));
    }

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
        while (TeleportManager.Instance != null && TeleportManager.Instance.IsTransitioning)
            yield return null;
        if (StageSelectionState.SelectedStage == 1)
        {
            startControlsAfterClose = true;
            OpenTutorial();
        }
    }

    public void OpenTutorial()
    {
        if (TeleportManager.Instance != null && TeleportManager.Instance.IsTransitioning) return;
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

    // Close restores play; only Let's Begin launches the interactive lesson.
    private void CloseHelpOnly()
    {
        bool pending = startControlsAfterClose;
        startControlsAfterClose = false;
        CloseTutorial();
        startControlsAfterClose = pending;
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
        if (titleText != null) StageHelpPage.Install((RectTransform)titleText.transform.parent, "PRELIM", CloseHelpOnly, CloseTutorial, startControlsAfterClose);
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
