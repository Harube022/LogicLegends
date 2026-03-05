using UnityEngine;
using System.Collections;

public enum LogicType
{
    AND,
    OR,
    IMPLICATION,
    BICONDITIONAL,
    NOT
}

public enum PuzzleColumn
{
    P,
    Q,
    OUTPUT_PQ,
    OUTPUT_QP
}

public class LogicPuzzle : MonoBehaviour
{
    private bool isProcessingPlacement = false;
    private bool puzzleCompleted = false;
    private Coroutine reviewCoroutine;
    private PuzzleManager puzzleManager;
    private bool isActivePuzzle = true;

    [Header("Logic Type")]
    [SerializeField] private LogicType logicType;
    // Add this so the PuzzleManager can read the type!
    public LogicType PuzzleLogicType => logicType;

    [Header("Expected Values (Top → Bottom)")]
    [SerializeField] private bool[] expectedP = new bool[4];
    [SerializeField] private bool[] expectedQ = new bool[4];

    [Header("Snap Points")]
    [SerializeField] private Transform[] pSnapPoints;
    [SerializeField] private Transform[] qSnapPoints;
    [SerializeField] private Transform[] outputPQSnaPoints;
    [SerializeField] private Transform[] outputQPSnapPoints;

    private PuzzleColumn currentColumn = PuzzleColumn.P;
    private int currentRow = 0;

    private bool[] placedP;
    private bool[] placedQ;

    // =========================================================
    // ENTRY
    // =========================================================

    public void TryPlace(TruthBlock block, PuzzleColumn column)
    {
        if (!isActivePuzzle)
        {
            block.ReturnToOrigin(true);
            Debug.Log("Complete previous puzzle first.");
            return;
        }
        if (puzzleCompleted)
            return;

        if (isProcessingPlacement)
            return;

        if (column != currentColumn)
        {
            block.ReturnToOrigin(true);
            return;
        }

        isProcessingPlacement = true;

        switch (currentColumn)
        {
            case PuzzleColumn.P:
                TryPlaceP(block);
                break;

            case PuzzleColumn.Q:
                TryPlaceQ(block);
                break;

            case PuzzleColumn.OUTPUT_PQ:
                TryPlaceOutputPQ(block);
                break;

            case PuzzleColumn.OUTPUT_QP:
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
        if (currentRow >= outputPQSnaPoints.Length)
        {
            Debug.LogError("Row overflow!");
            return;
        }

        bool correct;

        if (logicType == LogicType.NOT)
        {
            // OUTPUT_PQ behaves as ¬P
            correct = !placedP[currentRow];
        }
        else
        {
            // Normal binary evaluation (AND, OR, etc.)
            correct = EvaluateLogic(
                placedP[currentRow],
                placedQ[currentRow]
            );
        }

        if (block.value != correct)
        {
            block.ReturnToOrigin(true);
            return;
        }

        LockBlock(block, outputPQSnaPoints[currentRow]);

        Advance();
    }

    private void TryPlaceOutputQP(TruthBlock block)
    {
        if (currentRow >= outputQPSnapPoints.Length)
        {
            Debug.LogError("Row overflow!");
            return;
        }

        bool correct;

        if (logicType == LogicType.NOT)
        {
            // OUTPUT_QP behaves as ¬Q
            correct = !placedQ[currentRow];
        }
        else
        {
            // For commutative gates (AND/OR)
            correct = EvaluateLogic(
                placedQ[currentRow],
                placedP[currentRow]
            );
        }

        if (block.value != correct)
        {
            block.ReturnToOrigin(true);
            return;
        }

        LockBlock(block, outputQPSnapPoints[currentRow]);

        Advance();
    }

    // =========================================================
    // PROGRESSION
    // =========================================================

    private void Advance()
    {
        currentRow++;

        if (currentRow < 4)
            return;

        currentRow = 0;

        switch (currentColumn)
        {
            case PuzzleColumn.P:
                currentColumn = PuzzleColumn.Q;
                break;

            case PuzzleColumn.Q:
                currentColumn = PuzzleColumn.OUTPUT_PQ;
                break;

            case PuzzleColumn.OUTPUT_PQ:
                currentColumn = PuzzleColumn.OUTPUT_QP;
                break;

            case PuzzleColumn.OUTPUT_QP:
                CompletePuzzle();
                return;
        }

        Debug.Log("Now filling: " + currentColumn);
    }

    // =========================================================
    // LOGIC
    // =========================================================

    private bool EvaluateLogic(bool p, bool q)
    {
        switch (logicType)
        {
            case LogicType.AND:
                return p && q;

            case LogicType.OR:
                return p || q;

            case LogicType.IMPLICATION:
                return !p || q;

            case LogicType.BICONDITIONAL:
                return p == q;

            case LogicType.NOT:
                return !p;

            default:
                return false;
        }
    }

    // =========================================================
    // LOCK
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
    }

    // =========================================================
    // COMPLETE
    // =========================================================

    private void CompletePuzzle()
    {
        if (puzzleCompleted)
            return;

        puzzleCompleted = true;

        Debug.Log("Puzzle Finished! Review time started.");

        if (puzzleManager != null)
            puzzleManager.NotifyPuzzleCompleted(this);

        if (reviewCoroutine != null)
            StopCoroutine(reviewCoroutine);

        reviewCoroutine = StartCoroutine(ReviewThenReturn());
    }

    private IEnumerator ReviewThenReturn()
    {
        Debug.Log("Review time started!");

        float remaining = 10f;

        while (remaining > 0)
        {
            Debug.Log("Review time remaining: " + Mathf.CeilToInt(remaining) + "s");
            yield return new WaitForSeconds(1f);
            remaining--;
        }

        Debug.Log("Review time ended. Resetting puzzle.");

        TruthBlock[] blocks = FindObjectsByType<TruthBlock>(FindObjectsSortMode.None);

        foreach (var block in blocks)
        {
            block.StopAllCoroutines();
            block.ReturnToOrigin(false);
        }

        ResetPuzzle();

        reviewCoroutine = null;   // <-- IMPORTANT
    }

    private void Start()
    {
        placedP = new bool[4];
        placedQ = new bool[4];
        puzzleManager = FindFirstObjectByType<PuzzleManager>();
    }

    private void ResetPuzzle()
    {
        currentColumn = PuzzleColumn.P;
        currentRow = 0;

        placedP = new bool[4];
        placedQ = new bool[4];

        puzzleCompleted = false;
        isProcessingPlacement = false;

        Debug.Log("Puzzle Reset Complete");
    }

    public void SetActiveState(bool state)
    {
        isActivePuzzle = state;
    }
}