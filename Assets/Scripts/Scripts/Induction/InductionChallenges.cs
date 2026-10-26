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
        public const int ChallengesPerRun = 5;
        public const int VariantsPerChallenge = 10;

        // A term is a*i+b. Each round has ten different theorems but keeps its
        // own proof skill and number of required scroll placements.
        static readonly (int a, int b)[][] Families =
        {
            new[] { (1,0), (2,-1), (2,0), (3,-1), (3,0), (1,1), (2,1), (4,-1), (3,1), (4,1) },
            new[] { (2,2), (3,2), (4,2), (5,1), (3,3), (4,3), (5,2), (6,1), (6,2), (7,1) },
            new[] { (3,4), (4,4), (5,3), (6,3), (7,2), (5,4), (6,4), (7,3), (8,2), (8,3) },
            new[] { (1,0), (2,0), (3,0), (4,0), (5,0), (6,0), (7,0), (8,0), (9,0), (10,0) },
            new[] { (2,1), (3,1), (3,2), (4,1), (4,2), (5,1), (5,2), (5,3), (6,1), (6,2) }
        };

        public static readonly InductionChallenge[][] VariantSets = BuildVariants();
        public static readonly InductionChallenge[] All = VariantSets.Select(set => set[0]).ToArray();

        static FormulaTabletData T(string id, string expression) => new FormulaTabletData(id, expression);
        static Dictionary<string, string> Keys(params string[] pairs)
        {
            var result = new Dictionary<string, string>();
            for (int i = 0; i < pairs.Length; i += 2) result.Add(pairs[i], pairs[i + 1]);
            return result;
        }
        static string Term(int a, int b, string variable)
        {
            string term = (a == 1 ? "" : a.ToString()) + variable;
            return b == 0 ? term : term + (b > 0 ? " + " + b : " − " + (-b));
        }
        static string Formula(int a, int b, string n) => n + "(" + Term(a, a + 2 * b, n) + ")/2";
        static string Statement(int a, int b) => "S(n) = (" + (a + b) + ") + (" + (2 * a + b) + ") + ⋯ + (" + Term(a, b, "n") + ") = " + Formula(a, b, "n") + ", for all integers n ≥ 1";
        static InductionChallenge[][] BuildVariants()
        {
            var result = new InductionChallenge[ChallengesPerRun][];
            for (int round = 0; round < ChallengesPerRun; round++)
            {
                result[round] = new InductionChallenge[VariantsPerChallenge];
                for (int variant = 0; variant < VariantsPerChallenge; variant++)
                {
                    var family = Families[round][variant];
                    result[round][variant] = Build(round, variant, family.a, family.b);
                }
            }
            return result;
        }
        static InductionChallenge Build(int round, int variant, int a, int b)
        {
            string prefix = "r" + (round + 1) + "v" + (variant + 1) + "-";
            string theorem = Statement(a, b);
            string hypothesis = "Assume S(k) = " + Formula(a, b, "k");
            string goal = "S(k + 1) = " + Formula(a, b, "(k + 1)");
            string nextTerm = "(" + Term(a, b, "(k + 1)") + ")";
            if (round == 0)
            {
                int first = a + b;
                return new InductionChallenge {
                    title = "Verify the Base Case", theorem = theorem,
                    instructions = "Substitute n = 1.\nPlace the true equality at Base Case. Tap Submit.",
                    choices = new[] { T(prefix + "base-low", "S(1) = " + first + " = " + (first - 1)),
                        T(prefix + "base", "S(1) = " + first + " = (" + a + " + " + (a + 2 * b) + ")/2"),
                        T(prefix + "base-high", "S(1) = " + first + " = " + (first + 1)) },
                    answers = Keys("Base:1", prefix + "base") };
            }
            if (round == 1)
                return new InductionChallenge {
                    title = "Identify the Inductive Hypothesis", theorem = theorem,
                    instructions = "Assume P(k) for arbitrary k ≥ 1.\nChoose the statement about S(k).",
                    choices = new[] { T(prefix + "hypothesis", hypothesis),
                        T(prefix + "hyp-base", "Assume S(1) = " + (a + b)),
                        T(prefix + "hyp-goal", "Assume " + goal) },
                    answers = Keys("Hypothesis:1", prefix + "hypothesis") };
            if (round == 2)
                return new InductionChallenge {
                    title = "Identify the Inductive Goal", theorem = theorem,
                    instructions = "Assume P(k).\nChoose the goal P(k + 1). Place it at Inductive Step.",
                    choices = new[] { T(prefix + "goal-old", "S(k) = " + Formula(a, b, "k")),
                        T(prefix + "goal", goal),
                        T(prefix + "goal-wrong", "S(k + 1) = k(" + Term(a, a + 2 * b, "(k + 1)") + ")/2") },
                    answers = Keys("Step:1", prefix + "goal") };
            if (round == 3)
            {
                string add = a == 1 ? "(k + 1)" : a + "(k + 1)";
                string coefficient = a == 1 ? "" : a.ToString();
                return new InductionChallenge {
                    title = "Complete the Inductive Step", theorem = theorem,
                    instructions = "Assume P(k). Arrange four lines: append the next term, substitute P(k), combine over 2, then factor.",
                    choices = new[] { T(prefix + "step-1", "S(k + 1) = S(k) + " + add),
                        T(prefix + "step-2", "= " + coefficient + "k(k + 1)/2 + " + add),
                        T(prefix + "step-3", "= [" + coefficient + "k(k + 1) + " + (2 * a) + "(k + 1)]/2"),
                        T(prefix + "step-4", "= " + coefficient + "(k + 1)(k + 2)/2") },
                    answers = Keys("Step:1", prefix + "step-1", "Step:2", prefix + "step-2",
                        "Step:3", prefix + "step-3", "Step:4", prefix + "step-4") };
            }
            return new InductionChallenge {
                title = "Complete a Full Induction Proof", theorem = theorem,
                instructions = "Complete Base Case and Hypothesis.\nOrder four lines proving P(k + 1).\nUse all three altars, then Submit.",
                choices = new[] { T(prefix + "full-base", "S(1) = " + (a + b) + " = (" + a + " + " + (a + 2 * b) + ")/2"),
                    T(prefix + "full-hyp", hypothesis),
                    T(prefix + "full-step-1", "S(k + 1) = S(k) + " + nextTerm),
                    T(prefix + "full-step-2", "= " + Formula(a, b, "k") + " + " + nextTerm),
                    T(prefix + "full-step-3", "= [k(" + Term(a, a + 2 * b, "k") + ") + 2" + nextTerm + "]/2"),
                    T(prefix + "full-step-4", "= " + Formula(a, b, "(k + 1)")) },
                answers = Keys("Base:1", prefix + "full-base", "Hypothesis:1", prefix + "full-hyp",
                    "Step:1", prefix + "full-step-1", "Step:2", prefix + "full-step-2",
                    "Step:3", prefix + "full-step-3", "Step:4", prefix + "full-step-4") };
        }

        public static int[] SelectRun(Random random, IReadOnlyList<int> previous)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            var selected = new int[ChallengesPerRun];
            for (int round = 0; round < selected.Length; round++)
            {
                int prior = previous != null && round < previous.Count ? previous[round] : -1;
                bool validPrior = prior >= 0 && prior < VariantsPerChallenge;
                int pick = random.Next(VariantsPerChallenge - (validPrior ? 1 : 0));
                selected[round] = validPrior && pick >= prior ? pick + 1 : pick;
            }
            return selected;
        }

        public static string[] IncorrectSlots(InductionChallenge challenge, IEnumerable<InductionSlot> slots)
        {
            var placed = slots.ToDictionary(s => s.key, s => s.Tablet);
            return challenge.answers.Where(a => !placed.ContainsKey(a.Key) || placed[a.Key] == null || placed[a.Key].Data.id != a.Value)
                .Select(a => a.Key).Concat(slots.Where(s => s.Tablet != null && !challenge.answers.ContainsKey(s.key)).Select(s => s.key)).Distinct().ToArray();
        }
    }
}
