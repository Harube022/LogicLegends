using System.Collections.Generic;
using UnityEngine;
using TMPro;

[System.Serializable]
public class LogicQuestion
{
    public string questionText;
    public string[] options = new string[4];
    public int correctOptionIndex; 
}

[System.Serializable]
public class TopicChallenge
{
    public string topicName; 
    public List<LogicQuestion> questionsPool;

    // --- STRATEGIC CHANGE: One Canvas per room instead of managing multiple separate elements ---
    [Header("Room UI Configuration")]
    [Tooltip("Drag the single World Space Canvas belonging to this specific challenge room here")]
    public Canvas roomChoiceCanvas; 

    [Header("Doors for this Challenge Area")]
    public GameObject[] choiceDoors = new GameObject[4];
    
    [Header("Respawn Setup")]
    [Tooltip("Place an empty GameObject near this specific book stand to move the player here if they choose retry")]
    public Transform topicSpawnPoint; 
}

public class QuizManager : MonoBehaviour
{
    [Header("System References")]
    [SerializeField] private LevelTimerManager timerManager;

    [Header("UI References (Top HUD Overlay)")]
    [SerializeField] private GameObject quizPanel;
    [SerializeField] private GameObject safeAreaPanel;
    [SerializeField] private TextMeshProUGUI questionTextUI;

    [Header("Challenge Sequence")]
    [SerializeField] private List<TopicChallenge> challenges;

    [Header("Shared World UI Properties")]
    [Tooltip("Drag the top-level Canvas or the RadialLoader GameObject itself here")]
    [SerializeField] private GameObject sharedWorldLoaderObject;
    [Tooltip("Drag the UI Image component with Fill Method set to Radial 360 here")]
    [SerializeField] private UnityEngine.UI.Image sharedRadialFillImage; 
    
    private int currentTopicIndex = 0;
    private LogicQuestion currentQuestion;
    private BookInteract activeBookInstance;

    // Cache to hold the texts of the currently active room canvas to optimize lookups
    private TextMeshProUGUI[] activeRoomTexts = new TextMeshProUGUI[4];

    // Public property to let SelectionPads check if a question is actively visible
    public bool IsQuizActive => quizPanel != null && quizPanel.activeSelf;

    private void Start()
    {
        if (quizPanel != null) quizPanel.SetActive(false);
        HideSharedLoader();
        HideAllRoomCanvases(); // Ensure all 3D texts are completely hidden at launch

        if (timerManager == null)
        {
            timerManager = Object.FindFirstObjectByType<LevelTimerManager>();
        }

        // Check if we are recovering from a game-over timeout
        if (LevelTimerManager.isRespawningFromFail)
        {
            // 1. Recover our saved progress index room checkpoint
            currentTopicIndex = LevelTimerManager.savedTopicIndex;
            
            // 2. Physically move the character to the active room spawn anchor
            RespawnPlayerAtCurrentTopic();

            // 3. Reset flag state so standard updates run smoothly
            LevelTimerManager.isRespawningFromFail = false;
            
            // Clean setup handling for the doors of the room we just respawned into
            ResetCurrentChallengeDoors();

            // Instantly resume timer countdown because they chose retry
            if (timerManager != null)
            {
                timerManager.StartLevelTimer();
            }
        }
        else
        {
            // Clean fresh run sequence execution setup
            currentTopicIndex = 0;
            LevelTimerManager.savedTopicIndex = 0;
            InitializeLevelState();
            
            // Note: timerManager.StartLevelTimer() is omitted here intentionally 
            // so fresh runs stay completely frozen until the first book stand button is clicked!
        }
    }

    private System.Collections.IEnumerator RespawnPlayerPosition(GameObject player, Transform targetSpawn)
    {
        CharacterController charController = player.GetComponent<CharacterController>(); 
        if (charController != null) charController.enabled = false; 
        yield return new WaitForFixedUpdate(); 
        player.transform.position = targetSpawn.position; 
        player.transform.rotation = targetSpawn.rotation; 
        yield return null;
        if (charController != null) charController.enabled = true; 
    }

    public void OpenQuiz(BookInteract callingBook)
    {
        activeBookInstance = callingBook; 

        if (safeAreaPanel != null) safeAreaPanel.SetActive(true); 
        if (quizPanel != null) quizPanel.SetActive(true); 

        LevelTimerManager.savedTopicIndex = currentTopicIndex; 

        // THE TRIGGER: Start the level countdown timer the moment the book is opened!
        if (timerManager != null && !timerManager.IsTimerRunning) 
        {
            timerManager.StartLevelTimer(); 
        }

        LoadQuestion(); 
    }

    private void LoadQuestion()
    {
        if (currentTopicIndex >= challenges.Count) 
        {
            if (timerManager != null) timerManager.StopTimer(); 
            if (quizPanel != null) quizPanel.SetActive(false); 
            return;
        }

        List<LogicQuestion> pool = challenges[currentTopicIndex].questionsPool; 
        int randomIndex = Random.Range(0, pool.Count); 
        currentQuestion = pool[randomIndex]; 

        if (questionTextUI != null)
        {
            questionTextUI.text = currentQuestion.questionText; 
        }

        // --- NEW: Handle single-canvas content population ---
        UpdateRoomFloatingTexts();
    }

