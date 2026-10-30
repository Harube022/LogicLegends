using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace LogicLegends.Trees
{
    /// <summary>One isolated board-facing lesson per challenge. No gameplay answers or rewards are touched.</summary>
    public sealed class TreeChallengeIntroductions : MonoBehaviour
    {
        // Each action advances only after that input succeeds. Next is for explanations, never a shortcut over practice.
        public enum PracticeAction { Explain, Node, Clear, Connect, Remove, Number, Submit }
        sealed class Step
        {
            public PracticeAction action; public string text, node; public int value; public bool correct=true;
            public Step(PracticeAction action,string text,string node=null,int value=0,bool correct=true)
            {this.action=action;this.text=text;this.node=node;this.value=value;this.correct=correct;}
        }
        List<Step> steps;
        Step Current=>steps[PageIndex];
        public PracticeAction RequiredAction=>Current.action;
        public int StepCount=>steps.Count;
        public bool CanNext=>IsOpen&&!Returning&&Current.action==PracticeAction.Explain;
        public string RequiredNode=>Current.node;
        public int RequiredNumber=>Current.value;
        TreeStageManager stage;
        readonly bool[] seen=new bool[5];
        PrelimTutorialSkipControl skip;
        bool blockedBefore,playerBefore,boardBefore,timerBefore,cursorBefore;
        CursorLockMode lockBefore;
        Coroutine returning;
        public bool IsOpen{get;private set;}
        public bool Returning{get;private set;}
        public int PageIndex{get;private set;}
        public TreeBoardController PracticeBoard{get;private set;}
        public TreeChallengeData Practice{get;private set;}
        public string Feedback{get;private set;}="Practice only. No timer or progress changes.";
        public string Heading=>new[]{"Tree roles","Build a tree","Repair a tree","Depth and height","Spanning trees"}[stage.ChallengeIndex];
        public string Explanation=>Current.text;
        public string HighlightNode=>Current.node;
        public string HighlightControl=>Current.action==PracticeAction.Clear?"Board Clear Selection":Current.action==PracticeAction.Connect?"Board Connect":Current.action==PracticeAction.Remove?"Board Remove":Current.action==PracticeAction.Number?"Board Answer "+Current.value:Current.action==PracticeAction.Submit?"Submit Tree":Current.action==PracticeAction.Explain?"Tree Tutorial Next":null;
        bool ReadyForAction=>IsOpen&&!Returning&&!stage.boardFocus.Transitioning;
        public bool AllowsNode(string id)=>ReadyForAction&&Current.action==PracticeAction.Node&&Current.node==id;
        public bool AllowsControl(string name)=>ReadyForAction&&HighlightControl==name;
        public bool Seen(int challenge)=>seen[challenge];
        public void Initialize(TreeStageManager owner){stage=owner;}
        public bool TryBegin()
        {
            if(stage==null||IsOpen||seen[stage.ChallengeIndex]||stage.QuestionIndex!=0||stage.State!=TreeStageState.WAITING_FOR_ACTIVATION||!stage.CanAct||stage.BoardOpen)return false;
            Practice=Example(stage.ChallengeIndex);steps=Walkthrough(stage.ChallengeIndex);number=null;
            var root=new GameObject("Trees Tutorial Practice Only");root.transform.SetPositionAndRotation(stage.board.transform.position,stage.board.transform.rotation);
            PracticeBoard=root.AddComponent<TreeBoardController>();PracticeBoard.nodePrefab=stage.board.nodePrefab;PracticeBoard.edgePrefab=stage.board.edgePrefab;PracticeBoard.Build(Practice);
            foreach(var c in root.GetComponentsInChildren<Collider>())c.enabled=false;
            if(!stage.boardFocus.Enter(stage.LocalPlayer)){Destroy(root);PracticeBoard=null;Practice=null;return false;}
            blockedBefore=GameInput.Instance.GameplayInputBlocked;playerBefore=stage.LocalPlayer.enabled;boardBefore=stage.board.gameObject.activeSelf;timerBefore=stage.timer.Running;
            lockBefore=Cursor.lockState;cursorBefore=Cursor.visible;
            seen[stage.ChallengeIndex]=true;PageIndex=0;Returning=false;IsOpen=true;Feedback="Practice only • Countdown paused.";
            stage.board.gameObject.SetActive(false);stage.timer.Pause();stage.LocalPlayer.ToggleControl(false);GameInput.Instance.SetGameplayInputBlocked(true);
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            skip=PrelimTutorialSkipControl.Create(stage.ui.host,Finish);
            var skipLabel=skip.GetComponentInChildren<TMPro.TMP_Text>();skipLabel.font=StageUITheme.ReadingFont;skipLabel.fontSharedMaterial=StageUITheme.ReadingFont.material;skipLabel.fontStyle=TMPro.FontStyles.Bold;skipLabel.fontSizeMin=24;skipLabel.fontSizeMax=28;
            stage.ui.RebuildDiagram();stage.ui.Refresh();return true;
        }
        public void Next()
        {
            if(!ReadyForAction||!CanNext)return;
            AdvanceStep();
        }
        void AdvanceStep()
        {
            if(PageIndex==steps.Count-1){Finish();return;}
            PageIndex++;stage.ui.Refresh();
        }
        public void Select(string id)
        {
            if(!AllowsNode(id))return;
            PracticeBoard.Select(id);Feedback="Selected "+PracticeBoard.First+(PracticeBoard.Second==null?"":" → "+PracticeBoard.Second)+".";AdvanceStep();
        }
        int? number;
        public void SelectNumber(int value){if(!ReadyForAction||Current.action!=PracticeAction.Number||Current.value!=value)return;number=value;Feedback="Selected "+value+".";AdvanceStep();}
        public void Clear(){if(!AllowsControl("Board Clear Selection"))return;PracticeBoard.ClearSelection();number=null;Feedback="Pick cleared. Branches are kept.";AdvanceStep();}
        public void Edit(bool remove)
        {
            if(!AllowsControl(remove?"Board Remove":"Board Connect"))return;
            bool changed=PracticeBoard.Edit(remove,out var result);Feedback=result;
            if(changed)AdvanceStep();else stage.ui.Refresh();
        }
        public void Submit()
        {
            if(!AllowsControl("Submit Tree"))return;
            bool valid=TreeAnswerValidator.Validate(Practice,PracticeBoard.Edges,PracticeBoard.First,number,out _);
            foreach(var node in PracticeBoard.Nodes)node.ShowFeedback(valid);
            Feedback=valid?"Correct practice!":"Incorrect practice • No time lost.";
            if(valid==Current.correct)AdvanceStep();else stage.ui.Refresh();
        }
        public void Finish()
        {
            if(!IsOpen||Returning)return;Returning=true;skip?.Release();skip=null;
            PracticeBoard.gameObject.SetActive(false);stage.boardFocus.Exit();stage.ui.Refresh();returning=StartCoroutine(RestoreAfterBlend());
        }
        IEnumerator RestoreAfterBlend(){yield return null;float until=Time.unscaledTime+6;while(stage.boardFocus.Transitioning&&Time.unscaledTime<until)yield return null;Restore();returning=null;}
        void Restore()
        {
            if(!IsOpen)return;
            IsOpen=false;Returning=false;
            stage.board.gameObject.SetActive(boardBefore);if(PracticeBoard!=null)Destroy(PracticeBoard.gameObject);PracticeBoard=null;Practice=null;number=null;
            GameInput.Instance?.SetGameplayInputBlocked(blockedBefore);if(stage.LocalPlayer!=null)stage.LocalPlayer.ToggleControl(playerBefore);
            if(timerBefore&&stage.State==TreeStageState.ACTIVE)stage.timer.Resume();else stage.timer.Pause();
            Cursor.lockState=lockBefore;Cursor.visible=cursorBefore;MobileLookInput.ResetDelta();stage.ui.RebuildDiagram();stage.ui.Refresh();
        }
        public void CancelImmediate()
        {
            if(!IsOpen)return;if(returning!=null)StopCoroutine(returning);returning=null;skip?.Release();skip=null;stage.boardFocus.Exit();Restore();
        }
        void OnDisable(){if(stage!=null&&stage.ui!=null)CancelImmediate();}
        static List<Step> Walkthrough(int topic)
        {
            var s=new List<Step>();
            System.Action<string> explain=t=>s.Add(new Step(PracticeAction.Explain,t));
            System.Action<string,string> node=(id,t)=>s.Add(new Step(PracticeAction.Node,t,id));
            System.Action<PracticeAction,string> control=(a,t)=>s.Add(new Step(a,t));
            System.Action<string,string,string> pair=(parent,child,t)=>{node(parent,t+" Tap "+parent+" first.");node(child,"Now tap "+child+". The selected pair is "+parent+" → "+child+".");};
            if(topic==0)
            {
                explain("Nodes are joined by branches. A parent has children; a leaf has none. The root has no parent.");
                node("W","W has no children: it is a leaf. Tap W to try selecting a node.");
                control(PracticeAction.Clear,"Clear Pick cancels a selection. It keeps every branch. Tap Clear Pick.");
                node("X","Practice task: find the root. X has no parent. Tap X.");
                control(PracticeAction.Submit,"X is selected. Tap Submit to check it. In real questions, choose the role requested on the board.");
            }
            else if(topic==1)
            {
                explain("Build these practice links: X → Y, X → Z, Y → W. Each non-root node needs one parent. No cycles.");
                pair("X","Y","Select a parent, then its child.");control(PracticeAction.Connect,"Tap Connect to add X → Y.");
                explain("Remove undoes a branch, not just a pick. Try removing X → Y, then reconnect it.");
                pair("X","Y","Select the branch endpoints.");control(PracticeAction.Remove,"Tap Remove. X → Y will disappear.");
                pair("X","Y","Restore the branch by selecting parent and child.");control(PracticeAction.Connect,"Tap Connect to restore X → Y.");
                pair("X","Z","Next, give Z a parent.");control(PracticeAction.Connect,"Tap Connect to add X → Z.");
                pair("Y","W","Finally, W must be Y's child.");control(PracticeAction.Connect,"Tap Connect to add Y → W.");
                control(PracticeAction.Submit,"All three required links are ready. Tap Submit to check the whole tree.");
            }
            else if(topic==2)
            {
                explain("Repair means fixing the requested parent links. The starting tree is connected and has no cycle; a child has the wrong parent.");
                explain("Practice task: make W a child of Y. W is currently Z's child. Keep X → Y and X → Z.");
                pair("Z","W","Remove W's old parent link first.");control(PracticeAction.Remove,"Tap Remove to delete Z → W. W is temporarily disconnected until you reconnect it.");
                node("Z","If you pick the wrong parent, Clear Pick lets you restart. Tap Z for this practice correction.");
                control(PracticeAction.Clear,"Tap Clear Pick. This clears the pick without changing the remaining branches.");
                pair("Y","W","Choose W's requested parent.");control(PracticeAction.Connect,"Tap Connect to add Y → W. W now has one parent and is connected to the root.");
                control(PracticeAction.Submit,"Submit checks every requested link, with no extras. Keep unchanged links. Tap Submit.");
            }
            else if(topic==3)
            {
                explain("Depth counts branches from the root, whose depth is 0. Height is the longest root-to-leaf path.");
                node("W","Practice task: find W's depth. Tap W and trace X → Y → W: two branches.");
                s.Add(new Step(PracticeAction.Number,"Try 1 to see how correction works. Practice mistakes cost no time.",value:1));
                s.Add(new Step(PracticeAction.Submit,"Tap Submit to check the practice answer 1.",correct:false));
                control(PracticeAction.Clear,"That answer is incorrect. In real questions, a wrong submission costs 10 seconds. Tap Clear Pick to change it.");
                s.Add(new Step(PracticeAction.Number,"Count X at depth 0, Y at 1, W at 2. Tap 2.",value:2));
                control(PracticeAction.Submit,"Tap Submit to check the corrected answer. Your real question will use a different tree.");
            }
            else
            {
                explain("A spanning tree keeps all vertices connected with no cycle. Four vertices need three edges. Use only original edges.");
                pair("Y","Z","X–Y–Z–X forms a cycle. Remove Y–Z while keeping another route between them.");control(PracticeAction.Remove,"Tap Remove to delete Y–Z. All four vertices remain connected.");
                pair("Z","W","Four edges remain. Z–W is another extra edge; W is still connected through Y.");control(PracticeAction.Remove,"Tap Remove to delete Z–W. Three edges now connect all four vertices, without a cycle.");
                control(PracticeAction.Submit,"Tap Submit. Other spanning trees can also be valid. Clear Pick cancels a pick; Connect can restore an original edge.");
            }
            explain("Practice complete. Tap Next to return to the book. Use the hand button there to start the real question and countdown.");
            return s;
        }
        static TreeChallengeData Example(int topic)
        {
            var q=new TreeChallengeData{root="X",target=topic==3?"W":null,title="Practice only",type=new[]{TreeChallengeType.Identify,TreeChallengeType.Build,TreeChallengeType.Repair,TreeChallengeType.Depth,TreeChallengeType.Spanning}[topic],identification=TreeIdentification.Root,
                nodes=new List<TreeNodeData>{new TreeNodeData("X",0,1),new TreeNodeData("Y",-.6f,.5f),new TreeNodeData("Z",.6f,.5f),new TreeNodeData("W",-.6f,0)},
                required=new List<TreeEdgeData>{new TreeEdgeData("X","Y"),new TreeEdgeData("X","Z"),new TreeEdgeData("Y","W")}};
            if(topic!=1)q.initial=q.required.ConvertAll(e=>new TreeEdgeData(e.source,e.destination));
            if(topic==2)q.initial[2].source="Z";
            if(topic==4){q.initial.Add(new TreeEdgeData("Y","Z"));q.initial.Add(new TreeEdgeData("Z","W"));}
            return q;
        }
    }
}
