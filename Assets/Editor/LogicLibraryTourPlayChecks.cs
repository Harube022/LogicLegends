using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class LogicLibraryTourPlayChecks
{
    public static IEnumerator Run(LogicGardenIntroduction intro,bool freshReplay)
    {
        var tour=intro.LibraryTutorial;var door=Get<InteractPortal>(intro,"libraryDoor");
        if(freshReplay)
        {
            var original=Flags();tour.ReplayButton.onClick.Invoke();tour.LibraryOption.onClick.Invoke();yield return WaitFor(()=>intro.Talking,"library replay introduction");
            for(int i=0;i<4;i++)intro.SubtitleNext.onClick.Invoke();yield return TraverseTour(intro,true);
            Check(Flags()==original&&!door.InteractionUnlocked&&!intro.InLibrary,"replay from a fresh save returns to the garden without unlocking or completing objectives");
            Debug.Log("LIBRARY_FRESH_REPLAY_CHECKS_PASS: replay tour works without modifying incomplete wizard/library milestones or door lock.");yield break;
        }
        Face(Player.LocalInstance,door.transform.position+Vector3.back*2,door.transform.position+Vector3.up*1.5f);yield return new WaitForSecondsRealtime(.4f);intro.Interact();
        yield return WaitFor(()=>tour.TourActive,"automatic first eligible bookshelf tour");
        Check(intro.InLibrary&&Time.timeScale==0&&GameInput.Instance.GameplayInputBlocked&&!LogicGardenProgress.LibraryTourCompleted,"automatic tour pauses without marking completion early");
        string before=Flags();yield return TraverseTour(intro,true);Check(LogicGardenProgress.LibraryTourCompleted,"Yes persists first tour completion");
        var snapshot=Flags();
        tour.ReplayButton.onClick.Invoke();tour.LibraryOption.onClick.Invoke();Check(intro.Talking,"Library Tour starts with introduction");
        for(int i=0;i<4;i++)intro.SubtitleNext.onClick.Invoke();yield return TraverseTour(intro,false);Check(Flags()==snapshot,"library replay preserves every saved milestone");
        var shelf=UnityEngine.Object.FindObjectsByType<StudyShelfZone>(FindObjectsSortMode.None).Single(s=>s.Period=="PRELIM");
        Face(Player.LocalInstance,shelf.transform.position,shelf.transform.position+shelf.FocusCamera.transform.forward*4);yield return new WaitForSecondsRealtime(.3f);
        var prompt=DialogueManager.Instance.interactButton;var caption=prompt.GetComponentsInChildren<UnityEngine.UI.Text>(true).First().text;
        Check(caption=="Read","existing bookshelf interaction caption is Read");prompt.GetComponent<Button>().onClick.Invoke();yield return new WaitForSecondsRealtime(1.5f);
        var library=UnityEngine.Object.FindFirstObjectByType<StudyLibraryController>();Check(library.IsViewing(shelf),"dedicated Read uses the current shelf interaction");
        shelf.GetComponentsInChildren<Button>().First().onClick.Invoke();Check(library.IsReading,"selected book opens existing reading UI");library.ExitActiveShelf();
        tour.ReplayButton.onClick.Invoke();tour.GardenOption.onClick.Invoke();yield return WaitFor(()=>intro.Onboarding.IsActive,"Garden Tutorial replay");
        yield return GardenControls(intro);Check(intro.Talking,"garden replay continues to wizard introduction");
        for(int i=0;i<5;i++)intro.SubtitleNext.onClick.Invoke();Check(!tour.IsBusy&&!intro.Talking&&!intro.InLibrary&&Flags()==snapshot,"Garden Tutorial finishes without changing progress");
        tour.ReplayButton.onClick.Invoke();tour.FullOption.onClick.Invoke();yield return WaitFor(()=>intro.Onboarding.IsActive,"Full Tutorial garden controls");
        yield return GardenControls(intro);for(int i=0;i<5;i++)intro.SubtitleNext.onClick.Invoke();
        yield return WaitFor(()=>intro.InLibrary&&intro.Talking,"Full Tutorial transition then library introduction");
        for(int i=0;i<4;i++)intro.SubtitleNext.onClick.Invoke();yield return TraverseTour(intro,false);Check(Flags()==snapshot,"Full Tutorial preserves progress");
        tour.ReplayButton.onClick.Invoke();tour.LibraryOption.onClick.Invoke();for(int i=0;i<4;i++)intro.SubtitleNext.onClick.Invoke();
        Check(tour.CameraTransitioning,"skip check during shelf camera blend");tour.SkipControl.GetComponent<Button>().onClick.Invoke();yield return null;
        Check(!tour.IsBusy&&!tour.CameraTransitioning&&!GameInput.Instance.GameplayInputBlocked&&Time.timeScale==1&&Camera.main.GetComponent<Unity.Cinemachine.CinemachineBrain>().enabled,"Skip cancels transition and restores camera/input");
        Check(Flags()==snapshot&&UnityEngine.Object.FindObjectsByType<PrelimTutorialSkipControl>(FindObjectsSortMode.None).Length==0,"Skip cleans shared button and leaves progress unchanged");
        intro.ReturnToGarden();yield return WaitFor(()=>!intro.InLibrary&&!intro.Transitioning,"return after tour");
        Face(Player.LocalInstance,door.transform.position+Vector3.back*2,door.transform.position+Vector3.up*1.5f);yield return new WaitForSecondsRealtime(.4f);intro.Interact();
        yield return WaitFor(()=>intro.InLibrary&&!intro.Transitioning,"second library visit");Check(!intro.Talking&&!tour.IsBusy,"completed library tour does not repeat on return");
        Debug.Log("LIBRARY_TOUR_CHECKS_PASS: actual entrance, PRELIM/MIDTERMS/PRE-FINALS/FINALS framing and catalog content, No repeats intro/all shelves, Yes, Read/selected book, all replay choices, single Skip during camera blend, unchanged saved milestones and repeated travel.");
    }
    public static IEnumerator TraverseTour(LogicGardenIntroduction intro,bool repeat)
    {
        var tour=intro.LibraryTutorial;
        for(int pass=0;pass<(repeat?2:1);pass++)
        {
            for(int i=0;i<4;i++)
            {
                yield return WaitFor(()=>!tour.CameraTransitioning,"shelf camera settled");
                Check(tour.ShelfIndex==i&&intro.SubtitleUI.dialogueText.text==StudyLibraryCatalog.TourExplanation(new[]{"PRELIM","MIDTERMS","PRE-FINALS","FINALS"}[i]),"correct shelf and explanation "+i);
                Check(UnityEngine.Object.FindObjectsByType<PrelimTutorialSkipControl>(FindObjectsSortMode.None).Length==1,"one shared Skip throughout tour");
                Check(Time.timeScale==0&&GameInput.Instance.GameplayInputBlocked,"tour does not permit gameplay interactions");
                yield return Capture(new[]{"PRELIM","MIDTERMS","PRE-FINALS","FINALS"}[i]);intro.SubtitleNext.onClick.Invoke();
            }
            Check(tour.Confirming,"Did you get all that confirmation");yield return Capture("Confirmation");
            var hits=new System.Collections.Generic.List<RaycastResult>();
            var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,tour.SkipControl.transform.position)};
            EventSystem.current.RaycastAll(pointer,hits);
            Check(hits.Count>0&&(hits[0].gameObject.transform==tour.SkipControl.transform||hits[0].gameObject.transform.IsChildOf(tour.SkipControl.transform)),"shared Skip remains clickable above the confirmation shade");
            if(repeat&&pass==0)
            {
                tour.NoButton.onClick.Invoke();Check(intro.Talking&&intro.SubtitleUI.dialogueText.text=="Welcome to the Logic Library.","No repeats the full introduction");
                for(int i=0;i<4;i++)intro.SubtitleNext.onClick.Invoke();
            }
            else tour.YesButton.onClick.Invoke();
        }
        yield return WaitFor(()=>!tour.IsBusy,"tour/replay safely finished");yield return null;Check(!tour.TourActive&&!GameInput.Instance.GameplayInputBlocked&&Player.LocalInstance.enabled&&Time.timeScale==1,"Yes restores normal gameplay");
    }
    static IEnumerator GardenControls(LogicGardenIntroduction intro)
    {
        var t=intro.Onboarding;var mobile=MobileInputUI.FindJoystick();
        Drag(mobile,new Vector2(Player.LocalInstance.transform.position.x>4?-.9f:.9f,0));yield return WaitFor(()=>t.CurrentStep==LogicGardenControlsOnboarding.Step.Jump,"replayed movement");
        var cc=Player.LocalInstance.GetComponent<CharacterController>();yield return WaitFor(()=>cc.isGrounded,"grounded replay jump");GameInput.Instance.MobileJump();
        yield return WaitFor(()=>t.CurrentStep==LogicGardenControlsOnboarding.Step.Interaction,"replayed jump");
        GameInput.Instance.MobileInteract();
        yield return WaitFor(()=>!t.CameraTransitioning,"wizard preview");t.NextButton.onClick.Invoke();yield return WaitFor(()=>!t.IsActive,"replayed controls finished");
    }
    static IEnumerator Capture(string name)
    {
        yield return new WaitForSecondsRealtime(.15f);System.IO.Directory.CreateDirectory("Temp/LibraryQA");
        ScreenCapture.CaptureScreenshot("Temp/LibraryQA/"+Screen.width+"x"+Screen.height+"_"+name+".png");yield return null;yield return null;
    }
    static IEnumerator WaitFor(Func<bool> ready,string label)
    {float until=Time.realtimeSinceStartup+9;while(!ready()&&Time.realtimeSinceStartup<until)yield return null;Check(ready(),label);}
    static string Flags(){LogicGardenProgress.Load();return LogicGardenProgress.ConversationCompleted+":"+LogicGardenProgress.LibraryVisited+":"+LogicGardenProgress.IntroductionSeen+":"+LogicGardenProgress.ControlsTutorialCompleted+":"+LogicGardenProgress.LibraryTourCompleted;}
    static T Get<T>(LogicGardenIntroduction intro,string name)=>(T)typeof(LogicGardenIntroduction).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(intro);
    static void Face(Player player,Vector3 position,Vector3 target)
    {
        var dir=target-position;dir.y=0;PrelimTeleportTransition.MovePlayerImmediately(player.gameObject,position,Quaternion.LookRotation(dir));
        var look=player.GetComponentInChildren<CameraTargetController>();if(look!=null)look.SetViewAngles(18,Mathf.Atan2(dir.x,dir.z)*Mathf.Rad2Deg);TeleportCameraSync.ResetAfterMove(player.transform,Vector3.zero);
    }
    static void Drag(MobileInputUI mobile,Vector2 value)
    {
        var background=(RectTransform)typeof(MobileInputUI).GetField("joystickBackground",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(mobile);
        var screen=RectTransformUtility.WorldToScreenPoint(null,background.TransformPoint(new Vector3(background.sizeDelta.x*.5f*value.x,background.sizeDelta.y*.5f*value.y,0)));
        mobile.OnPointerDown(new PointerEventData(EventSystem.current){position=screen,pointerId=0});
    }
    static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException("Library tour check failed: "+message);}
}
