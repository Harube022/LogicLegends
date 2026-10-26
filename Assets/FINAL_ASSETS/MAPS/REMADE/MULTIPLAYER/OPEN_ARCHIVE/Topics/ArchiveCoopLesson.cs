using System;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Short, independent module exercises for the existing shared relay.</summary>
    public sealed class ArchiveCoopLesson
    {
        public string context, claimP, claimQ, connective, explanation;
        public bool p, q, target, freeInputs;
        public static ArchiveCoopLesson Build(int topic, int seed, int resetSerial = 0)
        {
            var random = new Random(unchecked(seed + topic * 10937 + resetSerial * 331 + 9173));
            bool a = random.Next(2) == 1, b = random.Next(2) == 1;
            string Truth(bool v) => v ? "TRUE" : "FALSE";
            int n = random.Next(2, 8);
            var lesson = new ArchiveCoopLesson { connective = "Conjunction" };
            switch (topic)
            {
                case 0:
                    lesson.freeInputs = true;
                    lesson.connective = ArchiveCurriculumQuestions.Connectives[random.Next(5)];
                    lesson.target = random.Next(2) == 1;
                    lesson.context = "Separate practice: choose P and Q so " + lesson.connective + " gives " + Truth(lesson.target) + ".";
                    lesson.claimP = "P: choose a truth value."; lesson.claimQ = "Q: choose a truth value.";
                    lesson.explanation = "Use the connective's full truth condition, not a keyword.";
                    return lesson;
                case 1:
                    lesson.context = "Given A=" + Truth(a) + ", B=" + Truth(b) + ". Decide whether each claim is true.";
                    lesson.claimP = "P: A → B evaluates to FALSE."; lesson.p = a && !b;
                    lesson.claimQ = "Q: ¬(A ∧ B) evaluates to TRUE."; lesson.q = !(a && b);
                    lesson.explanation = "An implication is false only for T → F. Negating AND reverses its result."; break;
                case 2:
                    lesson.context = a ? "Given A → B and ¬B." : "Given A ∨ B and ¬A.";
                    lesson.claimP = a ? "P: ¬A follows by Modus Tollens." : "P: B follows by Disjunctive Syllogism."; lesson.p = true;
                    lesson.claimQ = a ? "Q: A follows from these premises." : "Q: ¬B follows from these premises."; lesson.q = false;
                    lesson.explanation = "Match the premise structure to MT or DS. Never reverse a rule's conclusion."; break;
                case 3:
                    lesson.context = a ? "A proof lists premises, then each deduction with its rule and earlier supporting lines." : "A valid proof explains a pairing argument in everyday sentences.";
                    lesson.claimP = "P: This presentation is a formal proof."; lesson.p = a;
                    lesson.claimQ = "Q: A natural-language presentation is automatically an invalid argument."; lesson.q = false;
                    lesson.explanation = "Formal presentation explicitly justifies its derivation. An informal proof can still be valid."; break;
                case 4:
                    lesson.context = "Claim: if n² is even, n is even. An argument assumes n is odd and proves n² is odd.";
                    lesson.claimP = "P: The argument proves the contrapositive."; lesson.p = true;
                    lesson.claimQ = "Q: The required contrapositive is: if n² is odd, then n is odd."; lesson.q = false;
                    lesson.explanation = "For P → Q, the contrapositive is ¬Q → ¬P. Both direction and negation matter."; break;
                case 5:
                    lesson.context = "To prove S(n)=n(n+1)/2 for n≥1, the base case is S(1)=1.";
                    lesson.claimP = a ? "P: The induction hypothesis assumes S(k)=k(k+1)/2." : "P: The hypothesis may assume S(k+1) is already proved."; lesson.p = a;
                    lesson.claimQ = "Q: The next sum is S(k+1)=S(k)+(k+1)."; lesson.q = true;
                    lesson.explanation = "Assume P(k), then derive P(k+1) using the new term. Assuming the goal is circular."; break;
                case 6:
                    lesson.context = "U={" + n + "," + (n+1) + "," + (n+2) + "," + (n+3) + "}; A={" + n + "," + (n+1) + "}; B={" + (n+1) + "," + (n+2) + "}.";
                    lesson.claimP = "P: " + (n+1) + " belongs to A ∩ B."; lesson.p = true;
                    lesson.claimQ = "Q: " + (n+1) + " belongs to A − B."; lesson.q = false;
                    lesson.explanation = "The shared value is in both sets. A − B excludes every member of B."; break;
                case 7:
                    lesson.context = a ? "Domain={1,2,3}; codomain={A,B,C,D}; pairs={(1,A),(2,A),(3,C)}." : "Domain={1,2,3}; codomain={A,B,C,D}; pairs={(1,A),(1,B),(3,C)}.";
                    lesson.claimP = "P: The displayed relation is a function."; lesson.p = a;
                    lesson.claimQ = "Q: The displayed relation is onto its whole codomain."; lesson.q = false;
                    lesson.explanation = "A function needs one output per input. Onto requires reaching every codomain value, including D."; break;
                case 8:
                    lesson.context = "Directed edges: A → B, B → C, C → A, B → D. These are the only edges.";
                    lesson.claimP = "P: A → B → C → A is a directed cycle."; lesson.p = true;
                    lesson.claimQ = "Q: D → B → C is a valid directed path."; lesson.q = false;
                    lesson.explanation = "The cycle follows all arrows. B → D cannot be traversed backwards as D → B."; break;
                default:
                    lesson.context = "Rooted tree: A → B, A → C, B → D, D → E. Root depth is 0.";
                    lesson.claimP = "P: The depth of E is 3 edges."; lesson.p = true;
                    lesson.claimQ = a ? "Q: C is a leaf." : "Q: B is the parent of E."; lesson.q = a;
                    lesson.explanation = "A–B–D–E has three edges. C has no children; E's immediate parent is D."; break;
            }
            // Swapping the claims varies the role assignment without changing the mathematics.
            if (b) { string text = lesson.claimP; lesson.claimP = "P:" + lesson.claimQ.Substring(2); lesson.claimQ = "Q:" + text.Substring(2); bool value = lesson.p; lesson.p = lesson.q; lesson.q = value; }
            lesson.target = lesson.p && lesson.q;
            return lesson;
        }
        public bool Accepts(bool inputP, bool inputQ) => freeInputs
            ? ArchiveTopicSession.EvaluateConnective(connective, inputP, inputQ) == target
            : inputP == p && inputQ == q;
    }
}
