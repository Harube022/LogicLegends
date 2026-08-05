using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public enum DynamicLogicType
{
    AND,
    OR,
    EXCLUSIVE_OR,
    CONDITIONAL,
    BICONDITIONAL
}

public enum PuzzleMode
{
    EasyMode, // Progressive Topics: Conjunction -> Disjunction -> XOR -> Implication -> Biconditional
    HardMode  // 3 Empty Columns with randomized propositional logic operators
}

public class DynamicLogicPuzzle : MonoBehaviour
{
    private bool isProcessingPlacement = false;
    private bool puzzleCompleted = false;
    private Coroutine reviewCoroutine;
    private bool isActivePuzzle = true;
    private bool isPlayerNear = false;

    [Header("Puzzle Configuration Mode")]
    [SerializeField] private PuzzleMode puzzleMode = PuzzleMode.EasyMode; // Starts at Easy Mode first

    [Header("Snap Points for 3 Empty Columns")]
    [SerializeField] private Transform[] col1SnapPoints; // 4 rows for Empty Column 1
    [SerializeField] private Transform[] col2SnapPoints; // 4 rows for Empty Column 2
    [SerializeField] private Transform[] col3SnapPoints; // 4 rows for Empty Column 3

    [Header("Dynamic Column Masking Barriers")]
    [SerializeField] private GameObject col1Barrier;     // Barrier for Empty Column 1
    [SerializeField] private GameObject col2Barrier;     // Barrier for Empty Column 2
    [SerializeField] private GameObject col3Barrier;     // Barrier for Empty Column 3

    [Header("Barrier Visual Aesthetics")]
    [SerializeField] private Material lockedMaterial;     
    [SerializeField] private Material completedMaterial;

    [Header("Column Header UI Labels (Empty Columns 1, 2, 3)")]
    [SerializeField] private TMP_Text col1HeaderLabel;   
    [SerializeField] private TMP_Text col2HeaderLabel;   
    [SerializeField] private TMP_Text col3HeaderLabel;   

    [Header("Slot Placement Indicator")]
    [SerializeField] private Transform placementIndicator;

    [Header("External Block Spawner Reference")]
    [SerializeField] private TruthBlockSpawner blockSpawner;

    // --- HARD MODE RANDOMIZATION DATA ---
    private DynamicLogicType[] hardModeTypes = new DynamicLogicType[3];
    public DynamicLogicType[] HardModeTypes => hardModeTypes;

    private int currentColumnIndex = 0; // 0 = Col 1, 1 = Col 2, 2 = Col 3
    private int currentRow = 0;          // 0 to 3

    // --- EASY MODE TRACKER ---
    private int easyModeStep = 0;
    public int EasyModeStep => easyModeStep;

    private void Start()
    {
        if (puzzleMode == PuzzleMode.HardMode)
        {
            GenerateRandomHardModeLogics();
        }

        UpdateColumnMasking();
        UpdateColumnHeaderLabels();
        UpdatePlacementIndicator();

        SpawnCurrentStepBlocks();
    }

    // =========================================================
    // LOGIC RANDOMIZER & HEADERS
    // =========================================================

    private void GenerateRandomHardModeLogics()
    {
        System.Array types = System.Enum.GetValues(typeof(DynamicLogicType));
        List<DynamicLogicType> availableTypes = new List<DynamicLogicType>();
        foreach (DynamicLogicType t in types) availableTypes.Add(t);

        for (int i = 0; i < 3; i++)
        {
            int randomIndex = Random.Range(0, availableTypes.Count);
            hardModeTypes[i] = availableTypes[randomIndex];
            availableTypes.RemoveAt(randomIndex); 
        }
    }

    public void UpdateColumnHeaderLabels()
    {
        if (col1HeaderLabel == null || col2HeaderLabel == null || col3HeaderLabel == null)
            return;

        if (puzzleMode == PuzzleMode.HardMode)
        {
            col1HeaderLabel.text = GetLogicSymbolString(hardModeTypes[0], "P", "Q");
            col2HeaderLabel.text = GetLogicSymbolString(hardModeTypes[1], "P", "Q");
            col3HeaderLabel.text = GetLogicSymbolString(hardModeTypes[2], "P", "Q");
        }
        else
        {
            if (easyModeStep >= 3)
            {
                col1HeaderLabel.text = "P → Q";
                col2HeaderLabel.text = "P ↔ Q";
                // col3HeaderLabel.text = "P ⊕ Q";
            }
            else
            {
                col1HeaderLabel.text = "P ∧ Q";
                col2HeaderLabel.text = "P ∨ Q";
                col3HeaderLabel.text = "P ⊕ Q";
            }
        }
    }

