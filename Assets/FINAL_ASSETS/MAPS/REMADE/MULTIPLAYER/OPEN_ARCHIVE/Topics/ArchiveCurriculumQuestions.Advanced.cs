using System;
using System.Collections.Generic;
using System.Linq;
using LogicLegends.Induction;
using LogicLegends.Graph;
using LogicLegends.Trees;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    public static partial class ArchiveCurriculumQuestions
    {
        public static readonly string[] References = {
            PropositionReference, TruthReference, InferenceReference,
            "IT 105 · Midterm Week 6, slides 4–16; Prelim Week 3, inference justifications",
            "IT 105 · Midterm Week 7, slides 6–23 (direct, contradiction, contrapositive)",
            "IT 105 · Midterm Weeks 8–9, slides 6–12 and 16–20 (base, hypothesis, inductive step)",
            "IT 105 · Prefinal Weeks 11–12, slides 4–8, 12–18 and 23–30 (membership, operations, complement)",
            "IT 105 · Prefinal Week 13, slides 3–13 and 17–28 (relations, mappings, function properties)",
            "IT 105 · Final Week 16, slides 5–18 (vertices, edges, direction, degree, path, cycle)",
            "IT 105 · Final Weeks 17–18, slides 6–12 and 18–21 (rooted trees, depth, spanning trees)"
        };
        static T Copy<T>(T source) => JsonUtility.FromJson<T>(JsonUtility.ToJson(source));

        public static ProofChallengeData Proof(ArchiveTopicCatalog c, int round, int seed)
        {
            var random = new System.Random(unchecked(seed + round * 331));
            // Four proof excerpts instead of the two-card introductory solo task.
            var pool = c.formalProofs.variantSets[round == 0 ? 1 : round].variants;
            var p = Copy(pool[random.Next(pool.Length)]);
            if (round < 2)
            {
                p.title = round == 0 ? "Compare Proof Presentations" : "Classify Complete Arguments";
                foreach (var scroll in p.scrolls) scroll.title = "Proof excerpt";
            }
            if (round == 3)
            {
                int family = random.Next(1, 5);
                var bank = InferenceBanks[family]; var spec = bank[random.Next(bank.Length)];
                string Display(string s) => s.Replace("->", "→").Replace("!", "¬").Replace("&", "∧").Replace("|", "∨").ToUpperInvariant();
                var rule = c.inferenceRules.First(r => r.abbreviation == spec.RuleCode);
                p.context = string.Join("\n", spec.Premises.Select((s, i) => (i + 1) + ". " + Display(s) + " — Premise")) +
                    "\n∴ " + Display(spec.Conclusion) + " — [MISSING JUSTIFICATION]";
                p.expectedRule = rule.ruleName;
                var others = ArchiveTopicQuestions.Permutation(c.inferenceRules.Length, random.Next()).Select(i => c.inferenceRules[i].ruleName)
                    .Where(s => s != p.expectedRule).Take(3).Append(p.expectedRule).ToArray();
                p.ruleOptions = ArchiveTopicQuestions.Permutation(4, random.Next()).Select(i => others[i]).ToArray();
            }
            if (round == 4)
            {
                string[] premises; string intermediate, conclusion, firstRule, finalRule;
                int[] firstDependencies, finalDependencies;
                switch (random.Next(4))
                {
                    case 0: premises = new[] { "P ∨ Q", "¬P", "Q → R" }; intermediate = "Q"; conclusion = "R";
                        firstRule = "Disjunctive Syllogism from P ∨ Q and ¬P"; finalRule = "Modus Ponens from Q and Q → R";
                        firstDependencies = new[] { 0, 1 }; finalDependencies = new[] { 2, 3 }; break;
                    case 1: premises = new[] { "P → Q", "Q → R", "P" }; intermediate = "P → R"; conclusion = "R";
                        firstRule = "Hypothetical Syllogism from P → Q and Q → R"; finalRule = "Modus Ponens from P and P → R";
                        firstDependencies = new[] { 0, 1 }; finalDependencies = new[] { 2, 3 }; break;
                    case 2: premises = new[] { "P ∨ Q", "Q → R", "¬R" }; intermediate = "¬Q"; conclusion = "P";
                        firstRule = "Modus Tollens from Q → R and ¬R"; finalRule = "Disjunctive Syllogism from P ∨ Q and ¬Q";
                        firstDependencies = new[] { 1, 2 }; finalDependencies = new[] { 0, 3 }; break;
                    default: premises = new[] { "P", "Q", "(P ∧ Q) → R" }; intermediate = "P ∧ Q"; conclusion = "R";
                        firstRule = "Conjunction from P and Q"; finalRule = "Modus Ponens from P ∧ Q and (P ∧ Q) → R";
                        firstDependencies = new[] { 0, 1 }; finalDependencies = new[] { 2, 3 }; break;
                }
                p.context = "Premises: " + string.Join("; ", premises) + ". Conclusion: " + conclusion +
                    ". Formalize the argument, establishing " + intermediate + " before the conclusion. Match each step to its justification.";
                p.steps = premises.Select((s, i) => new ProofStepData { id = "given-" + i, statement = s, justification = "Premise" }).Concat(new[] {
                    new ProofStepData { id = "derive", statement = intermediate, justification = firstRule, prerequisiteIds = firstDependencies },
                    new ProofStepData { id = "conclude", statement = conclusion, justification = finalRule, prerequisiteIds = finalDependencies }
                }).ToArray();
                var orders = new List<ProofOrderData>();
                void Visit(List<int> order)
                {
                    if (order.Count == 5) { orders.Add(new ProofOrderData { stepIds = order.ToArray() }); return; }
                    for (int i = 0; i < 5; i++) if (!order.Contains(i) && (i != 4 || order.Count == 4) && p.steps[i].prerequisiteIds.All(order.Contains))
                    { order.Add(i); Visit(order); order.RemoveAt(order.Count - 1); }
                }
                Visit(new List<int>()); p.acceptableOrders = orders.ToArray();
            }
            return p;
        }

        public static TrialProofChallenge Method(ArchiveTopicCatalog c, int round, int seed)
        {
            var random = new System.Random(unchecked(seed + round * 331));
            int family = round == 0 ? 4 : round == 4 ? 1 + random.Next(3) : round;
            var pool = c.proofMethods.variantSets[family].variants;
            var p = Copy(pool[random.Next(pool.Length)]);
            if (round == 0 || round == 4)
            {
                p.kind = TrialProofKind.ChooseAndArrange; p.correctMethod = p.station;
                string claim = p.problem.Split(new[] { ". " }, StringSplitOptions.None)[0];
                // Name the claim without giving away the strategy to be identified.
                claim = claim.Replace("Prove directly that", "Prove that").Replace("Prove by contradiction that", "Prove that");
                p.title = round == 0 ? "Identify and Build an Argument" : "Audit a Proof Strategy";
                p.problem = claim + ". Identify the strategy used by the supplied tablets, then arrange all five into a valid proof.";
            }
            return p;
        }

        public static InductionChallenge Induction(int round, int seed)
        {
            var random = new System.Random(unchecked(seed + round * 331));
            if (round >= 3) return InductionChallenges.VariantSets[round][random.Next(InductionChallenges.VariantsPerChallenge)];
            // Start with two complementary proof roles, rather than one obvious tile.
            var source = InductionChallenges.VariantSets[4][random.Next(InductionChallenges.VariantsPerChallenge)];
            var roles = round == 0 ? new[] { "Base:1", "Hypothesis:1" } : round == 1 ? new[] { "Hypothesis:1", "Step:1" } : new[] { "Hypothesis:1", "Step:1", "Step:2" };
            var answers = source.answers.Where(p => roles.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value);
            var choices = source.choices.Where(t => answers.Values.Contains(t.id)).Select(t => new FormulaTabletData(t.id, t.expression)).ToList();
            choices.Add(new FormulaTabletData("distractor-goal", "Assume S(k + 1) is already true."));
            choices.Add(new FormulaTabletData("distractor-step", "S(k + 1) = S(k) + k"));
            return new InductionChallenge { title = round == 0 ? "Base Case and Hypothesis" : round == 1 ? "Hypothesis and Next Term" : "Use the Hypothesis",
                theorem = source.theorem, instructions = "Match each proof role. Establish the base case when requested, assume P(k), and use it to derive the next case. Never assume P(k + 1).",
                choices = choices.ToArray(), answers = answers };
        }

        public static ArchiveSetCase SetCase(ArchiveTopicCatalog c, int group, int entry)
        {
            if (group >= 3) return c.sets[group].questions[entry];
            int start = entry * 10 + 1;
            var u = Enumerable.Range(start, 10).ToArray();
            Func<int, bool> a = group == 0 ? (Func<int, bool>)(x => x % 2 == 0) : x => x <= start + (group == 1 ? 3 : 5);
            Func<int, bool> b = group == 0 ? (Func<int, bool>)(x => false) : group == 1 ? x => x >= start + 6 : (Func<int, bool>)(x => x % 2 == 0);
            return new ArchiveSetCase { a = u.Where(a).Select(x => x.ToString()).ToArray(), b = u.Where(b).Select(x => x.ToString()).ToArray(), universe = u.Select(x => x.ToString()).ToArray() };
        }
        public static string SetPrompt(int group, ArchiveSetCase c)
        {
            int start = int.Parse(c.universe[0]);
            return "U = { " + string.Join(", ", c.universe) + " }\nA = { x ∈ U | " + (group == 0 ? "x is even" : "x ≤ " + (start + (group == 1 ? 3 : 5))) + " }\nB = " +
                (group == 0 ? "∅" : "{ x ∈ U | " + (group == 1 ? "x ≥ " + (start + 6) : "x is even") + " }") +
                "\nTranslate the set-builder conditions before placing the elements.";
        }

        public static MappingChallengeData Mapping(ArchiveTopicCatalog c, int round, int seed)
        {
            var random = new System.Random(unchecked(seed + round * 331));
            var p = Copy(c.mappings.GetVariant(round, random.Next(c.mappings.ChallengeGroups[round].variants.Count)));
            if (round == 0)
            {
                int start = random.Next(-3, 1);
                p.domain = Enumerable.Range(start, 5).Select(x => x.ToString()).ToArray();
                p.codomain = Enumerable.Range(start, 5).Select(x => (2 * x + 1).ToString()).Append((2 * start + 11).ToString()).ToArray();
                p.requiredPairs = p.domain.Select(x => new MappingPair(x, (2 * int.Parse(x) + 1).ToString())).ToList();
                p.requireFunction = true; p.initialPairs.Clear();
                p.title = "Build a Function from its Rule"; p.instruction = "For every displayed input x, connect it to f(x) = 2x + 1. The codomain includes an unused value; the range contains only reached outputs.";
            }
            if (round == 1)
            {
                int start = random.Next(1, 5);
                p.domain = Enumerable.Range(start, 4).Select(x => x.ToString()).ToArray();
                p.codomain = Enumerable.Range(start, 5).Select(x => x.ToString()).ToArray();
                p.requiredPairs = p.domain.SelectMany(x => new[] { new MappingPair(x, x), new MappingPair(x, (int.Parse(x) + 1).ToString()) }).ToList();
                p.initialPairs.Clear(); p.requireFunction = false;
                p.title = "Translate a Relation Rule"; p.instruction = "Build R = { (x,y) | y = x or y = x + 1 } using the displayed domain and codomain. Include every qualifying ordered pair. A relation may give an input more than one output.";
            }
            if (round == 2)
            {
                p.domain = new[] { "1", "2", "3", "4", "5" }; p.codomain = new[] { "A", "B", "C", "D", "E" };
                int[] outputs = ArchiveTopicQuestions.Permutation(5, random.Next());
                p.requiredPairs = p.domain.Select((x, i) => new MappingPair(x, p.codomain[outputs[i]])).ToList();
                p.initialPairs = p.requiredPairs.Take(2).ToList();
                var remaining = Enumerable.Range(2, 3).OrderBy(i => outputs[i]).Select(i => p.domain[i]);
                p.instruction = "Keep the two fixed connections. Complete a bijection. The remaining output letters increase for inputs " + string.Join(", ", remaining) + ", in that order.";
            }
            if (round >= 3)
            {
                bool function = (seed & 1) == 0 || round == 4;
                bool onto = (seed & 2) == 0, injective = (seed & 4) == 0;
                p.domain = new[] { "1", "2", "3", "4", "5" };
                p.codomain = round == 4 && !injective && onto ? new[] { "A", "B", "C", "D" } : new[] { "A", "B", "C", "D", "E", "F" }.Take(onto ? 5 : 6).ToArray();
                var outputOrder = ArchiveTopicQuestions.Permutation(p.codomain.Length, random.Next());
                p.initialPairs = p.domain.Select((x, i) => new MappingPair(x, p.codomain[outputOrder[injective ? i : i % Math.Min(4, p.codomain.Length)]])).ToList();
                if (!function) p.initialPairs[4] = new MappingPair(p.domain[0], p.codomain[outputOrder[1]]);
                p.correctClassificationIndex = MappingRuleEvaluator.IsFunction(p.initialPairs, p.domain) ? 0 : 2;
                p.correctProperties = MappingRuleEvaluator.Properties(p.initialPairs, p.domain, p.codomain) & (MappingProperty.Injective | MappingProperty.Surjective | MappingProperty.Bijective);
                p.instruction = round == 3 ? "Inspect every input and all five preset connections. Decide whether the relation is a function; shared outputs alone do not invalidate a function." :
                    "Inspect the domain, the whole codomain and the preset connections. Select EVERY applicable property: injective, surjective, bijective. An unused codomain value matters.";
            }
            return p;
        }

        public static GraphChallengeData Graph(ArchiveTopicCatalog c, int round, int seed)
        {
            var random = new System.Random(unchecked(seed + round * 331));
            var pool = c.graphs.variantSets[round == 0 ? 2 : round].variants;
            var p = Copy(pool[random.Next(pool.Count)]);
            if (round == 0) { p.type = GraphChallengeType.VerticesAndEdges; p.title = "Inspect Vertices and Edges"; p.objective = "Trace a connected simple path from " + p.start + " to " + p.destination + ". Inspect each junction before choosing its next edge."; }
            if (round == 4)
            {
                // Combine two covered skills: directed edges and a closed simple path.
                foreach (var edge in p.edges) edge.directed = true;
                p.title = "Close a Directed Cycle";
                p.objective = "Begin at " + p.start + ", follow the arrows around a cycle, and return to " + p.start + ". Do not repeat another vertex. Inspect the dead-end branch.";
                p.lesson = "A directed cycle follows every arrow and returns to its starting vertex. Only the first/last vertex repeats. A reversed edge is not a legal shortcut.";
            }
            return p;
        }

        public static TreeChallengeData Tree(ArchiveTopicCatalog c, int round, int seed)
        {
            var random = new System.Random(unchecked(seed + round * 331));
            var p = Copy(c.trees.variantSets[round].variants[random.Next(c.trees.variantSets[round].variants.Count)]);
            var seven = Copy(c.trees.variantSets[0].variants[0]);
            if (round == 0)
            {
                p = seven; p.identification = TreeIdentification.Parent; p.target = "E";
                p.objective = "Identify the parent of node E. Follow the incoming parent link, not the node's position on the page.";
            }
            if (round == 1 || round == 2)
            {
                p = seven; p.type = round == 1 ? TreeChallengeType.Build : TreeChallengeType.Repair;
                p.required = p.initial.Select(e => new TreeEdgeData(e.source, e.destination)).ToList();
                if (round == 1) p.initial.Clear();
                else { p.initial.First(e => e.destination == "D").source = "C"; p.initial.First(e => e.destination == "F").source = "B"; }
                p.title = round == 1 ? "Build a Seven-Node Tree" : "Repair Two Parent Links";
                p.objective = round == 1 ? "Root A has children B and C. B has children D and E; C has children F and G. Build all six parent links without a cycle." :
                    "Root A has children B and C. Restore D and E under B, and F and G under C. Keep every other correct parent link; remove an old parent before adding its replacement.";
            }
            if (round == 3)
            {
                p = seven; p.type = (seed & 1) == 0 ? TreeChallengeType.Depth : TreeChallengeType.Height;
                p.initial = new List<TreeEdgeData> { new TreeEdgeData("A","B"), new TreeEdgeData("A","C"), new TreeEdgeData("B","D"), new TreeEdgeData("D","E"), new TreeEdgeData("E","F"), new TreeEdgeData("C","G") };
                p.target = "E"; p.title = "Compare Depth and Height";
                p.objective = p.type == TreeChallengeType.Depth ? "What is the depth of E? Count edges from the root; the deepest leaf belongs to a different question." : "What is this tree's height? Compare every root-to-leaf path and count edges, not vertices.";
            }
            if (round == 4)
            {
                p = seven; p.type = TreeChallengeType.Spanning; p.title = "Extract a Spanning Tree";
                p.initial.AddRange(new[] { new TreeEdgeData("D","E"), new TreeEdgeData("F","G"), new TreeEdgeData("E","F") });
                p.required.Clear(); p.objective = "Keep all seven original vertices connected. Remove cycle-forming branches until exactly six edges remain. Any connected acyclic spanning tree is accepted.";
            }
            p.lesson = p.type == TreeChallengeType.Spanning ? "A spanning tree contains every original vertex, is connected and has no cycle. Seven vertices require six edges. Remove branches without disconnecting the graph." :
                "A root has no parent; every other node has one parent. A leaf has no children. Parent links point away from the root. Depth and height count edges; root depth is zero. Follow the actual links when reading the diagram.";
            return p;
        }
    }
}
