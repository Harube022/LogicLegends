#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Cinematics.VFX;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.OpenArchiveTesting;
using Photon.Pun;
using UnityEngine;

namespace LogicLegends.Tests.Khezu
{
    public sealed class CrimsonBarrierPlayChecks : MonoBehaviour
    {
        const string Reports="Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Testing/CrimsonBarrier/Reports";
        [Serializable] sealed class Check {public string name,detail;public bool passed;}
        [Serializable] sealed class Report {
            public string checkedUtc,scope="Offline Editor snapshot fixtures through the existing encounter adapter and real two-second cinematic. Tests VFX timing, deduplication, late checkpoints, animation, release fade, limits and cleanup. Phase 3B movement/outcome verification is recorded separately in Testing/KhezuBarrier/Reports; these visual checks do not establish live multiplayer or Android performance.";
            public bool passed;public List<Check> checks=new List<Check>();public List<string> runtimeErrors=new List<string>();
        }
        readonly Report report=new Report();
        KhezuHuntingController hunting;KhezuCaptureEncounterController encounter;KhezuBarrierVfxPresentation presenter;
        void Logged(string condition,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert || (type==LogType.Warning && condition.Contains("Particle Velocity")))report.runtimeErrors.Add(condition);}
        void Record(string name,bool passed,string detail=""){report.checks.Add(new Check{name=name,passed=passed,detail=detail});}
        public static void Begin(){if(!Application.isPlaying || PhotonNetwork.InRoom)throw new InvalidOperationException("Use offline Play Mode.");new GameObject("Crimson_Barrier_Editor_Checks").AddComponent<CrimsonBarrierPlayChecks>();}
        void Capture(int actor,int serial,float age){
            var snapshot=hunting.GetSnapshot();snapshot.state=(int)KhezuHuntState.Caught;snapshot.target=actor;snapshot.caught=actor;snapshot.captureSerial=serial;snapshot.huntSerial=1;
            snapshot.mistakes=4;snapshot.stage=4;snapshot.awake=true;snapshot.pending=false;snapshot.encounterPhase=age>=2?4:1;snapshot.animation="Idle";snapshot.animationSpeed=1;snapshot.captureStartedAt=KhezuHuntingController.EncounterClock-age;
            snapshot.capturePosition=Player.LocalInstance.transform.position;snapshot.captureAnchorValid=true;
            hunting.ApplySnapshot(snapshot,true);
        }
        IEnumerator Start(){
            yield return new WaitForSecondsRealtime(.6f);
            hunting=FindFirstObjectByType<KhezuHuntingController>();encounter=hunting.GetComponent<KhezuCaptureEncounterController>();presenter=hunting.GetComponent<KhezuBarrierVfxPresentation>();
            var player=Player.LocalInstance;var session=FindFirstObjectByType<OpenArchiveExplorationSession>();bool oldSession=session.enabled;bool oldBlock=GameInput.Instance.GameplayInputBlocked;
            var position=hunting.transform.position;var rotation=hunting.transform.rotation;report.checkedUtc=DateTime.UtcNow.ToString("o");int serial=hunting.CaptureSerial+1;
            try{
                Application.logMessageReceived+=Logged;
                session.enabled=false;GameInput.Instance.SetMobileMovement(Vector2.zero);hunting.Request(KhezuCommand.Sleep);
                Record("Saved scene reuses the existing encounter adapter",presenter!=null && presenter.encounter==encounter && presenter.hunting==hunting && presenter.barrierPrefab!=null);
                Capture(-1,serial,0);yield return new WaitForSeconds(.8f);
                Record("No barrier during cinematic POV",encounter.cinematic.IsPlaying && presenter.ActiveBarrier==null);
                yield return new WaitForSeconds(1);
                Record("No early barrier before completed two-second jumpscare",presenter.ActiveBarrier==null);
                float deadline=Time.time+3;while(presenter.ActiveBarrier==null && Time.time<deadline)yield return null;
                var barrier=presenter.ActiveBarrier;
                Record("Barrier begins only at confirmed post-cinematic challenge",barrier!=null && encounter.Phase==KhezuEncounterPhase.BarrierChallenge && !encounter.cinematic.IsPlaying);
                yield return new WaitForSeconds(.25f);float early=barrier.Formation;var initialRotation=barrier.wallRunes.localRotation;
                yield return new WaitForSeconds(.45f);Record("Energy formation rises progressively",barrier.Formation>early && early<.9f);
                Record("Runes and opposing rings rotate",Quaternion.Angle(initialRotation,barrier.wallRunes.localRotation)>1 && barrier.upperRing.localRotation!=barrier.lowerRing.localRotation);
                yield return new WaitForSeconds(.6f);
                Record("At most one instance per capture serial",FindObjectsByType<CrimsonMathBarrierVfx>(FindObjectsSortMode.None).Length==1 && presenter.ActiveBarrier==barrier);
                Record("Particle counts obey the 32-particle ceiling",barrier.sparks.particleCount<=24 && barrier.fragments.particleCount<=8);
                var particles=new ParticleSystem.Particle[24];int count=barrier.sparks.GetParticles(particles);bool rising=false;for(int i=0;i<count;i++)rising|=particles[i].position.y>.25f;
                Record("Energy particles emit and rise above the ground",count>0 && rising && barrier.fragments.particleCount>0,"sparks="+count+", fragments="+barrier.fragments.particleCount);
                ScreenCapture.CaptureScreenshot(Reports+"/Crimson_Barrier_Gameplay_UI.png");yield return new WaitForEndOfFrame();
                Record("Presentation leaves UI available while the encounter owns movement protection",barrier.GetComponentsInChildren<Collider>().Length==0 && barrier.GetComponentsInChildren<Canvas>().Length==0 && player.enabled && player.IsMovementLocked && !GameInput.Instance.GameplayInputBlocked,"The existing encounter holds the Phase 3B movement token; the visual prefab owns no gameplay.");
                Record("Visual presentation never moves authoritative Khezu",Vector3.Distance(hunting.transform.position,position)<.001f && Quaternion.Angle(hunting.transform.rotation,rotation)<.01f);
                int captures=hunting.CaptureSerial;hunting.Request(KhezuCommand.ResetHunt);yield return new WaitForSeconds(.25f);
                Record("Existing encounter release breaks and fades runes",presenter.ActiveBarrier==null && barrier!=null && barrier.IsDissolving && barrier.Breakup>0 && barrier.Visibility<1 && barrier.Visibility>0);
                yield return new WaitForSeconds(1);Record("Dissolve releases all transient renderers and particles",barrier==null && FindObjectsByType<CrimsonMathBarrierVfx>(FindObjectsSortMode.None).Length==0 && hunting.CaptureSerial==captures);
                Capture(-1,++serial,20);yield return new WaitForSeconds(.3f);barrier=presenter.ActiveBarrier;
                Record("Late confirmed checkpoint resumes formed barrier",barrier!=null && barrier.Formation==1 && !encounter.cinematic.IsPlaying);
                hunting.Request(KhezuCommand.ResetHunt);yield return new WaitForSeconds(1.1f);
                Capture(999,++serial,20);yield return new WaitForSeconds(.3f);
                Record("Missing actor never produces a barrier on the local bystander",presenter.ActiveBarrier==null && !encounter.cinematic.IsPlaying && player.enabled);
                hunting.Request(KhezuCommand.ResetHunt);Capture(-1,++serial,20);yield return new WaitForSeconds(.3f);
                presenter.OnDisconnected(Photon.Realtime.DisconnectCause.DisconnectByClientLogic);yield return new WaitForSeconds(1.2f);
                Record("Disconnect does not recreate stale capture VFX",presenter.ActiveBarrier==null && FindObjectsByType<CrimsonMathBarrierVfx>(FindObjectsSortMode.None).Length==0);
                presenter.OnJoinedRoom();hunting.Request(KhezuCommand.ResetHunt);Capture(-1,++serial,20);yield return new WaitForSeconds(.3f);
                Record("New valid encounter can present again",presenter.ActiveBarrier!=null && presenter.PresentedSerial==serial);
                Record("No runtime or particle-configuration errors",report.runtimeErrors.Count==0);
            }
            finally{
                hunting.Request(KhezuCommand.Sleep);session.enabled=oldSession;GameInput.Instance.SetGameplayInputBlocked(oldBlock);
                Application.logMessageReceived-=Logged;
                report.passed=report.checks.Count>0 && report.checks.TrueForAll(c=>c.passed);Directory.CreateDirectory(Reports);File.WriteAllText(Reports+"/CrimsonBarrierPlayMode.json",JsonUtility.ToJson(report,true));Debug.Log("Crimson barrier visuals: "+report.checks.FindAll(c=>c.passed).Count+"/"+report.checks.Count);Destroy(gameObject);
            }
        }
    }
}
#endif