    private string GetLogicSymbolString(DynamicLogicType type, string left, string right)
    {
        switch (type)
        {
            case DynamicLogicType.AND:          return $"{left} ∧ {right}";
            case DynamicLogicType.OR:           return $"{left} ∨ {right}";
            case DynamicLogicType.EXCLUSIVE_OR: return $"{left} ⊕ {right}";
            case DynamicLogicType.CONDITIONAL:  return $"{left} → {right}";
            case DynamicLogicType.BICONDITIONAL:return $"{left} ↔ {right}";
            default:                            return "";
        }
    }

    // =========================================================
    // SPAWNING INTEGRATION
    // =========================================================

    private void SpawnCurrentStepBlocks()
    {
        if (blockSpawner == null) return;

        if (puzzleMode == PuzzleMode.EasyMode)
        {
            blockSpawner.SpawnBlocksForStep(easyModeStep);
        }
        else
        {
            // Spawn blocks only for the current active column in Hard Mode
            blockSpawner.SpawnBlocksForHardModeColumn(hardModeTypes[currentColumnIndex]);
        }
    }

    // =========================================================
    // SLOT INDICATOR & PROXIMITY
    // =========================================================

    public void SetPlayerProximity(bool near)
    {
        isPlayerNear = near;
        UpdatePlacementIndicator();
    }

    private Transform GetActiveSnapPoint()
    {
        if (puzzleCompleted) return null;

        Transform[] activeArray = null;

        if (puzzleMode == PuzzleMode.HardMode)
        {
            switch (currentColumnIndex)
            {
                case 0: activeArray = col1SnapPoints; break;
                case 1: activeArray = col2SnapPoints; break;
                case 2: activeArray = col3SnapPoints; break;
            }
        }
        else
        {
            int stepCol = GetEasyModeExpectedColumnIndex();
            switch (stepCol)
            {
                case 0: activeArray = col1SnapPoints; break;
                case 1: activeArray = col2SnapPoints; break;
                case 2: activeArray = col3SnapPoints; break;
            }
        }

        if (activeArray != null && currentRow >= 0 && currentRow < activeArray.Length)
        {
            return activeArray[currentRow];
        }

        return null;
    }

