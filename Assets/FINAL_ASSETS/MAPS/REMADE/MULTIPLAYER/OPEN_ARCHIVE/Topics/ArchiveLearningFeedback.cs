using System;
using System.Linq;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Explanations only. The original validators still decide every outcome.</summary>
    public static class ArchiveLearningFeedback
    {
        static string Item(ArchiveQuestion q, int i) => i >= 0 && i < q.items.Length ? q.items[i] : "an empty socket";
        static string Detail(ArchiveQuestion q, int i) => i >= 0 && i < q.details.Length ? q.details[i] : "";
        static int At(ArchiveStationState s, int i) => i >= 0 && i < s.placements.Length ? s.placements[i] : -1;
        public static string Diagnose(ArchiveQuestion q, ArchiveStationState s, string original = "")
        {
            if (q == null || s == null) return "Read the current question and check one step at a time.";
            if (q.graph != null || q.tree != null)
                return !string.IsNullOrWhiteSpace(original) && !original.StartsWith("Check the entire") ? original :
                    q.graph != null ? "Check the start and goal, edge directions, repeated vertices or edges, and the current route. Stay on the paths while recording it."
                    : "Check that every required node is connected, each non-root node has one parent, and no cycle is present. Count edges when measuring depth or height.";
            if (q.activity == ArchiveActivity.Choice)
                return "Your selected rule: " + ArchivePropositionNotation.Explanation(Item(q, s.selection)) + " Compare that truth condition with the question's condition.";
            if (q.activity == ArchiveActivity.Sets)
            {
                for (int i = 0; i < q.items.Length; i++)
                {
                    int region = At(s, i);
                    if (region < 0 || region >= q.slots.Length || q.regionValues[region].Contains(q.items[i])) continue;
                    string value = q.items[i];
                    return "Check element " + value + " in " + q.slots[region] + ": it " + (q.setA.Contains(value) ? "belongs" : "does not belong") + " to A and " +
                        (q.setB.Contains(value) ? "belongs" : "does not belong") + " to B. " +
                        (q.setGroup == 4 ? "A complement contains members of U outside the named set." : "An intersection needs membership in both sets; A − B needs membership in A and absence from B.");
                }
                for (int region = 0; region < q.slots.Length; region++)
                {
                    var values = q.items.Where((_, i) => At(s, i) == region).ToArray();
                    if (!values.OrderBy(v => v).SequenceEqual(q.regionValues[region].OrderBy(v => v)))
                        return "Review " + q.slots[region] + ". Its required members must each appear once. Check for a missing or duplicate value; unused distractors belong at the supply.";
                }
            }
            if (q.mapping != null)
            {
                var counts = q.inputLabels.Select((label, i) => new { label, n = s.links.Count(link => link / 100 == i) }).ToArray();
                var bad = counts.FirstOrDefault(v => v.n != 1);
                string structure = bad == null ? "Every domain input currently has exactly one outgoing connection, so this relation is a function."
                    : "Input " + bad.label + " has " + bad.n + " outgoing connections. A function needs exactly one output for every domain input.";
                return structure + (q.mapping.questionKind == MappingQuestionKind.SelectProperties
                    ? " Check injective (different inputs have different outputs) and surjective (every codomain output is reached). Bijective requires both."
                    : q.mapping.questionKind == MappingQuestionKind.BuildExactRelation ? " Also compare every ordered pair with the requested relation; valid function structure alone may not satisfy the task."
                    : " Choose the category for the displayed relation. A many-to-one relation can still be a function.");
            }
            if (q.trial != null)
            {
                if (q.trial.kind != TrialProofKind.ArrangeSteps && s.selection != q.method)
                    return "The selected method is " + (s.selection < 0 ? "missing" : ((TrialProofMethod)s.selection).ToString()) + ". " +
                        "Direct starts from P; contrapositive proves ¬Q → ¬P; contradiction assumes the negation of the claim and derives an impossibility.";
                int empty = Array.FindIndex(s.placements, i => i < 0);
                if (empty >= 0) return "Step " + (empty + 1) + " is empty. Use every tablet once, from the starting assumption to the conclusion.";
                return "The method is selected, but the proof order is incomplete or invalid. Each step must follow from earlier statements. Keep the final claim after its supporting algebra or contradiction.";
            }
            if (q.proof != null && q.statementCount > 0)
            {
                int n = q.statementCount;
                var steps = s.placements.Take(n).ToArray();
                for (int i = 0; i < n; i++)
                {
                    int step = At(s, i);
                    if (step < 0 || step >= n) return "Statement socket " + (i + 1) + " is empty or holds a rule card. Place statements before matching their justifications.";
                    if (steps.Take(i).Contains(step)) return "A statement is repeated. Use every physical statement once.";
                    if (q.proof.steps[step].prerequisiteIds.Any(prior => !steps.Take(i).Contains(prior)))
                        return Item(q, step) + " at step " + (i + 1) + " uses a fact that has not been established yet. Move its supporting statements earlier.";
                    int rule = At(s, n + i);
                    if (q.proof.kind == ProofChallengeKind.Formalize && (rule < n || rule >= 2 * n ||
                        !string.Equals(q.proof.steps[rule - n].justification.Trim(), q.proof.steps[step].justification.Trim(), StringComparison.OrdinalIgnoreCase)))
                        return "Check the justification for step " + (i + 1) + ". It must name the rule that produces this statement from the available earlier lines.";
                }
                return "All prerequisites must be established before use, and the intended conclusion belongs at the end. Check the complete chain of reasoning.";
            }
            for (int i = 0; i < q.expected.Length; i++)
            {
                int value = At(s, i);
                if (value == q.expected[i]) continue;
                string slot = i < q.slots.Length ? q.slots[i] : "Socket " + (i + 1);
                if (value < 0) return slot + " is empty. " + Concept(s.topic);
                if (q.activity == ArchiveActivity.Truth)
                    return "Re-evaluate " + slot + " for " + q.prompt + ". The complete expression gives " +
                        (q.expected[i] == 0 ? "TRUE" : "FALSE") + ". Work inside parentheses, apply NOT, then combine the intermediate values using the displayed connectives.";
                if (q.activity == ArchiveActivity.Induction)
                    return "Check " + slot + ": " + Item(q, value) + " does not fill this role. " +
                        (slot.StartsWith("Base") ? "Substitute the first allowed integer into both sides."
                        : slot.StartsWith("Hypothesis") ? "Assume the statement at an arbitrary allowed k."
                        : "Derive the k+1 case from the k hypothesis; do not assume the conclusion you need to prove.");
                if (q.activity == ArchiveActivity.Sort)
                    return "Review " + Item(q, i) + ". Formal proofs explicitly justify their logical steps; informal proofs explain valid reasoning in natural language. Both still need a valid argument.";
                if (q.activity == ArchiveActivity.Premises)
                    return "Check " + slot + ". The connector ‘" + Item(q, value) + "’ changes the premise's meaning. Distinguish AND, inclusive OR, NOT and IF ... THEN in the sentence.";
                return "Review " + Item(q, value) + " at " + slot + ". " + Detail(q, value) + " Compare its required premises with the displayed argument.";
            }
            return "Check each required part and explain why it follows. Your team's work is kept.";
        }

        public static string Hint(int topic, ArchiveQuestion q, ArchiveStationState s, int level)
        {
            if (level <= 0) return "Choose Next hint for a concept reminder. Hints are free and do not reset or pause the team clock.";
            if (level == 1) return "CONCEPT\n" + Concept(topic);
            if (level == 2)
            {
                bool correct = ArchiveTopicQuestions.Validate(q, s, out string reason);
                return "CHECK YOUR WORK\n" + (correct ? "Your current arrangement meets the validator. Explain why it works before confirming." : Diagnose(q, s, reason));
            }
            return "SEPARATE WORKED EXAMPLE\n" + Example(topic) + "\n\nApply this method to your current problem; the example does not change your answer.";
        }
        public static string Reference(int topic)
        {
            string[] modules = { "Prelim Week 1, slides 10–19", "Prelim Week 2, slides 3–12", "Prelim Weeks 3–4, inference rules and equivalences",
                "Midterm Week 6, slides 4–16", "Midterm Week 7, direct proof, contradiction and contrapositive",
                "Midterm Weeks 8–9, base case, hypothesis and step", "Prefinal Weeks 11–12, sets and set operations",
                "Prefinal Week 13, relations, mappings and functions", "Final Week 16, degree, path and cycle", "Final Weeks 17–18, rooted trees and spanning trees" };
            return "IT 105 · " + modules[Math.Max(0, Math.Min(9, topic))];
        }
        public static string Guide(int topic, ArchiveQuestion q)
        {
            return "<b>WHAT YOU ARE LEARNING</b>\n" + Concept(topic) + "\n\n<b>HOW TO PLAY</b>\n" + q.instructions +
                "\n\n<b>WORKED EXAMPLE</b>\n" + Example(topic) + "\n\n<b>SYMBOLS AND TERMS</b>\n" + Glossary(topic) +
                "\n\n<b>COMPUTER SCIENCE CONNECTION</b>\n" + Connection(topic) + "\n\n<b>COURSE REFERENCE</b>\n" + Reference(topic) +
                (string.IsNullOrEmpty(q.moduleReference) ? "" : "\nCurrent question scope: " + q.moduleReference + "\nDifficulty: " + q.difficulty) +
                "\n\nRead the Book to start or resume the actual challenge. Opening this guide never starts a timer. Once a challenge is running, reading and hints keep its clock and the world active.";
        }
        public static string Concept(int topic)
        {
            switch (topic)
            {
                case 0: return "A proposition has a truth value. A connective tells how the truth values of statements combine. Match the meaning of the whole condition, not just a familiar word.";
                case 1: return "A truth table checks every assignment: TT, TF, FT and FF. Evaluate parentheses and negations before the outer connective. Conditional P → Q fails only when P is true and Q is false.";
                case 2: return "An inference rule preserves truth: if all premises are true, the conclusion follows. Keep each proposition consistent while matching the argument's structure.";
                case 3: return "A formal proof states assumptions and justifies every step. An informal proof explains valid reasoning in ordinary language. An example alone does not prove a universal claim.";
                case 4: return "Direct: assume P and derive Q. Contrapositive: prove ¬Q → ¬P. Contradiction: assume the negation of the claim and derive an impossibility. Every step needs support.";
                case 5: return "Induction needs a base case AND a general step. Assume P(k) for an arbitrary allowed k, then derive P(k+1). Checking several examples alone does not prove every case.";
                case 6: return "A set contains distinct elements. Intersection means both sets; union means either set; A − B means in A but not B. A complement is relative to the named universe U.";
                case 7: return "A relation is a set of ordered pairs. A function gives each domain input exactly one output. The codomain lists allowed outputs; the range contains only reached outputs.";
                case 8: return "Vertices represent objects and edges represent connections. A path follows adjacent vertices; a simple path does not repeat vertices. A simple cycle returns to its start without repeating other vertices.";
                default: return "An undirected tree is connected and has no cycle, so any two vertices have exactly one simple path between them. A rooted tree adds a chosen root and parent–child relationships.";
            }
        }
        public static string Example(int topic)
        {
            switch (topic)
            {
                case 0: return "Let P mean ‘has a key’ and Q mean ‘switch is on’. A gate programmed to open exactly when both conditions hold tests P ∧ Q. With P=T and Q=F, AND is false, so the gate stays closed.";
                case 1: return "For ¬P ∨ Q at P=T, Q=F: first ¬P=F, then F ∨ F=F. At P=F, Q=F, ¬P=T and T ∨ F=T. Evaluate each row separately.";
                case 2: return "Premises: If the test passes, the build is accepted (P → Q). The test passes (P). Therefore the build is accepted (Q). This is Modus Ponens. Knowing Q alone would not let you infer P.";
                case 3: return "Claim: the sum of two even integers is even. Let a=2m and b=2n for integers m,n. Then a+b=2(m+n), which is even by definition. Each statement uses a definition or earlier equality; checking 2+4 alone would be insufficient.";
                case 4: return "Direct example: if n is even, write n=2k. Then n²=4k²=2(2k²), so n² is even. Contrapositive example: to prove n² even implies n even, assume n is odd and show n² is odd.";
                case 5: return "Claim: 1+...+n=n(n+1)/2 for n≥1. Base n=1 gives 1=1. Assume the formula at k. Add k+1: k(k+1)/2+(k+1)=(k+1)(k+2)/2, exactly the formula at k+1.";
                case 6: return "For U={1,2,3,4,5}, A={1,2,3}, B={3,4}: A ∩ B={3}; A ∪ B={1,2,3,4}; A − B={1,2}; B − A={4}; A's complement in U is {4,5}. The shared element 3 appears once in a Venn intersection.";
                case 7: return "Domain {a,b}, codomain {1,2,3}, pairs {(a,1),(b,2)}: this is an injective function, but not surjective because 3 is unreached. Adding (a,3) would break the function rule because a then has two outputs.";
                case 8: return "With undirected edges AB, BC and CA, A–B–C is a simple path and A–B–C–A is a simple cycle. B has degree 2. If an edge is directed B → C, walking C → B does not follow that arrow.";
                default: return "Root A has children B and C; B has child D. D is a leaf at depth 2, measured in edges A–B–D. The tree's height is 2. Four connected vertices use three edges; adding C–D makes a cycle.";
            }
        }
        public static string Glossary(int topic)
        {
            if (topic <= 2) return "¬ or ~ = NOT; ∧ = AND; ∨ = inclusive OR; ⊕ = XOR; → or ⇒ = implies; ↔ = if and only if. T/F mean true/false. A premise is given; a conclusion is derived. ∀ means for every, ∃ means there exists.";
            if (topic <= 5) return "P → Q: hypothesis P implies conclusion Q. ¬P negates P. ∀ means for every. A counterexample disproves a universal claim. In induction, k is arbitrary and P(k) is the induction hypothesis.";
            if (topic == 6) return "∈ member of; ∉ not a member of; ∪ union; ∩ intersection; ⊆ subset; { } empty set; U universe; A − B difference; A′ complement relative to U.";
            if (topic == 7) return "(x,y) is an ordered pair. Domain = inputs; codomain = allowed outputs; range = reached outputs. Injective = one-to-one; surjective = onto; bijective = both.";
            if (topic == 8) return "G=(V,E): vertex set V and edge set E. deg(v) counts incident edge ends (an undirected loop counts twice). An arrow sets direction. A walk may repeat; a simple path does not repeat vertices.";
            return "Root = designated starting node. Parent/child = adjacent generations. Leaf = no children. Depth = edges from root; height = longest downward path in edges. A spanning tree includes every original vertex using only original edges.";
        }
        static string Connection(int topic)
        {
            string[] links = { "Boolean conditions decide whether code runs: if (hasKey && switchOn). AND and OR produce different program behavior.",
                "Truth tables exhaustively test small Boolean expressions and help detect incorrect conditions in programs.",
                "Inference rules model safe deductions in program verification and rule-based systems.",
                "A verifier checks that each program-proof step follows from assumptions and established rules.",
                "Proof methods help justify algorithms for all valid inputs instead of trusting a few passing tests.",
                "Induction explains loop invariants and why recursive algorithms work for every allowed input size.",
                "Set operations appear in database queries, permissions, and combining search results.",
                "A lookup function returns one output per input. A database relation can contain several pairs sharing the same first value.",
                "Graphs model networks, dependencies and routes; edge direction determines which moves are allowed.",
                "Trees model folders and expression syntax. Parent relationships and traversal depth determine how these structures are processed." };
            return links[Math.Max(0, Math.Min(9, topic))];
        }
    }
}
