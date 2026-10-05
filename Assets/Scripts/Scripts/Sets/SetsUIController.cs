using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;

[DisallowMultipleComponent]
public sealed class SetsUIController : MonoBehaviour
{
    [Header("Optional Scene UI Overrides")]
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

    private SetsStageManager stageManager;
    private GameOverManager gameOverManager;
    private StageCompleteManager stageCompleteManager;
    private GameObject hudRoot;
    private Image feedbackBackground;
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
        if (promptText != null)
            promptText.text = waitingForActivation ? "INTERACT WITH BOOK STATUE" : string.Empty;
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
        if (promptText != null) promptText.text = canPickUp ? "[PICK UP]  Use INTERACT" : string.Empty;
        if (placeButton != null) placeButton.gameObject.SetActive(false);
    }

    public void ShowPlacementPrompt(bool canPlace, SetZone? zone)
    {
        if (promptText != null)
        {
            if (!canPlace) promptText.text = "Carry the element into a Venn region.";
            else promptText.text = "[PLACE]  " + SetsStageManager.FormatZone(zone.Value);
        }

        if (placeButton != null) placeButton.gameObject.SetActive(canPlace);
    }

    public void ShowFeedback(string message, SetsFeedbackKind kind)
    {
        BuildHudIfNeeded();
        if (feedbackText == null) return;
        feedbackText.text = message;
        feedbackText.gameObject.SetActive(!string.IsNullOrEmpty(message));

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
        if (promptText != null) promptText.text = string.Empty;
        ShowFeedback($"CHALLENGE {challengeNumber} COMPLETE", SetsFeedbackKind.Correct);
    }

    public void ShowStageComplete()
    {
        if (placeButton != null) placeButton.gameObject.SetActive(false);
        if (promptText != null) promptText.text = string.Empty;
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
        if (promptText != null) promptText.text = string.Empty;
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        ShowFeedback("TIME IS UP", SetsFeedbackKind.Wrong);
    }

    public void HideOverlays()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (stageCompletePanel != null) stageCompletePanel.SetActive(false);
        if (placeButton != null) placeButton.gameObject.SetActive(false);
        ClearFeedback();
    }

    private void PlaceCurrentElement()
    {
        if (stageManager != null) stageManager.PlaceCurrentElement();
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

        hudRoot = new GameObject("Sets Stage HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = hudRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        CanvasScaler scaler = hudRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        TextMeshProUGUI board = CreateText(hudRoot.transform, "Set Information Board", new Vector2(650f, 210f), new Vector2(0.34f, 0.9f), new Vector2(0.34f, 0.9f), new Vector2(-620f, -130f), 30, TextAlignmentOptions.TopLeft);
        challengeText = CreateText(board.transform, "Challenge", new Vector2(620f, 42f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), 31, TextAlignmentOptions.Left);
        setAText = CreateText(board.transform, "Set A", new Vector2(620f, 40f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -50f), 27, TextAlignmentOptions.Left);
        setBText = CreateText(board.transform, "Set B", new Vector2(620f, 40f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -92f), 27, TextAlignmentOptions.Left);
        universalSetText = CreateText(board.transform, "Universal Set", new Vector2(620f, 40f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -134f), 27, TextAlignmentOptions.Left);

        instructionText = CreateText(hudRoot.transform, "Challenge Instruction", new Vector2(780f, 95f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -115f), 29, TextAlignmentOptions.Center);
        promptText = CreateText(hudRoot.transform, "Interaction Prompt", new Vector2(640f, 70f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 80f), 28, TextAlignmentOptions.Center);
        timerText = CreateText(hudRoot.transform, "Sets Timer", new Vector2(220f, 64f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-150f, -42f), 34, TextAlignmentOptions.Center);

        GameObject feedback = CreatePanel(hudRoot.transform, "Feedback", new Vector2(520f, 78f), new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.78f), new Vector2(0f, 0f), neutralFeedback);
        feedbackBackground = feedback.GetComponent<Image>();
        feedbackText = CreateText(feedback.transform, "Feedback Text", new Vector2(500f, 70f), Vector2.zero, Vector2.one, Vector2.zero, 28, TextAlignmentOptions.Center);
        feedbackText.gameObject.SetActive(false);

        GameObject placeObject = CreateButton(hudRoot.transform, "Place Button", "PLACE", new Vector2(190f, 100f), new Vector2(0.86f, 0.4f), new Vector2(0.86f, 0.4f), Vector2.zero, PlaceCurrentElement);
        placeButton = placeObject.GetComponent<Button>();
        placeObject.SetActive(false);

        GameObject over = CreatePanel(hudRoot.transform, "Sets Game Over", new Vector2(660f, 350f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Color(0.1f, 0.045f, 0.05f, 0.97f));
        CreateText(over.transform, "Game Over Message", new Vector2(600f, 150f), new Vector2(0.5f, 0.68f), new Vector2(0.5f, 0.68f), Vector2.zero, 52, TextAlignmentOptions.Center).text = "TIME IS UP";
        CreateButton(over.transform, "Retry Button", "RETRY", new Vector2(210f, 80f), new Vector2(0.5f, 0.25f), new Vector2(0.5f, 0.25f), Vector2.zero, RetryStage);
        gameOverPanel = over;
        gameOverPanel.SetActive(false);

        GameObject complete = CreatePanel(hudRoot.transform, "Sets Stage Complete", new Vector2(700f, 360f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Color(0.04f, 0.18f, 0.12f, 0.97f));
        CreateText(complete.transform, "Completion Message", new Vector2(650f, 150f), new Vector2(0.5f, 0.68f), new Vector2(0.5f, 0.68f), Vector2.zero, 48, TextAlignmentOptions.Center).text = "SETS STAGE COMPLETE";
        CreateButton(complete.transform, "Menu Button", "MAIN MENU", new Vector2(240f, 80f), new Vector2(0.5f, 0.25f), new Vector2(0.5f, 0.25f), Vector2.zero, ReturnToMenu);
        stageCompletePanel = complete;
        stageCompletePanel.SetActive(false);

        EnsureEventSystem();
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = alignment;
        text.enableWordWrapping = true;
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

    private static GameObject CreateButton(Transform parent, string name, string label, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = CreatePanel(parent, name, size, anchorMin, anchorMax, anchoredPosition, new Color(0.08f, 0.42f, 0.55f, 0.97f));
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();
        button.onClick.AddListener(action);
        TextMeshProUGUI labelText = CreateText(buttonObject.transform, "Label", size, Vector2.zero, Vector2.one, Vector2.zero, 28, TextAlignmentOptions.Center);
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
