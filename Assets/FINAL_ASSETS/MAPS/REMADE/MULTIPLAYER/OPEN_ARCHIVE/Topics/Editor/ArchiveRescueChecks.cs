using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using LogicLegends.OpenArchive.Hunting;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveRescueChecks
    {
        [Serializable] sealed class Report
        {
            public bool passed;
            public string scope="Offline saved-scene Play Mode with the existing monster rig and baked navigation. Exercises real zero-heart detection, monster approach, visible pickup, physical return to nest, locked avatar, checkpoint serialization and authoritative one-heart restore primitive. Positive two-player Revival Kit consumption and rendered Android are separate tests.";
            public List<string> checks=new List<string>(), errors=new List<string>();
        }
        static Report report; static IEnumerator flow; static double next;
        static ArchiveTopicSession session; static bool previousSuppression;
        public static string Status {get;private set;}="Idle";
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Nest Rescue (Offline Play Mode)")]
        public static void Begin()
        {
            if(!Application.isPlaying || Photon.Pun.PhotonNetwork.InRoom || flow!=null)throw new InvalidOperationException("Use offline Play Mode from the saved MultiplayerGameplay scene.");
            session=ArchiveTopicSession.Current;if(session==null)throw new InvalidOperationException("Open MultiplayerGameplay first.");
            report=new Report();previousSuppression=session.suppressDefeatForVerification;session.suppressDefeatForVerification=true;
            flow=Flow();next=0;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;
        }
        static void Check(bool condition,string text){report.checks.Add((condition?"PASS ":"FAIL ")+text);if(!condition)throw new InvalidOperationException(text);}
        static void Log(string text,string stack,LogType type){if(report.errors.Count<8&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))report.errors.Add(text+"\n"+stack);}
        static void Tick()
        {
            if(flow==null)return;
            try
            {
                EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup<next)return;
                if(!Application.isPlaying)throw new InvalidOperationException("Play Mode ended during rescue verification.");
                if(!flow.MoveNext()){Finish(true);return;}next=EditorApplication.timeSinceStartup+(flow.Current is float delay?delay:0);
            }
            catch(Exception ex){report.errors.Add(Status+": "+ex);Finish(false);}
        }
        static void Finish(bool success)
        {
            EditorApplication.update-=Tick;Application.logMessageReceived-=Log;flow=null;
            if(session!=null)session.suppressDefeatForVerification=previousSuppression;
            report.passed=success&&report.errors.Count==0;Status=report.passed?"Passed":"Failed";
            Directory.CreateDirectory("Temp/ArchiveSurvival");File.WriteAllText("Temp/ArchiveSurvival/Rescue.json",JsonUtility.ToJson(report,true));
        }
        static IEnumerator Flow()
        {
            Status="Prepare real carry route";session.Initialize(17321);
            var monster=session.hunting;var health=monster.health;monster.Request(KhezuCommand.Sleep);
            Check(monster.Initialized&&health.HasAuthoritativeActorHealth,"Existing monster and original health owner are active");
            if(!health.IsActorAlive(-1))health.RestoreAuthoritativeActorHeart(-1,true);
            // The fixture begins at full health; no replacement health tracker is created.
            while(health.ActorHearts(-1)<health.MaximumHealth)health.RestoreAuthoritativeActorHeart(-1);
            if(health.IsActorProtected(-1))yield return 6.1f;
            var player=Player.LocalInstance;var filter=new NavMeshQueryFilter{agentTypeID=monster.Agent.agentTypeID,areaMask=monster.Agent.areaMask};
            Vector3 start=Vector3.zero;bool found=false;
            for(int i=0;i<12;i++)
            {
                var desired=monster.transform.position+Quaternion.Euler(0,i*30,0)*Vector3.forward*38;
                if(!NavMesh.SamplePosition(desired,out var hit,5,filter))continue;
                var path=new NavMeshPath();if(NavMesh.CalculatePath(monster.transform.position,hit.position,filter,path)&&path.status==NavMeshPathStatus.PathComplete){start=hit.position;found=true;break;}
            }
            Check(found,"A complete real baked route connects the victim fixture and nest");
            var capsule=player.GetComponent<CharacterController>();bool enabled=capsule!=null&&capsule.enabled;if(enabled)capsule.enabled=false;
            player.transform.position=start;player.ResetMotionAfterTeleport();if(enabled)capsule.enabled=true;
            for(int i=0;i<health.MaximumHealth;i++)Check(health.DamageAuthoritativeActor(-1),"Authoritative heart loss "+(i+1));
            yield return .2f;
            var fallen=session.Downed(-1);
            Check(fallen!=null&&fallen.phase==0&&!health.IsActorAlive(-1),"Zero hearts create one checkpointed downed record");
            Check(player.IsMovementLocked&&Vector3.Distance(player.transform.position,start)<1,"Victim remains movement-locked at the loss position before pickup");
            Check(!monster.CanTranquilize&&monster.CarryingRescueActor,"A carrying monster cannot be tranquilized or start another capture");
            Check(!session.TryReviveActor(-1,-1)&&!session.TryReviveActor(999,-1),"Self-revive and unknown/distant rescuer requests are rejected");
            Status="Physical approach, pickup and return";double deadline=EditorApplication.timeSinceStartup+100;bool observedPickup=false;
            while(session.Downed(-1)!=null&&session.Downed(-1).phase!=2)
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Real carry did not reach the nest: "+monster.Status);
                if(session.Downed(-1).phase==1)observedPickup=true;
                yield return .1f;
            }
            fallen=session.Downed(-1);
            Check(observedPickup&&fallen!=null&&fallen.phase==2,"Monster visibly picks up the victim and completes its native NavMesh return");
            yield return 1.2f;
            Check(Vector3.Distance(player.transform.position,fallen.anchor)<.2f&&player.IsMovementLocked,"Delivered teammate waits at the nest rescue pad with movement still locked");
            Check(Vector3.Distance(fallen.anchor,monster.monsterSpawnPoint.position)<14,"Rescue pad belongs to the Universal Monster Nest");
            var clone=JsonUtility.FromJson<ArchiveTeamState>(JsonUtility.ToJson(session.State));
            Check(clone.survival.downed.Count==1&&clone.survival.downed[0].phase==2&&clone.survival.downed[0].actor==-1,"Checkpoint serialization preserves delivered victim and identity");
            Check(!health.RestoreAuthoritativeActorHeart(-1),"A medkit cannot revive a zero-heart teammate");
            Check(health.RestoreAuthoritativeActorHeart(-1,true)&&health.ActorHearts(-1)==1,"Revival primitive restores exactly one heart through the original health owner");
            Check(health.IsActorProtected(-1)&&!health.DamageAuthoritativeActor(-1),"Six-second recovery protection prevents immediate damage stacking");
            Check(!health.RestoreAuthoritativeActorHeart(-1,true),"A living teammate cannot be revived twice");
            yield return .2f;
            Check(session.Downed(-1)==null&&!player.IsMovementLocked,"Revived body releases the rescue and health movement locks");
        }
    }
}
