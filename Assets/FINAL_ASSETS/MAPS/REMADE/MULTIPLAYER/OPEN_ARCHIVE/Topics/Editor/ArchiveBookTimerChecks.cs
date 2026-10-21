using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveBookTimerChecks
    {
        [Serializable] sealed class Report
        {
            public bool passed;public string scope="Offline Unity Play Mode: real Book/rune interactions, shared clock, timeout races and reset, Book animation, local world-label screen bounds. Deadlines are shortened by the QA harness to test expiry without a six-minute wait. Physical Android not verified.";
            public List<string> checks=new List<string>(),errors=new List<string>();
        }
        static Report report;
        static IEnumerator flow;
        static ArchiveTopicSession session;
        static double next;
        static bool huntingEnabled;
        public static string Status {get;private set;}="Idle";
        public static void Begin()
        {
            if(!Application.isPlaying||Photon.Pun.PhotonNetwork.InRoom||flow!=null)throw new InvalidOperationException("Use offline Play Mode.");
            session=ArchiveTopicSession.Current;report=new Report();huntingEnabled=session.hunting.enabled;
            session.hunting.Request(LogicLegends.OpenArchive.Hunting.KhezuCommand.Sleep);session.hunting.enabled=false;
            flow=Run();next=0;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            if(flow==null)return;EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup<next)return;
            try{if(!Application.isPlaying)throw new InvalidOperationException("Play Mode stopped.");if(!flow.MoveNext()){Finish(true);return;}next=EditorApplication.timeSinceStartup+(flow.Current is float delay?delay:0);}
            catch(Exception e){report.errors.Add(Status+": "+e);Finish(false);}
        }
        static void Check(bool pass,string name){if(!pass)throw new InvalidOperationException(name);report.checks.Add(name);}
        static void Finish(bool pass)
        {
            EditorApplication.update-=Tick;flow=null;report.passed=pass&&report.errors.Count==0;Status=report.passed?"Passed":"Failed";
            if(session!=null&&Application.isPlaying){session.hunting.enabled=huntingEnabled;session.hunting.Request(LogicLegends.OpenArchive.Hunting.KhezuCommand.Sleep);ArchiveTopicUI.Current.Close();}
            File.WriteAllText(ArchiveTopicSetup.Folder+"/Testing/Reports/T1BookTimer.json",JsonUtility.ToJson(report,true));Debug.Log("Book and timer checks: "+Status+" ("+report.checks.Count+").");
        }
        static IEnumerator Run()
        {
            Status="Book rest and activation";session.Initialize(271828);yield return .25f;
            var station=session.stations[0];var state=session.State.stations[0];var book=station.GetComponentInChildren<ArchiveAnimatedBook>();
            Check(FindObjects().Length==10,"All ten topic kits inherit the same rebuilt Book Statue asset");
            Check(book!=null&&book.OpenAmount==0&&Quaternion.Angle(book.coverHinge.localRotation,Quaternion.identity)<.01f,"Inactive Book is closed on its pedestal");
            Check(!state.timerRunning&&state.timerAttempt==0&&Mathf.Abs(session.TimerRemaining(state)-360)<.01f,"Approaching an inactive T1 leaves six minutes unstarted");
            ArchiveTopicPlayChecks.MoveTo(station,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);yield return .2f;
            ArchiveTopicUI.Current.Open(station);yield return .15f;Check(!state.timerRunning,"Reading the preview does not start or reset the challenge clock");ArchiveTopicUI.Current.Close();
            station.points.First(p=>p.role==ArchivePointRole.Book).Interact();yield return .95f;
            double deadline=state.timerDeadline;
            Check(state.phase==1&&state.timerRunning&&state.timerActor==-1&&state.timerAttempt==1&&session.TimerRemaining(state)>355,"First physical Book activation starts the shared 360-second attempt");
            Check(book.OpenAmount>.99f&&Quaternion.Angle(book.coverHinge.localRotation,Quaternion.identity)>150&&book.bookRig.localPosition.y>1.48f,"Active Book opens and floats smoothly above the lectern");
            float start=Time.unscaledTime;
            while(!book.turningPage.gameObject.activeSelf&&Time.unscaledTime-start<3)yield return .05f;
            Check(book.turningPage.gameObject.activeSelf&&book.turningPage.sharedMesh.vertexCount==52,"Active Book renders a curled double-sided turning page");
            Vector3 before=book.turningPage.sharedMesh.vertices[20];yield return .12f;
            Check((before-book.turningPage.sharedMesh.vertices[20]).sqrMagnitude>.0001f,"Paper vertices move during the page turn");
            ArchiveTopicUI.Current.Close();yield return .15f;Check(state.timerDeadline==deadline,"Closing and reopening the reader never extends the deadline");
            Status="Answer and inter-challenge clock";
            var q=session.Question(0);int wrong=(q.correctChoice+1)%4;
            ArchiveTopicPlayChecks.MoveTo(station,station.ItemPosition(wrong,q));session.Request(0,ArchiveAction.BeginChoice,wrong);yield return session.answerChannelSeconds+.3f;
            Check(state.round==0&&session.State.mistakes.Count==1&&state.timerDeadline==deadline,"Wrong answer records one mistake without resetting the clock");
            ArchiveTopicPlayChecks.MoveTo(station,station.ItemPosition(q.correctChoice,q));session.Request(0,ArchiveAction.BeginChoice,q.correctChoice);yield return session.answerChannelSeconds+.3f;
            Check(state.round==1&&state.phase==0&&state.timerRunning&&state.timerDeadline==deadline,"Solved challenge is kept while the same clock runs between challenges");
            yield return .8f;Check(book.OpenAmount==0,"Book returns to its closed resting pose between challenges");
            float remaining=session.TimerRemaining(state);yield return .2f;Check(session.TimerRemaining(state)<remaining,"Waiting for the next Book activation still consumes shared team time");
            ArchiveTopicPlayChecks.MoveTo(station,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);station.points.First(p=>p.role==ArchivePointRole.Book).Interact();yield return .2f;ArchiveTopicUI.Current.Close();
            Check(state.round==1&&state.timerDeadline==deadline&&state.timerAttempt==1,"Next Book activation uses the original shared deadline");
            Status="Pending answer timeout";q=session.Question(0);string prompt=q.prompt;
            ArchiveTopicPlayChecks.MoveTo(station,station.ItemPosition(q.correctChoice,q));session.Request(0,ArchiveAction.BeginChoice,q.correctChoice);
            state.timerDeadline=ArchiveTopicSession.TimerClock-.01;yield return .3f;
            Check(state.timerExpired&&!state.timerRunning&&state.round==1&&state.phase==0&&state.channelActor==0,"Expiry cancels the unfinished rune without losing a solved challenge");
            Check(session.State.mistakes.Count==2&&session.State.mistakes.Last().actor==-1&&session.State.mistakes.Last().reason=="timeout"&&state.timerTimeouts==1,"One timeout is attributed to the player who activated the challenge");
            yield return .5f;Check(session.State.mistakes.Count==2,"Expired clock cannot charge repeatedly on later updates");
            Check(session.TimerRemaining(state)==0&&session.Question(0).prompt==prompt,"Timeout retains the unfinished question and shows zero time");
            ArchiveTopicUI.Current.Open(station);yield return .15f;
            Check(ArchiveTopicUI.Current.transform.Find("T1_Question_Reader/Shared_T1_Timer").GetComponent<TMPro.TMP_Text>().text.Contains("00:00"),"Open reader displays the authoritative expired timer");ArchiveTopicUI.Current.Close();
            ArchiveTopicPlayChecks.MoveTo(station,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);station.points.First(p=>p.role==ArchivePointRole.Book).Interact();yield return .2f;ArchiveTopicUI.Current.Close();
            Check(state.round==1&&!state.timerExpired&&state.timerAttempt==2&&session.TimerRemaining(state)>359&&state.timerDeadline!=deadline,"Retry at the Book grants six minutes to finish the remaining challenges");
            var copy=JsonUtility.FromJson<ArchiveTeamState>(JsonUtility.ToJson(session.State)).stations[0];
            Check(Math.Abs(copy.timerDeadline-state.timerDeadline)<.000001&&copy.timerActor==state.timerActor&&copy.timerTimeouts==1&&copy.round==1,"Checkpoint contains deadline, activating player, timeouts and solved progress");
            Status="Five-challenge completion freezes clock";
            for(int round=1;round<5;round++)
            {
                q=session.Question(0);ArchiveTopicPlayChecks.MoveTo(station,station.ItemPosition(q.correctChoice,q));session.Request(0,ArchiveAction.BeginChoice,q.correctChoice);yield return session.answerChannelSeconds+.3f;
                Check(state.round==round+1,"Retry solves challenge "+(round+1));
                if(round<4){ArchiveTopicPlayChecks.MoveTo(station,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);session.Request(0,ArchiveAction.Start);yield return .15f;}
            }
            Check(state.phase==2&&!state.timerRunning&&state.timerSavedRemaining>0,"Team completion stops and saves the remaining time");
            remaining=session.TimerRemaining(state);yield return .3f;Check(session.TimerRemaining(state)==remaining,"Completed timer remains frozen");
            Check(session.State.stations.Skip(1).All(s=>s.phase==0&&s.round==0&&s.timerAttempt==0),"Timer does not alter the other nine topic states");
            Check(!Player.LocalInstance.IsMovementLocked&&Time.timeScale==1,"Book, labels and timer preserve native player movement and world time");
        }
        static ArchiveAnimatedBook[] FindObjects()=>UnityEngine.Object.FindObjectsByType<ArchiveAnimatedBook>(FindObjectsSortMode.None);
    }
}
