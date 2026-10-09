using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Uses the Setssss scene's existing numbered GrabbableObjects and hand/E input.
// The original SetsStageManager supplies the shared set-membership validator.
[DisallowMultipleComponent]
public sealed class SetssssChallengeOne : MonoBehaviour
{
    [Header("Setssss scene objects")]
    [SerializeField] private Transform bookStatue;
    [SerializeField] private Transform spawner;
    [SerializeField] private Transform diagram;
    [Header("Challenge 5 universal set")]
    [SerializeField] private Transform universalBoundary;
    [SerializeField, Min(0f)] private float universalPlacementInset = 2f;
    [Header("Challenge 5 views of the existing AB diagram")]
    [SerializeField] private Mesh existingACircleMesh;
    [SerializeField] private Mesh existingBCircleMesh;
    [Header("Challenge 4 set-difference regions")]
    [SerializeField] private GameObject aMinusBRegion;
    [SerializeField] private GameObject bMinusARegion;
    [SerializeField] private GrabbableObject[] numberedBlocks = new GrabbableObject[10];
    [SerializeField] private Vector3[] blockSpawnOffsets =
    {
        new Vector3(-4.95f, 1.84f, -2.85f), new Vector3(-1.65f, 1.84f, -2.85f),
        new Vector3(1.65f, 1.84f, -2.85f), new Vector3(4.95f, 1.84f, -2.85f),
        new Vector3(-4.95f, 1.84f, 0.75f), new Vector3(-1.65f, 1.84f, 0.75f),
        new Vector3(1.65f, 1.84f, 0.75f), new Vector3(4.95f, 1.84f, 0.75f),
        new Vector3(-1.65f, 1.84f, 4.35f), new Vector3(1.65f, 1.84f, 4.35f)
    };
    [SerializeField] private TextMeshPro boardText;
    [SerializeField] private TextMeshPro bookPrompt;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private Button submitButton;
    [SerializeField] private Button interactWithBookButton;
    [SerializeField] private Button pickUpBlockButton;
    [SerializeField] private Button returnBlockButton;
    [SerializeField] private TMP_Text reminderText;
    [Header("Challenge countdown")]
    [SerializeField] private TMP_Text timerText;
    [SerializeField, Min(1f)] private float challengeDurationSeconds = 18f * 60f;
    [SerializeField, Min(0f)] private float incorrectSubmissionPenaltySeconds = 10f;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private SetssssBoardView boardView;
    [SerializeField, Min(1f)] private float bookInteractionRange = 3.5f;
    [SerializeField, Min(1f)] private float blockInteractionRange = 2.4f;
    [SerializeField, Min(1f)] private float diagramInteractionRange = 3f;
    [SerializeField, Min(0f)] private float reminderPastBoardDistance = 5f;
    [SerializeField] private UnityEvent onChallengeCompleted;

    private const int BlocksPerCondition = 10;
    private const int ConditionsPerChallenge = 10;
    private const int ComplementConditions = 5;
    // Entry 6 is the supplied B = {29}; every other Challenge 1 entry is A-only.
    private static readonly SetsConditionData[] challengeOneBank =
    {
        Condition("47", ""), Condition("12", ""), Condition("83", ""),
        Condition("5", ""), Condition("71", ""), Condition("", "29"),
        Condition("94", ""), Condition("36", ""), Condition("58", ""),
        Condition("16", "")
    };
    private static readonly SetsConditionData[] challengeTwoBank =
    {
        Condition("12,27,45,68", "31,54,76,89"), Condition("7,23,51,84", "16,39,62,95"),
        Condition("14,36,57,73", "21,48,69,91"), Condition("5,28,44,82", "13,37,61,97"),
        Condition("19,33,56,74", "8,42,67,88"), Condition("3,25,47,79", "18,34,63,92"),
        Condition("11,38,52,86", "24,49,71,98"), Condition("6,29,53,77", "15,41,64,93"),
        Condition("17,32,59,81", "9,26,68,96"), Condition("22,43,65,87", "4,35,72,99")
    };
    private static readonly SetsConditionData[] challengeThreeBank =
    {
        Condition("12,27,45,68", "31,45,68,89"), Condition("7,23,51,84", "16,23,62,84"),
        Condition("14,36,57,73", "21,36,69,73"), Condition("5,28,44,82", "13,44,61,82"),
        Condition("19,33,56,74", "8,33,67,74"), Condition("3,25,47,79", "18,47,63,79"),
        Condition("11,38,52,86", "24,38,71,86"), Condition("6,29,53,77", "15,53,64,77"),
        Condition("17,32,59,81", "9,32,68,81"), Condition("22,43,65,87", "4,43,72,87")
    };
    private static readonly SetsConditionData[] challengeFourBank =
    {
        Condition("12,24,37,45,68", "12,37,56,79"),
        Condition("7,18,32,54,91", "7,32,46,63,88"),
        Condition("15,29,41,67,83", "22,29,67,74,96"),
        Condition("4,26,39,58,72", "11,26,58,81,94"),
        Condition("19,33,47,61,85", "19,47,53,76,99"),
        Condition("8,21,44,65,93", "14,21,65,78,87"),
        Condition("16,35,49,73,90", "16,49,62,77,98"),
        Condition("5,28,43,57,82", "5,43,69,71,95"),
        Condition("13,31,52,64,89", "20,31,52,75,97"),
        Condition("23,36,48,70,86", "9,36,48,59,92")
    };
    private sealed class ComplementCondition
    {
        public readonly string[] universe;
        public readonly string[] selected;
        public readonly string setName;

        public ComplementCondition(string universeValues, string selectedValues, string name)
        {
            universe = universeValues.Split(',');
            selected = selectedValues.Split(',');
            setName = name;
        }

    }
    private static readonly ComplementCondition[] challengeFiveBank =
    {
        new ComplementCondition("1,2,3,4,5,6,7,8,9", "1,2", "A"),
        new ComplementCondition("5,12,24,37,48,61,73,86,99", "12,37,61", "B"),
        new ComplementCondition("7,18,29,40,51,62,73,84,95", "7,29,51,73", "A"),
        new ComplementCondition("3,14,25,36,47,58,69,80,91", "14,36,58,80", "B"),
        new ComplementCondition("9,16,28,35,49,57,64,82,96", "16,35,57", "A")
    };
    private readonly int[] selectedEntryIndices = new int[ConditionsPerChallenge];
    private readonly List<int> unansweredEntries = new List<int>(ConditionsPerChallenge);

    private readonly Dictionary<string, SetZone> placed = new Dictionary<string, SetZone>();
    private readonly Dictionary<string, bool> placedOnDifferenceRegion = new Dictionary<string, bool>();
    private readonly Dictionary<string, int> placementSlots = new Dictionary<string, int>();
    // Challenge 4 allows one value in several regions, so its placements are
    // tracked by physical block instance rather than by the displayed number.
    private enum ChallengeFourRegion { A, B, Intersection, AMinusB, BMinusA }
    private enum ChallengeFourBlockState { AtSpawner, Held, Placed }
    private readonly HashSet<GrabbableObject> challengeFourCopies = new HashSet<GrabbableObject>();
    private readonly Dictionary<GrabbableObject, ChallengeFourBlockState> challengeFourStates =
        new Dictionary<GrabbableObject, ChallengeFourBlockState>();
    private readonly Dictionary<GrabbableObject, int> challengeFourSpawnSlots = new Dictionary<GrabbableObject, int>();
    private readonly GrabbableObject[] challengeFourAvailableAtSlot = new GrabbableObject[BlocksPerCondition];
    private readonly Dictionary<GrabbableObject, ChallengeFourRegion> challengeFourPlacements = new Dictionary<GrabbableObject, ChallengeFourRegion>();
    private readonly Dictionary<GrabbableObject, int> challengeFourSlots = new Dictionary<GrabbableObject, int>();
    private sealed class PlacementReview
    {
        public readonly HashSet<GrabbableObject> incorrectBlocks = new HashSet<GrabbableObject>();
        public bool missingRequired;
    }
    private readonly Dictionary<GrabbableObject, LineRenderer> errorIndicators =
        new Dictionary<GrabbableObject, LineRenderer>();
    private Material errorIndicatorMaterial;
    private readonly Vector3[] spawnPositions = new Vector3[BlocksPerCondition];
    private readonly Quaternion[] spawnRotations = new Quaternion[BlocksPerCondition];
    private readonly Transform[] spawnParents = new Transform[BlocksPerCondition];
    private GameInput input;
    private Player player;
    private GrabbableObject previouslyHeld;
    private Bounds diagramBounds;
    private Bounds aMinusBBounds;
    private Bounds bMinusABounds;
    private Bounds universalBounds;
    private Renderer diagramRenderer;
    private bool diagramRendererOriginallyEnabled;
    private MeshFilter diagramMeshFilter;
    private Mesh originalDiagramMesh;
    private TextMeshPro universalLabel;
    private ComplementCondition activeComplement;
    private bool started;
    private bool advancing;
    private bool complete;
    private bool gameOverTriggered;
    private float timeRemaining;
    private int displayedSeconds = -1;
    private int challengeNumber = 1;
    private int conditionIndex;
    private SetsConditionData activeCondition;
    private float lastInteractionTime = -10f;
    private float nextSubmitTime;
    private RectTransform viewBoardRect;
    private float feedbackDefaultFontSize;
    private Vector2 feedbackDefaultSize;
    private Vector2 feedbackDefaultPosition;
    private int feedbackGeneration;

