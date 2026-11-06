using System;
using System.Linq;
using UnityEngine;

// These adapters only observe gameplay. Keys contain question/action progress,
// never player positions, camera poses, or the correct answer.
public partial class QuizManager : ITrialHintSource
{
    public TrialHintAction RequiredHintAction()
    {
        if (IsSequenceComplete || timerManager == null || timerManager.RemainingTime <= 0) return default;
        bool pause = PropositionalControlsOnboarding.Active != null && PropositionalControlsOnboarding.Active.IsActive;
        string key = "logic/" + currentTopicIndex + "/" + nextQuestionOrderIndex;
        return !IsQuizActive ? new TrialHintAction(key + "/book", "Interact with the Book Statue to begin.", pause) :
            new TrialHintAction(key + "/door", "Compare the answers. Stand on your chosen door's pad.", pause);
    }
}

public partial class DynamicLogicPuzzle : ITrialHintSource
{
    public bool IsTruthTableHintSource => houseDoors != null && isActivePuzzle;
    public TrialHintAction RequiredHintAction()
    {
        var clock = houseDoors != null ? houseDoors.GetComponent<TruthTableStageClock>() : null;
        if (clock == null || clock.IsCompleted || clock.IsGameOver || PuzzleCompleted || challengePhase == null) return default;
        string key = "truth/" + challengePhase.ChallengeNumber + "/" + challengePhase.ColumnNumber;
        bool pause = clock.IsTutorialOpen || clock.IsQuizOpen || houseDoors.IsQuizOpen ||
            TruthTableControlsOnboarding.Active != null && TruthTableControlsOnboarding.Active.IsActive;
        if (clock.IsWaitingForBook) return new TrialHintAction(key + "/book", "Interact with the Book Statue to begin.", pause);
        if (challengePhase.CanSubmitColumn) return new TrialHintAction(key + "/submit", "Tap Submit Column to check your answer.", pause);
        key += "/row/" + challengePhase.HintPlacedRows;
        var inventory = InventoryManager.Instance;
        if (inventory != null && inventory.HasBlockSelected())
            return new TrialHintAction(key + "/place", "Place your selected block in the arrow's slot.", pause);
        if (inventory != null && inventory.HasStoredBlocks)
            return new TrialHintAction(key + "/inventory", "Select a True or False block from your inventory.", pause);
        bool available = houseDoors.HasOpenHouseHintBlocks;
        return new TrialHintAction(key + (available ? "/collect" : "/house"), available ?
            "Use the hand button to collect a True or False block." : "Use the hand button at a house to open its quiz.", pause);
    }
}
public sealed partial class TruthTableChallengePhase
{ public int HintPlacedRows => currentRow; }

namespace LogicLegends.Inference
{
    public partial class InferenceChallenge : ITrialHintSource
    {
        string hintFailedPremises;
        string HintPremiseValues => dragPuzzle != null ? string.Join("|", dragPuzzle.Zones.Select(zone => zone.Value)) : string.Join("|", fields.Select(field => field.text));
        public TrialHintAction RequiredHintAction()
        {
            var journey = StageJourneyUI.Instance;
            if (PuzzleCompleted || journey != null && journey.InferenceEnded) return default;
            bool pause = journey != null && journey.InferenceQuizOpen ||
                InferenceControlsOnboarding.Active != null && InferenceControlsOnboarding.Active.IsActive;
            string key = "inference/" + SolvedRounds;
            if (!HasStarted) return new TrialHintAction(key + "/book", "Interact with the Book Statue to begin.", pause || IsBoardOpen);
            if (roundSolved) return new TrialHintAction(key + "/continue", "Tap Continue or Back after reading the conclusion.", true);
            if (Stage == InferencePuzzleStage.CompletingPremises)
            {
                if (!IsBoardOpen) return new TrialHintAction(key + "/board", "Approach the altar and tap View Board.", pause);
                int filled = dragPuzzle != null ? dragPuzzle.Zones.Count(zone => !string.IsNullOrWhiteSpace(zone.Value)) :
                    fields.Count(field => !string.IsNullOrWhiteSpace(field.text));
                int total = dragPuzzle != null ? dragPuzzle.Zones.Count : fields.Count;
                if (hintFailedPremises != null && hintFailedPremises == HintPremiseValues)
                    return new TrialHintAction(key + "/correct-premises", "Correct the red premise blanks, then tap Check argument.", pause);
                return new TrialHintAction(key + "/premises/" + filled + "/" + (filled == total ? "check" : "fill"),
                    filled == total ? "Tap Check argument to check the filled blanks." : "Fill the premise blanks using the word bank.", pause);
            }
            var held = Player.LocalInstance != null ? Player.LocalInstance.GetHeldObject() : null;
            if (held != null && held.GetComponent<InferenceCrystal>() != null)
                return new TrialHintAction(key + (IsBoardOpen ? "/place" : "/return"), IsBoardOpen ?
                    "Tap Place Crystal to check your rule." : "Return to the altar and tap Submit to Altar.", pause);
            return new TrialHintAction(key + "/collect", "Collect a Rule Crystal.", pause || IsBoardOpen);
        }
    }
}

