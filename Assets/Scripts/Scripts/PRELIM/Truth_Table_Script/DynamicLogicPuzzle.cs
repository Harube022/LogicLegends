using UnityEngine;
using System.Collections;
using TMPro;

public class DynamicLogicPuzzle : MonoBehaviour
{
    private IPuzzlePhase currentPhase;
    private TruthTableChallengePhase challengePhase;
    private bool isProcessingPlacement = false;
    private Coroutine reviewCoroutine;
    private bool isActivePuzzle = true;
    private bool isPlayerNear = false;

    public bool PuzzleCompleted { get; private set; }
    public bool CanDebugClearChallenge => houseDoors != null && houseDoors.IsStageRunning &&
        challengePhase != null && !PuzzleCompleted;

    public bool DebugClearCurrentChallenge()
    {
        if (!CanDebugClearChallenge) return false;
        ClearColumnFeedback();
        challengePhase.DebugCompleteCurrentChallenge();
        return true;
    }

    [Header("Puzzle Configuration Mode")]
    [SerializeField] private PuzzleMode puzzleMode = PuzzleMode.EasyMode;
    // NEW: Determines how many distinct random logic rounds the player must solve
    [SerializeField, Min(1)] private int hardModeRequiredRounds = 3; 
    public int HardModeRequiredRounds => hardModeRequiredRounds;

    [Header("Snap Points for 3 Empty Columns")]
    [SerializeField] private Transform[] col1SnapPoints; 
    [SerializeField] private Transform[] col2SnapPoints; 
    [SerializeField] private Transform[] col3SnapPoints; 

    [Header("Dynamic Column Masking Barriers")]
    [SerializeField] private GameObject col1Barrier;     
    [SerializeField] private GameObject col2Barrier;     
    [SerializeField] private GameObject col3Barrier;     

    [Header("Barrier Visual Aesthetics")]
    [SerializeField] private Material lockedMaterial;     
    [SerializeField] private Material completedMaterial;
    public Material LockedMaterial => lockedMaterial;
    public Material CompletedMaterial => completedMaterial;

    [Header("Column Header UI Labels")]
    [SerializeField] private TMP_Text col1HeaderLabel;   
    [SerializeField] private TMP_Text col2HeaderLabel;   
    [SerializeField] private TMP_Text col3HeaderLabel;   

    [Header("Slot Placement Indicator")]
    [SerializeField] private Transform placementIndicator;

    [Header("External Block Spawner Reference")]
    [SerializeField] private TruthBlockSpawner blockSpawner;

    [Header("Completed Column Submission (Truth_Table board)")]
    [SerializeField] private UnityEngine.UI.Button submitColumnButton;
    [SerializeField] private TruthTableDoorInteraction houseDoors;
    [SerializeField] private GameObject columnFeedbackPanel;
    [SerializeField] private TMP_Text columnFeedbackText;
    [SerializeField, Min(0.5f)] private float columnFeedbackSeconds = 3f;
    private Coroutine columnFeedbackCoroutine;

    private void OnEnable()
    {
        if (submitColumnButton != null) submitColumnButton.onClick.AddListener(SubmitCurrentColumn);
    }

    private void OnDisable()
    {
        if (submitColumnButton != null)
        {
            submitColumnButton.onClick.RemoveListener(SubmitCurrentColumn);
            submitColumnButton.gameObject.SetActive(false);
        }
        ClearColumnFeedback();
    }

    private void Update()
    {
        // The quiz has its own feedback label. Never leave the previous
        // column result visible behind or above the quiz panel.
        if (houseDoors != null && houseDoors.IsQuizOpen) ClearColumnFeedback();
        if (submitColumnButton == null) return;
        bool ready = isActivePuzzle && !PuzzleCompleted &&
                     (houseDoors == null || houseDoors.IsStageRunning) &&
                     currentPhase != null && currentPhase.CanSubmitColumn;
        if (submitColumnButton.gameObject.activeSelf != ready)
            submitColumnButton.gameObject.SetActive(ready);
    }

    public void SubmitCurrentColumn()
    {
        if (!isActivePuzzle || PuzzleCompleted || currentPhase == null || !currentPhase.CanSubmitColumn ||
            (houseDoors != null && !houseDoors.IsStageRunning))
            return;

        bool correct = currentPhase.SubmitColumn();
        if (houseDoors != null) houseDoors.PlayAnswerFeedback(correct);
        if (!correct && houseDoors != null) houseDoors.ApplyIncorrectColumnPenalty();
        if (houseDoors != null && !houseDoors.IsStageRunning && !correct) return;
        if (houseDoors != null) houseDoors.ResetAfterColumnEvaluation();
        ShowColumnFeedback(correct);
        UpdatePlacementIndicator();
        Debug.Log(correct ? "Truth_Table column correct; advancing." :
                            "Truth_Table column incorrect; retry the same column.");
    }

