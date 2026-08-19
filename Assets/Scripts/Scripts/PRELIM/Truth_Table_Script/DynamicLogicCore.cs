using UnityEngine;

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
    EasyMode,
    HardMode
}

// NEW: Data structure to hold combinational logic
public struct ComplexLogicExpression
{
    public DynamicLogicType LeftSub;
    public DynamicLogicType RightSub;
    public DynamicLogicType MainOperator;
}

public interface IPuzzlePhase
{
    void StartPhase();
    void HandleTryPlace(TruthBlock block, int columnIndex);
    Transform GetActiveSnapPoint();
    void UpdateHeaders();
    void UpdateMasking();
}

public static class LogicUtility
{
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

    // NEW: Evaluates the combined logic
    public static bool EvaluateComplexLogic(ComplexLogicExpression expr, bool p, bool q)
    {
        bool leftResult = EvaluateLogic(expr.LeftSub, p, q);
        bool rightResult = EvaluateLogic(expr.RightSub, p, q);
        return EvaluateLogic(expr.MainOperator, leftResult, rightResult);
    }

    public static string GetLogicSymbolString(DynamicLogicType type, string left, string right)
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

    // NEW: Generates the UI string format: (P ∧ Q) ↔ (P ∨ Q)
    public static string GetComplexLogicString(ComplexLogicExpression expr)
    {
        string leftStr = GetLogicSymbolString(expr.LeftSub, "P", "Q");
        string rightStr = GetLogicSymbolString(expr.RightSub, "P", "Q");
        string mainOpStr = GetOperatorSymbol(expr.MainOperator);

        return $"({leftStr}) {mainOpStr} ({rightStr})";
    }

    // NEW: Helper to get just the math symbol
    private static string GetOperatorSymbol(DynamicLogicType type)
    {
        switch (type)
        {
            case DynamicLogicType.AND:          return "∧";
            case DynamicLogicType.OR:           return "∨";
            case DynamicLogicType.EXCLUSIVE_OR: return "⊕";
            case DynamicLogicType.CONDITIONAL:  return "→";
            case DynamicLogicType.BICONDITIONAL:return "↔";
            default:                            return "";
        }
    }
}