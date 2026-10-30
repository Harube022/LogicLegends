using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Opt-in fixtures exercise the existing authoritative clock, never a second test timer.</summary>
    public static class ArchiveMatchTimeChecks
    {
        [Serializable] sealed class Report
        {
            public bool passed;
            public string scope="Fresh offline MultiplayerGameplay Editor Play: production clock transitions, actual final T1 validator, ten-topic award receipt fixtures, real Admin no-farming, command/portal expiry ordering, checkpoint receive/master-restore callbacks, timescale-zero passage, and unchanged hearts. Clock timestamps are shortened in the authoritative state to exercise long deadlines. Live Photon, rendered remote clients, physical Android, and a naturally played 40-minute match are not claimed.";
            public List<string> checks=new List<string>(),errors=new List<string>();
        }
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static ArchiveTopicSession session;
        static Report report;
        static IEnumerator flow;
        static double next;
        static bool wasEnabled,wasHunting,wasDefeatSuppressed,wasThreatSuppressed;
        static float originalTimeScale;
        static Vector3 originalPosition;
        public static string Status{get;private set;}="Idle";

        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Overall Match Clock (Offline Play)")]
        public static void Begin()
        {
            if(!Application.isPlaying||Photon.Pun.PhotonNetwork.InRoom||flow!=null)throw new InvalidOperationException("Use fresh offline MultiplayerGameplay Play Mode.");
            session=ArchiveTopicSession.Current;
            if(session==null||!session.Ready||Player.LocalInstance==null||session.hunting?.health==null)throw new InvalidOperationException("Saved MultiplayerGameplay, player and health must be ready.");
            report=new Report();wasEnabled=session.enabled;wasHunting=session.hunting.enabled;wasDefeatSuppressed=session.suppressDefeatForVerification;
            wasThreatSuppressed=session.worldProgression!=null&&session.worldProgression.suppressThreatsForVerification;
            originalPosition=Player.LocalInstance.transform.position;originalTimeScale=Time.timeScale;
            session.RequestAdminTest(ArchiveAdminTestAction.RemoveTeammate);
            session.hunting.Request(Hunting.KhezuCommand.Sleep);session.hunting.enabled=false;session.enabled=false;session.suppressDefeatForVerification=true;
            if(session.worldProgression!=null)session.worldProgression.suppressThreatsForVerification=true;
            flow=Run();next=0;EditorApplication.update+=Tick;
        }
        static object Invoke(string name,params object[] values)
        {
            var method=typeof(ArchiveTopicSession).GetMethod(name,Private);
            if(method==null)throw new MissingMethodException(typeof(ArchiveTopicSession).Name,name);
            try{return method.Invoke(session,values);}catch(TargetInvocationException e){throw e.InnerException??e;}
        }
        static bool ClockTick()=>(bool)Invoke("TickMatchTime");
        static bool Award(int topic,int actor=-1)=>(bool)Invoke("AwardMatchTimeForTopic",topic,actor);
        static void Check(bool passed,string label)
        {report.checks.Add((passed?"PASS ":"FAIL ")+label);if(!passed)throw new InvalidOperationException(label);}
        static bool Close(double a,double b)=>Math.Abs(a-b)<.002;
        static ArchiveMatchTimeState FreshActive()
        {
            session.Initialize(20261030);
            var state=session.State.matchTime;
            state.phase=ArchiveMatchTimePhase.Active;state.startedAt=Math.Max(0,ArchiveTopicSession.Clock-1);state.deadline=state.startedAt+ArchiveTopicSession.MatchBaseSeconds;
            state.escapeStartedAt=state.escapeDeadline=state.expiredAt=-1;
            return state;
        }
        static void SolvedReceipt(int topic,int actor=-1)
        {
            var state=session.State.stations[topic];state.phase=2;state.round=ArchiveTopicCatalog.RoundCount(topic);
            state.lastAnswerResult=1;state.lastAnswerActor=actor;state.lastAnswerRound=state.round-1;state.lastAnswerAt=ArchiveTopicSession.Clock;
        }
        static void AllSolved(){for(int i=0;i<10;i++)SolvedReceipt(i);}
        static string Hearts()=>string.Join(";",session.hunting.health.ExportActorHealth().OrderBy(h=>h.actor).Select(h=>h.actor+":"+h.hearts+":"+h.lastPenaltySerial));
        static void Move(Vector3 position)
        {
            var player=Player.LocalInstance;var capsule=player.GetComponent<CharacterController>();bool enabled=capsule.enabled;capsule.enabled=false;
            player.transform.position=position;player.ResetMotionAfterTeleport();capsule.enabled=enabled;Physics.SyncTransforms();
        }
        static IEnumerator Run()
        {
            Status="Readiness and one shared start";
            Check(ArchiveTopicSession.MatchBaseSeconds==2400&&ArchiveTopicSession.MatchTopicBonusSeconds==120&&ArchiveTopicSession.MatchEscapeSeconds==120,"Policy is 40 minutes, 2 minutes per unique topic, and 2 minutes for escape");
            Check(ArchiveTopicSession.MatchMaximumPlaySeconds==3720,"Overall ceiling is 60 minutes of academic play plus at most two final escape minutes");
            session.Initialize(20261030);var time=session.State.matchTime;
            Check(time!=null&&time.startedAt<0&&time.awardedTopics.Count==0,"New existing team checkpoint owns an unstarted clock and empty award ledger");
            time.phase=ArchiveMatchTimePhase.WaitingForPlayers;time.readinessDeadline=ArchiveTopicSession.Clock+15;time.countdownStartedAt=-1;
            ClockTick();Check(time.phase==ArchiveMatchTimePhase.Countdown&&time.startedAt<0&&!session.MatchTimeAllowsGameplay,"Ready player starts one five-second countdown before gameplay");
            double countdown=time.countdownStartedAt;ClockTick();Check(time.countdownStartedAt==countdown,"Repeated readiness ticks never restart the countdown");
            time.countdownStartedAt=ArchiveTopicSession.Clock-5.25;countdown=time.countdownStartedAt;ClockTick();
            Check(time.phase==ArchiveMatchTimePhase.Active&&Close(time.startedAt,countdown+5)&&Close(time.deadline,time.startedAt+2400)&&session.MatchTimeAllowsGameplay,"Late countdown update anchors the 40-minute deadline to its original start");
            session.Initialize(20261030);time=session.State.matchTime;time.phase=ArchiveMatchTimePhase.WaitingForPlayers;time.readinessDeadline=ArchiveTopicSession.Clock-7;ClockTick();
            Check(time.readinessTimedOut&&time.phase==ArchiveMatchTimePhase.Active&&Close(time.startedAt,time.readinessDeadline+5),"Readiness fallback uses its stored deadline and cannot postpone the match indefinitely");

            Status="Academic awards and Admin farming rejection";
            time=FreshActive();double deadline=time.deadline;
            var station=session.State.stations[0];station.round=ArchiveTopicCatalog.RoundCount(0)-1;station.phase=1;
            var question=session.Question(0);station.selection=question.correctChoice;
            Invoke("Submit",-1,1,station,question);
            Check(station.phase==2&&station.lastAnswerResult==1&&session.MatchTimeBonusCount==1&&Close(time.deadline,deadline+120),"Actual accepted final T1 submission grants exactly 120 seconds");
            Check(!Award(0)&&Close(time.deadline,deadline+120),"Replaying a final academic receipt cannot grant its topic twice");
            Check(session.RequestAdminTest(ArchiveAdminTestAction.ResetTopic,0),"Existing Admin reset remains usable for a fixture");
            Check(session.RequestAdminTest(ArchiveAdminTestAction.CompleteTopic,0)&&session.MatchTimeBonusCount==1&&Close(time.deadline,deadline+120),"Admin reset and completion neither clear nor refill the one-time award ledger");
            SolvedReceipt(1);session.State.stations[1].lastAnswerResult=-1;
            Check(!Award(1)&&!Award(1,0)&&!Award(-1)&&!Award(10),"Incorrect, unattributed and out-of-range final receipts never award time");
            SolvedReceipt(1);Check(!Award(1,-2),"A different actor cannot claim another final solver's award");
            session.State.stations[1].lastAnswerAt=time.startedAt-1;
            Check(!Award(1),"A stale final receipt from before this match cannot award time");
            SolvedReceipt(1);
            for(int topic=1;topic<10;topic++){SolvedReceipt(topic);Check(Award(topic),"Unique academic final receipt awards T"+(topic+1));}
            Check(session.MatchTimeBonusCount==10&&time.awardedActors.All(a=>a==-1)&&Close(time.deadline,time.startedAt+3600),"Ten unique topics cap normal play at 60 minutes with solver attribution");
            Check(!Award(9)&&Close(time.deadline,time.startedAt+3600),"Additional receipts cannot exceed the ten-topic cap");

            Status="Final escape monotonicity and checkpoint restoration";
            double normalEnd=time.deadline;ClockTick();
            Check(time.phase==ArchiveMatchTimePhase.Escape&&Close(time.escapeDeadline-time.escapeStartedAt,120)&&time.deadline==normalEnd,"Tenth completion switches to exactly two final escape minutes without refilling normal time");
            double escapeEnd=time.escapeDeadline;double escapeStart=time.escapeStartedAt;ClockTick();
            Check(time.escapeDeadline==escapeEnd&&time.escapeStartedAt==escapeStart&&!Award(9),"Repeated ticks and award attempts cannot restart or extend escape");
            string checkpoint=JsonUtility.ToJson(session.State);var copy=JsonUtility.FromJson<ArchiveTeamState>(checkpoint);
            Check(Close(copy.matchTime.deadline,normalEnd)&&Close(copy.matchTime.escapeDeadline,escapeEnd)&&copy.matchTime.awardedTopics.Count==10,
                "JSON checkpoint preserves deadlines and unique award receipts; normal expected="+normalEnd.ToString("R")+" actual="+copy.matchTime.deadline.ToString("R")+" escape expected="+escapeEnd.ToString("R")+" actual="+copy.matchTime.escapeDeadline.ToString("R"));
            session.Initialize(123);Invoke("Receive",checkpoint,true);time=session.State.matchTime;
            Check(Close(time.escapeDeadline,escapeEnd)&&Close(time.escapeStartedAt,escapeStart)&&session.MatchTimeBonusCount==10,
                "Existing snapshot receive restores the saved clock without a local refill; end expected="+escapeEnd.ToString("R")+" actual="+time.escapeDeadline.ToString("R")+" start expected="+escapeStart.ToString("R")+" actual="+time.escapeStartedAt.ToString("R")+" bonuses="+session.MatchTimeBonusCount);
            session.OnMasterClientSwitched(null);time=session.State.matchTime;
            Check(Close(time.escapeDeadline,escapeEnd)&&Close(time.deadline,normalEnd),
                "Existing master-restore callback retains original deadlines; normal expected="+normalEnd.ToString("R")+" actual="+time.deadline.ToString("R")+" escape expected="+escapeEnd.ToString("R")+" actual="+time.escapeDeadline.ToString("R"));
            Check(time.startedAt>=0&&Close(session.MatchTimeAbsoluteEnd,time.startedAt+3720),"A valid production start exposes its fixed 62-minute absolute end");
            time.startedAt=ArchiveTopicSession.Clock-ArchiveTopicSession.MatchMaximumPlaySeconds+30;time.escapeDeadline=ArchiveTopicSession.Clock+120;
            Check(session.MatchTimeRemaining<=30.01f&&session.MatchTimeRemaining>29.5f,"Final escape remaining time respects the absolute 62-minute ceiling even for a late completion");

            Status="Pause and supply actions cannot refill time";
            time=FreshActive();deadline=time.deadline;float remaining=session.MatchTimeRemaining;
            Time.timeScale=0;yield return .3f;
            Check(time.deadline==deadline&&session.MatchTimeRemaining<remaining-.12f,"Local zero-timescale pause still consumes unscaled match time");Time.timeScale=originalTimeScale;
            Check(session.RequestAdminTest(ArchiveAdminTestAction.GrantAllItems,-1)&&time.deadline==deadline,"Item grants and supplies never refill the shared match timer");
            string hearts=Hearts();time.deadline=ArchiveTopicSession.Clock;int mistakes=session.State.mistakes.Count;ClockTick();
            Check(session.MatchTimeExpired&&session.TeamDefeated&&session.State.survival.defeatReason==ArchiveTopicSession.MatchTimeoutReason&&!session.TeamWon,"Exact deadline expiry becomes one timeout defeat instead of victory");
            Check(Hearts()==hearts&&session.State.mistakes.Count==mistakes,"Match timeout deducts no hearts and invents no academic mistakes");
            double expiredAt=time.expiredAt;ClockTick();Check(time.expiredAt==expiredAt&&session.MatchTimeRemaining==0&&!session.MatchTimeAllowsGameplay,"Expired state is monotonic and cannot restart gameplay");
            time=FreshActive();AllSolved();time.deadline=ArchiveTopicSession.Clock;
            Check(!Award(9),"A final academic receipt at the deadline cannot rescue an expired normal clock");
            ClockTick();Check(session.TeamDefeated&&time.phase==ArchiveMatchTimePhase.Expired&&time.escapeStartedAt<0,"Tenth completion at expiry cannot create a fresh escape window");

            Status="Expiry wins command and portal races";
            time=FreshActive();station=session.State.stations[0];station.phase=1;station.round=4;
            question=session.Question(0);station.selection=question.correctChoice;time.deadline=ArchiveTopicSession.Clock;
            Invoke("Submit",-1,88,station,question);
            Check(session.TeamDefeated&&station.phase==1&&station.round==4&&session.State.solved.Count==0&&session.MatchTimeBonusCount==0,
                "A held rune completing at the shared deadline cannot grade an answer or award time");
            time=FreshActive();station=session.State.stations[0];Move(session.stations[0].transform.TransformPoint(ArchiveTopicStation.BookPosition+Vector3.back));
            var command=new ArchiveCommand{topic=0,revision=station.revision,sequence=77,action=ArchiveAction.Start};Invoke("StampCommandContext",command);time.deadline=ArchiveTopicSession.Clock;
            Check(!session.Accept(-1,command)&&session.MatchTimeExpired&&session.TeamDefeated&&station.phase==0,"A valid Book command at the deadline expires before it can start work");
            time=FreshActive();AllSolved();time.phase=ArchiveMatchTimePhase.Escape;time.escapeStartedAt=ArchiveTopicSession.Clock-120;time.escapeDeadline=ArchiveTopicSession.Clock;
            var portal=session.exitPortal;Check(portal!=null,"Saved portal exists for the physical aperture race");
            session.State.exit.opened=true;session.State.exit.openedAt=ArchiveTopicSession.Clock-portal.formationSeconds-1;Move(portal.transform.TransformPoint(Vector3.up*3));
            Check(portal.Formed&&portal.ContainsPlayer(Player.LocalInstance),"Deadline probe is physically inside the formed portal");
            Invoke("AdvanceExitProgress");
            Check(session.MatchTimeExpired&&session.TeamDefeated&&!session.TeamWon&&session.State.exit.escaped.Count==0,"Portal entry at expiry cannot beat the timeout or publish an escape receipt");
            time=FreshActive();AllSolved();time.phase=ArchiveMatchTimePhase.Escape;time.escapeStartedAt=ArchiveTopicSession.Clock;time.escapeDeadline=ArchiveTopicSession.Clock+120;
            session.State.exit.opened=true;session.State.exit.openedAt=ArchiveTopicSession.Clock-portal.formationSeconds-1;Move(portal.transform.TransformPoint(Vector3.up*3));Invoke("AdvanceExitProgress");
            Check(session.TeamWon&&!session.TeamDefeated&&session.HasEscapedActor(-1),"Final living survivor may win through the real aperture before expiry");
            double wonAt=session.State.exit.wonAt;time.escapeDeadline=ArchiveTopicSession.Clock-1;ClockTick();
            Check(session.TeamWon&&!session.TeamDefeated&&session.State.exit.wonAt==wonAt,"A completed victory cannot turn into a later timeout defeat");
        }
        static void Tick()
        {
            if(flow==null)return;EditorApplication.QueuePlayerLoopUpdate();
            try{if(!Application.isPlaying)throw new InvalidOperationException("Play Mode ended.");if(EditorApplication.timeSinceStartup<next)return;if(!flow.MoveNext()){Finish(true);return;}next=EditorApplication.timeSinceStartup+(flow.Current is float delay?delay:0);}
            catch(Exception e){report.errors.Add(Status+": "+e);Finish(false);}
        }
        static void Finish(bool passed)
        {
            EditorApplication.update-=Tick;flow=null;Time.timeScale=originalTimeScale;
            if(Application.isPlaying&&session!=null)
            {
                session.Initialize(20261030);Move(originalPosition);session.enabled=wasEnabled;session.hunting.enabled=wasHunting;
                session.suppressDefeatForVerification=wasDefeatSuppressed;
                if(session.worldProgression!=null)session.worldProgression.suppressThreatsForVerification=wasThreatSuppressed;
            }
            report.passed=passed&&report.errors.Count==0;Status=report.passed?"Passed":"Failed";
            Directory.CreateDirectory("Temp/ArchiveMatchTime");File.WriteAllText("Temp/ArchiveMatchTime/Play.json",JsonUtility.ToJson(report,true));
            Debug.Log("Overall match clock checks: "+Status+" ("+report.checks.Count+").");
        }
    }
}
