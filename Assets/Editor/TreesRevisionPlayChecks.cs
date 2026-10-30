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
                var positions=TreeDiagramLayout.Calculate(q).Values.ToArray();
                for(int a=0;a<positions.Length;a++)for(int b=a+1;b<positions.Length;b++)Check(Vector3.Distance(positions[a],positions[b])>1.15f,"node circles have a visible gap");
            }
            Check(set.variants.Where(q=>q.difficulty==2).Min(q=>q.nodes.Count)>set.variants.Where(q=>q.difficulty==1).Max(q=>q.nodes.Count),"harder questions use more demanding structures");
            for(int i=0;i<1000;i++){var pair=TreeQuestionSelection.SelectPair(set.variants);Check(pair[0]!=pair[1]&&set.variants[pair[0]].difficulty==1&&set.variants[pair[1]].difficulty==2,"distinct ordered pair");}
        }
        Log("PASS fifty valid questions; 5,000 ordered random pairs; structural difficulty increases in every challenge.");
    }
    public static IEnumerator Run()
    {
        if(SessionState.GetBool("Help.TreesPlaylistVerify",false)){var owner=UnityEngine.Object.FindFirstObjectByType<TreeStageManager>();owner.SetLesson(false);yield return Wait(()=>owner.Introductions.IsOpen,5,"initial practice");owner.Introductions.Finish();yield return Wait(()=>!owner.Introductions.IsOpen,7,"practice cleanup");yield return TreesHelpPlaylistChecks.Run();yield break;}
        Data();stage=UnityEngine.Object.FindFirstObjectByType<TreeStageManager>();
        touch=InputSystem.AddDevice<Touchscreen>();
        try
        {
            yield return Wait(()=>stage.LessonOpen,6,"Help automatically opens on arrival");
            float arrivalTime=stage.timer.Remaining;
            Check(!stage.Introductions.IsOpen&&!stage.timer.Running&&Time.timeScale==0&&GameInput.Instance.GameplayInputBlocked,"arrival Help owns input before practice");
            stage.InteractBook();Check(!stage.BoardOpen&&stage.State==TreeStageState.WAITING_FOR_ACTIVATION,"Help cannot activate book");
            yield return new WaitForSecondsRealtime(.3f);
            Check(stage.timer.Remaining==arrivalTime,"arrival reading consumes no time");
            yield return Tap((RectTransform)UnityEngine.Object.FindFirstObjectByType<StageHelpPage>().BeginButton.transform);
            yield return Wait(()=>stage.Introductions.IsOpen,6,"Let's Begin starts practice only");
            Check(stage.State==TreeStageState.WAITING_FOR_ACTIVATION&&!stage.BoardOpen&&!stage.timer.Running,"Let's Begin never starts the real question");
            Log("PASS automatic arrival Help and real-touch Let's Begin enter practice without activating the book or timer.");
            for(int c=0;c<5;c++)for(int q=0;q<2;q++)
            {
                if(q==0)yield return Introduction(c);else Check(!stage.Introductions.IsOpen&&stage.Introductions.Seen(c),"introduction does not repeat for question two");
                yield return Wait(()=>stage.CanAct,10,"book ready");
                Check(stage.ChallengeIndex==c&&stage.QuestionIndex==q&&stage.Correct==c*2+q,"two-question progression");
                Check(!stage.timer.Running&&stage.State==TreeStageState.WAITING_FOR_ACTIVATION,"book gate keeps countdown paused");
                Check(stage.Challenge.difficulty==q+1,"question two is harder");
                if(c==0&&q==0){yield return Collisions();if(GameObject.Find("ChamberDecorations")!=null)yield return TreesChamberPlayChecks.Run(stage);}
                var hand=stage.ui.gameplayControls.transform.Find("InteractButton").GetComponent<Button>();
                yield return Tap((RectTransform)hand.transform);
                yield return Wait(()=>stage.CanSolve,10,"board opens through hand");
                if(SessionState.GetBool("Help.TreesInspectOnly",false)){while(true)yield return null;}
                Check(!stage.LocalPlayer.enabled&&GameInput.Instance.GameplayInputBlocked,"board locks world input");
                Check(stage.ui.BoardCanvas.Find("Board sidebar backdrop")!=null&&stage.boardFocus.outputCamera.rect.x==.25f,"earlier sidebar and board camera workspace restored");
                Check(!stage.ui.host.GetComponentsInChildren<TMP_Text>(true).Any(t=>(t.text??"").Contains("FIELD GUIDE")),"Help renamed");
                if(q==0)
                {
                    foreach(int size in new[]{6,12,2})
                    {
                        BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(.4f);Canvas.ForceUpdateCanvases();
                        foreach(var b in stage.ui.BoardCanvas.GetComponentsInChildren<Button>())
                        {var rect=ScreenRect((RectTransform)b.transform);Check(rect.xMin>=0&&rect.yMin>=0&&rect.xMax<=Screen.width&&rect.yMax<=Screen.height,"button on screen: "+b.name);}
                        Check(!stage.ui.QuestionText.isTextOverflowing,"question wraps without overflow");
                        Check(stage.ui.QuestionText.font==StageUITheme.ReadingFont,"readable question font");
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
                    yield return Tap((RectTransform)UnityEngine.Object.FindFirstObjectByType<StageHelpPage>().BeginButton.transform);yield return null;Check(stage.BoardOpen&&stage.timer.Running&&!stage.LocalPlayer.enabled&&!stage.Introductions.IsOpen,"Help restores previous board lock and countdown without repeating tutorial");
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
    static IEnumerator Collisions()
    {
        var cc=stage.LocalPlayer.GetComponent<CharacterController>();var root=stage.LocalPlayer.gameObject;
        foreach(var direction in new[]{Vector3.forward,Vector3.right,Vector3.left})
        {
            var start=stage.bookStatue.position-direction*4.5f;start.y=stage.spawnPoint.position.y;
            PrelimTeleportTransition.MovePlayerImmediately(root,start,Quaternion.identity);
            for(int i=0;i<50;i++){cc.Move(direction*.15f);yield return null;}
            var delta=stage.LocalPlayer.transform.position-stage.bookStatue.position;
            Check(Vector3.Dot(delta,direction)<-.9f,"real CharacterController blocked by book from "+direction);
        }
        // The visible center plinth has walkable bevels and an open gap beneath the board.
        // Test its solid support pillar instead of expecting the old enclosing box in that gap.
        PrelimTeleportTransition.MovePlayerImmediately(root,new Vector3(5,stage.spawnPoint.position.y,7),Quaternion.identity);
        for(int i=0;i<60;i++){cc.Move(Vector3.forward*.15f);yield return null;}
        Check(stage.LocalPlayer.transform.position.z<13.85f,"real CharacterController cannot walk through InformationBoard support pillar at "+stage.LocalPlayer.transform.position);
        PrelimTeleportTransition.MovePlayerImmediately(root,stage.spawnPoint.position,stage.spawnPoint.rotation);
        Log("PASS book collision from three sides and InformationBoard collision with live CharacterController.");
    }
    static IEnumerator Introduction(int challenge)
    {
        yield return Wait(()=>stage.Introductions.IsOpen,10,"challenge introduction");
        var intro=stage.Introductions;float clock=stage.timer.Remaining;int correct=stage.Correct,mistakes=stage.Mistakes;
        string edges=string.Join(",",stage.board.Edges.Select(e=>e.Key(true)));
        foreach(int size in new[]{6,12,2})
        {
            if(!intro.IsOpen){ResetSeen(challenge);Check(intro.TryBegin(),"restart isolated QA example");}
            BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(.3f);
            Check(!stage.board.gameObject.activeInHierarchy&&intro.Practice.nodes.All(n=>new[]{"X","Y","Z","W"}.Contains(n.id)),"real question hidden behind separate placeholders");
            Check(!stage.timer.Running&&GameInput.Instance.GameplayInputBlocked&&!stage.LocalPlayer.enabled,"practice freezes timer and controls");
            Check(stage.ui.host.GetComponentsInChildren<PrelimTutorialSkipControl>().Length==1,"one shared Skip button");
            if(challenge==2)
            {
                Check(TreeAnswerValidator.Rooted(intro.Practice,intro.Practice.initial),"repair example starts connected, acyclic and structurally valid");
                Check(!TreeAnswerValidator.Validate(intro.Practice,intro.Practice.initial,null,null,out _),"repair fails because its parent assignment differs from the required links");
            }
            yield return Wait(()=>!stage.boardFocus.Transitioning,7,"existing board camera blends");
            int count=intro.StepCount;
            while(intro.IsOpen&&!intro.Returning)
            {
                int step=intro.PageIndex;var action=intro.RequiredAction;
                Check(!stage.ui.QuestionText.isTextOverflowing,"practice explanation fits C"+(challenge+1)+" step "+step);
                Check(stage.ui.QuestionText.text==intro.Explanation,"instruction immediately matches current action");
                Check((stage.ui.NextButton.gameObject.activeSelf)==intro.CanNext,"Next appears only on explanation steps");
                foreach(var button in stage.ui.BoardCanvas.GetComponentsInChildren<Button>().Where(b=>b.interactable))
                {var r=ScreenRect((RectTransform)button.transform);Check(r.width>=40&&r.height>=32,"mobile practice target large enough: "+button.name);Check(r.xMin>=0&&r.xMax<=Screen.width&&r.yMin>=0&&r.yMax<=Screen.height,"practice target in screen: "+button.name);}
                if(action!=TreeChallengeIntroductions.PracticeAction.Explain){intro.Next();Check(intro.PageIndex==step,"Next cannot bypass required action");}
                if(challenge==2&&size==2)ScreenCapture.CaptureScreenshot("Library/TreesRepairStep_"+step+".png");
                if(step==0)ScreenCapture.CaptureScreenshot("Library/TreesPractice_C"+(challenge+1)+"_"+Screen.width+"x"+Screen.height+".png");
                yield return PracticeAction();
                Check(intro.Returning||intro.PageIndex==step+1,"required action advances exactly one step C"+(challenge+1)+" step "+step+" "+action);
                Check(stage.Correct==correct&&stage.Mistakes==mistakes&&stage.timer.Remaining==clock,"every practice action preserves real time and progress");
            }
            yield return Wait(()=>!intro.IsOpen,8,"complete practice and camera cleanup");
            Restored(edges,clock,correct,mistakes);
            Log("PASS challenge "+(challenge+1)+" all "+count+" action steps through submission at "+Screen.width+"x"+Screen.height+".");
        }
        // Also leave midway through each practice, including after edits/incorrect practice feedback.
        ResetSeen(challenge);Check(intro.TryBegin(),"skip test begins");
        if(challenge!=0)
        {
            yield return Wait(()=>!stage.boardFocus.Transitioning,7,"skip example camera");
            while(intro.PageIndex<(challenge==1?4:challenge==2?5:challenge==3?4:4))yield return PracticeAction();
        }
        var skip=stage.ui.host.GetComponentsInChildren<PrelimTutorialSkipControl>().Single();
        yield return Tap((RectTransform)skip.transform);
        yield return Wait(()=>!intro.IsOpen,8,"skip restores camera, input and practice content");
        Restored(edges,clock,correct,mistakes);
        stage.SetLesson(true);yield return null;Check(stage.LessonOpen,"Help remains available after practice");
        yield return Tap((RectTransform)UnityEngine.Object.FindFirstObjectByType<StageHelpPage>().BeginButton.transform);
        yield return null;Check(!intro.IsOpen&&stage.CanAct&&!stage.timer.Running,"reopened Help returns to waiting gameplay without replaying practice");
        BookMapMobileChecks.SetSize(6);yield return new WaitForSecondsRealtime(.2f);
        Log("PASS challenge "+(challenge+1)+" shared Skip and Help reopening restore book-controlled start.");
    }
    static void ResetSeen(int challenge)
    {
        // Test-only replay within an offline attempt; production has no progress-reset path here.
        var field=typeof(TreeChallengeIntroductions).GetField("seen",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        ((bool[])field.GetValue(stage.Introductions))[challenge]=false;
    }
    static void Restored(string edges,float clock,int correct,int mistakes)
    {
        Check(stage.Correct==correct&&stage.Mistakes==mistakes&&stage.timer.Remaining==clock&&!stage.timer.Running,"practice cannot change gameplay or time");
        Check(stage.board.gameObject.activeSelf&&string.Join(",",stage.board.Edges.Select(e=>e.Key(true)))==edges,"real tree restored exactly");
        Check(!stage.BoardOpen&&stage.State==TreeStageState.WAITING_FOR_ACTIVATION&&stage.LocalPlayer.enabled&&!GameInput.Instance.GameplayInputBlocked,"introduction returns to book-controlled start");
        Check(!stage.ui.host.GetComponentsInChildren<PrelimTutorialSkipControl>().Any(),"Skip removed");
        Check(GameObject.Find("Trees Tutorial Practice Only")==null,"practice objects destroyed");
    }
    static IEnumerator PracticeAction()
    {
        var intro=stage.Introductions;
        string control=intro.HighlightControl;
        if(intro.RequiredAction==TreeChallengeIntroductions.PracticeAction.Node)
        {
            string wrong=intro.Practice.nodes.First(n=>n.id!=intro.RequiredNode).id;
            int before=intro.PageIndex;intro.Select(wrong);Check(intro.PageIndex==before,"unrelated node cannot advance guided practice");
            yield return Tap(stage.ui.Target(intro.RequiredNode));
        }
        else yield return Tap((RectTransform)FindButton(control).transform);
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
