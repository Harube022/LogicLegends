using System;
using System.Collections.Generic;

namespace LogicLegends.Inference
{
    /// <summary>Equivalence practice adapted from IT 105, Prelim Week 4 (slides 4–24).
    /// These questions do not select the inference rule needed by the stage puzzle.</summary>
    public static class InferencePickupQuestions
    {
        public sealed class Question
        {
            public readonly string Id, Prompt, Explanation, LeftFormula, RightFormula;
            public readonly string[] Choices;
            public readonly int SourceSlide;
            // The bank's correct choice is always first. A Draw shuffles indices, never answer text.
            public Question(string id, int slide, string prompt, string explanation, string correct,
                string b, string c, string d, string left = null, string right = null)
            {
                Id = id; SourceSlide = slide; Prompt = prompt; Explanation = explanation;
                Choices = new[] { correct, b, c, d }; LeftFormula = left; RightFormula = right;
            }
        }

        public static readonly Question[] Bank = {
            new Question("DM-description-and", 5, "Which law says that negating an AND statement gives an OR statement with both parts negated?",
                "De Morgan's law changes NOT (P AND Q) into (NOT P) OR (NOT Q).", "De Morgan's law", "Commutativity", "Associativity", "Exportation"),
            new Question("DM-description-or", 6, "Which law says that negating an OR statement gives an AND statement with both parts negated?",
                "De Morgan's law changes NOT (P OR Q) into (NOT P) AND (NOT Q).", "De Morgan's law", "Distribution", "Material implication", "Transposition"),
            new Question("DM-formula-and", 5, "Identify the law:\n¬(P ∧ Q) ↔ (¬P ∨ ¬Q)",
                "Negate both parts and change AND to OR: De Morgan's law.", "De Morgan's law", "Material equivalence", "Commutativity", "Exportation", "!(p&q)", "!p|!q"),
            new Question("DM-formula-or", 6, "Identify the law:\n¬(P ∨ Q) ↔ (¬P ∧ ¬Q)",
                "Negate both parts and change OR to AND: De Morgan's law.", "De Morgan's law", "Distribution", "Associativity", "Transposition", "!(p|q)", "!p&!q"),
            new Question("COMM-description", 8, "Which law allows the order of two propositions joined by AND or OR to be reversed without changing the result?",
                "Commutativity changes order; associativity changes grouping.", "Commutativity", "Associativity", "Material implication", "Distribution"),
            new Question("COMM-formula", 8, "Identify the law:\n(P ∨ Q) ↔ (Q ∨ P)",
                "Only the order of P and Q changes: commutativity.", "Commutativity", "Associativity", "De Morgan's law", "Material equivalence", "p|q", "q|p"),
            new Question("ASSOC-description", 10, "Which law changes the grouping of three propositions joined by the same connective, while keeping their order?",
                "Associativity moves parentheses without changing the sequence of propositions.", "Associativity", "Commutativity", "Distribution", "Exportation"),
            new Question("ASSOC-formula", 12, "Identify the law:\n((P ∨ Q) ∨ R) ↔ (P ∨ (Q ∨ R))",
                "The grouping changes, but P, Q, R remain in the same order: associativity.", "Associativity", "Commutativity", "Material implication", "De Morgan's law", "(p|q)|r", "p|(q|r)"),
            new Question("DIST-description", 14, "Which law expands one proposition joined to a grouped expression by combining that proposition with each part of the group?",
                "Distribution describes how AND distributes over OR, and OR over AND.", "Distribution", "Transposition", "Commutativity", "Material equivalence"),
            new Question("DIST-formula", 16, "Identify the law:\n(P ∧ (Q ∨ R)) ↔ ((P ∧ Q) ∨ (P ∧ R))",
                "AND distributes over OR, repeating P in the two groups.", "Distribution", "Associativity", "Exportation", "De Morgan's law", "p&(q|r)", "(p&q)|(p&r)"),
            new Question("TRANS-description", 18, "Which equivalence reverses a conditional's antecedent and consequent and negates both?",
                "Transposition replaces P → Q with its contrapositive, ¬Q → ¬P.", "Transposition", "Material implication", "Material equivalence", "Exportation"),
            new Question("TRANS-formula", 18, "Identify the law:\n(P → Q) ↔ (¬Q → ¬P)",
                "A conditional is equivalent to its contrapositive: transposition.", "Transposition", "Commutativity", "Material implication", "Distribution", "p->q", "!q->!p"),
            new Question("IMPL-description", 20, "Which law rewrites a conditional as an OR statement with its antecedent negated?",
                "Material implication rewrites P → Q as ¬P ∨ Q.", "Material implication", "Material equivalence", "Transposition", "Associativity"),
            new Question("IMPL-formula", 20, "Identify the law:\n(P → Q) ↔ (¬P ∨ Q)",
                "A conditional is false only when P is true and Q is false: material implication.", "Material implication", "Transposition", "Exportation", "De Morgan's law", "p->q", "!p|q"),
            new Question("EQUIV-description", 22, "Which law expresses 'P if and only if Q' as the conjunction of the implications in both directions?",
                "Material equivalence requires both P → Q and Q → P.", "Material equivalence", "Material implication", "Exportation", "Distribution"),
            // Slide 22's explanation describes a biconditional. Its printed left-hand
            // single implication is a typo; the mathematically valid form is used here.
            new Question("EQUIV-formula", 22, "Identify the law:\n(P ↔ Q) ↔ ((P → Q) ∧ (Q → P))",
                "The biconditional means that each proposition implies the other: material equivalence.", "Material equivalence", "Material implication", "Commutativity", "Transposition", "p<->q", "(p->q)&(q->p)"),
            new Question("EXP-description", 24, "Which law changes an implication with a conjunction as its antecedent into a nested implication?",
                "Exportation transforms (P ∧ Q) → R into P → (Q → R), and conversely.", "Exportation", "Distribution", "Associativity", "Transposition"),
            new Question("EXP-formula", 24, "Identify the law:\n((P ∧ Q) → R) ↔ (P → (Q → R))",
                "The conjunction in the antecedent becomes two nested implications: exportation.", "Exportation", "Material equivalence", "Distribution", "De Morgan's law", "(p&q)->r", "p->(q->r)"),
            // The requested tautology terminology explains why the slides' equivalences hold.
            new Question("TAUTOLOGY-description", 5, "A proposition is true for every possible assignment of truth values. What is it called?",
                "A tautology is always true. A contradiction is always false; a contingency can be either.", "Tautology", "Contradiction", "Contingency", "An unassigned proposition"),
            new Question("TAUTOLOGY-formula", 6, "What kind of proposition is\n¬(P ∨ Q) ↔ (¬P ∧ ¬Q)?",
                "De Morgan's equivalence holds for every truth assignment, so this biconditional is a tautology.", "Tautology", "Contradiction", "Contingency", "Not a proposition", "!(p|q)", "!p&!q")
        };