public sealed partial class ProofsLibraryStageController : ITrialHintSource
{
    string hintRejectedProof;
    string HintProofArrangement => challengeIndex + "/" + questionIndex + "/" +
        (Current.kind == ProofChallengeKind.SortScrolls ? string.Join(",", placements) : worldBoard.HintPlacementProgress);
    public TrialHintAction RequiredHintAction()
    {
        if (finished || secondsLeft <= 0) return default;
        bool pause = InputSuppressed || HasReaderOpen || transitioning || flashing;
        string key = "proofs/" + challengeIndex + "/" + questionIndex + "/" + mistakeCount;
        if (!started) return new TrialHintAction(key + "/book", "Interact with the Book Statue to begin.", pause);
        if (HasCarriedScroll) return new TrialHintAction(key + "/place", "Place your scroll at an available altar or slot.", pause);
        if (hintRejectedProof != null && hintRejectedProof == HintProofArrangement)
            return new TrialHintAction(key + "/correct", "Review your placements. Take back a scroll to correct it.", pause);
        bool ready = Current.kind == ProofChallengeKind.SortScrolls ?
            placements.Take(Current.scrolls.Length).All(value => value >= 0) : worldBoard != null && worldBoard.IsComplete();
        if (ready) return new TrialHintAction(key + "/submit", "Return to the Book Statue to submit your answer.", pause);
        string progress = Current.kind == ProofChallengeKind.SortScrolls ? string.Join(",", placements) : worldBoard.HintPlacementProgress;
        return new TrialHintAction(key + "/collect/" + progress, "Read a scroll, then tap Get This Scroll.", pause);
    }
}
public sealed partial class ProofWorldBoardController
{ public string HintPlacementProgress => string.Join(",", assignedSteps) + "/" + string.Join(",", assignedRules); }

public sealed partial class DirectIndirectProofStageController : ITrialHintSource
{
    string hintRejectedProof;
    string HintProofArrangement => challengeIndex + "/" + questionIndex + "/" + selectedMethod + "/" + string.Join(",", placedTabletIds);
    public TrialHintAction RequiredHintAction()
    {
        if (Finished) return default;
        bool pause = GuidanceBlocksGameplay || readerPanel != null && readerPanel.activeSelf || IsViewingBoard || state == TrialState.Validating || state == TrialState.ChallengeComplete;
        string key = "direct/" + challengeIndex + "/" + questionIndex + "/" + mistakeCount;
        if (state == TrialState.WaitingForActivation) return new TrialHintAction(key + "/book", "Interact with the Book Statue to begin.", pause);
        if (hintRejectedProof != null && hintRejectedProof == HintProofArrangement)
            return new TrialHintAction(key + "/correct", challengeIndex == 0 ? "Review the proof. Change your altar selection to correct it." :
                altarBoard != null && altarBoard.IsOpen ? "Review the proof steps and change their order." : "Use the hand at the altar to review and correct your proof.", pause);
        if (challengeIndex == 0)
            return new TrialHintAction(key + (selectedMethod < 0 ? "/choose" : "/submit"), selectedMethod < 0 ?
                "Use the hand button at your chosen proof altar." : "Return to the Book Statue to submit your answer.", pause);
        if (!altarBoardOpened || !stepsReordered)
            return new TrialHintAction(key + (altarBoard != null && altarBoard.IsOpen ? "/arrange" : "/altar"), altarBoard != null && altarBoard.IsOpen ?
                "Arrange the proof steps, then return to the Book Statue." : "Use the hand button at the active altar.", pause);
        if (Current.kind == TrialProofKind.ChooseAndArrange && selectedMethod < 0)
            return new TrialHintAction(key + "/strategy", "Choose a proof strategy on the altar board.", pause);
        return new TrialHintAction(key + "/submit", "Return to the Book Statue to submit your answer.", pause);
    }
}

