using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>The Truth_Table stage clock. The existing door controller owns the start interaction.</summary>
public sealed class TruthTableStageClock : MonoBehaviour
{
    private enum StageState { WaitingForBook, Running, GameOver, Completed }

    private const float StartSeconds = 18f * 60f;
    private StageState state = StageState.WaitingForBook;
    private float remainingSeconds = StartSeconds;
    private bool quizOpen;
    private bool playerAtBoard;
    private bool tutorialOpen;
    private TruthTableDoorInteraction doors;
    private RectTransform hudRoot;
    private RectTransform hudHost;
    private RectTransform minimapPanel;
    private TextMeshProUGUI timerLabel;
    private TextMeshProUGUI adjustmentLabel;
    private TextMeshProUGUI challengeLabel;
    private TextMeshProUGUI columnLabel;
    private TextMeshProUGUI startLabel;
    private PrelimChallengeCompletionNotice completionNotice;
    private Coroutine startMessageRoutine;
    private int currentChallengeNumber = 1;
    private GameObject gameOverPanel;
    private TextMeshProUGUI resultLabel;
    private UnityEngine.UI.Button restartButton;
    private Coroutine adjustmentRoutine;

    public bool IsWaitingForBook => state == StageState.WaitingForBook;
    public bool IsRunning => state == StageState.Running;
    public bool IsGameOver => state == StageState.GameOver;
    public bool IsCompleted => state == StageState.Completed;
    public bool IsTutorialOpen => tutorialOpen;
    public bool IsPlayerAtBoard => playerAtBoard;
    public bool IsCountdownPaused => state != StageState.Running || quizOpen || playerAtBoard || tutorialOpen || Time.timeScale <= 0f;
    public float RemainingSeconds => remainingSeconds;
    public int CurrentChallengeNumber => currentChallengeNumber;
    public string ProgressSummary { get; private set; } = "Easy — Column 1 of 3";
    public bool TimeAdjustmentVisible => adjustmentLabel != null && adjustmentLabel.gameObject.activeSelf;
    public string TimeAdjustmentText => adjustmentLabel != null ? adjustmentLabel.text : string.Empty;
    public static float LastCompletedRemainingSeconds { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCompletedTime() { LastCompletedRemainingSeconds = 0f; }

    private void Awake()
    {
        doors = GetComponent<TruthTableDoorInteraction>();
        if (doors == null || doors.UiHost == null)
        {
            Debug.LogError("Truth_Table clock needs the existing door UI Canvas.", this);
            enabled = false;
            return;
        }
        minimapPanel = doors.MinimapPanel;
        BuildUi(doors.UiHost, doors.UiTextStyle);
        completionNotice = PrelimChallengeCompletionNotice.GetOrCreate(doors.UiHost, doors.UiTextStyle);
        UpdateHudLayout();
        RefreshTimer();
    }

    private void OnEnable()
    {
        if (hudRoot != null)
            hudRoot.gameObject.SetActive(StageSelectionState.SelectedStage == 2);
    }

    private void LateUpdate()
    {
        if (hudRoot == null) return;
        hudRoot.gameObject.SetActive(StageSelectionState.SelectedStage == 2 && StageJourneyUI.Instance == null);
        if (hudRoot.gameObject.activeSelf) UpdateHudLayout();
    }

    private void Update()
    {
        if (IsCountdownPaused || StageSelectionState.SelectedStage != 2) return;
        remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime);
        RefreshTimer();
        if (remainingSeconds <= 0f) TriggerGameOver();
    }

    public bool StartFromBook()
    {
        if (state != StageState.WaitingForBook || StageSelectionState.SelectedStage != 2 || tutorialOpen) return false;
        state = StageState.Running;
        completionNotice?.Hide();
        RefreshTimer();
        ShowStartMessage();
        return true;
    }

    public void SetQuizOpen(bool open) { quizOpen = open; }

    public void SetPlayerAtBoard(bool atBoard) { playerAtBoard = atBoard; }

    public void SetTutorialOpen(bool open) { tutorialOpen = open; }

    public void PrepareNextChallenge()
    {
        if (state != StageState.Running) return;
        state = StageState.WaitingForBook;
        remainingSeconds = StartSeconds;
        completionNotice?.ShowWaiting(currentChallengeNumber - 1, currentChallengeNumber);
        quizOpen = false;
        HideStartMessage();
        if (adjustmentRoutine != null) StopCoroutine(adjustmentRoutine);
        adjustmentRoutine = null;
        adjustmentLabel.gameObject.SetActive(false);
        RefreshTimer();
    }

