using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Immutable text snapshots. Creating one never selects, grades or advances a question.</summary>
public sealed class AdminAnswerPage
{
    public readonly string Title, Body;
    public AdminAnswerPage(string title, string body) { Title = title; Body = body; }
}

public static class AdminQuestionAnswers
{
    public static List<AdminAnswerPage> Capture()
    {
        var pages = new List<AdminAnswerPage>();
        string scene = SceneManager.GetActiveScene().name;
        if (scene == "RulesOfInference") UnityEngine.Object.FindFirstObjectByType<LogicLegends.Inference.InferenceChallenge>()?.AppendAdminAnswers(pages);
        else if (scene == "Setssss") UnityEngine.Object.FindFirstObjectByType<SetssssChallengeOne>()?.AppendAdminAnswers(pages);
        else if (scene == "Formal vs. Informal Proofs") UnityEngine.Object.FindFirstObjectByType<ProofsLibraryStageController>()?.AppendAdminAnswers(pages);
        else if (scene == "Direct&Indirect") UnityEngine.Object.FindFirstObjectByType<DirectIndirectProofStageController>()?.AppendAdminAnswers(pages);
        else if (scene == "Functions and Relations") UnityEngine.Object.FindFirstObjectByType<FunctionsRelationsStageManager>()?.AppendAdminAnswers(pages);
        else if (scene == "Induction") UnityEngine.Object.FindFirstObjectByType<LogicLegends.Induction.InductionStageManager>()?.AppendAdminAnswers(pages);
        else if (scene == "PRELIM" && StageSelectionState.SelectedStage == 1) UnityEngine.Object.FindFirstObjectByType<QuizManager>()?.AppendAdminAnswers(pages);
        else if (scene == "PRELIM" && StageSelectionState.SelectedStage == 2)
        {
            UnityEngine.Object.FindFirstObjectByType<DynamicLogicPuzzle>()?.AppendAdminAnswers(pages);
            UnityEngine.Object.FindFirstObjectByType<TruthTableDoorInteraction>()?.AppendAdminAnswers(pages);
        }
        if (pages.Count == 0) pages.Add(new AdminAnswerPage("Current question", "No question data is available in this stage."));
        return pages;
    }
    public static string Mark(bool selected, bool current) => current ? " [CURRENT / SELECTED]" : selected ? " [SELECTED THIS PLAYTHROUGH]" : "";
    public static string Values<T>(IEnumerable<T> values) => "{" + string.Join(", ", values) + "}";
    public static string Choice(string prompt, string[] choices, int correct)
    {
        var text = new StringBuilder(prompt + "\n");
        for (int i = 0; i < choices.Length; i++) text.AppendLine(((char)('A' + i)) + ". " + choices[i]);
        text.AppendLine("CORRECT ANSWER: " + ((char)('A' + correct)) + ". " + choices[correct]);
        return text.ToString();
    }
}

public partial class QuizManager
{
    readonly HashSet<LogicQuestion> adminDrawnQuestions = new HashSet<LogicQuestion>();
    public void AppendAdminAnswers(List<AdminAnswerPage> pages)
    {
        var now = new StringBuilder();
        now.AppendLine("Challenge order: " + string.Join(" → ", challengeOrder.Select(i => challenges[i].topicName)));
        now.AppendLine(currentQuestion == null ? "Waiting for the book. No question has been drawn." :
            AdminQuestionAnswers.Choice(currentQuestion.questionText, currentQuestion.options, currentQuestion.correctOptionIndex));
        if (currentQuestion != null && hasDoorOptionOrder)
            now.AppendLine("Correct physical door (left to right): " + (Array.IndexOf(currentDoorOptionOrder, currentQuestion.correctOptionIndex) + 1));
        now.AppendLine("Questions are drawn at book activation; choices are shuffled across doors. Selected badges record actual draws, including retries.");
        pages.Add(new AdminAnswerPage("Current question — Propositional Logic", now.ToString()));
        foreach (TopicChallenge group in challenges)
        {
            var text = new StringBuilder();
            for (int q = 0; q < group.questionsPool.Count; q++)
            {
                LogicQuestion question = group.questionsPool[q];
                text.AppendLine("QUESTION " + (q + 1) + AdminQuestionAnswers.Mark(adminDrawnQuestions.Contains(question), question == currentQuestion));
                text.AppendLine(AdminQuestionAnswers.Choice(question.questionText, question.options, question.correctOptionIndex));
                text.AppendLine();
            }
            pages.Add(new AdminAnswerPage(group.topicName + " — question bank", text.ToString()));
        }
    }
}

