#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Cinematics.VFX;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.OpenArchiveTesting;
using Photon.Pun;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LogicLegends.Tests.Khezu
{
    public sealed class KhezuBarrierPlayChecks : MonoBehaviour
    {
        public const string Reports="Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Testing/KhezuBarrier/Reports";
        public string reportFolder=Reports;
        public bool eventSystemPointerClicks;
        [Serializable] public sealed class Check {public string name,detail;public bool passed;}
        [Serializable] public sealed class Report {
            public string checkedUtc,scope="Offline Editor: two real awakening/navigation captures, real two-second cinematic and crimson barrier, virtual Input System keyboard/mouse, existing mobile input, original CharacterController, health, UI and thirty-second nest sleep. Authorization and death/observer cases use explicitly identified fixtures. Live 2–4-client transport and native Android input/device performance are separate checks.";
            public bool passed;public List<Check> checks=new List<Check>();public List<string> errors=new List<string>();
        }
        readonly Report report=new Report();
        public string Stage {get;private set;}
        Player player;CharacterController capsule;KhezuHuntingController hunt;KhezuCaptureEncounterController encounter;KhezuBarrierDevelopmentUI ui;
        Keyboard keyboard;Mouse mouse;int jumps;GameObject otherRestriction;
        void Record(string name,bool pass,string detail=""){report.checks.Add(new Check{name=name,passed=pass,detail=detail});Save();}
        void Save(){Directory.CreateDirectory(reportFolder);File.WriteAllText(reportFolder+"/BarrierPlayMode.json",JsonUtility.ToJson(report,true));}
        void Log(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)report.errors.Add(message);}
        void Jumped(){jumps++;}
        public static void Begin(){if(!Application.isPlaying || PhotonNetwork.InRoom)throw new InvalidOperationException("Run offline Play Mode.");new GameObject("Khezu_Barrier_EndToEnd_Checks").AddComponent<KhezuBarrierPlayChecks>();}
        public static void Begin(string folder){if(!Application.isPlaying || PhotonNetwork.InRoom)throw new InvalidOperationException("Run offline Play Mode.");var checks=new GameObject("Monster_Barrier_EndToEnd_Checks").AddComponent<KhezuBarrierPlayChecks>();checks.reportFolder=folder;}
        public static void Begin(string folder,bool pointerClicks){if(!Application.isPlaying || PhotonNetwork.InRoom)throw new InvalidOperationException("Run offline Play Mode.");var checks=new GameObject("Monster_Barrier_EndToEnd_Checks").AddComponent<KhezuBarrierPlayChecks>();checks.reportFolder=folder;checks.eventSystemPointerClicks=pointerClicks;if(pointerClicks)checks.report.scope+=" Outcome clicks use raycast-validated Unity EventSystem pointer fixtures because the desktop Editor does not have OS input focus. Native OS mouse/touch input remains unverified.";}
        void Keys(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));}
        void Mobile(Vector3 direction){var f=Camera.main.transform.forward;f.y=0;f.Normalize();var r=Camera.main.transform.right;r.y=0;r.Normalize();direction.y=0;direction.Normalize();GameInput.Instance.SetMobileMovement(new Vector2(Vector3.Dot(direction,r),Vector3.Dot(direction,f)));}
        void Place(Vector3 point){
            if(player.IsMovementLocked)throw new InvalidOperationException("The test may not teleport a movement-locked player.");
            RaycastHit floor;if(!Physics.Raycast(point+Vector3.up*30,Vector3.down,out floor,70,~(1<<7),QueryTriggerInteraction.Ignore))throw new InvalidOperationException("Missing real ground.");
            point.y=floor.point.y-(capsule.center.y-capsule.height*.5f)+capsule.skinWidth;capsule.enabled=false;player.transform.position=point;capsule.enabled=true;player.ResetMotionAfterTeleport();
        }
        bool Hit(Button button){var pointer=new PointerEventData(EventSystem.current){position=ScreenPoint(button)};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);return hits.Count>0 && hits[0].gameObject==button.gameObject;}
        Vector2 ScreenPoint(Button b){var rect=b.GetComponent<RectTransform>();return RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));}
        IEnumerator Click(Button button){
            if(eventSystemPointerClicks){
                if(!Hit(button) || !button.IsInteractable())throw new InvalidOperationException("Outcome button is blocked or disabled.");
                var pointer=new PointerEventData(EventSystem.current){position=ScreenPoint(button),button=PointerEventData.InputButton.Left};
                ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerDownHandler);yield return null;
                ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);yield return new WaitForSecondsRealtime(.12f);yield break;
            }
            var point=ScreenPoint(button);InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(MouseButton.Left));yield return new WaitForSecondsRealtime(.06f);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return new WaitForSecondsRealtime(.12f);
        }
        bool ServerRequest(int actor,int serial,bool success){return (bool)typeof(KhezuCaptureEncounterController).GetMethod("AcceptDevelopmentOutcome",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(encounter,new object[]{actor,serial,success});}
        IEnumerator WaitForChallenge(){
            float end=Time.realtimeSinceStartup+22;bool gap=false;float displacement=0;
            while(!ui.Visible && Time.realtimeSinceStartup<end){
                if(hunt.State==KhezuHuntState.Caught){gap|=!player.IsMovementLocked;displacement=Mathf.Max(displacement,Vector3.Distance(player.transform.position,hunt.CapturePosition));}
                yield return null;
            }
            Record("Capture-to-barrier has no movement-unlocked frame",!gap && displacement<.015f,"maximum displacement="+displacement);
            Record("Actual capture opens the connected owner-only challenge UI",hunt.State==KhezuHuntState.Caught && encounter.Phase==KhezuEncounterPhase.BarrierChallenge && ui.Visible && player.IsMovementLocked);
            if(!ui.Visible)throw new InvalidOperationException("Challenge did not open.");
        }
        IEnumerator Start(){
            yield return new WaitForSecondsRealtime(.7f);report.checkedUtc=DateTime.UtcNow.ToString("o");
            player=Player.LocalInstance;capsule=player.GetComponent<CharacterController>();hunt=FindFirstObjectByType<KhezuHuntingController>();encounter=hunt.GetComponent<KhezuCaptureEncounterController>();ui=FindFirstObjectByType<KhezuBarrierDevelopmentUI>();
            var session=FindFirstObjectByType<OpenArchiveExplorationSession>();bool oldSession=session.enabled,oldBlock=GameInput.Instance.GameplayInputBlocked;float oldTime=Time.timeScale;
            var look=player.GetComponentInChildren<CameraTargetController>();var oldLook=look.transform.eulerAngles;
            keyboard=InputSystem.AddDevice<Keyboard>("KhezuBarrierTestKeyboard");mouse=InputSystem.AddDevice<Mouse>("KhezuBarrierTestMouse");
            player.OnJumpStarted+=Jumped;Application.logMessageReceived+=Log;
            try{
                session.enabled=false;Time.timeScale=1;GameInput.Instance.SetGameplayInputBlocked(false);GameInput.Instance.SetMobileMovement(Vector2.zero);EventSystem.current.SetSelectedGameObject(null);
                Stage="Real capture and cinematic";hunt.Request(KhezuCommand.Sleep);Place(new Vector3(30,0,27.5f));hunt.Request(KhezuCommand.ForceFourMistakes);
                yield return WaitForChallenge();yield return new WaitForSeconds(1.3f);
                var visual=hunt.GetComponent<KhezuBarrierVfxPresentation>();Record("Crimson barrier is active after cinematic completion",visual.ActiveBarrier!=null && !encounter.cinematic.IsPlaying);
                Record("Player, CharacterController, UI and networking remain enabled",player.enabled && capsule.enabled && player.gameObject.activeInHierarchy && EventSystem.current.enabled && EventSystem.current.currentInputModule!=null);
                Record("Movement-only restriction leaves shared input unblocked",!GameInput.Instance.GameplayInputBlocked);
                Stage="Locked input and movement attempts";var anchor=hunt.CapturePosition;int beforeJumps=jumps;
                Keys(Key.W,Key.LeftShift,Key.Space);GameInput.Instance.SetMobileMovement(Vector2.one);GameInput.Instance.MobileJump();player.OnRun();
                var target=new GameObject("Temporary_Guided_Movement_Target");target.transform.position=anchor+Vector3.forward*10;player.BeginGuidedMovement(target.transform,30);
                yield return new WaitForSeconds(3.3f);
                Record("WASD, held run input, jump and mobile joystick cannot move the player",Vector3.Distance(player.transform.position,anchor)<.005f && !player.IsWalking() && !player.IsRunning() && !player.IsJumping() && jumps==beforeJumps);
                Record("Movement actions are rejected before the motor",GameInput.Instance.GetMovementVectorNormalized()==Vector2.zero);
                capsule.Move(new Vector3(3,2,1));yield return new WaitForEndOfFrame();Record("Direct CharacterController movement is reconciled to the host anchor",Vector3.Distance(player.transform.position,anchor)<.005f);
                player.transform.position+=new Vector3(5,4,5);yield return new WaitForEndOfFrame();Record("External teleport/ability transform writes cannot escape",Vector3.Distance(player.transform.position,anchor)<.005f);
                foreach(var animator in player.GetComponentsInChildren<Animator>())animator.applyRootMotion=true;
                yield return new WaitForEndOfFrame();Record("Root motion cannot move the captured root",player.GetComponentsInChildren<Animator>().All(a=>!a.applyRootMotion) && Vector3.Distance(player.transform.position,anchor)<.005f);
                session.ReturnToSpawn();yield return null;Record("Return-to-spawn rejects movement during capture",Vector3.Distance(player.transform.position,anchor)<.005f);
                Object.Destroy(target);Keys();GameInput.Instance.SetMobileMovement(Vector2.zero);yield return null;
                var rotation=look.transform.rotation;look.SetViewAngles(18,45);yield return null;Record("Camera rotation remains available after the POV sequence",look.enabled && Quaternion.Angle(rotation,look.transform.rotation)>1 && player.IsMovementLocked);
                Record("SUCCESS and FAILED are touch-sized, wired and unobstructed",Hit(ui.successButton) && Hit(ui.failedButton) && ui.successButton.GetComponent<RectTransform>().rect.height>=80 && ui.successButton.onClick.GetPersistentEventCount()==1 && ui.failedButton.onClick.GetPersistentEventCount()==1);
                ScreenCapture.CaptureScreenshot(reportFolder+"/Barrier_Locked_Buttons.png");yield return new WaitForEndOfFrame();
                int serial=hunt.CaptureSerial,hp=hunt.health.CurrentHealth;
                Record("Host rejects wrong owner and stale challenge serial",!ServerRequest(999,serial,true) && !ServerRequest(-1,serial-1,false) && hunt.ResolvedSerial!=serial);
                encounter.developmentTestOutcomes=false;Record("Host rejects test results with development configuration disabled",!ServerRequest(-1,serial,true));encounter.developmentTestOutcomes=true;
                otherRestriction=new GameObject("Temporary_Other_Movement_Restriction");player.AcquireMovementLock(otherRestriction,"Other gameplay restriction",anchor,10);GameInput.Instance.SetGameplayInputBlocked(true);
                Stage="SUCCESS through real Input System mouse";yield return Click(ui.successButton);
                Record("SUCCESS mouse click reaches the authoritative outcome handler",hunt.BarrierResolving && hunt.ResolvedOutcome==1 && hunt.ResolvedSerial==serial);
                if(!hunt.BarrierResolving)throw new InvalidOperationException("Real mouse click did not submit SUCCESS.");
                Record("Duplicate and opposite outcomes are rejected",!ServerRequest(-1,serial,true) && !ServerRequest(-1,serial,false));
                yield return new WaitForSeconds(.25f);Record("Player stays locked throughout the barrier dissolve",player.IsMovementLocked && hunt.State==KhezuHuntState.Caught && !ui.Visible);
                float deadline=Time.realtimeSinceStartup+5;while(hunt.State==KhezuHuntState.Caught && Time.realtimeSinceStartup<deadline)yield return null;
                Record("SUCCESS releases only its own movement token",player.IsMovementLocked && GameInput.Instance.GameplayInputBlocked && hunt.State==KhezuHuntState.ReturningToNest);
                player.ReleaseMovementLock(otherRestriction);Object.Destroy(otherRestriction);otherRestriction=null;GameInput.Instance.SetGameplayInputBlocked(false);yield return null;
                Record("Valid player movement returns without enabling unrelated systems",!player.IsMovementLocked && player.enabled && capsule.enabled && hunt.health.CurrentHealth==hp);
                var start=player.transform.position;Keys(Key.W);yield return new WaitForSeconds(.45f);Keys();yield return null;
                Record("WASD works again after release",Vector3.Distance(player.transform.position,start)>2);
                start=player.transform.position;Mobile(new Vector3(1,0,1));yield return new WaitForSeconds(.45f);GameInput.Instance.SetMobileMovement(Vector2.zero);
                Record("Mobile movement works again after release",Vector3.Distance(player.transform.position,start)>2);
                Stage="Return to nest";deadline=Time.realtimeSinceStartup+20;while(hunt.State!=KhezuHuntState.NestSleeping && Time.realtimeSinceStartup<deadline)yield return null;
                Record("Khezu navigates back to the original nest before sleeping",hunt.State==KhezuHuntState.NestSleeping && Vector2.Distance(new Vector2(hunt.transform.position.x,hunt.transform.position.z),new Vector2(hunt.monsterSpawnPoint.position.x,hunt.monsterSpawnPoint.position.z))<1);
                Record("Success deducts no hearts and starts exactly thirty seconds at arrival",hunt.health.CurrentHealth==hp && Math.Abs(hunt.NestSleepEndsAt-hunt.NestArrivedAt-30)<.001);
                if(hunt.State!=KhezuHuntState.NestSleeping)throw new InvalidOperationException("Khezu did not reach the nest.");
                Stage="Real thirty-second nest sleep";int huntBefore=hunt.HuntSerial;double sleepEnd=hunt.NestSleepEndsAt;bool earlyHunt=false;
                while(KhezuHuntingController.EncounterClock<sleepEnd-.05){earlyHunt|=hunt.HuntSerial!=huntBefore || hunt.State!=KhezuHuntState.NestSleeping;yield return null;}
                Record("No hunt resumes before the thirty-second sleep completes",!earlyHunt);
                Stage="Full awakening after sleep";deadline=Time.realtimeSinceStartup+8;while(hunt.State!=KhezuHuntState.Hunting && Time.realtimeSinceStartup<deadline)yield return null;
                Record("Original Full_Awaken completes and hunting resumes",hunt.State==KhezuHuntState.Hunting && hunt.HuntSerial==huntBefore+1);
                Place(new Vector3(-30,0,-27.5f));Stage="Second real capture and FAILED";yield return WaitForChallenge();yield return new WaitForSeconds(1.2f);
                hp=hunt.health.CurrentHealth;serial=hunt.CaptureSerial;double previousSleepEnd=hunt.NestSleepEndsAt;
                yield return Click(ui.failedButton);Record("FAILED mouse click reaches the host exactly once",hunt.BarrierResolving && hunt.ResolvedOutcome==2 && hunt.ResolvedSerial==serial);
                if(!hunt.BarrierResolving)throw new InvalidOperationException("Real mouse click did not submit FAILED.");
                Record("FAILED duplicates cannot deduct extra hearts",!ServerRequest(-1,serial,false));
                deadline=Time.realtimeSinceStartup+4;while(hunt.State==KhezuHuntState.Caught && Time.realtimeSinceStartup<deadline)yield return null;
                Record("FAILED deducts exactly one existing HealthManager heart",hunt.health.CurrentHealth==hp-1 && hunt.health.ActorHearts(-1)==hp-1);
                // The state changes in Update; Animator evaluates a crossfade later in this frame.
                yield return new WaitForEndOfFrame();
                var attackAnimator=hunt.GetComponent<Animator>();
                Record("FAILED releases movement and plays the existing Attack",!player.IsMovementLocked && hunt.State==KhezuHuntState.AttackRecovery && !ui.Visible &&
                    (attackAnimator.GetNextAnimatorStateInfo(0).IsName("Attack") || attackAnimator.GetCurrentAnimatorStateInfo(0).IsName("Attack")),"animation="+hunt.GetSnapshot().animation);
                Place(session.SpawnPoint.position);var checkpoint=hunt.GetSnapshot();hunt.ApplySnapshot(checkpoint,true);hunt.AuthorityChanged();
                Record("Reapplying an outcome/health checkpoint cannot deduct twice",hunt.health.CurrentHealth==hp-1 && hunt.ResolvedSerial==serial);
                deadline=Time.realtimeSinceStartup+5;while(hunt.State!=KhezuHuntState.Hunting && Time.realtimeSinceStartup<deadline)yield return null;
                Record("FAILED resumes hunting without a new nest sleep timer",hunt.State==KhezuHuntState.Hunting && hunt.NestSleepEndsAt==previousSleepEnd);
                bool rotationPass=true;var random=new System.Random(17);
                for(int count=2;count<=4;count++){var rotationModel=new KhezuHuntRotation();var ids=Enumerable.Range(1,count).ToArray();int last=0;for(int n=0;n<100;n++){int selected=rotationModel.Select(ids,max=>random.Next(max),last);rotationPass&=selected!=last;last=selected;}}
                Record("Simulated 2/3/4-player no-repeat rotation never immediately reselects the failed actor",rotationPass,"Live multiple clients are not simulated by this roster test.");
                hunt.Request(KhezuCommand.Sleep);var fixture=hunt.GetSnapshot();fixture.state=(int)KhezuHuntState.Caught;fixture.caught=fixture.target=999;fixture.captureSerial=serial+1;fixture.encounterPhase=4;fixture.captureStartedAt=KhezuHuntingController.EncounterClock-20;fixture.capturePosition=player.transform.position;fixture.captureAnchorValid=true;
                hunt.ApplySnapshot(fixture,true);yield return new WaitForSeconds(.25f);Record("Observer fixture leaves the local bystander free and hides outcome UI",!player.IsMovementLocked && !ui.Visible);
                hunt.Request(KhezuCommand.Sleep);fixture=hunt.GetSnapshot();fixture.state=(int)KhezuHuntState.Caught;fixture.caught=fixture.target=-1;fixture.captureSerial=serial+2;fixture.encounterPhase=4;fixture.captureStartedAt=KhezuHuntingController.EncounterClock-20;fixture.capturePosition=player.transform.position;fixture.captureAnchorValid=true;fixture.awake=true;
                hunt.ApplySnapshot(fixture,true);yield return new WaitForSeconds(.2f);hunt.health.ApplyActorHealthSnapshot(new[]{new HealthManager.ActorHealth{actor=-1,hearts=0,lastPenaltySerial=serial+2}});yield return new WaitForSeconds(.3f);
                Record("Death interrupts the challenge without removing the health movement restriction",hunt.State!=KhezuHuntState.Caught && player.IsMovementLocked && !ui.Visible && hunt.health.CurrentHealth==0 && Time.timeScale==1);
                otherRestriction=new GameObject("Temporary_Restriction_During_Health_Restoration");player.AcquireMovementLock(otherRestriction,"Other gameplay restriction",player.transform.position,10);
                hunt.health.ApplyActorHealthSnapshot(new[]{new HealthManager.ActorHealth{actor=-1,hearts=1,lastPenaltySerial=serial+2}});yield return null;
                Record("Restoring health preserves an unrelated movement restriction",hunt.health.CurrentHealth==1 && player.IsMovementLocked);
                player.ReleaseMovementLock(otherRestriction);Object.Destroy(otherRestriction);otherRestriction=null;yield return null;
                Record("Restored health leaves no stale barrier or health movement token",!player.IsMovementLocked && !ui.Visible);
                Record("No runtime errors",report.errors.Count==0);
                Stage="Complete";
            }
            finally{
                if(otherRestriction!=null){player.ReleaseMovementLock(otherRestriction);Object.Destroy(otherRestriction);}GameInput.Instance.SetMobileMovement(Vector2.zero);GameInput.Instance.SetGameplayInputBlocked(oldBlock);
                hunt.Request(KhezuCommand.Sleep);session.enabled=oldSession;Time.timeScale=oldTime;look.SetViewAngles(oldLook.x,oldLook.y);player.OnJumpStarted-=Jumped;
                if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(mouse!=null)InputSystem.RemoveDevice(mouse);Application.logMessageReceived-=Log;
                report.passed=report.checks.Count>0 && report.checks.All(c=>c.passed) && report.errors.Count==0;Save();Debug.Log("Khezu Phase 3B: "+report.checks.Count(c=>c.passed)+"/"+report.checks.Count);Object.Destroy(gameObject);
            }
        }
    }
}
#endif
