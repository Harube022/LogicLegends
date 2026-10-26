using System;
using System.Linq;
using LogicLegends.Inference;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>
    /// Multiplayer content selection over the existing question types and validators.
    /// Based on the supplied IT 105 Prelim modules. These are design targets;
    /// observed student difficulty still needs a playtest. Solo banks are untouched.
    /// </summary>
    public static partial class ArchiveCurriculumQuestions
    {
        public static readonly string[] TierNames = { "Medium", "Medium-Hard", "Hard" };
        public static readonly string[] Connectives = { "Conjunction", "Disjunction", "Exclusive OR", "Conditional", "Biconditional" };
        // Bit 0..3 are TT, TF, FT, FF. Used to author the T1 observation questions.
        public static readonly int[] ConnectiveMasks = { 1, 7, 6, 13, 9 };
        public static readonly int[][] TraceRows = {
            new[] { 0, 1, 3 }, new[] { 0, 1, 3 }, new[] { 0, 1, 3 },
            new[] { 1, 2, 3 }, new[] { 0, 2, 3 }
        };
        public const string PropositionReference = "IT 105 · Prelim Week 1, slides 12–19 (connectives and negation)";
        public const string TruthReference = "IT 105 · Prelim Week 2, slides 4–12 (connective tables and compound expressions)";
        public const string InferenceReference = "IT 105 · Prelim Week 3, slides 7–19 (inference patterns); Week 1, slides 13–18 (compound statements)";

        public static int Tier(int topic, int round)
        {
            if (topic == 1) return Math.Min(2, Math.Max(0, round / 3));
            int stage = topic == 6 ? round / ArchiveTopicCatalog.SetProblemsPerChallenge : round;
            return stage < 2 ? 0 : stage < 4 ? 1 : 2;
        }
        public static string TierName(int topic, int round) => TierNames[Tier(topic, round)];

        // First tier: translate a complete scenario. Second tier: reason about
        // the negated failure condition. Third tier: distinguish rules by evidence.
        // No heading, source reference or option layout names the correct rule.
        public static readonly string[][] MediumPropositions = {
            new[] {
                "P means the key is present; Q means the switch is active. Either missing requirement keeps the gate shut. The gate opens in the remaining case. Which connective combines P and Q?",
                "P and Q mean two sentries are awake. One awake sentry cannot open the seal; it opens when neither sentry is asleep. Which connective models the seal opening?"
            },
            new[] {
                "P and Q mean two sensors detect movement. The alarm is silent when neither detects anything. It sounds with one detecting sensor and also with two. Which connective models the alarm sounding?",
                "P and Q mean two teammates carry a torch. Darkness blocks the path only when neither has a torch. Which connective models the path being lit?"
            },
            new[] {
                "P and Q mean two switches are on. Matching switch states leave the bridge closed; different states open it. Which connective models the bridge opening?",
                "P and Q mean two runes are active. The seal opens with one active rune, but adding the second closes it again. With both inactive it stays closed. Which connective fits?"
            },
            new[] {
                "P means a key is used; Q means entry is logged. A rule is violated when a key is used without a log entry. With no key use, either log state satisfies the rule. Which connective models the rule being satisfied?",
                "P means the alarm sounds; Q means a guard responds. The promise fails when the alarm sounds without a response. If no alarm sounds, the promise is not broken. Which connective models the promise being kept?"
            },
            new[] {
                "P and Q mean two lanterns are lit. The portal opens with both lit or both dark. One lit lantern is insufficient. Which connective models the portal opening?",
                "P and Q are two teammates' answers. The team seal accepts two TRUE answers or two FALSE answers; mixed answers are rejected. Which connective models acceptance?"
            }
        };
        public static readonly string[][] MediumHardPropositions = {
            new[] {
                "P means the key is ready; Q means the switch is ready. Failure means at least one requirement is not ready. Which connective on P and Q is TRUE precisely when this failure does not occur?",
                "P and Q describe two powered crystals. The shield is disabled if either crystal has no power. Which connective on P and Q describes the shield being enabled in every other case?"
            },
            new[] {
                "P and Q describe two lit beacons. The path is closed precisely when both beacons are unlit. Which connective on P and Q describes the path being open?",
                "P and Q describe two valid tickets. Rejection occurs only when neither ticket is valid. Which connective on P and Q describes acceptance, including the case of two valid tickets?"
            },
            new[] {
                "P and Q describe two active runes. The barrier is locked when their truth values agree. Which connective on P and Q describes it being unlocked in all other cases?",
                "P and Q describe two switches. The device rejects both matching states. Which connective on P and Q is TRUE for exactly the states it does not reject?"
            },
            new[] {
                "P means an item is borrowed; Q means it is recorded. The ONLY violation is a borrowed item with no record. Which connective is TRUE when that violation is absent? Preserve the direction from P to Q.",
                "P means a request is made; Q means it is approved. Rejection of the rule occurs only for a request with no approval. No request satisfies the rule with either approval value. Which connective on P and Q fits?"
            },
            new[] {
                "P and Q describe two crystals. The seal is disabled when exactly one crystal is active. Which connective on P and Q describes the seal being enabled in every other case?",
                "P and Q describe two signal values. An error occurs whenever the values differ. Which connective on P and Q is TRUE exactly when no error occurs?"
            }
        };

        public static void BuildProposition(ArchiveQuestion q, int round, int seed)
        {
            int family = ArchiveTopicQuestions.Permutation(Connectives.Length, seed)[round];
            var random = new Random(unchecked(seed + round * 331));
            int tier = Tier(0, round);
            if (tier < 2)
            {
                var pool = (tier == 0 ? MediumPropositions : MediumHardPropositions)[family];
                q.prompt = pool[random.Next(pool.Length)];
            }
            else
            {
                var rows = ArchiveTopicQuestions.Permutation(3, random.Next()).Select(i => TraceRows[family][i]);
                q.prompt = "Audit these three logged tests. Which ONE connective on P and Q fits ALL the observed outputs?\n" +
                    string.Join("\n", rows.Select(row => "P=" + (row < 2 ? "T" : "F") + ", Q=" + (row % 2 == 0 ? "T" : "F") +
                        " gives " + ((ConnectiveMasks[family] & (1 << row)) != 0 ? "TRUE" : "FALSE")));
            }
            var distractors = ArchiveTopicQuestions.Permutation(Connectives.Length, random.Next()).Where(i => i != family).Take(3).Append(family).ToArray();
            q.items = ArchiveTopicQuestions.Permutation(4, random.Next()).Select(i => Connectives[distractors[i]]).ToArray();
            q.activity = ArchiveActivity.Choice;
            q.title = TierName(0, round) + " · Proposition challenge " + (round + 1);
            q.difficulty = TierName(0, round); q.moduleReference = PropositionReference;
            q.correctChoice = Array.IndexOf(q.items, Connectives[family]);
            q.successText = ArchivePropositionNotation.Explanation(q.items[q.correctChoice]);
        }

        static bool Implies(bool p, bool q) => !p || q;
        // Same reusable Question evaluator and four-row input format as Solo.
        // Every entry requires combining multiple operations; only these pools
        // are used by the multiplayer adapter.
        public static readonly TruthTableChallengeQuestions.Question[][] TruthBanks = {
            new[] {
                new TruthTableChallengeQuestions.Question("¬P ∨ Q", (p,q) => !p || q),
                new TruthTableChallengeQuestions.Question("¬Q ∧ P", (p,q) => !q && p),
                new TruthTableChallengeQuestions.Question("¬(P ∧ Q)", (p,q) => !(p && q)),
                new TruthTableChallengeQuestions.Question("¬(P ∨ Q)", (p,q) => !(p || q)),
                new TruthTableChallengeQuestions.Question("P ↔ ¬Q", (p,q) => p == !q),
                new TruthTableChallengeQuestions.Question("Q → ¬P", (p,q) => Implies(q,!p)),
                new TruthTableChallengeQuestions.Question("¬P ⊕ Q", (p,q) => !p != q),
                new TruthTableChallengeQuestions.Question("P ∨ ¬Q", (p,q) => p || !q)
            },
            new[] {
                new TruthTableChallengeQuestions.Question("(P → Q) ∧ ¬Q", (p,q) => Implies(p,q) && !q),
                new TruthTableChallengeQuestions.Question("(P ∨ Q) → ¬P", (p,q) => Implies(p || q,!p)),
                new TruthTableChallengeQuestions.Question("¬(P → Q) ∨ Q", (p,q) => !Implies(p,q) || q),
                new TruthTableChallengeQuestions.Question("(P ↔ Q) ∧ ¬P", (p,q) => (p == q) && !p),
                new TruthTableChallengeQuestions.Question("¬(P ⊕ Q) ∨ P", (p,q) => !(p != q) || p),
                new TruthTableChallengeQuestions.Question("(P → Q) ↔ ¬P", (p,q) => Implies(p,q) == !p),
                new TruthTableChallengeQuestions.Question("¬(P ∨ Q) → P", (p,q) => Implies(!(p || q),p)),
                new TruthTableChallengeQuestions.Question("(P ⊕ Q) ↔ ¬Q", (p,q) => (p != q) == !q)
            },
            new[] {
                new TruthTableChallengeQuestions.Question("((P → Q) ∧ P) ↔ ¬Q", (p,q) => (Implies(p,q) && p) == !q),
                new TruthTableChallengeQuestions.Question("¬((P ∨ Q) → (P ∧ Q))", (p,q) => !Implies(p || q,p && q)),
                new TruthTableChallengeQuestions.Question("(P → ¬Q) ↔ (¬P ∧ Q)", (p,q) => Implies(p,!q) == (!p && q)),
                new TruthTableChallengeQuestions.Question("((P ⊕ Q) ∨ ¬P) → Q", (p,q) => Implies((p != q) || !p,q)),
                new TruthTableChallengeQuestions.Question("¬(P ↔ Q) → (P ∧ ¬Q)", (p,q) => Implies(!(p == q),p && !q)),
                new TruthTableChallengeQuestions.Question("(P ∧ (Q → ¬P)) ∨ ¬Q", (p,q) => (p && Implies(q,!p)) || !q),
                new TruthTableChallengeQuestions.Question("((P → Q) → ¬Q) ↔ P", (p,q) => Implies(Implies(p,q),!q) == p),
                new TruthTableChallengeQuestions.Question("(P ∨ ¬Q) ↔ ¬(P → Q)", (p,q) => (p || !q) == !Implies(p,q))
            }
        };

        // Curate compound variants from the existing solo specs rather than
        // changing their definitions. Same five rule families, rigged props,
        // word normalization and conclusion reveal as the existing T3 flow.
        static readonly int[][] InferenceIndices = {
            new[] { 2, 3, 7, 8, 9 }, new[] { 3, 4, 5, 6, 7, 8, 9 },
            new[] { 3, 4, 5, 6, 7, 8, 9 }, new[] { 3, 4, 5, 6, 7, 8, 9 }, new[] { 4, 5, 6, 7, 8, 9 }
        };
        public static readonly InferenceQuestionSpec[][] InferenceBanks = InferenceIndices.Select((indices, round) =>
            indices.Select(i => {
                var source = InferenceStageQuestions.Banks[round][i];
                int blanks = round == 0 ? 2 : round == 1 ? 3 : round < 4 ? 4 : 5;
                return new InferenceQuestionSpec(source.RuleCode, blanks, i, source.Premises.Concat(new[] { source.Conclusion }).ToArray());
            }).ToArray()).ToArray();
    }
}