    public void SetChallengeProgress(int challenge, string difficulty, int column, string expression)
    {
        if (challengeLabel == null || columnLabel == null) return;
        currentChallengeNumber = challenge;
        ProgressSummary = difficulty + " — Column " + column + " of 3";
        challengeLabel.text = string.Format("Challenge {0} — {1}", challenge, difficulty);
        columnLabel.text = string.Format("Column {0} of 3", column);
    }

    public void AdjustSeconds(int amount)
    {
        if (state != StageState.Running) return;
        remainingSeconds = Mathf.Max(0f, remainingSeconds + amount);
        RefreshTimer();
        ShowAdjustment(amount);
        if (remainingSeconds <= 0f) TriggerGameOver();
    }

    public void CompleteStage()
    {
        if (state != StageState.Running) return;
        state = StageState.Completed;
        HideStartMessage();
        completionNotice?.Hide();
        LastCompletedRemainingSeconds = remainingSeconds;
        if (adjustmentRoutine != null) StopCoroutine(adjustmentRoutine);
        adjustmentLabel.gameObject.SetActive(false);
        resultLabel.text = "All challenges completed!\nTime Remaining: " + FormatTime(remainingSeconds);
        restartButton.gameObject.SetActive(false);
        if (StageJourneyUI.Instance != null)
            StageJourneyUI.Instance.ShowCompletion(2, "Easy, Medium and Hard mastered!\nTime remaining: " + FormatTime(remainingSeconds));
        else
        {
            gameOverPanel.SetActive(true);
            gameOverPanel.transform.SetAsLastSibling();
        }
    }

