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
    /// <summary>Opt-in native T3 interaction checks. Deadline aging is an explicit test fixture.</summary>
    public static class ArchiveInferenceChecks
    {
        [Serializable]sealed class Result{public string name;public bool passed;}
        [Serializable]sealed class Report{public bool passed;public string checkedUtc,scope="Unity Editor: five native two-stage inference challenges, physical words/crystals, incorrect answer retention, timer and retries using aged-deadline fixtures, checkpoint, UI and existing movement lock. Device performance/input is separate.";public List<Result> checks=new List<Result>();public List<string> errors=new List<string>();}
        static readonly Stack<IEnumerator> stack=new Stack<IEnumerator>();static Report report;
        static ArchiveTopicSession session;static ArchiveTopicStation station;static ArchiveInferencePresentation art;static ArchiveInferenceUI ui;
        static bool active,oldHunting;static double next;static GameObject restriction;
        public static string Status{get;private set;}="Idle";
        static ArchiveStationState State=>session.State.stations[2];
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test T3 Inference Polish (Play Mode)")]
        public static void Begin(){
            if(!Application.isPlaying||Photon.Pun.PhotonNetwork.InRoom||active)throw new InvalidOperationException("Offline Play Mode required.");
            session=ArchiveTopicSession.Current;station=session.stations[2];art=station.GetComponent<ArchiveInferencePresentation>();ui=ArchiveTopicUI.Current.GetComponent<ArchiveInferenceUI>();
            report=new Report{checkedUtc=DateTime.UtcNow.ToString("o")};stack.Clear();stack.Push(Flow());next=0;oldHunting=session.hunting.enabled;
            session.hunting.Request(LogicLegends.OpenArchive.Hunting.KhezuCommand.Sleep);session.hunting.enabled=false;active=true;Application.logMessageReceived+=Log;EditorApplication.update+=Tick;
        }
        static void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.errors.Add(message);}
        static void Tick(){
            if(!active)return;try{EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup<next)return;if(!Application.isPlaying)throw new InvalidOperationException("Play Mode stopped.");if(stack.Count==0){Finish(true);return;}var flow=stack.Peek();if(!flow.MoveNext()){stack.Pop();return;}if(flow.Current is IEnumerator child)stack.Push(child);else next=EditorApplication.timeSinceStartup+(flow.Current is float delay?delay:0);}catch(Exception e){report.errors.Add(Status+": "+e);Finish(false);}
        }
        static void Check(bool pass,string name){report.checks.Add(new Result{name=name,passed=pass});if(!pass)throw new InvalidOperationException(name);}
        static void Move(Vector3 point)=>ArchiveTopicPlayChecks.MoveTo(station,point);
        static void Book(){Move(ArchiveTopicStation.BookPosition+Vector3.back*1.7f);station.points.First(p=>p.role==ArchivePointRole.Book).Interact();}
        static IEnumerator Place(int slot,int item){
            var q=session.Question(2);Move(station.CurrentItemPosition(item,q,State));station.points.First(p=>p.role==ArchivePointRole.Item&&p.index==item).Interact();yield return .06f;
            Check(State.carries.Any(c=>c.actor==-1&&c.item==item),"Hand picks existing "+(State.subphase==0?"word tablet":"Rule Crystal")+" "+item);
            Move(station.SlotPosition(slot,q));station.points.First(p=>p.role==ArchivePointRole.Slot&&p.index==slot).Interact();yield return .06f;
            Check(State.placements[slot]==item&&!State.carries.Any(c=>c.actor==-1),"Hand places physical item in "+q.slots[slot]);
        }
        static IEnumerator CompleteWords(){var q=session.Question(2);var solution=ArchiveTopicChecks.Solution(q);for(int i=0;i<q.slots.Length;i++)if(State.placements[i]!=solution.placements[i])yield return Place(i,solution.placements[i]);}
        static IEnumerator Submit(){Move(Vector3.zero);ui.CheckButton.onClick.Invoke();yield return .16f;}
        static IEnumerator Preview(string file,bool reading){
            Move(new Vector3(0,0,6.3f));Player.LocalInstance.transform.rotation=station.transform.rotation*Quaternion.Euler(0,180,0);
            var look=Player.LocalInstance.GetComponentInChildren<CameraTargetController>();if(look!=null)look.SetViewAngles(31,station.transform.eulerAngles.y+180);
            if(reading)ArchiveTopicUI.Current.Open(station);else ArchiveTopicUI.Current.Close();yield return .35f;
            Directory.CreateDirectory("Temp/T3Polish");ScreenCapture.CaptureScreenshot("Temp/T3Polish/"+file+".png");yield return .25f;
        }
        static IEnumerator Flow(){
            Status="First Book and UI";session.Initialize(314159);ArchiveTopicUI.Current.Close();Move(Vector3.zero);yield return .65f;
            var book=station.GetComponentInChildren<ArchiveAnimatedBook>();Check(book!=null&&book.OpenAmount<.01f,"Existing T3 Book starts closed");
            Check(!State.timerRunning&&State.timerAttempt==0&&Mathf.Abs(session.TimerRemaining(State)-360)<.01f,"Arriving does not start the six-minute clock");
            ArchiveTopicUI.Current.Open(station);yield return .12f;Check(ui.IsOpen&&!State.timerRunning,"Read-only preview cannot start the clock");ArchiveTopicUI.Current.Close();
            Book();yield return .8f;double deadline=State.timerDeadline;
            Check(State.phase==1&&State.subphase==0&&State.timerRunning&&State.timerActor==-1&&State.timerAttempt==1&&session.TimerRemaining(State)>358,"Native first Book starts one shared 360-second timer");
            Check(book.OpenAmount>.98f,"Existing Book opens, floats and animates while T3 is active");
            ArchiveTopicUI.Current.Close();yield return .1f;Check(ui.IsObjectiveVisible&&!ui.CheckButton.interactable,"Compact objective appears with incomplete checks disabled");
            var corners=new Vector3[4];ui.Objective.GetWorldCorners(corners);Check(corners.Min(p=>p.x)>Screen.width*.60f&&corners.Max(p=>p.x)<=Screen.width+1&&corners.Min(p=>p.y)>=0,"Objective fits the right side of the screen");
            Check(station.points.Count(p=>p.role==ArchivePointRole.Item)==session.Question(2).items.Length&&station.points.Count(p=>p.role==ArchivePointRole.Slot)==session.Question(2).slots.Length,"Premise stage has exact non-duplicated physical words and numbered blanks");
            yield return Preview("T3_Premises",false);yield return CompleteWords();yield return Submit();
            Check(State.round==0&&State.subphase==1&&State.phase==1&&State.lastAnswerResult==1,"Checking correct premises advances only to the Rule Crystal stage");
            Check(State.timerDeadline==deadline&&State.timerAttempt==1,"Premise-to-rule transition preserves the original deadline");
            var q=session.Question(2);Check(station.points.Count(p=>p.role==ArchivePointRole.Item)==10&&station.points.Count(p=>p.role==ArchivePointRole.Slot)==1,"All ten curriculum rules remain available at the single altar");
            // A valid conclusion can already occur inside a premise (e.g. simplification).
            // Check the explicit reveal, rather than incorrectly hiding the premise's own text.
            ArchiveTopicUI.Current.Open(station);yield return .12f;Check(!ui.PremiseText.text.Contains("Conclusion:")&&string.IsNullOrEmpty(State.reveal),"Conclusion is sealed before a correct Rule Crystal answer");
            foreach(var text in station.workRoot.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();Check(!text.isTextOverflowing,"World label fits: "+text.gameObject.name+" / "+text.text);}
            yield return Preview("T3_Crystals",false);yield return Preview("T3_Reader",true);ArchiveTopicUI.Current.Close();
            int wrong=(q.expected[0]+1)%q.items.Length;yield return Place(0,wrong);var ids=art.TokenRoots.Select(t=>t.GetInstanceID()).ToArray();yield return Submit();
            Check(State.subphase==1&&State.round==0&&State.lastAnswerResult==-1&&session.State.mistakes.Count==1&&session.State.mistakes[0].actor==-1,"Wrong crystal adds exactly one attributed mistake");
            Check(State.placements[0]==wrong&&ids.SequenceEqual(art.TokenRoots.Select(t=>t.GetInstanceID()))&&State.timerDeadline==deadline,"Wrong crystal retains physical object identity, altar and deadline");
            Status="Timeout in crystal stage";State.timerDeadline=ArchiveTopicSession.TimerClock-.01;yield return .3f;
            Check(State.timerExpired&&State.phase==0&&State.subphase==1&&State.round==0&&State.placements[0]==wrong,"Crystal-stage timeout retains solved premises and current altar");
            Check(session.State.mistakes.Count==2&&session.State.mistakes.Last().reason=="timeout"&&session.State.mistakes.Last().actor==0,"Timeout records exactly one team event");
            yield return .3f;Check(session.State.mistakes.Count==2,"Expired clock cannot repeat the penalty");Book();yield return .2f;
            Check(State.phase==1&&State.subphase==1&&State.placements[0]==wrong&&State.timerAttempt==2&&session.TimerRemaining(State)>359,"Book retries the crystal stage without replaying premises");
            deadline=State.timerDeadline;
            restriction=new GameObject("T3 movement restriction fixture");Player.LocalInstance.AcquireMovementLock(restriction,"T3 capture validation",Player.LocalInstance.transform.position,100);int revision=State.revision;
            session.Request(2,ArchiveAction.Pick,q.expected[0]);yield return .12f;Check(State.revision==revision&&!ui.IsObjectiveVisible,"Existing capture/stun lock blocks T3 input and hides controls");Player.LocalInstance.ReleaseMovementLock(restriction);UnityEngine.Object.Destroy(restriction);restriction=null;
            yield return Place(0,q.expected[0]);yield return 1.6f;yield return Submit();
            Check(State.round==1&&State.subphase==0&&State.phase==0&&State.reveal==q.successText&&State.timerDeadline==deadline,"Correct crystal reveals the original conclusion and gates the next challenge at the Book");
            Check(session.ActiveTopic==2&&Enumerable.Range(0,10).Where(t=>t!=2).All(t=>!session.CanStartTopic(t)),"T3 reservation remains through both stages and between challenges");
            Book();yield return .2f;q=session.Question(2);yield return Place(0,ArchiveTopicChecks.Solution(q).placements[0]);var partial=State.placements.ToArray();string prompt=q.prompt;
            Status="Timeout in premise stage";State.timerDeadline=ArchiveTopicSession.TimerClock-.01;yield return .3f;
            Check(State.timerExpired&&State.round==1&&State.subphase==0&&State.placements.SequenceEqual(partial),"Premise-stage timeout keeps completed challenges and unfinished words");
            Book();yield return .2f;Check(State.timerAttempt==3&&State.placements.SequenceEqual(partial)&&session.Question(2).prompt==prompt,"Premise retry keeps the exact question and partial placements");
            string json=JsonUtility.ToJson(session.State);typeof(ArchiveTopicSession).GetMethod("Receive",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(session,new object[]{json,true});yield return .12f;
            Check(State.placements.SequenceEqual(partial)&&State.timerAttempt==3&&session.ActiveTopic==2,"Checkpoint retains partial premise stage, clock and topic reservation");
            yield return CompleteWords();q=session.Question(2);var solution=ArchiveTopicChecks.Solution(q).placements;
            int a=Enumerable.Range(0,solution.Length).First(i=>q.items[solution[i]]!=q.items[solution[0]]);yield return Place(0,solution[a]);yield return Place(a,solution[0]);
            int before=session.State.mistakes.Count;var arranged=State.placements.ToArray();var identities=art.TokenRoots.Select(t=>t.GetInstanceID()).ToArray();yield return Submit();
            Check(State.subphase==0&&State.round==1&&session.State.mistakes.Count==before+1&&State.placements.SequenceEqual(arranged)&&identities.SequenceEqual(art.TokenRoots.Select(t=>t.GetInstanceID())),"Wrong premise order keeps all word objects and adds one mistake");
            yield return CompleteWords();yield return 1.6f;
            Status="All five native inference challenges";
            for(int r=State.round;r<5;r++){
                if(State.phase==0){Book();yield return .15f;}
                q=session.Question(2);yield return CompleteWords();yield return Submit();Check(State.round==r&&State.subphase==1,"Native premise stage "+(r+1)+" solved");
                q=session.Question(2);yield return Place(0,q.expected[0]);yield return Submit();Check(State.round==r+1&&State.reveal==q.successText,"Native Rule Crystal stage "+(r+1)+" reveals curriculum conclusion");
            }
            Check(State.phase==2&&State.round==5&&!State.timerRunning&&session.ActiveTopic==-1,"Five two-stage challenges complete T3, freeze timer and unlock other topics");
            float remaining=session.TimerRemaining(State);yield return .2f;Check(session.TimerRemaining(State)==remaining,"Completed T3 retains its final time");
            Check(session.State.stations.Where(s=>s.topic!=2).All(s=>s.round==0&&s.timerAttempt==0),"Other topic progress and clocks remain untouched");
            Check(!Player.LocalInstance.IsMovementLocked&&Time.timeScale==1&&report.errors.Count==0,"Native controls remain free and no runtime errors occurred");Status="Passed";
        }
        static void Finish(bool success){
            active=false;EditorApplication.update-=Tick;Application.logMessageReceived-=Log;stack.Clear();if(restriction!=null){Player.LocalInstance.ReleaseMovementLock(restriction);UnityEngine.Object.Destroy(restriction);}
            if(session!=null&&Application.isPlaying){session.hunting.enabled=oldHunting;session.hunting.Request(LogicLegends.OpenArchive.Hunting.KhezuCommand.Sleep);ArchiveTopicUI.Current.Close();}
            report.passed=success&&report.errors.Count==0&&report.checks.All(c=>c.passed);Status=report.passed?"Passed":"Failed";
            Directory.CreateDirectory(ArchiveTopicSetup.Folder+"/Testing/Reports");File.WriteAllText(ArchiveTopicSetup.Folder+"/Testing/Reports/T3PolishPlayMode.json",JsonUtility.ToJson(report,true));Debug.Log("T3 Inference Constellation: "+Status+", "+report.checks.Count+" checks");
        }
    }
}
