using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LogicLegends.OpenArchive.Hunting;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveAdminLabChecks
    {
        [Serializable] sealed class Report { public bool passed;public string scope="Fresh offline Editor Play: actual Admin controls, existing player rig, actual health/downed/carry/revive path, full-screen timed night event and warning. The practice teammate is not a network client. Physical Android is unverified.";public List<string> checks=new List<string>(),errors=new List<string>(); }
        static Report report;static IEnumerator flow;static double next;static ArchiveTopicSession s;
        public static string Status {get;private set;}="Idle";
        public static void Begin(){if(!Application.isPlaying||Photon.Pun.PhotonNetwork.InRoom||flow!=null)throw new InvalidOperationException("Use fresh offline MultiplayerGameplay Play Mode.");s=ArchiveTopicSession.Current;report=new Report();flow=Flow();next=0;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
        static void Check(bool valid,string text){report.checks.Add((valid?"PASS ":"FAIL ")+text);Save();if(!valid)throw new InvalidOperationException(text);}
        static void Save(){Directory.CreateDirectory("Temp/ArchiveAdminLab");File.WriteAllText("Temp/ArchiveAdminLab/Play.json",JsonUtility.ToJson(report,true));}
        static void Log(string msg,string stack,LogType type){if(report.errors.Count<8&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))report.errors.Add(msg+"\n"+stack);}
        static void Tick(){if(flow==null)return;EditorApplication.QueuePlayerLoopUpdate();try{if(EditorApplication.timeSinceStartup<next)return;if(!Application.isPlaying)throw new Exception("Play stopped");if(!flow.MoveNext()){Finish(true);return;}next=EditorApplication.timeSinceStartup+(flow.Current is float delay?delay:0);}catch(Exception e){report.errors.Add(Status+": "+e);Finish(false);}}
        static void Finish(bool success){EditorApplication.update-=Tick;Application.logMessageReceived-=Log;flow=null;report.passed=success&&report.errors.Count==0;Status=report.passed?"Passed":"Failed";Save();}
        static void Move(Player player,Vector3 position){var c=player.GetComponent<CharacterController>();c.enabled=false;player.transform.position=position;c.enabled=true;player.ResetMotionAfterTeleport();Physics.SyncTransforms();}
        static IEnumerator Flow()
        {
            s.Initialize(31173);Status="Admin and practice body";yield return .3f;
            var local=Player.LocalInstance;var cameraTarget=UnityEngine.Object.FindFirstObjectByType<Unity.Cinemachine.CinemachineCamera>().Follow;
            Check(s.RequestAdminTest(ArchiveAdminTestAction.SpawnTeammate),"Admin creates a clearly labelled offline teammate");yield return .2f;
            var bot=s.ActorPlayer(-2);
            Check(bot!=null&&bot!=local&&s.AdminTestActors.Length==2,"Practice actor has a distinct body and stable actor identity");
            Check(Player.LocalInstance==local&&UnityEngine.Object.FindFirstObjectByType<Unity.Cinemachine.CinemachineCamera>().Follow==cameraTarget,"Practice body never steals local input or camera ownership");
            Check(bot.GetComponentsInChildren<Renderer>().Any(r=>r.enabled)&&s.hunting.health.ActorHearts(-2)==3&&!bot.IsMovementLocked,"Existing visible player rig starts with three authoritative hearts");
            Check(s.RequestAdminTest(ArchiveAdminTestAction.GrantAllItems,-1)&&s.CountItem(-1,ArchiveSurvivalItem.RevivalKit)==3,"Admin supplies give actual consumable inventories");
            Check(s.RequestAdminTest(ArchiveAdminTestAction.DamageHeart,-1)&&s.hunting.health.CurrentHealth==2,"Admin damage changes the existing health owner");
            Check(s.RequestUseItem(ArchiveSurvivalItem.Medkit)&&s.hunting.health.CurrentHealth==3,"A granted medkit uses the real item command and restores one heart");
            Status="Practice teammate knockout and physical carry";
            Check(s.RequestAdminTest(ArchiveAdminTestAction.SetHearts,-2,0),"Admin can deplete the test teammate hearts");yield return .3f;
            Check(s.Downed(-2)!=null&&bot.IsMovementLocked&&!local.IsMovementLocked&&!s.TeamDefeated,"Only the fallen teammate locks; the living tester remains free");
            double deadline=EditorApplication.timeSinceStartup+100;bool carried=false;
            while(s.Downed(-2)!=null&&s.Downed(-2).phase!=2){if(s.Downed(-2).phase==1)carried=true;if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Carry timeout "+s.hunting.Status);yield return .15f;}
            yield return 1.1f;
            Check(carried&&s.Downed(-2)?.phase==2,"Monster physically picks up the test teammate and delivers them to the nest");
            var fallen=s.Downed(-2);Move(local,fallen.anchor+s.hunting.monsterSpawnPoint.forward*2);yield return .2f;
            int kits=s.CountItem(-1,ArchiveSurvivalItem.RevivalKit);
            Check(s.RequestUseItem(ArchiveSurvivalItem.RevivalKit,-2),"The human tester revives the offline teammate using the actual kit command");yield return .2f;
            Check(s.hunting.health.ActorHearts(-2)==1&&s.CountItem(-1,ArchiveSurvivalItem.RevivalKit)==kits-1&&!bot.IsMovementLocked,"Revival consumes one kit, restores one heart and releases every practice-body lock");
            Check(s.RequestAdminTest(ArchiveAdminTestAction.RecallTeammate),"A living practice teammate can be recalled nearby");
            s.RequestAdminTest(ArchiveAdminTestAction.ResetMatch);yield return .2f;
            Status="Topics and deterministic chest selection";
            Check(s.RequestAdminTest(ArchiveAdminTestAction.StartTopic,0)&&s.ActiveTopic==0,"Admin visits and activates the real Book flow");
            Check(s.RequestAdminTest(ArchiveAdminTestAction.CompleteTopic,0)&&s.State.stations[0].phase==2&&s.State.mistakes.Count==0,"Admin completes a selected topic without mistakes");
            Check(s.FullyRestoredCrystals==0&&s.Chest(0)==null,"Academic skip does not invent earned restoration or random loot");
            Check(s.RequestAdminTest(ArchiveAdminTestAction.SpawnChest,0,(int)ArchiveSurvivalItem.Medkit),"Explicit chest fixture exposes a selected drop for testing");yield return 1.9f;
            Check(s.Chest(0).item==ArchiveSurvivalItem.Medkit&&s.RequestChest(0)&&s.CountItem(-1,ArchiveSurvivalItem.Medkit)==1,"Selected test chest grants the chosen item through normal proximity and ownership validation");
            var controls=s.GetComponent<ArchiveTopicTestControls>();controls.ToggleTopicAdmin();yield return .2f;
            Check(controls.AdminPanelOpen&&UnityEngine.Object.FindObjectsByType<ScrollRect>(FindObjectsSortMode.None).Any(x=>x.name=="Admin_Scroll"&&x.verticalScrollbar!=null),"Admin Lab opens with a real scroll view and visible scrollbar");
            ScreenCapture.CaptureScreenshot("Temp/ArchiveAdminLab/Admin_Topics.png");yield return .15f;controls.ToggleTopicAdmin();
            Status="Full-screen entrance and warning";
            foreach(var m in KhezuHuntingController.ActiveMonsters)foreach(int actor in s.AdminTestActors)m.SetPlayerEligibility(actor,false);
            Check(s.RequestAdminTest(ArchiveAdminTestAction.PreviewNight),"Admin previews night without clearing seven academic topics");yield return 1.5f;
            var w=s.worldProgression;
            Check(w.FullscreenVisible&&w.IntroLocksHeld==2&&local.IsMovementLocked&&bot.IsMovementLocked,"Full-screen entrance holds both actors safely while the guardian is revealed");
            var rect=GameObject.Find("Guardian_Entrance_Fullscreen").GetComponent<RectTransform>();Check(rect.anchorMin==Vector2.zero&&rect.anchorMax==Vector2.one,"Film fills the entire screen instead of an inset over gameplay");
            Check(s.hunting.State!=KhezuHuntState.Caught&&s.State.mistakes.Count==0,"Night event does not fabricate mistakes or capture players behind the film");
            ScreenCapture.CaptureScreenshot("Temp/ArchiveAdminLab/Fullscreen_Night.png");yield return 5.0f;
            Check(!w.FullscreenVisible&&w.WarningVisible&&!local.IsMovementLocked&&!bot.IsMovementLocked,"Film ends, releases controls and switches to the timed warning FX");
            ScreenCapture.CaptureScreenshot("Temp/ArchiveAdminLab/Night_Warning.png");yield return 5.1f;
            Check(s.State.worldTime.secondaryReleased&&KhezuHuntingController.ActiveMonsters.Length==2,"Shared night clock releases the existing second guardian after the warning");
            s.RequestAdminTest(ArchiveAdminTestAction.ReturnDaylight);yield return .2f;
            Check(!w.FullscreenVisible&&!w.WarningVisible&&w.IntroLocksHeld==0,"Returning to daylight cleans every cinematic overlay and movement lock");
            Check(s.RequestAdminTest(ArchiveAdminTestAction.PreviewBarrier,0),"Admin can enter the real barrier challenge without waiting for a chase");yield return .4f;
            Check(s.hunting.State==KhezuHuntState.Caught&&s.IsLocalBarrierQuestion&&local.IsMovementLocked,"Barrier preview uses the existing math state and real capture movement lock");
            Check(s.RequestAdminTest(ArchiveAdminTestAction.BarrierSuccess,0),"Admin can test the barrier success outcome");yield return 1.3f;
            Check(!local.IsMovementLocked,"Successful barrier test releases the player");
            s.RequestAdminTest(ArchiveAdminTestAction.ResetMatch);s.RequestAdminTest(ArchiveAdminTestAction.RemoveTeammate);yield return .2f;
            Check(s.ActorPlayer(-2)==null&&Player.LocalInstance==local&&!local.IsMovementLocked,"Removing the practice teammate leaves the actual player and controls intact");
        }
    }
}
