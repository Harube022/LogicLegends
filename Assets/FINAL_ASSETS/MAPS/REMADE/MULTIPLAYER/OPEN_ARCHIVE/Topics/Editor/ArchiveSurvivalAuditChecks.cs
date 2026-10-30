using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveSurvivalAuditChecks
    {
        [Serializable] sealed class Report
        {
            public bool passed;
            public string scope="Offline saved-scene fixtures: the practice teammate must also escape, automatic Revival Kit skips an unavailable closest body, overhead solid geometry cannot become the delivered body's floor, and a reused match resets defeat-return state. Inactive Photon membership is checked by the same production target-availability gate; this fixture does not simulate a disconnected phone.";
            public List<string> checks=new List<string>(),errors=new List<string>();
        }
        static ArchiveTopicSession session;static Report report;static IEnumerator flow;static double next;
        static GameObject overhang;static bool oldDefeat,oldThreats;static Vector3 originalPosition;
        public static string Status{get;private set;}="Idle";
        public static void Begin()
        {
            if(!Application.isPlaying||Photon.Pun.PhotonNetwork.InRoom||flow!=null)throw new InvalidOperationException("Use fresh offline MultiplayerGameplay Play Mode.");
            session=ArchiveTopicSession.Current;if(session==null||!session.Ready)throw new InvalidOperationException("Load MultiplayerGameplay first.");
            oldDefeat=session.suppressDefeatForVerification;oldThreats=session.worldProgression.suppressThreatsForVerification;
            originalPosition=Player.LocalInstance.transform.position;session.suppressDefeatForVerification=session.worldProgression.suppressThreatsForVerification=true;
            report=new Report();flow=Flow();next=0;EditorApplication.update+=Tick;
        }
        static void Check(bool passed,string name){report.checks.Add((passed?"PASS ":"FAIL ")+name);if(!passed)throw new InvalidOperationException(name);}
        static void Move(Player p,Vector3 position)
        {
            var capsule=p.GetComponent<CharacterController>();bool enabled=capsule.enabled;capsule.enabled=false;
            p.transform.position=position;p.ResetMotionAfterTeleport();capsule.enabled=enabled;Physics.SyncTransforms();
        }
        static IEnumerator Flow()
        {
            Status="Prepare a connected nest teammate";
            session.RequestAdminTest(ArchiveAdminTestAction.ResetMatch);session.RequestAdminTest(ArchiveAdminTestAction.RemoveTeammate);
            Check(session.RequestAdminTest(ArchiveAdminTestAction.SpawnTeammate),"The existing practice teammate spawns");
            Check(session.SurvivorCount==2,"Admin Lab counts both living practice players as survivors");
            var portal=session.exitPortal;Check(portal!=null,"The saved victory portal exists");
            session.State.exit.opened=true;session.State.exit.openedAt=ArchiveTopicSession.Clock-portal.formationSeconds-1;
            var advanceExit=typeof(ArchiveTopicSession).GetMethod("AdvanceExitProgress",BindingFlags.NonPublic|BindingFlags.Instance);
            Move(Player.LocalInstance,portal.transform.TransformPoint(new Vector3(0,3,0)));
            advanceExit.Invoke(session,null);
            Check(session.HasEscapedActor(-1)&&session.EscapedSurvivorCount==1&&!session.TeamWon,"Local portal entry waits for the living practice teammate");
            Move(session.ActorPlayer(-2),portal.transform.TransformPoint(new Vector3(1,3,0)));
            advanceExit.Invoke(session,null);
            Check(session.TeamWon&&session.HasEscapedActor(-2)&&session.EscapedSurvivorCount==2,"Both practice survivors must enter before team victory");
            session.RequestAdminTest(ArchiveAdminTestAction.ResetMatch);Move(Player.LocalInstance,originalPosition);
            session.RequestAdminTest(ArchiveAdminTestAction.GrantAllItems,-1);
            session.RequestAdminTest(ArchiveAdminTestAction.DownAtNest,-2);yield return .15f;
            var fallen=session.Downed(-2);Move(Player.LocalInstance,fallen.anchor+Vector3.back*2.5f);yield return .15f;
            Check(session.IsRevivalTargetAvailable(-2),"A connected zero-heart teammate delivered to the nest is eligible");
            var friend=session.ActorPlayer(-2);friend.gameObject.SetActive(false);
            Check(!session.IsRevivalTargetAvailable(-2)&&!session.TryReviveActor(-1,-2),"An unavailable avatar cannot be revived directly");
            friend.gameObject.SetActive(true);
            var own=session.Inventory(-1);own.nextUseAt=0;int before=session.CountItem(-1,ArchiveSurvivalItem.RevivalKit);
            session.State.survival.downed.Add(new ArchiveDownedActor{actor=999999,phase=2,anchor=Player.LocalInstance.transform.position,createdAt=ArchiveTopicSession.Clock,arrivedAt=ArchiveTopicSession.Clock-2});
            Check(session.RequestUseItem(ArchiveSurvivalItem.RevivalKit),"Automatic kit use skips a closer unavailable body and reaches the eligible teammate");
            Check(session.hunting.health.ActorHearts(-2)==1&&session.CountItem(-1,ArchiveSurvivalItem.RevivalKit)==before-1,"Exactly one kit revives the actual teammate with one heart");
            session.State.survival.downed.RemoveAll(d=>d.actor==999999);yield return .2f;
            Status="Ground beneath an overhead bridge fixture";
            var method=typeof(ArchiveDownedPresentation).GetMethod("GroundBelow",BindingFlags.NonPublic|BindingFlags.Static);
            float floor=(float)method.Invoke(null,new object[]{Player.LocalInstance});
            overhang=GameObject.CreatePrimitive(PrimitiveType.Cube);overhang.name="Survival_Audit_Overhead_Bridge";
            overhang.transform.position=Player.LocalInstance.transform.position+Vector3.up*8;overhang.transform.localScale=new Vector3(10,.5f,10);Physics.SyncTransforms();
            float withRoof=(float)method.Invoke(null,new object[]{Player.LocalInstance});
            Check(Mathf.Abs(withRoof-floor)<.05f,"An overhead bridge or pillar cap does not replace the body floor");
            UnityEngine.Object.Destroy(overhang);overhang=null;
            Status="Fresh-match defeat-return latch";
            var flag=typeof(ArchiveTopicSession).GetField("returningAfterDefeat",BindingFlags.NonPublic|BindingFlags.Instance);
            flag.SetValue(session,true);session.RequestAdminTest(ArchiveAdminTestAction.ResetMatch);
            typeof(ArchiveTopicSession).GetMethod("TickSurvival",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(session,null);
            Check(!(bool)flag.GetValue(session),"A fresh match clears the previous defeat-return latch");
        }
        static void Tick()
        {
            if(flow==null)return;EditorApplication.QueuePlayerLoopUpdate();
            try{if(!Application.isPlaying)throw new InvalidOperationException("Play Mode ended");if(EditorApplication.timeSinceStartup<next)return;if(!flow.MoveNext()){Finish(true);return;}next=EditorApplication.timeSinceStartup+(flow.Current is float delay?delay:0);}
            catch(Exception e){report.errors.Add(Status+": "+e);Finish(false);}
        }
        static void Finish(bool passed)
        {
            EditorApplication.update-=Tick;flow=null;
            if(overhang!=null)UnityEngine.Object.Destroy(overhang);
            if(Application.isPlaying&&session!=null)
            {
                session.RequestAdminTest(ArchiveAdminTestAction.ResetMatch);session.RequestAdminTest(ArchiveAdminTestAction.RemoveTeammate);
                Move(Player.LocalInstance,originalPosition);session.suppressDefeatForVerification=oldDefeat;session.worldProgression.suppressThreatsForVerification=oldThreats;
            }
            report.passed=passed&&report.errors.Count==0;Status=report.passed?"Passed":"Failed";
            Directory.CreateDirectory("Temp/ArchiveMultiplayerAudit");File.WriteAllText("Temp/ArchiveMultiplayerAudit/SurvivalFixes.json",JsonUtility.ToJson(report,true));
        }
    }
}
