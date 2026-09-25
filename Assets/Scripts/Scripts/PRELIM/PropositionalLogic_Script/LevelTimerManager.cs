using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class LevelTimerManager : MonoBehaviour
{
    [Header("UI Display")]
    [SerializeField] private TextMeshProUGUI timerTextUI;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Button startOverButton;
    [FormerlySerializedAs("retryButton")]
    [SerializeField] private Button middleActionButton;
    [FormerlySerializedAs("quitButton")]
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Text middleActionLabel;

    [SerializeField] private GameObject safeAreaPanel;
    [Tooltip("Drag your Quiz Panel here")]
    [SerializeField] private GameObject quizPanel;

    [Header("Global Timer Configuration")]
    [Tooltip("Total time shared by all five Propositional Logic challenges.")]
    [SerializeField] private float initialLevelDuration = 540f;
    [Tooltip("Time granted after retrying a timeout.")]
    [SerializeField] private float retryLevelDuration = 270f;

    [Header("Game Over Navigation")]
    [SerializeField] private string mainMenuSceneName = "Main Menu";
    [SerializeField] private string logicGardenSceneName = "LogicGarden";

    [SerializeField] private float currentTimer;
    private bool isTimerRunning = false;

    // Static variables persist automatically when reloading the scene
    public static int savedTopicIndex = 0;
    public static bool isRespawningFromFail = false;
    private static int tryAgainCount = 0;
    
    // Persistent global timer value tracking remaining seconds across scene reloads
    public static float savedRemainingTime = -1f;

    public bool IsTimerRunning => isTimerRunning;
    public float RemainingTime => currentTimer;
    public static int TryAgainCount => tryAgainCount;
    public bool IsStudyOptionAvailable => tryAgainCount >= 2;

    private void Start()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        UpdateGameOverOptions();
        
        // Ensure the timer stays completely paused on clean fresh load
        isTimerRunning = false;

        // Display the correct remaining time numbers immediately so the HUD isn't blank
        InitializeTimeDisplay();
    }

    private void InitializeTimeDisplay()
    {
        if (isRespawningFromFail && savedRemainingTime > 0)
        {
            currentTimer = savedRemainingTime;
        }
        else if (savedRemainingTime > 0)
        {
            currentTimer = savedRemainingTime;
        }
        else
        {
            currentTimer = initialLevelDuration;
        }
        UpdateTimerUI();
    }

    public void StartLevelTimer()
    {
        if (isRespawningFromFail && savedRemainingTime > 0)
        {
            currentTimer = savedRemainingTime;
        }
        else if (savedRemainingTime > 0)
        {
            currentTimer = savedRemainingTime;
        }
        else
        {
            currentTimer = initialLevelDuration;
            savedRemainingTime = initialLevelDuration;
        }

        isTimerRunning = true;
        UpdateTimerUI();
    }

    public void ResetForFreshRun()
    {
        ResetSession();
        currentTimer = initialLevelDuration;
        savedRemainingTime = initialLevelDuration;
        isTimerRunning = false;
        UpdateTimerUI();
    }

    private void Update()
    {
        if (isTimerRunning)
        {
            currentTimer -= Time.deltaTime;
            savedRemainingTime = currentTimer; // Keep static backup updated constantly
            UpdateTimerUI();

            if (currentTimer <= 0)
            {
                currentTimer = 0;
                savedRemainingTime = 0;
                TriggerGameOver();
            }
        }
    }

    public void SetTimerVisibility(bool isVisible)
    {
        if (timerTextUI != null)
        {
            timerTextUI.gameObject.SetActive(isVisible);
        }
    }

    // private void OnValidate()
    // {
    //     if (!isTimerRunning)
    //     {
    //         currentTimer = initialLevelDuration;
    //         UpdateTimerUI(); // Call your existing UI update function here
    //     }
    // }

    private void UpdateTimerUI()
    {
        if (timerTextUI == null) return;

        int minutes = Mathf.FloorToInt(currentTimer / 60f);
        int seconds = Mathf.FloorToInt(currentTimer % 60f);
        timerTextUI.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    public void DeductTime(float penaltyTime)
    {
        if (!isTimerRunning) return;
        currentTimer -= penaltyTime;
        if (currentTimer < 0) currentTimer = 0;
        savedRemainingTime = currentTimer;
        UpdateTimerUI();

        if (currentTimer <= 0f)
        {
            TriggerGameOver();
        }
    }

    public void StopTimer()
    {
        isTimerRunning = false;
    }

    private void TriggerGameOver()
    {
        isTimerRunning = false;

        // 1. Force close ongoing game interfaces
        if (timerTextUI != null) timerTextUI.gameObject.SetActive(false);
        if (quizPanel != null) quizPanel.SetActive(false);
        if (safeAreaPanel != null) safeAreaPanel.SetActive(false);

        BookInteract[] activeBooks = Object.FindObjectsByType<BookInteract>(FindObjectsSortMode.None);
        foreach (BookInteract book in activeBooks)
        {
            if (book != null) book.ForceHideButton();
        }

        // 2. Show Game Over panel options
        UpdateGameOverOptions();
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        
        Time.timeScale = 0f; 
    }

    public void OnStartOverClicked()
    {
        Time.timeScale = 1f;
        PrepareStartOverState();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void OnMiddleActionClicked()
    {
        Time.timeScale = 1f;

        if (!TryPrepareTryAgainState())
        {
            ResetSession();
            SceneManager.LoadScene(logicGardenSceneName);
            return;
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void PrepareStartOverState()
    {
        ResetSession();
        currentTimer = initialLevelDuration;
        savedRemainingTime = initialLevelDuration;
        isTimerRunning = false;
        UpdateTimerUI();
    }

    public bool TryPrepareTryAgainState()
    {
        if (tryAgainCount >= 2) return false;

        tryAgainCount++;
        PrepareRetryState();
        return true;
    }

    public void PrepareRetryState()
    {
        isRespawningFromFail = true;
        savedTopicIndex = 0;
        savedRemainingTime = retryLevelDuration;
        currentTimer = retryLevelDuration;
        isTimerRunning = false;
        UpdateTimerUI();
    }

    public void OnMainMenuClicked()
    {
        Debug.Log("Returning to Main Menu...");
        Time.timeScale = 1f; 
        ResetSession();
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void UpdateGameOverOptions()
    {
        if (startOverButton != null)
        {
            startOverButton.gameObject.SetActive(true);
            startOverButton.interactable = true;
        }

        if (middleActionButton != null)
        {
            middleActionButton.gameObject.SetActive(true);
            middleActionButton.interactable = true;
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.gameObject.SetActive(true);
            mainMenuButton.interactable = true;
        }

        if (middleActionLabel == null && middleActionButton != null)
        {
            middleActionLabel = middleActionButton.GetComponentInChildren<Text>(true);
        }

        if (middleActionLabel != null)
        {
            middleActionLabel.text = IsStudyOptionAvailable
                ? "STUDY IN LOGIC GARDEN LIBRARY"
                : "TRY AGAIN";
            middleActionLabel.resizeTextForBestFit = IsStudyOptionAvailable;
            middleActionLabel.resizeTextMinSize = 8;
            middleActionLabel.resizeTextMaxSize = 15;
        }
    }

    // Add this directly inside LevelTimerManager
    public static void ResetSession()
    {
        savedTopicIndex = 0;
        isRespawningFromFail = false;
        savedRemainingTime = -1f;
        tryAgainCount = 0;
    }
}
