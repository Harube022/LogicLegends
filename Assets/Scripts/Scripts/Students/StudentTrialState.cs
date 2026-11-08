using System;
using System.Linq;
using UnityEngine;
using LogicLegends.Students;

namespace LogicLegends.Students
{
    public interface IStudentTrialSource { StudentTrialState StudentState(); }
    public sealed class StudentTrialState
    {
        public int Trial, Challenge;
        public string QuestionKey, Topic, Concept;
        public TrialHintAction Action;
        public bool Finished, Ready=true, GraphVisible=true;
        public Transform[] Areas=Array.Empty<Transform>();
        public static StudentTrialState Make(int trial,int challenge,string question,ITrialHintSource source,string topic,string concept, params Transform[] areas)
        {
            var action=source.RequiredHintAction();
            return new StudentTrialState{Trial=trial,Challenge=challenge,QuestionKey=question,Action=action,
                Topic=topic,Concept=concept,Finished=!action.Available,Areas=areas.Where(a=>a!=null).ToArray()};
        }
    }
    public static class StudentConcepts
    {
        public static string Logic(string expression)
        {
            string t=(expression??"").ToLowerInvariant();
            if(t.Contains("↔")||t.Contains("if and only if")||t.Contains("bicond"))return "A biconditional is true when both statements have the same truth value.";
            if(t.Contains("→")||t.Contains("->")||t.Contains("implication"))return "An implication is false only when its first statement is True and its second is False.";
            if(t.Contains("exclusive")||t.Contains("⊕"))return "Exclusive OR is true when exactly one statement is true.";
            if(t.Contains("∧")||t.Contains("conjunction"))return "A conjunction is true only when both statements are true.";
            if(t.Contains("∨")||t.Contains("disjunction"))return "An inclusive disjunction is true when at least one statement is true.";
            if(t.Contains("¬")||t.Contains("negation"))return "Negation reverses a statement's truth value. Evaluate inner parentheses first.";
            return "Keep each statement's truth value fixed while evaluating the displayed expression.";
        }
    }
}

