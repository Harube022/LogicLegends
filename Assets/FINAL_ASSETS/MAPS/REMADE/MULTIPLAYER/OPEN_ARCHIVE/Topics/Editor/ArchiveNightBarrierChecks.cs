using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LogicLegends.OpenArchive.Hunting;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveNightBarrierChecks
    {
        [Serializable] sealed class Report {public bool passed;public string scope="Offline Editor night fixture with the two real rig/controller instances. Confirmed-capture fixtures invoke the existing capture transaction directly (not claimed physical collision tests). Answers use the actual mathematical validator and native request path; tests real attack, movement lock, stun and one-heart deduction for each monster.";public List<string> checks=new List<string>();public List<string> errors=new List<string>();}
        static Report report;static IEnumerator flow;static double next;static ArchiveTopicSession session;
        public static string Status {get;private set;}="Idle";
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Both Night Barriers (Offline Play Mode)")]
        public static void Begin()
        {
            if(!Application.isPlaying||Photon.Pun.PhotonNetwork.InRoom||flow!=null)throw new InvalidOperationException("Use offline Play Mode.");
            session=ArchiveTopicSession.Current;report=new Report();flow=Flow();next=0;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;
        }
        static void Check(bool value,string name){report.checks.Add((value?"PASS ":"FAIL ")+name);if(!value)throw new InvalidOperationException(name);}
        static void Log(string text,string stack,LogType type){if(report.errors.Count<10&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))report.errors.Add(text+"\n"+stack);}
        static void Tick(){if(flow==null)return;try{EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup<next)return;if(!flow.MoveNext()){Finish(true);return;}next=EditorApplication.timeSinceStartup+(flow.Current is float f?f:0);}catch(Exception e){report.errors.Add(Status+": "+e);Finish(false);}}
        static void Finish(bool passed)
        {
            EditorApplication.update-=Tick;Application.logMessageReceived-=Log;flow=null;session.hunting.enabled=true;session.hunting.Request(KhezuCommand.Sleep);session.worldProgression.secondary.Request(KhezuCommand.Sleep);
            report.passed=passed&&report.errors.Count==0;Status=report.passed?"Passed":"Failed";Directory.CreateDirectory("Temp/ArchiveWorld");File.WriteAllText("Temp/ArchiveWorld/Barriers.json",JsonUtility.ToJson(report,true));
        }
        static void Capture(KhezuHuntingController hunt)
        {
            hunt.Request(KhezuCommand.Sleep);var snapshot=hunt.GetSnapshot();snapshot.state=(int)KhezuHuntState.Ready;snapshot.target=-1;snapshot.caught=0;snapshot.awake=true;snapshot.mistakes=snapshot.stage=4;snapshot.animation="Idle";snapshot.animationSpeed=1;
            hunt.ApplySnapshot(snapshot,true);
            var type=typeof(KhezuHuntingController).GetNestedType("Target",BindingFlags.NonPublic);var target=Activator.CreateInstance(type,true);var player=Player.LocalInstance;
            type.GetField("transform").SetValue(target,player.transform);type.GetField("capsule").SetValue(target,player.GetComponent<CharacterController>());type.GetField("name").SetValue(target,"Confirmed capture fixture");
            typeof(KhezuHuntingController).GetMethod("CatchTarget",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(hunt,new[]{target});
        }
        static IEnumerator Flow()
        {
            var world=session.worldProgression;world.selector.SelectKhezu();
            session.Initialize(991);session.hunting.Request(KhezuCommand.Sleep);session.hunting.enabled=false;
            for(int topic=0;topic<7;topic++){ArchiveTopicPlayChecks.MoveTo(session.stations[topic],ArchiveTopicStation.BookPosition+Vector3.back*1.7f);session.Request(topic,ArchiveAction.Start);yield return .03f;Check(session.RequestTopicAdmin(topic,session.State.stations[topic].revision,true),"Authorized night fixture topic "+topic);}
            yield return world.entranceSeconds+world.warningSeconds+.5f;
            Check(world.SecondaryName=="Rajang","Khezu-first matches introduce the existing Rajang rig as their second guardian");
            world.secondary.Request(KhezuCommand.Sleep);session.hunting.enabled=true;session.hunting.Request(KhezuCommand.Sleep);
            ArchiveTopicPlayChecks.MoveTo(session.stations[0],ArchiveTopicStation.BookPosition+Vector3.back*1.7f);yield return .1f;
            int start=session.hunting.health.ActorHearts(-1),lastSerial=0;
            foreach(var hunt in new[]{world.secondary,session.hunting})
            {
                Status="Barrier for "+(string.IsNullOrEmpty(hunt.encounterId)?"primary":"second guardian");
                var other=hunt==session.hunting?world.secondary:session.hunting;other.Request(KhezuCommand.Sleep);
                Capture(hunt);Check(hunt.State==KhezuHuntState.Caught&&hunt.CaptureSerial>lastSerial,"Capture transaction assigns a globally increasing serial across both monsters");lastSerial=hunt.CaptureSerial;
                int beforeOther=other.CaptureSerial;Capture(other);
                Check(other.State!=KhezuHuntState.Caught&&other.CaptureSerial==beforeOther,"A second capture cannot overlap the existing prison");other.Request(KhezuCommand.Sleep);
                yield return 2.4f;
                Check(session.LearningHunting==hunt&&session.IsLocalBarrierQuestion&&Player.LocalInstance.IsMovementLocked,"The same barrier UI and movement lock bind to the actual capturing monster");
                var problem=session.BarrierProblem();int wrong=Enumerable.Range(0,problem.options.Length).First(i=>!problem.Validate(i,out _));int before=session.hunting.health.ActorHearts(-1);
                Check(session.RequestBarrierAnswer(wrong),"Wrong answer uses the actual authoritative math validator");yield return 1.4f;
                Check(session.hunting.health.ActorHearts(-1)==before-1&&hunt.FailureStunActive&&Player.LocalInstance.IsMovementLocked,"Failed challenge applies one real attack and one heart with the five-second stun");
                Check(!session.RequestBarrierAnswer(wrong)&&session.hunting.health.ActorHearts(-1)==before-1,"Repeated answer cannot deduct a second heart");
                yield return hunt.StunSecondsRemaining+.25f;
                Check(!hunt.FailureStunActive,"The existing timed stun ends after five seconds");hunt.Request(KhezuCommand.Sleep);yield return .15f;
            }
            Check(session.hunting.health.ActorHearts(-1)==start-2,"Primary and second-monster serials each deduct exactly one heart");
        }
    }
}
