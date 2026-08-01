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

public enum DynamicPuzzleColumn
{
    P,
    Q,
    OUTPUT_PQ,
    OUTPUT_QP
}

public enum PuzzleMode
{
    HardMode, // Standard: Manual P, Manual Q, Output PQ, Output QP
    EasyMode  // Progressive Topics: Conjunction -> Disjunction -> XOR -> Implication -> Biconditional
}

public class DynamicLogicPuzzle : MonoBehaviour
{
    private bool isProcessingPlacement = false;
    private bool puzzleCompleted = false;
    private Coroutine reviewCoroutine;
    private bool isActivePuzzle = true;

    // Track whether player is inside the proximity trigger zone
    private bool isPlayerNear = false;

    [Header("Puzzle Configuration Mode")]
    [SerializeField] private PuzzleMode puzzleMode = PuzzleMode.HardMode;

    [Header("Hard Mode Options")]
    [SerializeField] private DynamicLogicType logicType;
    public DynamicLogicType PuzzleLogicType => logicType;

    [Header("Expected Values (Hard Mode Only)")]
    [SerializeField] private bool[] expectedP = new bool[4];
    [SerializeField] private bool[] expectedQ = new bool[4];

    [Header("Snap Points")]
    [SerializeField] private Transform[] pSnapPoints;
    [SerializeField] private Transform[] qSnapPoints;
    [SerializeField] private Transform[] outputPQSnapPoints;
    [SerializeField] private Transform[] outputQPSnapPoints;

    [Header("Dynamic Column Masking Barriers")]
    [SerializeField] private GameObject pBarrier;         // Physical Column 1 (Leftmost)
    [SerializeField] private GameObject qBarrier;         // Physical Column 2 (Middle-Left)
    [SerializeField] private GameObject outputPQBarrier;  // Physical Column 3 (Middle-Right)
    [SerializeField] private GameObject outputQPBarrier;  // Physical Column 4 (Rightmost)

    [Header("Barrier Visual Aesthetics")]
    [SerializeField] private Material lockedMaterial;     
    [SerializeField] private Material completedMaterial;

    [Header("Column Header UI Labels")]
    [SerializeField] private TMP_Text pColumnLabel;         // Top of Column 1
    [SerializeField] private TMP_Text qColumnLabel;         // Top of Column 2
    [SerializeField] private TMP_Text outputPQColumnLabel;  // Top of Column 3
    [SerializeField] private TMP_Text outputQPColumnLabel;  // Top of Column 4  

    [Header("Slot Placement Indicator")]
    [SerializeField] private Transform placementIndicator; // Reference to SlotPlacementIndicator

    [Header("External Block Spawner Reference")]
    [SerializeField] private TruthBlockSpawner blockSpawner;
    // Hard Mode Tracking Trackers
    private DynamicPuzzleColumn currentColumn = DynamicPuzzleColumn.P;
    private int currentRow = 0;
    private bool[] placedP;
    private bool[] placedQ;

    // Easy Mode Sequence Tracker (0 = Conjunction, 1 = Disjunction, 2 = XOR, 3 = Implication, 4 = Biconditional)
    private int easyModeStep = 0;
    public int EasyModeStep => easyModeStep;

    private void Start()
    {
        placedP = new bool[4];
        placedQ = new bool[4];
        UpdateColumnMasking();
        UpdateColumnHeaderLabels();
        UpdatePlacementIndicator(); // Update indicator on launch

        if (puzzleMode == PuzzleMode.EasyMode && blockSpawner != null)
        {
            blockSpawner.SpawnBlocksForStep(easyModeStep);
        }
    }
    // =========================================================
    // DYNAMIC HEADER DISPLAY CONTROLLER
    // =========================================================