public partial class DynamicLogicPuzzle
{
    public void AppendAdminAnswers(List<AdminAnswerPage> pages)
    {
        if (challengePhase != null) challengePhase.AppendAdminAnswers(pages);
        else
        {
            pages.Add(new AdminAnswerPage("Current question — Truth Table", "The puzzle has not initialized. No new questions were drawn by this viewer."));
            TruthTableChallengePhase.AppendAdminBanks(pages, null);
        }
    }
}

public sealed partial class TruthTableChallengePhase
{
    static string AdminRows(TruthTableChallengeQuestions.Question q) => q.Expression +
        "\nRows top to bottom (P,Q): TT, TF, FT, FF\nCORRECT COLUMN: " +
        string.Join(", ", Enumerable.Range(0, 4).Select(r => q.EvaluateRow(r) ? "True" : "False"));
    public void AppendAdminAnswers(List<AdminAnswerPage> pages)
    {
        pages.Add(new AdminAnswerPage("Current question — Truth Table", "Challenge " + ChallengeNumber + ", column " + ColumnNumber + "\n" + AdminRows(selected[challengeIndex, columnIndex])));
        AppendAdminBanks(pages, this);
    }
    public static void AppendAdminBanks(List<AdminAnswerPage> pages, TruthTableChallengePhase phase)
    {
        for (int d = 0; d < TruthTableChallengeQuestions.Banks.Length; d++)
        {
            var text = new StringBuilder("Three expressions are selected for this difficulty.\n\n");
            var bank = TruthTableChallengeQuestions.Banks[d];
            for (int q = 0; q < bank.Length; q++)
            {
                int column = -1;
                if (phase != null) for (int c = 0; c < 3; c++) if (phase.selected[d, c] == bank[q]) column = c;
                text.AppendLine("QUESTION " + (q + 1) + AdminQuestionAnswers.Mark(column >= 0, phase != null && d == phase.challengeIndex && column == phase.columnIndex));
                if (column >= 0) text.AppendLine("Selected for column " + (column + 1));
                text.AppendLine(AdminRows(bank[q]) + "\n");
            }
            pages.Add(new AdminAnswerPage("Challenge " + (d + 1) + " — " + DifficultyNames[d], text.ToString()));
        }
    }
}

public partial class TruthTableDoorInteraction
{
    public bool AdminQuestionOpen => panelOpen && !answerSubmitted;
    public void AppendAdminAnswers(List<AdminAnswerPage> pages)
    {
        if (questionBank == null) return;
        if (panelOpen && currentDoorIndex >= 0 && assignedQuestions != null)
        {
            var current = assignedQuestions[currentDoorIndex];
            pages.Insert(0, new AdminAnswerPage("Current question — house " + (currentDoorIndex + 1),
                AdminQuestionAnswers.Choice(current.prompt, current.choices, current.correctChoice)));
        }
        var text = new StringBuilder("HOUSE POP QUIZZES\nThe assigned door questions can change after door resets. Badges below show the current assignment.\n\n");
        for (int q = 0; q < questionBank.questions.Count; q++)
        {
            var question = questionBank.questions[q];
            int door = assignedQuestions == null ? -1 : Array.IndexOf(assignedQuestions, question);
            text.AppendLine("QUESTION " + (q + 1) + AdminQuestionAnswers.Mark(door >= 0, door >= 0 && door == currentDoorIndex));
            if (door >= 0) text.AppendLine("Assigned to house " + (door + 1));
            text.AppendLine(AdminQuestionAnswers.Choice(question.prompt, question.choices, question.correctChoice) + "\n");
        }
        pages.Add(new AdminAnswerPage("Truth Table — pop quiz bank", text.ToString()));
    }
}

public sealed partial class ProofsLibraryStageController
{
    readonly int[] adminSelectedVariants = { -1, -1, -1, -1, -1 };
    static string AdminSolution(ProofChallengeData q)
    {
        var text = new StringBuilder(q.title + "\n" + q.context + "\n" + q.instructions + "\n\nEXPECTED SOLUTION\n");
        if (q.kind == ProofChallengeKind.SortScrolls)
            for (int i = 0; i < q.scrolls.Length; i++) text.AppendLine("Scroll " + (char)('A' + i) + ": " + q.scrolls[i].title + "\n" + q.scrolls[i].content + "\nPLACE AT: " + (q.scrolls[i].isFormal ? "Formal" : "Informal") + " altar\n");
        else
        {
            for (int i = 0; i < q.steps.Length; i++) text.AppendLine("Step ID " + i + ": " + q.steps[i].statement + "\nJustification: " + q.steps[i].justification);
            if (q.kind == ProofChallengeKind.MissingRule) text.AppendLine("CORRECT RULE: " + q.expectedRule);
            else foreach (ProofOrderData order in q.acceptableOrders)
                text.AppendLine("Accepted proof order (step IDs): " + string.Join(" → ", order.stepIds));
            if (q.kind == ProofChallengeKind.Formalize) text.AppendLine("For each placed statement, choose its matching justification (same step ID).");
        }
        return text.ToString();
    }
    public void AppendAdminAnswers(List<AdminAnswerPage> pages)
    {
        pages.Add(new AdminAnswerPage("Current question — Proofs Library", activeChallenge == null ? "Waiting for book activation. No variant has been selected for this challenge." : "Challenge " + ChallengeNumber + "\n" + AdminSolution(activeChallenge)));
        for (int c = 0; c < catalog.variantSets.Length; c++)
        {
            var text = new StringBuilder("One variant is drawn when this challenge starts. Future challenges are not selected yet.\n\n");
            var bank = catalog.variantSets[c].variants;
            for (int q = 0; q < bank.Length; q++)
                text.AppendLine("QUESTION " + (q + 1) + AdminQuestionAnswers.Mark(adminSelectedVariants[c] == q, c == challengeIndex && activeChallenge == bank[q]) + "\n" + AdminSolution(bank[q]) + "\n");
            pages.Add(new AdminAnswerPage("Challenge " + (c + 1) + " — Proofs Library", text.ToString()));
        }
    }
}