namespace LogicLegends.Induction
{
    public sealed partial class InductionStageManager : ITrialHintSource
    {
        public TrialHintAction RequiredHintAction()
        {
            if (!Ready || State == InductionState.GameOver || State == InductionState.StageComplete) return default;
            bool pause = Reading || help != null && (help.IsOpen || help.OnboardingPending) ||
                onboarding != null && onboarding.IsActive || boardView != null && boardView.Viewing;
            string key = "induction/" + ChallengeIndex + "/" + Mistakes;
            if (State == InductionState.WaitingForBook) return new TrialHintAction(key + "/book", "Interact with the Book Statue to begin.", pause);
            if (Held != null) return new TrialHintAction(key + "/place", "Place your scroll at an indicated proof station.", pause);
            var required = slots.Where(slot => slot != null && slot.gameObject.activeInHierarchy).ToArray();
            if (required.Any(slot => slot.HasError)) return new TrialHintAction(key + "/correct", "Replace the marked scrolls or fill missing slots, then tap Submit.", pause);
            if (required.Length > 0 && required.All(slot => slot.Tablet != null))
                return new TrialHintAction(key + "/submit", "Tap Submit to check your proof.", pause);
            return new TrialHintAction(key + "/collect/" + required.Count(slot => slot.Tablet != null), "Choose a formula scroll from the scroll stand.", pause);
        }
    }
}

public sealed partial class SetssssChallengeOne : ITrialHintSource
{
    int hintPlacementRevision, hintCheckRevision;
    bool hintNeedsCorrection;
    public TrialHintAction RequiredHintAction()
    {
        if (complete || gameOverTriggered || player == null) return default;
        bool pause = advancing || boardView != null && boardView.IsViewingBoard ||
            introductions != null && (introductions.IsOpen || introductions.MustIntroduceCurrentChallenge) ||
            SetssssControlsOnboarding.Active != null && SetssssControlsOnboarding.Active.IsActive;
        string key = "sets/" + challengeNumber + "/" + conditionIndex + "/" + hintPlacementRevision + "/" + hintCheckRevision;
        if (!started) return new TrialHintAction(key + "/book", "Interact with the Book Statue to begin.", pause);
        if (player.GetHeldObject() != null) return new TrialHintAction(key + "/place", "Place the element in the required region.", pause);
        int placed = AnswerBlocks().Count();
        int required = challengeNumber == 5 ? activeComplement.universe.Length : challengeNumber == 4 ?
            ExpectedFiveRegionValues().Sum(pair => pair.Value.Count) : activeCondition.setA.Union(activeCondition.setB).Count();
        if (hintNeedsCorrection)
            return new TrialHintAction(key + "/correct", "Keep green placements. Move red elements or add missing elements.", pause);
        if (challengeNumber == 5 && releasedBlocks.Count == 0)
            return new TrialHintAction(key + "/complement", "Move elements to match the complement question on the board.", pause);
        if (placed >= required) return new TrialHintAction(key + "/submit", "Tap Submit to check your placements.", pause);
        return new TrialHintAction(key + "/collect", "Collect an element needed for the question on the board.", pause);
    }
}