    public static SetssssChallengeOne ActiveInstance { get; private set; }

    public bool Started => started;
    public bool Complete => complete;
    public bool IsGameOver => gameOverTriggered;
    public float TimeRemaining => timeRemaining;
    public int ConditionIndex => conditionIndex;
    public int ChallengeNumber => challengeNumber;
    public SetsConditionData ActiveCondition => activeCondition;
    public IReadOnlyList<int> SelectedEntryIndices => selectedEntryIndices;
    private int CurrentConditionCount => challengeNumber == 5 ? ComplementConditions : ConditionsPerChallenge;
    private bool HasActiveQuestion => challengeNumber == 5 ? activeComplement != null : activeCondition != null;
    public bool CanDebugClearChallenge => started && !complete && !gameOverTriggered && !advancing && HasActiveQuestion;
    public bool CanDebugClearCondition => CanDebugClearChallenge;

    public bool DebugClearCurrentCondition()
    {
        if (!CanDebugClearCondition) return false;
        // Use the same advancement as a validated Submit: this removes only the
        // current bank entry, regenerates the next condition's blocks, and also
        // handles the final condition's normal challenge-complete branch.
        StartCoroutine(Advance());
        return true;
    }

    public bool DebugClearCurrentChallenge()
    {
        if (!CanDebugClearChallenge) return false;
        // Advance() owns the normal completion message, block reset, book gate,
        // and final-stage event. Point its final-condition branch at the active
        // unanswered entry so the debug action cannot skip another challenge.
        selectedEntryIndices[CurrentConditionCount - 1] = unansweredEntries[0];
        conditionIndex = CurrentConditionCount - 1;
        StartCoroutine(Advance());
        return true;
    }

    private void OnEnable()
    {
        ActiveInstance = this;
        BindInput();
        if (submitButton != null)
        {
            submitButton.onClick.RemoveListener(Submit);
            submitButton.onClick.AddListener(Submit);
        }
        if (interactWithBookButton != null)
        {
            interactWithBookButton.onClick.RemoveListener(UseExistingInteraction);
            interactWithBookButton.onClick.AddListener(UseExistingInteraction);
        }
        if (pickUpBlockButton != null)
        {
            pickUpBlockButton.onClick.RemoveListener(UseExistingInteraction);
            pickUpBlockButton.onClick.AddListener(UseExistingInteraction);
        }
        if (returnBlockButton != null)
        {
            returnBlockButton.onClick.RemoveListener(ReturnHeldBlock);
            returnBlockButton.onClick.AddListener(ReturnHeldBlock);
        }
    }

    private void Start()
    {
        if (feedbackText != null)
        {
            feedbackDefaultFontSize = feedbackText.fontSize;
            feedbackDefaultSize = feedbackText.rectTransform.sizeDelta;
            feedbackDefaultPosition = feedbackText.rectTransform.anchoredPosition;
        }
        ConfigureTimerDisplay();
        ResetTimerForNextChallenge();
        aMinusBBounds = GetRendererBounds(aMinusBRegion);
        bMinusABounds = GetRendererBounds(bMinusARegion);
        RefreshDifferenceRegions();
        ClearQuestionSelection();
        if (!EnsureBlockPool()) { enabled = false; return; }
        BindInput();
        if (submitButton != null && submitButton.transform.parent != null)
            viewBoardRect = submitButton.transform.parent.Find("ViewBoardButton") as RectTransform;
        if (submitButton != null)
        {
            submitButton.onClick.RemoveListener(Submit);
            submitButton.onClick.AddListener(Submit);
            submitButton.gameObject.SetActive(false);
        }
        ConfigureContextButton(interactWithBookButton);
        ConfigureContextButton(pickUpBlockButton);
        ConfigureContextButton(returnBlockButton);
        if (interactWithBookButton != null) interactWithBookButton.onClick.AddListener(UseExistingInteraction);
        if (pickUpBlockButton != null) pickUpBlockButton.onClick.AddListener(UseExistingInteraction);
        if (returnBlockButton != null) returnBlockButton.onClick.AddListener(ReturnHeldBlock);
        if (reminderText != null) reminderText.gameObject.SetActive(false);

        Renderer[] diagramRenderers = diagram != null ? diagram.GetComponentsInChildren<Renderer>() : null;
        if (diagramRenderers != null && diagramRenderers.Length > 0)
        {
            diagramBounds = diagramRenderers[0].bounds;
            for (int i = 1; i < diagramRenderers.Length; i++) diagramBounds.Encapsulate(diagramRenderers[i].bounds);
        }
        diagramRenderer = diagram != null ? diagram.GetComponent<Renderer>() : null;
        diagramRendererOriginallyEnabled = diagramRenderer != null && diagramRenderer.enabled;
        diagramMeshFilter = diagram != null ? diagram.GetComponent<MeshFilter>() : null;
        originalDiagramMesh = diagramMeshFilter != null ? diagramMeshFilter.sharedMesh : null;
        if (diagramMeshFilter == null || existingACircleMesh == null || existingBCircleMesh == null)
            Debug.LogError("Setssss Challenge 5 needs both existing AB circle mesh views.", this);
        if (universalBoundary == null)
        {
            GameObject existingBoundary = GameObject.Find("BORDERLINES");
            if (existingBoundary != null) universalBoundary = existingBoundary.transform;
        }
        universalBounds = GetRendererBounds(universalBoundary != null ? universalBoundary.gameObject : null);
        if (universalBounds.extents.x <= 0f || universalBounds.extents.z <= 0f)
            Debug.LogError("Setssss Challenge 5 requires the existing BORDERLINES renderer as U.", this);
        CreateComplementVisuals();

        for (int i = 0; i < BlocksPerCondition; i++)
        {
            GrabbableObject block = numberedBlocks[i];
            spawnParents[i] = spawner;
            spawnPositions[i] = blockSpawnOffsets != null && blockSpawnOffsets.Length == BlocksPerCondition
                ? blockSpawnOffsets[i] : DefaultSpawnOffset(i);
            spawnRotations[i] = block.transform.localRotation;
            block.transform.SetParent(spawner, false);
            block.transform.localPosition = spawnPositions[i];
            if (block.GetComponent<SetssssNumberedBlock>() == null)
                block.gameObject.AddComponent<SetssssNumberedBlock>();
            SetBlockColliders(block, false);
            block.gameObject.SetActive(false); // No old question visible while waiting for the book.
        }
        if (feedbackText != null) feedbackText.text = string.Empty;
        RefreshComplementVisuals();
        UpdateBookPrompt();
        RefreshBoard();
    }

    private void ConfigureTimerDisplay()
    {
        if (timerText == null && submitButton != null && submitButton.transform.parent != null)
            timerText = submitButton.transform.parent.Find("Gameplay_Interface/TimerText")?.GetComponent<TMP_Text>();
        if (timerText == null)
        {
            Debug.LogError("Setssss needs Canvas 1's existing TimerText for its challenge countdown.", this);
            return;
        }
        RectTransform rect = timerText.rectTransform;
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(-145f, -82f);
        rect.sizeDelta = new Vector2(230f, 75f);
        timerText.alignment = TextAlignmentOptions.Center;
        timerText.fontSize = 54f;
        timerText.color = new Color(0.96f, 0.84f, 0.56f);
        timerText.outlineColor = new Color(0.08f, 0.07f, 0.09f);
        timerText.outlineWidth = 0.16f;
        timerText.raycastTarget = false;
        timerText.gameObject.SetActive(true);
    }

    private void ResetTimerForNextChallenge()
    {
        timeRemaining = challengeDurationSeconds;
        UpdateTimerDisplay();
    }

    private void UpdateTimerDisplay()
    {
        if (timerText == null) return;
        int seconds = Mathf.CeilToInt(Mathf.Max(0f, timeRemaining));
        if (seconds == displayedSeconds) return;
        displayedSeconds = seconds;
        timerText.text = string.Format("{0:00}:{1:00}", seconds / 60, seconds % 60);
    }

    private void TickTimer()
    {
        if (!started || complete || gameOverTriggered || Time.deltaTime <= 0f) return;
        timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
        UpdateTimerDisplay();
        if (timeRemaining <= 0f) TriggerGameOver();
    }

    private void ApplyIncorrectSubmissionPenalty()
    {
        if (gameOverTriggered) return;
        timeRemaining = Mathf.Max(0f, timeRemaining - incorrectSubmissionPenaltySeconds);
        UpdateTimerDisplay();
        if (timeRemaining <= 0f) TriggerGameOver();
    }

