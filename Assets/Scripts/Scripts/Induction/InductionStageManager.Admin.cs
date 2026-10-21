using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LogicLegends.Induction
{
    public sealed partial class InductionStageManager
    {
        public bool CanDebugClearChallenge => CanAct && State == InductionState.Active &&
            help != null && !help.OnboardingPending;

        public bool DebugClearCurrentChallenge()
        {
        if (!AdminAuthorization.TryUseGameplayAdmin()) return false;
            if (!CanDebugClearChallenge) return false;
            CompleteCurrentChallenge();
            return true;
        }

        static string AdminSolution(InductionChallenge question)
        {
            var text = new StringBuilder(question.title + "\n" + question.theorem + "\n" + question.instructions + "\n\nEXPECTED PLACEMENTS / PROOF ORDER\n");
            foreach (var answer in question.answers.OrderBy(a => a.Key))
            {
                string[] key = answer.Key.Split(':');
                string altar = key[0] == "Base" ? "Base Case" : key[0] == "Hypothesis" ? "Inductive Hypothesis" : "Inductive Step";
                var scroll = question.choices.First(s => s.id == answer.Value);
                text.AppendLine(altar + " — position " + key[1] + ": " + scroll.expression + " [" + scroll.id + "]");
            }
            text.AppendLine("Leave other anchors empty. Scroll selection at the stand:");
            foreach (var scroll in question.choices)
                text.AppendLine((question.answers.ContainsValue(scroll.id) ? "REQUIRED: " : "DISTRACTOR — leave unplaced: ") + scroll.expression + " [" + scroll.id + "]");
            return text.ToString();
        }

        public void AppendAdminAnswers(List<AdminAnswerPage> pages)
        {
        if (!AdminAuthorization.IsAuthorized) return;
            bool initialized = runVariantIndices != null;
            pages.Add(new AdminAnswerPage("Current question — Induction", "Challenge " + (ChallengeIndex + 1) +
                " of " + InductionChallenges.ChallengesPerRun + " — " + State + "\n" +
                (initialized ? AdminSolution(Challenge) : "Waiting for question data.") +
                "\nThe book starts each challenge. Submit at an altar checks the entire proof; this viewer changes neither."));
            for (int c = 0; c < InductionChallenges.VariantSets.Length; c++)
            {
                var bank = InductionChallenges.VariantSets[c];
                var text = new StringBuilder();
                for (int q = 0; q < bank.Length; q++)
                {
                    bool selected = initialized && runVariantIndices[c] == q;
                    text.AppendLine("QUESTION " + (q + 1) + AdminQuestionAnswers.Mark(selected, selected && c == ChallengeIndex));
                    text.AppendLine(AdminSolution(bank[q]) + "\n");
                }
                pages.Add(new AdminAnswerPage("Challenge " + (c + 1) + " — " + bank[0].title, text.ToString()));
            }
        }
    }
}
