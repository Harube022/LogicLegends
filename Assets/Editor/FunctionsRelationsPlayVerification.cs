#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

/// <summary>Opt-in offline checks use real UI touches and the normal validator. No Admin clearing.</summary>
public static class FunctionsRelationsPlayVerification
{
    const string Report="Library/FunctionsRelationsPlayVerification.txt";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static FunctionsRelationsStageManager stage;static FunctionsRelationsPresentation ui;
    static Touchscreen touch;static int finger;
    static Button hand;
    static string questionBoardSets;
    public static IEnumerator Run()
    {
        File.WriteAllText(Report,"Offline normal gameplay: two distinct questions in all five challenges, Domain-first hand, exact removal, geometry, safe-area UI and Help.\n");
        stage=UnityEngine.Object.FindFirstObjectByType<FunctionsRelationsStageManager>();ui=UnityEngine.Object.FindFirstObjectByType<FunctionsRelationsPresentation>();
        touch=InputSystem.AddDevice<Touchscreen>();finger=9200;
        hand=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name=="InteractButton");
        try
        {
            for(int c=0;c<5;c++)Check(stage.SelectedVariant(c,0)!=stage.SelectedVariant(c,1),"distinct selected variants in challenge "+(c+1));
            yield return CheckBoundaries();
            yield return StartQuestion();
            Check(ui.progress.text.Contains("Question 1 of 2"),"HUD question one");
            // Output station cannot open or change a relation until an input is selected.
            Position(StationApproach(stage.RangeTarget));yield return Tap(hand);yield return null;
            Check(!ui.rangePanel.gameObject.activeInHierarchy&&stage.Mapping.ConnectionCount==0,"Range rejects missing input");
            foreach(int side in new[]{0,1})for(int angle=0;angle<8;angle++)
            {
                if(side==1){Position(StationApproach(stage.DomainTarget));yield return Tap(hand);yield return Tap(stage.DomainButtons[0]);}
                var target=side==0?stage.DomainTarget:stage.RangeTarget;var body=target.Find("Station Climb Guard").GetComponent<Collider>().bounds;
                Vector3 d=new Vector3(Mathf.Cos(angle*Mathf.PI/4),0,Mathf.Sin(angle*Mathf.PI/4));
                Position(Ground(new Vector3(body.center.x,0,body.center.z)+Vector3.Scale(d,new Vector3(body.extents.x+3,0,body.extents.z+3))));
                yield return Tap(hand);yield return null;
                Check((side==0?ui.domainPanel:ui.rangePanel).gameObject.activeInHierarchy,"hand opens "+target.name+" at angle "+angle);
                stage.CloseDiamondChoices();yield return null;
                Check(stage.SelectedDomain==null,"cancel clears pending input");
            }
            Log("Hand opens both stations from eight directions. Range is blocked without input; cancellation restores Domain guidance.");
            var required=stage.CurrentChallenge.requiredPairs;
            yield return Connect(required[0]);yield return Connect(required[1]);yield return null;
            var first=required[0];var second=required[1];
            Check(ui.Connections.Panel.parent == ui.rangePanel && ui.rangePanel.gameObject.activeInHierarchy,"connections belong to the existing Range panel");
            Check(stage.SelectedDomain == null && stage.RangeButtons.All(b=>!b.interactable),"completed pair leaves review only; output selection needs another input");
            yield return Tap(ui.Connections.RemoveButtons[first]);yield return null;
            Check(ui.rangePanel.gameObject.activeInHierarchy,"individual removal keeps Range review open");
            Check(!stage.Mapping.Pairs.Contains(first)&&stage.Mapping.Pairs.Contains(second)&&stage.Mapping.ConnectionCount==1,"delete older pair without undoing newer pair");
            Check(stage.WorldBoardLabels[1].text==questionBoardSets,"removing a connection never changes the given Domain or Range");
            Check(!GameObject.Find("Laser "+first),"removed beam disappears");
            Log("Individual Remove changes the beam and validation snapshot immediately; later connections remain.");
            float before=stage.TimeRemaining;int mistakes=stage.MistakeCount;
            yield return Tap(stage.SubmitButton);yield return new WaitForSecondsRealtime(.9f);
            Check(stage.MistakeCount==mistakes+1&&before-stage.TimeRemaining>=10&&before-stage.TimeRemaining<12,"existing single 10-second penalty");
            Check(stage.Mapping.Pairs.Contains(second),"incorrect submission preserves connections");
            Log("Incorrect arrangement keeps connections and applies the existing penalty once.");
            foreach(int size in new[]{6,12,2})
            {
                BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(.5f);Canvas.ForceUpdateCanvases();
                var safe=Rect(ui.safeArea);var hud=Rect(ui.hud);Check(Contains(safe,hud),"HUD safe area");
                Check(Mathf.Abs(Rect(ui.status).width-hud.width)<2&&Mathf.Abs(Rect(ui.interaction).width-hud.width)<2,"message widths match HUD");
                Check(Rect(ui.status).yMax<hud.yMin&&Rect(ui.interaction).yMax<Rect(ui.status).yMin,"messages stack below HUD");
                Position(StationApproach(stage.RangeTarget));
                yield return null;yield return null;
                // Reopen through Domain first if leaving the station closed its review.
                if(!ui.rangePanel.gameObject.activeInHierarchy)
                {
                    Position(StationApproach(stage.DomainTarget));yield return Tap(hand);yield return Tap(stage.DomainButtons[0]);
                    Position(StationApproach(stage.RangeTarget));yield return Tap(hand);yield return null;
                }
                Check(ui.rangePanel.gameObject.activeInHierarchy,"Range panel available for layout checks");
                var panel=Rect(ui.rangePanel);Check(Contains(safe,panel),"Range panel fits safe area");
                var list=Rect(ui.Connections.Panel);Check(Contains(panel,list),"connection list fits inside Range");
                foreach(var b in stage.RangeButtons)Check(!list.Overlaps(Rect((RectTransform)b.transform)),"connection list avoids output choices");
                foreach(var control in ui.mobileControls.Where(r=>r.gameObject.activeInHierarchy))
                {
                    Check(Contains(safe,Rect(control)),"mobile control safe area "+control.name);
                    if(new[]{"Joystick","JumpButton","InteractButton"}.Contains(control.name))
                    {
                        var saved=SessionState.GetString("Functions.PrelimLayout."+control.name,"");Check(saved.Length>0,"PRELIM reference captured");
                        var reference=JsonUtility.FromJson<FunctionsRelationsRestorePresentation.ControlLayout>(saved);
                        Check(reference.Matches(control),"exact PRELIM layout preserved in Play Mode: "+control.name);
                        Check(!Rect(control).Overlaps(panel),"Range avoids mobile controls "+control.name);
                    }
                    Check(!Rect(control).Overlaps(hud)&&!Rect(control).Overlaps(Rect(ui.status))&&!Rect(control).Overlaps(Rect(ui.interaction)),"instructions clear of "+control.name);
                    Check(!Rect(control).Overlaps(Rect((RectTransform)stage.SubmitButton.transform))&&!Rect(control).Overlaps(Rect((RectTransform)stage.ViewBoardButton.transform)),"actions clear of "+control.name);
                }
                foreach(var text in ui.hud.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();Check(!text.isTextOverflowing,"HUD fits "+text.text);}
                yield return Capture("Library/FunctionsRangeRestored_"+Screen.width+"x"+Screen.height+".png");
                stage.CloseDiamondChoices();yield return null;
                float clock=stage.TimeRemaining;var question=stage.CurrentChallenge;var pairs=stage.Mapping.Snapshot();
                yield return Tap(stage.Help.HelpButton);clock=stage.TimeRemaining;yield return new WaitForSecondsRealtime(.5f);
                var page=UnityEngine.Object.FindFirstObjectByType<StageHelpPage>();Check(page!=null,"Help opens");
                Check(stage.TimeRemaining==clock&&Time.timeScale==0,"Help pauses countdown");
                MobileInstructionAssetChecks.VerifyVisibleCopy("Functions and Relations");
                yield return SetsHelpVerification.Run(page,"Functions and Relations",FunctionsRelationsHelpClipCapture.Captions,Report);
                yield return Tap(page.BeginButton);yield return null;
                Check(!stage.Help.IsOpen&&!GameInput.Instance.GameplayInputBlocked&&Player.LocalInstance.enabled,"Help releases input");
                Check(ReferenceEquals(question,stage.CurrentChallenge)&&MappingRuleEvaluator.ExactPairs(stage.Mapping.Snapshot(),pairs),"Help keeps question and connections");
            }
            Log("Three screen sizes: compact question HUD, stacked instructions, four decoded videos/fades, touch playback/replay, Help exit and preserved data.");
            BookMapMobileChecks.SetSize(6);yield return new WaitForSecondsRealtime(.4f);
            Position(new Vector3(-188,2,908));yield return Tap(stage.ViewBoardButton);yield return new WaitForSecondsRealtime(1);
            Check(stage.IsBoardOpen&&stage.BoardView.Viewing,"board opens without submission");yield return Capture("Library/FunctionsBoard.png");
            yield return Tap(stage.BoardView.backButton);yield return new WaitForSecondsRealtime(1);
            Check(!stage.IsBoardOpen&&!GameInput.Instance.GameplayInputBlocked&&Player.LocalInstance.enabled,"Back restores normal control");
            for(int c=0;c<5;c++)for(int q=0;q<2;q++)
            {
                Check(stage.ChallengeIndex==c&&stage.QuestionIndex==q,"progress begins correct question");
                if(stage.State==FunctionsRelationsState.WAITING_FOR_ACTIVATION)yield return StartQuestion();
                foreach(var text in stage.WorldBoardLabels){text.ForceMeshUpdate(true);Check(!text.isTextOverflowing,"board text fits C"+(c+1)+"Q"+(q+1)+": "+text.name);}
                var data=stage.CurrentChallenge;
                if(data.questionKind==MappingQuestionKind.BuildExactRelation)
                {
                    foreach(var pair in data.requiredPairs)if(!stage.Mapping.Pairs.Contains(pair))yield return Connect(pair);
                    if(c==2)
                    {
                        var fixedPair=data.initialPairs[0];Check(stage.Mapping.IsLocked(fixedPair)&&!stage.RemoveConnection(fixedPair),"preset cannot be removed");
                        Check(!ui.Connections.RemoveButtons.ContainsKey(fixedPair),"fixed row has no Remove control");
                    }
                }
                else
                {
                    var altars=Field<Transform[]>(stage,"classificationPedestals");
                    for(int i=0;i<3;i++)if(data.questionKind==MappingQuestionKind.ClassifyFunction?i==data.correctClassificationIndex:(data.correctProperties&(i==0?MappingProperty.Injective:i==1?MappingProperty.Surjective:MappingProperty.Bijective))!=0)
                    {Position(Ground(altars[i].GetComponent<Renderer>().bounds.center+Vector3.back*2.7f));yield return Tap(hand);}
                }
                int correct=stage.CorrectCount;float time=stage.TimeRemaining;
                yield return Tap(stage.SubmitButton);
                float readyBy=Time.realtimeSinceStartup+6;
                while(stage.State==FunctionsRelationsState.CHALLENGE_COMPLETE&&Time.realtimeSinceStartup<readyBy)yield return null;
                Check(stage.CorrectCount==correct+1,"normal complete answer accepted C"+(c+1)+"Q"+(q+1));
                if(c!=4||q!=1)
                {
                    Check(stage.State==FunctionsRelationsState.WAITING_FOR_ACTIVATION,"next question waits for book C"+(c+1)+"Q"+(q+1)+" state="+stage.State+" scale="+Time.timeScale);float paused=stage.TimeRemaining;
                    yield return new WaitForSecondsRealtime(.25f);Check(stage.TimeRemaining==paused&&paused<=time,"success pauses shared remaining time");
                    Check(stage.ChallengeIndex==c+(q==1?1:0)&&stage.QuestionIndex==(q==0?1:0),"only two answers advance challenge");
                }
            }
            Check(stage.State==FunctionsRelationsState.STAGE_COMPLETE&&stage.CorrectCount==10&&stage.CompletedChallengeCount==5,"ten questions finish five challenges");
            Log("All ten real answers accepted: two distinct questions per challenge, fixed links protected, resets/book gating/shared clock preserved.");
            Log("ALL FUNCTIONS AND RELATIONS CHECKS PASSED");
        }
        finally {if(touch!=null&&touch.added)InputSystem.RemoveDevice(touch);}
    }
    static IEnumerator StartQuestion()
    {
        Position(stage.BookTarget.position+Vector3.back*3);yield return null;yield return Tap(hand);Check(stage.State==FunctionsRelationsState.ACTIVE,"hand starts waiting book");
        var question=stage.CurrentChallenge;
        questionBoardSets=stage.WorldBoardLabels[1].text;
        Check(questionBoardSets=="<b>Domain</b> = { "+string.Join(", ",question.domain)+" }\n<b>Range</b> = { "+string.Join(", ",question.IntendedRange)+" }","board shows only the question's two given sets");
        Check(!stage.WorldBoardLabels[2].text.Contains("Tap Submit to check")&&!stage.WorldBoardLabels[2].text.Contains("The range contains the outputs you use"),"board has no retired boilerplate");
    }
    static IEnumerator Connect(MappingPair pair)
    {
        Position(StationApproach(stage.DomainTarget));yield return Tap(hand);yield return Tap(stage.DomainButtons[Array.IndexOf(stage.CurrentChallenge.domain,pair.domain)]);
        yield return null;Check(stage.SelectedDomain==pair.domain,"input selected");
        Check(stage.WorldBoardLabels[1].text==questionBoardSets,"temporary input selection leaves given sets unchanged");
        var arrow=UnityEngine.Object.FindFirstObjectByType<FunctionsRelationsGuidance>();Check(arrow.Target==stage.RangeTarget,"arrow switches to Range");
        Position(StationApproach(stage.RangeTarget));yield return Tap(hand);yield return Tap(stage.RangeButtons[Array.IndexOf(stage.CurrentChallenge.codomain,pair.codomain)]);yield return null;
        Check(stage.Mapping.Pairs.Contains(pair)&&stage.SelectedDomain==null&&arrow.Target==stage.DomainTarget,"pair complete and guidance returns to Domain");
        Check(stage.WorldBoardLabels[1].text==questionBoardSets,"partial connections leave the intended Range unchanged");
    }
    static IEnumerator CheckBoundaries()
    {
        var p=Player.LocalInstance;var cc=p.GetComponent<CharacterController>();p.ToggleControl(false);
        foreach(var item in new[]{new Vector3(-240,1,900),new Vector3(-137,1,900),new Vector3(-188,1,857),new Vector3(-188,1,950)})
        {Position(Ground(item));Vector3 d=item.x<-230?Vector3.left:item.x>-145?Vector3.right:item.z<880?Vector3.back:Vector3.forward;cc.Move(d*100);yield return null;Check(p.transform.position.x>-247&&p.transform.position.x<-130&&p.transform.position.z>850&&p.transform.position.z<957,"continuous perimeter stops player");}
        foreach(string name in new[]{"Domain","Range","Pedestals"})
        {
            var target=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().First(g=>g.name==name);
            var guard=target.GetComponentsInChildren<BoxCollider>(true).Single(b=>b.name.Contains("Climb Guard"));
            Check(!guard.isTrigger&&guard.size.y*guard.transform.lossyScale.y>40,"vertical unclimbable guard "+name);
        }
        var root=GameObject.Find("StageCollisionGuards");Check(root.GetComponentsInChildren<BoxCollider>().Count(c=>c.name.StartsWith("Tree Guard"))==52,"all embedded trees guarded");
        var path=GameObject.Find("Grounded Stone Path");Check(path!=null&&path.GetComponent<MeshCollider>().sharedMesh!=null,"grounded path retains collision");
        Check(GameObject.Find("StageBorderDecorations")==null&&GameObject.Find("Distant Meadow")==null,"recent decorative additions removed");
        Check(GameObject.Find("Stage")!=null&&GameObject.Find("Trees")!=null,"original environment retained");
        p.ToggleControl(true);Log("Physical controller sweeps blocked on all four edges. Tall tree/altar guards, grounded path, restored default scenery verified.");
    }
    public static Vector3 StationApproach(Transform target)
    {var b=target.Find("Station Climb Guard").GetComponent<Collider>().bounds;return Ground(new Vector3(b.center.x,0,b.min.z-2));}
    public static Vector3 Ground(Vector3 p)
    {
        foreach(var h in Physics.RaycastAll(new Vector3(p.x,15,p.z),Vector3.down,50,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
            if(h.normal.y>.65f&&h.collider.GetComponentInParent<Player>()==null)return new Vector3(p.x,h.point.y+.12f,p.z);
        throw new InvalidOperationException("No valid ground at "+p);
    }
    static void Position(Vector3 p)=>PrelimTeleportTransition.MovePlayerImmediately(Player.LocalInstance.gameObject,Ground(p),Quaternion.identity);
    static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Private).GetValue(o);
    static IEnumerator Tap(Button button)
    {
        Check(button!=null&&button.gameObject.activeInHierarchy&&button.IsInteractable(),"available touch "+button?.name);Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();
        var point=Rect((RectTransform)button.transform).center;var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"touch raycast reaches "+button.name);
        int id=++finger;InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Began,position=point});yield return null;yield return null;
        InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=point});yield return null;yield return null;
    }
    static Rect Rect(RectTransform rt){var c=new Vector3[4];rt.GetWorldCorners(c);var cv=rt.GetComponentInParent<Canvas>();var cam=cv!=null&&cv.renderMode!=RenderMode.ScreenSpaceOverlay?cv.worldCamera:null;var p=c.Select(v=>RectTransformUtility.WorldToScreenPoint(cam,v)).ToArray();return UnityEngine.Rect.MinMaxRect(p.Min(v=>v.x),p.Min(v=>v.y),p.Max(v=>v.x),p.Max(v=>v.y));}
    static bool Contains(Rect a,Rect b)=>b.xMin>=a.xMin-1&&b.yMin>=a.yMin-1&&b.xMax<=a.xMax+1&&b.yMax<=a.yMax+1;
    static IEnumerator Capture(string path){yield return new WaitForEndOfFrame();var t=new Texture2D(Screen.width,Screen.height,TextureFormat.RGBA32,false);t.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.Destroy(t);}
    static void Check(bool valid,string message){if(!valid)throw new InvalidOperationException("Functions verification: "+message);}
    static void Log(string message)=>File.AppendAllText(Report,"PASS "+message+"\n");
}
#endif