public sealed partial class FunctionsRelationsStageManager : ITrialHintSource
{
    string hintRejectedMapping;
    string HintMappingArrangement => challengeIndex + "/" + questionIndex + "/" + classificationAnswer + "/" + chosenProperties + "/" +
        string.Join(",", mapping.Pairs.Select(pair => pair.ToString()).OrderBy(pair => pair));
    public TrialHintAction RequiredHintAction()
    {
        if (state == FunctionsRelationsState.GAME_OVER || state == FunctionsRelationsState.STAGE_COMPLETE || CurrentChallenge == null) return default;
        bool pause = GameplayPaused || boardOpen || informationBoardView != null && informationBoardView.Viewing ||
            state == FunctionsRelationsState.VALIDATING || state == FunctionsRelationsState.CHALLENGE_COMPLETE;
        string key = "relations/" + challengeIndex + "/" + questionIndex + "/" + mistakeCount;
        if (state == FunctionsRelationsState.WAITING_FOR_ACTIVATION) return new TrialHintAction(key + "/book", "Interact with the Book Statue to begin.", pause);
        if (mapping.SelectedDomain == null && hintRejectedMapping != null && hintRejectedMapping == HintMappingArrangement)
            return new TrialHintAction(key + "/correct", CurrentChallenge.questionKind == MappingQuestionKind.BuildExactRelation ?
                "Select an input at Domain, then use Range's Remove control." : "Review the relation and change your pedestal choices.", pause);
        if (CurrentChallenge.questionKind == MappingQuestionKind.ClassifyFunction)
            return new TrialHintAction(key + (classificationAnswer.HasValue ? "/submit" : "/classify"), classificationAnswer.HasValue ?
                "Tap Submit to check your answer." : "Select your answer at a classification pedestal.", pause);
        if (CurrentChallenge.questionKind == MappingQuestionKind.SelectProperties)
            return new TrialHintAction(key + (chosenProperties != MappingProperty.None ? "/submit" : "/properties"), chosenProperties != MappingProperty.None ?
                "Select all applicable properties, then tap Submit." : "Use the property pedestals to select your answer.", pause);
        if (mapping.SelectedDomain != null)
            return new TrialHintAction(key + "/output/" + mapping.ConnectionCount, "Use the hand button at Range to select an output.", pause);
        if (mapping.ConnectionCount >= CurrentChallenge.requiredPairs.Count)
            return new TrialHintAction(key + "/submit", "Tap Submit to check your connections.", pause);
        return new TrialHintAction(key + "/input/" + mapping.ConnectionCount, "Select an input from Domain.", pause);
    }
}

namespace LogicLegends.Graph
{
    public sealed partial class GraphStageManager : ITrialHintSource
    {
        string hintRejectedGraph;
        string HintGraphArrangement => ChallengeIndex + "/" + QuestionIndex + "/" + Answer + "/" + string.Join(",", Tracker.Route);
        public TrialHintAction RequiredHintAction()
        {
            if (!Ready || State == GraphStageState.GAME_OVER || State == GraphStageState.STAGE_COMPLETE) return default;
            bool pause = Moving || HelpOpen || OnboardingPending || TutorialActive ||
                Introductions != null && (Introductions.IsOpen || !Introductions.HasSeen(ChallengeIndex)) ||
                BoardView != null && BoardView.IsOpen || State == GraphStageState.VALIDATING || State == GraphStageState.CHALLENGE_COMPLETE;
            string key = "graphs/" + ChallengeIndex + "/" + QuestionIndex + "/" + Mistakes;
            if (State == GraphStageState.WAITING_FOR_ACTIVATION) return new TrialHintAction(key + "/book", "Interact with the Book Statue to begin.", pause);
            if (hintRejectedGraph != null && hintRejectedGraph == HintGraphArrangement)
                return new TrialHintAction(key + "/correct", Challenge.type == GraphChallengeType.Degree ?
                    "Recount the incident edges and change your selected number." : "Review the board. Tap Retry Route to try a new route.", pause);
            if (Challenge.type == GraphChallengeType.Degree)
                return new TrialHintAction(key + (Answer.HasValue ? "/confirm" : "/number"), Answer.HasValue ?
                    "Tap Confirm to check your selected number." : "Count the incident edges, then select a number.", pause);
            if (!Tracker.IsContinuous)
                return new TrialHintAction(key + "/retry", "Tap Retry Route to restart your route.", pause);
            bool atEnd = Tracker.Route.Count > 1 && Tracker.AtVertex && Challenge.requiredVertices.All(vertex => Tracker.Route.Contains(vertex)) &&
                (Challenge.type != GraphChallengeType.Cycle || Tracker.Route.Count - 1 >= Challenge.minimumCycleVertices) && Tracker.Current ==
                (Challenge.type == GraphChallengeType.Cycle ? Challenge.start : Challenge.destination);
            return new TrialHintAction(key + (atEnd ? "/confirm" : "/route/" + string.Join(",", Tracker.Route)), atEnd ?
                "Tap Confirm to check your route." : "Follow the board's route using the connecting bridges.", pause);
        }
    }
}