public partial class QuizManager : IStudentTrialSource
{
    public StudentTrialState StudentState()=>StudentTrialState.Make(1,currentTopicIndex,nextQuestionOrderIndex.ToString(),this,
        GetCurrentChallenge()?.topicName,StudentConcepts.Logic(GetCurrentChallenge()?.topicName+" "+currentQuestion?.questionText),
        challenges.Where(c=>c!=null&&c.topicSpawnPoint!=null).Select(c=>c.topicSpawnPoint).ToArray());
}
public partial class DynamicLogicPuzzle : IStudentTrialSource
{
    public StudentTrialState StudentState()
    {
        if(!IsTruthTableHintSource || challengePhase==null)return new StudentTrialState{Ready=false};
        var expression=challengePhase.GetSelectedExpression(challengePhase.ChallengeNumber-1,challengePhase.ColumnNumber-1);
        return StudentTrialState.Make(2,challengePhase.ChallengeNumber-1,challengePhase.ColumnNumber.ToString(),this,expression,StudentConcepts.Logic(expression));
    }
}
namespace LogicLegends.Inference
{
    public partial class InferenceChallenge : IStudentTrialSource
    {
        public StudentTrialState StudentState()=>StudentTrialState.Make(3,SolvedRounds,Question?.Id??"waiting",this,"Current argument",
            Stage==InferencePuzzleStage.CompletingPremises?"Premises are accepted statements. Fill the missing parts so the displayed argument remains consistent.":
            "Compare the structure of the completed premises with the rule labels. A rule must justify the conclusion, not just contain familiar words.");
    }
}
public sealed partial class ProofsLibraryStageController : IStudentTrialSource
{
    public StudentTrialState StudentState()=>StudentTrialState.Make(4,challengeIndex,questionIndex.ToString(),this,Current.title,
        Current.kind==ProofChallengeKind.SortScrolls?"Formal proofs state justified mathematical steps explicitly. Informal proofs explain valid reasoning in prose; informal does not mean incorrect.":
        "A proof step may use only definitions and results already established. Keep each statement with its justification.",bookStatue);
}
public sealed partial class DirectIndirectProofStageController : IStudentTrialSource
{
    public StudentTrialState StudentState()=>StudentTrialState.Make(5,challengeIndex,questionIndex.ToString(),this,Current.title,
        "A direct proof starts from the hypotheses. Contraposition proves the contrapositive; contradiction assumes the negation and derives an impossibility.",bookStatue);
}
namespace LogicLegends.Induction
{
    public sealed partial class InductionStageManager : IStudentTrialSource
    {
        public StudentTrialState StudentState()
        {
            if(!Ready)return new StudentTrialState{Ready=false};
            return StudentTrialState.Make(6,ChallengeIndex,ActiveVariantIndex.ToString(),this,Challenge.theorem,
                "Check the stated starting value. In the inductive step, use the hypothesis for k to justify the statement for k + 1.",book);
        }
    }
}
public sealed partial class SetssssChallengeOne : IStudentTrialSource
{
    public StudentTrialState StudentState()=>StudentTrialState.Make(7,challengeNumber-1,conditionIndex.ToString(),this,CurrentProblem,
        new[]{"An element block represents one element. Use the set requested by this question.",
        "Compare membership in both displayed sets before choosing a placement region.",
        "The intersection A ∩ B contains elements shared by both sets.",
        "A − B contains elements in A but not B; B − A reverses that test. This challenge also needs copies in its other required regions.",
        "A complement is taken within U: select elements of the universal set that are outside the specified set."}[Mathf.Clamp(challengeNumber-1,0,4)]);
}
public sealed partial class FunctionsRelationsStageManager : IStudentTrialSource
{
    public StudentTrialState StudentState()
    {
        if(CurrentChallenge==null || mapping==null)return new StudentTrialState{Ready=false};
        return StudentTrialState.Make(8,challengeIndex,questionIndex.ToString(),this,CurrentChallenge.instruction,
            CurrentChallenge.questionKind==MappingQuestionKind.BuildExactRelation?
            "Choose an input before an output. Compare each connection with the current mapping instructions; a connection's Remove control changes only that pair.":
            "For a function, each domain element must have exactly one output. Different inputs are allowed to share an output.",bookStatue);
    }
}
namespace LogicLegends.Graph
{
    public sealed partial class GraphStageManager : IStudentTrialSource
    {
        public StudentTrialState StudentState()
        {
            if(!Ready)return new StudentTrialState{Ready=false};
            var state=StudentTrialState.Make(9,ChallengeIndex,QuestionIndex.ToString(),this,Challenge.objective,
                Challenge.type==GraphChallengeType.DirectedPath?"A directed edge can be followed only in its arrow's direction.":
                Challenge.type==GraphChallengeType.Cycle?"A simple cycle returns to its start without repeating an intermediate vertex.":
                "Platforms represent vertices and bridges represent edges. A simple path cannot repeat a vertex.",bookStatue);
            state.GraphVisible=GraphRevealed;return state;
        }
    }
}
namespace LogicLegends.Trees
{
    public sealed partial class TreeStageManager : IStudentTrialSource
    {
        public StudentTrialState StudentState()
        {
            if(!Ready)return new StudentTrialState{Ready=false};
            return StudentTrialState.Make(10,ChallengeIndex,QuestionIndex.ToString(),this,Challenge.objective,
                Challenge.type==TreeChallengeType.Depth?"Depth counts edges from the root to the requested node.":
                Challenge.type==TreeChallengeType.Height?"Height counts edges on the longest downward path, not the number of nodes.":
                "A tree is connected and has no cycle. With n vertices it has n − 1 edges; that edge count alone does not prove it is a tree.",bookStatue);
        }
    }
}
