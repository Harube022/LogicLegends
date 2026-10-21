using System.Collections.Generic;
using System.Linq;
using System.Text;

public sealed partial class FunctionsRelationsStageManager
{
    public bool CanDebugClearChallenge => isActiveAndEnabled && !GameplayPaused && !boardOpen &&
        state == FunctionsRelationsState.ACTIVE && (input == null || !input.GameplayInputBlocked);

    public bool DebugClearCurrentChallenge()
    {
        if (!AdminAuthorization.TryUseGameplayAdmin()) return false;
        if (!CanDebugClearChallenge) return false;
        state = FunctionsRelationsState.VALIDATING;
        CloseDiamondChoices();
        mapping.SetInteractive(false);
        StartCoroutine(CorrectSubmission());
        return true;
    }

    static string AdminSolution(MappingChallengeData question)
    {
        var text = new StringBuilder(question.title + "\n" + question.instruction + "\n");
        text.AppendLine("Domain (inputs): " + AdminQuestionAnswers.Values(question.domain));
        text.AppendLine("Codomain (possible outputs): " + AdminQuestionAnswers.Values(question.codomain));
        if (question.questionKind == MappingQuestionKind.BuildExactRelation)
        {
            text.AppendLine("EXPECTED CONNECTIONS (complete arrangement):");
            foreach (var pair in question.requiredPairs) text.AppendLine(pair.ToString());
            text.AppendLine("No extra or missing connections are accepted.");
            text.AppendLine("Resulting range: " + AdminQuestionAnswers.Values(question.requiredPairs.Select(p => p.codomain).Distinct()));
            if (question.requireFunction) text.AppendLine("Must be a function: exactly one output for every input.");
            if (question.requireInjective) text.AppendLine("Must be injective: distinct inputs have distinct outputs.");
            if (question.requireSurjective) text.AppendLine("Must be surjective: every codomain value is used.");
        }
        else
        {
            text.AppendLine("Given connections: " + string.Join(", ", question.initialPairs));
            text.AppendLine("Actual properties: " + MappingRuleEvaluator.Properties(question.initialPairs, question.domain, question.codomain));
            text.AppendLine(question.questionKind == MappingQuestionKind.ClassifyFunction
                ? "EXPECTED ALTAR: " + PedestalName(question.correctClassificationIndex, question.questionKind)
                : "EXPECTED SELECTION (all and only these properties): " + question.correctProperties);
        }
        return text.ToString();
    }

    public void AppendAdminAnswers(List<AdminAnswerPage> pages)
    {
        if (!AdminAuthorization.IsAuthorized) return;
        if (catalog == null) return;
        pages.Add(new AdminAnswerPage("Current question — Functions and Relations",
            "Challenge " + (challengeIndex + 1) + " of " + ChallengeCount + " — " + state + "\n" +
            (CurrentChallenge != null ? AdminSolution(CurrentChallenge) : "Waiting for question data.") +
            "\nThe book starts each challenge. Submit checks the answer; this viewer changes neither."));
        for (int c = 0; c < catalog.ChallengeCount; c++)
        {
            var group = catalog.ChallengeGroups[c];
            var text = new StringBuilder();
            for (int q = 0; q < group.variants.Count; q++)
            {
                bool selected = selectedVariantIndices[c] == q;
                text.AppendLine("QUESTION " + (q + 1) + AdminQuestionAnswers.Mark(selected, selected && c == challengeIndex));
                text.AppendLine(AdminSolution(group.variants[q]) + "\n");
            }
            pages.Add(new AdminAnswerPage("Challenge " + (c + 1) + " — " + group.stageName, text.ToString()));
        }
    }
}
