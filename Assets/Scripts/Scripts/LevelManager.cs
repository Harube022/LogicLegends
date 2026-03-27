using UnityEngine;
using TMPro;
using Photon.Pun;

public class LevelManager : MonoBehaviourPun
{
    public static LevelManager Instance;

    [Header("Game Over Management")]
    [SerializeField] private GameOverManager gameOverManager;

    // ---> NEW: Array to hold your Challenge GameObjects <---
    [Header("Environment Reset")]
    [Tooltip("Drag your Challenge 1, 2, and 3 root GameObjects here in order.")]
    [SerializeField] private GameObject[] stageChallengeEnvironments;

    [Header("Global Game State")]
    [SerializeField] private int playerHearts = 3;
    [SerializeField] private int maxHearts = 5;
    [SerializeField] private GameObject[] heartIcons; 
    [SerializeField] private GameObject healthBarParent;

    [Header("Timer Setup")]
    [SerializeField] private float timeRemaining = 180f;
    [SerializeField] private float currentMaxTime = 180f;
    private bool isTimerRunning = false;
    [SerializeField] private TextMeshProUGUI timerText; 

    [Header("Current Progress")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform ovalRespawnPoint; // The very first spawn

    [Header("The Active Challenge")]
    [Tooltip("This automatically updates when the player enters a new Challenge area")]
    [SerializeField] private ChallengeModule currentChallenge;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        HideTimer(); 
    }

    private void Update()
    {
        if (isTimerRunning)
        {
            timeRemaining -= Time.deltaTime;
            UpdateTimerUI();

            if (timeRemaining <= 0) HandleTimeout();
        }
    }

    // --- GLOBAL HEALTH & RESPAWN ---

    // 2. We now ask for the specific player who failed
    public void LoseHeartAndRespawn(Transform playerWhoFailed = null)
    {
        if (PhotonNetwork.InRoom && photonView != null)
        {
            // Get the unique network ID of the player who failed
            int failedViewID = -1;
            if (playerWhoFailed != null)
            {
                PhotonView pv = playerWhoFailed.GetComponent<PhotonView>();
                if (pv != null) failedViewID = pv.ViewID;
            }
            
            // Tell EVERYONE to drop a heart, but pass along who specifically needs to teleport
            photonView.RPC("RPC_HandleMistake", RpcTarget.All, failedViewID);
        }
        else
        {
            RPC_HandleMistake(-1); // Solo fallback
        }
    }

    [PunRPC]
    public void RPC_HandleMistake(int failedPlayerViewID)
    {
        // 1. EVERYONE updates the shared heart UI
        playerHearts--;
        UpdateHeartsUI();

        if (playerHearts <= 0)
        {
            // ---> NEW: Added a safety check! <---
            if (gameOverManager != null)
            {
                gameOverManager.ShowGameOver();
            }
            else
            {
                Debug.LogError("No Game Over Manager assigned! Forcing a reset instead.");
                ResetFromGameOver(); // Auto-restart if we forgot the UI!
            }
            return;
        }

        // 2. Check who needs to teleport
        bool isMyPlayer = false;
        Transform targetPlayer = null;

        if (failedPlayerViewID == -1) 
        {
            // ---> THE FIX FOR CHALLENGE 1 & 2 <---
            // If no specific player was blamed, it means a global puzzle failed (like wrong fruit).
            // We teleport everyone so the team can try the puzzle again together.
            isMyPlayer = true; 
            if (PhotonNetwork.InRoom)
            {
                Player[] allPlayers = FindObjectsByType<Player>(FindObjectsSortMode.None);
                foreach (Player p in allPlayers)
                {
                    if (p.GetComponent<PhotonView>().IsMine)
                    {
                        targetPlayer = p.transform;
                        break;
                    }
                }
            }
            else
            {
                targetPlayer = player; // Solo fallback
            }
        }
        else
        {
            // Search the network for the specific clumsy player (Challenge 3 Water)
            PhotonView pv = PhotonView.Find(failedPlayerViewID);
            if (pv != null)
            {
                targetPlayer = pv.transform;
                if (pv.IsMine) isMyPlayer = true; 
            }
        }

        // 3. ONLY the person who owns the target player forces them to teleport
        if (isMyPlayer && targetPlayer != null)
        {
            Transform spawnPoint = currentChallenge != null ? currentChallenge.GetRespawnPoint() : ovalRespawnPoint;
            
            targetPlayer.position = spawnPoint.position;
            Rigidbody playerRb = targetPlayer.GetComponent<Rigidbody>();
            if (playerRb != null && !playerRb.isKinematic) 
                playerRb.linearVelocity = Vector3.zero; 
        }

        // 4. Reset the module (boulders, etc.) globally so they can try again
        if (currentChallenge != null && PhotonNetwork.IsMasterClient)
        {
            currentChallenge.ResetThisChallenge();
        }
    }

    private void HandleTimeout()
    {
        isTimerRunning = false;
        playerHearts--;
        UpdateHeartsUI(); 

        if (playerHearts <= 0)
        {
            gameOverManager.ShowGameOver();
        }
        else
        {
            timeRemaining = currentMaxTime; 
            RespawnPlayerAtCurrentCheckpoint();
            StartTimer(); 
        }
    }

