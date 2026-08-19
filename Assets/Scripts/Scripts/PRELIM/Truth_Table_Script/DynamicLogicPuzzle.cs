using UnityEngine;
using System.Collections;
using TMPro;

public class DynamicLogicPuzzle : MonoBehaviour
{
    private IPuzzlePhase currentPhase;
    private bool isProcessingPlacement = false;
    private Coroutine reviewCoroutine;
    private bool isActivePuzzle = true;
    private bool isPlayerNear = false;

    public bool PuzzleCompleted { get; private set; }

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

    private void Start()
    {
        InitializeMode(puzzleMode);
    }

    private void InitializeMode(PuzzleMode mode)
    {
        puzzleMode = mode;
        if (mode == PuzzleMode.HardMode)
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
        if (!isActivePuzzle || PuzzleCompleted || isProcessingPlacement || currentPhase == null)
        {
            block.ReturnToOrigin(true);
            return;
        }

        isProcessingPlacement = true;
        currentPhase.HandleTryPlace(block, columnIndex);
        isProcessingPlacement = false;
    }

    public void SetPlayerProximity(bool near)
    {
        isPlayerNear = near;
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

    public void CompletePuzzle()
    {
        if (PuzzleCompleted) return;

        PuzzleCompleted = true;
        currentPhase.UpdateMasking();
        currentPhase.UpdateHeaders();
        UpdatePlacementIndicator();

        Debug.Log("Entire Puzzle Completed!");

        if (reviewCoroutine != null) StopCoroutine(reviewCoroutine);
        reviewCoroutine = StartCoroutine(ReviewThenReturn());
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
    
    // Replace the old SpawnHardBlocks with these two:
    public void SpawnHardBlocksSimple(DynamicLogicType type) => blockSpawner.SpawnBlocksForHardModeColumnSimple(type);
    
    public void SpawnHardBlocksComplex(ComplexLogicExpression expr) => blockSpawner.SpawnBlocksForHardModeColumnComplex(expr);
    public void LockBlock(TruthBlock block, Transform snapPoint)
    {
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