using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveSingleTopicChecks
    {
        [Serializable] sealed class Report{public bool passed;public List<string> checks=new List<string>(),errors=new List<string>();public string scope="Offline Unity Play Mode: all ten topic reservations, direct authority bypass attempts, Book gates, timeout retry, UI feedback and checkpoint reload. Completion is tested through all 59 native rounds by AllActivitiesPlayMode.json.";}
        static Report report;static ArchiveTopicSession session;static IEnumerator flow;static double next;static bool huntingEnabled;
        public static string Status{get;private set;}="Idle";
        public static void Begin()
        {
            if(!Application.isPlaying||Photon.Pun.PhotonNetwork.InRoom||flow!=null)throw new InvalidOperationException("Use offline Play Mode.");
            session=ArchiveTopicSession.Current;report=new Report();huntingEnabled=session.hunting.enabled;session.hunting.Request(LogicLegends.OpenArchive.Hunting.KhezuCommand.Sleep);session.hunting.enabled=false;
            flow=Run();next=0;EditorApplication.update+=Tick;
        }
        static void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);report.checks.Add(name);}
        static void Tick()
        {
            EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup<next)return;
            try{if(!Application.isPlaying)throw new InvalidOperationException("Play Mode stopped.");if(!flow.MoveNext()){Finish(true);return;}next=EditorApplication.timeSinceStartup+(flow.Current is float d?d:0);}
            catch(Exception e){report.errors.Add(Status+": "+e);Finish(false);}
        }
        static void Finish(bool ok)
        {
            EditorApplication.update-=Tick;flow=null;report.passed=ok&&report.errors.Count==0;Status=report.passed?"Passed":"Failed";
            if(Application.isPlaying){session.hunting.enabled=huntingEnabled;session.hunting.Request(LogicLegends.OpenArchive.Hunting.KhezuCommand.Sleep);ArchiveTopicUI.Current.Close();}
            File.WriteAllText(ArchiveTopicSetup.Folder+"/Testing/Reports/SingleTopicLock.json",JsonUtility.ToJson(report,true));
        }
        static ArchiveCommand StartCommand(int topic)=>new ArchiveCommand{topic=topic,action=ArchiveAction.Start,revision=session.State.stations[topic].revision,sequence=(session.State.actors.FirstOrDefault(a=>a.actor==-1)?.lastSequence??0)+1};
        static void AtBook(int topic)=>ArchiveTopicPlayChecks.MoveTo(session.stations[topic],ArchiveTopicStation.BookPosition+Vector3.back*1.7f);
        static IEnumerator Run()
        {
            for(int topic=0;topic<10;topic++)
            {
                Status="T"+(topic+1)+" reservation";session.Initialize(314159);AtBook(topic);yield return .12f;
                Check(session.ActiveTopic==-1&&Enumerable.Range(0,10).All(session.CanStartTopic),"Fresh team can choose any topic: "+(topic+1));
                Check(session.Accept(-1,StartCommand(topic))&&session.ActiveTopic==topic,"First valid Book reserves T"+(topic+1));
                int revision=session.State.revision;
                for(int other=0;other<10;other++)if(other!=topic)
                {
                    AtBook(other);Check(!session.Accept(-1,StartCommand(other))&&session.State.stations[other].phase==0&&session.State.revision==revision,"Authority blocks T"+(other+1)+" while T"+(topic+1)+" is unfinished");
                }
                Check(session.State.mistakes.Count==0&&session.hunting.Mistakes==0,"Blocked Books charge no mistake: T"+(topic+1));
                AtBook(topic);yield return .12f;
            }
            Status="Book gates and timeout";session.Initialize(104729);AtBook(0);session.Request(0,ArchiveAction.Start);yield return .2f;
            var state=session.State.stations[0];var q=session.Question(0);ArchiveTopicPlayChecks.MoveTo(session.stations[0],session.stations[0].ItemPosition(q.correctChoice,q));session.Request(0,ArchiveAction.BeginChoice,q.correctChoice);yield return session.answerChannelSeconds+.3f;
            Check(state.round==1&&state.phase==0&&session.ActiveTopic==0,"Solved round retains the reservation while waiting at the next Book");
            AtBook(1);Check(!session.Accept(-1,StartCommand(1)),"Other topic stays locked between T1 challenges");
            session.State.stations[0].timerDeadline=ArchiveTopicSession.TimerClock-.01;yield return .3f;
            Check(state.timerExpired&&state.phase==0&&session.ActiveTopic==0,"Timeout keeps the unfinished topic reserved");
            Check(!session.Accept(-1,StartCommand(1)),"Timeout cannot be used to switch topics");
            ArchiveTopicUI.Current.Open(session.stations[1]);yield return .12f;
            // Locate by visible label as the existing button helper keeps spaces in object names.
            var button=ArchiveTopicUI.Current.GetComponentsInChildren<Button>().First(b=>b.GetComponentInChildren<TMPro.TMP_Text>()?.text=="Start / Read");
            Check(!button.interactable&&ArchiveTopicUI.Current.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text.Contains("Finish T1")),"Blocked Book disables Start and names the unfinished topic");
            ArchiveTopicUI.Current.Close();AtBook(0);session.Request(0,ArchiveAction.Start);yield return .2f;
            Check(state.phase==1&&state.round==1&&state.timerAttempt==2&&session.ActiveTopic==0,"Same topic can retry without losing solved progress");
            var restore=typeof(ArchiveTopicSession).GetMethod("Receive",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            string json=JsonUtility.ToJson(session.State);restore.Invoke(session,new object[]{json,true});
            Check(session.ActiveTopic==0&&!session.CanStartTopic(1),"Checkpoint restoration retains the team reservation");
            session.Initialize(271828);AtBook(1);session.Request(1,ArchiveAction.Start);AtBook(0);yield return .2f;ArchiveTopicUI.Current.Open(session.stations[0]);yield return .15f;
            Check(ArchiveTopicUI.Current.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text.Contains("Finish T2")),"T1 reader also identifies the other active topic");
            Check(!Player.LocalInstance.IsMovementLocked&&Time.timeScale==1,"Topic reservation leaves native movement and world time active");
        }
    }
}
