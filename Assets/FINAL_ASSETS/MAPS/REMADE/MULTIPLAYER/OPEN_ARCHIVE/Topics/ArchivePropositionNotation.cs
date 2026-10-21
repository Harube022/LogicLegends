namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Notation for the five T1 connectives. Explanations appear only after a correct answer.</summary>
    public static class ArchivePropositionNotation
    {
        public static string Symbol(string name)
        {
            switch((name??"").Trim())
            {
                case "Conjunction": return "∧";
                case "Disjunction": return "∨";
                case "Exclusive OR": return "⊕";
                case "Conditional": return "→";
                case "Biconditional": return "↔";
                default: return "·";
            }
        }
        public static string Explanation(string name)
        {
            switch((name??"").Trim())
            {
                case "Conjunction": return "P ∧ Q — Conjunction (AND) is true only when both P and Q are true.";
                case "Disjunction": return "P ∨ Q — Disjunction (inclusive OR) is true when at least one statement is true. Both may be true.";
                case "Exclusive OR": return "P ⊕ Q — Exclusive OR (XOR) is true when exactly one statement is true. Two true statements give false.";
                case "Conditional": return "P → Q — Conditional (if P, then Q) is false only when P is true and Q is false.";
                case "Biconditional": return "P ↔ Q — Biconditional (if and only if) is true when P and Q have the same truth value: both true or both false.";
                default: return "";
            }
        }
    }
}
