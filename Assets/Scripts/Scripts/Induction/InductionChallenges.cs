using System;
using System.Collections.Generic;
using System.Linq;

namespace LogicLegends.Induction
{
    [Serializable]
    public sealed class FormulaTabletData
    {
        public string id, expression;
        public FormulaTabletData(string id, string expression) { this.id = id; this.expression = expression; }
    }

    public sealed class InductionChallenge
    {
        public string title, theorem, instructions;
        public FormulaTabletData[] choices;
        // Keys identify invisible proof anchors; values identify mathematical data, never mesh names.
        public Dictionary<string, string> answers;
        public bool Uses(string station) => answers.Keys.Any(k => k.StartsWith(station + ":", StringComparison.Ordinal));
    }

    public static class InductionChallenges
    {
        static FormulaTabletData T(string id, string expression) => new FormulaTabletData(id, expression);
        static Dictionary<string, string> Keys(params string[] pairs)
        {
            var result = new Dictionary<string, string>();
            for (int i = 0; i < pairs.Length; i += 2) result.Add(pairs[i], pairs[i + 1]);
            return result;
        }
        public static readonly InductionChallenge[] All =
        {
            new InductionChallenge {
                title = "Verify the Base Case", theorem = "1 + 2 + ⋯ + n = n(n + 1)/2, for all integers n ≥ 1",
                instructions = "P(1): 1 = 1(1 + 1)/2\nSimplify both sides. Place the matching equality at the Base Case altar. Press Submit to check your answer.",
                choices = new[] { T("base-wrong-two", "1 = 2"), T("base", "1 = 1"), T("base-wrong-zero", "1 = 0") },
                answers = Keys("Base:1", "base") },
            new InductionChallenge {
                title = "Identify the Inductive Hypothesis", theorem = "1 + 2 + ⋯ + n = n(n + 1)/2, for all integers n ≥ 1",
                instructions = "Assume P(k) holds for an arbitrary positive integer k. Choose the hypothesis. This is an assumption for the inductive step, not a separate proof of P(k). Press Submit to check your answer.",
                choices = new[] { T("hypothesis", "1 + 2 + ⋯ + k = k(k + 1)/2"), T("hyp-minus", "1 + 2 + ⋯ + k = k(k − 1)/2"), T("hyp-extra", "1 + 2 + ⋯ + k + 1 = k(k + 1)/2") },
                answers = Keys("Hypothesis:1", "hypothesis") },
            new InductionChallenge {
                title = "Identify the Inductive Goal", theorem = "Assume 1 + 2 + ⋯ + k = k(k + 1)/2",
                instructions = "What must you prove for k + 1? Place the statement P(k + 1) at the Inductive Step altar, then press Submit. The step must work for every applicable integer k.",
                choices = new[] { T("goal-old", "1 + 2 + ⋯ + k = k(k + 1)/2"), T("goal", "1 + 2 + ⋯ + k + (k + 1) = (k + 1)(k + 2)/2"), T("goal-wrong", "1 + 2 + ⋯ + k + (k + 1) = k(k + 2)/2") },
                answers = Keys("Step:1", "goal") },
            new InductionChallenge {
                title = "Complete the Inductive Step", theorem = "1 + 2 + ⋯ + n = n(n + 1)/2, for all integers n ≥ 1\nProve P(k) ⇒ P(k + 1) for every integer k ≥ 1",
                instructions = "Arrange four numbered lines above the Inductive Step altar. Use the interaction prompt to choose a line position. Line 2 uses the inductive hypothesis; then combine fractions and factor. Press Submit to check the complete sequence.",
                choices = new[] { T("step-1", "1 + 2 + ⋯ + k + (k + 1)"), T("step-2", "k(k + 1)/2 + (k + 1)"), T("step-3", "[k(k + 1) + 2(k + 1)]/2"), T("step-4", "(k + 1)(k + 2)/2") },
                answers = Keys("Step:1", "step-1", "Step:2", "step-2", "Step:3", "step-3", "Step:4", "step-4") },
            new InductionChallenge {
                title = "Complete a Full Induction Proof", theorem = "1 + 3 + 5 + ⋯ + (2n − 1) = n², for all integers n ≥ 1",
                instructions = "Complete the Base Case and Hypothesis altars and four numbered lines at the Inductive Step altar. Establish P(1), assume P(k), then prove P(k) ⇒ P(k + 1) for every k ≥ 1. Edit all sections before pressing Submit to check the full proof.",
                choices = new[] { T("odd-base", "1 = 1² = 1"), T("odd-hyp", "1 + 3 + ⋯ + (2k − 1) = k²"), T("odd-step-1", "1 + 3 + ⋯ + (2k − 1) + (2k + 1)"), T("odd-step-2", "k² + (2k + 1)"), T("odd-step-3", "k² + 2k + 1"), T("odd-step-4", "(k + 1)²") },
                answers = Keys("Base:1", "odd-base", "Hypothesis:1", "odd-hyp", "Step:1", "odd-step-1", "Step:2", "odd-step-2", "Step:3", "odd-step-3", "Step:4", "odd-step-4") }
        };

        public static string[] IncorrectSlots(InductionChallenge challenge, IEnumerable<InductionSlot> slots)
        {
            var placed = slots.ToDictionary(s => s.key, s => s.Tablet);
            return challenge.answers.Where(a => !placed.ContainsKey(a.Key) || placed[a.Key] == null || placed[a.Key].Data.id != a.Value)
                .Select(a => a.Key).Concat(slots.Where(s => s.Tablet != null && !challenge.answers.ContainsKey(s.key)).Select(s => s.key)).Distinct().ToArray();
        }
    }
}
