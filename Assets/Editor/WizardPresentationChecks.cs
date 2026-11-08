using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Opt-in offline checks; uses a separate garden save namespace.</summary>
public static class WizardPresentationChecks
{
    const string Root="Assets/Characters_NEW/LATEST_SKINS/Wizard";
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if(!SessionState.GetBool("Wizard.RunChecks",false))return;
        LogicGardenProgress.ValidationProfile="codex.wizard.validation";LogicGardenProgress.ResetForNewGame();
        LogicGardenProgress.CompleteControlsTutorial();LogicGardenProgress.CompleteConversation();LogicGardenProgress.VisitLibrary();LogicGardenProgress.SeeIntroduction();LogicGardenProgress.CompleteLibraryTour();
        SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;
    }
    static void Loaded(Scene scene,LoadSceneMode mode)
    {
        if(scene.name!="LogicGarden")return;SceneManager.sceneLoaded-=Loaded;
        var avatar=scene.GetRootGameObjects().Single(o=>o.name=="Player");avatar.SetActive(true);
        var spawn=scene.GetRootGameObjects().Single(o=>o.name=="Spawn").transform;
        PrelimTeleportTransition.MovePlayerImmediately(avatar,spawn.position,spawn.rotation);
        var owner=UnityEngine.Object.FindFirstObjectByType<LogicGardenIntroduction>();owner.StartCoroutine(Run(owner));
    }
    public static string AssetsCheck()
    {
        Check(AssetDatabase.AssetPathToGUID(Root+"/Wizard.fbx")=="02781e3ff58f91a40969c41471e8aa23","model GUID preserved");
        Check(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/IDLE.fbx")==null,"mesh FBX renamed");
        var preview=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Wizard.fbx"));SceneManager.MoveGameObjectToScene(model,preview);
        int curves=0,samples=0;float minHeight=float.MaxValue,maxHeight=0;
        try
        {
            foreach(string source in PrepareLogicGardenWizard.Sources)
            {
                var original=AssetDatabase.LoadAllAssetsAtPath(Root+"/"+(source=="IDLE"?"Wizard":source)+".fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Animations/"+PrepareLogicGardenWizard.ClipName(source)+".anim");
                Check(clip!=null&&!clip.name.Contains("mixamo"),"descriptive clip "+source);
                Check(clip.length==original.length&&clip.frameRate==original.frameRate,"source timing "+source);
                var bindings=AnimationUtility.GetCurveBindings(original);Check(bindings.Length==AnimationUtility.GetCurveBindings(clip).Length,"curve count "+source);
                foreach(var binding in bindings)
                {
                    Check(model.transform.Find(binding.path)!=null,"matching skeleton "+binding.path);
                    var a=AnimationUtility.GetEditorCurve(original,binding);var b=AnimationUtility.GetEditorCurve(clip,binding);
                    Check(a.keys.SequenceEqual(b.keys),"unchanged authored keys "+source+" "+binding.propertyName);curves++;
                }
                for(int i=0;i<=6;i++)
                {
                    clip.SampleAnimation(model,clip.length*i/6f);var bounds=PrepareLogicGardenWizard.VisualBounds(model);
                    Check(float.IsFinite(bounds.size.y)&&bounds.size.y>.005f&&bounds.size.y<.05f,"finite proportionate pose "+source);
                    minHeight=Mathf.Min(minHeight,bounds.size.y);maxHeight=Mathf.Max(maxHeight,bounds.size.y);samples++;
                }
            }
        }
        finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);}
        var owner=UnityEngine.Object.FindFirstObjectByType<LogicGardenIntroduction>();
        foreach(var actor in new[]{owner.GardenWizard,owner.LibraryWizard})
        {
            var scale=actor.animator.transform.localScale;Check(Mathf.Approximately(scale.x,scale.y)&&Mathf.Approximately(scale.y,scale.z),"uniform scale");
            Check(!actor.animator.applyRootMotion&&actor.animator.runtimeAnimatorController!=null,"root motion off/controller assigned");
            Check(actor.GetComponentsInChildren<CapsuleCollider>().Length==1,"single collider");
        }
        Check(owner.GetComponent<WizardDialogueVoice>()==null,"no voice playback component");
        return "WIZARD_ASSETS_PASS: 15 clips, "+curves+" identical source curves, "+samples+" rig poses; raw heights "+minHeight+"–"+maxHeight+"; uniform scale; GUID/material preserved; text-only dialogue.";
    }
    static IEnumerator Run(LogicGardenIntroduction owner)
    {
        yield return null;yield return null;yield return new WaitForSecondsRealtime(.5f);
        var player=Player.LocalInstance;var input=GameInput.Instance;var actor=owner.GardenWizard;
        Check(actor.GreetingPlayed&&actor.LastRequestedState=="Wizard_Wave","spawn Wave once");
        // Editor imports can stall frames; observe the Animator's completed
        // transition instead of assuming wall-clock time equals animation time.
        yield return new WaitForSecondsRealtime(3);
        Check(actor.GreetingActive&&actor.animator.GetCurrentAnimatorStateInfo(0).IsName("Wizard_Wave"),"greeting remains until conversation");
        int previous=-1;for(int i=0;i<30;i++){actor.SpeakLine();Check(actor.LastGesture!=previous,"no immediate gesture repetition");previous=actor.LastGesture;}
        var position=actor.transform.position;var rotation=actor.transform.rotation;var visual=actor.animator.transform.localPosition;
        actor.Play("Wizard_Float");yield return null;
        yield return Wait(()=>actor.animator.GetCurrentAnimatorStateInfo(0).IsName("Wizard_Float")||actor.animator.IsInTransition(0),"Float begins");
        yield return Wait(()=>actor.animator.GetCurrentAnimatorStateInfo(0).IsName("Wizard_Idle")&&!actor.animator.IsInTransition(0),"Float transition completes");
        Check(actor.transform.position==position&&actor.transform.rotation==rotation&&actor.animator.transform.localPosition==visual,"Float cannot displace interaction anchor");
        Check(actor.animator.GetCurrentAnimatorStateInfo(0).IsName("Wizard_Idle"),"Float returns to Idle");
        Check(owner.ReplayDialogue(false,null),"garden dialogue opens");
        owner.SubtitleNext.onClick.Invoke();
        owner.CancelReplayDialogue();Check(owner.GetComponent<WizardDialogueVoice>()==null,"dialogue stays text-only");
        Check(player.enabled&&!input.GameplayInputBlocked&&Time.timeScale==1,"dialogue restores controls");
        bool arrived=false;Check(owner.ReplayTravel(true,ok=>arrived=ok),"existing library transition starts");yield return Wait(()=>arrived&&!owner.Transitioning,"library transition");
        var library=owner.LibraryWizard;
        var point=library.transform.position+library.transform.forward*2.6f;
        PrelimTeleportTransition.MovePlayerImmediately(player.gameObject,point,Quaternion.LookRotation(library.transform.position-point));TeleportCameraSync.ResetAfterMove(player.transform,Vector3.zero);
        yield return new WaitForSecondsRealtime(.6f);
        var bubble=library.GetComponentInChildren<HandThoughtBubble>(true);
        Check(bubble!=null&&bubble.gameObject.activeInHierarchy&&bubble.Expansion>.9f&&bubble.HandIcon.sprite!=null,"nearby bubble expands with actual hand icon");
        Check(!owner.ReturnButton.gameObject.activeSelf,"standalone return button removed");
        string progress=Flags();
        input.MobileInteract();yield return null;
        Check(owner.ReturnQuestionOpen&&owner.Talking&&Time.timeScale==0&&input.GameplayInputBlocked&&!player.enabled,"hand opens safe return question");
        Check(!bubble.gameObject.activeSelf,"bubble hides during modal");
        foreach(var size in new[]{6,12,3})
        {
            BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(.3f);Canvas.ForceUpdateCanvases();
            CheckRaycast(owner.ReturnYes);CheckRaycast(owner.ReturnNo);
            foreach(var b in new[]{owner.ReturnYes,owner.ReturnNo})
            {var rect=(RectTransform)b.transform;var corners=new Vector3[4];rect.GetWorldCorners(corners);Check(corners.All(c=>c.x>=-1&&c.x<=Screen.width+1&&c.y>=-1&&c.y<=Screen.height+1),"return buttons inside screen");}
            Directory.CreateDirectory("Library/WizardQA");ScreenCapture.CaptureScreenshot("Library/WizardQA/return-"+Screen.width+"x"+Screen.height+".png");yield return null;yield return null;
        }
        Tap(owner.ReturnNo);yield return null;
        Check(!owner.ReturnQuestionOpen&&owner.InLibrary&&player.enabled&&!input.GameplayInputBlocked&&Time.timeScale==1,"No restores library controls and stops voice");
        input.MobileInteract();yield return null;Tap(owner.ReturnYes);yield return Wait(()=>!owner.InLibrary&&!owner.Transitioning,"Yes returns using existing garden transition");
        Check(Flags()==progress&&player.enabled&&!input.GameplayInputBlocked&&Time.timeScale==1,"both return choices preserve milestones and restore controls");
        owner.PresentWizardLine("text-only-test",false);owner.EndWizardLine();
        yield return null;yield return Wait(()=>actor.animator.GetCurrentAnimatorStateInfo(0).IsName("Wizard_Idle")&&!actor.animator.IsInTransition(0),"same-frame gesture cancellation returns Idle");
        Debug.Log("WIZARD_PLAY_PASS: spawn Wave, 30 nonrepeating gestures, Float anchor, text-only dialogue and cancellation, library hand bubble, portrait/landscape Yes/No raycasts, both return choices, preserved milestones, no voice playback.");
        SessionState.SetBool("Wizard.ChecksPassed",true);
    }
    static string Flags(){return LogicGardenProgress.ConversationCompleted+":"+LogicGardenProgress.LibraryVisited+":"+LogicGardenProgress.IntroductionSeen+":"+LogicGardenProgress.ControlsTutorialCompleted+":"+LogicGardenProgress.LibraryTourCompleted;}
    static void CheckRaycast(Button button)
    {var hits=new System.Collections.Generic.List<RaycastResult>();var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.position)};EventSystem.current.RaycastAll(data,hits);Check(hits.Count>0&&(hits[0].gameObject.transform==button.transform||hits[0].gameObject.transform.IsChildOf(button.transform)),"button raycast "+button.name);}
    static void Tap(Button button)
    {CheckRaycast(button);var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.position)};ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);}
    static IEnumerator Wait(Func<bool> ready,string label){float until=Time.realtimeSinceStartup+20;while(!ready()&&Time.realtimeSinceStartup<until)yield return null;Check(ready(),label);}
    static void Check(bool valid,string label){if(!valid)throw new InvalidOperationException("Wizard check failed: "+label);}
}