public sealed partial class DirectIndirectProofStageController
{
    readonly int[] adminSelectedVariants = { -1, -1, -1, -1, -1 };
    static string AdminSolution(TrialProofChallenge q)
    {
        var text = new StringBuilder(q.title + "\n" + q.problem + "\n\nEXPECTED SOLUTION\n");
        text.AppendLine("Method: " + q.correctMethod + "\nPlacement station: " + q.station);
        for (int i = 0; i < q.tablets.Length; i++)
            text.AppendLine("Tablet ID " + i + ": " + q.tablets[i].statement + "\nJustification: " + q.tablets[i].justification);
        if (q.kind != TrialProofKind.IdentifyMethod)
            foreach (TrialProofOrder order in q.acceptedOrders) text.AppendLine("Accepted proof order (tablet IDs): " + string.Join(" → ", order.tabletIds));
        text.AppendLine(q.explanation);
        return text.ToString();
    }
    public void AppendAdminAnswers(List<AdminAnswerPage> pages)
    {
        pages.Add(new AdminAnswerPage("Current question — Direct & Indirect", activeChallenge == null ? "Waiting for book activation. No variant has been selected for this challenge." : "Challenge " + ChallengeNumber + "\n" + AdminSolution(activeChallenge)));
        for (int c = 0; c < catalog.variantSets.Length; c++)
        {
            var text = new StringBuilder("One variant is drawn when this challenge starts.\n\n");
            var bank = catalog.variantSets[c].variants;
            for (int q = 0; q < bank.Length; q++) text.AppendLine("QUESTION " + (q + 1) + AdminQuestionAnswers.Mark(adminSelectedVariants[c] == q, c == challengeIndex && bank[q] == activeChallenge) + "\n" + AdminSolution(bank[q]) + "\n");
            pages.Add(new AdminAnswerPage("Challenge " + (c + 1) + " — Direct & Indirect", text.ToString()));
        }
    }
}

