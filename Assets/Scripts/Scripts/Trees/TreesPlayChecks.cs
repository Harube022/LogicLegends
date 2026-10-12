#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using LogicLegends.Graph;
using UnityEngine;
using UnityEngine.UI;
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
            stage=FindFirstObjectByType<TreeStageManager>();
            while(!stage.Ready||!stage.CanAct)yield return null;
            Check(stage.NearBook,"spawn near book");Check(!stage.timer.Running,"paused initially");
            var camera=Camera.main;bool previousProjection=camera.orthographic;var previousViewport=camera.rect;
            var brain=camera.GetComponent<Unity.Cinemachine.CinemachineBrain>();
            Check(stage.boardFocus.focusCamera!=null&&!stage.boardFocus.focusCamera.gameObject.activeSelf,"scene owns an inactive board camera");
            Check(stage.GetComponent<GraphCompletion>()==null,"Trees has no reward completion handler");
            yield return OpenBoard();Check(stage.State==TreeStageState.ACTIVE,"activate");
            Check(brain.enabled&&brain.ActiveVirtualCamera==stage.boardFocus.focusCamera,"dedicated camera is live through enabled Cinemachine brain");
            Check(stage.BoardOpen&&!stage.ui.gameplayControls.activeInHierarchy,"board hides original controls");
            Check(!stage.LocalPlayer.enabled&&GameInput.Instance.GameplayInputBlocked,"board locks movement");
            Check(camera.orthographic&&Quaternion.Angle(camera.transform.rotation,stage.board.transform.rotation)<.1f,"camera faces board in orthographic view");
            var node=stage.board.Nodes.First(n=>n.id=="B");var pick=Button("Board Node B");
            var projected=camera.WorldToScreenPoint(node.transform.position);
            Check(Vector2.Distance(pick.transform.position,projected)<1,"2D tap target follows world node");
            var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=projected};
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer,hits);
            Check(hits.Count>0&&hits[0].gameObject==pick.gameObject,"node receives the UI pointer raycast");
            Check(camera.transform.InverseTransformPoint(stage.LocalPlayer.transform.position).z<0&&camera.transform.InverseTransformPoint(stage.bookStatue.position).z<0,"player and statue stay behind the board camera");
            pick.onClick.Invoke();float exitTime=stage.timer.Remaining;Button("Exit Tree Board").onClick.Invoke();
            Check(!stage.BoardOpen&&stage.LocalPlayer.enabled&&!GameInput.Instance.GameplayInputBlocked,"exit restores exploration");
            yield return WaitForExploration();
            Check(camera.orthographic==previousProjection&&camera.rect==previousViewport,"exit restores camera settings");
            Check(!stage.boardFocus.focusCamera.gameObject.activeSelf&&brain.ActiveVirtualCamera!=stage.boardFocus.focusCamera,"exit returns to exploration camera");
            Check(stage.board.First=="B"&&stage.board.Edges.Count==6,"exit keeps work");
            stage.timer.Tick(.25f);Check(stage.timer.Remaining<exitTime&&stage.timer.Running,"timer continues outside board");
            stage.SelectNode("A");stage.Confirm();Check(stage.board.First=="B"&&stage.Mistakes==0,"exploration cannot edit or confirm");
            yield return OpenBoard();Check(stage.board.First=="B","reopen keeps selected node");
            stage.SelectNode("B");float before=stage.timer.Remaining;stage.Confirm();stage.Confirm();
            Check(Mathf.Approximately(stage.timer.Remaining,before-10),"exact 10 second penalty");Check(stage.Mistakes==1,"duplicate confirmation ignored");Check(stage.board.First=="B"&&stage.board.Edges.Count==6,"incorrect work preserved");
            yield return new WaitForSeconds(stage.feedbackSeconds+.1f);
            Check(stage.BoardOpen&&!stage.LocalPlayer.enabled,"wrong feedback retains board movement lock");
            stage.SelectNode("A");stage.Confirm();Check(!stage.timer.Running,"correct pauses immediately");
            while(stage.State==TreeStageState.CHALLENGE_COMPLETE||stage.Moving)yield return null;
            Check(stage.ChallengeIndex==1&&stage.State==TreeStageState.WAITING_FOR_ACTIVATION,"round 2 waits for book");
            Check(!stage.BoardOpen&&stage.ui.gameplayControls.activeInHierarchy&&!GameInput.Instance.GameplayInputBlocked,"round transition restores exploration");
            float remaining=stage.timer.Remaining;yield return new WaitForSeconds(.15f);Check(stage.timer.Remaining==remaining,"paused transition");
            yield return OpenBoard();
            stage.SelectNode("A");stage.SelectNode("B");stage.Edit(false);stage.SelectNode("B");stage.SelectNode("A");stage.Edit(false);Check(stage.board.Edges.Count==1,"reject root parent/cycle");
            Button("Exit Tree Board").onClick.Invoke();yield return OpenBoard();
            Check(stage.board.Edges.Count==1&&stage.board.First=="B"&&stage.board.Second=="A","exit and reopen keep edited branches and both endpoints");
            foreach(var edge in stage.Challenge.required.Skip(1)){stage.SelectNode(edge.source);stage.SelectNode(edge.destination);stage.Edit(false);}
            stage.Confirm();while(stage.State==TreeStageState.CHALLENGE_COMPLETE||stage.Moving)yield return null;
            Check(stage.ChallengeIndex==2,"round 3");yield return OpenBoard();
            stage.SelectNode("B");stage.SelectNode("E");stage.Edit(false);Check(stage.board.Edges.Count==4,"multiple parents prevented");
            stage.SelectNode("C");stage.SelectNode("E");stage.Edit(true);stage.SelectNode("B");stage.SelectNode("E");stage.Edit(false);stage.Confirm();
            while(stage.State==TreeStageState.CHALLENGE_COMPLETE||stage.Moving)yield return null;
            Check(stage.ChallengeIndex==3,"round 4");yield return OpenBoard();stage.SetLesson(true);Check(!stage.timer.Running,"lesson pauses clock");stage.SetLesson(false);Check(stage.timer.Running,"lesson resumes clock");
            Check(stage.BoardOpen&&!stage.ui.gameplayControls.activeInHierarchy&&!stage.LocalPlayer.enabled,"lesson returns to locked board");stage.SelectAnswer(3);stage.Confirm();
            while(stage.State==TreeStageState.CHALLENGE_COMPLETE||stage.Moving)yield return null;
            Check(stage.ChallengeIndex==4&&stage.Correct==4,"no early victory");yield return OpenBoard();
            stage.SelectNode("A");stage.SelectNode("C");stage.Edit(true);stage.SelectNode("B");stage.SelectNode("D");stage.Edit(true);stage.Confirm();
            while(stage.State==TreeStageState.CHALLENGE_COMPLETE)yield return null;
            Check(stage.State==TreeStageState.STAGE_COMPLETE&&stage.Correct==5,"five-round victory");Check(!stage.timer.Running,"victory stops clock");
            yield return WaitForExploration();
            Check(!stage.BoardOpen&&!stage.ui.gameplayControls.activeInHierarchy&&camera.orthographic==previousProjection,"victory closes board without enabling controls");
            Check(stage.GetComponent<GraphCompletion>()==null,"victory has no reward handler or persistence suppression");
            stage.HandleGameOver();Check(stage.State==TreeStageState.STAGE_COMPLETE,"victory exclusive with game over");
            // Reinitialize just the existing clock to exercise its terminal callback on a live stage.
            Check(stage.timer.ActiveSeconds>0&&stage.Mistakes==1,"active stopwatch and mistakes");
            Result="PASS: "+checks+" Play Mode assertions; five rounds completed; Trees has no rewards handler.";Debug.Log("TREES PLAY CHECKS "+Result);
        }
        Button Button(string name)=>stage.ui.host.GetComponentsInChildren<Button>(true).First(b=>b.name==name);
        IEnumerator OpenBoard(){while(!stage.CanAct)yield return null;stage.InteractBook();while(!stage.CanSolve)yield return null;yield return null;}
        IEnumerator WaitForExploration(){while(stage.boardFocus.Transitioning)yield return null;yield return null;}
    }
}
#endif