    private void TriggerGameOver()
    {
        if (gameOverTriggered || complete) return;
        gameOverTriggered = true;
        timeRemaining = 0f;
        UpdateTimerDisplay();
        StopAllCoroutines();
        RestoreFeedbackLayout();
        if (feedbackText != null) feedbackText.text = string.Empty;
        if (boardView != null && boardView.IsViewingBoard) boardView.Back();
        if (bookPrompt != null) bookPrompt.gameObject.SetActive(false);
        if (submitButton != null) submitButton.gameObject.SetActive(false);
        SetVisible(interactWithBookButton, false);
        SetVisible(pickUpBlockButton, false);
        SetVisible(returnBlockButton, false);
        if (reminderText != null) reminderText.gameObject.SetActive(false);
        if (input != null) input.SetGameplayInputBlocked(true);
        if (player != null) player.ToggleControl(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    private static SetsConditionData Condition(string a, string b)
    {
        return new SetsConditionData
        {
            setA = string.IsNullOrEmpty(a) ? Array.Empty<string>() : a.Split(','),
            setB = string.IsNullOrEmpty(b) ? Array.Empty<string>() : b.Split(',')
        };
    }

    private bool EnsureBlockPool()
    {
        if (spawner == null || numberedBlocks == null) return false;
        var pool = new List<GrabbableObject>();
        foreach (GrabbableObject block in numberedBlocks)
            if (block != null && !pool.Contains(block)) pool.Add(block);
        if (pool.Count == 0 || pool.Count > BlocksPerCondition)
        {
            Debug.LogError("Setssss needs a pool of exactly ten numbered blocks.", this);
            return false;
        }
        while (pool.Count < BlocksPerCondition)
        {
            GrabbableObject copy = Instantiate(pool[0], spawner);
            copy.name = "NumberBlock_" + (pool.Count + 1);
            pool.Add(copy);
        }
        numberedBlocks = pool.ToArray();
        return true;
    }

    private static Vector3 DefaultSpawnOffset(int index)
    {
        int column = index < 8 ? index % 4 : index - 7;
        return new Vector3((column - 1.5f) * 3.3f, 1.84f, -2.85f + (index / 4) * 3.6f);
    }

    private void ClearQuestionSelection()
    {
        unansweredEntries.Clear();
        activeCondition = null;
        activeComplement = null;
        for (int i = 0; i < selectedEntryIndices.Length; i++) selectedEntryIndices[i] = -1;
    }

    private void ShuffleUnansweredEntries()
    {
        unansweredEntries.Clear();
        for (int i = 0; i < CurrentConditionCount; i++) unansweredEntries.Add(i);
        for (int i = unansweredEntries.Count - 1; i > 0; i--)
        {
            int swap = UnityEngine.Random.Range(0, i + 1);
            int previous = unansweredEntries[i];
            unansweredEntries[i] = unansweredEntries[swap];
            unansweredEntries[swap] = previous;
        }
    }

    private SetsConditionData GetBankCondition(int index)
    {
        return challengeNumber == 1 ? challengeOneBank[index] :
            challengeNumber == 2 ? challengeTwoBank[index] :
            challengeNumber == 3 ? challengeThreeBank[index] : challengeFourBank[index];
    }

    private void DrawCondition()
    {
        ResetAllBlocks();
        if (unansweredEntries.Count == 0)
            throw new InvalidOperationException("No unanswered condition remains for this challenge.");

        // Draw only at a book start or after a correct answer. Wrong submissions
        // keep this question and every physical block placement unchanged.
        int entryIndex = unansweredEntries[0];
        activeComplement = challengeNumber == 5 ? challengeFiveBank[entryIndex] : null;
        activeCondition = challengeNumber == 5 ? null : GetBankCondition(entryIndex);
        selectedEntryIndices[conditionIndex] = entryIndex;
        PrepareBlocksForCondition();
        RefreshComplementVisuals();
        RefreshBoard();
    }

    private void PrepareBlocksForCondition()
    {
        if (challengeNumber == 5) { PrepareComplementBlocks(); return; }
        var required = new HashSet<int>();
        foreach (string value in activeCondition.setA) required.Add(int.Parse(value));
        foreach (string value in activeCondition.setB) required.Add(int.Parse(value));
        if (required.Count > BlocksPerCondition)
            throw new InvalidOperationException("A condition requires more than ten distinct elements.");

        var numbers = new List<int>(required);
        while (numbers.Count < BlocksPerCondition)
        {
            int distractor = UnityEngine.Random.Range(1, 100);
            if (required.Add(distractor)) numbers.Add(distractor);
        }
        for (int i = numbers.Count - 1; i > 0; i--)
        {
            int swap = UnityEngine.Random.Range(0, i + 1);
            int previous = numbers[i]; numbers[i] = numbers[swap]; numbers[swap] = previous;
        }
        for (int i = 0; i < BlocksPerCondition; i++)
        {
            GrabbableObject block = numberedBlocks[i];
            block.GetComponent<SetssssNumberedBlock>().SetValue(numbers[i]);
            block.gameObject.SetActive(true);
            SetBlockColliders(block, true);
            if (challengeNumber == 4)
            {
                challengeFourStates[block] = ChallengeFourBlockState.AtSpawner;
                challengeFourSpawnSlots[block] = i;
                challengeFourAvailableAtSlot[i] = block;
            }
        }
    }

    private void PrepareComplementBlocks()
    {
        if (activeComplement == null || universalBoundary == null || universalBounds.extents.x <= 0f)
            throw new InvalidOperationException("Challenge 5 needs BORDERLINES and a complement condition.");
        var selected = new HashSet<int>();
        foreach (string value in activeComplement.selected) selected.Add(int.Parse(value));
        var initiallyInside = new HashSet<int>(selected);
        if (selectedEntryIndices[conditionIndex] == 0)
        {
            // The first supplied condition specifically starts with 3 and 4
            // incorrectly inside A, alongside its correct members 1 and 2.
            initiallyInside.Add(3);
            initiallyInside.Add(4);
        }
        else
        {
            int firstSelected = int.Parse(activeComplement.selected[0]);
            initiallyInside.Remove(firstSelected);
            foreach (string value in activeComplement.universe)
                if (!selected.Contains(int.Parse(value))) { initiallyInside.Add(int.Parse(value)); break; }
        }

        int insideSlot = 0;
        int outsideSlot = 0;
        for (int i = 0; i < activeComplement.universe.Length; i++)
        {
            GrabbableObject block = numberedBlocks[i];
            int value = int.Parse(activeComplement.universe[i]);
            block.GetComponent<SetssssNumberedBlock>().SetValue(value);
            Rigidbody body = block.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
                body.useGravity = false;
            }
            bool inside = initiallyInside.Contains(value);
            block.transform.SetParent(universalBoundary, true);
            block.transform.position = ComplementPosition(inside, inside ? insideSlot++ : outsideSlot++);
            block.gameObject.SetActive(true);
            SetBlockColliders(block, true);
        }
        // Challenge 5 has exactly the nine values in U, not a tenth distractor.
        numberedBlocks[9].gameObject.SetActive(false);
        SetBlockColliders(numberedBlocks[9], false);
    }

    private Vector3 SelectedSetCenter()
    {
        float radius = SelectedSetRadius();
        float offset = diagramBounds.extents.x - radius;
        return new Vector3(diagramBounds.center.x + (activeComplement.setName == "A" ? -offset : offset),
            universalBounds.max.y + 0.9f, diagramBounds.center.z);
    }

    private float SelectedSetRadius()
    {
        return Mathf.Min(diagramBounds.extents.z, diagramBounds.extents.x * 0.61f);
    }

    private Vector3 ComplementPosition(bool inside, int slot)
    {
        float y = universalBounds.max.y + 0.9f;
        if (inside)
        {
            Vector3 center = SelectedSetCenter();
            float spacing = SelectedSetRadius() * 0.3f;
            return new Vector3(center.x + ((slot % 3) - 1) * spacing, y,
                center.z + ((slot / 3) - 1) * spacing);
        }
        float x = slot % 2 == 0
            ? universalBounds.min.x + universalBounds.extents.x * 0.2f
            : universalBounds.max.x - universalBounds.extents.x * 0.2f;
        float z = universalBounds.center.z + ((slot / 2) - 2) * universalBounds.extents.z * 0.3f;
        return new Vector3(x, y, z);
    }

    private bool IsInsideUniversal(Vector3 point)
    {
        return universalBounds.extents.x > 0f && universalBounds.extents.z > 0f &&
            point.x >= universalBounds.min.x + universalPlacementInset &&
            point.x <= universalBounds.max.x - universalPlacementInset &&
            point.z >= universalBounds.min.z + universalPlacementInset &&
            point.z <= universalBounds.max.z - universalPlacementInset;
    }

    private bool IsInsideSelectedSet(Vector3 point)
    {
        if (activeComplement == null) return false;
        if (!TryGetMainVennRegion(point, out SetZone region)) return false;
        return activeComplement.setName == "A"
            ? region == SetZone.A_ONLY || region == SetZone.INTERSECTION
            : region == SetZone.B_ONLY || region == SetZone.INTERSECTION;
    }

    private void SnapToComplement(GrabbableObject block, Vector3 playerPosition)
    {
        if (block == null) return;
        bool inside = IsInsideSelectedSet(playerPosition);
        int slot = 0;
        for (; slot < 9; slot++)
        {
            Vector3 candidate = ComplementPosition(inside, slot);
            bool occupied = false;
            for (int i = 0; i < activeComplement.universe.Length; i++)
            {
                GrabbableObject other = numberedBlocks[i];
                if (other == null || other == block || !other.gameObject.activeInHierarchy) continue;
                Vector3 delta = other.transform.position - candidate;
                delta.y = 0f;
                if (delta.sqrMagnitude < 6.25f) { occupied = true; break; }
            }
            if (!occupied) break;
        }
        Rigidbody body = block.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.useGravity = false;
        }
        block.transform.SetParent(universalBoundary, true);
        block.transform.position = slot < 9 ? ComplementPosition(inside, slot) :
            new Vector3(playerPosition.x, universalBounds.max.y + 0.9f, playerPosition.z);
    }