    // --- NEW METHOD: Targets the active room canvas and distributes strings to its child elements ---
    private void UpdateRoomFloatingTexts()
    {
        if (currentTopicIndex >= challenges.Count) return;

        TopicChallenge currentChallenge = challenges[currentTopicIndex];
        
        if (currentChallenge.roomChoiceCanvas == null)
        {
            Debug.LogWarning($"[QuizManager] Challenge room '{currentChallenge.topicName}' is missing its Room Choice Canvas assignment!");
            return;
        }

        // 1. Gather all TextMeshProUGUI elements attached inside this single canvas container
        TextMeshProUGUI[] foundTexts = currentChallenge.roomChoiceCanvas.GetComponentsInChildren<TextMeshProUGUI>(true);

        // 2. Map and update text data based on array limits safely
        for (int i = 0; i < foundTexts.Length; i++)
        {
            if (i < currentQuestion.options.Length)
            {
                foundTexts[i].text = currentQuestion.options[i];
            }
        }

        // 3. Make the single canvas completely visible to the player
        currentChallenge.roomChoiceCanvas.gameObject.SetActive(true);
    }

    // --- NEW METHOD: Turns off all 5 room canvases instantly ---
    private void HideAllRoomCanvases()
    {
        foreach (var challenge in challenges)
        {
            if (challenge.roomChoiceCanvas != null)
            {
                challenge.roomChoiceCanvas.gameObject.SetActive(false);
            }
        }
    }

    private void RespawnPlayerAtCurrentTopic()
    {
        if (currentTopicIndex >= challenges.Count) return;

        Transform spawnPoint = challenges[currentTopicIndex].topicSpawnPoint;

        // Ensure the spawn point is actually assigned in the Inspector
        if (spawnPoint != null)
        {
            StartCoroutine(WaitAndRespawnPlayer(spawnPoint));
        }
        else
        {
            Debug.LogWarning($"[QuizManager] Failed to respawn. SpawnPoint for Challenge Index {currentTopicIndex} is missing in the Inspector!");
        }
    }

    private System.Collections.IEnumerator WaitAndRespawnPlayer(Transform targetSpawn)
    {
        GameObject player = null;

        // 1. Wait until the player is actually in the scene and tagged correctly
        while (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
            yield return null; // Wait for the next frame and try again
        }

        // 2. Safely disable the CharacterController for the teleport
        CharacterController charController = player.GetComponent<CharacterController>();
        if (charController != null) charController.enabled = false;

        // 3. Wait for the physics engine to update
        yield return new WaitForFixedUpdate();

        // 4. Move the player to the saved challenge room
        player.transform.position = targetSpawn.position;
        player.transform.rotation = targetSpawn.rotation;

        yield return null;

        // 5. Re-enable the controller
        if (charController != null) charController.enabled = true;
    }

    private void InitializeLevelState()
    {
        ResetCurrentChallengeDoors();
    }

    public void ClearQuizUI()
    {
        if (quizPanel != null) quizPanel.SetActive(false);

        if (activeBookInstance != null) 
        {
            activeBookInstance.ForceHideButton();
        }
        HideAllRoomCanvases(); // Completely hide the world texts when quiz exits
    }

    public void FinalizeChallengeCompletion()
    {
        if (quizPanel != null) quizPanel.SetActive(false);
        if (activeBookInstance != null) activeBookInstance.ForceHideButton();
    }

    public bool IsChoiceCorrect(int index)
    {
        return currentQuestion != null && index == currentQuestion.correctOptionIndex; 
    }

    public void AdvanceToNextChallenge()
    {
        ClearQuizUI();

        currentTopicIndex++; 
        LevelTimerManager.savedTopicIndex = currentTopicIndex; 

        if (currentTopicIndex >= challenges.Count) 
        {
            Debug.Log("All challenges complete!");
            if (timerManager != null) 
            {
                timerManager.StopTimer(); 
                // NEW: Hide the timer from the screen
                timerManager.SetTimerVisibility(false); 
            }
            if (quizPanel != null) quizPanel.SetActive(false);

            // NEW: Show the inventory bar for the Truth Table game
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.SetInventoryVisibility(true);
            }
        }
    }

    public void ResetCurrentChallengeDoors()
    {
        if (currentTopicIndex >= challenges.Count) return; 

        TopicChallenge currentChallenge = challenges[currentTopicIndex]; 
        foreach (GameObject door in currentChallenge.choiceDoors) 
        {
            if (door != null) door.SetActive(true); 
        }
    }

    public void PrepareSharedLoader(Transform targetAnchor)
    {
        if (sharedWorldLoaderObject != null)
        {
            sharedWorldLoaderObject.transform.position = targetAnchor.position;
            sharedWorldLoaderObject.SetActive(true);
        }

        if (sharedRadialFillImage != null)
        {
            sharedRadialFillImage.fillAmount = 0f;
        }
    }

    public void UpdateSharedLoaderFill(float fillPercentage)
    {
        if (sharedRadialFillImage != null)
        {
            sharedRadialFillImage.fillAmount = fillPercentage;
        }
    }

    public void HideSharedLoader()
    {
        if (sharedRadialFillImage != null)
        {
            sharedRadialFillImage.fillAmount = 0f;
        }

        if (sharedWorldLoaderObject != null)
        {
            sharedWorldLoaderObject.SetActive(false);
        }
    }
}