using System;
using System.Linq;

namespace LogicLegends.OpenArchive.Topics
{
    [Serializable] public sealed class ArchiveBarrierChallengeState
    {
        public int actor, captureSerial, huntSerial, kind, sourceTopic, sourceRound, seed, revision, hintLevel, outcome;
        public int bankVersion, questionIndex = -1;
        public int selected = -1;
        public string encounterId = "";
        public string feedback = "";
    }
    [Serializable] public sealed class ArchiveLearningHint
    {
        public int actor, topic, round, subphase, resetSerial, level;
        public string diagnosis;
    }

    /// <summary>The shared module-based monster bank; retains legacy solo adapters for old content checks.</summary>
    public sealed class ArchiveBarrierProblem
    {
        public readonly ArchiveQuestion source;
        public readonly bool inference;
        public readonly string title, prompt;
        public readonly string[] options;
        readonly int[] optionIds;
        readonly ArchiveMonsterChallenge bankQuestion;
        public string QuestionId=>bankQuestion?.id??"legacy";
        public string ModuleSource=>bankQuestion?.module??"";
        public int Topic=>bankQuestion?.topic??(inference?2:1);
        public int[] SourceSlides=>bankQuestion?.slides??Array.Empty<int>();
        public ArchiveBarrierProblem(ArchiveTopicCatalog catalog, ArchiveBarrierChallengeState state)
        {
            inference = state.kind == 1;
            if(state.bankVersion>0)
            {
                if(state.bankVersion!=ArchiveMonsterChallengeBank.CurrentVersion)throw new InvalidOperationException("Monster challenge content version mismatch.");
                bankQuestion=ArchiveMonsterChallengeBank.Get(state.questionIndex);
                title=(inference?"RAJANG":"KHEZU")+" · DM1 ESCAPE CHALLENGE";
                prompt=ArchiveTopicCatalog.Names[bankQuestion.topic]+"\n\n"+bankQuestion.prompt;
                optionIds=ArchiveTopicQuestions.Permutation(bankQuestion.options.Length,unchecked(state.seed ^ state.questionIndex*7919 ^ 0x4d3101));
                options=optionIds.Select(i=>bankQuestion.options[i]).ToArray();
                return;
            }
            source = ArchiveTopicQuestions.Build(catalog, inference ? 2 : 1, state.sourceRound, state.seed, inference ? 1 : 0);
            title = inference ? "RAJANG · PROVE YOUR ESCAPE" : "KHEZU · BREAK THE FALSE CLAIM";
            prompt = inference
                ? "Which rule justifies the stated conclusion?\n\nPREMISES\n" + source.prompt + "\n\n" + source.successText
                : "Disprove the claim ‘always TRUE’.\n\n" + source.prompt +
                  "\n\nChoose any assignment that makes the expression FALSE. If all four rows are TRUE, choose No counterexample.";
            if (inference)
            {
                var pool = ArchiveTopicQuestions.Permutation(source.items.Length, unchecked(state.seed + 173)).Where(i => i != source.expected[0]).Take(3).Append(source.expected[0]).ToArray();
                optionIds = ArchiveTopicQuestions.Permutation(pool.Length, unchecked(state.seed + 733)).Select(i => pool[i]).ToArray();
                options = optionIds.Select(i => source.items[i]).ToArray();
            }
            else options = source.slots.Concat(new[] { "No counterexample · always true" }).ToArray();
        }
        public string OptionDetail(int choice) => bankQuestion!=null?
            (choice>=0&&choice<options.Length?"Selected: "+options[choice]+". Confirm when ready.":""):
            inference && choice >= 0 && choice < options.Length ? source.details[optionIds[choice]] : "";
        public bool Validate(int choice, out string explanation)
        {
            explanation = "Select an answer before confirming.";
            if (choice < 0 || choice >= options.Length) return false;
            if(bankQuestion!=null)
            {
                bool accepted=optionIds[choice]==bankQuestion.correctIndex;
                explanation=(accepted?"Correct. ":"The correct answer is "+bankQuestion.options[bankQuestion.correctIndex]+". ")+bankQuestion.explanation;
                return accepted;
            }
            var candidate = new ArchiveStationState();
            ArchiveTopicQuestions.Prepare(source, candidate);
            if (inference) candidate.placements[0] = optionIds[choice];
            else
            {
                // Only the chosen row is under test. Other rows use the bank's known outputs.
                // This accepts every false row, rather than selecting one arbitrary counterexample.
                candidate.placements = source.expected.ToArray();
                if (choice < source.slots.Length) candidate.placements[choice] = 1;
                else candidate.placements = Enumerable.Repeat(0, source.slots.Length).ToArray();
            }
            bool correct = ArchiveTopicQuestions.Validate(source, candidate, out _);
            if (inference)
                explanation = correct ? options[choice] + " is valid. " + OptionDetail(choice) + "\n" + source.successText
                    : options[choice] + " does not justify this conclusion from these premises. The valid rule is " + source.items[source.expected[0]] + ". " + source.details[source.expected[0]];
            else if (choice < source.slots.Length)
                explanation = source.slots[choice] + " gives " + (source.expected[choice] == 1 ? "FALSE" : "TRUE") + " for " + source.prompt + ". " +
                    (correct ? "One false case disproves an always-true claim." : "A true case cannot disprove an always-true claim. Evaluate the entire expression, including negations.");
            else explanation = correct ? "All four assignments give TRUE. This expression is a tautology, so no counterexample exists."
                : "At least one assignment gives FALSE. The expression is not a tautology.";
            return correct;
        }
        public string Hint(int level)
        {
            if (level <= 0) return "Hints are free. The shared encounter clock keeps running. Answer promptly to rejoin your team.";
            if(bankQuestion!=null)return bankQuestion.hints[Math.Min(3,level)-1];
            if (inference)
            {
                if (level == 1) return "Treat the premises as given. Compare their logical structure with the conclusion; the story's real-world truth is irrelevant.";
                if (level == 2) return "Write a letter for each simple statement. Preserve AND, OR, NOT and IF. Look for a rule whose premises AND conclusion have that pattern. You can inspect a selected rule before confirming.";
                return "Separate example: P → Q and ¬Q imply ¬P by Modus Tollens. This is different from affirming Q to conclude P, which is invalid. Match the structure of your actual problem.";
            }
            if (level == 1) return "A counterexample is one case where a universal claim fails. Here you need the whole expression to evaluate to FALSE.";
            if (level == 2) return "Work inside parentheses first, then negation. AND needs two true inputs; OR needs at least one; P → Q is false only for T → F; ↔ needs matching values.";
            return "Separate example: for the claim ‘P ∧ Q is always true’, P=T and Q=F gives FALSE, so it is a counterexample. A different false row would also work. Apply that method to your displayed expression.";
        }
    }
}
