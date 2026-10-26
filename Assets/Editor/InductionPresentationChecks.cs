#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LogicLegends.Induction;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

/// <summary>Offline Play Mode checks using the real hand, catalogue, placement and Submit UI.</summary>
public static class InductionPresentationChecks
{
    const string Report="Library/InductionPresentationChecks.txt";
    static InductionStageManager stage;
    static Touchscreen touch;static int finger=8000;static Button hand;
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static RectTransform Hud=>stage.ui.TimerTarget.parent as RectTransform;
    static RectTransform Notice=>stage.ui.Root.Find("PrelimChallengeCompletionNotice") as RectTransform;
    public static IEnumerator Run()
    {
        File.WriteAllText(Report,"Isolated Induction presentation, chamber access and all-five-challenge checks.\n");
        stage=InductionStageManager.Instance;touch=InputSystem.AddDevice<Touchscreen>();
        hand=stage.uiHost.GetComponentsInChildren<Button>(true).Single(b=>b.name=="InteractButton");
        try
        {
            yield return Tutorial();
            yield return WalkRoutes();
            foreach(int resolution in new[]{6,12,3})
            {
                BookMapMobileChecks.SetSize(resolution);yield return new WaitForSecondsRealtime(.4f);stage.ui.Refresh();Canvas.ForceUpdateCanvases();
                Check(Hud.sizeDelta==new Vector2(500,136)&&Mathf.Abs(Hud.anchoredPosition.x+24)<.1f,"exact PRELIM HUD dimensions and safe-area dock");
                if(Screen.width>=Screen.height)Check(Mathf.Abs(Hud.anchoredPosition.y+24)<.1f,"PRELIM landscape top-right position");
                Check(Hud.GetComponent<Image>().sprite.name=="GraphStatusFrame","shared PRELIM frame artwork");
                foreach(var text in Hud.GetComponentsInChildren<TMP_Text>())Check(text.font==StageUITheme.HeadingFont&&!text.isTextOverflowing,"PRELIM font and unclipped HUD: "+text.text);
                foreach(var button in stage.uiHost.GetComponentsInChildren<Button>().Where(b=>b.name=="SettingsButton"||b==stage.help.HelpButton))Check(!Rect(Hud).Overlaps(Rect((RectTransform)button.transform)),"HUD clears settings and Help");
                stage.ui.ShowWaiting(1,2);yield return null;yield return new WaitForEndOfFrame();
                CheckWaiting(1);yield return Capture("Waiting_"+Screen.width+"x"+Screen.height);
                Notice.gameObject.SetActive(false);
            }
            BookMapMobileChecks.SetSize(6);yield return new WaitForSecondsRealtime(.3f);
            for(int challenge=0;challenge<5;challenge++)
            {
                Face(stage.book.position+Vector3.back*2.6f,stage.book.position);yield return null;yield return Tap(hand);
                Check(stage.State==InductionState.Active&&stage.ChallengeIndex==challenge&&stage.timer.Running,"hand starts/resumes C"+(challenge+1));
                var message=Notice.GetComponentInChildren<TMP_Text>();
                Check(Notice.gameObject.activeSelf&&message.text=="Start!"&&!Notice.GetComponent<Image>().enabled,"transparent PRELIM Start on successful activation");
                yield return new WaitForSecondsRealtime(.35f);Check(Notice.gameObject.activeSelf,"Start remains visible during PRELIM interval");
                yield return Capture("Start_C"+(challenge+1));
                yield return new WaitForSecondsRealtime(1.25f);Check(!Notice.gameObject.activeSelf,"Start expires after shared 1.5-second interval");
                // Verify the normal board transition and Back at every challenge.
                var b=stage.stations[0].GetComponent<Renderer>().bounds;
                Face(new Vector3(b.center.x,29.12f,b.min.z-2.3f),b.center);yield return null;yield return Tap(stage.ui.ViewBoardButton);
                float before=stage.timer.Remaining;yield return new WaitForSecondsRealtime(1);
                Check(stage.boardView.Viewing&&!stage.boardView.Transitioning&&Mathf.Abs(stage.timer.Remaining-before)<.1f,"C"+(challenge+1)+" board camera and paused timer");
                yield return Capture("Board_C"+(challenge+1));yield return Tap(stage.ui.BoardBackButton);yield return new WaitForSecondsRealtime(1);
                Check(!stage.Reading&&!stage.boardView.Viewing&&stage.CanAct,"Back restores controls and camera");
                foreach(var answer in stage.Challenge.answers)
                {
                    var tablet=stage.Tablets.Single(t=>t.Data.id==answer.Value);yield return Collect(tablet);
                    var slot=stage.slots.Single(s=>s.key==answer.Key);var station=stage.stations.First(s=>slot.transform.IsChildOf(s));b=station.GetComponent<Renderer>().bounds;
                    Face(new Vector3(slot.transform.position.x<=b.center.x?b.min.x-1.25f:b.max.x+1.25f,29.12f,slot.transform.position.z),slot.transform.position);
                    yield return null;yield return Tap(stage.ui.InteractButton);Check(slot.Tablet==tablet&&stage.Held==null,"C"+(challenge+1)+" normal placement "+slot.key);
                }
                foreach(int size in new[]{6,12,3})
                {
                    BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(.2f);stage.ui.Refresh();Canvas.ForceUpdateCanvases();
                    var safe=Rect((RectTransform)stage.ui.Root.parent);var submit=Rect((RectTransform)stage.ui.SubmitButton.transform);
                    Check(Mathf.Abs(submit.center.x-safe.center.x)<2&&submit.yMin>=safe.yMin,"Submit bottom-center safe area");
                    foreach(var button in stage.uiHost.GetComponentsInChildren<Button>().Where(bu=>bu.name=="JumpButton"||bu.name=="InteractButton"))
                        Check(!submit.Overlaps(Rect((RectTransform)button.transform)),"Submit clears "+button.name);
                    Check(Rect(Hud).xMin>=safe.xMin-1&&Rect(Hud).xMax<=safe.xMax+1,"HUD fits mobile safe area");
                    yield return Capture("Arranged_C"+(challenge+1)+"_"+Screen.width+"x"+Screen.height);
                }
                BookMapMobileChecks.SetSize(6);yield return new WaitForSecondsRealtime(.2f);yield return Tap(stage.ui.SubmitButton);
                Check(stage.Correct==challenge+1&&stage.Mistakes==0,"Submit validates complete C"+(challenge+1)+" exactly once");
                yield return null;yield return new WaitForEndOfFrame();
                if(challenge<4)
                {
                    Check(stage.State==InductionState.WaitingForBook&&!stage.timer.Running,"next challenge waits for book with paused timer");CheckWaiting(challenge+1);
                    yield return Capture("Completed_C"+(challenge+1));
                    yield return Tap(stage.help.HelpButton);yield return null;Check(!Notice.gameObject.activeSelf,"Help hides completion prompt");
                    var page=UnityEngine.Object.FindFirstObjectByType<StageHelpPage>();yield return Tap(page.BeginButton);yield return null;
                    CheckWaiting(challenge+1);Check(stage.CanAct&&!stage.onboarding.IsActive,"reopened Help restores waiting prompt and gameplay");
                }
            }
            Check(stage.State==InductionState.StageComplete&&!Notice.gameObject.activeSelf&&stage.ui.Result.Root.gameObject.activeInHierarchy,"final challenge uses only completion screen");
            Log("All five challenges: actual touch starts, board/Back, catalogue/Get, all placements, Submit, notices, mobile layouts and final results passed.");
            Log("ALL INDUCTION PRESENTATION CHECKS PASSED");
        }
        finally{if(touch!=null&&touch.added)InputSystem.RemoveDevice(touch);}
    }
    static void CheckWaiting(int completed)
    {
        Canvas.ForceUpdateCanvases();var text=Notice.GetComponentInChildren<TMP_Text>();text.ForceMeshUpdate();
        Check(Notice.gameObject.activeSelf&&!Notice.GetComponent<Image>().enabled&&text.text=="Challenge "+completed+" completed! Interact with the book statue to start Challenge "+(completed+1)+".","correct transparent completion prompt");
        var hud=Rect(Hud);var notice=Rect(Notice);
        Check(Mathf.Abs(hud.width-notice.width)<2&&Mathf.Abs(hud.xMax-notice.xMax)<2&&notice.yMax<hud.yMin&&!text.isTextOverflowing,"wrapped notice directly below HUD with identical width");
        foreach(var control in stage.uiHost.GetComponentsInChildren<Button>().Where(b=>b.name=="JumpButton"||b.name=="InteractButton"))Check(!notice.Overlaps(Rect((RectTransform)control.transform)),"completion prompt clears "+control.name);
    }
    static IEnumerator Tutorial()
    {
        var t=stage.onboarding;typeof(InductionHelpUI).GetProperty("OnboardingPending").SetValue(stage.help,true);
        Check(t.Begin(stage),"initial tutorial begins");
        var renderers=GameObject.Find("ChamberDecorations").GetComponentsInChildren<Renderer>(true).Concat(GameObject.Find("ChamberCeiling").GetComponentsInChildren<Renderer>(true)).ToDictionary(r=>r,r=>r.enabled);
        for(int attempt=0;attempt<2;attempt++)
        {
            var mobile=MobileInputUI.FindJoystick();var background=(RectTransform)typeof(MobileInputUI).GetField("joystickBackground",Private).GetValue(mobile);
            var point=RectTransformUtility.WorldToScreenPoint(null,background.TransformPoint(new Vector3(background.rect.width*.44f,0,0)));
            mobile.OnPointerDown(new PointerEventData(EventSystem.current){position=point,pointerId=0});
            yield return Until(()=>t.CurrentStep==InductionTutorialStep.Jump,5,"intentional joystick movement");mobile.ResetJoystick();
            yield return Until(()=>Player.LocalInstance.GetComponent<CharacterController>().isGrounded,4,"grounded before jump touch");
            yield return new WaitForSecondsRealtime(.2f);
            yield return Tap(stage.uiHost.GetComponentsInChildren<Button>(true).Single(b=>b.name=="JumpButton"));
            yield return Until(()=>t.CurrentStep==InductionTutorialStep.Interaction,4,"successful jump");yield return Tap(hand);
            Check(t.CurrentStep==InductionTutorialStep.Book&&t.CameraTransitioning,"hand practice starts smooth book camera");
            Check(renderers.Where(p=>p.Key.name=="ChamberCeiling"||p.Key.GetComponentsInParent<Transform>().Any(x=>x.name=="Hanging Chandelier")).All(p=>!p.Key.enabled),"ceiling and hanging fixtures hidden during tutorial views");
            if(attempt==0){yield return Tap(t.SkipControl.GetComponent<Button>());Check(renderers.All(p=>p.Key.enabled==p.Value),"skip during camera blend restores all art");yield return Tap(t.NoButton);continue;}
            yield return Until(()=>!t.CameraTransitioning,4,"book close-up settles");yield return Capture("Tutorial_Book");yield return Tap(hand);
            Check(t.CurrentStep==InductionTutorialStep.Timer&&t.Highlight.parent==stage.ui.TimerTarget,"timer highlight follows actual compact HUD");yield return Tap(t.NextButton);
            Check(t.Highlight.parent==stage.ui.ProgressTarget,"progress highlight follows HUD");yield return Tap(t.NextButton);
            yield return Until(()=>!t.CameraTransitioning,4,"stand close-up");yield return Tap(hand);yield return Tap(stage.ui.PracticeChoice);yield return Tap(stage.ui.GetScrollButton);
            yield return Until(()=>!t.CameraTransitioning,4,"altar overview");yield return Capture("Tutorial_Altars");
            for(int i=0;i<4;i++)yield return Tap(t.NextButton);
            Check(t.CurrentStep==InductionTutorialStep.Place,"practice placement step");yield return Tap(stage.ui.InteractButton);
            yield return Tap(t.NextButton);yield return Tap(t.NextButton);yield return Tap(stage.ui.SubmitButton);yield return Tap(stage.ui.ViewBoardButton);
            yield return Until(()=>!t.CameraTransitioning,4,"practice board close-up");yield return Capture("Tutorial_Board");yield return Tap(t.NextButton);yield return Tap(stage.ui.BoardBackButton);
            yield return Until(()=>t.CurrentStep==InductionTutorialStep.Confirm,4,"practice Back restores camera");
            Check(renderers.All(p=>p.Key.enabled==p.Value),"completed tutorial restores ceiling and suspended decorations");yield return Tap(t.YesButton);
        }
        Check(!t.IsActive&&!stage.help.OnboardingPending&&stage.Correct==0&&stage.Mistakes==0&&stage.State==InductionState.WaitingForBook&&stage.timer.Remaining==stage.startingSeconds,"tutorial completion/skip preserve time and progress");
        Log("Full tutorial and skip during transition: actual HUD targets, practice content cleanup, camera/ceiling/lamp restoration and unchanged time/progress.");
    }
    static IEnumerator WalkRoutes()
    {
        var art=GameObject.Find("ChamberDecorations");Check(art.GetComponentsInChildren<MonoBehaviour>(true).Length==0,"decorations contain no gameplay scripts");
        Check(art.GetComponentsInChildren<Light>().Count(l=>l.enabled)==12,"eight warm shared lamps plus four accent lights");
        foreach(var r in art.GetComponentsInChildren<Renderer>())foreach(var m in r.sharedMaterials)Check(m!=null&&m.shader.isSupported,"supported art material");
        var route=new[]{new Vector3(-109.23f,29.12f,252),new Vector3(-114.23f,29.12f,256.21f),new Vector3(-115,29.12f,267),new Vector3(-99.2f,29.12f,266),new Vector3(-88.45f,29.12f,258.97f),new Vector3(-82.4f,29.12f,268.8f),new Vector3(-82.4f,29.12f,280),new Vector3(-99.49f,29.12f,281)};
        Face(route[0],stage.book.position);var controller=Player.LocalInstance.GetComponent<CharacterController>();
        foreach(var goal in route.Skip(1))
        {
            float until=Time.realtimeSinceStartup+8;
            while(Vector2.Distance(new Vector2(Player.LocalInstance.transform.position.x,Player.LocalInstance.transform.position.z),new Vector2(goal.x,goal.z))>.3f&&Time.realtimeSinceStartup<until)
            {
                var delta=goal-Player.LocalInstance.transform.position;delta.y=0;
                controller.Move((delta.normalized*7+Vector3.down*2)*Time.deltaTime);yield return null;
            }
            Check(Vector2.Distance(new Vector2(Player.LocalInstance.transform.position.x,Player.LocalInstance.transform.position.z),new Vector2(goal.x,goal.z))<.5f,"physical walking route clear: "+goal);
        }
        yield return Capture("Chamber_Access");Log("Solid CharacterController route from spawn/book around all three altars, scroll stand and main board is clear.");
    }
    static IEnumerator Collect(InductionTablet tablet)
    {
        Face(stage.scrollStand.position+Vector3.back*2.8f,stage.scrollStand.position);yield return null;yield return Tap(hand);Check(stage.BrowsingScrolls,"hand opens catalogue");
        var button=stage.ui.Collection.GetComponentsInChildren<Button>().Single(b=>b.name=="Scroll Choice "+tablet.Data.id);var scroll=button.GetComponentInParent<ScrollRect>();Canvas.ForceUpdateCanvases();
        scroll.content.anchoredPosition=new Vector2(0,Mathf.Clamp(-((RectTransform)button.transform).anchoredPosition.y,0,Mathf.Max(0,scroll.content.rect.height-scroll.viewport.rect.height)));
        yield return null;yield return Tap(button);yield return Tap(stage.ui.GetScrollButton);Check(stage.Held==tablet&&!stage.Reading,"exact scroll collected once");
    }
    static void Face(Vector3 at,Vector3 toward){at.y=29.12f;var d=toward-at;d.y=0;PrelimTeleportTransition.MovePlayerImmediately(Player.LocalInstance.gameObject,at,Quaternion.LookRotation(d));}
    static IEnumerator Until(Func<bool> predicate,float seconds,string label){float until=Time.realtimeSinceStartup+seconds;while(!predicate()&&Time.realtimeSinceStartup<until)yield return null;Check(predicate(),label);}
    static IEnumerator Tap(Button button)
    {
        Check(button!=null&&button.gameObject.activeInHierarchy&&button.IsInteractable(),"available touch: "+button?.name);yield return null;Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();
        var point=Rect((RectTransform)button.transform).center;var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"raycast reaches "+button.name);int id=++finger;
        InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Began,position=point});yield return null;yield return null;
        InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=point});yield return null;yield return null;
    }
    static Rect Rect(RectTransform rect){var corners=new Vector3[4];rect.GetWorldCorners(corners);return UnityEngine.Rect.MinMaxRect(corners.Min(c=>c.x),corners.Min(c=>c.y),corners.Max(c=>c.x),corners.Max(c=>c.y));}
    static IEnumerator Capture(string name){yield return new WaitForEndOfFrame();var t=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);t.Apply();File.WriteAllBytes("Library/InductionPresentation_"+name+".png",t.EncodeToPNG());UnityEngine.Object.Destroy(t);}
    static void Check(bool valid,string detail){if(!valid)throw new InvalidOperationException("Induction presentation: "+detail);}
    static void Log(string detail){File.AppendAllText(Report,"PASS "+detail+"\n");}
}
#endif