    private void UpdateColumnHeaderLabels()
    {
        if (pColumnLabel == null || qColumnLabel == null || outputPQColumnLabel == null || outputQPColumnLabel == null)
            return;

        if (puzzleMode == PuzzleMode.HardMode)
        {
            pColumnLabel.text = "P";
            qColumnLabel.text = "Q";
            outputPQColumnLabel.text = GetLogicSymbolString(logicType, "P", "Q");
            outputQPColumnLabel.text = GetLogicSymbolString(logicType, "Q", "P");
        }
        else
        {
            // --- EASY MODE PROGRESSIVE TOPICS ---
            if (easyModeStep >= 3)
            {
                // Phase 2: Update BOTH headers immediately when XOR is completed
                pColumnLabel.text = "P → Q"; // Implication (Physical Column 1)
                qColumnLabel.text = "P ↔ Q"; // Biconditional (Physical Column 2)
            }
            else
            {
                // Phase 1: Initial Topic Headers
                pColumnLabel.text = "P ∧ Q"; // Conjunction (Physical Column 1)
                qColumnLabel.text = "P ∨ Q"; // Disjunction (Physical Column 2)
            }
            // Physical Column 3 (OUTPUT_PQ)
            outputPQColumnLabel.text = "P ⊕ Q"; // Exclusive OR

            // Physical Column 4 is unused in Easy Mode
            outputQPColumnLabel.text = "";
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
    // ACTIVE SLOT INDICATOR CONTROLLER
    // =========================================================
    public void SetPlayerProximity(bool near)
    {
        isPlayerNear = near;
        UpdatePlacementIndicator();
    }
    private Transform GetActiveSnapPoint()
    {
        if (puzzleCompleted) return null;

        // Determine which physical column is currently active
        DynamicPuzzleColumn activeCol = (puzzleMode == PuzzleMode.HardMode) 
            ? currentColumn 
            : GetEasyModeExpectedColumn();

        Transform[] activeArray = null;
        switch (activeCol)
        {
            case DynamicPuzzleColumn.P: activeArray = pSnapPoints; break;
            case DynamicPuzzleColumn.Q: activeArray = qSnapPoints; break;
            case DynamicPuzzleColumn.OUTPUT_PQ: activeArray = outputPQSnapPoints; break;
            case DynamicPuzzleColumn.OUTPUT_QP: activeArray = outputQPSnapPoints; break;
        }

        // Return the snap point matching the active row index (0 = top row, 3 = bottom row)
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
    // ENTRY CONTROLLER
    // =========================================================

    public void TryPlace(TruthBlock block, DynamicPuzzleColumn column)
    {
        if (!isActivePuzzle || puzzleCompleted || isProcessingPlacement)
        {
            block.ReturnToOrigin(true);
            return;
        }

        isProcessingPlacement = true;

        if (puzzleMode == PuzzleMode.HardMode)
        {
            ProcessHardMode(block, column);
        }
        else
        {
            ProcessEasyMode(block, column);
        }

        isProcessingPlacement = false;
    }

    // =========================================================
    // EASY MODE CORE MECHANICS (PROGRESSIVE TOPICS)
    // =========================================================

    private void ProcessEasyMode(TruthBlock block, DynamicPuzzleColumn column)
    {
        // 1. Validate if player is trying to interact with the correct sequential column
        DynamicPuzzleColumn expectedPhysicalColumn = GetEasyModeExpectedColumn();

        if (column != expectedPhysicalColumn)
        {
            block.ReturnToOrigin(true);
            return;
        }

        // 2. Fetch baseline mathematical standard P and Q values based on row index
        // Row 0: T, T | Row 1: T, F | Row 2: F, T | Row 3: F, F
        bool p = (currentRow == 0 || currentRow == 1);
        bool q = (currentRow == 0 || currentRow == 2);

        // 3. Compute target truth value for the active topic
        bool correctValue = false;
        switch (easyModeStep)
        {
            case 0: correctValue = p && q; break;       // Conjunction (Physical Column 1)
            case 1: correctValue = p || q; break;       // Disjunction (Physical Column 2)
            case 2: correctValue = p != q; break;      // Exclusive OR (Physical Column 3)
            case 3: correctValue = !p || q; break;     // Implication / Conditional (Physical Column 1)
            case 4: correctValue = p == q; break;      // Biconditional (Physical Column 2)
        }

        if (block.value != correctValue)
        {
            block.ReturnToOrigin(true);
            return;
        }

        // 4. Find the matching snap point position
        Transform targetSnap = null;
        if (expectedPhysicalColumn == DynamicPuzzleColumn.P) targetSnap = pSnapPoints[currentRow];
        else if (expectedPhysicalColumn == DynamicPuzzleColumn.Q) targetSnap = qSnapPoints[currentRow];
        else if (expectedPhysicalColumn == DynamicPuzzleColumn.OUTPUT_PQ) targetSnap = outputPQSnapPoints[currentRow];

        LockBlock(block, targetSnap);
        AdvanceEasyMode();
    }

    private DynamicPuzzleColumn GetEasyModeExpectedColumn()
    {
        switch (easyModeStep)
        {
            case 0: return DynamicPuzzleColumn.P;          // Conjunction uses Column 1
            case 1: return DynamicPuzzleColumn.Q;          // Disjunction uses Column 2
            case 2: return DynamicPuzzleColumn.OUTPUT_PQ;   // XOR uses Column 3
            case 3: return DynamicPuzzleColumn.P;          // Implication reuses Column 1
            case 4: return DynamicPuzzleColumn.Q;          // Biconditional reuses Column 2
            default: return DynamicPuzzleColumn.P;
        }
    }

    private void AdvanceEasyMode()
    {
        currentRow++;

        // FIX: Update indicator position immediately after incrementing currentRow
        UpdatePlacementIndicator();

        if (currentRow < 4) return; // Must finish all 4 rows of the current topic first

        currentRow = 0;
        easyModeStep++;

        // Phase Transition Trigger: Conjunction, Disjunction, and XOR are all completed!
        // Clear columns 1 and 2 automatically so Implication and Biconditional can occupy them.
        if (easyModeStep == 3)
        {
            ClearPhysicalColumnsForPhase2();
        }
        else if (easyModeStep >= 5)
        {
            CompletePuzzle();
            return;
        }

        UpdateColumnMasking();
        UpdateColumnHeaderLabels();
        UpdatePlacementIndicator();

        // Notify the spawner to spawn blocks for the next step
        if (blockSpawner != null)
        {
            blockSpawner.SpawnBlocksForStep(easyModeStep);
        }

    }

    private void ClearPhysicalColumnsForPhase2()
    {
        TruthBlock[] allBlocks = FindObjectsByType<TruthBlock>(FindObjectsSortMode.None);
        foreach (var block in allBlocks)
        {
            // Only send back items snapped into physical columns 1 & 2
            if (IsSnappedToAny(block, pSnapPoints) || IsSnappedToAny(block, qSnapPoints))
            {
                block.StopAllCoroutines();
                // block.ReturnToOrigin(false);
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
    // ORIGINAL HARD MODE MECHANICS
    // =========================================================

    private void ProcessHardMode(TruthBlock block, DynamicPuzzleColumn column)
    {
        if (column != currentColumn)
        {
            block.ReturnToOrigin(true);
            return;
        }

        switch (currentColumn)
        {
            case DynamicPuzzleColumn.P:
                if (block.value != expectedP[currentRow]) { block.ReturnToOrigin(true); return; }
                LockBlock(block, pSnapPoints[currentRow]);
                placedP[currentRow] = block.value;
                AdvanceHardMode();
                break;

            case DynamicPuzzleColumn.Q:
                if (block.value != expectedQ[currentRow]) { block.ReturnToOrigin(true); return; }
                LockBlock(block, qSnapPoints[currentRow]);
                placedQ[currentRow] = block.value;
                AdvanceHardMode();
                break;

            case DynamicPuzzleColumn.OUTPUT_PQ:
                if (block.value != EvaluateHardModeLogic(placedP[currentRow], placedQ[currentRow])) { block.ReturnToOrigin(true); return; }
                LockBlock(block, outputPQSnapPoints[currentRow]);
                AdvanceHardMode();
                break;

            case DynamicPuzzleColumn.OUTPUT_QP:
                if (block.value != EvaluateHardModeLogic(placedQ[currentRow], placedP[currentRow])) { block.ReturnToOrigin(true); return; }
                LockBlock(block, outputQPSnapPoints[currentRow]);
                AdvanceHardMode();
                break;
        }
    }

    private void AdvanceHardMode()
    {
        currentRow++;

        UpdatePlacementIndicator();
        if (currentRow < 4) return;

        currentRow = 0;
        switch (currentColumn)
        {
            case DynamicPuzzleColumn.P: currentColumn = DynamicPuzzleColumn.Q; break;
            case DynamicPuzzleColumn.Q: currentColumn = DynamicPuzzleColumn.OUTPUT_PQ; break;
            case DynamicPuzzleColumn.OUTPUT_PQ: currentColumn = DynamicPuzzleColumn.OUTPUT_QP; break;
            case DynamicPuzzleColumn.OUTPUT_QP: CompletePuzzle(); return;
        }
        UpdateColumnMasking();
        UpdateColumnHeaderLabels();
        UpdatePlacementIndicator();

    }

    private bool EvaluateHardModeLogic(bool left, bool right)
    {
        switch (logicType)
        {
            case DynamicLogicType.AND: return left && right;
            case DynamicLogicType.OR: return left || right;
            case DynamicLogicType.EXCLUSIVE_OR: return left != right;
            case DynamicLogicType.CONDITIONAL: return !left || right;
            case DynamicLogicType.BICONDITIONAL: return left == right;
            default: return false;
        }
    }

    // =========================================================
    // MASKING AND BARRIER SYSTEM (STATE ENGINE)
    // =========================================================

    private void UpdateColumnMasking()
    {
        if (puzzleMode == PuzzleMode.HardMode)
        {
            SetBarrierStateHardMode(pBarrier, DynamicPuzzleColumn.P);
            SetBarrierStateHardMode(qBarrier, DynamicPuzzleColumn.Q);
            SetBarrierStateHardMode(outputPQBarrier, DynamicPuzzleColumn.OUTPUT_PQ);
            SetBarrierStateHardMode(outputQPBarrier, DynamicPuzzleColumn.OUTPUT_QP);
        }
        else
        {
            // --- EASY MODE PROGRESSIVE TOPIC MASKING ---
            
            // Set everything to locked default first
            SetBarrierVisual(pBarrier, true, lockedMaterial);
            SetBarrierVisual(qBarrier, true, lockedMaterial);
            SetBarrierVisual(outputPQBarrier, true, lockedMaterial);
            SetBarrierVisual(outputQPBarrier, true, lockedMaterial); // Column 4 is completely unused in Easy Mode

            if (puzzleCompleted)
            {
                SetBarrierVisual(pBarrier, true, completedMaterial);
                SetBarrierVisual(qBarrier, true, completedMaterial);
                SetBarrierVisual(outputPQBarrier, true, completedMaterial);
                return;
            }

            switch (easyModeStep)
            {
                case 0: // Conjunction (Column 1 Open, others Locked)
                    SetBarrierVisual(pBarrier, false, null); 
                    break;

                case 1: // Disjunction (Column 2 Open, Column 1 Protected/Completed)
                    SetBarrierVisual(pBarrier, true, completedMaterial);
                    SetBarrierVisual(qBarrier, false, null); 
                    break;

                case 2: // Exclusive OR (Column 3 Open, Columns 1 & 2 Protected/Completed)
                    SetBarrierVisual(pBarrier, true, completedMaterial);
                    SetBarrierVisual(qBarrier, true, completedMaterial);
                    SetBarrierVisual(outputPQBarrier, false, null); 
                    break;

                case 3: // Implication (Column 1 Cleared & Open Again, Column 3 Protected/Completed)
                    SetBarrierVisual(pBarrier, false, null); 
                    SetBarrierVisual(outputPQBarrier, true, completedMaterial);
                    break;

                case 4: // Biconditional (Column 2 Cleared & Open Again, Columns 1 & 3 Protected/Completed)
                    SetBarrierVisual(pBarrier, true, completedMaterial);
                    SetBarrierVisual(qBarrier, false, null); 
                    SetBarrierVisual(outputPQBarrier, true, completedMaterial);
                    break;
            }
        }
    }

    private void SetBarrierStateHardMode(GameObject barrier, DynamicPuzzleColumn barrierColumn)
    {
        if (barrier == null) return;

        if (puzzleCompleted)
        {
            SetBarrierVisual(barrier, true, completedMaterial);
            return;
        }

        if (currentColumn == barrierColumn)
            SetBarrierVisual(barrier, false, null);
        else if (currentColumn > barrierColumn)
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
    // SYSTEM UTILITIES
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

        // ---> ADD THIS CRITICAL FIX HERE <---
        // Tell the inventory to stop tracking and hiding this block!
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

        Debug.Log("Easy Mode Progressive Puzzle Finished!");

        if (reviewCoroutine != null)
            StopCoroutine(reviewCoroutine);

        reviewCoroutine = StartCoroutine(ReviewThenReturn());
        
    }

    private IEnumerator ReviewThenReturn()
    {
        yield return new WaitForSeconds(10f);

        TruthBlock[] blocks = FindObjectsByType<TruthBlock>(FindObjectsSortMode.None);
        foreach (var block in blocks)
        {
            block.StopAllCoroutines();
            // block.ReturnToOrigin(false);
            Destroy(block.gameObject);
        }

        ResetPuzzle();
        reviewCoroutine = null;   
    }

    private void ResetPuzzle()
    {
        currentColumn = DynamicPuzzleColumn.P;
        easyModeStep = 0;
        currentRow = 0;

        placedP = new bool[4];
        placedQ = new bool[4];

        puzzleCompleted = false;
        isProcessingPlacement = false;

        UpdateColumnMasking(); 
        UpdateColumnHeaderLabels();
        UpdateColumnHeaderLabels();

        if (puzzleMode == PuzzleMode.EasyMode && blockSpawner != null)
        {
            blockSpawner.SpawnBlocksForStep(easyModeStep);
        }
    }

    public void SetActiveState(bool state)
    {
        isActivePuzzle = state;
    }
}