namespace LogicLegends.Trees
{
    public sealed partial class TreeStageManager : ITrialHintSource
    {
        string hintRejectedTree;
        string HintTreeArrangement => ChallengeIndex + "/" + QuestionIndex + "/" + Answer + "/" +
            (Challenge.Editable ? string.Join(",", board.Edges.Select(edge => edge.Key(Challenge.Directed)).OrderBy(edge => edge)) : board.First);
        public TrialHintAction RequiredHintAction()
        {
            if (!Ready || State == TreeStageState.GAME_OVER || State == TreeStageState.STAGE_COMPLETE) return default;
            bool pause = LessonOpen || Moving || Introductions != null && Introductions.IsOpen ||
                boardFocus != null && boardFocus.Transitioning || State == TreeStageState.VALIDATING || State == TreeStageState.CHALLENGE_COMPLETE;
            string key = "trees/" + ChallengeIndex + "/" + QuestionIndex + "/" + Mistakes;
            if (!BoardOpen) return new TrialHintAction(key + "/book", State == TreeStageState.ACTIVE ?
                "Use the hand at the Book Statue to reopen the tree board." : "Interact with the Book Statue to begin.", pause);
            if (!Challenge.Editable && hintRejectedTree != null && hintRejectedTree == HintTreeArrangement)
                return new TrialHintAction(key + "/correct", Answer.HasValue ? "Review the tree and change your selected number." : "Review the question and choose another node.", pause);
            if (Challenge.type == TreeChallengeType.Depth || Challenge.type == TreeChallengeType.Height)
                return new TrialHintAction(key + (Answer.HasValue ? "/submit" : "/number"), Answer.HasValue ?
                    "Tap Submit to check your answer." : "Count edges for the requested depth or height. Select a number.", pause);
            if (!Challenge.Editable)
                return new TrialHintAction(key + (board.First == null ? "/node" : "/submit"), board.First == null ?
                    "Tap the node described in the question." : "Tap Submit to check your selected node.", pause);
            string edits = string.Join(",", board.Edges.Select(edge => edge.Key(Challenge.Directed)).OrderBy(edge => edge));
            key += "/" + edits;
            if (board.First != null && board.Second != null)
                return new TrialHintAction(key + "/edit", "Tap Connect or Remove for the selected pair.", pause);
            if (board.First != null) return new TrialHintAction(key + "/second", "Tap the second endpoint of the branch.", pause);
            // Structural checks only: do not test the solution before submission.
            bool edited = edits != string.Join(",", Challenge.initial.Select(edge => edge.Key(Challenge.Directed)).OrderBy(edge => edge));
            bool filled = board.Edges.Count == Challenge.nodes.Count - 1;
            if (edited && filled) return new TrialHintAction(key + "/submit", "Tap Submit to check your tree. You can still edit branches.", pause);
            return new TrialHintAction(key + "/first", "Select two branch endpoints, then use Connect or Remove.", pause);
        }
    }
}