    private void TriggerGameOver()
    {
        if (state != StageState.Running) return;
        remainingSeconds = 0f;
        state = StageState.GameOver;
        HideStartMessage();
        completionNotice?.Hide();
        RefreshTimer();
        if (adjustmentRoutine != null) StopCoroutine(adjustmentRoutine);
        adjustmentLabel.gameObject.SetActive(false);
        doors.EndForGameOver();
        if (StageJourneyUI.Instance != null)
            StageJourneyUI.Instance.ShowFailure(2, "The truth-table countdown reached 00:00.\nRestart at Easy and begin again at the book.", RestartStage);
        else
        {
            gameOverPanel.SetActive(true);
            gameOverPanel.transform.SetAsLastSibling();
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RestartStage()
    {
        if (state != StageState.GameOver) return;
        LastCompletedRemainingSeconds = 0f;
        TruthTableDoorInteraction.ClearSessionForRestart();
        GameInput input = FindFirstObjectByType<GameInput>();
        if (input != null) input.SetGameplayInputBlocked(false);
        Player player = Player.LocalInstance;
        if (player != null) player.ToggleControl(true);
        StageSelectionState.Select(2);
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void ShowAdjustment(int amount)
    {
        if (adjustmentRoutine != null) StopCoroutine(adjustmentRoutine);
        adjustmentLabel.text = (amount > 0 ? "+" : "") + amount + "s";
        adjustmentLabel.color = amount > 0 ? new Color(0.25f, 0.95f, 0.35f) :
                                            new Color(1f, 0.27f, 0.27f);
        adjustmentLabel.gameObject.SetActive(true);
        adjustmentRoutine = StartCoroutine(HideAdjustment());
    }

    private IEnumerator HideAdjustment()
    {
        yield return new WaitForSecondsRealtime(1.8f);
        adjustmentLabel.gameObject.SetActive(false);
        adjustmentRoutine = null;
    }

    private void RefreshTimer()
    {
        if (timerLabel != null) timerLabel.text = FormatTime(remainingSeconds);
    }

    private void ShowStartMessage()
    {
        if (startLabel == null) return;
        HideStartMessage();
        startLabel.text = "Start!!";
        startLabel.gameObject.SetActive(true);
        startMessageRoutine = StartCoroutine(HideStartMessageAfterDelay());
    }

    private IEnumerator HideStartMessageAfterDelay()
    {
        yield return new WaitForSecondsRealtime(1.5f);
        startMessageRoutine = null;
        if (startLabel != null) startLabel.gameObject.SetActive(false);
    }

    private void HideStartMessage()
    {
        if (startMessageRoutine != null) StopCoroutine(startMessageRoutine);
        startMessageRoutine = null;
        if (startLabel != null) startLabel.gameObject.SetActive(false);
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        return string.Format("{0:00}:{1:00}", total / 60, total % 60);
    }

    private void BuildUi(RectTransform host, TMP_Text style)
    {
        hudHost = host;
        var hud = new GameObject("TruthTableHud", typeof(RectTransform),
            typeof(UnityEngine.UI.VerticalLayoutGroup));
        hudRoot = (RectTransform)hud.transform;
        hudRoot.SetParent(host, false);
        hudRoot.anchorMin = Vector2.one;
        hudRoot.anchorMax = Vector2.one;
        hudRoot.pivot = Vector2.one;
        var layout = hud.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        timerLabel = CreateHudText("TruthTableCountdown", style, 38f, 43f);
        timerLabel.text = "18:00";
        challengeLabel = CreateHudText("TruthTableChallengeProgress", style, 27f, 34f);
        challengeLabel.enableAutoSizing = true;
        challengeLabel.fontSizeMin = 17f;
        challengeLabel.fontSizeMax = 27f;
        challengeLabel.text = "Challenge 1 — Easy";
        columnLabel = CreateHudText("TruthTableColumnProgress", style, 25f, 31f);
        columnLabel.text = "Column 1 of 3";
        adjustmentLabel = CreateHudText("TruthTableTimeAdjustment", style, 27f, 30f);
        adjustmentLabel.gameObject.SetActive(false);
        hudRoot.gameObject.SetActive(StageSelectionState.SelectedStage == 2);

        startLabel = CreateText("TruthTableStartMessage", host, style, 86f);
        Place(startLabel.rectTransform, new Vector2(0.5f, 0.5f),
            new Vector2(0f, 145f), new Vector2(620f, 120f));
        startLabel.color = new Color(1f, 0.9f, 0.55f);
        startLabel.outlineColor = new Color32(0, 0, 0, 240);
        startLabel.outlineWidth = 0.2f;
        startLabel.gameObject.SetActive(false);

        gameOverPanel = new GameObject("TruthTableGameOver", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        RectTransform panelRect = (RectTransform)gameOverPanel.transform;
        panelRect.SetParent(host, false);
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        gameOverPanel.GetComponent<UnityEngine.UI.Image>().color = new Color(0.02f, 0.07f, 0.07f, 0.9f);

        resultLabel = CreateText("GameOverMessage", panelRect, style, 50f);
        Place(resultLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(650f, 160f));
        resultLabel.text = "Game Over\n00:00\nTime ran out";

        var buttonObject = new GameObject("RestartTruthTableButton", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        RectTransform buttonRect = (RectTransform)buttonObject.transform;
        buttonRect.SetParent(panelRect, false);
        Place(buttonRect, new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(300f, 80f));
        var buttonImage = buttonObject.GetComponent<UnityEngine.UI.Image>();
        buttonImage.color = new Color(0.08f, 0.28f, 0.22f, 1f);
        restartButton = buttonObject.GetComponent<UnityEngine.UI.Button>();
        restartButton.targetGraphic = buttonImage;
        restartButton.onClick.AddListener(RestartStage);
        TextMeshProUGUI buttonLabel = CreateText("Label", buttonRect, style, 32f);
        buttonLabel.text = "Restart";
        buttonLabel.raycastTarget = false;
        buttonLabel.rectTransform.anchorMin = Vector2.zero;
        buttonLabel.rectTransform.anchorMax = Vector2.one;
        buttonLabel.rectTransform.offsetMin = Vector2.zero;
        buttonLabel.rectTransform.offsetMax = Vector2.zero;
        gameOverPanel.SetActive(false);
    }

    private TextMeshProUGUI CreateHudText(string name, TMP_Text style, float size, float height)
    {
        TextMeshProUGUI label = CreateText(name, hudRoot, style, size);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.outlineColor = new Color32(0, 0, 0, 230);
        label.outlineWidth = 0.16f;
        var sizing = label.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        sizing.preferredHeight = height;
        return label;
    }

    private void UpdateHudLayout()
    {
        if (hudHost == null || minimapPanel == null || minimapPanel.parent != hudHost) return;
        float width = Mathf.Min(Mathf.Max(220f, minimapPanel.rect.width), hudHost.rect.width - 16f);
        Vector2 position = minimapPanel.anchoredPosition;
        position.y -= minimapPanel.rect.height + 12f;
        if (hudRoot.anchoredPosition != position) hudRoot.anchoredPosition = position;
        if (hudRoot.sizeDelta != new Vector2(width, 150f))
            hudRoot.sizeDelta = new Vector2(width, 150f);
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, TMP_Text style, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<TextMeshProUGUI>();
        if (style != null) label.font = style.font;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 offset, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = offset;
        rect.sizeDelta = size;
    }

    private void OnDisable()
    {
        HideStartMessage();
        if (hudRoot != null) hudRoot.gameObject.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (restartButton != null) restartButton.onClick.RemoveListener(RestartStage);
        if (startLabel != null) Destroy(startLabel.gameObject);
        if (hudRoot != null) Destroy(hudRoot.gameObject);
        if (gameOverPanel != null) Destroy(gameOverPanel);
    }
}