    public void ResetFromGameOver()
    {
        // 1. Refill Hearts
        playerHearts = 3;
        UpdateHeartsUI();

        // 2. Stop and hide the timer (it will wait for the wizard to start it again)
        ResetTimer(); 

        // 3. Reset the current Challenge and Respawn
        RespawnPlayerAtStageStart();

        // 4. THE FIX: Find every wizard in the scene and reset them
        // We use FindObjectsInactive.Include just in case a wizard is temporarily hidden
        WizardInteraction[] allWizards = FindObjectsByType<WizardInteraction>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        
        foreach(WizardInteraction wizard in allWizards)
        {
            wizard.ResetWizardStatus();
        }
    }

    // ---> NEW: This method forces the player back to the ovalRespawnPoint <---
    private void RespawnPlayerAtStageStart()
    {
        Transform targetPlayer = player; 

        if (PhotonNetwork.InRoom)
        {
            Player[] allPlayers = FindObjectsByType<Player>(FindObjectsSortMode.None);
            foreach (Player p in allPlayers)
            {
                if (p.GetComponent<PhotonView>().IsMine)
                {
                    targetPlayer = p.transform;
                    break;
                }
            }
        }

        if (targetPlayer != null)
        {
            // Force teleport to the very first spawn point
            targetPlayer.position = ovalRespawnPoint.position;
            
            Rigidbody playerRb = targetPlayer.GetComponent<Rigidbody>();
            if (playerRb != null && !playerRb.isKinematic) 
                playerRb.linearVelocity = Vector3.zero; 
        }

        // ---> NEW: Reset EVERY challenge module in the scene, not just the active one <---
        ChallengeModule[] allChallenges = FindObjectsByType<ChallengeModule>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach(ChallengeModule challenge in allChallenges)
        {
            challenge.ResetThisChallenge();
        }

        // Clear the current challenge so walking into the Stage 1 trigger sets it up fresh
        currentChallenge = null;

        // ---> NEW: Reset the physical environments <---
        if (stageChallengeEnvironments != null && stageChallengeEnvironments.Length > 0)
        {
            // 1. Turn ON the first challenge
            if (stageChallengeEnvironments[0] != null) stageChallengeEnvironments[0].SetActive(true);
            
            // 2. Turn OFF all subsequent challenges
            for (int i = 1; i < stageChallengeEnvironments.Length; i++)
            {
                if (stageChallengeEnvironments[i] != null) stageChallengeEnvironments[i].SetActive(false);
            }
        } 
    }

    private void RespawnPlayerAtCurrentCheckpoint()
    {
        // 1. Figure out where we need to go
        Transform spawnPoint = currentChallenge != null ? currentChallenge.GetRespawnPoint() : ovalRespawnPoint;

        // --- THE MULTIPLAYER FIX ---
        Transform targetPlayer = player; // Fallback to your Inspector reference for solo testing

        if (PhotonNetwork.InRoom)
        {
            // Search the scene for all players, but ONLY grab the one that belongs to this specific computer/phone
            Player[] allPlayers = FindObjectsByType<Player>(FindObjectsSortMode.None);
            foreach (Player p in allPlayers)
            {
                if (p.GetComponent<PhotonView>().IsMine)
                {
                    targetPlayer = p.transform;
                    break;
                }
            }
        }

        // Teleport the correct player
        if (targetPlayer != null)
        {
            targetPlayer.position = spawnPoint.position;
            
            Rigidbody playerRb = targetPlayer.GetComponent<Rigidbody>();
            if (playerRb != null && !playerRb.isKinematic) 
                playerRb.linearVelocity = Vector3.zero; 
        }
        // ---------------------------

        // 2. Tell the current module to reset its own specific puzzles!
        if (currentChallenge != null)
        {
            currentChallenge.ResetThisChallenge();
        }
    }

    // Called by trigger zones when entering a new area
    public void SetNewChallenge(ChallengeModule newChallenge, bool isNewStage = false)
    {
        currentChallenge = newChallenge;
        
        if (isNewStage)
        {
            playerHearts = 3; // Baseline for a brand new stage
        }
        else
        {
            playerHearts++; // Reward for beating the previous challenge!
            if (playerHearts > maxHearts) playerHearts = maxHearts; // Cap at 5
        }
        
        UpdateHeartsUI();
    }

    private void UpdateHeartsUI()
    {
        for (int i = 0; i < heartIcons.Length; i++)
        {
            if (heartIcons[i] != null) heartIcons[i].SetActive(i < playerHearts);
        }
    }

    // --- TIMER METHODS ---

    public void StartTimer() 
    { 
        isTimerRunning = true; 
        if (timerText != null) timerText.gameObject.SetActive(true); 
    }
    
    public void StopTimer() { isTimerRunning = false; }
    
    public void HideTimer()
    {
        isTimerRunning = false; 
        if (timerText != null) timerText.gameObject.SetActive(false); 
    }

    public void ResetTimer()
    {
        timeRemaining = 180f; 
        UpdateTimerUI();      
        HideTimer();          
    }

    public void StartCustomTimer(float newDurationInSeconds)
    {
        currentMaxTime = newDurationInSeconds; 
        timeRemaining = currentMaxTime;
        UpdateTimerUI();
        StartTimer();
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

    public void ShowHealthBar() { if(healthBarParent != null) healthBarParent.SetActive(true); }
    public void HideHealthBar() { if(healthBarParent != null) healthBarParent.SetActive(false); }
}

