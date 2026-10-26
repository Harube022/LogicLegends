using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    public enum ArchiveBonusMechanic
    {
        TruthTiles, InferenceChain, ProofSort, ProofRepair, InductionSequence,
        SetDelivery, MappingLinks, GraphRoute, TreeRepair
    }

    /// <summary>
    /// Separate, seed-stable practice exercises for the existing authoritative optional activity.
    /// These data do not own progress, clocks, network authority or rewards. The main topic
    /// and the original Solo catalogs remain independent of this bonus content.
    /// </summary>
    public sealed class ArchiveCoopActivity
    {
        public string title = "", context = "", instructions = "", explanation = "", moduleReference = "";
        public ArchiveBonusMechanic mechanic;
        public string[] items = Array.Empty<string>(), slots = Array.Empty<string>(), nodes = Array.Empty<string>();
        // Placement activities: expected[slot] is an item index. For regionPlacement,
        // expected[item] is a region index. For InductionSequence it is an item-index route.
        public int[] expected = Array.Empty<int>(), initialValues = Array.Empty<int>(),
            initialLinks = Array.Empty<int>(), requiredLinks = Array.Empty<int>();
        public Vector2[] positions = Array.Empty<Vector2>();
        public bool reusableItems, regionPlacement;
        public int startNode, goalNode;

        static string Values(IEnumerable<int> source) => "{ " + string.Join(", ", source) + " }";
        static string Truth(bool value) => value ? "TRUE" : "FALSE";
        static int[] Empty(int count) => Enumerable.Repeat(-1, count).ToArray();
        static int Edge(int from, int to) => from * 100 + to;

        public static ArchiveCoopActivity Build(int topic, int seed, int resetSerial = 0)
        {
            if (topic < 1 || topic > 9) throw new ArgumentOutOfRangeException(nameof(topic), "T1 uses its existing P/Q rune activity. This catalog contains T2–T10.");
            var random = new System.Random(unchecked(seed * 397 + topic * 10937 + resetSerial * 331 + 28411));
            var activity = new ArchiveCoopActivity { moduleReference = ArchiveCurriculumQuestions.References[topic] };
            switch (topic)
            {
                case 1: BuildTruth(activity, random); break;
                case 2: BuildInference(activity, random); break;
                case 3: BuildProofSort(activity, random); break;
                case 4: BuildProofRepair(activity, random); break;
                case 5: BuildInduction(activity, random); break;
                case 6: BuildSets(activity, random); break;
                case 7: BuildMapping(activity, random); break;
                case 8: BuildGraph(activity, random); break;
                case 9: BuildTree(activity, random); break;
            }
            return activity;
        }

        static void BuildTruth(ArchiveCoopActivity a, System.Random random)
        {
            a.mechanic = ArchiveBonusMechanic.TruthTiles;
            a.title = "Truth Mosaic Support";
            a.items = new[] { "TRUE tile", "FALSE tile" };
            a.slots = new[] { "P=T, Q=T", "P=T, Q=F", "P=F, Q=T", "P=F, Q=F" };
            a.reusableItems = true;
            int variant = random.Next(5);
            string expression = new[] { "¬P ∨ Q", "P ∧ ¬Q", "¬(P ∨ Q)", "(P → Q) ∧ P", "P ↔ ¬Q" }[variant];
            a.context = "Separate bonus table: " + expression + ". Evaluate all four displayed P/Q assignments.";
            a.expected = Enumerable.Range(0, 4).Select(row =>
            {
                bool p = row < 2, q = row % 2 == 0;
                bool result = variant == 0 ? !p || q : variant == 1 ? p && !q : variant == 2 ? !(p || q) : variant == 3 ? (!p || q) && p : p == !q;
                return result ? 0 : 1;
            }).ToArray();
            a.initialValues = Empty(4);
            a.instructions = "Split the rows with your teammates. Use Hand / E to take a reusable TRUE or FALSE tile, then place it in that row's output socket. Replace any tile you want to correct, then check the complete table.";
            a.explanation = expression + " gives " + string.Join(", ", a.expected.Select((value, row) => a.slots[row] + ": " + Truth(value == 0))) + ". Evaluate the parentheses and negations before combining the final values.";
        }

        static void BuildInference(ArchiveCoopActivity a, System.Random random)
        {
            a.mechanic = ArchiveBonusMechanic.InferenceChain;
            a.title = "Argument Assembly";
            a.slots = new[] { "Compound premise", "Supporting premise", "Rule", "Conclusion" };
            int variant = random.Next(3);
            string[] correct;
            string[] distractors;
            if (variant == 0)
            {
                a.context = "A means the beacon is lit. B means the gate is open. Given A → B and A, assemble the valid argument.";
                correct = new[] { "A → B", "A", "Modus Ponens", "B" };
                distractors = new[] { "¬B", "Modus Tollens" };
                a.explanation = "A → B and A justify B by Modus Ponens. A conditional alone does not establish either A or B.";
            }
            else if (variant == 1)
            {
                a.context = "A means the beacon is lit. B means the gate is open. Given A → B and ¬B, assemble the valid argument.";
                correct = new[] { "A → B", "¬B", "Modus Tollens", "¬A" };
                distractors = new[] { "A", "Modus Ponens" };
                a.explanation = "A → B and ¬B justify ¬A by Modus Tollens. Affirming B would not justify A.";
            }
            else
            {
                a.context = "A means the team takes the east path. B means it takes the west path. Given A ∨ B and ¬A, assemble the valid argument.";
                correct = new[] { "A ∨ B", "¬A", "Disjunctive Syllogism", "B" };
                distractors = new[] { "¬B", "Simplification" };
                a.explanation = "A ∨ B and ¬A justify B by Disjunctive Syllogism. Simplification requires a conjunction, not a disjunction.";
            }
            ShuffleCards(a, correct.Concat(distractors).ToArray(), correct, random);
            a.initialValues = Empty(a.slots.Length);
            a.instructions = "Share the argument roles: one player reads the givens while another carries the premise, rule and conclusion tablets with Hand / E. Fill each named socket and check the completed argument. Extra tablets are distractors.";
        }

        static void BuildProofSort(ArchiveCoopActivity a, System.Random random)
        {
            a.mechanic = ArchiveBonusMechanic.ProofSort;
            a.title = "Scribes' Sorting Court";
            a.slots = new[] { "Formal proof", "Informal proof" };
            a.regionPlacement = true;
            var examples = random.Next(2) == 0 ? new[]
            {
                "Claim: an even integer has an even square.\n1. n=2k, k an integer — definition of even.\n2. n²=4k²=2(2k²) — algebra.\n3. n² is even — definition of even.",
                "The square of an even integer is even: an even number is twice an integer, so its square still contains a factor of two.",
                "Premises: A → B; A.\n1. A → B — premise.\n2. A — premise.\n3. B — Modus Ponens, lines 1 and 2.",
                "The beacon is lit, and whenever it is lit the gate opens. Those facts tell us that the gate is open."
            } : new[]
            {
                "Claim: two odd integers have an even sum.\n1. a=2r+1, b=2s+1 — definition of odd.\n2. a+b=2(r+s+1) — algebra.\n3. a+b is even — definition of even.",
                "Two odd groups each leave one unpaired object. Their two leftovers make a pair, so the combined number of objects is even.",
                "Premises: A → B; ¬B.\n1. A → B — premise.\n2. ¬B — premise.\n3. ¬A — Modus Tollens, lines 1 and 2.",
                "The gate would open if the beacon were lit. The gate is not open, so the given implication tells us that the beacon is not lit."
            };
            int[] order = ArchiveTopicQuestions.Permutation(examples.Length, random.Next());
            a.items = order.Select(i => examples[i]).ToArray();
            a.expected = order.Select(i => i % 2).ToArray();
            a.initialValues = Empty(a.items.Length);
            a.context = "These four arguments are valid. Classify how each argument presents its reasoning; do not confuse presentation style with mathematical validity.";
            a.instructions = "Divide the scrolls between your teammates. Read a scroll, carry it with Hand / E, and deliver it to the Formal or Informal tray. All four scrolls must be classified before the shared check.";
            a.explanation = "Formal excerpts list deductions with explicit justifications and supporting lines. Informal excerpts explain valid reasoning in everyday language. An informal proof is not automatically incorrect.";
        }

        static void BuildProofRepair(ArchiveCoopActivity a, System.Random random)
        {
            a.mechanic = ArchiveBonusMechanic.ProofRepair;
            a.title = "Repair the Broken Derivation";
            a.slots = new[] { "Assumption", "Representation", "Derivation", "Conclusion" };
            string[] correct;
            int variant = random.Next(3);
            if (variant == 0)
            {
                a.context = "Direct proof: if n is even, then n² is even. The four tablets are currently in the wrong order. Repair the derivation.";
                correct = new[] { "Assume n is an even integer.", "n=2k for some integer k.", "n²=4k²=2(2k²), and 2k² is an integer.", "Therefore n² is even." };
                a.explanation = "Begin with the hypothesis, express evenness as n=2k, derive a factor of two in n², and only then conclude that n² is even.";
            }
            else if (variant == 1)
            {
                a.context = "Direct proof: if n is odd, then n−3 is even. The four tablets are currently in the wrong order. Repair the derivation.";
                correct = new[] { "Assume n is an odd integer.", "n=2k+1 for some integer k.", "n−3=2k−2=2(k−1), and k−1 is an integer.", "Therefore n−3 is even." };
                a.explanation = "Oddness gives n=2k+1. Subtraction yields n−3=2(k−1), so the conclusion follows from the definition of even.";
            }
            else
            {
                a.context = "Contrapositive proof: if n² is even, then n is even. The supplied proof establishes: if n is odd, n² is odd. Repair its tablet order.";
                correct = new[] { "Assume n is an odd integer.", "n=2k+1 for some integer k.", "n²=4k²+4k+1=2(2k²+2k)+1.", "Thus n² is odd; the contrapositive proves the original claim." };
                a.explanation = "The original conditional P→Q is proved through ¬Q→¬P. Assuming n is odd gives an odd square; assuming n² is odd instead would reverse the argument.";
            }
            ShuffleCards(a, correct, correct, random);
            // Every physical tablet is already placed. Swapping the two central roles
            // creates a concrete repair task rather than another empty order puzzle.
            a.initialValues = new[] { a.expected[2], a.expected[0], a.expected[3], a.expected[1] };
            a.instructions = "Inspect the existing arrangement together. Use Hand / E to lift and move misplaced proof tablets into their named roles. Keep the valid assumption before the calculation and the conclusion after its justification. Check only after repairing all four positions.";
        }

        static void BuildInduction(ArchiveCoopActivity a, System.Random random)
        {
            a.mechanic = ArchiveBonusMechanic.InductionSequence;
            a.title = "Induction Signal Chain";
            int multiplier = random.Next(1, 4);
            string coefficient = multiplier == 1 ? "" : multiplier.ToString();
            string sum = multiplier == 1 ? "S(n)=1+2+…+n" : "S(n)=" + multiplier + "+" + (2 * multiplier) + "+…+" + multiplier + "n";
            string formula = coefficient + "n(n+1)/2";
            string[] correct = {
                "BASE: S(1)=" + multiplier + ".",
                "HYPOTHESIS: assume S(k)=" + coefficient + "k(k+1)/2 for an arbitrary k≥1.",
                "STEP: S(k+1)=S(k)+" + coefficient + "(k+1)=" + coefficient + "(k+1)(k+2)/2.",
                "CONCLUDE: S(n)=" + formula + " for every integer n≥1."
            };
            string distractor = "CIRCULAR: assume S(k+1) is already true before deriving it.";
            ShuffleCards(a, correct.Append(distractor).ToArray(), correct, random);
            a.context = "Claim: " + sum + "=" + formula + " for every integer n≥1. Activate the proof signals in the order that justifies mathematical induction.";
            a.instructions = "Coordinate the signal order. Use Hand / E at the next correct pillar: base case, hypothesis, inductive step and final conclusion. Activated signals form a shared sequence. Clear the unfinished sequence to retry; the circular-assumption pillar does not belong in the proof.";
            a.explanation = "The base case establishes P(1). Assume P(k) for arbitrary k≥1, add the next term to derive P(k+1), and use induction to conclude every allowed case. Assuming P(k+1) before deriving it is circular.";
        }

        static void BuildSets(ArchiveCoopActivity a, System.Random random)
        {
            a.mechanic = ArchiveBonusMechanic.SetDelivery;
            a.title = "Venn Supply Run";
            a.regionPlacement = true;
            a.slots = new[] { "A only", "A ∩ B", "B only" };
            int first = random.Next(1, 15);
            int[] values = Enumerable.Range(first, 6).ToArray();
            int[] order = ArchiveTopicQuestions.Permutation(6, random.Next());
            int[] setA = values.Take(4).ToArray(), setB = values.Skip(2).ToArray();
            a.items = order.Select(i => values[i].ToString()).ToArray();
            a.expected = order.Select(i => i < 2 ? 0 : i < 4 ? 1 : 2).ToArray();
            a.initialValues = Empty(6);
            a.context = "A=" + Values(setA) + "; B=" + Values(setB) + ". Deliver each supplied element exactly once to its Venn region. The overlap is shared membership, not a second copy.";
            a.instructions = "Split the delivery work. Pick up a numbered token with Hand / E, carry it to A only, A ∩ B or B only, and place it there. Teammates may work on different tokens at the same time. Check the complete six-token arrangement together.";
            a.explanation = "A only contains " + Values(values.Take(2)) + "; A ∩ B contains " + Values(values.Skip(2).Take(2)) + "; B only contains " + Values(values.Skip(4)) + ". Every shared element appears once in the overlap.";
        }

        static void BuildMapping(ArchiveCoopActivity a, System.Random random)
        {
            a.mechanic = ArchiveBonusMechanic.MappingLinks;
            a.title = "Function Cable Crew";
            int offset = random.Next(-2, 5), scale = random.Next(1, 3), first = random.Next(-1, 3);
            int[] inputs = Enumerable.Range(first, 3).ToArray();
            int[] outputs = inputs.Select(x => scale * x + offset).ToArray();
            int[] outputOrder = ArchiveTopicQuestions.Permutation(3, random.Next());
            a.nodes = inputs.Select(x => "x=" + x).Concat(outputOrder.Select(i => "y=" + outputs[i])).ToArray();
            a.positions = new[] { new Vector2(-4.5f,-2.6f), new Vector2(-4.5f,0), new Vector2(-4.5f,2.6f),
                new Vector2(4.5f,-2.6f), new Vector2(4.5f,0), new Vector2(4.5f,2.6f) };
            a.requiredLinks = Enumerable.Range(0, 3).Select(i => Edge(i, 3 + Array.IndexOf(outputOrder, i))).ToArray();
            string rule = (scale == 1 ? "x" : scale + "x") + (offset < 0 ? " − " + -offset : offset == 0 ? "" : " + " + offset);
            a.context = "Function f(x)=" + rule + ". Domain=" + Values(inputs) + "; codomain=" + Values(outputOrder.Select(i => outputs[i])) + ". Build the three directed input-to-output cables.";
            a.instructions = "Divide the inputs among the team. Use Hand / E at an input connector, then its correct output connector, to add a cable. Remove an incorrect cable before checking. Every input must have exactly one output, following f(x).";
            a.explanation = "The completed ordered pairs are " + string.Join(", ", inputs.Select((x, i) => "(" + x + "," + outputs[i] + ")")) + ". The function rule determines the output; matching connector positions do not.";
        }

        static void BuildGraph(ArchiveCoopActivity a, System.Random random)
        {
            a.mechanic = ArchiveBonusMechanic.GraphRoute;
            a.title = "Navigator and Runner";
            string[] labels = random.Next(2) == 0 ? new[] { "A", "B", "C", "D", "E" } : new[] { "S", "J", "K", "L", "G" };
            a.nodes = labels;
            a.positions = new[] { new Vector2(-6,0),new Vector2(-2,-2.4f),new Vector2(-2,2.4f),new Vector2(2,2.4f),new Vector2(6,0) };
            a.startNode = 0; a.goalNode = 4;
            bool lowerOpen = random.Next(2) == 0;
            a.initialLinks = lowerOpen ? new[] { Edge(0,1), Edge(1,4), Edge(0,2), Edge(2,3), Edge(4,3) }
                : new[] { Edge(0,1), Edge(4,1), Edge(0,2), Edge(2,3), Edge(3,4) };
            a.context = "One teammate navigates while another walks. Start at " + labels[0] + " and finish at " + labels[4] + ". Follow the directed arrows; one branch cannot reach the goal without walking backwards.";
            a.instructions = "Discuss a legal route first. The runner starts at the start node and physically follows its connected arrows to the goal. The navigator watches the junctions and warns about the dead end. A path may not repeat a vertex or reverse an arrow. Clear an unfinished route to restart.";
            a.explanation = lowerOpen ? "The valid simple directed path is " + labels[0] + " → " + labels[1] + " → " + labels[4] + ". The other branch ends at " + labels[3] + "; its goal edge points the wrong way." :
                "The valid simple directed path is " + labels[0] + " → " + labels[2] + " → " + labels[3] + " → " + labels[4] + ". The branch to " + labels[1] + " cannot enter the goal because that arrow starts at the goal.";
        }

        static void BuildTree(ArchiveCoopActivity a, System.Random random)
        {
            a.mechanic = ArchiveBonusMechanic.TreeRepair;
            a.title = "Branch Repair Crew";
            a.nodes = new[] { "A · root", "B", "C", "D", "E" };
            a.positions = new[] { new Vector2(0,2.8f), new Vector2(-3.5f,0), new Vector2(3.5f,0), new Vector2(-4.8f,-2.8f), new Vector2(4.8f,-2.8f) };
            bool dUnderB = random.Next(2) == 0;
            int parentD = dUnderB ? 1 : 2, parentE = dUnderB ? 2 : 1;
            a.requiredLinks = new[] { Edge(0,1), Edge(0,2), Edge(parentD,3), Edge(parentE,4) };
            // Exactly one original parent is incorrect. The repair must replace it,
            // rather than merely adding a second parent or rebuilding the whole graph.
            a.initialLinks = new[] { Edge(0,1), Edge(0,2), Edge(parentE,3), Edge(parentE,4) };
            string dParent = dUnderB ? "B" : "C", eParent = dUnderB ? "C" : "B";
            a.context = "Root A must have children B and C. D must be a child of " + dParent + "; E must be a child of " + eParent + ". One existing parent link is wrong. Keep all other correct links.";
            a.instructions = "Inspect the branches together. One teammate identifies the incorrect parent link; another selects its parent and child with Hand / E and removes it. Select the correct parent and child to connect the replacement. Each non-root node must have exactly one parent. Check the repaired tree.";
            a.explanation = "Keep A→B, A→C and " + eParent + "→E. Replace the incorrect link into D with " + dParent + "→D. The result connects every node, points away from the root, has one parent per non-root node and has no cycle.";
        }

        static void ShuffleCards(ArchiveCoopActivity a, string[] cards, string[] accepted, System.Random random)
        {
            a.items = ArchiveTopicQuestions.Permutation(cards.Length, random.Next()).Select(i => cards[i]).ToArray();
            a.expected = accepted.Select(text => Array.IndexOf(a.items, text)).ToArray();
        }

        public bool Validate(int[] values, List<int> links, List<int> route)
        {
            switch (mechanic)
            {
                case ArchiveBonusMechanic.TruthTiles:
                case ArchiveBonusMechanic.InferenceChain:
                case ArchiveBonusMechanic.ProofSort:
                case ArchiveBonusMechanic.ProofRepair:
                case ArchiveBonusMechanic.SetDelivery:
                    if (expected.Length == 0 || values == null || values.Length != expected.Length || !values.SequenceEqual(expected)) return false;
                    if (regionPlacement) return values.All(v => v >= 0 && v < slots.Length);
                    return values.All(v => v >= 0 && v < items.Length) && (reusableItems || values.Distinct().Count() == values.Length);
                case ArchiveBonusMechanic.InductionSequence:
                    return expected.Length > 0 && route != null && route.Count == expected.Length && route.SequenceEqual(expected) && route.Distinct().Count() == route.Count;
                case ArchiveBonusMechanic.MappingLinks:
                case ArchiveBonusMechanic.TreeRepair:
                    if (requiredLinks.Length == 0 || links == null || links.Count != requiredLinks.Length || links.Distinct().Count() != links.Count ||
                        links.Any(link => link < 0 || link / 100 >= nodes.Length || link % 100 >= nodes.Length || link / 100 == link % 100)) return false;
                    return new HashSet<int>(links).SetEquals(requiredLinks);
                case ArchiveBonusMechanic.GraphRoute:
                    if (route == null || route.Count < 2 || route.Count > nodes.Length || route[0] != startNode || route.Last() != goalNode ||
                        route.Any(i => i < 0 || i >= nodes.Length) || route.Distinct().Count() != route.Count) return false;
                    for (int i = 1; i < route.Count; i++) if (!initialLinks.Contains(Edge(route[i - 1], route[i]))) return false;
                    return true;
                default: return false;
            }
        }
    }
}