    private bool ValidateComplementArrangement()
    {
        if (activeComplement == null || player.GetHeldObject() != null) return false;
        var universe = new HashSet<int>();
        var selected = new HashSet<int>();
        var seen = new HashSet<int>();
        foreach (string value in activeComplement.universe) universe.Add(int.Parse(value));
        foreach (string value in activeComplement.selected) selected.Add(int.Parse(value));
        for (int i = 0; i < activeComplement.universe.Length; i++)
        {
            GrabbableObject block = numberedBlocks[i];
            if (block == null || !block.gameObject.activeInHierarchy) return false;
            int value = GetBlockNumber(block);
            Vector3 point = block.transform.position;
            if (!universe.Contains(value) || !seen.Add(value) || !IsInsideUniversal(point) ||
                IsInsideSelectedSet(point) != selected.Contains(value)) return false;
        }
        return seen.SetEquals(universe) && !numberedBlocks[9].gameObject.activeInHierarchy;
    }

    private void UpdateBookPrompt()
    {
        if (bookPrompt == null) return;
        bookPrompt.text = "Interact to start Challenge " + challengeNumber;
        bookPrompt.gameObject.SetActive(!started && !complete && !gameOverTriggered);
    }

    // The Game Over Retry button repeats the timed-out challenge; a deliberate
    // stage restart outside Game Over starts again at Challenge 1.
    public void RestartChallenge()
    {
        int restartAt = gameOverTriggered ? challengeNumber : 1;
        if (boardView != null && boardView.IsViewingBoard) boardView.Back();
        StopAllCoroutines();
        RestoreFeedbackLayout();
        ResetAllBlocks();
        started = false;
        advancing = false;
        complete = false;
        gameOverTriggered = false;
        conditionIndex = 0;
        lastInteractionTime = -10f;
        nextSubmitTime = 0f;
        challengeNumber = restartAt;
        ResetTimerForNextChallenge();
        RefreshDifferenceRegions();
        RefreshComplementVisuals();
        ClearQuestionSelection();
        foreach (GrabbableObject block in numberedBlocks)
            if (block != null) { SetBlockColliders(block, false); block.gameObject.SetActive(false); }
        UpdateBookPrompt();
        if (feedbackText != null) feedbackText.text = string.Empty;
        if (reminderText != null) reminderText.gameObject.SetActive(false);
        if (submitButton != null) submitButton.gameObject.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        Time.timeScale = 1f;
        if (input != null) input.SetGameplayInputBlocked(false);
        if (player != null) player.ToggleControl(true);
        RefreshBoard();
    }

    private void OnDisable()
    {
        if (ActiveInstance == this) ActiveInstance = null;
        ClearAllErrorIndicators();
        if (diagramMeshFilter != null) diagramMeshFilter.sharedMesh = originalDiagramMesh;
        if (diagramRenderer != null) diagramRenderer.enabled = diagramRendererOriginallyEnabled;
        if (universalLabel != null) universalLabel.gameObject.SetActive(false);
        if (submitButton != null) submitButton.onClick.RemoveListener(Submit);
        if (interactWithBookButton != null) interactWithBookButton.onClick.RemoveListener(UseExistingInteraction);
        if (pickUpBlockButton != null) pickUpBlockButton.onClick.RemoveListener(UseExistingInteraction);
        if (returnBlockButton != null) returnBlockButton.onClick.RemoveListener(ReturnHeldBlock);
    }

    private void OnDestroy()
    {
        if (errorIndicatorMaterial != null) Destroy(errorIndicatorMaterial);
    }

