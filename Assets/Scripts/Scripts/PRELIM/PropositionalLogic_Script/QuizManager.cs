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

    [Header("Progressive Truth Table Hint")]
    [Tooltip("The existing 3-column truth-table board above this room's doors.")]
    public TruthTableHintBoard hintBoard;
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
    private readonly List<int> challengeOrder = new List<int>();
    private readonly List<int> currentQuestionOrder = new List<int>();
    private readonly int[] currentDoorOptionOrder = { 0, 1, 2, 3 };
    private int nextQuestionOrderIndex;
    private int previousQuestionIndex = -1;
    private bool currentChallengeStarted;
    private bool hasDoorOptionOrder;

    // The timer retry reloads PRELIM, so keep the generated order for that same run.
    private static readonly List<int> savedChallengeOrder = new List<int>();

    // Cache to hold the texts of the currently active room canvas to optimize lookups
    private TextMeshProUGUI[] activeRoomTexts = new TextMeshProUGUI[4];

    // Public property to let SelectionPads check if a question is actively visible
    public bool IsQuizActive => quizPanel != null && quizPanel.activeSelf;
    public bool IsSequenceComplete => challengeOrder.Count > 0 && currentTopicIndex >= challengeOrder.Count;

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
            RestoreChallengeOrder();

            // 1. Recover our saved progress index room checkpoint
            currentTopicIndex = Mathf.Clamp(
                LevelTimerManager.savedTopicIndex,
                0,
                Mathf.Max(0, challengeOrder.Count - 1));
            
            // 2. Physically move the character to the active room spawn anchor
            RespawnPlayerAtCurrentTopic();

            // 3. Reset flag state so standard updates run smoothly
            LevelTimerManager.isRespawningFromFail = false;
            
            // Clean setup handling for the doors of the room we just respawned into
            ResetCurrentChallengeDoors();

            // Retry remains paused at 04:30 until the player activates this room's Book.
            PrepareCurrentHintBoard();
        }
        else
        {
            // Clean fresh run sequence execution setup
            currentTopicIndex = 0;
            LevelTimerManager.savedTopicIndex = 0;
            if (timerManager != null)
            {
                timerManager.ResetForFreshRun();
            }
            else
            {
                LevelTimerManager.ResetSession();
            }
            GenerateChallengeOrder();
            InitializeLevelState();
            RespawnPlayerAtCurrentTopic();
            PrepareCurrentHintBoard();
            
            // Note: timerManager.StartLevelTimer() is omitted here intentionally 
            // so fresh runs stay completely frozen until the first book stand button is clicked!
        }
    }

    private void Update()
    {
        if (!currentChallengeStarted || timerManager == null || !timerManager.IsTimerRunning)
        {
            return;
        }

        GetCurrentChallenge()?.hintBoard?.AdvanceActiveTime(Time.deltaTime);
    }

    private void GenerateChallengeOrder()
    {
        challengeOrder.Clear();

        for (int i = 0; i < challenges.Count; i++)
        {
            challengeOrder.Add(i);
        }

        // Fisher-Yates shuffle: every configured challenge appears exactly once.
        for (int i = challengeOrder.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            (challengeOrder[i], challengeOrder[swapIndex]) = (challengeOrder[swapIndex], challengeOrder[i]);
        }

        savedChallengeOrder.Clear();
        savedChallengeOrder.AddRange(challengeOrder);

        Debug.Log($"[QuizManager] Propositional Logic order: {GetChallengeOrderDescription()}");
    }

    private void RestoreChallengeOrder()
    {
        if (!IsSavedChallengeOrderValid())
        {
            GenerateChallengeOrder();
            return;
        }

        challengeOrder.Clear();
        challengeOrder.AddRange(savedChallengeOrder);
        Debug.Log($"[QuizManager] Restored Propositional Logic order: {GetChallengeOrderDescription()}");
    }

    private bool IsSavedChallengeOrderValid()
    {
        if (savedChallengeOrder.Count != challenges.Count)
        {
            return false;
        }

        bool[] seen = new bool[challenges.Count];
        foreach (int challengeIndex in savedChallengeOrder)
        {
            if (challengeIndex < 0 || challengeIndex >= challenges.Count || seen[challengeIndex])
            {
                return false;
            }

            seen[challengeIndex] = true;
        }

        return true;
    }

    private TopicChallenge GetCurrentChallenge()
    {
        if (currentTopicIndex < 0 || currentTopicIndex >= challengeOrder.Count)
        {
            return null;
        }

        int challengeIndex = challengeOrder[currentTopicIndex];
        return challengeIndex >= 0 && challengeIndex < challenges.Count
            ? challenges[challengeIndex]
            : null;
    }

    private string GetChallengeOrderDescription()
    {
        List<string> names = new List<string>();
        foreach (int challengeIndex in challengeOrder)
        {
            names.Add(challenges[challengeIndex].topicName);
        }

        return string.Join(" -> ", names);
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
        if (GetCurrentChallenge() == null || IsQuizActive ||
            (timerManager != null && timerManager.RemainingTime <= 0f)) return;

        activeBookInstance = callingBook; 

        if (safeAreaPanel != null) safeAreaPanel.SetActive(true); 
        if (quizPanel != null) quizPanel.SetActive(true); 

        LevelTimerManager.savedTopicIndex = currentTopicIndex; 

        bool isFirstActivationForChallenge = !currentChallengeStarted;
        if (isFirstActivationForChallenge)
        {
            currentChallengeStarted = true;
            PrepareCurrentHintBoard();
            // Only the first book activation of THIS challenge resumes the shared
            // countdown. Wrong-answer reactivation only loads another question.
            if (timerManager != null) timerManager.StartLevelTimer();
        }

        LoadQuestion();
    }

    private void LoadQuestion()
    {
        TopicChallenge currentChallenge = GetCurrentChallenge();
        if (currentChallenge == null)
        {
            if (timerManager != null) timerManager.StopTimer(); 
            if (quizPanel != null) quizPanel.SetActive(false); 
            return;
        }

        List<LogicQuestion> pool = currentChallenge.questionsPool;
        if (pool == null || pool.Count == 0)
        {
            Debug.LogError($"[QuizManager] Challenge '{currentChallenge.topicName}' has no questions configured.");
            return;
        }

        SelectNextQuestion(pool);

        ShuffleDoorOptions();

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
        TopicChallenge currentChallenge = GetCurrentChallenge();
        if (currentChallenge == null) return;
        
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
                int optionIndex = currentDoorOptionOrder[i];
                foundTexts[i].text = optionIndex < currentQuestion.options.Length
                    ? currentQuestion.options[optionIndex]
                    : string.Empty;
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
        TopicChallenge currentChallenge = GetCurrentChallenge();
        if (currentChallenge == null) return;

        Transform spawnPoint = currentChallenge.topicSpawnPoint;

        // Ensure the spawn point is actually assigned in the Inspector
        if (spawnPoint != null)
        {
            StartCoroutine(WaitAndRespawnPlayer(spawnPoint));
        }
        else
        {
            Debug.LogWarning($"[QuizManager] Failed to respawn. SpawnPoint for '{currentChallenge.topicName}' is missing in the Inspector!");
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
        Vector3 deltaPosition = targetSpawn.position - player.transform.position;
        player.transform.position = targetSpawn.position;
        player.transform.rotation = targetSpawn.rotation;
        Unity.Cinemachine.CinemachineCore.OnTargetObjectWarped(player.transform, deltaPosition);

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
        return currentQuestion != null && index >= 0 && index < currentDoorOptionOrder.Length &&
            currentDoorOptionOrder[index] == currentQuestion.correctOptionIndex;
    }

    public int GetOptionIndexForDoor(int doorIndex)
    {
        return doorIndex >= 0 && doorIndex < currentDoorOptionOrder.Length
            ? currentDoorOptionOrder[doorIndex]
            : -1;
    }

    public Transform AdvanceToNextChallenge()
    {
        // A correct answer pauses the shared timer before any transition work occurs.
        if (timerManager != null) timerManager.StopTimer();

        ClearQuizUI();

        currentTopicIndex++; 
        LevelTimerManager.savedTopicIndex = currentTopicIndex; 
        currentChallengeStarted = false;
        currentQuestion = null;
        ResetQuestionSequence();
        hasDoorOptionOrder = false;

        if (currentTopicIndex >= challengeOrder.Count)
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

            // Call the dedicated visibility manager
            if (AreaVisibilityManager.Instance != null)
            {
                AreaVisibilityManager.Instance.TransitionToTruthTable();
            }

            return null;
        }

        TopicChallenge nextChallenge = GetCurrentChallenge();
        ResetCurrentChallengeDoors();
        PrepareCurrentHintBoard();
        return nextChallenge != null ? nextChallenge.topicSpawnPoint : null;
    }

    private void PrepareCurrentHintBoard()
    {
        TopicChallenge challenge = GetCurrentChallenge();
        if (challenge == null || challenge.hintBoard == null) return;

        challenge.hintBoard.BeginChallenge(
            TruthTableHintBoard.ParseOperator(challenge.topicName));
    }

    public void ResetCurrentChallengeDoors()
    {
        TopicChallenge currentChallenge = GetCurrentChallenge();
        if (currentChallenge == null) return;

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

    private void ShuffleDoorOptions()
    {
        int[] previousOrder = (int[])currentDoorOptionOrder.Clone();

        for (int i = 0; i < currentDoorOptionOrder.Length; i++)
        {
            currentDoorOptionOrder[i] = i;
        }

        // Fisher-Yates gives every door permutation equal probability.
        for (int i = currentDoorOptionOrder.Length - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            (currentDoorOptionOrder[i], currentDoorOptionOrder[swapIndex]) =
                (currentDoorOptionOrder[swapIndex], currentDoorOptionOrder[i]);
        }

        // Every Book activation must visibly move at least two choices. This also
        // prevents a valid Fisher-Yates shuffle from appearing unchanged by chance.
        if (hasDoorOptionOrder && OrdersMatch(previousOrder, currentDoorOptionOrder))
        {
            int first = Random.Range(0, currentDoorOptionOrder.Length);
            int second = (first + Random.Range(1, currentDoorOptionOrder.Length)) % currentDoorOptionOrder.Length;
            (currentDoorOptionOrder[first], currentDoorOptionOrder[second]) =
                (currentDoorOptionOrder[second], currentDoorOptionOrder[first]);
        }

        hasDoorOptionOrder = true;
    }

    private void SelectNextQuestion(List<LogicQuestion> pool)
    {
        if (currentQuestionOrder.Count != pool.Count ||
            nextQuestionOrderIndex >= currentQuestionOrder.Count)
        {
            GenerateQuestionOrder(pool.Count);
        }

        int questionIndex = currentQuestionOrder[nextQuestionOrderIndex++];
        currentQuestion = pool[questionIndex];
        previousQuestionIndex = questionIndex;
    }

    private void GenerateQuestionOrder(int questionCount)
    {
        currentQuestionOrder.Clear();
        for (int i = 0; i < questionCount; i++)
        {
            currentQuestionOrder.Add(i);
        }

        // Shuffle the existing premise pool once per cycle so every configured
        // premise is used before any premise can repeat.
        for (int i = currentQuestionOrder.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            (currentQuestionOrder[i], currentQuestionOrder[swapIndex]) =
                (currentQuestionOrder[swapIndex], currentQuestionOrder[i]);
        }

        // At a cycle boundary, keep the newly shuffled first premise from being
        // the same one the player just saw.
        if (currentQuestionOrder.Count > 1 && currentQuestionOrder[0] == previousQuestionIndex)
        {
            int swapIndex = Random.Range(1, currentQuestionOrder.Count);
            (currentQuestionOrder[0], currentQuestionOrder[swapIndex]) =
                (currentQuestionOrder[swapIndex], currentQuestionOrder[0]);
        }

        nextQuestionOrderIndex = 0;
    }

    private void ResetQuestionSequence()
    {
        currentQuestionOrder.Clear();
        nextQuestionOrderIndex = 0;
        previousQuestionIndex = -1;
    }

    private static bool OrdersMatch(int[] left, int[] right)
    {
        if (left.Length != right.Length) return false;

        for (int i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i]) return false;
        }

        return true;
    }
}
