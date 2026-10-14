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
            var firstProblem=stage.Challenge;
            string reason;
            string wrongNode=firstProblem.nodes.First(n=>!TreeAnswerValidator.Validate(firstProblem,firstProblem.initial,n.id,null,out reason)).id;
            string rightNode=firstProblem.nodes.First(n=>TreeAnswerValidator.Validate(firstProblem,firstProblem.initial,n.id,null,out reason)).id;
            var node=stage.board.Nodes.First(n=>n.id==wrongNode);var pick=Button("Board Node "+wrongNode);
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
            Check(stage.board.First==wrongNode&&stage.board.Edges.Count==6,"exit keeps work");
            stage.timer.Tick(.25f);Check(stage.timer.Remaining<exitTime&&stage.timer.Running,"timer continues outside board");
            stage.SelectNode(rightNode);stage.Confirm();Check(stage.board.First==wrongNode&&stage.Mistakes==0,"exploration cannot edit or confirm");
            yield return OpenBoard();Check(stage.board.First==wrongNode,"reopen keeps selected node");
            stage.SelectNode(wrongNode);float before=stage.timer.Remaining;stage.Confirm();stage.Confirm();
            Check(Mathf.Approximately(stage.timer.Remaining,before-10),"exact 10 second penalty");Check(stage.Mistakes==1,"duplicate confirmation ignored");Check(stage.board.First==wrongNode&&stage.board.Edges.Count==6,"incorrect work preserved");
            yield return new WaitForSeconds(stage.feedbackSeconds+.1f);
            Check(stage.BoardOpen&&!stage.LocalPlayer.enabled,"wrong feedback retains board movement lock");
            Check(stage.Challenge==firstProblem,"wrong attempt retains selected variant");
            stage.SelectNode(rightNode);stage.Confirm();Check(!stage.timer.Running,"correct pauses immediately");
            while(stage.State==TreeStageState.CHALLENGE_COMPLETE||stage.Moving)yield return null;
            Check(stage.ChallengeIndex==1&&stage.State==TreeStageState.WAITING_FOR_ACTIVATION,"round 2 waits for book");
            Check(!stage.BoardOpen&&stage.ui.gameplayControls.activeInHierarchy&&!GameInput.Instance.GameplayInputBlocked,"round transition restores exploration");
            float remaining=stage.timer.Remaining;yield return new WaitForSeconds(.15f);Check(stage.timer.Remaining==remaining,"paused transition");
            yield return OpenBoard();
            var firstEdge=stage.Challenge.required[0];
            stage.SelectNode(firstEdge.source);stage.SelectNode(firstEdge.destination);stage.Edit(false);
            stage.SelectNode(firstEdge.destination);stage.SelectNode(firstEdge.source);stage.Edit(false);Check(stage.board.Edges.Count==1,"reject root parent/cycle");
            Button("Exit Tree Board").onClick.Invoke();yield return OpenBoard();
            Check(stage.board.Edges.Count==1&&stage.board.First==firstEdge.destination&&stage.board.Second==firstEdge.source,"exit and reopen keep edited branches and both endpoints");
            foreach(var edge in stage.Challenge.required.Skip(1)){stage.SelectNode(edge.source);stage.SelectNode(edge.destination);stage.Edit(false);}
            stage.Confirm();while(stage.State==TreeStageState.CHALLENGE_COMPLETE||stage.Moving)yield return null;
            Check(stage.ChallengeIndex==2,"round 3");yield return OpenBoard();
            var oldEdge=stage.Challenge.initial.First(e=>!stage.Challenge.required.Any(r=>r.Key(true)==e.Key(true)));
            var newEdge=stage.Challenge.required.First(e=>!stage.Challenge.initial.Any(r=>r.Key(true)==e.Key(true)));
            stage.SelectNode(newEdge.source);stage.SelectNode(newEdge.destination);stage.Edit(false);Check(stage.board.Edges.Count==4,"multiple parents prevented");
            stage.SelectNode(oldEdge.source);stage.SelectNode(oldEdge.destination);stage.Edit(true);
            stage.SelectNode(newEdge.source);stage.SelectNode(newEdge.destination);stage.Edit(false);stage.Confirm();
            while(stage.State==TreeStageState.CHALLENGE_COMPLETE||stage.Moving)yield return null;
            Check(stage.ChallengeIndex==3,"round 4");yield return OpenBoard();stage.SetLesson(true);Check(!stage.timer.Running,"lesson pauses clock");stage.SetLesson(false);Check(stage.timer.Running,"lesson resumes clock");
            Check(stage.BoardOpen&&!stage.ui.gameplayControls.activeInHierarchy&&!stage.LocalPlayer.enabled,"lesson returns to locked board");
            stage.SelectAnswer(TreeAnswerValidator.Depth(stage.Challenge,stage.Challenge.initial,stage.Challenge.target));stage.Confirm();
            while(stage.State==TreeStageState.CHALLENGE_COMPLETE||stage.Moving)yield return null;
            Check(stage.ChallengeIndex==4&&stage.Correct==4,"no early victory");yield return OpenBoard();
            var spanning=stage.Challenge;
            System.Collections.Generic.List<TreeEdgeData> winning=null;
            for(int mask=0;mask<32&&winning==null;mask++)
            {
                var subset=spanning.initial.Where((e,i)=>(mask&(1<<i))!=0).ToList();
                if(TreeAnswerValidator.Validate(spanning,subset,null,null,out reason))winning=subset;
            }
            Check(winning!=null,"spanning variant has a solution");
            foreach(var edge in spanning.initial.Where(e=>!winning.Contains(e)))
            {stage.SelectNode(edge.source);stage.SelectNode(edge.destination);stage.Edit(true);}
            stage.Confirm();
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
