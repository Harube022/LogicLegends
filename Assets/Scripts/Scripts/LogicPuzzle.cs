using UnityEngine;
using System.Collections.Generic;

public enum LogicType
{
    AND,
    OR,
    NOT,
    IMPLICATION,
    BICONDITIONAL
}

public class LogicPuzzle : MonoBehaviour
{
    [SerializeField] private LogicType logicType;
    [SerializeField] private List<LogicRow> rows;

    private int currentRowIndex = 0;

    public bool TryPlace(TruthBlock block)
    {
        if (currentRowIndex >= rows.Count)
            return false; // puzzle already complete

        LogicRow currentRow = rows[currentRowIndex];

        bool correct = EvaluateRow(currentRow, block.value);

        if (!correct)
            return false; // reject placement

        currentRow.LockIn(block);
        currentRowIndex++;

        return true;
    }

    private bool EvaluateRow(LogicRow row, bool placedValue)
    {
        bool expected;

        switch (logicType)
        {
            case LogicType.AND:
                expected = row.inputA && row.inputB;
                break;

            case LogicType.OR:
                expected = row.inputA || row.inputB;
                break;

            case LogicType.NOT:
                expected = !row.inputA;
                break;

            case LogicType.IMPLICATION:
                expected = !row.inputA || row.inputB;
                break;

            case LogicType.BICONDITIONAL:
                expected = row.inputA == row.inputB;
                break;

            default:
                expected = false;
                break;
        }

        return placedValue == expected;
    }
}