    private void Start()
    {
        ClearColumnFeedback();
        InitializeMode(puzzleMode);
    }

    private void ShowColumnFeedback(bool correct)
    {
        if (columnFeedbackPanel == null || columnFeedbackText == null) return;
        ClearColumnFeedback();
        columnFeedbackText.text = correct ? "Correct" : "Incorrect, Try again";
        columnFeedbackText.color = correct ? new Color(0.25f, 0.95f, 0.35f) :
                                             new Color(1f, 0.27f, 0.27f);
        columnFeedbackPanel.SetActive(true);
        columnFeedbackCoroutine = StartCoroutine(HideColumnFeedbackAfterDelay());
    }

    private IEnumerator HideColumnFeedbackAfterDelay()
    {
        yield return new WaitForSecondsRealtime(columnFeedbackSeconds);
        columnFeedbackCoroutine = null;
        ClearColumnFeedback();
    }

    private void ClearColumnFeedback()
    {
        if (columnFeedbackCoroutine != null)
        {
            StopCoroutine(columnFeedbackCoroutine);
            columnFeedbackCoroutine = null;
        }
        if (columnFeedbackText != null) columnFeedbackText.text = string.Empty;
        if (columnFeedbackPanel != null) columnFeedbackPanel.SetActive(false);
    }

    private void InitializeMode(PuzzleMode mode)
    {
        puzzleMode = mode;
        // The active TRUTIBOL puzzle has the shared house-door controller. Keep
        // legacy puzzle components on their existing modes.
        if (houseDoors != null && blockSpawner != null)
        {
            if (challengePhase == null) challengePhase = new TruthTableChallengePhase(this);
            currentPhase = challengePhase;
        }
        else if (mode == PuzzleMode.HardMode)
            currentPhase = new HardPuzzlePhase(this);
        else
            currentPhase = new EasyPuzzlePhase(this);

        currentPhase.StartPhase();
        UpdatePlacementIndicator();
    }

    // =========================================================
    // PUBLIC API FOR SLOTS & PLAYER
    // =========================================================

    public void TryPlace(TruthBlock block, int columnIndex)
    {
        if (!isActivePuzzle || PuzzleCompleted || currentPhase == null ||
            (houseDoors != null && !houseDoors.IsStageRunning))
        {
            block.ReturnToOrigin(true);
            return;
        }
        if (isProcessingPlacement)
        {
            RejectInvalidPlacement(block);
            return;
        }

        isProcessingPlacement = true;
        ClearColumnFeedback();
        currentPhase.HandleTryPlace(block, columnIndex);
        isProcessingPlacement = false;
    }

    public void RejectInvalidPlacement(TruthBlock block)
    {
        block.ReturnToOrigin(true);
        if (houseDoors != null && houseDoors.IsStageRunning)
            houseDoors.ResetAfterColumnEvaluation();
    }

    public void SetPlayerProximity(bool near)
    {
        isPlayerNear = near;
        if (houseDoors != null)
        {
            TruthTableStageClock clock = houseDoors.GetComponent<TruthTableStageClock>();
            if (clock != null) clock.SetPlayerAtBoard(near);
        }
        UpdatePlacementIndicator();
    }

    public void UpdatePlacementIndicator()
    {
        if (placementIndicator == null || currentPhase == null) return;

        Transform targetSnap = currentPhase.GetActiveSnapPoint();

        if (isPlayerNear && targetSnap != null && !PuzzleCompleted)
        {
            placementIndicator.gameObject.SetActive(true);
            placementIndicator.position = targetSnap.position;
            placementIndicator.rotation = targetSnap.rotation;
        }
        else
        {
            placementIndicator.gameObject.SetActive(false);
        }
    }

    // =========================================================
    // HELPER METHODS FOR PHASES TO CONTROL THE GAME WORLD
    // =========================================================

    public void SwitchToHardMode()
    {
        InitializeMode(PuzzleMode.HardMode);
    }

    public void PrepareNextChallenge()
    {
        if (houseDoors != null) houseDoors.PrepareNextChallenge();
    }

    public void CompletePuzzle()
    {
        if (PuzzleCompleted) return;

        PuzzleCompleted = true;
        if (houseDoors != null)
        {
            houseDoors.ResetAfterColumnEvaluation();
            houseDoors.CompleteStage();
        }
        StageCompleteManager.UnlockStage(3);
        currentPhase.UpdateMasking();
        currentPhase.UpdateHeaders();
        UpdatePlacementIndicator();

        Debug.Log("Entire Puzzle Completed!");

        // if (reviewCoroutine != null) StopCoroutine(reviewCoroutine);
        // reviewCoroutine = StartCoroutine(ReviewThenReturn());
        // Completion now waits for the result panel's Next Stage or Main Menu action.
    }

