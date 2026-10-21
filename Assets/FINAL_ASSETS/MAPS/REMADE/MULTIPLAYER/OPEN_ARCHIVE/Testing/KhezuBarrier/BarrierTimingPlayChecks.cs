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
using LogicLegends.Tests.Monsters;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

namespace LogicLegends.Tests.Khezu
{
    public sealed class BarrierTimingPlayChecks:MonoBehaviour
    {
        public const string Folder="Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Testing/KhezuBarrier/Reports";
        [Serializable] public sealed class Check{public string name,detail;public bool passed;}
        [Serializable] public sealed class Report{
            public string checkedUtc,scope="Offline Unity Editor. Both existing monster rigs, real awakening/navigation/capture/cinematic, FAILED submission, full five-second stun, shared existing player motor and animations. Timeout, success and collision cases additionally use explicitly aged checkpoint fixtures; no three-minute real-time wait or device run is implied.";
            public bool passed;public List<Check> checks=new List<Check>();public List<string> errors=new List<string>();
        }
        readonly Report report=new Report();
        public static string Status{get;private set;}="Idle";
        KhezuHuntingController hunt;KhezuCaptureEncounterController encounter;KhezuBarrierVfxPresentation vfx;
        Player player;CharacterController cc;DevelopmentMonsterSelection selection;Keyboard keyboard;
        GameObject restriction,wall,ledge;
        static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        void CheckThat(bool passed,string name,string detail=""){
            report.checks.Add(new Check{name=name,passed=passed,detail=detail});
            if(!passed)throw new InvalidOperationException(name+": "+detail);
        }
        void Log(string text,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)report.errors.Add(text);}
        public static void Begin(){if(!Application.isPlaying || PhotonNetwork.InRoom)throw new InvalidOperationException("Offline Play Mode required.");new GameObject("Barrier_Timer_And_Stun_QA").AddComponent<BarrierTimingPlayChecks>();}
        IEnumerator Wait(Func<bool> ready,string name,float seconds=25){float end=Time.realtimeSinceStartup+seconds;while(!ready()){if(Time.realtimeSinceStartup>end)throw new TimeoutException(name+"; "+hunt.Status);yield return null;}}
        void Place(Vector3 position){
            cc.enabled=false;player.transform.position=position;cc.enabled=true;player.ResetMotionAfterTeleport();Physics.SyncTransforms();
        }
        void Ground(Vector3 position){
            var hit=Physics.RaycastAll(position+Vector3.up*40,Vector3.down,90,~(1<<7),QueryTriggerInteraction.Ignore).Where(h=>!h.transform.IsChildOf(hunt.transform) && h.normal.y>.6f).OrderBy(h=>h.distance).First();
            position.y=hit.point.y-cc.center.y+cc.height*.5f+cc.skinWidth;Place(position);
        }
        void Fixture(float elapsedAfterCinematic){
            hunt.Request(KhezuCommand.Sleep);encounter.SynchronizeBarrierMovement();
            var s=hunt.GetSnapshot();s.state=(int)KhezuHuntState.Caught;s.caught=s.target=-1;s.captureSerial++;
            s.captureStartedAt=KhezuHuntingController.EncounterClock-KhezuCinematicJumpscareController.Duration-elapsedAfterCinematic;
            s.encounterPhase=4;s.capturePosition=player.transform.position;s.captureAnchorValid=true;s.awake=true;
            s.animation="Idle";s.normalizedTime=0;s.animationSpeed=1;
            hunt.ApplySnapshot(s,true);hunt.AuthorityChanged();
        }
        bool Submit(int actor,int serial,bool success)=>(bool)typeof(KhezuCaptureEncounterController).GetMethod("AcceptDevelopmentOutcome",Private).Invoke(encounter,new object[]{actor,serial,success});
        IEnumerator Start(){
            yield return new WaitForSecondsRealtime(.5f);report.checkedUtc=DateTime.UtcNow.ToString("o");
            hunt=FindFirstObjectByType<KhezuHuntingController>();encounter=hunt.GetComponent<KhezuCaptureEncounterController>();vfx=hunt.GetComponent<KhezuBarrierVfxPresentation>();
            player=Player.LocalInstance;cc=player.GetComponent<CharacterController>();selection=FindFirstObjectByType<DevelopmentMonsterSelection>();
            var oldSelection=selection.Selected;var oldPosition=player.transform.position;var oldRotation=player.transform.rotation;
            var session=FindFirstObjectByType<OpenArchiveExplorationSession>();bool oldSession=session.enabled,oldInput=GameInput.Instance.GameplayInputBlocked;
            bool oldOutcomes=encounter.developmentTestOutcomes;var oldHealth=hunt.health.ExportActorHealth();
            keyboard=InputSystem.AddDevice<Keyboard>("Barrier timing keyboard");Application.logMessageReceived+=Log;
            try{
                session.enabled=false;GameInput.Instance.SetGameplayInputBlocked(false);encounter.developmentTestOutcomes=true;
                foreach(var monster in new[]{DevelopmentMonster.Khezu,DevelopmentMonster.Rajang}){
                    Status=monster+": real capture";hunt.Request(KhezuCommand.Sleep);yield return null;selection.Select(monster);
                    hunt.SetPlayerEligibility(-1,true);Ground(new Vector3(30,0,27.5f));hunt.Request(KhezuCommand.ForceFourMistakes);
                    yield return Wait(()=>encounter.LocalChallengeActive,"Real capture and completed cinematic",35);
                    yield return new WaitForSecondsRealtime(.25f);
                    CheckThat(Math.Abs(hunt.BarrierDeadline-hunt.BarrierStartedAt-180)<.001 && Math.Abs(hunt.BarrierStartedAt-hunt.CaptureStartedAt-KhezuCinematicJumpscareController.Duration)<.001,monster+": 180 seconds starts after completed cinematic");
                    CheckThat(vfx.ActiveBarrier!=null && !encounter.cinematic.IsPlaying && player.IsMovementLocked,monster+": formed barrier, owner UI and continuous capture lock");
                    CheckThat(hunt.GetComponent<KhezuBarrierStatusUI>().Visible && hunt.GetComponent<KhezuBarrierStatusUI>().Message.StartsWith("BARRIER") && hunt.BarrierSecondsRemaining>178,monster+": visible owner countdown");
                    var palette=vfx.ActiveBarrier.style;
                    CheckThat(monster==DevelopmentMonster.Rajang?palette.scarlet.g>.15f:palette.scarlet.g<.25f,monster+": existing monster VFX palette retained",palette.name);
                    int hp=hunt.health.ActorHearts(-1),serial=hunt.CaptureSerial;Vector3 anchor=hunt.CapturePosition;
                    CheckThat(!Submit(999,serial,false) && !Submit(-1,serial-1,false),monster+": wrong owner and stale serial rejected");
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.LeftShift,Key.Space));GameInput.Instance.SetMobileMovement(Vector2.one);GameInput.Instance.MobileJump();
                    CheckThat(encounter.SubmitDevelopmentOutcome(false),monster+": FAILED accepted through existing owner command");
                    CheckThat(!Submit(-1,serial,false) && !Submit(-1,serial,true),monster+": duplicate/conflicting results rejected");
                    bool held=true,attack=false;double hitAt=hunt.AttackHitAt;
                    while(!hunt.FailureHitApplied){
                        held&=player.IsMovementLocked && Vector3.Distance(player.transform.position,anchor)<.01f && vfx.ActiveBarrier!=null && !vfx.ActiveBarrier.IsDissolving && hunt.health.ActorHearts(-1)==hp;
                        attack|=hunt.GetSnapshot().animation=="Attack";yield return null;
                    }
                    CheckThat(held && attack && KhezuHuntingController.EncounterClock>=hitAt,monster+": attack plays while barrier holds, with no early damage or movement");
                    yield return new WaitForEndOfFrame();
                    CheckThat(hunt.health.ActorHearts(-1)==hp-1 && hunt.FailureStunActive && player.IsMovementLocked,monster+": contact deducts one heart and transfers directly to stun");
                    CheckThat(Math.Abs(hunt.StunEndsAt-hunt.AttackHitAt-5)<.001 && vfx.ActiveBarrier==null,monster+": barrier dissolves at contact and stun lasts exactly five seconds");
                    yield return new WaitForSecondsRealtime(.65f);
                    CheckThat(hunt.KnockbackPath.Length>1 && Vector3.Distance(player.transform.position,anchor)>.5f && Vector3.Distance(player.transform.position,anchor)<=4.05f && Vector3.Distance(player.transform.position,hunt.KnockbackPath.Last())<.02f,monster+": visible smooth knockback reaches safe ground",string.Join(";",hunt.KnockbackPath.Select(p=>p.ToString())));
                    var skins=player.GetComponentsInChildren<Animator>().Where(a=>a.parameters.Any(p=>p.name=="IsStunned")).ToArray();
                    CheckThat(skins.Length>0 && skins.All(a=>a.GetBool("IsStunned") && a.GetCurrentAnimatorStateInfo(0).loop && a.GetCurrentAnimatorClipInfo(0).Any(c=>c.clip.name.IndexOf("stun",StringComparison.OrdinalIgnoreCase)>=0 || c.clip.name.Contains("ADHD"))),monster+": existing rig-compatible looping dizzy animation plays");
                    var checkpoint=hunt.GetSnapshot();double ends=hunt.StunEndsAt;hunt.ApplySnapshot(checkpoint,true);hunt.AuthorityChanged();
                    CheckThat(hunt.StunEndsAt==ends && hunt.health.ActorHearts(-1)==hp-1,monster+": checkpoint resumes remaining stun without repeated damage");
                    hunt.SetPlayerEligibility(-1,false);Status=monster+": five-second stun";
                    bool locked=true;while(KhezuHuntingController.EncounterClock<ends-.04){locked&=player.IsMovementLocked;yield return null;}
                    CheckThat(locked,monster+": held desktop/mobile movement and jump cannot escape stun");
                    restriction=new GameObject("Other movement restriction");player.AcquireMovementLock(restriction,"Unrelated owner",player.transform.position,10);
                    yield return new WaitForSecondsRealtime(.15f);
                    CheckThat(player.IsMovementLocked && skins.All(a=>!a.GetBool("IsStunned")),monster+": recovery releases only encounter stun, retaining unrelated restriction");
                    player.ReleaseMovementLock(restriction);Destroy(restriction);restriction=null;
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());GameInput.Instance.SetMobileMovement(Vector2.zero);
                    yield return null;CheckThat(!player.IsMovementLocked && !GameInput.Instance.GameplayInputBlocked,monster+": controls return after recovery without globally blocking input");
                    hunt.Request(KhezuCommand.Sleep);yield return null;
                    Status=monster+": automatic timeout";Ground(oldPosition);Fixture(179.5f);encounter.developmentTestOutcomes=false;
                    yield return null;CheckThat(hunt.BarrierSecondsRemaining>0 && hunt.BarrierSecondsRemaining<.6f && !hunt.BarrierResolving,monster+": checkpoint retains remaining deadline");
                    yield return Wait(()=>hunt.BarrierResolving,"Automatic timeout",2);
                    CheckThat(hunt.BarrierTimedOut && hunt.ResolvedOutcome==2,monster+": timeout fails without development buttons or a client request");
                    yield return Wait(()=>hunt.FailureHitApplied,"Timeout attack hit",3);
                    CheckThat(hunt.health.ActorHearts(-1)==hp-2,monster+": timeout follows same single-heart hit path");
                    hunt.Request(KhezuCommand.Sleep);yield return null;
                    CheckThat(!player.IsMovementLocked && skins.All(a=>!a.GetBool("IsStunned")),monster+": reset cancels every encounter token");
                    hunt.health.ApplyActorHealthSnapshot(new[]{new HealthManager.ActorHealth{actor=-1,hearts=3,lastPenaltySerial=hunt.CaptureSerial}});
                    encounter.developmentTestOutcomes=true;
                    Fixture(181);var expired=hunt.GetSnapshot();expired.barrierTimerSerial=expired.captureSerial;expired.barrierStartedAt=expired.captureStartedAt+KhezuCinematicJumpscareController.Duration;expired.barrierDeadline=expired.barrierStartedAt+180;
                    hunt.ApplySnapshot(expired,true);
                    CheckThat(!hunt.ResolveBarrierOutcome(-1,hunt.CaptureSerial,true,.9f),monster+": a late SUCCESS cannot beat the host deadline");
                    hunt.Request(KhezuCommand.Sleep);yield return null;Fixture(0);yield return null;hp=hunt.health.ActorHearts(-1);
                    CheckThat(encounter.SubmitDevelopmentOutcome(true),monster+": SUCCESS still accepted before expiry");
                    yield return Wait(()=>hunt.State!=KhezuHuntState.Caught,"Success dissolve",3);
                    CheckThat(!player.IsMovementLocked && hunt.health.ActorHearts(-1)==hp && hunt.State==KhezuHuntState.ReturningToNest,monster+": success releases undamaged and preserves return-to-nest flow");
                    hunt.Request(KhezuCommand.Sleep);yield return null;
                }
                Status="Knockback collision fixtures";Ground(oldPosition);Fixture(0);yield return null;
                Vector3 start=hunt.CapturePosition,dir=start-hunt.transform.position;dir.y=0;dir.Normalize();
                wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="QA_knockback_wall";wall.transform.position=start+dir*2+Vector3.up*cc.center.y;wall.transform.rotation=Quaternion.LookRotation(dir);wall.transform.localScale=new Vector3(8,cc.height+4,.3f);Physics.SyncTransforms();
                var path=(Vector3[])typeof(KhezuHuntingController).GetMethod("BuildFailureKnockback",Private).Invoke(hunt,new object[]{-1});
                CheckThat(Vector3.Distance(path.Last(),start)<1.8f,"Knockback stops before a real solid wall");Destroy(wall);wall=null;hunt.Request(KhezuCommand.Sleep);yield return null;
                ledge=GameObject.CreatePrimitive(PrimitiveType.Cube);ledge.name="QA_knockback_ledge";ledge.transform.position=new Vector3(1000,200,1000);ledge.transform.localScale=new Vector3(5,1,5);
                Place(new Vector3(1000,200.5f-cc.center.y+cc.height*.5f+cc.skinWidth,1000));Fixture(0);Physics.SyncTransforms();
                path=(Vector3[])typeof(KhezuHuntingController).GetMethod("BuildFailureKnockback",Private).Invoke(hunt,new object[]{-1});
                var endpoint=path.Last()-ledge.transform.position;float margin=cc.radius*.9f;
                CheckThat(Mathf.Abs(endpoint.x)<=2.5f-margin+.02f && Mathf.Abs(endpoint.z)<=2.5f-margin+.02f,"Knockback stops with capsule support before an exposed ledge",path.Last().ToString());
                CheckThat(report.errors.Count==0,"No Unity runtime errors");Status="Passed";
            }
            finally{
                hunt.Request(KhezuCommand.Sleep);encounter.SynchronizeBarrierMovement();
                if(restriction!=null){player.ReleaseMovementLock(restriction);Destroy(restriction);}if(wall!=null)Destroy(wall);if(ledge!=null)Destroy(ledge);
                hunt.SetPlayerEligibility(-1,true);selection.Select(oldSelection);encounter.developmentTestOutcomes=oldOutcomes;session.enabled=oldSession;
                hunt.health.ApplyActorHealthSnapshot(oldHealth);Place(oldPosition);player.transform.rotation=oldRotation;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());GameInput.Instance.SetMobileMovement(Vector2.zero);GameInput.Instance.SetGameplayInputBlocked(oldInput);InputSystem.RemoveDevice(keyboard);
                Application.logMessageReceived-=Log;report.passed=Status=="Passed" && report.errors.Count==0 && report.checks.All(c=>c.passed);
                if(!report.passed)Status="Failed";Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/BarrierTimerAndStun.json",JsonUtility.ToJson(report,true));
                Debug.Log("Barrier timer and stun: "+Status+", "+report.checks.Count+" checks");Destroy(gameObject);
            }
        }
    }
}
#endif
