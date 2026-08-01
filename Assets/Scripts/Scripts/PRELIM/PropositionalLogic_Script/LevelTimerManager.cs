using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LevelTimerManager : MonoBehaviour
{
    [Header("UI Display")]
    [SerializeField] private TextMeshProUGUI timerTextUI;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button quitButton;

    [SerializeField] private GameObject safeAreaPanel;
    [Tooltip("Drag your Quiz Panel here")]
    [SerializeField] private GameObject quizPanel;

    [Header("Global Timer Configuration")]
    [Tooltip("Total level time in seconds (e.g., 300 for 5 minutes). Only used on first entry.")]
    [SerializeField] private float initialLevelDuration = 300f;

    [SerializeField] private float currentTimer;
    private bool isTimerRunning = false;

    // Static variables persist automatically when reloading the scene
    public static int savedTopicIndex = 0;
    public static bool isRespawningFromFail = false;
    
    // Persistent global timer value tracking remaining seconds across scene reloads
    public static float savedRemainingTime = -1f;

    public bool IsTimerRunning => isTimerRunning;

    private void Start()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        
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
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        
        Time.timeScale = 0f; 
    }

    public void OnRetryClicked()
    {
        Time.timeScale = 1f;

        // Flag the static system that we are recovering from a timeout fail
        isRespawningFromFail = true;
        
        // Give them a refreshed 60-second baseline amount of time to clear this specific challenge room
        savedRemainingTime = 150; 

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void OnQuitClicked()
    {
        Debug.Log("Returning to Main Menu...");
        Time.timeScale = 1f; 
        
        // Clear all session static values for clean initializations later
        savedTopicIndex = 0;
        isRespawningFromFail = false;
        savedRemainingTime = -1f;

        SceneManager.LoadScene("Main Menu");
    }
}