    public void SetHeaderLabel(int columnIndex, string text)
    {
        TMP_Text label = columnIndex == 0 ? col1HeaderLabel : 
                         columnIndex == 1 ? col2HeaderLabel : col3HeaderLabel;
        if (label != null) label.text = text;
    }

    public void UpdateBarrier(int columnIndex, bool blockPlayer, Material mat)
    {
        GameObject barrier = columnIndex == 0 ? col1Barrier : 
                             columnIndex == 1 ? col2Barrier : col3Barrier;

        if (barrier == null) return;
        
        if (barrier.TryGetComponent(out MeshRenderer renderer)) 
            if (mat != null) renderer.material = mat;
            
        if (barrier.TryGetComponent(out Collider col)) 
            col.enabled = blockPlayer;

        barrier.SetActive(blockPlayer || renderer.material == null); // Hide completely if needed
    }

    public Transform[] GetColumnSnapPoints(int columnIndex)
    {
        return columnIndex == 0 ? col1SnapPoints : 
               columnIndex == 1 ? col2SnapPoints : col3SnapPoints;
    }

    public void SpawnEasyBlocks(int step) => blockSpawner.SpawnBlocksForStep(step);

    public void SpawnBlocksForTruthValues(bool[] values) => blockSpawner.SpawnBlocksForTruthValues(values);

    public void RespawnBlocksForRetry(bool[] values) => blockSpawner.RespawnBlocksForRetry(values);

    public void SetChallengeProgress(int challenge, string difficulty, int column, string expression)
    {
        if (houseDoors == null) return;
        TruthTableStageClock clock = houseDoors.GetComponent<TruthTableStageClock>();
        if (clock != null) clock.SetChallengeProgress(challenge, difficulty, column, expression);
    }
    
    // Replace the old SpawnHardBlocks with these two:
    public void SpawnHardBlocksSimple(DynamicLogicType type) => blockSpawner.SpawnBlocksForHardModeColumnSimple(type);
    
    public void SpawnHardBlocksComplex(ComplexLogicExpression expr) => blockSpawner.SpawnBlocksForHardModeColumnComplex(expr);
    public void LockBlock(TruthBlock block, Transform snapPoint)
    {
        block.MarkPlacedInColumn();
        block.transform.position = snapPoint.position;
        block.transform.rotation = snapPoint.rotation;

        Rigidbody rb = block.GetComponent<Rigidbody>();
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.useGravity = false;

        block.GetComponent<Collider>().enabled = false;

        if (InventoryManager.Instance != null)
            InventoryManager.Instance.TryRemoveBlock(block);
    }

    // =========================================================
    // CLEANUP & UTILITIES
    // =========================================================

    public void ClearColumnsOfBlocks(params int[] columnIndices)
    {
        TruthBlock[] allBlocks = FindObjectsByType<TruthBlock>(FindObjectsSortMode.None);
        foreach (var block in allBlocks)
        {
            foreach (int colIdx in columnIndices)
            {
                if (IsSnappedToAny(block, GetColumnSnapPoints(colIdx)))
                {
                    block.StopAllCoroutines();
                    Destroy(block.gameObject);
                    break;
                }
            }
        }
    }

    public void ClearAllSnappedBlocks()
    {
        ClearColumnsOfBlocks(0, 1, 2);
    }

    public void ClearActiveBlocksForDebug()
    {
        if (blockSpawner != null) blockSpawner.ClearActiveBlocks(true);
    }

    private bool IsSnappedToAny(TruthBlock block, Transform[] points)
    {
        foreach (var pt in points)
        {
            if (pt != null && Vector3.Distance(block.transform.position, pt.position) < 0.1f)
                return true;
        }
        return false;
    }

    private IEnumerator ReviewThenReturn()
    {
        yield return new WaitForSeconds(10f);
        ClearAllSnappedBlocks();
        ResetPuzzle();
        reviewCoroutine = null;   
    }

    private void ResetPuzzle()
    {
        PuzzleCompleted = false;
        isProcessingPlacement = false;
        InitializeMode(PuzzleMode.EasyMode);
    }

    public void SetActiveState(bool state) => isActivePuzzle = state;

    // Backward compatibility for anything outside this script calling it
    public static bool EvaluateLogic(DynamicLogicType type, bool left, bool right)
    {
        return LogicUtility.EvaluateLogic(type, left, right);
    }
}