public sealed partial class SetssssChallengeOne
{
    readonly Dictionary<int, int[]> adminSelectedQuestions = new Dictionary<int, int[]>();
    static string AdminSolution(int challenge, SetsConditionData q, ComplementCondition complement)
    {
        var text = new StringBuilder();
        if (challenge == 5)
        {
            text.AppendLine("U = " + AdminQuestionAnswers.Values(complement.universe) + "\n" + complement.setName + " = " + AdminQuestionAnswers.Values(complement.selected));
            text.AppendLine("PLACE inside " + complement.setName + ": " + AdminQuestionAnswers.Values(complement.selected));
            text.AppendLine("PLACE inside U, outside " + complement.setName + " (complement): " + AdminQuestionAnswers.Values(complement.universe.Except(complement.selected)));
            text.AppendLine("Every element of U must be placed once; no elements outside U.");
            return text.ToString();
        }
        var a = new HashSet<string>(q.setA); var b = new HashSet<string>(q.setB);
        text.AppendLine("A = " + AdminQuestionAnswers.Values(q.setA) + "\nB = " + AdminQuestionAnswers.Values(q.setB));
        if (challenge == 4)
        {
            text.AppendLine("A region: " + AdminQuestionAnswers.Values(q.setA) + "\nB region: " + AdminQuestionAnswers.Values(q.setB));
            text.AppendLine("A ∩ B region: " + AdminQuestionAnswers.Values(a.Intersect(b)));
            text.AppendLine("A − B separate region: " + AdminQuestionAnswers.Values(a.Except(b)));
            text.AppendLine("B − A separate region: " + AdminQuestionAnswers.Values(b.Except(a)));
            text.AppendLine("Use separate physical copies for each listed placement. One block counts in only one region.");
        }
        else
        {
            text.AppendLine("A-only: " + AdminQuestionAnswers.Values(a.Except(b)));
            text.AppendLine("B-only: " + AdminQuestionAnswers.Values(b.Except(a)));
            text.AppendLine("Intersection A ∩ B: " + AdminQuestionAnswers.Values(a.Intersect(b)));
            text.AppendLine("Place every listed element once; leave unrelated spawner blocks unplaced.");
        }
        return text.ToString();
    }
    public void AppendAdminAnswers(List<AdminAnswerPage> pages)
    {
        pages.Add(new AdminAnswerPage("Current question — Sets", !HasActiveQuestion ? "Waiting for book activation. No current question." : "Challenge " + challengeNumber + ", question " + (conditionIndex + 1) + " of " + QuestionsPerChallenge + "\n" + AdminSolution(challengeNumber, activeCondition, activeComplement)));
        for (int c = 1; c <= 5; c++)
        {
            SetsConditionData[] bank = c == 1 ? challengeOneBank : c == 2 ? challengeTwoBank : c == 3 ? challengeThreeBank : challengeFourBank;
            int count = c == 5 ? challengeFiveBank.Length : bank.Length;
            var text = new StringBuilder("Five distinct questions are selected when this challenge starts.\n\n");
            int[] selection; adminSelectedQuestions.TryGetValue(c, out selection);
            for (int q = 0; q < count; q++)
            {
                bool current = c == challengeNumber && HasActiveQuestion && selectedEntryIndices[Mathf.Clamp(conditionIndex, 0, QuestionsPerChallenge - 1)] == q;
                text.AppendLine("QUESTION " + (q + 1) + AdminQuestionAnswers.Mark(selection != null && selection.Contains(q), current));
                text.AppendLine(AdminSolution(c, c == 5 ? null : bank[q], c == 5 ? challengeFiveBank[q] : null) + "\n");
            }
            pages.Add(new AdminAnswerPage("Challenge " + c + " — Sets", text.ToString()));
        }
    }
}

namespace LogicLegends.Inference
{
    public partial class InferenceChallenge
    {
        static string AdminSolution(InferenceQuestion q)
        {
            var puzzle = new RuleOfInferencePuzzle(q);
            var text = new StringBuilder("PREMISES\n");
            if (q.Spec != null) foreach (string row in q.Spec.Sentences(q.Example)) text.AppendLine(row);
            else foreach (var premise in q.Form.premises) text.AppendLine(string.Join(" ", premise.tokens.Select(q.Resolve)));
            text.AppendLine("\nFill the blanks, in board order: " + string.Join(" | ", puzzle.CorrectPlacements));
            text.AppendLine("RULE CRYSTAL: " + puzzle.RuleLabel + "\nCONCLUSION: " + puzzle.Conclusion);
            return text.ToString();
        }
        public void AppendAdminAnswers(List<AdminAnswerPage> pages)
        {
            InferenceQuestion current = Question ?? (stageQuestions != null && SolvedRounds < stageQuestions.Length ? stageQuestions[SolvedRounds] : null);
            pages.Add(new AdminAnswerPage("Current question — Rules of Inference", current == null ? "No current question." : "Challenge " + (SolvedRounds + 1) + (HasStarted ? " (active)" : " (waiting for book)") + "\n" + AdminSolution(current)));
            for (int c = 0; c < InferenceStageQuestions.Banks.Length; c++)
            {
                var text = new StringBuilder("One question is selected per challenge. Correct rules do not repeat.\n\n");
                InferenceQuestion chosen = stageQuestions != null && c < stageQuestions.Length ? stageQuestions[c] : null;
                var bank = InferenceStageQuestions.Banks[c];
                for (int q = 0; q < bank.Length; q++)
                {
                    var rule = rules.FirstOrDefault(r => r.abbreviation == bank[q].RuleCode);
                    if (rule == null || rule.examples.Length == 0) continue;
                    bool selected = chosen != null && chosen.Spec == bank[q];
                    var source = selected ? chosen : new InferenceQuestion(rule, bank[q], rule.examples[0]);
                    text.AppendLine("QUESTION " + (q + 1) + " — " + bank[q].Key + AdminQuestionAnswers.Mark(selected, selected && current == chosen));
                    text.AppendLine("Symbolic premises: " + string.Join("; ", bank[q].Premises) + "\nSymbolic conclusion: " + bank[q].Conclusion);
                    text.AppendLine(AdminSolution(source) + "\n");
                }
                pages.Add(new AdminAnswerPage("Challenge " + (c + 1) + " — Inference", text.ToString()));
            }
        }
    }
}
