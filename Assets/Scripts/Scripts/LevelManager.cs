using UnityEngine;
using TMPro;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance;

    [Header("Game State")]
    public int playerHearts = 3;
    public float timeRemaining = 180f;
    private bool isTimerRunning = false;

    [Header("UI References")]
    [Tooltip("Drag your Timer Text here")]
    public TextMeshProUGUI timerText; 
    
    // ---> NEW: Array to hold your heart icons <---
    [Tooltip("Drag your 3 Heart GameObjects here from the Canvas")]
    public GameObject[] heartIcons; 

    [Header("Reset References")]
    public Transform player;
    public Transform ovalRespawnPoint;
    public LeverController leverController;
    public GateController andGateController;
    public ResettableObject[] boulders;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Update()
    {
        if (isTimerRunning)
        {
            timeRemaining -= Time.deltaTime;
            UpdateTimerUI();

            if (timeRemaining <= 0)
            {
                HandleTimeout();
            }
        }
    }

    public void StartTimer() 
    { 
        isTimerRunning = true; 
    }

    public void StopTimer() 
    { 
        isTimerRunning = false; 
    }

    private void HandleTimeout()
    {
        isTimerRunning = false;
        playerHearts--;
        
        // ---> NEW: Update the hearts on screen! <---
        UpdateHeartsUI(); 

        if (playerHearts <= 0)
        {
            Debug.Log("Game Over! No hearts left.");
            // We will add Game Over logic here later
        }
        else
        {
            Debug.Log("Time's up! Lost 1 heart. Respawning...");
            timeRemaining = 180f; 
            RespawnPlayer();
            ResetPuzzles();
            StartTimer(); 
        }
    }

    // ---> NEW: Method to turn off the heart icons <---
    private void UpdateHeartsUI()
    {
        // Loop through all the hearts in the array
        for (int i = 0; i < heartIcons.Length; i++)
        {
            if (heartIcons[i] != null)
            {
                // If the index is less than our current hearts, turn it on. Otherwise, turn it off.
                // Example: If playerHearts is 2, index 0 and 1 stay ON, index 2 turns OFF.
                heartIcons[i].SetActive(i < playerHearts);
            }
        }
    }

    private void RespawnPlayer()
    {
        player.position = ovalRespawnPoint.position;
    }

    private void ResetPuzzles()
    {
        if (leverController != null) leverController.ResetLever();
        if (andGateController != null) andGateController.ResetGate();
        
        foreach (var boulder in boulders)
        {
            if (boulder != null) boulder.ResetPosition();
        }
    }

    private void UpdateTimerUI()
    {
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(timeRemaining / 60);
            int seconds = Mathf.FloorToInt(timeRemaining % 60);
            timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }
}