using UnityEngine;
using TMPro;
using Photon.Pun;

public class LevelManager : MonoBehaviourPun
{
    public static LevelManager Instance;

    [Header("Game Over Management")]
    [SerializeField] private GameOverManager gameOverManager;
    
    [Header("Stage Stats")]
    public int totalHeartsLostThisStage = 0;
    public float totalStageTime = 0f;
    private bool isStageActive = true;

    // ---> NEW: Array to hold your Challenge GameObjects <---
    [Header("Environment Reset")]
    [Tooltip("Drag your Challenge 1, 2, and 3 root GameObjects here in order.")]
    [SerializeField] private GameObject[] stageChallengeEnvironments;

    // ---> NEW: Array to hold your UI Objective Panels <---
    [Header("UI Reset")]
    [Tooltip("Drag your Ch1_Objectives, Ch2_Objectives, etc. here in order.")]
    [SerializeField] private GameObject[] stageObjectivePanels;

    // NEW (Audio Setup)
    [Header("Audio")]
    [SerializeField] private AudioClip loseHeartSound;
    [SerializeField, Range(0f, 1f)] private float loseHeartVolume = 0.8f;

    [Header("Global Game State")]
    [SerializeField] private int playerHearts = 3;
    // [SerializeField] private int maxHearts = 5;
    [SerializeField] private GameObject[] heartIcons; 
    [SerializeField] private GameObject healthBarParent;

    [Header("Timer Setup")]
    [SerializeField] private float timeRemaining = 180f;
    [SerializeField] private float currentMaxTime = 180f;
    private bool isTimerRunning = false;
    [SerializeField] private TextMeshProUGUI timerText; 

    [Header("Current Progress")]
    [HideInInspector] public Transform player;
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
        // ---> NEW: Global Stage Stopwatch <---
        if (isStageActive)
        {
            totalStageTime += Time.deltaTime;
        }

