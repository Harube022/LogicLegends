#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LogicLegends.Trees;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public static class TreesRevisionPlayChecks
{
    const string Report="Library/TreesRevisionChecks.txt";
    static TreeStageManager stage;static Touchscreen touch;static int finger=7000;
    public static void Data()
    {
        var catalog=AssetDatabase.LoadAssetAtPath<TreeChallengeCatalog>("Assets/Prefabs/Trees/TreeChallenges.asset");
        File.WriteAllText(Report,"Trees: authored bank, genuine difficulty, real touch, ten answers and isolated persistence.\n");
        foreach(var set in catalog.variantSets)
        {
            Check(set.variants.Count==10,"existing ten-entry bank retained");
            foreach(var q in set.variants)
            {
                var solution=Solution(q);string node=q.type==TreeChallengeType.Identify?q.nodes.First(n=>TreeAnswerValidator.Validate(q,q.initial,n.id,null,out _)).id:null;
                int? answer=q.type==TreeChallengeType.Depth?TreeAnswerValidator.Depth(q,q.initial,q.target):q.type==TreeChallengeType.Height?TreeAnswerValidator.Height(q,q.initial):(int?)null;
                Check(TreeAnswerValidator.Validate(q,solution,node,answer,out _),"solvable "+q.title+" difficulty "+q.difficulty);
                Check(q.nodes.All(n=>Mathf.Abs(n.position.x)<=1&&n.position.y>=0&&n.position.y<=1),"diagram coordinates fit");
            }
            Check(set.variants.Where(q=>q.difficulty==2).Min(q=>q.nodes.Count)>set.variants.Where(q=>q.difficulty==1).Max(q=>q.nodes.Count),"harder questions use more demanding structures");
            for(int i=0;i<1000;i++){var pair=TreeQuestionSelection.SelectPair(set.variants);Check(pair[0]!=pair[1]&&set.variants[pair[0]].difficulty==1&&set.variants[pair[1]].difficulty==2,"distinct ordered pair");}
        }
        Log("PASS fifty valid questions; 5,000 ordered random pairs; structural difficulty increases in every challenge.");
    }
    public static IEnumerator Run()
    {
        if(SessionState.GetBool("Help.TreesPlaylistVerify",false)){yield return TreesHelpPlaylistChecks.Run();yield break;}
        Data();stage=UnityEngine.Object.FindFirstObjectByType<TreeStageManager>();
        touch=InputSystem.AddDevice<Touchscreen>();
        try
        {
            for(int c=0;c<5;c++)for(int q=0;q<2;q++)
            {
                yield return Wait(()=>stage.CanAct,10,"book ready");
                Check(stage.ChallengeIndex==c&&stage.QuestionIndex==q&&stage.Correct==c*2+q,"two-question progression");
                Check(!stage.timer.Running&&stage.State==TreeStageState.WAITING_FOR_ACTIVATION,"book gate keeps countdown paused");
                Check(stage.Challenge.difficulty==q+1,"question two is harder");
                var hand=stage.ui.gameplayControls.transform.Find("InteractButton").GetComponent<Button>();
                yield return Tap((RectTransform)hand.transform);
                yield return Wait(()=>stage.CanSolve,10,"board opens through hand");
                if(SessionState.GetBool("Help.TreesInspectOnly",false)){while(true)yield return null;}
                Check(!stage.LocalPlayer.enabled&&GameInput.Instance.GameplayInputBlocked,"board locks world input");
                Check(stage.ui.BoardCanvas.GetComponent<Canvas>().renderMode==RenderMode.WorldSpace,"question, diagram and controls belong to the board");
                Check(!stage.ui.host.GetComponentsInChildren<TMP_Text>(true).Any(t=>(t.text??"").Contains("FIELD GUIDE")),"Help renamed");
                if(q==0)
                {
                    foreach(int size in new[]{6,12,2})
                    {
                        BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(.4f);Canvas.ForceUpdateCanvases();
                        foreach(var b in stage.ui.BoardCanvas.GetComponentsInChildren<Button>())
                        {var rect=ScreenRect((RectTransform)b.transform);Check(rect.xMin>=0&&rect.yMin>=0&&rect.xMax<=Screen.width&&rect.yMax<=Screen.height,"button on screen: "+b.name);}
                        Check(!stage.ui.QuestionText.isTextOverflowing,"question wraps without overflow");
                        ScreenCapture.CaptureScreenshot("Library/Trees_C"+(c+1)+"_"+Screen.width+"x"+Screen.height+".png");yield return null;
                    }
                    BookMapMobileChecks.SetSize(6);yield return new WaitForSecondsRealtime(.2f);
                }
                if(c==0&&q==0)
                {
                    string wrong=stage.Challenge.nodes.First(n=>!TreeAnswerValidator.Validate(stage.Challenge,stage.board.Edges,n.id,null,out _)).id;
                    yield return Tap((RectTransform)FindButton("Board Node "+wrong).transform);
                    float before=stage.timer.Remaining;int mistakes=stage.Mistakes;
                    yield return Tap((RectTransform)stage.ui.SubmitButton.transform);stage.Confirm();
                    Check(stage.Mistakes==mistakes+1&&before-stage.timer.Remaining>=10&&before-stage.timer.Remaining<11,"one penalty per validation");
                    Check(stage.board.First==wrong&&stage.BoardOpen,"incorrect work and board preserved");
                    yield return Wait(()=>stage.CanSolve,5,"correction unlocked");
                    stage.SetLesson(true);yield return null;Check(stage.LessonOpen&&!stage.timer.Running,"Help pauses editing");
                    stage.SetLesson(false);yield return null;Check(stage.BoardOpen&&stage.timer.Running&&!stage.LocalPlayer.enabled,"Help restores previous board lock and countdown");
                    yield return Tap((RectTransform)stage.ui.BackButton.transform);yield return Wait(()=>stage.CanAct,5,"Back camera restored");
                    Check(!stage.BoardOpen&&stage.LocalPlayer.enabled&&!GameInput.Instance.GameplayInputBlocked,"Back restores exploration");
                    yield return Tap((RectTransform)hand.transform);yield return Wait(()=>stage.CanSolve,5,"reopen keeps work");Check(stage.board.First==wrong,"selection retained");
                }
                yield return Solve();int completed=stage.Correct;
                yield return Tap((RectTransform)stage.ui.SubmitButton.transform);stage.Confirm();
                Check(stage.Correct==completed+1&&!stage.timer.Running,"correct counts once and pauses");
                yield return Wait(()=>!stage.Moving&&stage.State!=TreeStageState.CHALLENGE_COMPLETE,12,"next question gate");
            }
            Check(stage.State==TreeStageState.STAGE_COMPLETE&&stage.Correct==10,"only ten answers finish the trial");
            Log("PASS all ten questions, normal board interactions, editing, mobile layouts, Help/Back, penalties and completion.");
        }
        finally{if(touch!=null&&touch.added)InputSystem.RemoveDevice(touch);}
    }
    public static List<TreeEdgeData> Solution(TreeChallengeData q)=>q.type==TreeChallengeType.Spanning?AdminGraphTreeAnswers.TreeSpanningSolution(q):q.Editable?q.required:q.initial;
    static IEnumerator Solve()
    {
        var q=stage.Challenge;
        if(q.type==TreeChallengeType.Identify)
        {string id=q.nodes.First(n=>TreeAnswerValidator.Validate(q,q.initial,n.id,null,out _)).id;yield return Tap((RectTransform)FindButton("Board Node "+id).transform);}
        else if(q.type==TreeChallengeType.Depth||q.type==TreeChallengeType.Height)
        {int answer=q.type==TreeChallengeType.Depth?TreeAnswerValidator.Depth(q,q.initial,q.target):TreeAnswerValidator.Height(q,q.initial);yield return Tap((RectTransform)FindButton("Board Answer "+answer).transform);}
        else
        {
            var solution=Solution(q);
            foreach(var edge in stage.board.Edges.Where(e=>!solution.Any(s=>s.Key(q.Directed)==e.Key(q.Directed))).ToArray())
            {yield return Endpoints(edge);yield return Tap((RectTransform)FindButton("Board Remove").transform);}
            foreach(var edge in solution.Where(e=>!stage.board.Edges.Any(s=>s.Key(q.Directed)==e.Key(q.Directed))).ToArray())
            {yield return Endpoints(edge);yield return Tap((RectTransform)FindButton("Board Connect").transform);}
        }
    }
    static IEnumerator Endpoints(TreeEdgeData edge)
    {stage.ClearSelection();yield return Tap((RectTransform)FindButton("Board Node "+edge.source).transform);yield return Tap((RectTransform)FindButton("Board Node "+edge.destination).transform);}
    static Button FindButton(string name)=>stage.ui.BoardCanvas.GetComponentsInChildren<Button>(true).First(b=>b.name==name);
    static Rect ScreenRect(RectTransform rt)
    {var canvas=rt.GetComponentInParent<Canvas>();var cam=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:Camera.main;var points=new Vector3[4];rt.GetWorldCorners(points);var p=points.Select(x=>RectTransformUtility.WorldToScreenPoint(cam,x)).ToArray();return Rect.MinMaxRect(p.Min(x=>x.x),p.Min(x=>x.y),p.Max(x=>x.x),p.Max(x=>x.y));}
    static IEnumerator Tap(RectTransform rt)
    {
        yield return null;Canvas.ForceUpdateCanvases();var at=ScreenRect(rt).center;
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=at},hits);
        Check(hits.Count>0&&(hits[0].gameObject.transform==rt||hits[0].gameObject.transform.IsChildOf(rt)),"raycast reaches "+rt.name+"; got "+(hits.Count>0?hits[0].gameObject.name:"nothing"));
        int id=++finger;InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Began,position=at});yield return null;yield return null;
        InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=at});yield return null;yield return null;
    }
    static IEnumerator Wait(Func<bool> fn,float seconds,string message)
    {float end=Time.realtimeSinceStartup+seconds;while(!fn()){Check(Time.realtimeSinceStartup<end,"timeout "+message);yield return null;}}
    static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    static void Log(string message)=>File.AppendAllText(Report,message+"\n");
}
#endif
