using UnityEngine;
using System.Collections;

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

public class DynamicLogicPuzzle : MonoBehaviour
{
    private bool isProcessingPlacement = false;
    private bool puzzleCompleted = false;
    private Coroutine reviewCoroutine;
    private bool isActivePuzzle = true;

    [Header("Logic Type")]
    [SerializeField] private DynamicLogicType logicType;
    public DynamicLogicType PuzzleLogicType => logicType;

    [Header("Expected Values (Top → Bottom)")]
    [SerializeField] private bool[] expectedP = new bool[4];
    [SerializeField] private bool[] expectedQ = new bool[4];

    [Header("Snap Points")]
    [SerializeField] private Transform[] pSnapPoints;
    [SerializeField] private Transform[] qSnapPoints;
    [SerializeField] private Transform[] outputPQSnapPoints;
    [SerializeField] private Transform[] outputQPSnapPoints;

    [Header("Dynamic Column Masking Barriers")]
    [SerializeField] private GameObject pBarrier;
    [SerializeField] private GameObject qBarrier;
    [SerializeField] private GameObject outputPQBarrier;
    [SerializeField] private GameObject outputQPBarrier;

    [Header("Barrier Visual Aesthetics")]
    [SerializeField] private Material lockedMaterial;     
    [SerializeField] private Material completedMaterial;  

    private DynamicPuzzleColumn currentColumn = DynamicPuzzleColumn.P;
    private int currentRow = 0;

    private bool[] placedP;
    private bool[] placedQ;

    private void Start()
    {
        placedP = new bool[4];
        placedQ = new bool[4];
        UpdateColumnMasking();
    }

    // =========================================================
    // ENTRY
    // =========================================================

    public void TryPlace(TruthBlock block, DynamicPuzzleColumn column)
    {
        if (!isActivePuzzle || puzzleCompleted || isProcessingPlacement)
        {
            block.ReturnToOrigin(true);
            return;
        }

        if (column != currentColumn)
        {
            block.ReturnToOrigin(true);
            return;
        }

        isProcessingPlacement = true;

        switch (currentColumn)
        {
            case DynamicPuzzleColumn.P:
                TryPlaceP(block);
                break;

            case DynamicPuzzleColumn.Q:
                TryPlaceQ(block);
                break;

            case DynamicPuzzleColumn.OUTPUT_PQ:
                TryPlaceOutputPQ(block);
                break;

            case DynamicPuzzleColumn.OUTPUT_QP:
                TryPlaceOutputQP(block);
                break;
        }

        isProcessingPlacement = false;
    }

    // =========================================================
    // COLUMN LOGIC
    // =========================================================

    private void TryPlaceP(TruthBlock block)
    {
        if (block.value != expectedP[currentRow])
        {
            block.ReturnToOrigin(true);
            return;
        }

        LockBlock(block, pSnapPoints[currentRow]);
        placedP[currentRow] = block.value;
        Advance();
    }

    private void TryPlaceQ(TruthBlock block)
    {
        if (block.value != expectedQ[currentRow])
        {
            block.ReturnToOrigin(true);
            return;
        }

        LockBlock(block, qSnapPoints[currentRow]);
        placedQ[currentRow] = block.value;
        Advance();
    }

    private void TryPlaceOutputPQ(TruthBlock block)
    {
        if (currentRow >= outputPQSnapPoints.Length) return;

        bool correct = EvaluateLogic(placedP[currentRow], placedQ[currentRow]);

        if (block.value != correct)
        {
            block.ReturnToOrigin(true);
            return;
        }

        LockBlock(block, outputPQSnapPoints[currentRow]);
        Advance();
    }

    private void TryPlaceOutputQP(TruthBlock block)
    {
        if (currentRow >= outputQPSnapPoints.Length) return;

        bool correct = EvaluateLogic(placedQ[currentRow], placedP[currentRow]);

        if (block.value != correct)
        {
            block.ReturnToOrigin(true);
            return;
        }

        LockBlock(block, outputQPSnapPoints[currentRow]);
        Advance();
    }

    // =========================================================
    // PROGRESSION & MASKING CORE LOGIC
    // =========================================================

    private void Advance()
    {
        currentRow++;

        if (currentRow < 4)
            return;

        currentRow = 0;

        switch (currentColumn)
        {
            case DynamicPuzzleColumn.P:
                currentColumn = DynamicPuzzleColumn.Q;
                break;

            case DynamicPuzzleColumn.Q:
                currentColumn = DynamicPuzzleColumn.OUTPUT_PQ;
                break;

            case DynamicPuzzleColumn.OUTPUT_PQ:
                currentColumn = DynamicPuzzleColumn.OUTPUT_QP;
                break;

            case DynamicPuzzleColumn.OUTPUT_QP:
                CompletePuzzle();
                return;
        }

        UpdateColumnMasking(); 
    }

    private void UpdateColumnMasking()
    {
        SetBarrierState(pBarrier, DynamicPuzzleColumn.P);
        SetBarrierState(qBarrier, DynamicPuzzleColumn.Q);
        SetBarrierState(outputPQBarrier, DynamicPuzzleColumn.OUTPUT_PQ);
        SetBarrierState(outputQPBarrier, DynamicPuzzleColumn.OUTPUT_QP);
    }

    private void SetBarrierState(GameObject barrier, DynamicPuzzleColumn barrierColumn)
    {
        if (barrier == null) return;

        MeshRenderer renderer = barrier.GetComponent<MeshRenderer>();
        Collider col = barrier.GetComponent<Collider>();

        if (puzzleCompleted)
        {
            barrier.SetActive(true);
            if (renderer != null && completedMaterial != null) renderer.material = completedMaterial;
            if (col != null) col.enabled = true; 
            return;
        }

        if (currentColumn == barrierColumn)
        {
            barrier.SetActive(false); 
            if (col != null) col.enabled = false; 
        }
        else if (currentColumn > barrierColumn)
        {
            barrier.SetActive(true); 
            if (renderer != null && completedMaterial != null) renderer.material = completedMaterial;
            if (col != null) col.enabled = true; 
        }
        else
        {
            barrier.SetActive(true); 
            if (renderer != null && lockedMaterial != null) renderer.material = lockedMaterial;
            if (col != null) col.enabled = true; 
        }
    }

    // =========================================================
    // MATHEMATICAL LOGICAL OPERATORS
    // =========================================================

    private bool EvaluateLogic(bool left, bool right)
    {
        switch (logicType)
        {
            case DynamicLogicType.AND: 
                return left && right;

            case DynamicLogicType.OR:  
                return left || right;

            case DynamicLogicType.EXCLUSIVE_OR: 
                return left != right; 

            case DynamicLogicType.CONDITIONAL:  
                return !left || right; 

            case DynamicLogicType.BICONDITIONAL: 
                return left == right; 

            default: 
                return false;
        }
    }

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
    }

    private void CompletePuzzle()
    {
        if (puzzleCompleted) return;

        puzzleCompleted = true;
        UpdateColumnMasking();

        Debug.Log("Dynamic Puzzle Finished!");

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
            block.ReturnToOrigin(false);
        }

        ResetPuzzle();
        reviewCoroutine = null;   
    }

    private void ResetPuzzle()
    {
        currentColumn = DynamicPuzzleColumn.P;
        currentRow = 0;

        placedP = new bool[4];
        placedQ = new bool[4];

        puzzleCompleted = false;
        isProcessingPlacement = false;

        UpdateColumnMasking(); 
    }

    public void SetActiveState(bool state)
    {
        isActivePuzzle = state;
    }
}