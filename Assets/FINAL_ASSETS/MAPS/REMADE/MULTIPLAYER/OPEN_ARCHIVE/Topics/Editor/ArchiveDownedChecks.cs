using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Explicit offline fixtures exercise existing health, rescue and item commands; never runs automatically.</summary>
    public static class ArchiveDownedChecks
    {
        [Serializable] sealed class Report
        {
            public bool passed;
            public string scope="Native offline MultiplayerGameplay Play: real practice avatar, nest body bounds, original player/CharacterController orientation, actual Revival Kit consumption, local spectator target, camera restoration. Two real network peers and physical Android remain separate verification.";
            public List<string> checks=new List<string>(), errors=new List<string>();
        }
        sealed class RigState
        {
            public Animator animator; public Transform parent; public Vector3 position,scale; public Quaternion rotation; public bool enabled;
        }
        static Report report; static IEnumerator flow; static double next;
        static ArchiveTopicSession session; static bool oldDefeat,oldThreats;
        public static string Status {get;private set;}="Idle";

        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Downed Bodies and Spectator (Offline Play Mode)")]
        public static void Begin()
        {
            if(!Application.isPlaying||Photon.Pun.PhotonNetwork.InRoom||flow!=null)
                throw new InvalidOperationException("Use fresh offline MultiplayerGameplay Play Mode.");
            session=ArchiveTopicSession.Current;
            if(session==null)throw new InvalidOperationException("Open the saved MultiplayerGameplay scene.");
            report=new Report(); oldDefeat=session.suppressDefeatForVerification;
            oldThreats=session.worldProgression!=null&&session.worldProgression.suppressThreatsForVerification;
            session.suppressDefeatForVerification=true;
            if(session.worldProgression!=null)session.worldProgression.suppressThreatsForVerification=true;
            Directory.CreateDirectory("Temp/ArchiveDowned");
            flow=Flow(); next=0; EditorApplication.update+=Tick; Application.logMessageReceived+=Log;
        }
        static void Check(bool value,string text)
        {report.checks.Add((value?"PASS ":"FAIL ")+text);Save();if(!value)throw new InvalidOperationException(text);}
        static void Save()=>File.WriteAllText("Temp/ArchiveDowned/Play.json",JsonUtility.ToJson(report,true));
        static void Log(string text,string stack,LogType type)
        {if(report.errors.Count<8&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))report.errors.Add(text+"\n"+stack);}
        static void Tick()
        {
            if(flow==null)return;
            try
            {
                EditorApplication.QueuePlayerLoopUpdate(); if(EditorApplication.timeSinceStartup<next)return;
                if(!Application.isPlaying)throw new InvalidOperationException("Play stopped during downed verification.");
                if(!flow.MoveNext()){Finish(true);return;}
                next=EditorApplication.timeSinceStartup+(flow.Current is float delay?delay:0);
            }
            catch(Exception e){report.errors.Add(Status+": "+e);Finish(false);}
        }
        static void Finish(bool ok)
        {
            EditorApplication.update-=Tick;Application.logMessageReceived-=Log;flow=null;
            if(session!=null)
            {
                session.suppressDefeatForVerification=oldDefeat;
                if(session.worldProgression!=null)session.worldProgression.suppressThreatsForVerification=oldThreats;
            }
            report.passed=ok&&report.errors.Count==0;Status=report.passed?"Passed":"Failed";Save();
        }
        static RigState[] Snapshot(Player player)=>player.GetComponentsInChildren<Animator>(true)
            .Where(a=>a.transform!=player.transform&&a.gameObject.activeInHierarchy)
            .Select(a=>new RigState{animator=a,parent=a.transform.parent,position=a.transform.localPosition,rotation=a.transform.localRotation,scale=a.transform.localScale,enabled=a.enabled}).ToArray();
        static bool Restored(RigState[] rigs)=>rigs.Length>0&&rigs.All(r=>r.animator!=null&&r.animator.transform.parent==r.parent&&r.animator.enabled==r.enabled&&
            Vector3.Distance(r.animator.transform.localPosition,r.position)<.02f&&Quaternion.Angle(r.animator.transform.localRotation,r.rotation)<.5f&&
            Vector3.Distance(r.animator.transform.localScale,r.scale)<.0001f);
        static float Floor(Player player)
        {
            foreach(var hit in Physics.RaycastAll(player.transform.position+Vector3.up*12,Vector3.down,35,~0,QueryTriggerInteraction.Ignore)
                .Where(h=>h.normal.y>.6f&&h.collider.GetComponentInParent<Player>()==null).OrderBy(h=>h.distance))return hit.point.y;
            throw new InvalidOperationException("No physical nest floor found below the downed body.");
        }
        static void CheckBody(ArchiveDownedPresentation view,Player player,int actor)
        {
            var b=view.LyingBounds(actor);var cc=player.GetComponent<CharacterController>();
            Check(view.IsActorLying(actor)&&b.size.y<Mathf.Max(b.size.x,b.size.z)*.7f&&
                Mathf.Max(b.size.x,b.size.z)>cc.height*.65f,
                "Actor "+actor+" lies horizontally with full-size baked skin bounds (no double-applied import scale)");
            Check(Mathf.Abs(b.min.y-Floor(player)-view.floorClearance)<.35f,"Actor "+actor+" lies on the physical nest floor without sinking or floating");
            Check(Vector3.Dot(player.transform.up,Vector3.up)>.999f&&cc!=null&&cc.enabled&&cc.height>cc.radius*2,
                "Actor "+actor+" keeps its upright player root and original CharacterController shape");
        }
        static void Move(Player player,Vector3 target)
        {
            var cc=player.GetComponent<CharacterController>();bool wasEnabled=cc!=null&&cc.enabled;
            if(wasEnabled)cc.enabled=false; player.transform.position=target; player.ResetMotionAfterTeleport();
            if(wasEnabled)cc.enabled=true;Physics.SyncTransforms();
        }
        static IEnumerator Flow()
        {
            Status="Prepare actual nest fixtures";session.Initialize(41823);yield return .3f;
            var local=Player.LocalInstance;var view=session.GetComponent<ArchiveDownedPresentation>();
            Check(view!=null&&view.enabled,"Saved topic session includes the downed presentation component");
            var camera=UnityEngine.Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None)
                .FirstOrDefault(c=>c.Follow!=null&&(c.Follow==local.transform||c.Follow.IsChildOf(local.transform)));
            Check(camera!=null,"Existing Cinemachine camera follows the real local player's target");
            var originalFollow=camera.Follow;var originalLookAt=camera.LookAt;var originalTarget=camera.Target;
            var originalRigs=Snapshot(local);var cc=local.GetComponent<CharacterController>();
            var originalHeight=cc.height;var originalCenter=cc.center;var originalRadius=cc.radius;
            Check(session.RequestAdminTest(ArchiveAdminTestAction.SpawnTeammate),"Admin creates the existing labelled practice teammate");yield return .3f;
            var bot=session.ActorPlayer(-2);var botRigs=Snapshot(bot);
            Check(bot!=null&&Player.LocalInstance==local,"Practice body has its own identity and never owns local input");
            Check(session.RequestAdminTest(ArchiveAdminTestAction.GrantItem,-1,(int)ArchiveSurvivalItem.RevivalKit),"Rescuer receives a real Revival Kit");
            Check(session.RequestAdminTest(ArchiveAdminTestAction.DownAtNest,-2),"Actual downed checkpoint delivers the practice teammate to the nest");yield return .35f;
            CheckBody(view,bot,-2);
            Check(!view.IsSpectating&&camera.Follow==originalFollow&&!local.IsMovementLocked&&bot.IsMovementLocked,
                "A living rescuer retains their camera and movement while only the fallen teammate locks");
            Move(local,session.Downed(-2).anchor+Vector3.back*2.5f);yield return .75f;
            ScreenCapture.CaptureScreenshot("Temp/ArchiveDowned/Teammate_At_Nest.png");yield return .25f;
            int kits=session.CountItem(-1,ArchiveSurvivalItem.RevivalKit);
            Check(session.RequestEquipItem((int)ArchiveSurvivalItem.RevivalKit)&&session.RequestUseEquippedItem(-2),
                "Equipped Revival Kit uses the real proximity-validated rescue command");yield return .3f;
            Check(session.hunting.health.ActorHearts(-2)==1&&session.CountItem(-1,ArchiveSurvivalItem.RevivalKit)==kits-1&&
                !view.IsActorLying(-2)&&!bot.IsMovementLocked&&Restored(botRigs),
                "Real revival consumes one kit, restores one heart and restores the upright animated teammate rig");

            Status="Local downed spectator and camera restoration";
            Check(session.RequestAdminTest(ArchiveAdminTestAction.SetHearts,-2,3),"Living teammate is available for spectating");
            Check(session.RequestAdminTest(ArchiveAdminTestAction.RecallTeammate),"Living teammate is moved clear of the rescue pad");
            Check(session.RequestAdminTest(ArchiveAdminTestAction.DownAtNest,-1),"Local player's zero-heart checkpoint uses the existing rescue system");yield return .35f;
            CheckBody(view,local,-1);
            Check(view.IsSpectating&&view.SpectatedActor==-2&&camera.Follow!=originalFollow&&camera.Follow!=null&&
                camera.Follow.name=="Downed_Spectator_Target","Downed local POV follows the living teammate through a separate spectator target");
            Check(Player.LocalInstance==local&&local.IsMovementLocked&&!bot.IsMovementLocked&&session.hunting.health.ActorHearts(-1)==0,
                "Spectating never changes input ownership, revives the viewer or locks the living teammate");
            view.CycleSpectator(1);
            Check(view.SpectatedActor==-2,"One available teammate stays selected when cycling spectator controls");
            Check(cc.height==originalHeight&&cc.center==originalCenter&&cc.radius==originalRadius,
                "Lying pose changes only the visual rig and preserves every CharacterController dimension");
            yield return .9f;
            Check(Camera.main!=null&&Vector3.Distance(Camera.main.transform.position,camera.Follow.position)>3f,
                "The actual spectator camera stays outside the living teammate capsule in the open spawn area");
            ScreenCapture.CaptureScreenshot("Temp/ArchiveDowned/Spectator.png");yield return .25f;

            Check(session.RequestAdminTest(ArchiveAdminTestAction.DownAtNest,-2),"Test fixture can remove the last living spectator target");yield return .25f;
            Check(view.IsSpectating&&view.SpectatedActor==0,"No living target shows the waiting outcome instead of spectating a corpse");
            Check(session.RequestAdminTest(ArchiveAdminTestAction.ReviveAtNest,-2),"Existing Admin rescue restores a valid target");yield return .25f;
            Check(view.SpectatedActor==-2,"Spectator automatically reacquires a revived living teammate");
            Check(session.RequestAdminTest(ArchiveAdminTestAction.ReviveAtNest,-1),"Existing Admin rescue revives the local viewer");yield return .35f;
            Check(!view.IsSpectating&&!view.IsActorLying(-1)&&camera.Follow==originalFollow&&camera.LookAt==originalLookAt,
                "Local revival restores the exact original camera Follow and LookAt bindings");
            Check(camera.Target.TrackingTarget==originalTarget.TrackingTarget&&
                camera.Target.CustomLookAtTarget==originalTarget.CustomLookAtTarget&&camera.Target.LookAtTarget==originalTarget.LookAtTarget,
                "Local revival restores raw Cinemachine tracking, custom LookAt mode and raw LookAt reference");
            var rebound=new GameObject("Spectator_Later_Target_Rebind_QA");rebound.transform.position=originalFollow.position;
            try
            {
                camera.Target.TrackingTarget=rebound.transform;
                Check(camera.Follow==rebound.transform&&camera.LookAt==(originalTarget.CustomLookAtTarget?originalLookAt:rebound.transform),
                    "A later TrackingTarget rebind retains the original inherited or custom LookAt behavior");
            }
            finally{camera.Target=originalTarget;camera.PreviousStateIsValid=false;UnityEngine.Object.Destroy(rebound);}
            Check(Restored(originalRigs)&&!local.IsMovementLocked&&Vector3.Dot(local.transform.up,Vector3.up)>.999f,
                "Local revival restores original visual transforms and Animator state with controls unlocked");
            Status="Repeated down and revive preserves animation defaults";
            Check(session.RequestAdminTest(ArchiveAdminTestAction.DownAtNest,-1),"Local player can enter a second independent downed cycle");yield return .35f;
            CheckBody(view,local,-1);
            Check(session.RequestAdminTest(ArchiveAdminTestAction.ReviveAtNest,-1),"Second local revive uses the original rescue state owner");yield return .8f;
            Check(Restored(originalRigs)&&!view.IsActorLying(-1)&&!view.IsSpectating&&camera.Follow==originalFollow&&
                camera.Target.CustomLookAtTarget==originalTarget.CustomLookAtTarget&&camera.Target.LookAtTarget==originalTarget.LookAtTarget,
                "Several later Animator frames and repeated revival preserve original rig parents, transforms and camera settings");
            Check(Restored(botRigs),"The repeatedly downed teammate also preserves its original animated rig parent and transforms");
            Status="Clean fixture";session.RequestAdminTest(ArchiveAdminTestAction.ResetMatch);
            session.RequestAdminTest(ArchiveAdminTestAction.RemoveTeammate);yield return .25f;
            Check(view.LyingActorCount==0&&!view.IsSpectating&&camera.Follow==originalFollow&&Player.LocalInstance==local,
                "Reset and practice teammate removal leave no lying bodies, spectator camera or input-owner changes");
        }
    }
}
