#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>Offline saved-scene checks; uses the real hand callbacks and existing quiz/clock data.</summary>
public static class TruthTableInteractionChecks
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const string Report="Library/TruthTableInteractionChecks.txt";
    [MenuItem("Logic Legends/Truth Table/Check Door and Element UI")]
    public static void Begin()
    {
        File.WriteAllText(Report,"Truth Table interaction and mobile UI checks\n");
        SessionState.SetBool("Truth.InteractionQA",true);BookStatuePlayChecks.BeginTruthTable();
    }
    public static void BeginHelp()
    { SessionState.SetBool("Truth.HelpQA",true);BookStatuePlayChecks.BeginTruthTable(); }
    public static IEnumerator CheckHelp()
    {
        var help=UnityEngine.Object.FindFirstObjectByType<TruthTableTutorial>();
        var clock=help.GetComponent<TruthTableStageClock>();float before=clock.RemainingSeconds;
        help.OpenTutorial();Set(help,"startControlsOnClose",false);
        foreach(int size in new[]{6,12,3})
        {
            BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(.3f);
            while(help.PageIndex>0)help.PreviousPage();
            for(int page=0;page<help.PageCount;page++)
            {
                yield return null;yield return new WaitForEndOfFrame();
                var body=Field<TMP_Text>(help,"bodyText");body.ForceMeshUpdate();
                Check(!body.isTextOverflowing,"Help page fits "+page+" at "+Screen.width+"x"+Screen.height);
                Check(!body.text.Contains("TRUTIBOL")&&!body.text.Contains("Truth_Table"),"consistent Truth Table terminology");
                if(page==1)yield return Capture("HelpOperators");
                help.NextPage();
            }
        }
        help.CloseTutorial();Check(clock.IsWaitingForBook&&clock.RemainingSeconds==before,"Help preserves waiting timer");
        File.AppendAllText(Report,"PASS: all twelve Help pages fit at three resolutions; consistent terminology; Help restores waiting state and time.\n");
    }
    public static IEnumerator Run()
    {
        var doors=UnityEngine.Object.FindFirstObjectByType<TruthTableDoorInteraction>();
        var clock=doors.GetComponent<TruthTableStageClock>();var player=Player.LocalInstance;
        var hand=UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).First(b=>b.name=="InteractButton");
        var map=doors.MinimapPanel;var hud=StageJourneyUI.Instance.HudPanel;
        var bubbles=Field<HandThoughtBubble[]>(doors,"doorBubbles");
        var blocks=TruthBlock.ActiveBlocks.Where(b=>b.UsesHandBubble).ToArray();
        Check(blocks.Length==4,"four actual challenge element blocks");

        // The tutorial quiz opens only practice UI and does not unlock a real house.
        var coach=doors.GetComponent<TruthTableControlsOnboarding>();
        var help=doors.GetComponent<TruthTableTutorial>();
        float savedTime=clock.RemainingSeconds;
        coach.Begin(Field<UnityEngine.UI.Button>(help,"helpButton"));Call(coach,"BeginBookDemo");Call(coach,"BeginDoorDemo");
        hand.onClick.Invoke();yield return null;
        Check(coach.IsPracticeQuizOpen&&!clock.IsQuizOpen,"hand opens practice quiz only");
        Field<UnityEngine.UI.Button[]>(coach,"practiceAnswerButtons")[0].onClick.Invoke();yield return null;
        Check(Field<TMP_Text>(coach,"practiceQuizFeedback").text.Contains("Correct answer: False"),"practice wrong feedback teaches correction");
        Check(!hud.gameObject.activeInHierarchy,"practice quiz hides gameplay HUD");
        Field<UnityEngine.UI.Button>(coach,"practiceQuizContinue").onClick.Invoke();
        coach.SkipTutorial();Call(coach,"ConfirmYes");yield return new WaitForSecondsRealtime(.3f);
        Check(clock.IsWaitingForBook&&clock.RemainingSeconds==savedTime&&Field<bool[]>(doors,"answered").All(x=>!x),"practice and Skip preserve real time and locked doors");
        foreach(var block in blocks)
        {
            Check(!block.CanCollectNow,"no pickup before book activation");
            block.CollectBlock();Check(!block.GetComponent<GrabbableObject>().isStoredInInventory,"direct pickup cannot bypass start restriction");
        }
        Check(bubbles.All(b=>!b.gameObject.activeInHierarchy),"waiting doors have no bubbles");

        PrelimTeleportTransition.MovePlayerImmediately(player.gameObject,doors.TutorialBook.position+Vector3.back*2,Quaternion.identity);
        yield return new WaitForSecondsRealtime(.4f);hand.onClick.Invoke();yield return new WaitForSecondsRealtime(.3f);
        Check(clock.IsRunning,"hand starts challenge from book");
        yield return CheckHouseIndicators(doors);
        foreach(int size in new[]{6,12,3})
        {
            BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(.4f);yield return new WaitForEndOfFrame();
            Check(Mathf.Abs(ScreenRect(hud).width-ScreenRect(map).width)<2,"HUD exactly matches minimap width");
            Check(ScreenRect(hud).yMax<ScreenRect(map).yMin,"HUD below map");
            foreach(var label in hud.GetComponentsInChildren<TMP_Text>()){label.ForceMeshUpdate();Check(!label.isTextOverflowing,"compact HUD text fits: "+label.name);}
            var jump=(RectTransform)UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).First(b=>b.name=="JumpButton").transform;
            Check(!ScreenRect(hud).Overlaps(ScreenRect(jump)),"compact HUD clears jump");
            yield return Capture("HUD");
        }
        BookMapMobileChecks.SetSize(6);yield return null;
        foreach(var block in blocks)Check(!block.CanCollectNow,"closed houses block pickup after start");
        Check(bubbles.All(b=>b.gameObject.activeInHierarchy),"six available doors show bubbles");

        var assigned=Field<TruthTableDoorQuestionBank.Question[]>(doors,"assignedQuestions");
        assigned[0]=Field<TruthTableDoorQuestionBank>(doors,"questionBank").questions[0]; // Longest correct-answer description.
        float originalDelay=Field<float>(doors,"feedbackSeconds");
        Set(doors,"feedbackSeconds",12f);
        for(int i=0;i<6;i++)
        {
            var door=doors.GetTutorialDoor(i);var house=Field<Transform[]>(doors,"houseRoots")[i];
            var direction=(door.bounds.center-house.position);direction.y=0;direction.Normalize();
            var at=door.bounds.center+direction*2;at.y=door.bounds.center.y-1.5f;
            PrelimTeleportTransition.MovePlayerImmediately(player.gameObject,at,Quaternion.LookRotation(-direction));
            var look=player.GetComponentInChildren<CameraTargetController>();if(look!=null)look.SetViewAngles(10,Quaternion.LookRotation(-direction).eulerAngles.y);
            yield return new WaitForSecondsRealtime(.4f);
            Check(bubbles[i].Expansion>.99f,"door proximity expands "+i+" at="+player.transform.position+" bounds="+door.bounds+" distance="+(door.ClosestPoint(player.transform.position+Vector3.up*1.5f)-(player.transform.position+Vector3.up*1.5f)).magnitude+" enabled="+player.enabled+" running="+clock.IsRunning+" tutorial="+clock.IsTutorialOpen+" blocked="+GameInput.Instance.GameplayInputBlocked);
            Check(bubbles[i].HandIcon.sprite==hand.image.sprite,"door uses actual hand artwork");
            if(i==0)yield return Capture("DoorBubble");
            hand.onClick.Invoke();yield return null;yield return new WaitForEndOfFrame();
            Check(doors.IsQuizOpen&&clock.IsCountdownPaused&&!player.enabled,"hand opens door quiz and pauses controls/time "+i);
            Check(!hud.gameObject.activeInHierarchy&&!map.gameObject.activeInHierarchy,"quiz hides HUD and minimap");
            Check(!Field<GameObject>(doors,"interactionPrompt").activeSelf,"old dedicated door button removed");
            float before=clock.RemainingSeconds;
            int choice=i<2?(assigned[i].correctChoice+1)%4:assigned[i].correctChoice;
            Field<UnityEngine.UI.Button[]>(doors,"answerButtons")[choice].onClick.Invoke();
            doors.SubmitAnswer(choice);doors.SubmitAnswer(choice);
            Check(Mathf.Abs(clock.RemainingSeconds-(before-(i<2?10:0)))<.01f,"quiz applies exactly one appropriate penalty "+i);
            if(i==0)
            {
                Check(Field<TMP_Text>(doors,"correctAnswerText").text=="Correct answer: "+assigned[i].choices[assigned[i].correctChoice],"correct answer comes from actual assigned quiz");
                Check(clock.TimeAdjustmentText=="−10s"&&clock.TimeAdjustmentVisible,"immediate visible −10s");
                foreach(int size in new[]{6,12,3})
                {
                    BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(.25f);yield return new WaitForEndOfFrame();
                    CheckQuiz(doors);yield return Capture("WrongQuiz");
                }
                Check(clock.RemainingSeconds==before-10,"reading feedback consumes no countdown time");
                doors.ClosePanel();Set(doors,"feedbackSeconds",originalDelay);
            }
            else if(i==1)
            {
                yield return new WaitForSecondsRealtime(2.5f);
                Check(doors.IsQuizOpen,"incorrect feedback remains readable beyond old delay");
                yield return new WaitForSecondsRealtime(2.8f);
                Check(!doors.IsQuizOpen,"quiz automatically closes after reading interval");
            }
            else {doors.ClosePanel();}
            yield return new WaitForSecondsRealtime(.6f);
            Check(Field<bool[]>(doors,"doorOpened")[i]&&!bubbles[i].gameObject.activeInHierarchy,"answered door opens and prompt hides "+i);
            foreach(var block in blocks)
                Check(block.CanCollectNow==Field<bool[]>(doors,"doorOpened")[doors.HouseForBlock(block)],"element availability matches its own house");
        }
        BookMapMobileChecks.SetSize(6);yield return new WaitForSecondsRealtime(.3f);
        Check(hud.gameObject.activeInHierarchy&&map.gameObject.activeInHierarchy&&player.enabled,"quiz close restores HUD/map/controls");
        var pickup=blocks[0];var pickupCollider=pickup.GetComponent<Collider>();
        var pickupAt=pickupCollider.ClosestPoint(pickupCollider.bounds.center+Vector3.back*10)+Vector3.back*1.3f-Vector3.up*1.5f;
        PrelimTeleportTransition.MovePlayerImmediately(player.gameObject,pickupAt,Quaternion.identity);
        yield return new WaitForSecondsRealtime(.4f);
        var bubble=Field<HandThoughtBubble>(pickup,"pickupBubble");Check(bubble!=null&&bubble.Expansion>.99f,"available block bubble expands nearby: at="+player.transform.position+" block="+pickup.transform.position+" distance="+Vector3.Distance(player.transform.position,pickup.transform.position)+" available="+pickup.CanCollectNow+" bubble="+(bubble!=null?bubble.Expansion:-1));
        Check(IsYellow(bubble.Marker),"element marker is yellow");
        yield return Capture("ElementBubble");hand.onClick.Invoke();yield return null;yield return null;
        Check(pickup.GetComponent<GrabbableObject>().isStoredInInventory&&!bubble.gameObject.activeInHierarchy,"hand collects exact element and hides bubble: stored="+pickup.GetComponent<GrabbableObject>().isStoredInInventory+" bubble="+bubble.gameObject.activeInHierarchy+" count="+InventoryCount()+" held="+pickup.GetComponent<GrabbableObject>().IsHeld+" enabled="+player.enabled+" blocked="+GameInput.Instance.GameplayInputBlocked+" canCollect="+pickup.CanCollectNow);
        int count=InventoryCount();pickup.CollectBlock();Check(InventoryCount()==count,"duplicate collection rejected");
        foreach(var item in blocks)
        {
            var old=Field<GameObject>(item.GetComponent<GrabbableObject>(),"interactionHUD");
            Check(old==null||!old.activeSelf,"no dedicated element pickup HUD");
        }
        doors.ResetAfterColumnEvaluation();yield return null;yield return null;
        foreach(var block in blocks)Check(!block.CanCollectNow,"door reset hides and gates remaining blocks");
        clock.SetPlayerAtBoard(true);clock.AdjustSeconds(-20);
        Check(clock.TimeAdjustmentText=="−20s","other penalties show actual amount");
        yield return new WaitForSecondsRealtime(1.5f);
        Check(Field<TMP_Text>(clock,"adjustmentLabel").color.a>0&&Field<TMP_Text>(clock,"adjustmentLabel").color.a<1,"deduction fades");
        yield return new WaitForSecondsRealtime(.9f);Check(!clock.TimeAdjustmentVisible,"deduction hides after fade");
        var puzzle=doors.transform.root.GetComponentsInChildren<DynamicLogicPuzzle>(true).First(p=>Field<object>(p,"currentPhase") is TruthTableChallengePhase);
        ((TruthTableChallengePhase)Field<object>(puzzle,"currentPhase")).DebugCompleteCurrentChallenge();yield return null;
        Check(clock.IsWaitingForBook&&clock.CurrentChallengeNumber==2&&clock.RemainingSeconds==1080,"next challenge waits for book with existing 18-minute reset");
        File.AppendAllText(Report,"PASS: six hand-only door quizzes; actual correct answers; one penalty per answer; five-second feedback; closed-house block gates; inventory pickup; quiz pause/restore; −10s/−20s fade; tutorial isolation; book-controlled next challenge; three desktop/mobile sizes.\n");
    }
    static IEnumerator CheckHouseIndicators(TruthTableDoorInteraction doors)
    {
        var elapsed=Field<float[]>(doors,"houseCycleElapsed");
        var visibility=Field<float[]>(doors,"houseVisibility");
        var groups=Field<CanvasGroup[]>(doors,"doorBubbleVisibility");
        var bubbles=Field<HandThoughtBubble[]>(doors,"doorBubbles");
        var tick=doors.GetType().GetMethod("UpdateHouseIndicator",Flags);
        for(int i=0;i<6;i++)
        {
            Check(bubbles[i].Marker.font==StageUITheme.ReadingFont&&bubbles[i].Marker.fontStyle==FontStyles.Normal,"separated dot and stem font");
            Check(IsYellow(bubbles[i].Marker),"yellow house marker "+i);
            Check(bubbles[i].GetComponent<UnityEngine.UI.Image>().color==Color.white,"white house bubble preserved");
            tick.Invoke(doors,new object[]{i,false,false,0f});
            for(int frame=1;frame<=288;frame++) // Two complete cycles at half-second intervals.
            {
                tick.Invoke(doors,new object[]{i,true,false,.5f});
                float phase=frame*.5f%72;
                if(phase<=10)Check(groups[i].alpha==1,"full ten-second visible interval");
                else if(phase>=11&&phase<=71)Check(groups[i].alpha==0,"full sixty-second hidden interval");
                else Check(groups[i].alpha>0&&groups[i].alpha<1,"smooth fade between intervals");
            }
            elapsed[i]=45;visibility[i]=0;groups[i].alpha=0;
            for(int frame=0;frame<100;frame++)tick.Invoke(doors,new object[]{i,true,true,.1f});
            Check(elapsed[i]==45&&groups[i].alpha==1,"nearby override pauses cycle and restores prompt");
            tick.Invoke(doors,new object[]{i,true,false,1f});
            Check(elapsed[i]==46&&groups[i].alpha==0,"cycle resumes on departure");
            tick.Invoke(doors,new object[]{i,false,true,1f});
            Check(elapsed[i]==0,"eligibility reset is not bypassed by proximity");
        }
        // Exercise the actual LateUpdate path from deep inside a house, beyond door range.
        var player=Player.LocalInstance;
        var houses=Field<Transform[]>(doors,"houseRoots");
        var bounds=Field<Bounds[]>(doors,"houseLocalBounds");
        var inside=Vector3.zero;float furthest=-1;
        for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
        {
            var candidate=houses[0].TransformPoint(bounds[0].center+Vector3.Scale(bounds[0].extents,new Vector3(x*.5f,0,z*.5f)));
            candidate.y=doors.GetTutorialDoor(0).bounds.center.y-1.5f;
            float distance=Vector3.Distance(doors.GetTutorialDoor(0).ClosestPoint(candidate+Vector3.up*1.5f),candidate+Vector3.up*1.5f);
            if(distance>furthest){furthest=distance;inside=candidate;}
        }
        Check(Vector3.Distance(doors.GetTutorialDoor(0).ClosestPoint(inside+Vector3.up*1.5f),inside+Vector3.up*1.5f)>4.5f,"inside fixture is beyond door range");
        PrelimTeleportTransition.MovePlayerImmediately(player.gameObject,inside,Quaternion.identity);
        elapsed[0]=45;visibility[0]=0;groups[0].alpha=0;
        yield return new WaitForSecondsRealtime(1.2f);
        Check(elapsed[0]==45&&groups[0].alpha==1,"being inside keeps the house indicator visible");
        Check(!doors.IsQuizOpen&&Field<bool[]>(doors,"answered").All(x=>!x),"visual cycling never opens or answers a quiz");
        Check(TruthBlock.ActiveBlocks.Where(b=>b.UsesHandBubble).All(b=>!b.CanCollectNow),"inside override cannot expose closed-house pickups");
        PrelimTeleportTransition.MovePlayerImmediately(player.gameObject,doors.TutorialBook.position+Vector3.back*2,Quaternion.identity);
        yield return new WaitForSecondsRealtime(1.2f);
        Check(elapsed[0]>45&&groups[0].alpha==0,"leaving the house resumes timed hiding");
        for(int i=0;i<6;i++)tick.Invoke(doors,new object[]{i,false,false,0f});
        File.AppendAllText(Report,"PASS: six yellow house markers, two 10-second/60-second show/hide cycles with smooth fades, proximity and interior overrides, resumed cycle, unchanged closed-house restrictions.\n");
    }
    static void CheckQuiz(TruthTableDoorInteraction doors)
    {
        var card=Field<RectTransform>(doors,"quizCard");var parent=(RectTransform)Field<GameObject>(doors,"blankPanel").transform;
        Check(Contains(ScreenRect(parent),ScreenRect(card)),"quiz fits safe screen");
        foreach(var label in card.GetComponentsInChildren<TMP_Text>())
        {label.ForceMeshUpdate();Check(!label.isTextOverflowing,"quiz text fits: "+label.text);Check(Contains(ScreenRect(card),ScreenRect(label.rectTransform)),"quiz text stays in card: "+label.name);}
        var close=(RectTransform)Field<UnityEngine.UI.Button>(doors,"closeButton").transform;
        Check(Contains(ScreenRect(card),ScreenRect(close)),"Close inside card");
    }
    static int InventoryCount()=>Field<InventorySlot[]>(InventoryManager.Instance,"slots").Sum(s=>s.physicalBlocks.Count);
    static bool IsYellow(TMP_Text label)=>label.color.r>.99f&&Mathf.Abs(label.color.g-.8f)<.01f&&label.color.b<.01f;
    static Rect ScreenRect(RectTransform rect){var c=new Vector3[4];rect.GetWorldCorners(c);return Rect.MinMaxRect(c[0].x,c[0].y,c[2].x,c[2].y);}
    static bool Contains(Rect a,Rect b)=>b.xMin>=a.xMin-1&&b.yMin>=a.yMin-1&&b.xMax<=a.xMax+1&&b.yMax<=a.yMax+1;
    static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Flags).GetValue(target);
    static void Set(object target,string name,object value)=>target.GetType().GetField(name,Flags).SetValue(target,value);
    static void Call(object target,string name)=>target.GetType().GetMethod(name,Flags).Invoke(target,null);
    static void Check(bool pass,string message){if(!pass){File.AppendAllText(Report,"FAIL "+message+"\n");throw new InvalidOperationException(message);}}
    static IEnumerator Capture(string name){Directory.CreateDirectory("Temp/TruthTableUIQA");yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Temp/TruthTableUIQA/"+Screen.width+"x"+Screen.height+"_"+name+".png");yield return null;}
}
#endif