    private static void ConfigureContextButton(Button button)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.gameObject.SetActive(false);
    }

    private void BindInput()
    {
        GameInput active = GameInput.Instance != null ? GameInput.Instance : FindFirstObjectByType<GameInput>();
        input = active;
    }

    private void UseExistingInteraction()
    {
        if (input != null) input.MobileInteract();
    }

    // Called by Player's existing E/hand interaction callback so every input
    // follows the same target selection and pickup/drop path in Setssss.
    public bool TryHandleInteraction()
    {
        if (complete || gameOverTriggered || advancing) return true;
        if (input == null || input.GameplayInputBlocked ||
            boardView != null && boardView.IsViewingBoard) return false;
        if (player == null) player = Player.LocalInstance;
        if (player == null) return false;

        GrabbableObject held = player.GetHeldObject();
        bool relevant = GetBlockNumber(held) > 0 || IsBookTargeted() ||
                        started && FindTargetedBlock() != null;
        if (!relevant) return false;
        if (Time.unscaledTime - lastInteractionTime < 0.2f) return true;
        lastInteractionTime = Time.unscaledTime;

        int heldNumber = GetBlockNumber(held);
        if (heldNumber > 0)
        {
            held.Drop();
            player.SetHeldObjectSilently(null);
            PlaceOrReturnBlock(held, heldNumber);
            previouslyHeld = null;
            return true;
        }

        if (IsBookTargeted())
        {
            if (!started)
            {
                started = true;
                RefreshDifferenceRegions();
                if (bookPrompt != null) bookPrompt.gameObject.SetActive(false);
                ShuffleUnansweredEntries();
                DrawCondition();
                StartCoroutine(ShowChallengeStartFeedback());
            }
            return true;
        }

        GrabbableObject target = FindTargetedBlock();
        if (target == null || !started) return false;
        if (challengeNumber == 4)
        {
            if (!challengeFourStates.TryGetValue(target, out ChallengeFourBlockState state) ||
                state == ChallengeFourBlockState.Held) return true;
            // Every actual spawner pickup, including a replacement pickup,
            // creates exactly one successor at the same numbered slot.
            if (state == ChallengeFourBlockState.AtSpawner && !ReplaceSpawnerPickup(target)) return true;
            challengeFourPlacements.Remove(target);
            challengeFourSlots.Remove(target);
            challengeFourStates[target] = ChallengeFourBlockState.Held;
        }
        int number = GetBlockNumber(target);
        ClearErrorIndicator(target);
        if (challengeNumber != 4)
        {
            placed.Remove(number.ToString());
            placementSlots.Remove(number.ToString());
        }
        target.Grab(player.GetHoldPoint());
        player.SetHeldObjectSilently(target);
        previouslyHeld = target;
        return true;
    }

    private bool ReplaceSpawnerPickup(GrabbableObject picked)
    {
        int slot = challengeFourSpawnSlots[picked];
        if (challengeFourAvailableAtSlot[slot] != picked) return false;
        GrabbableObject replacement = Instantiate(picked, spawner);
        replacement.name = "NumberBlock_" + (slot + 1) + "_Replacement";
        replacement.transform.localPosition = spawnPositions[slot];
        replacement.transform.localRotation = spawnRotations[slot];
        replacement.GetComponent<SetssssNumberedBlock>().SetValue(GetBlockNumber(picked));
        Rigidbody body = replacement.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.useGravity = false;
        }
        replacement.isStoredInInventory = false;
        replacement.gameObject.SetActive(true);
        SetBlockColliders(replacement, true);
        challengeFourCopies.Add(replacement);
        challengeFourStates[replacement] = ChallengeFourBlockState.AtSpawner;
        challengeFourSpawnSlots[replacement] = slot;
        challengeFourAvailableAtSlot[slot] = replacement;
        return true;
    }

    private bool IsBookTargeted()
    {
        return bookStatue != null && IsFacingTarget(bookStatue.position, bookInteractionRange, 0.45f);
    }

    private GrabbableObject FindTargetedBlock()
    {
        if (!started || player == null || player.GetHeldObject() != null) return null;
        GrabbableObject best = null;
        float bestScore = float.MaxValue;
        foreach (GrabbableObject block in InteractableBlocks())
        {
            if (block == null || !block.gameObject.activeInHierarchy || block.isStoredInInventory ||
                !IsFacingTarget(block.transform.position, blockInteractionRange, 0.5f)) continue;
            if (challengeNumber == 4)
            {
                if (!challengeFourStates.TryGetValue(block, out ChallengeFourBlockState state) ||
                    state == ChallengeFourBlockState.Held) continue;
                if (state == ChallengeFourBlockState.AtSpawner &&
                    (!challengeFourSpawnSlots.TryGetValue(block, out int slot) ||
                     challengeFourAvailableAtSlot[slot] != block)) continue;
            }
            Collider collider = block.GetComponent<Collider>();
            if (collider == null || !collider.enabled) continue;
            Vector3 delta = block.transform.position - player.transform.position;
            float score = delta.sqrMagnitude;
            if (score < bestScore) { best = block; bestScore = score; }
        }
        return best;
    }

    private IEnumerable<GrabbableObject> InteractableBlocks()
    {
        foreach (GrabbableObject block in numberedBlocks) yield return block;
        if (challengeNumber == 4)
            foreach (GrabbableObject block in challengeFourCopies) yield return block;
    }

    private bool IsFacingTarget(Vector3 target, float range, float minimumDot)
    {
        if (player == null) return false;
        Vector3 delta = target - player.transform.position;
        if (Mathf.Abs(delta.y) > 3f) return false;
        delta.y = 0f;
        if (delta.sqrMagnitude > range * range || delta.sqrMagnitude < 0.01f) return false;
        Vector3 forward = player.transform.forward;
        forward.y = 0f;
        return Vector3.Dot(forward.normalized, delta.normalized) >= minimumDot;
    }

    public void ReturnHeldBlock()
    {
        if (!started || complete || gameOverTriggered || advancing || input == null || input.GameplayInputBlocked || player == null) return;
        if (boardView != null && boardView.IsViewingBoard) return;
        GrabbableObject held = player.GetHeldObject();
        int number = GetBlockNumber(held);
        if (number < 1) return;
        held.Drop();
        player.SetHeldObjectSilently(null);
        ReturnOrRecycleBlock(held);
        previouslyHeld = null;
        if (returnBlockButton != null) returnBlockButton.gameObject.SetActive(false);
    }

    private void Update()
    {
        TickTimer();
        AdjustControlLayout();
        if (input == null) BindInput();
        if (player == null) player = Player.LocalInstance;
        if (player == null) return;

        if (bookPrompt != null && bookPrompt.gameObject.activeSelf && Camera.main != null)
            FaceCamera(bookPrompt.transform);
        if (universalLabel != null && universalLabel.gameObject.activeSelf && Camera.main != null)
            FaceCamera(universalLabel.transform);

        bool nearDiagram = started && !complete && !gameOverTriggered && !advancing && IsNearPlacementArea(player.transform.position);
        bool controlsAvailable = !gameOverTriggered && input != null && !input.GameplayInputBlocked &&
                                 (boardView == null || !boardView.IsViewingBoard);
        if (submitButton != null)
        {
            bool show = nearDiagram && controlsAvailable;
            if (submitButton.gameObject.activeSelf != show) submitButton.gameObject.SetActive(show);
        }

        GrabbableObject currentHeld = player.GetHeldObject();
        bool holdingNumber = GetBlockNumber(currentHeld) > 0;
        SetVisible(interactWithBookButton, controlsAvailable && !started && !complete && IsBookTargeted());
        SetVisible(pickUpBlockButton, controlsAvailable && started && !complete && !advancing &&
                                      !holdingNumber && FindTargetedBlock() != null);
        SetVisible(returnBlockButton, controlsAvailable && started && !complete && !advancing && holdingNumber);
        if (reminderText != null)
        {
            bool showReminder = started && !complete && !gameOverTriggered && IsPastBoard();
            if (reminderText.gameObject.activeSelf != showReminder) reminderText.gameObject.SetActive(showReminder);
        }

        if (!started || complete || gameOverTriggered || advancing) return;
        GrabbableObject held = player.GetHeldObject();
        if (held != null)
        {
            ClearErrorIndicator(held);
            int number = GetBlockNumber(held);
            if (number > 0)
            {
                if (challengeNumber == 4)
                {
                    challengeFourPlacements.Remove(held);
                    challengeFourSlots.Remove(held);
                }
                else
                {
                    placed.Remove(number.ToString());
                    placedOnDifferenceRegion.Remove(number.ToString());
                    placementSlots.Remove(number.ToString());
                }
            }
        }
        if (previouslyHeld != null && held != previouslyHeld)
        {
            int number = GetBlockNumber(previouslyHeld);
            if (number > 0)
            {
                PlaceOrReturnBlock(previouslyHeld, number);
            }
        }
        previouslyHeld = held != null && GetBlockNumber(held) > 0 ? held : null;
    }

    // Landscape tablets have less room between the anchored joystick and hand
    // control than phones. Lift the center actions above those controls there.
    private void AdjustControlLayout()
    {
        float safeHeight = Mathf.Max(1f, Screen.safeArea.height);
        bool narrow = Screen.safeArea.width / safeHeight < 1.6f;
        Vector2 viewPosition = narrow ? new Vector2(-175f, 610f) : new Vector2(-175f, 38f);
        Vector2 submitPosition = narrow ? new Vector2(105f, 610f) : new Vector2(105f, 38f);
        Vector2 contextPosition = narrow ? new Vector2(0f, 745f) : new Vector2(0f, 165f);
        if (viewBoardRect != null && viewBoardRect.anchoredPosition != viewPosition)
            viewBoardRect.anchoredPosition = viewPosition;
        if (submitButton != null)
        {
            RectTransform rect = submitButton.GetComponent<RectTransform>();
            if (rect.anchoredPosition != submitPosition) rect.anchoredPosition = submitPosition;
        }
        SetContextPosition(interactWithBookButton, contextPosition);
        SetContextPosition(pickUpBlockButton, contextPosition);
        SetContextPosition(returnBlockButton, contextPosition);
    }

    private static void SetContextPosition(Button button, Vector2 position)
    {
        if (button == null) return;
        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect.anchoredPosition != position) rect.anchoredPosition = position;
    }

    private static void SetVisible(Button button, bool visible)
    {
        if (button != null && button.gameObject.activeSelf != visible) button.gameObject.SetActive(visible);
    }

    private bool IsPastBoard()
    {
        if (boardText == null || spawner == null || diagram == null || player == null) return false;
        Vector3 direction = diagram.position - spawner.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) return false;
        Vector3 offset = player.transform.position - boardText.transform.position;
        offset.y = 0f;
        return Vector3.Dot(offset, direction.normalized) > reminderPastBoardDistance;
    }

    private bool IsNearDiagram(Vector3 point)
    {
        return diagram != null &&
               point.x >= diagramBounds.min.x - diagramInteractionRange && point.x <= diagramBounds.max.x + diagramInteractionRange &&
               point.z >= diagramBounds.min.z - diagramInteractionRange && point.z <= diagramBounds.max.z + diagramInteractionRange;
    }

    private static Bounds GetRendererBounds(GameObject region)
    {
        if (region == null) return default;
        Renderer[] renderers = region.GetComponents<Renderer>();
        if (renderers.Length == 0) return default;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private void RefreshDifferenceRegions()
    {
        bool available = started && !complete && challengeNumber == 4;
        if (aMinusBRegion != null) aMinusBRegion.SetActive(available);
        if (bMinusARegion != null) bMinusARegion.SetActive(available);
    }

    private void CreateComplementVisuals()
    {
        if (universalBoundary == null || universalLabel != null) return;
        // BORDERLINES remains the existing U boundary. The AB GameObject itself
        // displays one of its original circles; no new circle is instantiated.
        universalLabel = CreateComplementLabel("Universal Set U", "U", new Color(0.4f, 0.95f, 0.95f));
    }

    private TextMeshPro CreateComplementLabel(string objectName, string value, Color color)
    {
        GameObject labelObject = new GameObject(objectName);
        labelObject.transform.SetParent(universalBoundary, false);
        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        if (boardText != null) label.font = boardText.font;
        label.text = value;
        label.fontSize = 6f;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta = new Vector2(9f, 5f);
        labelObject.SetActive(false);
        return label;
    }

    private void RefreshComplementVisuals()
    {
        bool visible = started && !complete && challengeNumber == 5 && activeComplement != null;
        if (diagramMeshFilter != null)
            diagramMeshFilter.sharedMesh = visible
                ? (activeComplement.setName == "A" ? existingACircleMesh : existingBCircleMesh)
                : originalDiagramMesh;
        if (diagramRenderer != null) diagramRenderer.enabled = diagramRendererOriginallyEnabled;
        if (universalLabel != null) universalLabel.gameObject.SetActive(visible);
        if (!visible) return;
        universalLabel.transform.position = new Vector3(universalBounds.min.x + 5f,
            universalBounds.max.y + 2.6f, universalBounds.min.z + 4f);
    }

    private bool IsNearPlacementArea(Vector3 point)
    {
        if (started && challengeNumber == 5) return IsWithinRegion(point, universalBounds);
        return IsNearDiagram(point) ||
               started && challengeNumber == 4 &&
               (IsWithinRegion(point, aMinusBBounds) || IsWithinRegion(point, bMinusABounds));
    }

    private bool IsWithinRegion(Vector3 point, Bounds bounds)
    {
        return bounds.size.x > 0f && bounds.size.z > 0f &&
               point.x >= bounds.min.x - diagramInteractionRange && point.x <= bounds.max.x + diagramInteractionRange &&
               point.z >= bounds.min.z - diagramInteractionRange && point.z <= bounds.max.z + diagramInteractionRange;
    }

    private static bool IsInsideVisibleRegion(Vector3 point, Bounds bounds)
    {
        if (bounds.extents.x <= 0f || bounds.extents.z <= 0f) return false;
        float x = (point.x - bounds.center.x) / bounds.extents.x;
        float z = (point.z - bounds.center.z) / bounds.extents.z;
        return x * x + z * z <= 1f;
    }

    private bool TryGetMainVennRegion(Vector3 point, out SetZone region)
    {
        region = SetZone.OUTSIDE;
        if (diagram == null || diagramBounds.extents.x <= 0f || diagramBounds.extents.z <= 0f)
            return false;
        // The visible AB mesh contains two overlapping circular areas. Checking
        // both circles also keeps the intersection from being swallowed by A or B.
        float radius = Mathf.Min(diagramBounds.extents.z, diagramBounds.extents.x * 0.61f);
        float centerOffset = diagramBounds.extents.x - radius;
        float x = point.x - diagramBounds.center.x;
        float z = point.z - diagramBounds.center.z;
        bool inA = (x + centerOffset) * (x + centerOffset) + z * z <= radius * radius;
        bool inB = (x - centerOffset) * (x - centerOffset) + z * z <= radius * radius;
        if (inA && inB) region = SetZone.INTERSECTION;
        else if (inA) region = SetZone.A_ONLY;
        else if (inB) region = SetZone.B_ONLY;
        return region != SetZone.OUTSIDE;
    }

    private bool TryGetRegion(Vector3 point, out SetZone zone, out Bounds surface)
    {
        zone = SetZone.OUTSIDE;
        surface = diagramBounds;
        if (started && challengeNumber == 4)
        {
            if (IsInsideVisibleRegion(point, aMinusBBounds)) { zone = SetZone.A_ONLY; surface = aMinusBBounds; return true; }
            if (IsInsideVisibleRegion(point, bMinusABounds)) { zone = SetZone.B_ONLY; surface = bMinusABounds; return true; }
            return TryGetMainVennRegion(point, out zone);
        }
        if (!IsNearDiagram(point)) return false;
        float offset = point.x - diagramBounds.center.x;
        float intersectionHalfWidth = diagramBounds.extents.x * 0.13f;
        if (offset < -intersectionHalfWidth) { zone = SetZone.A_ONLY; return true; }
        if (offset > intersectionHalfWidth) { zone = SetZone.B_ONLY; return true; }
        if (challengeNumber >= 3) { zone = SetZone.INTERSECTION; return true; }
        return false; // The first two challenge banks have no intersection membership.
    }

    private bool TryGetChallengeFourRegion(Vector3 point, out ChallengeFourRegion region, out Bounds surface)
    {
        region = ChallengeFourRegion.Intersection;
        surface = diagramBounds;
        // Test the dedicated regions first; they can sit near the main board.
        if (IsInsideVisibleRegion(point, aMinusBBounds))
        {
            region = ChallengeFourRegion.AMinusB;
            surface = aMinusBBounds;
            return true;
        }
        if (IsInsideVisibleRegion(point, bMinusABounds))
        {
            region = ChallengeFourRegion.BMinusA;
            surface = bMinusABounds;
            return true;
        }
        if (!TryGetMainVennRegion(point, out SetZone main)) return false;
        region = main == SetZone.A_ONLY ? ChallengeFourRegion.A :
            main == SetZone.B_ONLY ? ChallengeFourRegion.B : ChallengeFourRegion.Intersection;
        return true;
    }

    private void PlaceOrReturnBlock(GrabbableObject block, int number)
    {
        if (challengeNumber == 5)
        {
            if (IsInsideUniversal(player.transform.position)) SnapToComplement(block, player.transform.position);
            else ReturnBlock(GetBlockIndex(block));
        }
        else if (challengeNumber == 4)
        {
            if (TryGetChallengeFourRegion(player.transform.position, out ChallengeFourRegion region, out Bounds surface))
                SnapToChallengeFourRegion(block, region, surface);
            else ReturnOrRecycleBlock(block);
        }
        else if (TryGetRegion(player.transform.position, out SetZone zone, out Bounds surface))
            SnapToDiagram(block, number, zone, surface);
        else ReturnOrRecycleBlock(block);
    }

    private void SnapToChallengeFourRegion(GrabbableObject block, ChallengeFourRegion region, Bounds surface)
    {
        if (block == null) return;
        Rigidbody body = block.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.useGravity = false;
        }
        challengeFourPlacements.Remove(block);
        challengeFourSlots.Remove(block);
        var occupied = new HashSet<int>();
        foreach (var pair in challengeFourPlacements)
            if (pair.Value == region && challengeFourSlots.TryGetValue(pair.Key, out int used)) occupied.Add(used);
        int slot = 0;
        while (occupied.Contains(slot)) slot++;
        float side = region == ChallengeFourRegion.A ? -1f : region == ChallengeFourRegion.B ? 1f : 0f;
        bool difference = region == ChallengeFourRegion.AMinusB || region == ChallengeFourRegion.BMinusA;
        float x = surface.center.x + (difference ? 0f : side * surface.extents.x * 0.32f) + ((slot % 3) - 1) * 1.9f;
        float z = surface.center.z + ((slot / 3) - 1) * 1.9f;
        block.transform.position = new Vector3(x, surface.max.y + 0.85f, z);
        challengeFourPlacements[block] = region;
        challengeFourSlots[block] = slot;
        challengeFourStates[block] = ChallengeFourBlockState.Placed;
    }

    private void SnapToDiagram(GrabbableObject block, int number, SetZone zone, Bounds surface)
    {
        if (block == null || diagram == null) return;
        Rigidbody body = block.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.useGravity = false;
        }
        string key = number.ToString();
        placed.Remove(key);
        placedOnDifferenceRegion.Remove(key);
        placementSlots.Remove(key);
        var occupied = new HashSet<int>();
        foreach (var pair in placed)
            if (pair.Value == zone && placementSlots.TryGetValue(pair.Key, out int usedSlot)) occupied.Add(usedSlot);
        int slot = 0;
        while (occupied.Contains(slot)) slot++;
        float side = zone == SetZone.A_ONLY ? -1f : zone == SetZone.B_ONLY ? 1f : 0f;
        bool differenceSurface = surface.center != diagramBounds.center;
        float x = surface.center.x + (differenceSurface ? 0f : side * surface.extents.x * 0.32f) + ((slot % 3) - 1) * 1.9f;
        float z = surface.center.z + ((slot / 3) - 1) * 1.9f;
        block.transform.position = new Vector3(x, surface.max.y + 0.85f, z);
        placed[key] = zone;
        placedOnDifferenceRegion[key] = differenceSurface;
        placementSlots[key] = slot;
    }

    private bool ValidateFiveRegionPlacements()
    {
        if (activeCondition == null) return false;
        var expected = ExpectedFiveRegionValues();
        var seen = new Dictionary<ChallengeFourRegion, HashSet<int>>();
        foreach (ChallengeFourRegion region in expected.Keys) seen[region] = new HashSet<int>();
        foreach (var placement in challengeFourPlacements)
        {
            int number = GetBlockNumber(placement.Key);
            if (number < 1 || !expected[placement.Value].Contains(number) || !seen[placement.Value].Add(number))
                return false;
        }
        foreach (var pair in expected)
            if (!seen[pair.Key].SetEquals(pair.Value)) return false;
        return true;
    }

    private Dictionary<ChallengeFourRegion, HashSet<int>> ExpectedFiveRegionValues()
    {
        var a = new HashSet<int>();
        var b = new HashSet<int>();
        foreach (string value in activeCondition.setA) a.Add(int.Parse(value));
        foreach (string value in activeCondition.setB) b.Add(int.Parse(value));
        var expected = new Dictionary<ChallengeFourRegion, HashSet<int>>
        {
            { ChallengeFourRegion.A, a },
            { ChallengeFourRegion.B, b },
            { ChallengeFourRegion.Intersection, new HashSet<int>(a) },
            { ChallengeFourRegion.AMinusB, new HashSet<int>(a) },
            { ChallengeFourRegion.BMinusA, new HashSet<int>(b) }
        };
        expected[ChallengeFourRegion.Intersection].IntersectWith(b);
        expected[ChallengeFourRegion.AMinusB].ExceptWith(b);
        expected[ChallengeFourRegion.BMinusA].ExceptWith(a);
        return expected;
    }

    private PlacementReview ReviewIncorrectPlacement()
    {
        var review = new PlacementReview();
        if (challengeNumber == 5)
        {
            var universe = new HashSet<int>();
            var selected = new HashSet<int>();
            foreach (string value in activeComplement.universe) universe.Add(int.Parse(value));
            foreach (string value in activeComplement.selected) selected.Add(int.Parse(value));
            var firstByValue = new Dictionary<int, GrabbableObject>();
            foreach (GrabbableObject block in numberedBlocks)
            {
                if (block == null || !block.gameObject.activeInHierarchy) continue;
                if (block == numberedBlocks[9]) review.incorrectBlocks.Add(block);
                int value = GetBlockNumber(block);
                if (firstByValue.TryGetValue(value, out GrabbableObject first))
                {
                    review.incorrectBlocks.Add(first);
                    review.incorrectBlocks.Add(block);
                }
                else firstByValue.Add(value, block);
                Vector3 position = block.transform.position;
                if (!universe.Contains(value) || !IsInsideUniversal(position) ||
                    IsInsideSelectedSet(position) != selected.Contains(value))
                    review.incorrectBlocks.Add(block);
            }
            foreach (int value in universe)
                if (!firstByValue.ContainsKey(value)) review.missingRequired = true;
            return review;
        }

        if (challengeNumber == 4)
        {
            var expected = ExpectedFiveRegionValues();
            var firstByRegion = new Dictionary<ChallengeFourRegion, Dictionary<int, GrabbableObject>>();
            foreach (ChallengeFourRegion region in expected.Keys)
                firstByRegion.Add(region, new Dictionary<int, GrabbableObject>());
            foreach (var placement in challengeFourPlacements)
            {
                GrabbableObject block = placement.Key;
                if (block == null || !block.gameObject.activeInHierarchy) continue;
                int value = GetBlockNumber(block);
                var first = firstByRegion[placement.Value];
                if (first.TryGetValue(value, out GrabbableObject earlier))
                {
                    review.incorrectBlocks.Add(earlier);
                    review.incorrectBlocks.Add(block);
                }
                else first.Add(value, block);
                if (!expected[placement.Value].Contains(value)) review.incorrectBlocks.Add(block);
            }
            foreach (var pair in expected)
                foreach (int value in pair.Value)
                    if (!firstByRegion[pair.Key].ContainsKey(value)) review.missingRequired = true;
            return review;
        }

        var expectedZones = new Dictionary<int, SetZone>();
        var bValues = new HashSet<string>(activeCondition.setB);
        foreach (string value in activeCondition.setA)
            expectedZones[int.Parse(value)] = bValues.Contains(value) ? SetZone.INTERSECTION : SetZone.A_ONLY;
        foreach (string value in activeCondition.setB)
            if (!expectedZones.ContainsKey(int.Parse(value))) expectedZones[int.Parse(value)] = SetZone.B_ONLY;
        foreach (GrabbableObject block in numberedBlocks)
        {
            if (block == null || !block.gameObject.activeInHierarchy) continue;
            int value = GetBlockNumber(block);
            if (placed.TryGetValue(value.ToString(), out SetZone zone) &&
                (!expectedZones.TryGetValue(value, out SetZone expectedZone) || zone != expectedZone))
                review.incorrectBlocks.Add(block);
        }
        foreach (int value in expectedZones.Keys)
            if (!placed.ContainsKey(value.ToString())) review.missingRequired = true;
        return review;
    }

    private void ShowPlacementErrors(PlacementReview review)
    {
        foreach (LineRenderer line in errorIndicators.Values)
            if (line != null) line.gameObject.SetActive(false);
        foreach (GrabbableObject block in review.incorrectBlocks)
        {
            if (block == null) continue;
            if (!errorIndicators.TryGetValue(block, out LineRenderer line) || line == null)
            {
                GameObject marker = new GameObject("Incorrect Placement Outline");
                marker.transform.SetParent(block.transform, false);
                line = marker.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.loop = true;
                line.positionCount = 4;
                line.startWidth = 0.16f;
                line.endWidth = 0.16f;
                line.startColor = new Color(1f, 0.12f, 0.12f);
                line.endColor = line.startColor;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                if (errorIndicatorMaterial == null)
                {
                    Shader shader = Shader.Find("Sprites/Default");
                    if (shader != null) errorIndicatorMaterial = new Material(shader);
                }
                if (errorIndicatorMaterial != null) line.sharedMaterial = errorIndicatorMaterial;
                MeshFilter mesh = block.GetComponent<MeshFilter>();
                Bounds bounds = mesh != null && mesh.sharedMesh != null ? mesh.sharedMesh.bounds :
                    new Bounds(Vector3.zero, Vector3.one * 1.4f);
                float x = bounds.extents.x + 0.18f;
                float z = bounds.extents.z + 0.18f;
                float y = bounds.max.y + 0.08f;
                line.SetPosition(0, new Vector3(bounds.center.x - x, y, bounds.center.z - z));
                line.SetPosition(1, new Vector3(bounds.center.x + x, y, bounds.center.z - z));
                line.SetPosition(2, new Vector3(bounds.center.x + x, y, bounds.center.z + z));
                line.SetPosition(3, new Vector3(bounds.center.x - x, y, bounds.center.z + z));
                errorIndicators[block] = line;
            }
            line.gameObject.SetActive(true);
        }
    }

    private void ClearErrorIndicator(GrabbableObject block)
    {
        if (block != null && errorIndicators.TryGetValue(block, out LineRenderer line) && line != null)
            line.gameObject.SetActive(false);
    }

    private void ClearAllErrorIndicators()
    {
        foreach (LineRenderer line in errorIndicators.Values)
        {
            if (line == null) continue;
            line.gameObject.SetActive(false);
            Destroy(line.gameObject);
        }
        errorIndicators.Clear();
    }

    public void Submit()
    {
        if (!started || complete || gameOverTriggered || advancing || !HasActiveQuestion || player == null || input == null || input.GameplayInputBlocked ||
            !IsNearPlacementArea(player.transform.position)) return;
        if (Time.unscaledTime < nextSubmitTime) return;
        nextSubmitTime = Time.unscaledTime + 0.25f;
        if (player.GetHeldObject() != null) { StartCoroutine(ShowFeedback("Place your block first.", Color.red, 2f)); return; }

        var entries = new List<KeyValuePair<string, SetZone>>();
        foreach (var pair in placed) entries.Add(pair);
        bool correct = challengeNumber == 5 ? ValidateComplementArrangement() :
            challengeNumber == 4 ? ValidateFiveRegionPlacements() :
            SetsStageManager.ValidateConditionPlacements(activeCondition, entries);
        if (!correct)
        {
            PlacementReview review = ReviewIncorrectPlacement();
            ShowPlacementErrors(review);
            ApplyIncorrectSubmissionPenalty();
            if (gameOverTriggered) return;
            string message = "Incorrect. Fix the highlighted blocks and submit again.";
            if (review.missingRequired) message += "\nSome required elements are missing.";
            message += "\n-10s";
            StartCoroutine(ShowFeedback(message, new Color(1f, 0.35f, 0.35f), 2.8f));
            return;
        }
        ClearAllErrorIndicators();
        RestoreFeedbackLayout();
        StartCoroutine(Advance());
    }

    private IEnumerator Advance()
    {
        advancing = true;
        if (submitButton != null) submitButton.gameObject.SetActive(false);
        int answeredEntry = selectedEntryIndices[conditionIndex];
        if (!unansweredEntries.Remove(answeredEntry))
            throw new InvalidOperationException("The completed condition was not in the unanswered pool.");
        if (conditionIndex == CurrentConditionCount - 1)
        {
            ResetAllBlocks();
            activeCondition = null;
            activeComplement = null;
            if (challengeNumber < 5)
            {
                int finishedChallenge = challengeNumber++;
                conditionIndex = 0;
                ClearQuestionSelection();
                started = false;
                ResetTimerForNextChallenge();
                RefreshDifferenceRegions();
                RefreshComplementVisuals();
                advancing = false;
                foreach (GrabbableObject block in numberedBlocks)
                    if (block != null) { SetBlockColliders(block, false); block.gameObject.SetActive(false); }
                UpdateBookPrompt();
                RefreshBoard();
                if (feedbackText != null)
                {
                    feedbackText.text = "Challenge " + finishedChallenge + " Complete! Interact with the Book Statue to start Challenge " + challengeNumber + ".";
                    feedbackText.color = Color.green;
                }
                yield break;
            }
            complete = true;
            RefreshDifferenceRegions();
            RefreshComplementVisuals();
            RefreshBoard();
            if (feedbackText != null) { feedbackText.text = "Challenge 5 Complete!"; feedbackText.color = Color.green; }
            onChallengeCompleted?.Invoke();
            yield break;
        }
        if (feedbackText != null) { feedbackText.text = "Correct!"; feedbackText.color = Color.green; }
        yield return new WaitForSeconds(1.2f);
        conditionIndex++;
        DrawCondition();
        advancing = false;
        if (feedbackText != null) feedbackText.text = string.Empty;
    }

    private void ResetAllBlocks()
    {
        ClearAllErrorIndicators();
        if (player != null && player.GetHeldObject() != null && GetBlockNumber(player.GetHeldObject()) > 0)
        {
            player.GetHeldObject().Drop();
            player.SetHeldObjectSilently(null);
        }
        foreach (GrabbableObject copy in challengeFourCopies)
            if (copy != null) { copy.gameObject.SetActive(false); Destroy(copy.gameObject); }
        challengeFourCopies.Clear();
        challengeFourStates.Clear();
        challengeFourSpawnSlots.Clear();
        Array.Clear(challengeFourAvailableAtSlot, 0, challengeFourAvailableAtSlot.Length);
        challengeFourPlacements.Clear();
        challengeFourSlots.Clear();
        for (int i = 0; i < numberedBlocks.Length && i < BlocksPerCondition; i++) ReturnBlock(i);
        previouslyHeld = null;
        placed.Clear();
        placedOnDifferenceRegion.Clear();
        placementSlots.Clear();
    }

    private void ReturnOrRecycleBlock(GrabbableObject block)
    {
        if (block == null) return;
        if (challengeNumber == 4 && challengeFourSpawnSlots.TryGetValue(block, out int slot))
        {
            challengeFourPlacements.Remove(block);
            challengeFourSlots.Remove(block);
            // A successor normally occupies this slot. Retire the returned
            // instance instead of stacking it on top of that successor.
            if (challengeFourAvailableAtSlot[slot] != null && challengeFourAvailableAtSlot[slot] != block)
            {
                challengeFourStates.Remove(block);
                challengeFourSpawnSlots.Remove(block);
                block.gameObject.SetActive(false);
                if (challengeFourCopies.Remove(block)) Destroy(block.gameObject);
                return;
            }
            Rigidbody body = block.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
                body.useGravity = false;
            }
            block.transform.SetParent(spawner, false);
            block.transform.localPosition = spawnPositions[slot];
            block.transform.localRotation = spawnRotations[slot];
            block.isStoredInInventory = false;
            block.gameObject.SetActive(true);
            SetBlockColliders(block, true);
            challengeFourStates[block] = ChallengeFourBlockState.AtSpawner;
            challengeFourAvailableAtSlot[slot] = block;
        }
        else ReturnBlock(GetBlockIndex(block));
    }

    private void ReturnBlock(int index)
    {
        if (index < 0 || index >= numberedBlocks.Length || numberedBlocks[index] == null) return;
        GrabbableObject block = numberedBlocks[index];
        Rigidbody body = block.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.useGravity = false;
        }
        block.transform.SetParent(spawnParents[index] != null ? spawnParents[index] : spawner, false);
        block.transform.localPosition = spawnPositions[index];
        block.transform.localRotation = spawnRotations[index];
        block.isStoredInInventory = false;
        block.gameObject.SetActive(true);
        SetBlockColliders(block, true);
        int value = GetBlockNumber(block);
        if (value > 0)
        {
            placed.Remove(value.ToString());
            placedOnDifferenceRegion.Remove(value.ToString());
            placementSlots.Remove(value.ToString());
        }
    }

    private static void SetBlockColliders(GrabbableObject block, bool enabled)
    {
        foreach (Collider collider in block.GetComponentsInChildren<Collider>(true)) collider.enabled = enabled;
    }

    private int GetBlockNumber(GrabbableObject block)
    {
        if (GetBlockIndex(block) < 0 && !challengeFourCopies.Contains(block)) return 0;
        SetssssNumberedBlock numbered = block.GetComponent<SetssssNumberedBlock>();
        return numbered != null ? numbered.Value : 0;
    }

    private int GetBlockIndex(GrabbableObject block)
    {
        if (block == null || numberedBlocks == null) return -1;
        for (int i = 0; i < numberedBlocks.Length; i++) if (numberedBlocks[i] == block) return i;
        return -1;
    }

    private void RefreshBoard()
    {
        if (boardText == null) return;
        if (complete) { boardText.fontSize = 13.5f; boardText.text = "<color=#F4D794>Challenge 5</color>\nComplete!"; return; }
        if (!started)
        {
            boardText.fontSize = challengeNumber == 1 ? 13.5f : 9f;
            boardText.text = challengeNumber == 1
                ? "<color=#F4D794>Challenge 1</color>\nInteract with the Book Statue to begin"
                : "<color=#F4D794>Challenge " + (challengeNumber - 1) + " Complete!</color>\nInteract with the Book Statue to start Challenge " + challengeNumber + ".";
            return;
        }
        if (challengeNumber == 5 && activeComplement != null)
        {
            string universal = "U = {" + string.Join(", ", activeComplement.universe) + "}";
            string selected = activeComplement.setName + " = {" + string.Join(", ", activeComplement.selected) + "}";
            boardText.fontSize = 6.7f;
            boardText.text = "<color=#F4D794>Challenge 5 — Set Complement</color>\nCondition " +
                (conditionIndex + 1) + " of 5\n\n" + universal + "\n" + selected +
                "\nMove every U element into its correct area.\n" + activeComplement.setName +
                " members go inside " + activeComplement.setName + ".\n" + activeComplement.setName +
                "<sup>c</sup>: outside " + activeComplement.setName + ", but inside U.";
            if (reminderText != null)
                reminderText.text = "Challenge 5 — Condition " + (conditionIndex + 1) +
                    " of 5: " + selected + "\nPlace all U elements; others go outside " +
                    activeComplement.setName + " but inside U.";
            return;
        }
        if (activeCondition == null) return;
        string instruction = "A = {" + string.Join(", ", activeCondition.setA) + "}";
        if (challengeNumber > 1 || activeCondition.setB.Length > 0)
            instruction += "\nB = {" + string.Join(", ", activeCondition.setB) + "}";
        if (challengeNumber == 4)
        {
            boardText.fontSize = 6.8f;
            boardText.text = "<color=#F4D794>Challenge 4 — Set Difference</color>\nCondition " +
                (conditionIndex + 1) + " of 10\n\n" + instruction +
                "\nFill A and B with all their members.\nPlace shared members in A ∩ B.\nFill A − B and B − A with differences.";
            if (reminderText != null)
                reminderText.text = "Challenge 4 — Condition " + (conditionIndex + 1) +
                    " of 10: " + instruction.Replace(" = ", "=").Replace("\n", "  ");
            return;
        }
        boardText.fontSize = challengeNumber == 1 ? 13.5f : 9f;
        boardText.text = "<color=#F4D794>Challenge " + challengeNumber + "</color>\nCondition " + (conditionIndex + 1) + " of 10\n\n" + instruction;
        if (reminderText != null)
            reminderText.text = "Challenge " + challengeNumber + " — Condition " + (conditionIndex + 1) + " of 10: " + instruction;
    }

    private IEnumerator ShowFeedback(string message, Color color, float seconds)
    {
        if (feedbackText == null) yield break;
        int generation = ++feedbackGeneration;
        if (message.StartsWith("Incorrect. Fix the highlighted blocks", StringComparison.Ordinal))
        {
            feedbackText.fontSize = 32f;
            feedbackText.rectTransform.sizeDelta = new Vector2(feedbackDefaultSize.x, 155f);
            feedbackText.rectTransform.anchoredPosition = new Vector2(feedbackDefaultPosition.x, -175f);
        }
        feedbackText.text = message;
        feedbackText.color = color;
        yield return new WaitForSeconds(seconds);
        if (generation == feedbackGeneration && !complete && feedbackText.text == message)
        {
            feedbackText.text = string.Empty;
            RestoreFeedbackLayout();
        }
    }

    private IEnumerator ShowChallengeStartFeedback()
    {
        yield return ShowFeedback("Start", new Color(0.95f, 0.86f, 0.52f), 1.4f);
        if (challengeNumber == 4)
        {
            if (feedbackText != null)
            {
                feedbackText.fontSize = 30f;
                Vector2 size = feedbackDefaultSize;
                size.y = Mathf.Max(size.y, 150f);
                feedbackText.rectTransform.sizeDelta = size;
            }
            yield return ShowFeedback("Difficulty increased! Two new set-difference regions are now available: A − B and B − A.",
                new Color(0.95f, 0.86f, 0.52f), 4f);
            RestoreFeedbackLayout();
        }
    }

    private void RestoreFeedbackLayout()
    {
        if (feedbackText == null || feedbackDefaultFontSize <= 0f) return;
        feedbackText.fontSize = feedbackDefaultFontSize;
        feedbackText.rectTransform.sizeDelta = feedbackDefaultSize;
        feedbackText.rectTransform.anchoredPosition = feedbackDefaultPosition;
    }

    private static void FaceCamera(Transform target)
    {
        Vector3 direction = target.position - Camera.main.transform.position;
        if (direction.sqrMagnitude > 0.001f) target.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

#if UNITY_EDITOR
    public void Configure(Transform book, Transform spawn, Transform venn, GrabbableObject[] blocks,
        TextMeshPro boardLabel, TextMeshPro startPrompt, TMP_Text feedback, Button submit, SetssssBoardView view,
        Button bookButton, Button pickupButton, Button returnButton, TMP_Text reminder)
    {
        bookStatue = book;
        spawner = spawn;
        diagram = venn;
        numberedBlocks = blocks;
        boardText = boardLabel;
        bookPrompt = startPrompt;
        feedbackText = feedback;
        submitButton = submit;
        boardView = view;
        interactWithBookButton = bookButton;
        pickUpBlockButton = pickupButton;
        returnBlockButton = returnButton;
        reminderText = reminder;
    }
#endif
}
