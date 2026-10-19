using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LogicLegends.Graph;
using Photon.Pun;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveTopicPlayChecks
    {
        [Serializable] sealed class Check{public string name;public bool passed;}
        [Serializable] sealed class Report
        {
            public bool passed;public int acceptedCommands,completedTopics,completedRounds,completedSetProblems;
            public int setChallenges=ArchiveTopicCatalog.SetChallengeCount,setProblemsPerChallenge=ArchiveTopicCatalog.SetProblemsPerChallenge;
            public string scope="Offline Editor Play Mode. Real books/props and validated commands; graph routes use small CharacterController.Move steps. Native OS mouse/touch and physical Android are not verified.";
            public List<Check> checks=new List<Check>();public List<string> errors=new List<string>();
        }
        static Report report;
        static IEnumerator run;
        static ArchiveTopicSession session;
        static double nextTick;
        static bool oldHuntingEnabled;
        public static string Status{get;private set;}="Idle";
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test All Adapted Activities (Play Mode)")]
        public static void Begin()
        {
            if(!Application.isPlaying||PhotonNetwork.InRoom)throw new InvalidOperationException("Use offline Play Mode in the topic test scene.");
            if(run!=null)throw new InvalidOperationException("A topic check is already running.");
            session=UnityEngine.Object.FindFirstObjectByType<ArchiveTopicSession>();if(session==null||!session.Ready)throw new InvalidOperationException("Topic session is not ready.");
            report=new Report();oldHuntingEnabled=session.hunting.enabled;session.hunting.Request(LogicLegends.OpenArchive.Hunting.KhezuCommand.Sleep);session.hunting.enabled=false;
            run=Flow();nextTick=0;Application.logMessageReceived+=Log;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            if(run==null)return;EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup<nextTick)return;
            try
            {
                if(!Application.isPlaying)throw new InvalidOperationException("Play Mode stopped during validation.");
                if(!run.MoveNext()){Finish(true);return;}
                nextTick=EditorApplication.timeSinceStartup+(run.Current is float delay?delay:0);
            }
            catch(Exception e){report.errors.Add(Status+": "+e);Finish(false);}
        }
        static void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.errors.Add(message);}
        static void Record(string name,bool pass){report.checks.Add(new Check{name=name,passed=pass});if(!pass)throw new InvalidOperationException(name);Save();}
        static void Save(){Directory.CreateDirectory(ArchiveTopicSetup.Folder+"/Testing/Reports");File.WriteAllText(ArchiveTopicSetup.Folder+"/Testing/Reports/AllActivitiesPlayMode.json",JsonUtility.ToJson(report,true));}
        static void Finish(bool complete)
        {
            EditorApplication.update-=Tick;Application.logMessageReceived-=Log;run=null;
            if(session!=null&&Application.isPlaying){session.hunting.enabled=oldHuntingEnabled;session.hunting.Request(LogicLegends.OpenArchive.Hunting.KhezuCommand.Sleep);}
            report.passed=complete&&report.errors.Count==0&&report.checks.All(c=>c.passed);Status=report.passed?"Passed":"Failed";Save();
            Debug.Log("Open Archive activity flow: "+Status+", "+report.completedRounds+" rounds, "+report.acceptedCommands+" commands.");
        }
        static IEnumerator Flow()
        {
            session.Initialize(314159);ArchiveTopicUI.Current.Close();yield return .2f;
            for(int topic=0;topic<10;topic++)
            {
                var station=session.stations[topic];var state=session.State.stations[topic];
                for(int round=0;round<ArchiveTopicCatalog.RoundCount(topic);round++)
                {
                    Status="T"+(topic+1)+" / round "+(round+1);MoveTo(station,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);
                    station.points.First(p=>p.role==ArchivePointRole.Book).Interact();yield return .12f;
                    Record(Status+" starts through the existing hand/Book action",state.phase==1);
                    while(state.round==round)
                    {
                        var q=session.Question(topic);var solution=ArchiveTopicChecks.Solution(q);
                        if(q.activity==ArchiveActivity.Choice)
                        {
                            MoveTo(station,station.ItemPosition(q.correctChoice,q));Send(topic,ArchiveAction.BeginChoice,q.correctChoice);yield return session.answerChannelSeconds+.2f;
                        }
                        else
                        {
                            if(q.activity==ArchiveActivity.Method)Send(topic,ArchiveAction.Select,solution.selection);
                            if(q.regionPlacement)
                            {
                                for(int i=0;i<solution.placements.Length;i++)if(solution.placements[i]>=0)
                                {
                                    MoveTo(station,station.CurrentItemPosition(i,q,state));Send(topic,ArchiveAction.Pick,i);yield return null;
                                    MoveTo(station,station.SlotPosition(solution.placements[i],q));Send(topic,ArchiveAction.Place,solution.placements[i]);yield return null;
                                }
                            }
                            else for(int slot=0;slot<solution.placements.Length;slot++)
                            {
                                int item=solution.placements[slot];MoveTo(station,station.CurrentItemPosition(item,q,state));Send(topic,ArchiveAction.Pick,item);yield return null;
                                MoveTo(station,station.SlotPosition(slot,q));Send(topic,ArchiveAction.Place,slot);yield return null;
                            }
                            if(q.mapping!=null)
                            {
                                if(q.mapping.questionKind==MappingQuestionKind.ClassifyFunction)Send(topic,ArchiveAction.Select,solution.selection);
                                else if(q.mapping.questionKind==MappingQuestionKind.SelectProperties)
                                {foreach(int flag in new[]{2,4,8})if((solution.flags&flag)!=0)Send(topic,ArchiveAction.Select,flag);}
                                else foreach(int link in solution.links.Except(state.links).ToArray())
                                {
                                    MoveTo(station,station.InputPosition(link/100));Send(topic,ArchiveAction.Select,link/100);yield return null;
                                    MoveTo(station,station.OutputPosition(link%100));Send(topic,ArchiveAction.Connect,link/100,link%100);yield return null;
                                }
                            }
                            if(q.tree!=null)
                            {
                                MoveTo(station,Vector3.zero);
                                if(q.tree.Editable)
                                {
                                    foreach(int link in state.links.Except(solution.links).ToArray())Send(topic,ArchiveAction.Remove,link/100,link%100);
                                    foreach(int link in solution.links.Except(state.links).ToArray())Send(topic,ArchiveAction.Connect,link/100,link%100);
                                }
                                if(q.tree.type==LogicLegends.Trees.TreeChallengeType.Identify)Send(topic,ArchiveAction.Select,solution.selection);
                                if(solution.flags>=0)Send(topic,ArchiveAction.Select,100+solution.flags);
                            }
                            if(q.graph!=null)
                            {
                                if(q.graph.type==GraphChallengeType.Degree){MoveTo(station,Vector3.zero);Send(topic,ArchiveAction.Select,solution.selection);}
                                else
                                {
                                    var start=q.nodes[solution.route[0]]+ArchiveTopicStation.GraphOffset;MoveTo(station,new Vector3(start.x,0,start.y));yield return .2f;
                                    for(int i=1;i<solution.route.Count;i++)
                                    {
                                        var end=q.nodes[solution.route[i]]+ArchiveTopicStation.GraphOffset;var target=station.transform.TransformPoint(new Vector3(end.x,0,end.y));
                                        var cc=Player.LocalInstance.GetComponent<CharacterController>();float until=Time.realtimeSinceStartup+35;
                                        while(HorizontalDistance(Player.LocalInstance.transform.position,target)>.18f&&Time.realtimeSinceStartup<until)
                                        {
                                            var delta=target-Player.LocalInstance.transform.position;delta.y=0;cc.Move(delta.normalized*Mathf.Min(.35f,delta.magnitude));yield return .025f;
                                        }
                                        yield return .15f;
                                    }
                                    Record(Status+" records a continuous physical graph walk",state.continuous&&state.route.SequenceEqual(solution.route));
                                }
                            }
                            yield return 1.55f;Send(topic,ArchiveAction.Submit);yield return .08f;
                        }
                        if(state.round==round&&!(topic==2&&state.subphase==1))throw new InvalidOperationException(Status+" did not advance after a correct submission. "+state.feedback);
                    }
                    report.completedRounds++;Record(Status+" accepts the complete team answer",state.round==round+1);
                    if(topic==2)Record(Status+" reveals the derived conclusion after the rule is correct",!string.IsNullOrWhiteSpace(state.reveal));
                }
                report.completedTopics++;Record("T"+(topic+1)+" completes once for the team",state.phase==2);
                if(topic==6){report.completedSetProblems=state.round;Record("Sets completes five challenges with exactly two problems each",state.round==10&&ArchiveTopicCatalog.SetChallengeCount==5&&ArchiveTopicCatalog.SetProblemsPerChallenge==2);}
            }
            Record("All ten topics complete",session.State.stations.All(s=>s.phase==2));
            Record("Correct work adds no mistakes",session.State.mistakes.Count==0);
            var roundtrip=JsonUtility.FromJson<ArchiveTeamState>(JsonUtility.ToJson(session.State));
            Record("Checkpoint preserves all ten completed topics",roundtrip.stations.All(s=>s.phase==2));
            Status="Authority and monster handoff";session.Initialize(271828);session.hunting.enabled=true;session.hunting.Request(LogicLegends.OpenArchive.Hunting.KhezuCommand.Sleep);
            var t=session.stations[0];MoveTo(t,ArchiveTopicStation.BookPosition);Send(0,ArchiveAction.Start);yield return .1f;
            var question=session.Question(0);int wrong=(question.correctChoice+1)%question.items.Length;MoveTo(t,t.ItemPosition(wrong,question));
            var command=Command(0,ArchiveAction.BeginChoice,wrong);Record("Answer channel starts",session.Accept(-1,command));yield return session.answerChannelSeconds+.2f;
            Record("Wrong rune adds exactly one attributed mistake",session.State.mistakes.Count==1&&session.State.mistakes[0].actor==-1&&session.State.mistakes[0].sequence==command.sequence&&session.State.stations[0].round==0);
            Record("Duplicate command is rejected",!session.Accept(-1,command)&&session.State.mistakes.Count==1);
            Record("Existing monster receives one awakening mistake",session.hunting.Mistakes==1);
            var stale=Command(0,ArchiveAction.BeginChoice,wrong);stale.revision--;Record("Stale station revision is rejected",!session.Accept(-1,stale));
            var owner=new GameObject("Temporary_Activity_Lock_Check");Player.LocalInstance.AcquireMovementLock(owner,"Activity validation",Player.LocalInstance.transform.position,100);
            Record("Movement-locked player cannot edit a topic",!session.Accept(-1,Command(0,ArchiveAction.BeginChoice,wrong)));Player.LocalInstance.ReleaseMovementLock(owner);UnityEngine.Object.Destroy(owner);
            MoveTo(t,Vector3.one*50);Record("Out-of-range submissions are rejected",!session.Accept(-1,Command(0,ArchiveAction.Submit)));
            Record("No rejected request creates another mistake",session.State.mistakes.Count==1);
            session.hunting.Request(LogicLegends.OpenArchive.Hunting.KhezuCommand.Sleep);session.Initialize(314159);
            var controls=session.GetComponent<ArchiveTopicTestControls>();controls.selectedTopic=0;controls.VisitSelected();ArchiveTopicUI.Current.Close();
            Record("Cleanup leaves player free and monster sleeping",!Player.LocalInstance.IsMovementLocked&&session.hunting.State==LogicLegends.OpenArchive.Hunting.KhezuHuntState.Sleeping);
        }
        static float HorizontalDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
        public static void MoveTo(ArchiveTopicStation station,Vector3 local)
        {
            var player=Player.LocalInstance;var cc=player.GetComponent<CharacterController>();var position=station.transform.TransformPoint(new Vector3(local.x,0,local.z));
            position.y+=player.transform.position.y-LogicLegends.OpenArchive.Water.OpenArchiveWater.Feet(cc).y+.1f;
            GameInput.Instance.SetMobileMovement(Vector2.zero);Vector3 delta=position-player.transform.position;cc.enabled=false;player.transform.SetPositionAndRotation(position,station.transform.rotation);cc.enabled=true;player.ResetMotionAfterTeleport();Physics.SyncTransforms();TeleportCameraSync.ResetAfterMove(player.transform,delta);
        }
        static ArchiveCommand Command(int topic,ArchiveAction action,int a=-1,int b=-1)
        {
            var actor=session.State.actors.FirstOrDefault(p=>p.actor==-1);
            return new ArchiveCommand{topic=topic,revision=session.State.stations[topic].revision,sequence=(actor?.lastSequence??0)+1,action=action,a=a,b=b};
        }
        static void Send(int topic,ArchiveAction action,int a=-1,int b=-1)
        {
            var command=Command(topic,action,a,b);if(!session.Accept(-1,command))throw new InvalidOperationException(Status+" rejected "+action+"("+a+","+b+")");report.acceptedCommands++;
        }
    }
}
