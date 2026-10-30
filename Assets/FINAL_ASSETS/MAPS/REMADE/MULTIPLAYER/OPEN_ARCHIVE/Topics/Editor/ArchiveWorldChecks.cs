using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LogicLegends.OpenArchive.Hunting;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveWorldChecks
    {
        [Serializable] public sealed class Report {public bool passed;public string scope="Editor Play Mode with authorized Admin topic-completion fixtures. Checks real lighting, existing second rig, entrance, warning and navigation release. Academic rounds are verified separately; physical Android remains unverified.";public List<string> checks=new List<string>();public List<string> errors=new List<string>();}
        static Report report;static IEnumerator flow;static double next;static ArchiveTopicSession session;static bool originalHunt;
        public static string Status {get;private set;}="Idle";
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Day To Night (Offline Play Mode)")]
        public static void Begin()
        {
            if(!Application.isPlaying||Photon.Pun.PhotonNetwork.InRoom||flow!=null)throw new InvalidOperationException("Use offline Play Mode.");
            session=ArchiveTopicSession.Current;originalHunt=session.hunting.enabled;session.hunting.Request(KhezuCommand.Sleep);session.hunting.enabled=false;
            Directory.CreateDirectory("Temp/ArchiveWorld");
            report=new Report();flow=Flow();next=0;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;
        }
        static void Check(bool pass,string name){report.checks.Add((pass?"PASS ":"FAIL ")+name);if(!pass)throw new InvalidOperationException(name);}
        static void Log(string text,string stack,LogType type){if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&report.errors.Count<10)report.errors.Add(text+"\n"+stack);}
        static void Tick()
        {
            if(flow==null)return;
            try{EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup<next)return;if(!flow.MoveNext()){Finish(true);return;}next=EditorApplication.timeSinceStartup+(flow.Current is float delay?delay:0);}
            catch(Exception e){report.errors.Add(Status+": "+e);Finish(false);}
        }
        static void Finish(bool complete)
        {
            EditorApplication.update-=Tick;Application.logMessageReceived-=Log;flow=null;
            if(session!=null){session.hunting.enabled=originalHunt;session.hunting.Request(KhezuCommand.Sleep);session.worldProgression.secondary.Request(KhezuCommand.Sleep);}
            report.passed=complete&&report.errors.Count==0;Status=report.passed?"Passed":"Failed";
            Directory.CreateDirectory("Temp/ArchiveWorld");File.WriteAllText("Temp/ArchiveWorld/Play.json",JsonUtility.ToJson(report,true));
        }
        static IEnumerator Flow()
        {
            session.Initialize(18271);var world=session.worldProgression;world.suppressThreatsForVerification=true;
            Check(Enumerable.Range(0,11).Select(ArchiveTopicSession.WorldPhaseForTopics).SequenceEqual(new[]{0,0,0,1,1,2,2,3,3,3,3}),"3, 5 and 7 topics are the exact phase thresholds");
            yield return .5f;float day=world.sun.intensity;
            Check(session.State.worldTime.phase==0&&!world.secondary.gameObject.activeSelf,"Fresh match starts in daylight with one placed monster");
            for(int topic=0;topic<7;topic++)
            {
                Status="Complete Admin topic fixture "+(topic+1);
                ArchiveTopicPlayChecks.MoveTo(session.stations[topic],ArchiveTopicStation.BookPosition+Vector3.back*1.7f);
                session.Request(topic,ArchiveAction.Start);yield return .08f;
                Check(session.RequestTopicAdmin(topic,session.State.stations[topic].revision,true),"Admin fixture completion is authorized");
                Check(session.State.worldTime.phase==ArchiveTopicSession.WorldPhaseForTopics(topic+1),"Shared phase follows accepted topic count "+(topic+1));
                if(topic==2||topic==4)
                {
                    yield return world.lightingTransitionSeconds+.15f;
                    Check(world.sun.intensity<day&&world.sun.intensity>0,"Daylight blends into the next readable lighting palette");
                    UnityEngine.ScreenCapture.CaptureScreenshot("Temp/ArchiveWorld/"+(topic==2?"Afternoon.png":"Evening.png"));yield return .1f;
                }
                if(topic==6)world.suppressThreatsForVerification=false;
            }
            Status="Night guardian entrance";
            ArchiveTopicPlayChecks.MoveTo(session.stations[0],ArchiveTopicStation.BookPosition+Vector3.back*1.7f);
            yield return .5f;
            Check(world.secondary.gameObject.activeInHierarchy&&world.secondary.Initialized,"Existing second monster initializes only on nightfall");
            Check(world.secondary.GetComponent<Animator>().avatar!=session.hunting.GetComponent<Animator>().avatar,"The second actor uses the other existing monster rig");
            Check(world.secondary.EntranceHeld&&world.secondary.TargetPlayerId==0,"Entrance remains stationary and cannot target a player");
            var nest=UnityEngine.Object.FindFirstObjectByType<ArchiveNestAtmosphere>();
            var living=UnityEngine.Object.FindFirstObjectByType<ArchiveLivingEnvironment>();
            if(nest!=null)Check(nest.NearNest&&nest.ActiveNestRendererCount>0&&nest.ActiveNestRendererCount<=12,
                "Nest decorations remain visible in the entrance cinematic even when the player stands at a distant topic");
            if(living!=null)Check(living.IsNight&&living.VisibleMoteCount==0&&living.birdVoices.All(a=>a==null||!a.isPlaying),
                "Night entrance quiets decorative motes and daytime birds");
            var a=session.hunting.GetComponent<KhezuPhotonSync>();var b=world.secondary.GetComponent<KhezuPhotonSync>();
            Check(a.SyncChannel!=b.SyncChannel&&a.StateKey!=b.StateKey,"Each monster has a separate authoritative network namespace");
            Check(world.secondary.health==session.hunting.health,"Both monsters reuse the same authoritative heart owner");
            UnityEngine.ScreenCapture.CaptureScreenshot("Temp/ArchiveWorld/Night_Entrance.png");
            Vector3 before=world.secondary.transform.position;
            yield return world.entranceSeconds;
            Check(!session.State.worldTime.secondaryReleased&&world.secondary.EntranceHeld&&Vector3.Distance(before,world.secondary.transform.position)<.01f,"The warning follows the visible entrance without early pursuit or teleporting");
            UnityEngine.ScreenCapture.CaptureScreenshot("Temp/ArchiveWorld/Night_Warning.png");yield return .1f;
            float remaining=(float)(session.State.worldTime.arrivalStartedAt+world.entranceSeconds+world.warningSeconds-ArchiveTopicSession.Clock);
            if(remaining>0)yield return remaining+.3f;else yield return .3f;
            Check(session.State.worldTime.secondaryReleased&&!world.secondary.EntranceHeld,"Host releases the second guardian after the full warning");
            Check(world.secondary.State==KhezuHuntState.Hunting||world.secondary.State==KhezuHuntState.NavigationBlocked||world.secondary.State==KhezuHuntState.WaitingForPlayers,"Second guardian enters its existing hunting state machine");
            Check(world.moon.intensity>0&&world.sun.intensity<day*.3f,"Night includes moonlight and reduced sunlight");
            Check(world.NightAmbiencePlaying,"Night wind and eerie cracking ambience play through the existing SFX routing");
            if(living!=null)Check(living.IsNight&&living.VisibleMoteCount<=18&&living.AnimatedCanopyCount<=8&&living.ActiveAmbientVoiceCount<=2,
                "Night fireflies, leaf wind and localized voices keep their mobile budgets after the entrance");
            Check(session.CrystalBonusTopics.Length==0,"Admin night previews do not award crystal bonuses");
            var checkpoint=JsonUtility.FromJson<ArchiveTeamState>(JsonUtility.ToJson(session.State));
            Check(Math.Abs(checkpoint.worldTime.arrivalStartedAt-session.State.worldTime.arrivalStartedAt)<.000001&&checkpoint.worldTime.secondaryReleased,"Checkpoint preserves entrance and release times without replaying (within one microsecond)");
        }
    }
}
