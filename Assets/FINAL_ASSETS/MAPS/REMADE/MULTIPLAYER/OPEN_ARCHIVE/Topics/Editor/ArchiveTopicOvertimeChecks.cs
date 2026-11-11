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
    /// <summary>Focused replacement for the old hard-timeout expectations. No device or live-room claims.</summary>
    public static class ArchiveTopicOvertimeChecks
    {
        [Serializable] public sealed class Report
        {
            public bool passed;
            public string scope="Offline Unity Play Mode. Real Book/hint/choice commands plus accepted solution fixtures through the existing grader. Shortened authoritative deadlines, checkpoint restoration, native cues, all ten topic types. Android and real multi-device play not tested.";
            public List<string> checks=new List<string>(),errors=new List<string>();
        }
        static Report report;static IEnumerator flow;static ArchiveTopicSession session;static double next;
        static bool huntingEnabled;static string output;
        static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        public static string Status{get;private set;}="Idle";
        public static string ReportJson=>report==null?"":JsonUtility.ToJson(report,true);
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Verify Topic Overtime (Offline Play Mode)")]
        public static void Menu()=>Begin();
        public static void Begin(string outputPath="Temp/ArchiveTopicOvertime/Report.json")
        {
            if(!Application.isPlaying||Photon.Pun.PhotonNetwork.InRoom||flow!=null)throw new InvalidOperationException("Use a fresh offline MultiplayerGameplay Play Mode session.");
            session=ArchiveTopicSession.Current;if(session==null||!session.Ready||!session.CanAct(session.LocalActor))throw new InvalidOperationException("Wait for the player introduction to finish.");
            report=new Report();output=outputPath;huntingEnabled=session.hunting.enabled;
            session.hunting.Request(LogicLegends.OpenArchive.Hunting.KhezuCommand.Sleep);session.hunting.enabled=false;
            flow=Run();next=0;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            if(flow==null)return;EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup<next)return;
            try{if(!Application.isPlaying)throw new InvalidOperationException("Play Mode stopped.");if(!flow.MoveNext()){Finish(true);return;}next=EditorApplication.timeSinceStartup+(flow.Current is float delay?delay:0);}
            catch(Exception e){report.errors.Add(Status+": "+e);Finish(false);}
        }
        static void Finish(bool pass)
        {
            EditorApplication.update-=Tick;flow=null;report.passed=pass&&report.errors.Count==0;Status=report.passed?"Passed":"Failed";
            if(session!=null&&Application.isPlaying){session.hunting.enabled=huntingEnabled;session.hunting.Request(LogicLegends.OpenArchive.Hunting.KhezuCommand.Sleep);ArchiveTopicUI.Current.Close();}
            string directory=Path.GetDirectoryName(output);if(!string.IsNullOrEmpty(directory))Directory.CreateDirectory(directory);
            File.WriteAllText(output,ReportJson);Debug.Log("Topic overtime: "+Status+" ("+report.checks.Count+" checks).");
        }
        static void Check(bool pass,string text){if(!pass)throw new InvalidOperationException(text);report.checks.Add(text);}
        static void Move(int topic,Vector3 point)=>ArchiveTopicPlayChecks.MoveTo(session.stations[topic],point);
        static void Book(int topic){Move(topic,ArchiveTopicStation.BookPosition+Vector3.back*1.5f);session.stations[topic].points.First(p=>p.role==ArchivePointRole.Book).Interact();}
        static void Reset(int topic)
        {
            ArchiveTopicUI.Current.Close();foreach(var state in session.State.stations.Where(s=>s.phase!=0||s.timerAttempt>0).ToArray())session.RequestAdminTest(ArchiveAdminTestAction.ResetTopic,state.topic);
            session.RequestAdminTest(ArchiveAdminTestAction.ResetTopic,topic);session.hunting.Request(LogicLegends.OpenArchive.Hunting.KhezuCommand.Sleep);Move(topic,ArchiveTopicStation.BookPosition+Vector3.back*1.5f);
        }
        static bool Expire(ArchiveStationState state)=> (bool)typeof(ArchiveTopicSession).GetMethod("ExpireTopicTimer",Private).Invoke(session,new object[]{state,true});
        static string Work(ArchiveStationState s)=>JsonUtility.ToJson(new WorkSnapshot{round=s.round,subphase=s.subphase,selection=s.selection,flags=s.flags,placements=s.placements,links=s.links,route=s.route,carries=s.carries,routeActor=s.routeActor,channelActor=s.channelActor,channelChoice=s.channelChoice,channelStarted=s.channelStarted,routeEdge=s.routeEdge,routeCrossedMiddle=s.routeCrossedMiddle,routeAtVertex=s.routeAtVertex,continuous=s.continuous});
        [Serializable] sealed class WorkSnapshot
        {
            public int round,subphase,selection,flags,routeActor,channelActor,channelChoice,routeEdge;
            public double channelStarted;public bool routeCrossedMiddle,routeAtVertex,continuous;
            public int[] placements;public List<int> links,route;public List<ArchiveCarry> carries;
        }
        static void SolveFixture(int topic)
        {
            var q=session.Question(topic);var state=session.State.stations[topic];var solution=ArchiveTopicChecks.Solution(q);
            state.placements=solution.placements;state.links=solution.links;state.route=solution.route;state.selection=solution.selection;state.flags=solution.flags;
            state.carries.Clear();state.channelActor=0;state.routeAtVertex=solution.routeAtVertex;state.continuous=true;
            typeof(ArchiveTopicSession).GetMethod("Submit",Private).Invoke(session,new object[]{session.LocalActor,0,state,q});
        }
        static IEnumerator Run()
        {
            Status="All topic clocks preserve work across zero";
            for(int topic=0;topic<10;topic++)
            {
                Reset(topic);yield return .15f;var state=session.State.stations[topic];
                Check(!state.timerRunning&&!state.timerOvertime,"T"+(topic+1)+" waits for Book activation");Book(topic);yield return .15f;
                Check(state.phase==1&&state.timerRunning,"T"+(topic+1)+" starts through the existing Book command");
                var fixture=ArchiveTopicChecks.Solution(session.Question(topic));
                if(state.placements.Length>0)state.placements[0]=fixture.placements[0];state.selection=fixture.selection;state.flags=fixture.flags;
                state.links=fixture.links.Take(1).ToList();state.route=fixture.route.Take(2).ToList();
                string work=Work(state);int count=session.State.mistakes.Count,personal=session.State.actors.Sum(a=>a.mistakes),revision=state.revision;
                int hearts=session.hunting.health.ActorHearts(session.LocalActor);double matchDeadline=session.State.matchTime.deadline;
                state.timerDeadline=ArchiveTopicSession.TimerClock-.05;
                Check(Expire(state)&&state.phase==1&&state.timerRunning&&state.timerOvertime&&!state.timerExpired,"T"+(topic+1)+" enters overtime without a retry gate");
                Check(Work(state)==work&&state.revision==revision,"T"+(topic+1)+" keeps the puzzle and accepts in-flight answer revisions");
                Check(session.State.mistakes.Count==count+1&&session.State.mistakes.Last().actor==0&&session.State.mistakes.Last().reason=="timeout"&&state.timerTimeouts==1,"T"+(topic+1)+" records one team timeout");
                Check(session.State.actors.Sum(a=>a.mistakes)==personal&&session.hunting.health.ActorHearts(session.LocalActor)==hearts,"T"+(topic+1)+" assigns no personal blame or direct heart loss");
                Check(!Expire(state)&&session.State.mistakes.Count==count+1,"T"+(topic+1)+" cannot repeatedly charge overtime");
                Check(session.ActiveTopic==topic&&Enumerable.Range(0,10).Where(i=>i!=topic).All(i=>!session.CanStartTopic(i)),"T"+(topic+1)+" still reserves the active team topic");
                Check(session.State.matchTime.deadline==matchDeadline&&session.TimerRemaining(state)==0&&ArchiveTopicTimerPresentation.Format(session,state).StartsWith("+"),"T"+(topic+1)+" does not extend the shared match deadline");
                string json=JsonUtility.ToJson(session.State);typeof(ArchiveTopicSession).GetMethod("Receive",Private).Invoke(session,new object[]{json,true});state=session.State.stations[topic];
                Check(state.timerOvertime&&Work(state)==work&&!Expire(state)&&session.State.mistakes.Count==count+1,"T"+(topic+1)+" checkpoint restoration preserves overtime without a second penalty");
                SolveFixture(topic);
                Check(state.lastAnswerResult==1&&(state.round>0||state.subphase>0),"T"+(topic+1)+" accepts its original correct answer during overtime");
                if(topic>=7)
                {
                    float elapsed=session.TopicOvertimeSeconds(state);Check(!state.timerRunning,"T"+(topic+1)+" retains its existing paused Book gate");yield return .15f;
                    Check(session.TopicOvertimeSeconds(state)==elapsed,"T"+(topic+1)+" checkpoints elapsed overtime while paused");Book(topic);yield return .1f;
                    Check(state.timerOvertime&&state.timerRunning&&session.TopicOvertimeSeconds(state)>=elapsed&&state.timerTimeouts==1,"T"+(topic+1)+" resumes overtime without a fresh budget or penalty");
                }
                if(topic==6)
                {
                    Check(state.phase==1&&state.round==1&&state.timerOvertime,"T7 problem two shares the same overtime clock");SolveFixture(topic);
                    Check(state.round==2&&state.phase==0&&!state.timerRunning&&!state.timerOvertime,"T7 clears overtime only after both problems are accepted");Book(topic);
                    Check(state.phase==1&&session.TimerRemaining(state)>state.timerLimit-1&&!state.timerOvertime,"T7 gives the next challenge its existing fresh budget");
                }
            }
            Status="Warning cues and real overtime hints";Reset(5);yield return .2f;Book(5);yield return .25f;
            var timer=session.State.stations[5];var cues=ArchiveTopicUI.Current.GetComponent<ArchiveTopicTimerPresentation>();int warnings=cues.PlayedWarnings;
            timer.timerDeadline=ArchiveTopicSession.TimerClock+59;yield return .25f;Check(cues.PlayedWarnings==warnings+1&&cues.LastWarningSeconds==60,"60-second cue plays once through the existing SFX channel");
            yield return .2f;Check(cues.PlayedWarnings==warnings+1,"60-second cue does not replay every update");
            timer.timerDeadline=ArchiveTopicSession.TimerClock+29;yield return .25f;Check(cues.PlayedWarnings==warnings+2&&cues.LastWarningSeconds==30,"30-second cue plays once");
            timer.timerDeadline=ArchiveTopicSession.TimerClock-.01;yield return .25f;Check(timer.timerOvertime&&cues.PlayedWarnings==warnings+3,"Natural ticking enters overtime and presents its single cue");
            Check(session.CanRequestHint(5)&&ArchiveTopicTimerPresentation.HintCue(session),"Hints remain actionable and highlighted in overtime");int mistakes=session.State.mistakes.Count;double deadline=timer.timerDeadline;
            Check(session.RequestLearningHint(5)&&session.HintLevel(5,session.LocalActor)==1,"The real authenticated hint command succeeds at zero time");
            Check(session.State.mistakes.Count==mistakes&&timer.timerDeadline==deadline,"Overtime hints do not charge mistakes or reset the clock");
            timer.placements=Enumerable.Repeat(-1,timer.placements.Length).ToArray();typeof(ArchiveTopicSession).GetMethod("Submit",Private).Invoke(session,new object[]{session.LocalActor,0,timer,session.Question(5)});
            Check(timer.lastAnswerResult<0&&session.State.mistakes.Count==mistakes+1&&timer.timerDeadline==deadline&&timer.timerTimeouts==1,"Wrong overtime answer still records one normal mistake and no repeated timeout");
            Status="Penalty reaching zero and pending native rune";Reset(6);Book(6);var sets=session.State.stations[6];sets.timerDeadline=ArchiveTopicSession.TimerClock+3;mistakes=session.State.mistakes.Count;
            typeof(ArchiveTopicSession).GetMethod("Submit",Private).Invoke(session,new object[]{session.LocalActor,0,sets,session.Question(6)});
            Check(sets.timerOvertime&&sets.phase==1&&session.State.mistakes.Count==mistakes+1&&session.State.mistakes.Last().reason=="answer","Wrong-answer time penalty reaching zero is not charged twice");
            Reset(0);Book(0);ArchiveTopicUI.Current.Close();var choice=session.State.stations[0];var q=session.Question(0);Move(0,session.stations[0].ItemPosition(q.correctChoice,q));session.Request(0,ArchiveAction.BeginChoice,q.correctChoice);choice.timerDeadline=ArchiveTopicSession.TimerClock-.01;
            yield return session.answerChannelSeconds+.4f;
            Check(choice.round==1&&choice.lastAnswerResult==1&&choice.timerOvertime&&choice.timerTimeouts==1,"A real held rune crosses zero and completes without cancellation");
            Status="Shared deadline remains final";session.RequestAdminTest(ArchiveAdminTestAction.ExpireMatchClock);yield return .15f;
            Check(session.TeamDefeated&&!session.CanAct(session.LocalActor),"Shared match expiry still causes team defeat during topic overtime");
        }
    }
}
