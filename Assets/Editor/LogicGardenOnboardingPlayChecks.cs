using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Real input/physics checks with an isolated save and the scene's offline test avatar.</summary>
public static class LogicGardenOnboardingPlayChecks
{
    const string Profile="codex.gardenOnboarding.validation";
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if(!SessionState.GetBool("Garden.RunChecks",false))return;
        LogicGardenProgress.ValidationProfile=Profile;LogicGardenProgress.ResetForNewGame();
        if(SessionState.GetString("Garden.CheckMode","complete")=="returning")
        {LogicGardenProgress.CompleteConversation();LogicGardenProgress.VisitLibrary();LogicGardenProgress.SeeIntroduction();}
        if(SessionState.GetString("Garden.CheckMode","complete")=="library")
        {LogicGardenProgress.CompleteControlsTutorial();LogicGardenProgress.CompleteConversation();LogicGardenProgress.VisitLibrary();LogicGardenProgress.SeeIntroduction();}
        if(SessionState.GetString("Garden.CheckMode","complete")=="freshReplay")LogicGardenProgress.CompleteControlsTutorial();
        SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;
    }
    static void Loaded(Scene scene,LoadSceneMode mode)
    {
        if(scene.name!="LogicGarden")return;SceneManager.sceneLoaded-=Loaded;
        var intro=UnityEngine.Object.FindFirstObjectByType<LogicGardenIntroduction>();
        var avatar=scene.GetRootGameObjects().Single(o=>o.name=="Player");avatar.SetActive(true);
        var spawn=scene.GetRootGameObjects().Single(o=>o.name=="Spawn").transform;
        PrelimTeleportTransition.MovePlayerImmediately(avatar,spawn.position,spawn.rotation);
        intro.StartCoroutine(Run(intro));
    }
    static IEnumerator Run(LogicGardenIntroduction intro)
    {
        yield return null;yield return null;
        var mode=SessionState.GetString("Garden.CheckMode","complete");
        var view=Type.GetType("UnityEditor.GameView,UnityEditor");var window=EditorWindow.GetWindow(view);
        view.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(window,SessionState.GetInt("Garden.CheckSize",2),null);window.Focus();
        Crystal.SafeArea.Sim=SessionState.GetInt("Garden.CheckSize",2)==12?Crystal.SafeArea.SimDevice.Pixel3XL_LSR:SessionState.GetInt("Garden.CheckSize",2)==4?Crystal.SafeArea.SimDevice.iPhoneX:Crystal.SafeArea.SimDevice.None;
        var player=Player.LocalInstance;var input=GameInput.Instance;
        var look=player.GetComponentInChildren<CameraTargetController>();if(look!=null)look.SetViewAngles(18,0);
        yield return new WaitForSecondsRealtime(.8f);
        var t=intro.Onboarding;
        if(mode=="library"||mode=="freshReplay"){yield return LogicLibraryTourPlayChecks.Run(intro,mode=="freshReplay");yield break;}
        if(mode=="returning")
        {
            Check(!t.IsActive&&!t.IsPending&&!LogicGardenProgress.ControlsTutorialCompleted,"legacy completed conversation skips onboarding without needing the new flag");
            Check(Get<InteractPortal>(intro,"libraryDoor").InteractionUnlocked&&Get<TMPro.TMP_Text>(intro,"objectivesText").text.Contains("<s>Talk with Lefyahj."),"returning objective and library unlock preserved");
            Debug.Log("GARDEN_RETURNING_CHECKS_PASS: saved wizard/library milestones skip controls onboarding and keep the library unlocked.");yield break;
        }
        float deadline=Time.realtimeSinceStartup+5;while(!t.IsActive&&Time.realtimeSinceStartup<deadline)yield return null;
        Check(t.IsActive&&t.CurrentStep==LogicGardenControlsOnboarding.Step.Movement,"first spawn starts movement");
        var door=Get<InteractPortal>(intro,"libraryDoor");
        Check(!door.InteractionUnlocked&&!door.TryInteract(player.gameObject),"library remains locked");
        intro.Interact();input.MobileJump();input.MobileInteract();Check(!intro.Talking&&t.CurrentStep==LogicGardenControlsOnboarding.Step.Movement,"unrelated interactions blocked");
        yield return Capture(t,"Movement");
        var mobile=MobileInputUI.FindJoystick();Drag(mobile,new Vector2(.1f,0));yield return new WaitForSecondsRealtime(.2f);mobile.ResetJoystick();
        Check(t.CurrentStep==LogicGardenControlsOnboarding.Step.Movement,"dead zone ignored");
        Drag(mobile,new Vector2(.9f,0));yield return new WaitForSecondsRealtime(.12f);mobile.ResetJoystick();
        Check(t.CurrentStep==LogicGardenControlsOnboarding.Step.Movement,"brief nudge cannot complete movement");
        var start=player.transform.position;Drag(mobile,new Vector2(.9f,0));deadline=Time.realtimeSinceStartup+4;
        while(t.CurrentStep==LogicGardenControlsOnboarding.Step.Movement&&Time.realtimeSinceStartup<deadline)yield return null;
        Check(t.CurrentStep==LogicGardenControlsOnboarding.Step.Jump&&Vector2.Distance(new Vector2(start.x,start.z),new Vector2(player.transform.position.x,player.transform.position.z))>=t.movementDistance,"real intentional movement advances only to Jump");
        Check(t.Highlight.parent.name=="JumpButton","jump highlight follows actual control");yield return Capture(t,"Jump");
        var controller=player.GetComponent<CharacterController>();deadline=Time.realtimeSinceStartup+4;while(!controller.isGrounded&&Time.realtimeSinceStartup<deadline)yield return null;
        t.InstructionPanel.parent.GetComponentsInChildren<Button>(true).Single(b=>b.name=="JumpButton").onClick.Invoke();deadline=Time.realtimeSinceStartup+4;
        while(t.CurrentStep==LogicGardenControlsOnboarding.Step.Jump&&Time.realtimeSinceStartup<deadline)yield return null;
        Check(t.CurrentStep==LogicGardenControlsOnboarding.Step.Interaction&&player.IsJumping()&&t.PracticeObject!=null,"successful grounded jump advances to hand practice");
        Check(t.PracticeObject.GetComponent<GrabbableObject>()==null&&t.PracticeObject.GetComponent<Collider>().isTrigger,"practice target cannot enter inventory");yield return Capture(t,"Interaction");
        if(mode=="skipPractice"){t.SkipControl.GetComponent<Button>().onClick.Invoke();yield return null;CheckCleanup(intro);Debug.Log("GARDEN_SKIP_PRACTICE_CHECKS_PASS: practice target and highlights cleaned; wizard objective and locked library preserved.");yield break;}
        var direction=t.PracticeObject.transform.position-player.transform.position;direction.y=0;player.transform.rotation=Quaternion.LookRotation(-direction);
        input.MobileInteract();Check(t.CurrentStep==LogicGardenControlsOnboarding.Step.Interaction,"unsuccessful hand interaction cannot advance");
        player.transform.rotation=Quaternion.LookRotation(direction);t.InstructionPanel.parent.GetComponentsInChildren<Button>(true).Single(b=>b.name=="InteractButton").onClick.Invoke();
        Check(t.CurrentStep==LogicGardenControlsOnboarding.Step.MeetLefyahj&&t.PracticeObject==null&&!intro.Talking,"practice interaction opens only wizard preview");
        if(mode=="skipCamera"){t.SkipControl.GetComponent<Button>().onClick.Invoke();yield return null;CheckCleanup(intro);Debug.Log("GARDEN_SKIP_CAMERA_CHECKS_PASS: transition cancelled; camera and controls restored without completing dialogue.");yield break;}
        yield return Settled(t);yield return Capture(t,"MeetLefyahj");
        Check(UnityEngine.Object.FindObjectsByType<PrelimTutorialSkipControl>(FindObjectsSortMode.None).Length==1,"one shared Skip");
        Check(Get<GameObject>(intro,"wizardAttentionMarker").activeSelf&&!LogicGardenProgress.ConversationCompleted,"wizard attention remains until actual conversation");
        t.NextButton.onClick.Invoke();yield return Settled(t);yield return null;CheckCleanup(intro);
        var point=intro.Wizard.position+intro.Wizard.forward*3;Face(player,point,intro.Wizard.position+Vector3.up*2.8f);yield return null;
        Get<Button>(intro,"contextualButton").onClick.Invoke();Check(intro.Talking&&!door.InteractionUnlocked,"contextual talk starts dialogue without unlocking");
        intro.enabled=false;yield return null;intro.enabled=true;
        Check(!intro.Talking&&!LogicGardenProgress.ConversationCompleted&&Get<GameObject>(intro,"wizardAttentionMarker").activeSelf,"interrupted conversation preserves objective/attention and locked door");
        Face(player,point,intro.Wizard.position+Vector3.up*2.8f);yield return null;input.MobileInteract();
        Check(intro.Talking&&!door.InteractionUnlocked,"real hand also begins wizard dialogue");
        for(int i=0;i<4;i++){Get<Button>(intro,"nextButton").onClick.Invoke();Check(!door.InteractionUnlocked,"door locked before final acknowledgement");}
        yield return new WaitForSecondsRealtime(1.4f);yield return Capture(t,"WizardDialogue");
        Get<Button>(intro,"nextButton").onClick.Invoke();yield return null;
        Check(!intro.Talking&&door.InteractionUnlocked&&LogicGardenProgress.ConversationCompleted&&!Get<GameObject>(intro,"wizardAttentionMarker").activeSelf,"final acknowledgement unlocks and hides attention");
        Face(player,door.transform.position+Vector3.back*2,door.transform.position+Vector3.up*1.5f);yield return null;
        Get<Button>(intro,"contextualButton").onClick.Invoke();yield return new WaitForSecondsRealtime(2);
        Check(intro.InLibrary&&intro.Talking&&!t.IsActive&&Get<TMPro.TMP_Text>(intro,"locationTitle").text=="Logic Library","real transition starts eligible library intro without onboarding");
        for(int i=0;i<4;i++)Get<Button>(intro,"nextButton").onClick.Invoke();Check(LogicGardenProgress.IntroductionSeen,"library intro completes");
        yield return LogicLibraryTourPlayChecks.TraverseTour(intro,false);
        intro.ReturnToGarden();yield return new WaitForSecondsRealtime(2);
        Check(!intro.InLibrary&&!t.IsActive&&!t.IsPending&&door.InteractionUnlocked&&Get<TMPro.TMP_Text>(intro,"locationTitle").text=="LogicGarden","return keeps completed milestones and camera/control state");
        LogicGardenProgress.Load();Check(LogicGardenProgress.ControlsTutorialCompleted&&LogicGardenProgress.ConversationCompleted&&LogicGardenProgress.LibraryVisited,"milestones persisted");
        Debug.Log("GARDEN_ONBOARDING_CHECKS_PASS: actual joystick/jump/targeted hand practice, wizard camera/Next, unchanged wizard/library milestones during tutorial, both wizard controls, final-line unlock, library intro and return visit without replay.");
    }
    static void CheckCleanup(LogicGardenIntroduction intro)
    {
        var t=intro.Onboarding;Check(!t.IsActive&&LogicGardenControlsOnboarding.Active==null&&LogicGardenProgress.ControlsTutorialCompleted,"tutorial completion persisted");
        Check(!LogicGardenProgress.ConversationCompleted&&!LogicGardenProgress.LibraryVisited&&!Get<InteractPortal>(intro,"libraryDoor").InteractionUnlocked,"controls practice never unlocks library");
        Check(Player.LocalInstance.enabled&&!GameInput.Instance.GameplayInputBlocked&&Time.timeScale==1&&Camera.main.GetComponent<Unity.Cinemachine.CinemachineBrain>().enabled&&!t.CameraTransitioning,"controls and camera restored");
        Check(Get<TMPro.TMP_Text>(intro,"objectivesText").text=="Talk with Lefyahj."&&Get<GameObject>(intro,"wizardAttentionMarker").activeSelf,"Talk objective and marker retained");
        Check(!UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(x=>x.name=="Garden Practice Interaction"||x.name=="Garden Practice Label"||x.name=="Garden Tutorial Highlight"||x.name=="Garden Tutorial Instructions"||x.name=="PRELIM Shared Skip Tutorial"),"all practice content cleaned");
        intro.Onboarding.Initialize(intro,(RectTransform)Get<TMPro.TMP_Text>(intro,"locationTitle").transform.parent);
        Check(!intro.Onboarding.IsActive&&!intro.Onboarding.IsPending,"saved controls completion prevents replay even while wizard objective is pending");
    }
    static IEnumerator Settled(LogicGardenControlsOnboarding t){float until=Time.realtimeSinceStartup+4;while(t.CameraTransitioning&&Time.realtimeSinceStartup<until)yield return null;Check(!t.CameraTransitioning,"camera blend completed");}
    static IEnumerator Capture(LogicGardenControlsOnboarding t,string name)
    {
        if(!SessionState.GetBool("Garden.CaptureChecks",true))yield break;
        yield return new WaitForSecondsRealtime(.15f);System.IO.Directory.CreateDirectory("Temp/GardenQA");
        ScreenCapture.CaptureScreenshot("Temp/GardenQA/"+Screen.width+"x"+Screen.height+"_"+name+".png");yield return null;yield return null;
    }
    static void Face(Player player,Vector3 position,Vector3 target)
    {
        PrelimTeleportTransition.MovePlayerImmediately(player.gameObject,position,Quaternion.LookRotation(target-position));
        Vector3 direction=target-position;var look=player.GetComponentInChildren<CameraTargetController>();if(look!=null){look.enabled=true;look.SetViewAngles(18,Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg);}
        TeleportCameraSync.ResetAfterMove(player.transform,Vector3.zero);
    }
    static T Get<T>(LogicGardenIntroduction intro,string name)=> (T)typeof(LogicGardenIntroduction).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(intro);
    static void Drag(MobileInputUI mobile,Vector2 value)
    {
        var background=(RectTransform)typeof(MobileInputUI).GetField("joystickBackground",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(mobile);
        var screen=RectTransformUtility.WorldToScreenPoint(null,background.TransformPoint(new Vector3(background.sizeDelta.x*.5f*value.x,background.sizeDelta.y*.5f*value.y,0)));
        mobile.OnPointerDown(new PointerEventData(EventSystem.current){position=screen,pointerId=0});
    }
    static void Check(bool result,string message){if(!result)throw new InvalidOperationException("Garden onboarding check failed: "+message);}
}