    public void UpdatePlacementIndicator()
    {
        if (placementIndicator == null) return;

        Transform targetSnap = GetActiveSnapPoint();

        if (isPlayerNear && targetSnap != null && !puzzleCompleted)
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
    // PLACEMENT PROCESSOR
    // =========================================================

    public void TryPlace(TruthBlock block, int columnIndex)
    {
        if (!isActivePuzzle || puzzleCompleted || isProcessingPlacement)
        {
            block.ReturnToOrigin(true);
            return;
        }

        isProcessingPlacement = true;

        if (puzzleMode == PuzzleMode.HardMode)
        {
            ProcessHardMode(block, columnIndex);
        }
        else
        {
            ProcessEasyMode(block, columnIndex);
        }

        isProcessingPlacement = false;
    }

    private void ProcessHardMode(TruthBlock block, int columnIndex)
    {
        if (columnIndex != currentColumnIndex)
        {
            block.ReturnToOrigin(true);
            return;
        }

        bool p = (currentRow == 0 || currentRow == 1);
        bool q = (currentRow == 0 || currentRow == 2);

        bool expectedValue = EvaluateLogic(hardModeTypes[currentColumnIndex], p, q);

        if (block.value != expectedValue)
        {
            block.ReturnToOrigin(true);
            return;
        }

        Transform targetSnap = null;
        if (currentColumnIndex == 0) targetSnap = col1SnapPoints[currentRow];
        else if (currentColumnIndex == 1) targetSnap = col2SnapPoints[currentRow];
        else if (currentColumnIndex == 2) targetSnap = col3SnapPoints[currentRow];

        LockBlock(block, targetSnap);
        AdvanceHardMode();
    }

    private void AdvanceHardMode()
    {
        currentRow++;
        UpdatePlacementIndicator();

        if (currentRow < 4) return;

        currentRow = 0;
        currentColumnIndex++;

        if (currentColumnIndex >= 3)
        {
            CompletePuzzle();
            return;
        }

        UpdateColumnMasking();
        UpdateColumnHeaderLabels();
        UpdatePlacementIndicator();
        SpawnCurrentStepBlocks();
    }

    public static bool EvaluateLogic(DynamicLogicType type, bool left, bool right)
    {
        switch (type)
        {
            case DynamicLogicType.AND:          return left && right;
            case DynamicLogicType.OR:           return left || right;
            case DynamicLogicType.EXCLUSIVE_OR: return left != right;
            case DynamicLogicType.CONDITIONAL:  return !left || right;
            case DynamicLogicType.BICONDITIONAL:return left == right;
            default:                            return false;
        }
    }

    // =========================================================
    // EASY MODE LOGIC & TRANSITION
    // =========================================================

    private void ProcessEasyMode(TruthBlock block, int columnIndex)
    {
        int expectedCol = GetEasyModeExpectedColumnIndex();
        if (columnIndex != expectedCol)
        {
            block.ReturnToOrigin(true);
            return;
        }

        bool p = (currentRow == 0 || currentRow == 1);
        bool q = (currentRow == 0 || currentRow == 2);

        bool correctValue = false;
        switch (easyModeStep)
        {
            case 0: correctValue = p && q; break;
            case 1: correctValue = p || q; break;
            case 2: correctValue = p != q; break;
            case 3: correctValue = !p || q; break;
            case 4: correctValue = p == q; break;
        }

        if (block.value != correctValue)
        {
            block.ReturnToOrigin(true);
            return;
        }

        Transform targetSnap = (columnIndex == 0) ? col1SnapPoints[currentRow] : 
                               (columnIndex == 1) ? col2SnapPoints[currentRow] : col3SnapPoints[currentRow];

        LockBlock(block, targetSnap);
        AdvanceEasyMode();
    }

    private int GetEasyModeExpectedColumnIndex()
    {
        switch (easyModeStep)
        {
            case 0: return 0; // Column 1
            case 1: return 1; // Column 2
            case 2: return 2; // Column 3
            case 3: return 0; // Reuse Column 1
            case 4: return 1; // Reuse Column 2
            default: return 0;
        }
    }

    private void AdvanceEasyMode()
    {
        currentRow++;
        UpdatePlacementIndicator();

        if (currentRow < 4) return;

        currentRow = 0;
        easyModeStep++;

        if (easyModeStep == 3)
        {
            ClearPhysicalColumnsForPhase2();
        }
        else if (easyModeStep >= 5)
        {
            // Transition directly into Hard Mode after Easy Mode completion
            StartHardMode();
            return;
        }

        UpdateColumnMasking();
        UpdateColumnHeaderLabels();
        UpdatePlacementIndicator();
        SpawnCurrentStepBlocks();
    }

    private void StartHardMode()
    {
        puzzleMode = PuzzleMode.HardMode;
        currentColumnIndex = 0;
        currentRow = 0;

        ClearAllSnappedBlocks();
        GenerateRandomHardModeLogics();

        UpdateColumnMasking();
        UpdateColumnHeaderLabels();
        UpdatePlacementIndicator();
        SpawnCurrentStepBlocks();
    }

    private void ClearPhysicalColumnsForPhase2()
    {
        TruthBlock[] allBlocks = FindObjectsByType<TruthBlock>(FindObjectsSortMode.None);
        foreach (var block in allBlocks)
        {
            if (IsSnappedToAny(block, col1SnapPoints) || IsSnappedToAny(block, col2SnapPoints))
            {
                block.StopAllCoroutines();
                Destroy(block.gameObject);
            }
        }
    }

    private void ClearAllSnappedBlocks()
    {
        TruthBlock[] allBlocks = FindObjectsByType<TruthBlock>(FindObjectsSortMode.None);
        foreach (var block in allBlocks)
        {
            if (IsSnappedToAny(block, col1SnapPoints) || IsSnappedToAny(block, col2SnapPoints) || IsSnappedToAny(block, col3SnapPoints))
            {
                block.StopAllCoroutines();
                Destroy(block.gameObject);
            }
        }
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

    // =========================================================
    // BARRIER MASKING SYSTEM
    // =========================================================

    private void UpdateColumnMasking()
    {
        if (puzzleMode == PuzzleMode.HardMode)
        {
            SetBarrierState(col1Barrier, 0, currentColumnIndex);
            SetBarrierState(col2Barrier, 1, currentColumnIndex);
            SetBarrierState(col3Barrier, 2, currentColumnIndex);
        }
        else
        {
            SetBarrierVisual(col1Barrier, true, lockedMaterial);
            SetBarrierVisual(col2Barrier, true, lockedMaterial);
            SetBarrierVisual(col3Barrier, true, lockedMaterial);

            if (puzzleCompleted)
            {
                SetBarrierVisual(col1Barrier, true, completedMaterial);
                SetBarrierVisual(col2Barrier, true, completedMaterial);
                SetBarrierVisual(col3Barrier, true, completedMaterial);
                return;
            }

            switch (easyModeStep)
            {
                case 0: SetBarrierVisual(col1Barrier, false, null); break;
                case 1: SetBarrierVisual(col1Barrier, true, completedMaterial); SetBarrierVisual(col2Barrier, false, null); break;
                case 2: SetBarrierVisual(col1Barrier, true, completedMaterial); SetBarrierVisual(col2Barrier, true, completedMaterial); SetBarrierVisual(col3Barrier, false, null); break;
                case 3: SetBarrierVisual(col1Barrier, false, null); SetBarrierVisual(col3Barrier, true, completedMaterial); break;
                case 4: SetBarrierVisual(col1Barrier, true, completedMaterial); SetBarrierVisual(col2Barrier, false, null); SetBarrierVisual(col3Barrier, true, completedMaterial); break;
            }
        }
    }

    private void SetBarrierState(GameObject barrier, int barrierIndex, int activeIndex)
    {
        if (barrier == null) return;

        if (puzzleCompleted)
        {
            SetBarrierVisual(barrier, true, completedMaterial);
            return;
        }

        if (activeIndex == barrierIndex)
            SetBarrierVisual(barrier, false, null);
        else if (activeIndex > barrierIndex)
            SetBarrierVisual(barrier, true, completedMaterial);
        else
            SetBarrierVisual(barrier, true, lockedMaterial);
    }

    private void SetBarrierVisual(GameObject barrier, bool blockPlayer, Material mat)
    {
        if (barrier == null) return;
        
        MeshRenderer renderer = barrier.GetComponent<MeshRenderer>();
        Collider col = barrier.GetComponent<Collider>();

        if (blockPlayer)
        {
            barrier.SetActive(true);
            if (renderer != null && mat != null) renderer.material = mat;
            if (col != null) col.enabled = true;
        }
        else
        {
            barrier.SetActive(false);
            if (col != null) col.enabled = false;
        }
    }

    // =========================================================
    // UTILITIES
    // =========================================================

    private void LockBlock(TruthBlock block, Transform snapPoint)
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
        {
            InventoryManager.Instance.TryRemoveBlock(block);
        }
    }

    private void CompletePuzzle()
    {
        if (puzzleCompleted) return;

        puzzleCompleted = true;
        UpdateColumnMasking();
        UpdateColumnHeaderLabels();
        UpdatePlacementIndicator();

        Debug.Log("Entire Puzzle (Easy + Hard Mode) Completed!");

        if (reviewCoroutine != null) StopCoroutine(reviewCoroutine);
        reviewCoroutine = StartCoroutine(ReviewThenReturn());
    }

    private IEnumerator ReviewThenReturn()
    {
        yield return new WaitForSeconds(10f);

        TruthBlock[] blocks = FindObjectsByType<TruthBlock>(FindObjectsSortMode.None);
        foreach (var block in blocks)
        {
            block.StopAllCoroutines();
            Destroy(block.gameObject);
        }

        ResetPuzzle();
        reviewCoroutine = null;   
    }

    private void ResetPuzzle()
    {
        puzzleMode = PuzzleMode.EasyMode;
        currentColumnIndex = 0;
        currentRow = 0;
        easyModeStep = 0;

        puzzleCompleted = false;
        isProcessingPlacement = false;

        UpdateColumnMasking(); 
        UpdateColumnHeaderLabels();
        SpawnCurrentStepBlocks();
    }

    public void SetActiveState(bool state)
    {
        isActivePuzzle = state;
    }
}