        public sealed class Draw
        {
            public readonly Question Question;
            public readonly int[] ChoiceOrder;
            public int CorrectChoice => Array.IndexOf(ChoiceOrder, 0);
            public Draw(Question question, int[] order) { Question = question; ChoiceOrder = order; }
        }

        public sealed class Deck
        {
            readonly Random random;
            readonly List<int> remaining = new List<int>();
            int last = -1;
            public Deck(int seed) { random = new Random(seed); }
            public Draw Next()
            {
                if (remaining.Count == 0)
                {
                    for (int i = 0; i < Bank.Length; i++) remaining.Add(i);
                    Shuffle(remaining);
                    int end = remaining.Count - 1;
                    if (remaining[end] == last) { int t = remaining[0]; remaining[0] = remaining[end]; remaining[end] = t; }
                }
                int selected = remaining[remaining.Count - 1]; remaining.RemoveAt(remaining.Count - 1); last = selected;
                var order = new List<int> { 0, 1, 2, 3 }; Shuffle(order);
                return new Draw(Bank[selected], order.ToArray());
            }
            void Shuffle(List<int> items)
            {
                for (int i = items.Count - 1; i > 0; i--) { int j = random.Next(i + 1); int t = items[i]; items[i] = items[j]; items[j] = t; }
            }
        }
    }
}
