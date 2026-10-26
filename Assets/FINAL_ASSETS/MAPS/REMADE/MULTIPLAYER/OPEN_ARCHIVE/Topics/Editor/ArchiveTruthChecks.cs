using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Opt-in T2 checks through existing Book/tile/socket commands; no alternative puzzle solver runs in gameplay.</summary>
    public static class ArchiveTruthChecks
    {
        [Serializable] sealed class CheckResult{public string name,detail;public bool passed;}
        [Serializable] sealed class Report{public bool passed;public string checkedUtc,scope="Unity Editor: nine native Truth Mosaic tables, wrong answer retention, same tile identity, Book animation, compact UI, six-minute shared deadline using aged timeout fixtures, existing movement restriction and full topic reservation. Device input/performance is separate.";public List<CheckResult> checks=new List<CheckResult>();public List<string> errors=new List<string>();}
        static Report report;static readonly Stack<IEnumerator> stack=new Stack<IEnumerator>();
        static ArchiveTopicSession session;static ArchiveTopicStation station;static ArchiveTruthPresentation art;static ArchiveTruthUI ui;
        static double next;static bool active,oldHunting;static GameObject restriction;
        public static string Status{get;private set;}="Idle";
        public static void Begin(){
            if(!Application.isPlaying||Photon.Pun.PhotonNetwork.InRoom||active)throw new InvalidOperationException("Offline Play Mode required.");
            session=ArchiveTopicSession.Current;station=session.stations[1];art=station.GetComponent<ArchiveTruthPresentation>();ui=ArchiveTopicUI.Current.GetComponent<ArchiveTruthUI>();
            report=new Report{checkedUtc=DateTime.UtcNow.ToString("o")};stack.Clear();stack.Push(Flow());next=0;oldHunting=session.hunting.enabled;
            session.hunting.Request(LogicLegends.OpenArchive.Hunting.KhezuCommand.Sleep);session.hunting.enabled=false;active=true;Application.logMessageReceived+=Log;EditorApplication.update+=Tick;
        }
        static void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.errors.Add(message);}
        static void Tick(){
            if(!active)return;
            try{
                EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup<next)return;
                if(!Application.isPlaying)throw new InvalidOperationException("Play Mode stopped.");
                if(stack.Count==0){Finish(true);return;}
                var flow=stack.Peek();if(!flow.MoveNext()){stack.Pop();return;}
                if(flow.Current is IEnumerator child)stack.Push(child);else next=EditorApplication.timeSinceStartup+(flow.Current is float delay?delay:0);
            }catch(Exception e){report.errors.Add(Status+": "+e);Finish(false);}
        }
        static void Check(bool pass,string name,string detail=""){report.checks.Add(new CheckResult{name=name,passed=pass,detail=detail});if(!pass)throw new InvalidOperationException(name+": "+detail);}
        static ArchiveStationState State=>session.State.stations[1];
        static void Move(Vector3 point)=>ArchiveTopicPlayChecks.MoveTo(station,point);
        static void Book(){Move(ArchiveTopicStation.BookPosition+Vector3.back*1.7f);station.points.First(p=>p.role==ArchivePointRole.Book).Interact();}
        static IEnumerator Place(int row,int item){
            var q=session.Question(1);Move(station.ItemPosition(item,q));station.points.First(p=>p.role==ArchivePointRole.Item&&p.index==item).Interact();yield return .045f;
            Check(State.carries.Any(c=>c.actor==-1&&c.item==item),"Native tile stand provides reusable "+(item==0?"True":"False"));
            Move(station.SlotPosition(row,q));station.points.First(p=>p.role==ArchivePointRole.Slot&&p.index==row).Interact();yield return .045f;
            Check(State.placements[row]==item&&!State.carries.Any(c=>c.actor==-1),"Native row "+(row+1)+" accepts carried tile");
        }
        static IEnumerator Flow(){
            Status="Idle preview and first Book";session.Initialize(314159);ArchiveTopicUI.Current.Close();Move(Vector3.zero);yield return .75f;
            var book=station.GetComponentInChildren<ArchiveAnimatedBook>();Check(book!=null&&book.OpenAmount<.01f,"T2 starts with the existing animated Book closed");
            Check(!State.timerRunning&&State.timerAttempt==0&&Mathf.Abs(session.TimerRemaining(State)-360)<.01f,"Approach leaves six-minute T2 clock unstarted");
            ArchiveTopicUI.Current.Open(station);yield return .12f;Check(ui.IsOpen&&!State.timerRunning,"Read-only preview never starts the clock");ArchiveTopicUI.Current.Close();
            Book();yield return .85f;double deadline=State.timerDeadline;
            Check(State.phase==1&&State.timerAttempt==1&&State.timerActor==-1&&State.timerRunning&&session.TimerRemaining(State)>358,"First real Book action starts one shared 360-second clock");
            Check(book.OpenAmount>.98f&&book.bookRig.localPosition.y>.2f,"Active Book opens and floats; page animation retained");
            ArchiveTopicUI.Current.Close();yield return .1f;Check(ui.IsObjectiveVisible&&!ui.IsOpen,"Compact right-side objective replaces the always-open generic panel");
            var corners=new Vector3[4];ui.Objective.GetWorldCorners(corners);
            Check(corners.Min(p=>p.x)>Screen.width*.60f&&corners.Max(p=>p.x)<=Screen.width+1&&corners.Min(p=>p.y)>=0,"Compact card stays inside right-side screen space");
            Check(!ui.CheckButton.interactable,"Check button stays disabled until four outputs are placed");
            Check(station.points.Count(p=>p.role==ArchivePointRole.Item)==2&&station.points.Count(p=>p.role==ArchivePointRole.Slot)==4,"Two reusable stands and four explicit output sockets");
            var q=session.Question(1);int[] wrong=q.expected.ToArray();wrong[0]=1-wrong[0];
            for(int row=0;row<4;row++)yield return Place(row,wrong[row]);
            var ids=art.PlacedTileRoots.Select(t=>t.GetInstanceID()).ToArray();var values=State.placements.ToArray();
            Check(ui.CheckButton.interactable,"Filled team table enables Check table");
            Move(Vector3.zero);ui.CheckButton.onClick.Invoke();yield return .12f;
            Check(State.round==0&&State.lastAnswerResult==-1&&session.State.mistakes.Count==1&&session.State.mistakes[0].actor==-1&&session.State.mistakes[0].reason=="answer","Wrong table records one mistake for the submitting player");
            Check(State.placements.SequenceEqual(values)&&ids.SequenceEqual(art.PlacedTileRoots.Select(t=>t.GetInstanceID())),"Incorrect submission preserves exact placed tile objects and all current values");
            Check(State.timerDeadline==deadline,"Wrong answers cannot reset or extend the team deadline");
            restriction=new GameObject("T2 temporary movement lock fixture");Player.LocalInstance.AcquireMovementLock(restriction,"T2 encounter validation",Player.LocalInstance.transform.position,100);int revision=State.revision;
            session.Request(1,ArchiveAction.Pick,0);yield return .1f;
            Check(State.revision==revision&&!ui.IsObjectiveVisible,"Captured/stunned player cannot manipulate T2 and loses local controls UI");Player.LocalInstance.ReleaseMovementLock(restriction);UnityEngine.Object.Destroy(restriction);restriction=null;
            yield return Place(0,q.expected[0]);yield return 1.6f;Move(Vector3.zero);ui.CheckButton.onClick.Invoke();yield return .12f;
            Check(State.round==1&&State.phase==0&&State.timerRunning&&State.timerDeadline==deadline,"Correct table preserves solved progress and the same clock between Book gates");
            Check(State.placements.All(p=>p<0)&&State.reveal.Contains(q.prompt),"Solved table is revealed separately; next table cannot inherit old outputs");
            Check(session.ActiveTopic==1&&Enumerable.Range(0,10).Where(t=>t!=1).All(t=>!session.CanStartTopic(t)),"Unfinished T2 reserves the topic for the whole team between tables");
            Book();yield return .2f;q=session.Question(1);Check(State.timerDeadline==deadline&&State.timerAttempt==1,"Second table's Book keeps the original deadline");
            yield return Place(0,q.expected[0]);yield return Place(1,q.expected[1]);var partial=State.placements.ToArray();string expression=q.prompt;
            Status="Timeout and retry";State.timerDeadline=ArchiveTopicSession.TimerClock-.01;yield return .3f;
            Check(State.timerExpired&&!State.timerRunning&&State.phase==0&&State.round==1&&State.placements.SequenceEqual(partial),"Timeout retains solved table and unfinished table's placed outputs");
            Check(session.State.mistakes.Count==2&&session.State.mistakes.Last().actor==0&&session.State.mistakes.Last().topic==1&&session.State.mistakes.Last().reason=="timeout","Timeout adds exactly one shared T2 event");
            yield return .3f;Check(session.State.mistakes.Count==2,"Expired clock cannot repeatedly add mistakes");
            Book();yield return .2f;Check(State.timerAttempt==2&&!State.timerExpired&&State.placements.SequenceEqual(partial)&&session.Question(1).prompt==expression&&session.TimerRemaining(State)>359,"Book retry starts a fresh attempt while retaining the same unfinished expression and outputs");
            var serialized=JsonUtility.ToJson(session.State);typeof(ArchiveTopicSession).GetMethod("Receive",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(session,new object[]{serialized,true});yield return .1f;
            Check(State.placements.SequenceEqual(partial)&&State.timerAttempt==2&&State.timerDeadline>ArchiveTopicSession.TimerClock,"Checkpoint restores partial table, timer and team reservation");
            Status="All nine native tables";
            for(int round=State.round;round<9;round++){
                if(State.phase==0){Book();yield return .1f;}
                q=session.Question(1);Status="Native T2 table "+(round+1)+" / 9";
                Check(q.title.StartsWith(round<3?"Easy":round<6?"Medium":"Hard"),"Table "+(round+1)+" preserves original difficulty tier");
                for(int row=0;row<4;row++)if(State.placements[row]!=q.expected[row])yield return Place(row,q.expected[row]);
                if(round==1){
                    Move(new Vector3(1,0,6.1f));ArchiveTopicUI.Current.Close();
                    var look=Player.LocalInstance.GetComponentInChildren<CameraTargetController>();if(look!=null)look.SetViewAngles(30,station.transform.eulerAngles.y+180);
                    yield return .5f;Directory.CreateDirectory("Temp/T2Polish");ScreenCapture.CaptureScreenshot("Temp/T2Polish/T2_PlayPreview.png");
                    ArchiveTopicUI.Current.Open(station);yield return .1f;ScreenCapture.CaptureScreenshot("Temp/T2Polish/T2_ReaderPreview.png");ArchiveTopicUI.Current.Close();
                }
                yield return .4f;Move(Vector3.zero);ui.CheckButton.onClick.Invoke();yield return .1f;
                Check(State.round==round+1,"Native Check table advances shared table "+(round+1));
            }
            Status="Completed topic";Check(State.phase==2&&State.round==9&&!State.timerRunning&&session.ActiveTopic==-1,"Nine restored tables complete T2, freeze timer and release other topics");
            float remaining=session.TimerRemaining(State);yield return .2f;Check(session.TimerRemaining(State)==remaining,"Completed topic retains its final time");
            Check(session.State.stations.Where(s=>s.topic!=1).All(s=>s.round==0&&s.timerAttempt==0),"T2 polish preserves all other topics' progress and timers");
            Check(!Player.LocalInstance.IsMovementLocked&&Time.timeScale==1,"Native movement remains enabled while reading, placing and solving");
            Check(report.errors.Count==0,"No runtime errors");Status="Passed";
        }
        static void Finish(bool success){
            active=false;EditorApplication.update-=Tick;Application.logMessageReceived-=Log;stack.Clear();
            if(restriction!=null){Player.LocalInstance.ReleaseMovementLock(restriction);UnityEngine.Object.Destroy(restriction);}
            if(session!=null&&Application.isPlaying){session.hunting.enabled=oldHunting;session.hunting.Request(LogicLegends.OpenArchive.Hunting.KhezuCommand.Sleep);ArchiveTopicUI.Current.Close();}
            report.passed=success&&report.errors.Count==0&&report.checks.All(c=>c.passed);Status=report.passed?"Passed":"Failed";
            Directory.CreateDirectory(ArchiveTopicSetup.Folder+"/Testing/Reports");File.WriteAllText(ArchiveTopicSetup.Folder+"/Testing/Reports/T2PolishPlayMode.json",JsonUtility.ToJson(report,true));Debug.Log("T2 Truth Mosaic: "+Status+", "+report.checks.Count+" checks");
        }
    }
}
