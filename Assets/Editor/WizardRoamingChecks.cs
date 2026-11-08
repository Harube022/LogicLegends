using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Opt-in offline checks. Never uses a real account's garden milestones.</summary>
public static class WizardRoamingChecks
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if(!SessionState.GetBool("Wizard.RoamingChecks",false))return;
        Application.runInBackground=true;
        LogicGardenProgress.ValidationProfile="codex.wizardRoaming.validation";LogicGardenProgress.ResetForNewGame();
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
    static IEnumerator Run(LogicGardenIntroduction owner)
    {
        yield return null;yield return new WaitForSecondsRealtime(4);
        var garden=owner.GardenWizard;var library=owner.LibraryWizard;var roaming=library.GetComponent<WizardLibraryRoaming>();
        var player=Player.LocalInstance;var input=GameInput.Instance;
        Check(garden.GreetingActive&&garden.animator.GetCurrentAnimatorStateInfo(0).IsName("Wizard_Wave"),"greeting persists beyond one clip");
        var anchor=owner.Wizard.position;Check(owner.ReplayDialogue(false,null),"garden text dialogue");
        Check(!garden.GreetingActive&&owner.Talking,"talk ends greeting");owner.SubtitleNext.onClick.Invoke();owner.CancelReplayDialogue();
        yield return Wait(()=>garden.animator.GetCurrentAnimatorStateInfo(0).IsName("Wizard_Idle")&&!garden.animator.IsInTransition(0),"conversation ends at Idle");
        Check(owner.Wizard.position==anchor&&owner.GetComponent<WizardDialogueVoice>()==null,"stable anchor and no voice component");
        var sample=new System.Random(12345);int rare=0;for(int i=0;i<10000;i++)rare+=WizardLibraryRoaming.ChooseWalkingVariant(sample.NextDouble());
        Check(rare>940&&rare<1060&&WizardLibraryRoaming.ChooseWalkingVariant(.89999)==0&&WizardLibraryRoaming.ChooseWalkingVariant(.9)==1,"90/10 probability boundary");
        bool arrived=false;Check(owner.ReplayTravel(true,ok=>arrived=ok),"library travel");yield return Wait(()=>arrived&&!roaming.Paused&&roaming.agent.isOnNavMesh,"library navigation ready");
        int count=roaming.SegmentCount;Check(!roaming.TryDestination(new Vector3(0,0,0))&&roaming.SegmentCount==count,"outside destination rejected without reroll");
        Check(!roaming.TryDestination(new Vector3(-113.06f,21.1f,667.28f)),"plant destination rejected");
        int attempts=0,startCount=roaming.SegmentCount,startRare=roaming.Walking1Segments;
        while(attempts++<200)Check(roaming.TryDestination(new Vector3(-120,21.1f,682)),"complete interior path");
        int actualRare=roaming.Walking1Segments-startRare;Check(actualRare>=5&&actualRare<=40&&roaming.SegmentCount-startCount==200,"one weighted choice per accepted segment");
        Vector3 previous=library.transform.position;float distance=0;float until=Time.realtimeSinceStartup+7;int segment=roaming.SegmentCount,variant=roaming.CurrentVariant;
        while(Time.realtimeSinceStartup<until)
        {
            yield return null;
            Check(roaming.Inside(library.transform.position),"wizard remains inside library");
            if(segment==roaming.SegmentCount)Check(variant==roaming.CurrentVariant,"no per-frame reroll");else{segment=roaming.SegmentCount;variant=roaming.CurrentVariant;}
            distance+=Vector3.Distance(previous,library.transform.position);previous=library.transform.position;
        }
        Check(distance>3,"wizard actually walks");
        var hips=library.GetComponentsInChildren<Transform>().Single(t=>t.name=="mixamorig:Hips");
        float home=(float)typeof(WizardActor).GetField("hipsHomeZ",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(library);
        float maxRootError=0;
        for(int walking=0;walking<2;walking++)
        {
            var target=new Vector3(library.transform.position.x<-126?-115:-138,21.1f,680);
            int tries=0;do{Check(roaming.TryDestination(target),"walk-variant route");}while(roaming.CurrentVariant!=walking&&tries++<100);
            Check(roaming.CurrentVariant==walking,"both walking choices available");
            string state=walking==0?"Wizard_Walking":"Wizard_Walking1";
            yield return Wait(()=>library.animator.GetCurrentAnimatorStateInfo(0).IsName(state)&&!library.animator.IsInTransition(0),"walk blend settled");
            float end=Time.realtimeSinceStartup+3;
            while(Time.realtimeSinceStartup<end)
            {
                yield return null;float error=Mathf.Abs(hips.localPosition.z-home);maxRootError=Mathf.Max(maxRootError,error);
                Check(error<.00005f,"authored forward travel consumed without pose drift");
            }
            float expected=roaming.movementSpeed/(walking==0?library.walkingNaturalSpeed:library.walking1NaturalSpeed);
            Check(Mathf.Abs(library.animator.GetFloat("WalkPlayback")-expected)<.15f,"walking playback matches actual travel speed");
        }
        // Place a temporary solid obstruction in a valid route, then remove it.
        Check(roaming.TryDestination(new Vector3(library.transform.position.x<-126?-115:-138,21.1f,682)),"obstacle example path");yield return null;
        var direction=roaming.agent.steeringTarget-library.transform.position;direction.y=0;direction.Normalize();
        var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);obstacle.name="Wizard QA Temporary Obstacle";
        obstacle.transform.position=library.transform.position+direction*3+Vector3.up*2.5f;obstacle.transform.localScale=new Vector3(2,5,2);Physics.SyncTransforms();
        int blocked=roaming.BlockedSegments;yield return Wait(()=>roaming.BlockedSegments>blocked,"dynamic obstruction stops/replans");UnityEngine.Object.Destroy(obstacle);yield return null;
        // An interaction must stop this same moving agent immediately.
        var point=library.transform.position+library.transform.right*2.7f;
        PrelimTeleportTransition.MovePlayerImmediately(player.gameObject,point,Quaternion.LookRotation(library.transform.position-point));TeleportCameraSync.ResetAfterMove(player.transform,Vector3.zero);
        yield return null;input.MobileInteract();yield return null;
        Check(owner.ReturnQuestionOpen&&roaming.Paused&&roaming.agent.velocity.sqrMagnitude<.001f,"hand stops roaming before opening return dialogue");
        Vector3 stopped=library.transform.position;yield return new WaitForSecondsRealtime(.8f);
        Check(library.transform.position==stopped&&library.animator.GetCurrentAnimatorStateInfo(0).IsName("Wizard_Idle"),"dialogue stays still in Idle");
        owner.ReturnNo.onClick.Invoke();yield return Wait(()=>!roaming.Paused&&roaming.agent.hasPath,"No resumes roaming");
        var main=Camera.main;var brain=main.GetComponent<Unity.Cinemachine.CinemachineBrain>();bool brainEnabled=brain.enabled;
        Vector3 cameraPosition=main.transform.position;Quaternion cameraRotation=main.transform.rotation;float time=Time.timeScale;
        brain.enabled=false;Time.timeScale=0;
        PrelimTeleportTransition.MovePlayerImmediately(player.gameObject,library.transform.position+Vector3.right*2.7f,Quaternion.identity);
        var bubble=library.GetComponentInChildren<HandThoughtBubble>(true);float largest=0;
        foreach(int size in new[]{6,12,3})
        {
            BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(.35f);
            foreach(float depth in new[]{8f,14f,22f})
            {
                var target=library.transform.position+Vector3.up*(library.standingHeight+1.55f);
                main.transform.SetPositionAndRotation(target-Vector3.forward*depth,Quaternion.identity);
                yield return new WaitForSecondsRealtime(.4f);
                Check(bubble.gameObject.activeInHierarchy&&bubble.Expansion>.9f,"nearby hand prompt visible");
                var corners=new Vector3[4];bubble.Rect.GetWorldCorners(corners);var a=main.WorldToScreenPoint(corners[0]);var b=main.WorldToScreenPoint(corners[2]);
                float width=Mathf.Abs(b.x-a.x),height=Mathf.Abs(b.y-a.y);largest=Mathf.Max(largest,width);
                Check(width<=362&&height<=84&&width<=Screen.safeArea.width*.66f,"bounded mobile bubble at camera distance "+depth);
            }
            Directory.CreateDirectory("Library/WizardQA");ScreenCapture.CaptureScreenshot("Library/WizardQA/roaming-bubble-"+Screen.width+"x"+Screen.height+".png");yield return null;yield return null;
        }
        var far=library.transform.position+Vector3.up*(library.standingHeight+1.55f)-Vector3.forward*36;main.transform.position=far;
        yield return null;Check(!bubble.gameObject.activeSelf,"indicator hidden beyond visibility distance");
        main.transform.SetPositionAndRotation(cameraPosition,cameraRotation);brain.enabled=brainEnabled;Time.timeScale=time;
        PrelimTeleportTransition.MovePlayerImmediately(player.gameObject,library.transform.position+library.transform.right*2.7f,Quaternion.identity);yield return null;
        input.MobileInteract();yield return null;Check(owner.ReturnQuestionOpen,"return dialogue reopens");owner.ReturnYes.onClick.Invoke();
        yield return Wait(()=>!owner.InLibrary&&!owner.Transitioning,"Yes returns to garden");
        Check(roaming.Paused&&player.enabled&&!input.GameplayInputBlocked&&LogicGardenProgress.LibraryTourCompleted,"garden return preserves progress and pauses library actor");
        SessionState.SetBool("Wizard.RoamingPassed",true);
        Debug.Log("WIZARD_ROAMING_PASS: sustained greeting/text-only dialogue; 90/10 selector "+rare+"/10000 and "+actualRare+"/200 actual segments; both walk speeds/root compensation (max local Z error "+maxRootError+"); interior paths, blocked/outside destinations, dynamic obstacle, dialogue interrupt/Idle/resume, Yes/No, bubble cap "+largest+"px at three mobile sizes and 8/14/22-unit camera distances; visibility cutoff and preserved progress.");
    }
    static IEnumerator Wait(Func<bool> condition,string label){float until=Time.realtimeSinceStartup+20;while(!condition()&&Time.realtimeSinceStartup<until)yield return null;Check(condition(),label);}
    static void Check(bool valid,string label){if(!valid)throw new InvalidOperationException("Wizard roaming check failed: "+label);}
}
