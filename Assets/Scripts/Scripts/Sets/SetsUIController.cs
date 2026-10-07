using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class SetsUIController : MonoBehaviour
{
    [Header("Optional Scene UI Overrides")]
    [SerializeField] private RectTransform safeAreaRoot;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI challengeText;
    [SerializeField] private TextMeshProUGUI setAText;
    [SerializeField] private TextMeshProUGUI setBText;
    [SerializeField] private TextMeshProUGUI universalSetText;
    [SerializeField] private TextMeshProUGUI instructionText;
    [SerializeField] private TextMeshProUGUI promptText;
    [SerializeField] private TextMeshProUGUI feedbackText;
    [SerializeField] private Button placeButton;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject stageCompletePanel;

    [Header("Temporary Sets HUD Visibility")]
    [SerializeField] private bool showChallengeInformation;
    [SerializeField] private bool showTimerDisplay;
    [SerializeField] private bool showBookStartBanner;

    private SetsStageManager stageManager;
    private GameOverManager gameOverManager;
    private StageCompleteManager stageCompleteManager;
    private GameObject hudRoot;
    private Image feedbackBackground;
    private GameObject helpOverlay;
    private Button helpButton;
    private Button helpBackButton;
    private Button helpNextButton;
    private TextMeshProUGUI helpTitle;
    private TextMeshProUGUI helpBody;
    private TextMeshProUGUI helpPage;
    private int helpPageIndex;
    private bool helpOpen;
    private bool previousInputBlocked;
    private bool previousPlayerEnabled;
    private bool previousCameraEnabled;
    private bool previousPinchEnabled;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLock;
    private float previousTimeScale;
    private GameInput pausedInput;
    private Player pausedPlayer;
    private ThirdPersonCameraController pausedCamera;
    private CinemachinePinchZoom pausedPinch;
    private TMP_FontAsset headingFont;
    private RectTransform promptCard;
    private float neutralFeedbackClearAt;
    private static readonly Color Ink = new Color(0.025f, 0.14f, 0.15f, 0.94f);
    private static readonly Color Gold = new Color(0.93f, 0.76f, 0.42f);
    private static readonly (string title, string body)[] HelpPages =
    {
        ("Start at the book", "Approach the Book Statue and tap Interact to begin the current Sets challenge. The timer waits until you start. Each new challenge also begins at the book."),
        ("Read the sets", "Check A, B, and U on the Sets information board. Follow the current objective: find the indicated element or construct a union, intersection, difference, or complement."),
        ("Collect an element", "Move with the on-screen joystick. When an element is nearby, tap Interact to carry it. On a keyboard, use WASD to move and E to interact."),
        ("Place it in the Venn diagram", "Carry the element into the matching region of the Venn diagram. Tap PLACE when the region prompt appears. You may need A only, A ∩ B, B only, or the area outside A but inside U."),
        ("Correct mistakes", "A correct placement stays in the diagram. A wrong placement costs 10 seconds, briefly stops movement, and returns that element so you can try again. The timer begins at 03:00; reaching zero ends the stage."),
        ("Finish the stage", "Place every required element to complete a challenge. The next challenge waits for another book interaction. Finish all five challenges to complete Sets. If time runs out, use RETRY to start over.")
    };
    private Color neutralFeedback = new Color(0.04f, 0.09f, 0.15f, 0.9f);
    private Color correctFeedback = new Color(0.08f, 0.42f, 0.18f, 0.95f);
    private Color wrongFeedback = new Color(0.55f, 0.08f, 0.08f, 0.95f);

    public TextMeshProUGUI TimerText => timerText;
    public GameObject HudRoot => hudRoot;
    public GameObject GameOverPanel => gameOverPanel;
    public GameObject StageCompletePanel => stageCompletePanel;

    private void Awake()
    {
        BuildHudIfNeeded();
    }

    private void LateUpdate()
    {
        if (hudRoot == null || promptCard == null) return;
        Rect rect = ((RectTransform)hudRoot.transform).rect;
        if (rect.height <= 0f) return;
        // Landscape tablets leave less room between the two touch controls.
        // Lift the prompt above the joystick on those narrower screens.
        float y = rect.width / rect.height < 1.55f ? 520f : 134f;
        if (!Mathf.Approximately(promptCard.anchoredPosition.y, y))
            promptCard.anchoredPosition = new Vector2(0f, y);
    }

    private void Update()
    {
        if (neutralFeedbackClearAt > 0f && Time.time >= neutralFeedbackClearAt)
            ClearFeedback();
    }

    public void Configure(SetsStageManager manager, GameOverManager gameOver, StageCompleteManager stageComplete)
    {
        stageManager = manager;
        gameOverManager = gameOver;
        stageCompleteManager = stageComplete;
        if (placeButton != null)
        {
            placeButton.onClick.RemoveListener(PlaceCurrentElement);
            placeButton.onClick.AddListener(PlaceCurrentElement);
        }
    }

    public void DisplayChallenge(SetsChallengeData challenge, int displayNumber, int totalChallenges, bool waitingForActivation)
    {
        if (challenge == null) return;
        BuildHudIfNeeded();

        if (challengeText != null)
            challengeText.text = $"CHALLENGE {displayNumber} / {totalChallenges} — {challenge.challengeType}";
        if (setAText != null) setAText.text = "A = " + challenge.FormatSet(challenge.setA);
        if (setBText != null) setBText.text = challenge.setB != null && challenge.setB.Length > 0
            ? "B = " + challenge.FormatSet(challenge.setB)
            : "B = ∅";
        if (universalSetText != null)
        {
            bool hasUniversal = challenge.universalSet != null && challenge.universalSet.Length > 0;
            universalSetText.gameObject.SetActive(hasUniversal);
            if (hasUniversal) universalSetText.text = "U = " + challenge.FormatSet(challenge.universalSet);
        }
        if (instructionText != null)
            instructionText.text = waitingForActivation ? "Interact with the Book Statue to begin." : challenge.instruction;
        SetPrompt(waitingForActivation ? "INTERACT WITH BOOK STATUE" : string.Empty);
        if (placeButton != null) placeButton.gameObject.SetActive(false);
        ClearFeedback();
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (stageCompletePanel != null) stageCompletePanel.SetActive(false);
    }

    public void SetTimer(float seconds)
    {
        if (timerText == null) return;
        int safeSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
        timerText.text = $"{safeSeconds / 60:00}:{safeSeconds % 60:00}";
    }

    public void ShowPickupPrompt(bool canPickUp)
    {
        SetPrompt(canPickUp ? "[PICK UP]  Use INTERACT" : string.Empty);
        if (placeButton != null) placeButton.gameObject.SetActive(false);
    }

    public void ShowPlacementPrompt(bool canPlace, SetZone? zone)
    {
        SetPrompt(!canPlace ? "Carry the element into a Venn region." :
            "[PLACE]  " + SetsStageManager.FormatZone(zone.Value));

        if (placeButton != null) placeButton.gameObject.SetActive(canPlace);
    }

    public void ShowFeedback(string message, SetsFeedbackKind kind)
    {
        BuildHudIfNeeded();
        if (feedbackText == null) return;
        feedbackText.text = message;
        feedbackText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        neutralFeedbackClearAt = kind == SetsFeedbackKind.Neutral && !string.IsNullOrEmpty(message)
            ? Time.time + 2.5f : 0f;

        if (feedbackBackground != null)
        {
            feedbackBackground.enabled = !string.IsNullOrEmpty(message);
            switch (kind)
            {
                case SetsFeedbackKind.Correct: feedbackBackground.color = correctFeedback; break;
                case SetsFeedbackKind.Wrong: feedbackBackground.color = wrongFeedback; break;
                default: feedbackBackground.color = neutralFeedback; break;
            }
        }
    }

    public void ClearFeedback()
    {
        neutralFeedbackClearAt = 0f;
        if (feedbackText != null)
        {
            feedbackText.text = string.Empty;
            feedbackText.gameObject.SetActive(false);
        }
        if (feedbackBackground != null) feedbackBackground.enabled = false;
    }

    public void ShowChallengeComplete(int challengeNumber)
    {
        if (placeButton != null) placeButton.gameObject.SetActive(false);
        SetPrompt(string.Empty);
        ShowFeedback($"CHALLENGE {challengeNumber} COMPLETE", SetsFeedbackKind.Correct);
    }

    public void ShowStageComplete()
    {
        if (placeButton != null) placeButton.gameObject.SetActive(false);
        SetPrompt(string.Empty);
        ClearFeedback();
        if (helpButton != null) helpButton.gameObject.SetActive(false);
        if (stageCompletePanel != null)
        {
            stageCompletePanel.SetActive(true);
            return;
        }

        ShowFeedback("SETS STAGE COMPLETE", SetsFeedbackKind.Correct);
    }

    public void ShowGameOver()
    {
        if (placeButton != null) placeButton.gameObject.SetActive(false);
        SetPrompt(string.Empty);
        SetTimer(0f);
        ClearFeedback();
        if (helpButton != null) helpButton.gameObject.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }

    public void HideOverlays()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (stageCompletePanel != null) stageCompletePanel.SetActive(false);
        if (placeButton != null) placeButton.gameObject.SetActive(false);
        ClearFeedback();
        if (helpButton != null) helpButton.gameObject.SetActive(true);
    }

    private void SetPrompt(string message)
    {
        if (promptText != null) promptText.text = message;
        if (promptCard != null)
            promptCard.gameObject.SetActive(!string.IsNullOrEmpty(message) &&
                (showBookStartBanner || message != "INTERACT WITH BOOK STATUE"));
    }

    private void PlaceCurrentElement()
    {
        if (!helpOpen && stageManager != null) stageManager.PlaceCurrentElement();
    }

    private void RetryStage()
    {
        if (gameOverManager != null) gameOverManager.RetryChallenge();
        else if (stageManager != null) stageManager.ResetStageAfterGameOver();
    }

    private void ReturnToMenu()
    {
        if (stageCompleteManager != null) stageCompleteManager.ReturnToMenu();
        else UnityEngine.SceneManagement.SceneManager.LoadScene("Main Menu");
    }

    private void BuildHudIfNeeded()
    {
        if (hudRoot != null) return;

        // Sets content belongs inside this scene's linked WORLD_UI canvas safe area.
        GameObject sceneCanvas = GameObject.Find("Canvas 1") ?? GameObject.Find("Canvas");
        Transform parent = safeAreaRoot != null ? safeAreaRoot : sceneCanvas != null ? sceneCanvas.transform.Find("SafeArea") : null;
        if (parent == null && sceneCanvas != null) parent = sceneCanvas.transform;
        if (parent == null)
        {
            sceneCanvas = new GameObject("Sets Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            sceneCanvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = sceneCanvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            parent = sceneCanvas.transform;
        }

        headingFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/IMPACT SDF");
        hudRoot = new GameObject("Sets Stage HUD", typeof(RectTransform));
        hudRoot.transform.SetParent(parent, false);
        RectTransform hudRect = (RectTransform)hudRoot.transform;
        hudRect.anchorMin = Vector2.zero;
        hudRect.anchorMax = Vector2.one;
        hudRect.offsetMin = hudRect.offsetMax = Vector2.zero;

        GameObject board = CreatePanel(hudRoot.transform, "Sets Information", new Vector2(610f, 318f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(240f, -22f), Ink);
        SetPivot(board, new Vector2(0f, 1f));
        AddBorder(board, Gold, 2f);
        challengeText = CreateText(board.transform, "Challenge", new Vector2(570f, 48f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -22f), 33, TextAlignmentOptions.Left, heading: true);
        setAText = CreateText(board.transform, "Set A", new Vector2(555f, 41f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -77f), 28, TextAlignmentOptions.Left);
        setBText = CreateText(board.transform, "Set B", new Vector2(555f, 41f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -117f), 28, TextAlignmentOptions.Left);
        universalSetText = CreateText(board.transform, "Universal Set", new Vector2(555f, 41f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -157f), 28, TextAlignmentOptions.Left);
        instructionText = CreateText(board.transform, "Challenge Instruction", new Vector2(550f, 94f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -207f), 26, TextAlignmentOptions.TopLeft);
        board.SetActive(showChallengeInformation);

        GameObject timerCard = CreatePanel(hudRoot.transform, "Timer Card", new Vector2(255f, 100f), Vector2.one, Vector2.one, new Vector2(-30f, -24f), Ink);
        SetPivot(timerCard, Vector2.one);
        AddBorder(timerCard, Gold, 2f);
        timerText = CreateText(timerCard.transform, "Sets Timer", new Vector2(225f, 75f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, 55, TextAlignmentOptions.Center, heading: true);
        timerText.color = Gold;
        timerCard.SetActive(showTimerDisplay);

        GameObject prompt = CreatePanel(hudRoot.transform, "Prompt Card", new Vector2(690f, 80f), new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 134f), Ink);
        promptCard = (RectTransform)prompt.transform;
        AddBorder(prompt, Gold, 1f);
        promptText = CreateText(prompt.transform, "Interaction Prompt", new Vector2(650f, 65f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, 29, TextAlignmentOptions.Center, heading: true);
        prompt.SetActive(false);

        GameObject feedback = CreatePanel(hudRoot.transform, "Feedback", new Vector2(560f, 88f), new Vector2(0.5f, 0.58f), new Vector2(0.5f, 0.58f), Vector2.zero, neutralFeedback);
        feedbackBackground = feedback.GetComponent<Image>();
        AddBorder(feedback, Gold, 2f);
        feedbackText = CreateText(feedback.transform, "Feedback Text", new Vector2(520f, 72f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, 29, TextAlignmentOptions.Center, heading: true);
        feedbackText.gameObject.SetActive(false);

        GameObject placeObject = CreateButton(hudRoot.transform, "Place Button", "PLACE", new Vector2(205f, 90f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-530f, 345f), PlaceCurrentElement);
        placeButton = placeObject.GetComponent<Button>();
        placeObject.SetActive(false);

        gameOverPanel = CreateModal("Sets Game Over", "TIME IS UP", "RETRY", RetryStage);
        gameOverPanel.SetActive(false);
        stageCompletePanel = CreateModal("Sets Stage Complete", "SETS STAGE COMPLETE", "MAIN MENU", ReturnToMenu);
        stageCompletePanel.SetActive(false);

        BuildHelp();

        EnsureEventSystem();
    }

    private GameObject CreateModal(string name, string title, string actionLabel, UnityAction action)
    {
        GameObject dim = CreatePanel(hudRoot.transform, name, Vector2.zero, Vector2.zero, Vector2.one, Vector2.zero, new Color(0f, 0f, 0f, .68f));
        ((RectTransform)dim.transform).offsetMin = ((RectTransform)dim.transform).offsetMax = Vector2.zero;
        GameObject card = CreatePanel(dim.transform, "Dialog", new Vector2(710f, 365f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, Ink);
        AddBorder(card, Gold, 3f);
        CreateText(card.transform, "Title", new Vector2(650f, 130f), new Vector2(.5f, .67f), new Vector2(.5f, .67f), Vector2.zero, 52, TextAlignmentOptions.Center, heading: true).text = title;
        CreateButton(card.transform, "Action Button", actionLabel, new Vector2(265f, 86f), new Vector2(.5f, .27f), new Vector2(.5f, .27f), Vector2.zero, action);
        return dim;
    }

    private void BuildHelp()
    {
        helpButton = CreateButton(hudRoot.transform, "Sets Help Button", "HELP", new Vector2(160f, 110.5f),
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(234f, -25f), OpenHelp).GetComponent<Button>();
        // Gameplay_Interface fills the SafeArea, so these RectTransform values
        // also align the scene-only Help button with Canvas 1's Settings button.
        Transform settings = hudRoot.transform.parent.Find("Gameplay_Interface/SettingsButton");
        if (settings is RectTransform settingsRect)
        {
            RectTransform helpRect = (RectTransform)helpButton.transform;
            helpRect.anchorMin = settingsRect.anchorMin;
            helpRect.anchorMax = settingsRect.anchorMax;
            helpRect.pivot = settingsRect.pivot;
            helpRect.sizeDelta = settingsRect.sizeDelta;
            helpRect.anchoredPosition = settingsRect.anchoredPosition + new Vector2(settingsRect.rect.width + 24f, 0f);
        }
        helpOverlay = CreatePanel(hudRoot.transform, "Sets Help Overlay", Vector2.zero, Vector2.zero, Vector2.one, Vector2.zero, new Color(0f, 0f, 0f, .84f));
        ((RectTransform)helpOverlay.transform).offsetMin = ((RectTransform)helpOverlay.transform).offsetMax = Vector2.zero;
        GameObject card = CreatePanel(helpOverlay.transform, "Help Pages", new Vector2(1100f, 690f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, Ink);
        AddBorder(card, Gold, 3f);
        CreateText(card.transform, "Help Header", new Vector2(980f, 75f), new Vector2(.5f, .88f), new Vector2(.5f, .88f), Vector2.zero, 46, TextAlignmentOptions.Center, heading: true).text = "SETS • HOW TO PLAY";
        helpTitle = CreateText(card.transform, "Page Title", new Vector2(940f, 65f), new Vector2(.5f, .7f), new Vector2(.5f, .7f), Vector2.zero, 38, TextAlignmentOptions.Center, heading: true);
        helpTitle.color = Gold;
        helpBody = CreateText(card.transform, "Page Content", new Vector2(900f, 250f), new Vector2(.5f, .46f), new Vector2(.5f, .46f), Vector2.zero, 28, TextAlignmentOptions.TopLeft);
        helpPage = CreateText(card.transform, "Page Number", new Vector2(200f, 48f), new Vector2(.5f, .20f), new Vector2(.5f, .20f), Vector2.zero, 29, TextAlignmentOptions.Center, heading: true);
        helpBackButton = CreateButton(card.transform, "Back Button", "BACK", new Vector2(205f, 70f), new Vector2(.19f, .20f), new Vector2(.19f, .20f), Vector2.zero, PreviousHelpPage).GetComponent<Button>();
        helpNextButton = CreateButton(card.transform, "Next Button", "NEXT", new Vector2(205f, 70f), new Vector2(.81f, .20f), new Vector2(.81f, .20f), Vector2.zero, NextHelpPage).GetComponent<Button>();
        CreateButton(card.transform, "Got It Button", "GOT IT", new Vector2(240f, 70f), new Vector2(.5f, .08f), new Vector2(.5f, .08f), Vector2.zero, CloseHelp);
        helpOverlay.SetActive(false);
    }

    private void OpenHelp()
    {
        if (helpOpen || helpOverlay == null || Time.timeScale <= 0f ||
            (stageManager != null && (stageManager.State == SetsStageState.GAME_OVER || stageManager.State == SetsStageState.STAGE_COMPLETE))) return;
        pausedInput = GameInput.Instance != null ? GameInput.Instance : FindFirstObjectByType<GameInput>();
        pausedPlayer = Player.LocalInstance != null ? Player.LocalInstance : FindFirstObjectByType<Player>();
        pausedCamera = FindFirstObjectByType<ThirdPersonCameraController>();
        pausedPinch = FindFirstObjectByType<CinemachinePinchZoom>();
        previousInputBlocked = pausedInput != null && pausedInput.GameplayInputBlocked;
        previousPlayerEnabled = pausedPlayer != null && pausedPlayer.enabled;
        previousCameraEnabled = pausedCamera != null && pausedCamera.enabled;
        previousPinchEnabled = pausedPinch != null && pausedPinch.enabled;
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        previousTimeScale = Time.timeScale;
        pausedInput?.SetGameplayInputBlocked(true);
        if (pausedPlayer != null) pausedPlayer.enabled = false;
        if (pausedCamera != null) pausedCamera.enabled = false;
        if (pausedPinch != null) pausedPinch.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
        helpOpen = true;
        helpPageIndex = 0;
        helpOverlay.SetActive(true);
        helpOverlay.transform.SetAsLastSibling();
        RefreshHelpPage();
    }

    private void CloseHelp()
    {
        if (!helpOpen) return;
        helpOpen = false;
        helpOverlay.SetActive(false);
        if (pausedInput != null) pausedInput.SetGameplayInputBlocked(previousInputBlocked);
        if (pausedPlayer != null) pausedPlayer.enabled = previousPlayerEnabled;
        if (pausedCamera != null) pausedCamera.enabled = previousCameraEnabled;
        if (pausedPinch != null) pausedPinch.enabled = previousPinchEnabled;
        Time.timeScale = previousTimeScale;
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
    }

    private void OnDisable() { CloseHelp(); }
    private void PreviousHelpPage() { if (helpPageIndex > 0) { helpPageIndex--; RefreshHelpPage(); } }
    private void NextHelpPage() { if (helpPageIndex < HelpPages.Length - 1) { helpPageIndex++; RefreshHelpPage(); } }

    private void RefreshHelpPage()
    {
        helpTitle.text = HelpPages[helpPageIndex].title;
        helpBody.text = HelpPages[helpPageIndex].body;
        helpPage.text = $"{helpPageIndex + 1} / {HelpPages.Length}";
        helpBackButton.interactable = helpPageIndex > 0;
        helpNextButton.interactable = helpPageIndex < HelpPages.Length - 1;
    }

    private static void SetPivot(GameObject target, Vector2 pivot) { ((RectTransform)target.transform).pivot = pivot; }

    private static void AddBorder(GameObject target, Color color, float distance)
    {
        Outline border = target.AddComponent<Outline>();
        border.effectColor = color;
        border.effectDistance = new Vector2(distance, -distance);
        border.useGraphicAlpha = true;
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, float fontSize, TextAlignmentOptions alignment, bool heading = false)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = anchorMin == new Vector2(0f, 1f) && anchorMax == anchorMin
            ? new Vector2(0f, 1f) : new Vector2(.5f, .5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (heading && headingFont != null) text.font = headingFont;
        else if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = alignment;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject CreatePanel(Transform parent, string name, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        panel.GetComponent<Image>().color = color;
        return panel;
    }

    private GameObject CreateButton(Transform parent, string name, string label, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, UnityAction action)
    {
        GameObject buttonObject = CreatePanel(parent, name, size, anchorMin, anchorMax, anchoredPosition, Ink);
        AddBorder(buttonObject, Gold, 2f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(.78f, 1f, 1f);
        colors.pressedColor = new Color(.58f, .84f, .85f);
        button.colors = colors;
        button.onClick.AddListener(action);
        TextMeshProUGUI labelText = CreateText(buttonObject.transform, "Label", size - new Vector2(16f, 12f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, 30, TextAlignmentOptions.Center, heading: true);
        labelText.text = label;
        return buttonObject;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }
}

public enum SetsFeedbackKind
{
    Neutral,
    Correct,
    Wrong
}
