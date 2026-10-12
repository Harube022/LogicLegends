using System;
using System.Collections;
using System.Linq;
using LogicLegends.Induction;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Uses real joystick pointer events, character motion, jump and hand callbacks.</summary>
public static class InductionOnboardingPlayChecks
{
    public static bool CaptureScreens;
    public static IEnumerator Run(bool exerciseReplay=true)
    {
        var m=InductionStageManager.Instance;var t=m.onboarding;var input=GameInput.Instance;var player=Player.LocalInstance;
        Check(t.IsActive&&t.CurrentStep==InductionTutorialStep.Movement,"first Help confirmation starts movement practice");
        var stock=m.Tablets.Select(s=>s.Data.id).OrderBy(s=>s).ToArray();float clock=m.timer.Remaining;
        input.SetMobileMovement(Vector2.right);input.MobileJump();input.MobileInteract();m.InteractBook();m.Submit();m.OpenScrollCollection();m.ui.SubmitButton.onClick.Invoke();
        yield return new WaitForSecondsRealtime(.2f);
        Check(t.CurrentStep==InductionTutorialStep.Movement&&m.Correct==0&&!m.BrowsingScrolls,"keyboard/mobile-vector/button inputs alone do not advance or activate real game");
        input.SetMobileMovement(Vector2.zero);
        var mobile=MobileInputUI.FindJoystick();Drag(mobile,new Vector2(.1f,0));yield return new WaitForSecondsRealtime(.2f);
        Check(t.CurrentStep==InductionTutorialStep.Movement,"joystick dead zone ignored");mobile.ResetJoystick();
        Drag(mobile,new Vector2(.9f,0));yield return new WaitForSecondsRealtime(.12f);mobile.ResetJoystick();
        Check(t.CurrentStep==InductionTutorialStep.Movement,"brief joystick nudge cannot complete movement");
        yield return Actions(m);
        if(exerciseReplay)
        {
            t.NoButton.onClick.Invoke();Check(t.IsActive&&t.CurrentStep==InductionTutorialStep.Movement&&t.PracticeObject==null&&Time.timeScale==1,"No restarts and cleans practice");
            yield return Actions(m,true);
            Check(!t.CameraTransitioning&&!m.ui.PracticeCatalogueOpen&&Camera.main.GetComponent<Unity.Cinemachine.CinemachineBrain>().enabled,"Skip cancels in-flight book camera and cleans content");
            t.NoButton.onClick.Invoke();
            yield return Actions(m,false,true);
            Check(t.CurrentStep==InductionTutorialStep.Confirm&&!m.ui.PracticeCatalogueOpen&&t.Highlight!=null,"Skip from parchment catalogue restores reusable highlights");
            t.NoButton.onClick.Invoke();
            yield return Actions(m);
        }
        t.YesButton.onClick.Invoke();yield return null;
        Check(!t.IsActive&&InductionControlsOnboarding.Active==null&&!m.help.OnboardingPending&&!GameInput.Instance.GameplayInputBlocked&&player.enabled,"Yes restores real controls");
        Check(m.State==InductionState.WaitingForBook&&!m.timer.Running&&m.timer.Remaining==clock&&m.Correct==0&&m.Mistakes==0&&m.Held==null&&m.slots.All(s=>s.Tablet==null),"tutorial preserves question, time and placements");
        Check(m.Tablets.Select(s=>s.Data.id).OrderBy(s=>s).SequenceEqual(stock)&&m.Tablets.All(s=>s.AtStand),"no gameplay scroll collected");
        Check(!UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(s=>s.name=="Induction Practice Interaction"||s.name=="Induction Control Highlight"||s.name=="Induction Control Instructions"||s.name=="Induction Tutorial Confirmation"||s.name=="Induction Demonstration Scroll"||s.name=="Induction Practice Altar Arrow"||s.name=="PRELIM Shared Skip Tutorial"||s.name=="Practice Scroll Choice"),"all tutorial content removed");
        m.help.Open();m.help.BeginButton.onClick.Invoke();Check(!t.IsActive&&!m.help.IsOpen&&!m.timer.Running,"reopening Help never repeats onboarding");
        Debug.Log("INDUCTION_ONBOARDING_CHECKS_PASS: intentional joystick displacement, jump, hand, practice book, parchment selection/Get, three altars, practice placement/Submit, View Board/Back, single Skip during blend, replay, Yes, camera/UI cleanup and unchanged real time/progress/inventory.");
    }
    static IEnumerator Actions(InductionStageManager m,bool skip=false,bool skipCatalogue=false)
    {
        var t=m.onboarding;var input=GameInput.Instance;var player=Player.LocalInstance;
        var mobile=MobileInputUI.FindJoystick();var controller=player.GetComponent<CharacterController>();
        float until=Time.realtimeSinceStartup+3;while(!controller.isGrounded&&Time.realtimeSinceStartup<until)yield return null;
        Check(controller.isGrounded,"player grounded for practice");Vector3 start=player.transform.position;
        Drag(mobile,new Vector2(.9f,0));until=Time.realtimeSinceStartup+3;
        while(t.CurrentStep==InductionTutorialStep.Movement&&Time.realtimeSinceStartup<until)yield return null;
        Check(t.CurrentStep==InductionTutorialStep.Jump&&Vector2.Distance(new Vector2(start.x,start.z),new Vector2(player.transform.position.x,player.transform.position.z))>=t.movementDistance,"actual joystick moves character");
        Check(t.Highlight.parent.name=="JumpButton","highlight follows jump control");
        input.MobileInteract();Check(t.CurrentStep==InductionTutorialStep.Jump,"hand before practice cannot advance");
        var jump=m.uiHost.GetComponentsInChildren<Button>(true).Single(b=>b.name=="JumpButton");jump.onClick.Invoke();until=Time.realtimeSinceStartup+3;
        while(t.CurrentStep==InductionTutorialStep.Jump&&Time.realtimeSinceStartup<until)yield return null;
        Check(t.CurrentStep==InductionTutorialStep.Interaction&&player.IsJumping()&&t.PracticeObject!=null,"real successful jump advances");
        Check(t.Highlight.parent.name=="InteractButton"&&t.PracticeObject.GetComponent<Collider>().isTrigger&&t.PracticeObject.GetComponent<GrabbableObject>()==null,"isolated hand practice target");
        Vector3 toward=t.PracticeObject.transform.position-player.transform.position;toward.y=0;player.transform.rotation=Quaternion.LookRotation(-toward);
        input.MobileInteract();Check(t.CurrentStep==InductionTutorialStep.Interaction,"wrong facing rejected");
        player.transform.rotation=Quaternion.LookRotation(toward);
        m.uiHost.GetComponentsInChildren<Button>(true).Single(b=>b.name=="InteractButton").onClick.Invoke();
        Check(t.CurrentStep==InductionTutorialStep.Book&&t.PracticeObject==null,"hand practice advances to book, never ends tutorial");
        Check(UnityEngine.Object.FindObjectsByType<PrelimTutorialSkipControl>(FindObjectsSortMode.None).Length==1,"one shared Skip Tutorial");
        if(skip){t.SkipControl.GetComponent<Button>().onClick.Invoke();Check(t.CurrentStep==InductionTutorialStep.Confirm,"Skip opens same confirmation");yield break;}
        yield return Guide(m,skipCatalogue);
    }
    public static IEnumerator Guide(InductionStageManager m,bool skipCatalogue=false)
    {
        var t=m.onboarding;float clock=m.timer.Remaining;
        yield return Settled(t);yield return Capture(m);m.ui.InteractButton.onClick.Invoke();Check(t.CurrentStep==InductionTutorialStep.Timer,"dedicated book practice");
        yield return Capture(m);
        t.NextButton.onClick.Invoke();Check(t.CurrentStep==InductionTutorialStep.Progress,"timer followed by separate progress lesson");
        t.NextButton.onClick.Invoke();yield return Settled(t);yield return Capture(m);GameInput.Instance.MobileInteract();
        Check(t.CurrentStep==InductionTutorialStep.SelectScroll&&m.ui.PracticeCatalogueOpen&&!m.BrowsingScrolls,"stand opens separate parchment practice catalogue");
        if(skipCatalogue){t.SkipControl.GetComponent<Button>().onClick.Invoke();yield return null;yield break;}
        yield return Capture(m);m.ui.PracticeChoice.onClick.Invoke();Check(t.CurrentStep==InductionTutorialStep.GetScroll,"selected exact practice scroll");yield return Capture(m);
        m.ui.GetScrollButton.onClick.Invoke();yield return Settled(t);
        Check(t.CurrentStep==InductionTutorialStep.BaseCase&&m.Held==null&&m.Tablets.All(s=>s.AtStand),"practice collection never changes inventory");
        yield return Capture(m);
        foreach(var step in new[]{InductionTutorialStep.Hypothesis,InductionTutorialStep.InductiveStep,InductionTutorialStep.AltarArrow,InductionTutorialStep.Place})
        {t.NextButton.onClick.Invoke();Check(t.CurrentStep==step,"altar lesson "+step);yield return null;}
        yield return Capture(m);m.ui.InteractButton.onClick.Invoke();Check(t.CurrentStep==InductionTutorialStep.Replace&&m.slots.All(s=>s.Tablet==null),"visual practice placement only");
        t.NextButton.onClick.Invoke();Check(t.CurrentStep==InductionTutorialStep.Rearrange,"rearrangement lesson");
        t.NextButton.onClick.Invoke();Check(t.CurrentStep==InductionTutorialStep.Submit,"submit explanation requires action");
        yield return Capture(m);m.ui.SubmitButton.onClick.Invoke();Check(t.CurrentStep==InductionTutorialStep.ViewBoard,"practice submission advances without grading");
        m.ui.ViewBoardButton.onClick.Invoke();yield return Settled(t);Check(t.CurrentStep==InductionTutorialStep.Board&&!m.boardView.Viewing&&!m.Reading,"practice board view separate from real modal");
        yield return Capture(m);t.NextButton.onClick.Invoke();Check(t.CurrentStep==InductionTutorialStep.Back,"Back highlighted");
        m.ui.BoardBackButton.onClick.Invoke();yield return Settled(t);yield return null;
        Check(t.CurrentStep==InductionTutorialStep.Confirm&&Time.timeScale==0&&GameInput.Instance.GameplayInputBlocked,"Back restores camera and opens confirmation");
        Check(m.State==InductionState.WaitingForBook&&m.timer.Remaining==clock&&!m.timer.Running&&m.Correct==0&&m.Mistakes==0&&m.slots.All(s=>s.Tablet==null),"extended tutorial preserves real gameplay");
        Check(!m.ui.PracticeCatalogueOpen&&!t.CameraTransitioning&&Camera.main.GetComponent<Unity.Cinemachine.CinemachineBrain>().enabled&&!m.boardTitle.text.Contains("PRACTICE"),"confirmation restores camera and board content");
    }
    static IEnumerator Settled(InductionControlsOnboarding t)
    {float until=Time.realtimeSinceStartup+4;while(t.CameraTransitioning&&Time.realtimeSinceStartup<until)yield return null;Check(!t.CameraTransitioning,"camera transition completes");}
    static IEnumerator Capture(InductionStageManager m)
    {
        if(!CaptureScreens)yield break;
        yield return new WaitForSecondsRealtime(.15f);
        System.IO.Directory.CreateDirectory("Temp/InductionQA");
        ScreenCapture.CaptureScreenshot("Temp/InductionQA/"+Screen.width+"x"+Screen.height+"_"+m.onboarding.CurrentStep+".png");
        yield return null;yield return null;
    }
    static void Drag(MobileInputUI mobile,Vector2 value)
    {
        var background=(RectTransform)typeof(MobileInputUI).GetField("joystickBackground",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(mobile);
        var screen=RectTransformUtility.WorldToScreenPoint(null,background.TransformPoint(new Vector3(background.sizeDelta.x*.5f*value.x,background.sizeDelta.y*.5f*value.y,0)));
        mobile.OnPointerDown(new PointerEventData(EventSystem.current){position=screen,pointerId=0});
    }
    static void Check(bool result,string message){if(!result)throw new InvalidOperationException("Induction onboarding check failed: "+message);}
}