        if (isTimerRunning)
        {
            timeRemaining -= Time.deltaTime;
            UpdateTimerUI();

            if (timeRemaining <= 0) HandleTimeout();
        }
    }

    // ---> NEW: A method to drop a heart WITHOUT teleporting the player! <---
    public void LoseHeart()
    {
        if (PhotonNetwork.InRoom && photonView != null)
        {
            photonView.RPC("RPC_LoseHeartOnly", RpcTarget.All);
        }
        else
        {
            RPC_LoseHeartOnly();
        }
    }

    [PunRPC]
    public void RPC_LoseHeartOnly()
    {
        playerHearts--;
        totalHeartsLostThisStage++;
        PlayLoseHeartSound();
        UpdateHeartsUI();

        if (playerHearts <= 0)
        {
            if (gameOverManager != null) gameOverManager.ShowGameOver();
            else ResetFromGameOver();
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
            // ---> THE FIX: Clean Solo Mode Logic <---
            playerHearts--;
            totalHeartsLostThisStage++;
            PlayLoseHeartSound();
            UpdateHeartsUI();

            if (playerHearts <= 0)
            {
                if (gameOverManager != null) gameOverManager.ShowGameOver();
                else ResetFromGameOver();
                return;
            }

            // Trust the exact player who fell in!
            Transform targetPlayer = playerWhoFailed != null ? playerWhoFailed : player;

            if (targetPlayer != null)
            {
                Transform spawnPoint = currentChallenge != null ? currentChallenge.GetRespawnPoint() : ovalRespawnPoint;
                
                // ---> NEW: Use the safe teleport!
                ForceTeleportPlayer(targetPlayer, spawnPoint);
            }

            if (currentChallenge != null) currentChallenge.ResetThisChallenge();
        }
    }

    [PunRPC]
    public void RPC_HandleMistake(int failedPlayerViewID)
    {
        // 1. EVERYONE updates the shared heart UI
        playerHearts--;
        totalHeartsLostThisStage++;
        UpdateHeartsUI();
        PlayLoseHeartSound();

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
            
            // ---> NEW: Use the safe teleport!
            ForceTeleportPlayer(targetPlayer, spawnPoint);
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
        totalHeartsLostThisStage++;
        UpdateHeartsUI();
        PlayLoseHeartSound();

        if (playerHearts <= 0)
        {
            gameOverManager.ShowGameOver();
        }
        else
        {
            timeRemaining = currentMaxTime; 
            // RespawnPlayerAtCurrentCheckpoint(false);
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

        // 3. Reset Wizards and Arrows BEFORE turning off the environments!
        WizardInteraction[] allWizards = FindObjectsByType<WizardInteraction>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach(WizardInteraction wizard in allWizards)
        {
            wizard.ResetWizardStatus();
        }

        // ---> RESTORED FIX: Find all arrows and wipe them! <---
        DynamicObjectiveIndicator[] allIndicators = FindObjectsByType<DynamicObjectiveIndicator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach(var indicator in allIndicators)
        {
            indicator.ResetArrows();
        }

        // 4. Reset the challenges and Respawn the player
        RespawnPlayerAtCurrentCheckpoint();
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
        else
        {
            // ---> THE FIX: Find the solo player dynamically if 'player' is null <---
            if (targetPlayer == null)
            {
                Player localPlayer = FindFirstObjectByType<Player>();
                if (localPlayer != null)
                {
                    targetPlayer = localPlayer.transform;
                    player = targetPlayer; // Cache it
                }
            }
        }

        if (targetPlayer != null)
        {
            // ---> NEW: Force teleport to the very first spawn point safely
            ForceTeleportPlayer(targetPlayer, ovalRespawnPoint);
        }

        // ---> THE ULTIMATE FIX: Reset ALL environments and set them On/Off <---
        if (stageChallengeEnvironments != null && stageChallengeEnvironments.Length > 0)
        {
            for (int i = 0; i < stageChallengeEnvironments.Length; i++)
            {
                if (stageChallengeEnvironments[i] != null) 
                {
                    // Find the script even if it is hiding on a child object (like Challenge_1_Logic)
                    ChallengeModule challenge = stageChallengeEnvironments[i].GetComponentInChildren<ChallengeModule>(true);
                    
                    if (challenge != null)
                    {
                        // Reset gates, boulders, fruits, etc. for EVERY challenge
                        challenge.ResetThisChallenge();

                        // Make Challenge 1 the active challenge again so timers work
                        if (i == 0) currentChallenge = challenge;
                    }

                    // Turn Challenge 1 ON, and turn all subsequent challenges OFF
                    stageChallengeEnvironments[i].SetActive(i == 0);
                }
            }
        } 

        // ---> FIX: Reset the UI Objective Panels back to Challenge 1 <---
        if (stageObjectivePanels != null && stageObjectivePanels.Length > 0)
        {
            if (stageObjectivePanels[0] != null) stageObjectivePanels[0].SetActive(true);
            
            for (int i = 1; i < stageObjectivePanels.Length; i++)
            {
                if (stageObjectivePanels[i] != null) stageObjectivePanels[i].SetActive(false);
            }
        }
    }

    private void RespawnPlayerAtCurrentCheckpoint(bool resetPuzzles = true)
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
        else
        {
            // ---> THE FIX: Find the solo player dynamically if 'player' is null <---
            if (targetPlayer == null)
            {
                Player localPlayer = FindFirstObjectByType<Player>();
                if (localPlayer != null)
                {
                    targetPlayer = localPlayer.transform;
                    player = targetPlayer; // Cache it for next time!
                }
            }
        }

        // Teleport the correct player
        if (targetPlayer != null)
        {
           // ---> NEW: Use the safe teleport!
            ForceTeleportPlayer(targetPlayer, spawnPoint);
        }
        // ---------------------------

        // 2. Tell the current module to reset its own specific puzzles!
        // ---> FIX: Only reset if resetPuzzles is true! <---
        if (resetPuzzles && currentChallenge != null)
        {
            currentChallenge.ResetThisChallenge();
        }
    }

    // Called by trigger zones when entering a new area
    public void SetNewChallenge(ChallengeModule newChallenge, bool isNewStage = false)
    {
        currentChallenge = newChallenge;

        playerHearts = 3; // Baseline for a brand new stage

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

    public void StopStageTimer() { isStageActive = false; }

    public void ShowHealthBar() { if(healthBarParent != null) healthBarParent.SetActive(true); }
    public void HideHealthBar() { if(healthBarParent != null) healthBarParent.SetActive(false); }

    // ---> NEW: Safe Teleport Helper <---
    private void ForceTeleportPlayer(Transform targetPlayer, Transform destination)
    {
        if (targetPlayer == null || destination == null) return;

        // 1. Turn OFF the Character Controller so it doesn't fight the teleport
        CharacterController cc = targetPlayer.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        // ---> THE FIX: Grounded Spawn <---
        Vector3 safePosition = destination.position;
        
        // Only apply the spread and air-drop if we are online AND there is more than 1 person!
        if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.PlayerCount > 1)
        {
            Vector2 randomSpread = UnityEngine.Random.insideUnitCircle * 1.0f; 
            safePosition += new Vector3(randomSpread.x, 2f, randomSpread.y);
        }
        // Notice: The "else" block adding +2f for Solo mode has been completely deleted!
        // You now spawn exactly flush with the ground to prevent velocity tunneling!

        // 2. Teleport to the safe position
        targetPlayer.position = safePosition;

        // 3. Turn the Character Controller back ON
        if (cc != null) cc.enabled = true;

        // 4. Kill any leftover falling or spinning momentum
        Rigidbody playerRb = targetPlayer.GetComponent<Rigidbody>();
        if (playerRb != null && !playerRb.isKinematic) 
        {
            playerRb.linearVelocity = Vector3.zero; 
            playerRb.angularVelocity = Vector3.zero; 
        }
        
        // 5. INSTANTLY SNAP THE CAMERA!
        ThirdPersonCameraController camController = Object.FindFirstObjectByType<ThirdPersonCameraController>();
        if (camController != null)
        {
            camController.WarpCamera(targetPlayer);
        }


    }

    //  NEW (Same pattern as your other audio systems)
    private void PlayLoseHeartSound()
    {
        if (loseHeartSound == null) return;

        Vector3 soundPosition = player != null ? player.position : transform.position;

        SpawnHeartAudio(loseHeartSound, soundPosition);
    }

    private void SpawnHeartAudio(AudioClip clip, Vector3 position)
    {
        GameObject audioObj = new GameObject("TempHeartAudio");
        audioObj.transform.position = position;

        AudioSource source = audioObj.AddComponent<AudioSource>();
        source.clip = clip;

        source.pitch = Random.Range(0.95f, 1.05f);
        source.volume = loseHeartVolume;

        source.spatialBlend = 1f;
        source.minDistance = 1f;
        source.maxDistance = 15f;

        source.Play();

        Destroy(audioObj, clip.length + 0.1f);
    }
}

