#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using LogicLegends.Graph;
using UnityEngine;
namespace LogicLegends.Trees
{
    public sealed class TreesPlayChecks : MonoBehaviour
    {
        public string Result {get;private set;}="Running";
        TreeStageManager stage;
        int checks;
        void Check(bool pass,string name){if(!pass){Result="FAIL: "+name;throw new Exception(Result);}checks++;}
        IEnumerator Start()
        {
            GraphCompletion.SuppressPersistenceForValidation=true;
            stage=FindFirstObjectByType<TreeStageManager>();
            while(!stage.Ready||!stage.CanAct)yield return null;
            Check(stage.NearBook,"spawn near book");Check(!stage.timer.Running,"paused initially");
            stage.InteractBook();Check(stage.State==TreeStageState.ACTIVE,"activate");
            stage.SelectNode("B");float before=stage.timer.Remaining;stage.Confirm();stage.Confirm();
            Check(Mathf.Approximately(stage.timer.Remaining,before-10),"exact 10 second penalty");Check(stage.Mistakes==1,"duplicate confirmation ignored");Check(stage.board.First=="B"&&stage.board.Edges.Count==6,"incorrect work preserved");
            yield return new WaitForSeconds(stage.feedbackSeconds+.1f);
            stage.SelectNode("A");stage.Confirm();Check(!stage.timer.Running,"correct pauses immediately");
            while(stage.State==TreeStageState.CHALLENGE_COMPLETE||stage.Moving)yield return null;
            Check(stage.ChallengeIndex==1&&stage.State==TreeStageState.WAITING_FOR_ACTIVATION,"round 2 waits for book");
            float remaining=stage.timer.Remaining;yield return new WaitForSeconds(.15f);Check(stage.timer.Remaining==remaining,"paused transition");
            stage.InteractBook();
            stage.SelectNode("A");stage.SelectNode("B");stage.Edit(false);stage.SelectNode("B");stage.SelectNode("A");stage.Edit(false);Check(stage.board.Edges.Count==1,"reject root parent/cycle");
            foreach(var edge in stage.Challenge.required.Skip(1)){stage.SelectNode(edge.source);stage.SelectNode(edge.destination);stage.Edit(false);}
            stage.Confirm();while(stage.State==TreeStageState.CHALLENGE_COMPLETE||stage.Moving)yield return null;
            Check(stage.ChallengeIndex==2,"round 3");stage.InteractBook();
            stage.SelectNode("B");stage.SelectNode("E");stage.Edit(false);Check(stage.board.Edges.Count==4,"multiple parents prevented");
            stage.SelectNode("C");stage.SelectNode("E");stage.Edit(true);stage.SelectNode("B");stage.SelectNode("E");stage.Edit(false);stage.Confirm();
            while(stage.State==TreeStageState.CHALLENGE_COMPLETE||stage.Moving)yield return null;
            Check(stage.ChallengeIndex==3,"round 4");stage.InteractBook();stage.SetLesson(true);Check(!stage.timer.Running,"lesson pauses clock");stage.SetLesson(false);Check(stage.timer.Running,"lesson resumes clock");stage.SelectAnswer(3);stage.Confirm();
            while(stage.State==TreeStageState.CHALLENGE_COMPLETE||stage.Moving)yield return null;
            Check(stage.ChallengeIndex==4&&stage.Correct==4,"no early victory");stage.InteractBook();
            stage.SelectNode("A");stage.SelectNode("C");stage.Edit(true);stage.SelectNode("B");stage.SelectNode("D");stage.Edit(true);stage.Confirm();
            while(stage.State==TreeStageState.CHALLENGE_COMPLETE)yield return null;
            Check(stage.State==TreeStageState.STAGE_COMPLETE&&stage.Correct==5,"five-round victory");Check(!stage.timer.Running,"victory stops clock");
            stage.HandleGameOver();Check(stage.State==TreeStageState.STAGE_COMPLETE,"victory exclusive with game over");
            // Reinitialize just the existing clock to exercise its terminal callback on a live stage.
            Check(stage.timer.ActiveSeconds>0&&stage.Mistakes==1,"active stopwatch and mistakes");
            Result="PASS: "+checks+" Play Mode assertions; five rounds completed; test persistence suppressed.";Debug.Log("TREES PLAY CHECKS "+Result);
        }
        void OnDestroy(){GraphCompletion.SuppressPersistenceForValidation=false;}
    }
}
#endif
