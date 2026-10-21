using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Photon.Pun;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchivePropositionChecks
    {
        [Serializable] sealed class Result
        {
            public bool passed;public int questionVariants,seededQuestions,rounds;
            public string scope="Offline Editor Play Mode: Book and rune interactions, authority range and channel validation, mistake attribution, reader buttons, five-round completion and movement locks. Native OS touch, rendered remote clients and physical Android are not verified.";
            public List<string> checks=new List<string>(),errors=new List<string>();
        }
        static Result report;
        static IEnumerator flow;
        static ArchiveTopicSession session;
        static bool oldHunting;
        static double next;
        public static string Status{get;private set;}="Idle";
        [MenuItem("Tools/Logic Legends/Open Archive/T1/Verify T1 Polish (Play Mode)")]
        public static void Begin()
        {
            if(!Application.isPlaying||PhotonNetwork.InRoom||flow!=null)throw new InvalidOperationException("Use offline Play Mode with no test already running.");
            session=ArchiveTopicSession.Current;if(session==null||!session.Ready)throw new InvalidOperationException("Wait for the topic session.");
            report=new Result();oldHunting=session.hunting.enabled;session.hunting.Request(LogicLegends.OpenArchive.Hunting.KhezuCommand.Sleep);session.hunting.enabled=false;
            next=0;flow=Run();EditorApplication.update+=Tick;Application.logMessageReceived+=Log;
        }
        static void Tick()
        {
            if(flow==null)return;EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup<next)return;
            try{if(!Application.isPlaying)throw new InvalidOperationException("Play Mode stopped.");if(!flow.MoveNext()){Finish(true);return;}next=EditorApplication.timeSinceStartup+(flow.Current is float delay?delay:0);}
            catch(Exception e){report.errors.Add(Status+": "+e);Finish(false);}
        }
        static void Check(bool pass,string name){if(!pass)throw new InvalidOperationException(name);report.checks.Add(name);}
        static void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.errors.Add(message);}
        static void Save(){File.WriteAllText(ArchiveTopicSetup.Folder+"/Testing/Reports/T1Polish.json",JsonUtility.ToJson(report,true));}
        static void Finish(bool complete)
        {
            EditorApplication.update-=Tick;Application.logMessageReceived-=Log;flow=null;
            if(Application.isPlaying&&session!=null){session.hunting.enabled=oldHunting;session.hunting.Request(LogicLegends.OpenArchive.Hunting.KhezuCommand.Sleep);ArchiveTopicUI.Current.Close();}
            report.passed=complete&&report.errors.Count==0;Status=report.passed?"Passed":"Failed";Save();Debug.Log("T1 polish checks: "+Status+" ("+report.checks.Count+").");
        }
        static ArchiveCommand Command(ArchiveAction action,int a=-1)
        {
            var record=session.State.actors.FirstOrDefault(p=>p.actor==-1);
            return new ArchiveCommand{topic=0,revision=session.State.stations[0].revision,sequence=(record?.lastSequence??0)+1,action=action,a=a};
        }
        static IEnumerator Run()
        {
            Status="T1 question content";
            foreach(var bank in session.catalog.propositions)foreach(var q in bank.questions)
            {
                Check(q.options.Length==4&&q.options.Distinct().Count()==4&&q.options.All(o=>o==o.Trim())&&q.options[q.correctOptionIndex]==bank.title,"Unique trimmed choices and audited key: "+bank.title+" #"+(++report.questionVariants));
            }
            for(int seed=0;seed<128;seed++)
            {
                var solved=new HashSet<string>();
                for(int round=0;round<5;round++)
                {
                    var q=ArchiveTopicQuestions.Build(session.catalog,0,round,seed);report.seededQuestions++;
                    if(string.IsNullOrEmpty(q.successText)||!solved.Add(q.items[q.correctChoice]))throw new InvalidOperationException("Explanation or five-connective coverage failed at seed "+seed);
                }
            }
            Check(true,"640 seeded questions cover each of the five connectives once and explain the correct answer");
            Check("∧∨⊕→↔".All(c=>session.stations[0].font.HasCharacter(c,true)&&ArchiveTopicUI.Current.textFont.HasCharacter(c,true)),"All five mathematical symbols resolve in world and reader font assets");
            session.Initialize(104729);ArchiveTopicUI.Current.Close();yield return .25f;
            var station=session.stations[0];var state=session.State.stations[0];
            Status="Book gating and reader";
            ArchiveTopicPlayChecks.MoveTo(station,Vector3.zero);Check(!session.Accept(-1,Command(ArchiveAction.Start)),"A challenge cannot start away from the Book Statue");
            ArchiveTopicPlayChecks.MoveTo(station,ArchiveTopicStation.BookPosition+Vector3.back*1.8f);yield return .2f;
            station.points.First(p=>p.role==ArchivePointRole.Book).Interact();yield return .25f;
            Check(state.phase==1&&ArchiveTopicUI.Current.GetComponent<ArchivePropositionUI>().IsOpen,"Book interaction starts the shared challenge and opens T1 reader");
            Check(!Player.LocalInstance.IsMovementLocked&&Time.timeScale==1,"Reading keeps original movement and world time active");
            Canvas.ForceUpdateCanvases();
            var reader=ArchiveTopicUI.Current.transform.Find("T1_Question_Reader");
            var close=reader.Find("Close").GetComponent<Button>();
            var rect=close.GetComponent<RectTransform>();Vector3 center=rect.TransformPoint(rect.rect.center);
            var hits=new List<RaycastResult>();ArchiveTopicUI.Current.GetComponent<GraphicRaycaster>().Raycast(new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,center)},hits);
            Check(hits.Any(h=>h.gameObject==close.gameObject),"Reader Close button receives graphic raycasts");close.onClick.Invoke();yield return .1f;
            var q1=session.Question(0);int wrong=(q1.correctChoice+1)%4;var rune=station.ItemPosition(wrong,q1);
            Status="Channel range and cancellation";
            ArchiveTopicPlayChecks.MoveTo(station,rune+Vector3.right*(ArchiveTopicSession.ChoiceUseRadius+.2f));yield return .2f;
            Check(!session.Accept(-1,Command(ArchiveAction.BeginChoice,wrong)),"Rune use is rejected just outside the ring");
            Check(ArchiveTopicUI.Current.FocusedPoint==null||ArchiveTopicUI.Current.FocusedPoint.role!=ArchivePointRole.Choice,"Interaction hint does not promise an out-of-range rune");
            ArchiveTopicPlayChecks.MoveTo(station,rune);yield return .2f;
            Check(ArchiveTopicUI.Current.FocusedPoint!=null&&ArchiveTopicUI.Current.FocusedPoint.index==wrong,"Hand prompt selects the reachable rune");
            var begin=Command(ArchiveAction.BeginChoice,wrong);Check(session.Accept(-1,begin),"Standing in a rune starts the existing timed channel");
            Check(!session.Accept(-1,begin),"Repeated command cannot start a second channel");
            ArchiveTopicPlayChecks.MoveTo(station,Vector3.zero);yield return .3f;
            Check(state.channelActor==0&&state.feedback.StartsWith("Channel cancelled")&&session.State.mistakes.Count==0,"Stepping off cancels with feedback and no mistake");
            ArchiveTopicPlayChecks.MoveTo(station,rune);session.Request(0,ArchiveAction.BeginChoice,wrong);yield return session.answerChannelSeconds+.25f;
            Check(state.phase==1&&state.round==0&&session.State.mistakes.Count==1&&session.State.mistakes[0].actor==-1,"Wrong channel records exactly one attributed mistake and keeps the same question");
            Check(state.answerSerial==1&&state.lastAnswerResult==-1&&state.lastAnswerChoice==wrong,"Wrong receipt identifies the rune without exposing the correct answer");
            yield return .4f;Check(session.State.mistakes.Count==1,"Remaining on a wrong rune does not auto-submit again");
            Status="Five T1 challenges";
            for(int round=0;round<5;round++)
            {
                var q=session.Question(0);ArchiveTopicPlayChecks.MoveTo(station,station.ItemPosition(q.correctChoice,q));yield return .15f;
                station.points.First(p=>p.role==ArchivePointRole.Choice&&p.index==q.correctChoice).Interact();yield return session.answerChannelSeconds+.25f;
                Check(state.round==round+1&&state.phase==(round==4?2:0)&&state.reveal==q.successText,"Correct rune advances challenge "+(round+1)+" with its explanation and Book gate");report.rounds++;
                var copy=JsonUtility.FromJson<ArchiveTeamState>(JsonUtility.ToJson(session.State));
                Check(copy.stations[0].answerSerial==state.answerSerial&&copy.stations[0].lastAnswerRound==round,"Answer receipt survives the same serialized checkpoint format: "+(round+1));
                if(round<4)
                {
                    ArchiveTopicPlayChecks.MoveTo(station,ArchiveTopicStation.BookPosition+Vector3.back*1.8f);yield return .15f;
                    station.points.First(p=>p.role==ArchivePointRole.Book).Interact();yield return .15f;ArchiveTopicUI.Current.Close();
                }
            }
            Check(state.lastAnswerResult==1&&session.State.stations.Skip(1).All(s=>s.round==0&&s.phase==0),"T1 completion leaves the other nine topics untouched");
            var owner=new GameObject("T1_Movement_Lock_Check");Player.LocalInstance.AcquireMovementLock(owner,"T1 polish QA",Player.LocalInstance.transform.position);
            Check(!session.CanAct(-1),"Existing capture movement lock still blocks topic actions");Player.LocalInstance.ReleaseMovementLock(owner);UnityEngine.Object.Destroy(owner);
            Check(!Player.LocalInstance.IsMovementLocked,"Cleanup restores movement without adding another player control system");
        }
    }
}
