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
    public static class ArchiveProofChecks
    {
        [Serializable]sealed class Result{public string name;public bool passed;}
        [Serializable]sealed class Report{public bool passed;public string checkedUtc,scope="Unity Editor: all five native Scribes' Court challenge kinds, physical manuscripts and rule seals, readable text, wrong answer retention, duplicate Premise cards, aged-deadline retry fixtures, checkpoint, UI and existing movement restrictions. Physical Android is separate.";public List<Result> checks=new List<Result>();public List<string> errors=new List<string>();}
        static readonly Stack<IEnumerator> stack=new Stack<IEnumerator>();static Report report;static ArchiveTopicSession session;static ArchiveTopicStation station;static ArchiveProofPresentation art;static ArchiveProofUI ui;
        static bool active,oldHunting;static double next;static GameObject restriction;
        public static string Status{get;private set;}="Idle";
        static ArchiveStationState State=>session.State.stations[3];
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test T4 Scribes Court (Play Mode)")]
        public static void Begin(){
            if(!Application.isPlaying||Photon.Pun.PhotonNetwork.InRoom||active)throw new InvalidOperationException("Offline Play Mode required.");session=ArchiveTopicSession.Current;station=session.stations[3];art=station.GetComponent<ArchiveProofPresentation>();ui=ArchiveTopicUI.Current.GetComponent<ArchiveProofUI>();
            report=new Report{checkedUtc=DateTime.UtcNow.ToString("o")};stack.Clear();stack.Push(Flow());next=0;oldHunting=session.hunting.enabled;session.hunting.Request(LogicLegends.OpenArchive.Hunting.KhezuCommand.Sleep);session.hunting.enabled=false;active=true;Application.logMessageReceived+=Log;EditorApplication.update+=Tick;
        }
        static void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.errors.Add(message);}
        static void Tick(){if(!active)return;try{EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup<next)return;if(!Application.isPlaying)throw new InvalidOperationException("Play Mode stopped.");if(stack.Count==0){Finish(true);return;}var flow=stack.Peek();if(!flow.MoveNext()){stack.Pop();return;}if(flow.Current is IEnumerator child)stack.Push(child);else next=EditorApplication.timeSinceStartup+(flow.Current is float delay?delay:0);}catch(Exception e){report.errors.Add(Status+": "+e);Finish(false);}}
        static void Check(bool passed,string name){report.checks.Add(new Result{name=name,passed=passed});if(!passed)throw new InvalidOperationException(name);}
        static void Move(Vector3 p)=>ArchiveTopicPlayChecks.MoveTo(station,p);
        static void Book(){Move(ArchiveTopicStation.BookPosition+Vector3.back*1.7f);station.points.First(p=>p.role==ArchivePointRole.Book).Interact();}
        static IEnumerator Place(int item,int slot){
            var q=session.Question(3);Move(station.CurrentItemPosition(item,q,State));station.points.First(p=>p.role==ArchivePointRole.Item&&p.index==item).Interact();yield return .055f;
            Check(State.carries.Any(c=>c.actor==-1&&c.item==item),"Hand picks existing "+q.items[item]);
            Move(station.SlotPosition(slot,q));station.points.First(p=>p.role==ArchivePointRole.Slot&&p.index==slot).Interact();yield return .055f;
            Check((q.regionPlacement?State.placements[item]==slot:State.placements[slot]==item)&&State.carries.Count==0,"Hand places "+q.items[item]+" on "+q.slots[slot]);
        }
        static IEnumerator Arrange(int[] answer){var q=session.Question(3);for(int i=0;i<answer.Length;i++)if(State.placements[i]!=answer[i]){if(q.regionPlacement)yield return Place(i,answer[i]);else yield return Place(answer[i],i);}}
        static IEnumerator Submit(){Move(Vector3.zero);ui.CheckButton.onClick.Invoke();yield return .18f;}
        static IEnumerator Preview(string file,bool reading){
            Move(new Vector3(0,0,6.1f));Player.LocalInstance.transform.rotation=station.transform.rotation*Quaternion.Euler(0,180,0);var look=Player.LocalInstance.GetComponentInChildren<CameraTargetController>();if(look!=null)look.SetViewAngles(27,station.transform.eulerAngles.y+180);
            if(reading)ArchiveTopicUI.Current.Open(station);else ArchiveTopicUI.Current.Close();yield return .35f;Directory.CreateDirectory("Temp/T4Polish");ScreenCapture.CaptureScreenshot("Temp/T4Polish/"+file+".png");yield return .25f;
        }
        static IEnumerator Flow(){
            Status="Book and reader";session.Initialize(314159);ArchiveTopicUI.Current.Close();Move(Vector3.zero);yield return .7f;var book=station.GetComponentInChildren<ArchiveAnimatedBook>();
            float expectedLimit=session.proofsTimeLimitSeconds*session.PacingMultiplier;
            Check(book!=null&&book.OpenAmount<.01f,"Existing T4 Book starts closed");Check(!State.timerRunning&&State.timerAttempt==0&&Mathf.Abs(session.TimerRemaining(State)-expectedLimit)<.01f,"Arrival preserves the current configured T4 clock without starting it");
            ArchiveTopicUI.Current.Open(station);yield return .15f;Check(ui.IsOpen&&!State.timerRunning,"Reader preview cannot start or reset the clock");ArchiveTopicUI.Current.Close();Book();yield return .8f;double deadline=State.timerDeadline;
            Check(State.phase==1&&State.timerRunning&&State.timerAttempt==1&&State.timerActor==-1&&State.timerLimit==expectedLimit&&session.TimerRemaining(State)>expectedLimit-2,"Native Book starts the current configured shared T4 clock");Check(book.OpenAmount>.98f,"Existing Book opens and floats during the proof challenge");
            var q=session.Question(3);ui.SelectItem(0);yield return .1f;Check(ui.ReadingText.text.Contains(q.details[0]),"Each manuscript can be read in full before classifying it");Check(string.IsNullOrEmpty(State.reveal),"Reader never publishes a solved proof before success");
            ArchiveTopicUI.Current.Close();yield return .1f;var corners=new Vector3[4];ui.Objective.GetWorldCorners(corners);Check(ui.IsObjectiveVisible&&corners.Min(p=>p.x)>Screen.width*.60f&&corners.Max(p=>p.x)<=Screen.width+1,"Compact T4 objective stays on the right side");Check(!ui.CheckButton.interactable,"Incomplete classification cannot be submitted accidentally");
            yield return Preview("T4_Classification",false);yield return Preview("T4_Reader",true);ArchiveTopicUI.Current.Close();
            int[] wrong=q.expected.Select(v=>1-v).ToArray();yield return Arrange(wrong);var ids=art.TokenRoots.Select(t=>t.GetInstanceID()).ToArray();yield return Submit();
            Check(State.round==0&&session.State.mistakes.Count==1&&session.State.mistakes[0].actor==-1&&State.lastAnswerResult==-1,"Wrong classification charges exactly the submitting player once");Check(State.placements.SequenceEqual(wrong)&&ids.SequenceEqual(art.TokenRoots.Select(t=>t.GetInstanceID()))&&State.timerDeadline==deadline,"Wrong classification preserves manuscript identity, arrangement and deadline");
            Move(station.CurrentItemPosition(0,q,State));restriction=new GameObject("T4 movement restriction fixture");Player.LocalInstance.AcquireMovementLock(restriction,"T4 capture validation",Player.LocalInstance.transform.position,100);int revision=State.revision;session.Request(3,ArchiveAction.Pick,0);yield return .12f;
            Check(!session.CanAct(-1)&&State.revision==revision&&!ui.IsObjectiveVisible,"Existing capture/stun lock rejects a nearby valid manuscript pickup and hides UI");Player.LocalInstance.ReleaseMovementLock(restriction);UnityEngine.Object.Destroy(restriction);restriction=null;
            yield return Arrange(q.expected);yield return 1.6f;yield return Submit();Check(State.round==1&&State.phase==0&&State.reveal.Contains("Formal")&&State.reveal.Contains("Informal")&&State.timerDeadline==deadline,"Correct classification publishes the result and keeps time across the Book gate");
            Check(session.ActiveTopic==3&&Enumerable.Range(0,10).Where(t=>t!=3).All(t=>!session.CanStartTopic(t)),"T4 reserves one team topic between all five challenges");
            Book();yield return .2f;q=session.Question(3);Check(q.items.Length==4&&q.regionPlacement,"Second challenge preserves four original proof scrolls and two style altars");yield return Place(0,q.expected[0]);var partial=State.placements.ToArray();string first=q.details[0];
            Status="Classification timeout";State.timerDeadline=ArchiveTopicSession.TimerClock-.01;yield return .3f;Check(State.timerExpired&&State.phase==0&&State.round==1&&State.placements.SequenceEqual(partial),"Timeout keeps solved challenges and current classifications");Check(session.State.mistakes.Count==2&&session.State.mistakes.Last().reason=="timeout"&&session.State.mistakes.Last().actor==0,"Timeout records exactly one current Book activator mistake");yield return .3f;Check(session.State.mistakes.Count==2,"Expired timer cannot repeatedly charge mistakes");
            Book();yield return .2f;Check(State.timerAttempt==2&&State.placements.SequenceEqual(partial)&&session.Question(3).details[0]==first&&session.TimerRemaining(State)>359,"Book retry retains the exact manuscripts and classifications");
            var json=JsonUtility.ToJson(session.State);typeof(ArchiveTopicSession).GetMethod("Receive",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(session,new object[]{json,true});yield return .12f;Check(State.placements.SequenceEqual(partial)&&State.timerAttempt==2&&session.ActiveTopic==3,"Checkpoint restores partial court work, time and topic reservation");
            yield return Arrange(session.Question(3).expected);yield return Submit();Check(State.round==2,"Four-scroll classification completes through native interactions");
            Status="Statement order";Book();yield return .15f;q=session.Question(3);var answer=ArchiveTopicChecks.Solution(q).placements;yield return Arrange(answer.Reverse().ToArray());int before=session.State.mistakes.Count;ids=art.TokenRoots.Select(t=>t.GetInstanceID()).ToArray();yield return Submit();
            Check(State.round==2&&session.State.mistakes.Count==before+1&&ids.SequenceEqual(art.TokenRoots.Select(t=>t.GetInstanceID())),"Invalid proof order records one mistake without replacing statement objects");yield return Arrange(answer);yield return 1.6f;yield return Submit();Check(State.round==3&&State.reveal.Contains(q.proof.steps.Last().statement),"Valid prerequisite order publishes the completed original proof");
            Status="Missing justification";Book();yield return .15f;q=session.Question(3);Check(q.proof.kind==ProofChallengeKind.MissingRule&&q.items.Length==4,"Fourth challenge preserves the incomplete proof and four rule options");yield return Place(q.expected[0],0);yield return Submit();Check(State.round==4&&State.reveal.Contains(q.items[q.expected[0]])&&!State.reveal.Contains("[MISSING"),"Correct rule replaces the missing marker in the published proof");
            Status="Formalize an explanation";Book();yield return .15f;q=session.Question(3);Check(q.items.Length==10&&q.slots.Length==10&&q.statementCount==5,"Final challenge has five statement manuscripts and five paired justification seals");yield return Preview("T4_Formalization",false);yield return Arrange(ArchiveTopicChecks.Solution(q).placements);
            foreach(var text in station.workRoot.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();Check(!text.isTextOverflowing,"World label fits: "+text.text);}
            int n=q.statementCount;var correct=ArchiveTopicChecks.Solution(q).placements;var invalid=correct.ToArray();int temp=invalid[n];invalid[n]=invalid[n*2-1];invalid[n*2-1]=temp;yield return Arrange(invalid);before=session.State.mistakes.Count;yield return Submit();Check(State.round==4&&session.State.mistakes.Count==before+1&&State.placements.SequenceEqual(invalid),"Mismatched justifications are rejected with the complete arrangement kept");
            var retained=State.placements.ToArray();State.timerDeadline=ArchiveTopicSession.TimerClock-.01;yield return .3f;Check(State.timerExpired&&State.round==4&&State.placements.SequenceEqual(retained),"Formalization timeout retains all ten current placements");Book();yield return .2f;Check(State.timerAttempt==3&&State.placements.SequenceEqual(retained),"Formalization retry keeps the paired proof board");
            int equal=Enumerable.Range(1,n-1).First(i=>q.proof.steps[correct[i]].justification==q.proof.steps[correct[0]].justification);temp=correct[n];correct[n]=correct[n+equal];correct[n+equal]=temp;yield return Arrange(correct);yield return 1.6f;yield return Submit();
            Check(State.phase==2&&State.round==5&&State.reveal.Contains(q.proof.steps.Last().statement),"Swapping equal Premise seals still completes the mathematically valid formal proof");Check(!State.timerRunning&&session.ActiveTopic==-1,"All five proof challenges freeze time and release the next topic");float remaining=session.TimerRemaining(State);yield return .2f;Check(session.TimerRemaining(State)==remaining,"Completed topic keeps its final time");
            Check(session.State.stations.Where(s=>s.topic!=3).All(s=>s.round==0&&s.timerAttempt==0),"T4 preserves every other topic's progress and timers");Check(!Player.LocalInstance.IsMovementLocked&&Time.timeScale==1&&report.errors.Count==0,"Player remains free to move and no runtime errors occurred");Status="Passed";
        }
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Validate T4 Proof Variants")]
        public static string ValidateVariants(){
            var source=AssetDatabase.LoadAssetAtPath<ArchiveTopicCatalog>(ArchiveTopicSetup.Folder+"/Archive_Topic_Catalog.asset");var copy=UnityEngine.Object.Instantiate(source);var proofs=ScriptableObject.CreateInstance<ProofChallengeCatalog>();copy.formalProofs=proofs;
            var checks=new List<Result>();var failures=new List<string>();int variants=0,acceptedOrders=0;
            Action<bool,string> test=(passed,name)=>{checks.Add(new Result{name=name,passed=passed});if(!passed)failures.Add(name);};
            try{
                for(int r=0;r<5;r++)for(int v=0;v<source.formalProofs.variantSets[r].variants.Length;v++){
                    string id="Challenge "+(r+1)+" / variant "+(v+1);proofs.variantSets=source.formalProofs.variantSets.Select(set=>new ProofChallengeVariantSet{variants=new[]{set.variants[0]}}).ToArray();proofs.variantSets[r].variants=new[]{source.formalProofs.variantSets[r].variants[v]};
                    var q=ArchiveTopicQuestions.Build(copy,3,r,314159);var good=ArchiveTopicChecks.Solution(q);string message;variants++;
                    test(ArchiveTopicQuestions.Validate(q,good,out message),id+" accepts original solution");test(!string.IsNullOrEmpty(ArchiveTopicQuestions.ProofReveal(q,good)),id+" publishes the accepted proof");
                    if(q.proof.kind==ProofChallengeKind.MissingRule)test(!ArchiveTopicQuestions.ProofReveal(q,good).Contains("[MISSING"),id+" replaces the missing marker with the accepted justification");
                    var points=Enumerable.Range(0,q.items.Length).Select(i=>ArchiveProofPresentation.TokenPosition(i,q)).ToArray();var sockets=Enumerable.Range(0,q.slots.Length).Select(i=>ArchiveProofPresentation.SocketPosition(i,q)).ToArray();
                    test(points.Distinct().Count()==points.Length&&points.All(p=>new Vector2(p.x,p.z).magnitude<=7.51f)&&sockets.Distinct().Count()==sockets.Length,id+" has distinct pickups and sockets inside the reserved court");
                    var bad=JsonUtility.FromJson<ArchiveStationState>(JsonUtility.ToJson(good));
                    if(q.regionPlacement){bad.placements[0]=1-bad.placements[0];test(q.details.SequenceEqual(q.proof.scrolls.Select(s=>s.content)),id+" preserves full scroll text");}
                    else if(q.proof.kind==ProofChallengeKind.MissingRule)bad.placements[0]=(bad.placements[0]+1)%q.items.Length;
                    else{
                        int n=q.statementCount;foreach(var order in q.orders){var candidate=ArchiveTopicChecks.Solution(q);for(int i=0;i<n;i++){candidate.placements[i]=order[i];if(q.proof.kind==ProofChallengeKind.Formalize)candidate.placements[n+i]=n+order[i];}test(ArchiveTopicQuestions.Validate(q,candidate,out message),id+" accepts original alternative proof order "+acceptedOrders);acceptedOrders++;}
                        if(q.proof.kind==ProofChallengeKind.Formalize){
                            int other=Enumerable.Range(1,n-1).First(i=>q.proof.steps[good.placements[i]].justification==q.proof.steps[good.placements[0]].justification);int swap=good.placements[n];good.placements[n]=good.placements[n+other];good.placements[n+other]=swap;test(ArchiveTopicQuestions.Validate(q,good,out message),id+" accepts interchangeable equal Premise seals");
                            var duplicate=JsonUtility.FromJson<ArchiveStationState>(JsonUtility.ToJson(good));duplicate.placements[n]=duplicate.placements[n+1];test(!ArchiveTopicQuestions.Validate(q,duplicate,out message),id+" rejects duplicated physical rule IDs");
                            int different=Enumerable.Range(1,n-1).First(i=>q.proof.steps[bad.placements[i]].justification!=q.proof.steps[bad.placements[0]].justification);swap=bad.placements[n];bad.placements[n]=bad.placements[n+different];bad.placements[n+different]=swap;
                        }else Array.Reverse(bad.placements);
                    }
                    test(!ArchiveTopicQuestions.Validate(q,bad,out message),id+" rejects a wrong style, order, rule or justification");
                }
            }catch(Exception e){failures.Add(e.ToString());}finally{UnityEngine.Object.DestroyImmediate(proofs);UnityEngine.Object.DestroyImmediate(copy);}
            var data=new{passed=failures.Count==0,variants=variants,acceptedOrders=acceptedOrders,checks=checks,failures=failures};Directory.CreateDirectory(ArchiveTopicSetup.Folder+"/Testing/Reports");File.WriteAllText(ArchiveTopicSetup.Folder+"/Testing/Reports/T4ProofVariants.json",Newtonsoft.Json.JsonConvert.SerializeObject(data,Newtonsoft.Json.Formatting.Indented));return (data.passed?"Passed":"Failed")+": "+variants+" variants, "+checks.Count+" checks";
        }
        static void Finish(bool success){active=false;EditorApplication.update-=Tick;Application.logMessageReceived-=Log;stack.Clear();if(restriction!=null){Player.LocalInstance.ReleaseMovementLock(restriction);UnityEngine.Object.Destroy(restriction);}if(session!=null&&Application.isPlaying){session.hunting.enabled=oldHunting;session.hunting.Request(LogicLegends.OpenArchive.Hunting.KhezuCommand.Sleep);ArchiveTopicUI.Current.Close();}report.passed=success&&report.errors.Count==0&&report.checks.All(c=>c.passed);Status=report.passed?"Passed":"Failed";Directory.CreateDirectory(ArchiveTopicSetup.Folder+"/Testing/Reports");File.WriteAllText(ArchiveTopicSetup.Folder+"/Testing/Reports/T4PolishPlayMode.json",JsonUtility.ToJson(report,true));Debug.Log("T4 Scribes' Court: "+Status+", "+report.checks.Count+" checks");}
    }
}
