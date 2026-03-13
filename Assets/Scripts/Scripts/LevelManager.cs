using UnityEngine;
using TMPro;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance;
    [Header("Challenge Environments")]
    public GameObject challenge1Area;
    public GameObject challenge2Area;
    public GameObject challenge3Area;

    [Header("Global Game State")]
    public int playerHearts = 3;
    
    [Header("Challenge 1: Timer")]
    public float timeRemaining = 180f;
    private bool isTimerRunning = false;
    [Tooltip("Drag your Timer Text here")]
    public TextMeshProUGUI timerText; 

    [Header("UI References")]
    [Tooltip("Drag your 3 Heart GameObjects here from the Canvas")]
    public GameObject[] heartIcons; 

    [Header("UI Objectives Reset")]
    [Tooltip("Drag the Ch1_Objectives parent object here")]
    public GameObject challenge1ObjectiveUI;
    [Tooltip("Drag the Ch2_Objectives parent object here")]
    public GameObject challenge2ObjectiveUI;
    [Tooltip("Drag the Ch3_Objectives parent object here")]
    public GameObject challenge3ObjectiveUI;

    [Header("Current Progress")]
    public Transform player;
    [Tooltip("The place the player will respawn if they fail their CURRENT challenge")]
    public Transform currentRespawnPoint; 
    [Tooltip("The very first respawn point (the oval)")]
    public Transform ovalRespawnPoint;
    
    [Header("Challenge 1 Reset References")]
    public LeverController leverController;
    public GateController andGateController;
    public ResettableObject[] boulders;

    [Header("Challenge 2 Reset References")]
    public FruitBasket challenge2Basket;
    public ResettableObject[] challenge2Fruits;

    [Tooltip("Drag the Challenge 2 Wizard here")]
    public WizardInteraction challenge2Wizard;

    [Tooltip("Drag the Challenge 3 Wizard here")]
    public WizardInteraction challenge3Wizard;

    [Tooltip("Drag the Wizard here so we can reset his dialogue")]
    public WizardInteraction startingWizard;

    private void Start()
    {
        HideTimer(); 
    }
    private void Awake()
    {
        if (Instance == null) Instance = this;
        
        // Ensure we have a starting respawn point when the game loads
        if (currentRespawnPoint == null && ovalRespawnPoint != null)
        {
            currentRespawnPoint = ovalRespawnPoint;
        }
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

    // --- GLOBAL HEALTH & RESPAWN METHODS ---

    // Any script can call this to deduct a heart!
    public void LoseHeartAndRespawn()
    {
        playerHearts--;
        UpdateHeartsUI();

        if (playerHearts <= 0)
        {
            Debug.Log("Game Over! Restarting the whole stage.");
            RestartEntireStage(); 
        }
        else
        {
            Debug.Log("Lost 1 heart. Respawning at current checkpoint...");
            player.position = currentRespawnPoint.position;
            
            Rigidbody playerRb = player.GetComponent<Rigidbody>();
            if (playerRb != null) playerRb.linearVelocity = Vector3.zero; 

            ResetChallenge2();
        }
    }

    private void HandleTimeout()
    {
        isTimerRunning = false;
        playerHearts--;
        UpdateHeartsUI(); 

        if (playerHearts <= 0)
        {
            Debug.Log("Game Over! Restarting the whole stage.");
            RestartEntireStage();
        }
        else
        {
            Debug.Log("Time's up! Lost 1 heart. Respawning...");
            timeRemaining = 180f; 
            player.position = currentRespawnPoint.position;
            
            Rigidbody playerRb = player.GetComponent<Rigidbody>();
            if (playerRb != null) playerRb.linearVelocity = Vector3.zero;

            ResetChallenge1();
            StartTimer(); 
        }
    }

    // Method to completely reset the game back to Challenge 1
    public void RestartEntireStage()
    {
        // 1. Refill Hearts
        playerHearts = 3;
        UpdateHeartsUI();

        // 2. Teleport back to the very beginning (the oval)
        player.position = ovalRespawnPoint.position;
        currentRespawnPoint = ovalRespawnPoint; 

        // 3. Reset the timer but DON'T start it! 
        timeRemaining = 180f;
        HideTimer(); // <--- CHANGED: This stops and hides it instead of starting it

        // 4. Reset the puzzles for Challenge 1
        ResetChallenge1();
        ResetChallenge2();

        // 5. Reset the visibility so they see Challenge 1 again
        if (challenge1Area != null) challenge1Area.SetActive(true);
        if (challenge2Area != null) challenge2Area.SetActive(false);
        if (challenge3Area != null) challenge3Area.SetActive(false);

        // 6. Reset the Wizard so they have to talk to him again <---
        if (startingWizard != null)
        {
            startingWizard.ResetWizardStatus();
        }

        if (challenge2Wizard != null)
        {
            challenge2Wizard.ResetWizardStatus();
        }

        if (challenge3Wizard != null)
        {
            challenge3Wizard.ResetWizardStatus();
        }

        // 7. Reset the Objective UI Text back to Challenge 1 <---
            if (challenge1ObjectiveUI != null) challenge1ObjectiveUI.SetActive(true);
            if (challenge2ObjectiveUI != null) challenge2ObjectiveUI.SetActive(false);
            if (challenge3ObjectiveUI != null) challenge3ObjectiveUI.SetActive(false);
    }
    private void UpdateHeartsUI()
    {
        for (int i = 0; i < heartIcons.Length; i++)
        {
            if (heartIcons[i] != null) heartIcons[i].SetActive(i < playerHearts);
        }
    }

    // --- PROGRESSION METHODS ---

    // ChallengeTransition calls this to update where the player respawns
    public void UpdateRespawnPoint(Transform newCheckpoint)
    {
        currentRespawnPoint = newCheckpoint;
    }

    // --- CHALLENGE 1 SPECIFIC METHODS ---

    public void StartTimer() 
    { 
        isTimerRunning = true; 

        if (timerText != null) 
        {
            timerText.gameObject.SetActive(true); 
        }
    }
    public void StopTimer() { isTimerRunning = false; }
    
    public void HideTimer()
    {
        isTimerRunning = false; 
        if (timerText != null) timerText.gameObject.SetActive(false); 
    }

    private void ResetChallenge1()
    {
        if (leverController != null) leverController.ResetLever();
        if (andGateController != null) andGateController.ResetGate();
        
        foreach (var boulder in boulders)
        {
            if (boulder != null) boulder.ResetPosition();
        }
    }

    private void ResetChallenge2()
    {
        if (challenge2Basket != null) challenge2Basket.ClearBasket();
        
        // Loop through all fruits and force them back to the trees
        foreach (var fruit in challenge2Fruits)
        {
            if (fruit != null) fruit.ResetPosition();
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