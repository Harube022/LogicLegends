using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExitGames.Client.Photon;
using LogicLegends.OpenArchive.Hunting;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Opt-in private Photon room checks. Independent peers exercise the actual transport and owner validation.</summary>
    public static partial class ArchiveTopicNetworkChecks
    {
        [Serializable] sealed class CheckResult { public string name;public bool passed; }
        [Serializable] sealed class Report
        {
            public bool passed;public int acceptedRemoteCommands;
            public string scope="Live private Photon room: rendered Editor host and two independent Realtime peers. Verifies transport, sender ownership, shared progress, late join and host handoff. Remote rendered UI and physical Android are not verified.";
            public List<CheckResult> checks=new List<CheckResult>();public List<string> errors=new List<string>();
        }
        sealed class Peer
        {
            public LoadBalancingClient client;public ConnectionHandler heartbeat;public ArchiveTeamState state;public int sequence,viewId;public string name;
            public readonly Dictionary<int,ArchiveCommandReceipt> receipts=new Dictionary<int,ArchiveCommandReceipt>();
            public int Actor=>client.LocalPlayer.ActorNumber;
        }
        static readonly List<Peer> peers=new List<Peer>();
        static readonly Stack<IEnumerator> stack=new Stack<IEnumerator>();
        static Report report;static ArchiveTopicSession session;static ArchiveCommand lastCommand;
        static bool active,previousSync,inferenceOnly,proofsOnly,methodsOnly,inductionOnly,setsOnly,mappingOnly,graphOnly,treeOnly,exitOnly,adminOnly,learningOnly;static string previousName;static double nextTick;
        public static string Status { get;private set; }="Idle";
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Shared Progress (Live Photon)")]
        public static void Begin()
        {
            StartChecks(false);
        }
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test T3 Shared Progress (Live Photon)")]
        public static void BeginInference(){StartChecks(true);}
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test T4 Shared Progress (Live Photon)")]
        public static void BeginProofs(){StartChecks(false,true);}
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test T5 Shared Progress (Live Photon)")]
        public static void BeginMethods(){StartChecks(false,false,true);}
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test T6 Shared Progress (Live Photon)")]
        public static void BeginInduction(){StartChecks(false,false,false,true);}
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test T7 Shared Progress (Live Photon)")]
        public static void BeginSets(){StartChecks(false,false,false,false,true);}
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test T8 Shared Progress (Live Photon)")]
        public static void BeginMapping(){StartChecks(false,false,false,false,false,true);}
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test T9 Shared Progress (Live Photon)")]
        public static void BeginGraph(){StartChecks(false,false,false,false,false,false,true);}
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test T10 Shared Progress (Live Photon)")]
        public static void BeginTree(){StartChecks(false,false,false,false,false,false,false,true);}
        static void StartChecks(bool inference,bool proofs=false,bool methods=false,bool induction=false,bool sets=false,bool mapping=false,bool graph=false,bool tree=false,bool exit=false,bool admin=false,bool learning=false,bool fairness=false,bool optional=false,bool topicBonuses=false)
        {
            if(!Application.isPlaying||PhotonNetwork.InRoom||active)throw new InvalidOperationException("Run in offline Play Mode with no active room.");
            topicBonusesOnly=topicBonuses;optionalRelaysOnly=optional;fairnessOnly=fairness;inferenceOnly=inference;proofsOnly=proofs;methodsOnly=methods;inductionOnly=induction;setsOnly=sets;mappingOnly=mapping;graphOnly=graph;treeOnly=tree;exitOnly=exit;adminOnly=admin;learningOnly=learning;report=new Report();peers.Clear();stack.Clear();previousSync=PhotonNetwork.AutomaticallySyncScene;previousName=PhotonNetwork.NickName;
            PhotonNetwork.AutomaticallySyncScene=false;PhotonNetwork.NickName="Archive host QA";
            session=ArchiveTopicSession.Current;if(session!=null)session.enabled=false;
            active=true;nextTick=0;stack.Push(Flow());EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=PlayState;Save();
        }
        static void Tick()
        {
            if(!active)return;
            try
            {
                EditorApplication.QueuePlayerLoopUpdate();foreach(var p in peers)p.client.Service();
                if(EditorApplication.timeSinceStartup<nextTick)return;
                if(!Application.isPlaying)throw new InvalidOperationException("Play Mode stopped during network validation.");
                if(stack.Count==0){Finish(true);return;}
                var current=stack.Peek();if(!current.MoveNext()){stack.Pop();return;}
                if(current.Current is IEnumerator nested)stack.Push(nested);
                else nextTick=EditorApplication.timeSinceStartup+(current.Current is float delay?delay:0);
            }
            catch(Exception e){report.errors.Add(Status+": "+e);Finish(false);}
        }
        static void PlayState(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode&&active){report.errors.Add("Play Mode interrupted.");Finish(false);}}
        static void Check(bool pass,string name){report.checks.Add(new CheckResult{name=name,passed=pass});if(!pass)throw new InvalidOperationException(name);}
        static void Save(){if(topicBonusesOnly){Directory.CreateDirectory("Temp/ArchiveTopicBonuses");File.WriteAllText("Temp/ArchiveTopicBonuses/Photon.json",JsonUtility.ToJson(report,true));return;}if(optionalRelaysOnly){Directory.CreateDirectory("Temp/ArchiveCurriculum");File.WriteAllText("Temp/ArchiveCurriculum/OptionalRelaysPhoton.json",JsonUtility.ToJson(report,true));return;}if(fairnessOnly){Directory.CreateDirectory("Temp/ArchiveFairness");File.WriteAllText("Temp/ArchiveFairness/Photon.json",JsonUtility.ToJson(report,true));return;}Directory.CreateDirectory(ArchiveTopicSetup.Folder+"/Testing/Reports");File.WriteAllText(ArchiveTopicSetup.Folder+"/Testing/Reports/"+(learningOnly?"LearningSprintPhoton.json":adminOnly?"TopicAdminPhoton.json":exitOnly?"ExitPortalPhoton.json":treeOnly?"T10SharedProgressPhoton.json":graphOnly?"T9SharedProgressPhoton.json":mappingOnly?"T8SharedProgressPhoton.json":setsOnly?"T7SharedProgressPhoton.json":inductionOnly?"T6SharedProgressPhoton.json":methodsOnly?"T5SharedProgressPhoton.json":proofsOnly?"T4SharedProgressPhoton.json":inferenceOnly?"T3SharedProgressPhoton.json":"SharedProgressPhoton.json"),JsonUtility.ToJson(report,true));}
        static void Finish(bool success)
        {
            active=false;stack.Clear();EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=PlayState;
            foreach(var p in peers)
            {
                p.client.Disconnect();p.client.Service();if(p.heartbeat!=null){p.heartbeat.StopFallbackSendAckThread();UnityEngine.Object.Destroy(p.heartbeat.gameObject);}
            }
            PhotonNetwork.AutomaticallySyncScene=previousSync;PhotonNetwork.NickName=previousName;
            if(PhotonNetwork.IsConnected)PhotonNetwork.Disconnect();
            report.passed=success&&report.errors.Count==0&&report.checks.All(c=>c.passed);Status=report.passed?"Passed":"Failed";Save();
            Debug.Log("Open Archive shared progress: "+Status+", "+report.checks.Count+" checks.");
        }
        static IEnumerator Wait(Func<bool> ready,string name,float seconds=35)
        {
            double deadline=EditorApplication.timeSinceStartup+seconds;
            while(!ready()){if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException(name+"; PUN="+PhotonNetwork.NetworkClientState+", peers="+string.Join(",",peers.Select(p=>p.client.State.ToString())));yield return .025f;}
        }
        static Peer Connect(string name)
        {
            var settings=PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings());settings.AppVersion=PhotonNetwork.NetworkingClient.AppVersion;
            settings.FixedRegion=PhotonNetwork.CloudRegion;
            var p=new Peer{client=new LoadBalancingClient(),name=name};p.client.NickName=name;
            p.client.EventReceived+=ev=>
            {
                if(ev.Code==ArchiveTopicSession.CommandReceiptEvent&&ev.CustomData is object[] receiptPacket&&receiptPacket.Length==2&&Equals(receiptPacket[0],ArchiveTopicSession.Channel))
                {var receipt=JsonUtility.FromJson<ArchiveCommandReceipt>((string)receiptPacket[1]);p.receipts[receipt.sequence]=receipt;return;}
                if(ev.Code!=ArchiveTopicSession.SnapshotEvent||!p.client.InRoom||ev.Sender!=p.client.CurrentRoom.MasterClientId||!(ev.CustomData is object[] packet)||packet.Length!=2||!Equals(packet[0],ArchiveTopicSession.Channel))return;
                try{p.state=JsonUtility.FromJson<ArchiveTeamState>((string)packet[1]);}catch(Exception e){report.errors.Add("Peer snapshot: "+e.Message);}
            };
            var keeper=new GameObject(name+" heartbeat");UnityEngine.Object.DontDestroyOnLoad(keeper);p.heartbeat=keeper.AddComponent<ConnectionHandler>();p.heartbeat.Client=p.client;p.heartbeat.StartFallbackSendAckThread();
            peers.Add(p);p.client.ConnectUsingSettings(settings);return p;
        }
        static Vector3 Position(ArchiveTopicStation station,Vector3 local){var p=station.transform.TransformPoint(local);p.y=station.transform.position.y+1.1f;return p;}
        static void Spawn(Peer peer,Vector3 position)
        {
            peer.viewId=peer.Actor*PhotonNetwork.MAX_VIEW_IDS+1;
            peer.client.OpRaiseEvent(202,new Hashtable{[(byte)0]="Multiplayer_Female",[(byte)1]=position,[(byte)6]=PhotonNetwork.ServerTimestamp,[(byte)7]=peer.viewId},new RaiseEventOptions{Receivers=ReceiverGroup.Others,CachingOption=EventCaching.AddToRoomCache},SendOptions.SendReliable);
        }
        static IEnumerator Move(Peer peer,ArchiveTopicStation station,Vector3 local)
        {
            Vector3 position=Position(station,local);
            peer.client.OpRaiseEvent(201,new object[]{PhotonNetwork.ServerTimestamp,null,new object[]{peer.viewId,false,null,position,station.transform.rotation,false,false,false,0f}},new RaiseEventOptions{Receivers=ReceiverGroup.Others},SendOptions.SendReliable);
            yield return Wait(()=>session.ActorPlayer(peer.Actor)!=null&&Vector3.Distance(session.ActorPlayer(peer.Actor).transform.position,position)<.25f,"Remote avatar reaches interaction point",8);
            yield return .05f;
        }
        static ArchiveCommand Command(Peer peer,int topic,ArchiveAction action,int a=-1,int b=-1)
        {
            var actor=session.State.actors.FirstOrDefault(p=>p.actor==peer.Actor);peer.sequence=Math.Max(peer.sequence,actor?.lastSequence??0)+1;
            return new ArchiveCommand{topic=topic,revision=session.State.stations[topic].revision,sequence=peer.sequence,action=action,a=a,b=b};
        }
        static void Packet(Peer peer,ArchiveCommand command,bool spoof=false)
        {
            string json=JsonUtility.ToJson(command);if(spoof)json=json.Substring(0,json.Length-1)+",\"actor\":"+PhotonNetwork.LocalPlayer.ActorNumber+"}";
            peer.client.OpRaiseEvent(ArchiveTopicSession.CommandEvent,new object[]{ArchiveTopicSession.Channel,json},new RaiseEventOptions{Receivers=ReceiverGroup.MasterClient},SendOptions.SendReliable);
        }
        static IEnumerator Send(Peer peer,int topic,ArchiveAction action,int a=-1,int b=-1,bool spoof=false)
        {
            int before=session.State.revision;lastCommand=Command(peer,topic,action,a,b);Packet(peer,lastCommand,spoof);
            yield return Wait(()=>session.State.revision>before,"Remote "+action+" accepted",8);report.acceptedRemoteCommands++;
            yield return Wait(()=>peer.state!=null&&peer.state.revision>=session.State.revision,"Authoritative snapshot arrives at sender",8);
        }
        static IEnumerator BlockedBook(Peer peer,int topic,string name)
        {
            yield return Move(peer,session.stations[topic],ArchiveTopicStation.BookPosition+Vector3.back*1.7f);
            int revision=session.State.revision,mistakes=session.State.mistakes.Count;
            Packet(peer,Command(peer,topic,ArchiveAction.Start),true);yield return .4f;
            Check(session.State.stations[topic].phase==0&&session.State.revision==revision&&session.State.mistakes.Count==mistakes,name);
        }
        static IEnumerator FinishTruth(Peer peer)
        {
            var station=session.stations[1];
            while(session.State.stations[1].phase!=2)
            {
                if(session.State.stations[1].phase==0)
                {
                    ArchiveTopicPlayChecks.MoveTo(station,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);session.Request(1,ArchiveAction.Start);yield return .1f;
                }
                var q=session.Question(1);var answer=ArchiveTopicChecks.Solution(q);
                for(int slot=0;slot<answer.placements.Length;slot++)
                {
                    int item=answer.placements[slot];yield return Move(peer,station,station.CurrentItemPosition(item,q,session.State.stations[1]));yield return Send(peer,1,ArchiveAction.Pick,item);
                    yield return Move(peer,station,station.SlotPosition(slot,q));yield return Send(peer,1,ArchiveAction.Place,slot);
                }
                yield return Send(peer,1,ArchiveAction.Submit);
            }
        }
        static IEnumerator Flow()
        {
            Status="Connect private room";
            if(PhotonNetwork.OfflineMode)PhotonNetwork.OfflineMode=false;
            if(!PhotonNetwork.IsConnected)PhotonNetwork.ConnectUsingSettings();
            yield return Wait(()=>PhotonNetwork.IsConnectedAndReady,"Host connected");
            string room="archive-topics-qa-"+Guid.NewGuid().ToString("N");
            PhotonNetwork.CreateRoom(room,new RoomOptions{MaxPlayers=4,IsVisible=false,CustomRoomProperties=new Hashtable{
                [LobbyRules.VersionKey]=LobbyRules.Protocol,[LobbyRules.StateKey]=LobbyRules.Loading,
                [LobbyRules.SceneKey]=ArchiveTopicSetup.ScenePath,
                [MultiplayerMonsterRotation.RoomKey]=(int)LogicLegends.Tests.Monsters.DevelopmentMonster.Rajang}});
            yield return Wait(()=>PhotonNetwork.InRoom,"Private room created");
            var a=Connect("Archive guest A QA");yield return Wait(()=>a.client.State==ClientState.ConnectedToMasterServer,"Guest A connected");
            a.client.OpJoinRoom(new EnterRoomParams{RoomName=room});yield return Wait(()=>a.client.InRoom&&PhotonNetwork.CurrentRoom.PlayerCount==2,"Guest A join acknowledged");
            Check(PhotonNetwork.IsMasterClient&&!PhotonNetwork.CurrentRoom.IsVisible,"Independent clients share an invisible private room");
            Status="Load saved scene online";
            var load=EditorSceneManager.LoadSceneAsyncInPlayMode(ArchiveTopicSetup.ScenePath,new LoadSceneParameters(LoadSceneMode.Single));yield return Wait(()=>load.isDone,"Topic scene loaded",60);
            yield return Wait(()=>ArchiveTopicSession.Current!=null&&ArchiveTopicSession.Current.Ready&&Player.LocalInstance!=null&&Player.LocalInstance.GetComponent<PhotonView>().ViewID!=0,"Owned avatar and session ready");
            session=ArchiveTopicSession.Current;session.Initialize(314159);session.hunting.Request(KhezuCommand.Sleep);
            Check(session.ActorPlayer(PhotonNetwork.LocalPlayer.ActorNumber)==Player.LocalInstance,"Native player retains local control ownership");
            var truth=session.stations[1];var t1=session.stations[0];
            Spawn(a,Position(t1,ArchiveTopicStation.BookPosition+Vector3.back*1.7f));yield return Wait(()=>session.ActorPlayer(a.Actor)!=null,"Guest-owned prefab ready");
            Check(!session.ActorPlayer(a.Actor).enabled,"Remote replica cannot consume host input");
            if(topicBonusesOnly){yield return TopicBonusesFlow(a,room);yield break;}
            if(optionalRelaysOnly){yield return OptionalRelayFlow(a,room);yield break;}
            if(fairnessOnly){yield return FairnessFlow(a,room);yield break;}
            if(learningOnly){yield return LearningFlow(a,room);yield break;}
            if(adminOnly){yield return AdminFlow(a,room);yield break;}
            if(exitOnly){yield return ExitFlow(a,room);yield break;}
            if(treeOnly){yield return TreeFlow(a,room);yield break;}
            if(graphOnly){yield return GraphFlow(a,room);yield break;}
            if(mappingOnly){yield return MappingFlow(a,room);yield break;}
            if(setsOnly){yield return SetsFlow(a,room);yield break;}
            if(inductionOnly){yield return InductionFlow(a,room);yield break;}
            if(methodsOnly){yield return MethodsFlow(a,room);yield break;}
            if(proofsOnly){yield return ProofsFlow(a,room);yield break;}
            if(inferenceOnly){yield return InferenceFlow(a,room);yield break;}
            Status="Competing topic starts";
            ArchiveTopicPlayChecks.MoveTo(truth,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);
            Packet(a,Command(a,0,ArchiveAction.Start));session.Request(1,ArchiveAction.Start);yield return .4f;
            Check(session.ActiveTopic==1&&session.State.stations.Count(s=>s.phase==1)==1&&session.State.stations[0].phase==0,"Competing host and guest Book requests activate exactly one topic");
            double truthDeadline=session.State.stations[1].timerDeadline;
            Check(session.State.stations[1].timerRunning&&session.State.stations[1].timerLimit==session.truthTimeLimitSeconds*session.PacingMultiplier&&session.State.stations[1].timerActor==PhotonNetwork.LocalPlayer.ActorNumber,"T2 starts its own six-minute shared clock at the native Book");
            yield return BlockedBook(a,0,"Guest cannot forge another topic start while Truth Table is active");
            var q=session.Question(1);var solution=ArchiveTopicChecks.Solution(q);
            Status="Shared Truth Table work and attribution";
            for(int slot=0;slot<solution.placements.Length;slot++)
            {
                int item=slot==0?1-solution.placements[slot]:solution.placements[slot];
                yield return Move(a,truth,truth.CurrentItemPosition(item,q,session.State.stations[1]));yield return Send(a,1,ArchiveAction.Pick,item);
                yield return Move(a,truth,truth.SlotPosition(slot,q));yield return Send(a,1,ArchiveAction.Place,slot);
            }
            yield return Send(a,1,ArchiveAction.Submit,spoof:true);
            Check(session.State.mistakes.Count==1&&session.State.mistakes[0].actor==a.Actor&&session.hunting.Mistakes==1,"Wrong answer charges its authenticated sender once through the existing monster controller");
            int revision=session.State.revision;Packet(a,lastCommand);yield return .3f;
            Check(session.State.revision==revision&&session.State.mistakes.Count==1,"Replay cannot charge another mistake");
            var b=Connect("Archive guest B QA");yield return Wait(()=>b.client.State==ClientState.ConnectedToMasterServer,"Guest B connected");
            b.client.OpJoinRoom(new EnterRoomParams{RoomName=room});yield return Wait(()=>b.client.InRoom&&PhotonNetwork.CurrentRoom.PlayerCount==3,"Late guest joined");
            yield return Wait(()=>b.state!=null&&b.state.mistakes.Count==1,"Late guest checkpoint received");
            Check(b.state.stations[1].phase==1&&b.state.stations[1].placements.SequenceEqual(session.State.stations[1].placements)&&b.state.mistakes[0].actor==a.Actor,"Late join retains the active topic, shared work and mistake identity");
            Check(Math.Abs(b.state.stations[1].timerDeadline-truthDeadline)<.000001&&Math.Abs(a.state.stations[1].timerDeadline-truthDeadline)<.000001&&b.state.stations[1].lastAnswerActor==a.Actor,"Late join resumes T2's exact deadline and attributed answer receipt");
            PhotonNetwork.SetMasterClient(PhotonNetwork.CurrentRoom.GetPlayer(a.Actor));yield return Wait(()=>!PhotonNetwork.IsMasterClient&&a.client.CurrentRoom.MasterClientId==a.Actor,"T2 temporary master change");
            a.client.CurrentRoom.SetMasterClient(a.client.CurrentRoom.GetPlayer(PhotonNetwork.LocalPlayer.ActorNumber));yield return Wait(()=>PhotonNetwork.IsMasterClient,"T2 Editor regains authority");yield return .2f;
            Check(Math.Abs(session.State.stations[1].timerDeadline-truthDeadline)<.000001&&session.State.stations[1].placements.SequenceEqual(b.state.stations[1].placements)&&session.ActiveTopic==1,"T2 Master handoff preserves current outputs, exact clock and team reservation");
            Spawn(b,Position(truth,truth.CurrentItemPosition(solution.placements[0],q,session.State.stations[1])));yield return Wait(()=>session.ActorPlayer(b.Actor)!=null,"Second owned replica ready");
            yield return BlockedBook(b,2,"Late guest cannot start another topic");
            yield return Move(b,truth,truth.CurrentItemPosition(solution.placements[0],q,session.State.stations[1]));yield return Send(b,1,ArchiveAction.Pick,solution.placements[0]);
            yield return Move(b,truth,truth.SlotPosition(0,q));yield return Send(b,1,ArchiveAction.Place,0);yield return Send(b,1,ArchiveAction.Submit);
            Check(session.State.stations[1].round==1&&session.ActiveTopic==1,"Another player completes a shared round while the topic remains reserved");
            Check(Math.Abs(session.State.stations[1].timerDeadline-truthDeadline)<.000001&&session.State.stations[1].placements.All(p=>p<0),"Solved T2 table keeps the team clock and clears outputs for the next expression");
            yield return BlockedBook(a,0,"Other topics remain blocked between Truth Table rounds");
            Status="T2 remote timeout and partial retry";
            yield return Move(b,truth,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);yield return Send(b,1,ArchiveAction.Start);
            q=session.Question(1);int firstOutput=q.expected[0];
            yield return Move(b,truth,truth.CurrentItemPosition(firstOutput,q,session.State.stations[1]));yield return Send(b,1,ArchiveAction.Pick,firstOutput);
            yield return Move(b,truth,truth.SlotPosition(0,q));yield return Send(b,1,ArchiveAction.Place,0);
            int[] partialTruth=session.State.stations[1].placements.ToArray();
            session.State.stations[1].timerDeadline=ArchiveTopicSession.TimerClock-.01;
            yield return Wait(()=>session.State.stations[1].timerExpired&&b.state.stations[1].timerExpired,"T2 timeout replicated",8);
            Check(session.State.stations[1].round==1&&session.State.stations[1].placements.SequenceEqual(partialTruth)&&session.State.mistakes.Count==2&&session.State.mistakes.Last().topic==1&&session.State.mistakes.Last().actor==b.Actor,"T2 timeout preserves solved and partial work and charges the guest Book activator once");
            yield return BlockedBook(a,0,"T2 timeout cannot unlock another topic");
            yield return Move(b,truth,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);yield return Send(b,1,ArchiveAction.Start);
            Check(session.State.stations[1].timerAttempt==2&&session.TimerRemaining(session.State.stations[1])>355&&session.State.stations[1].placements.SequenceEqual(partialTruth),"Guest retries T2 with a fresh six minutes and the same unfinished outputs");
            session.hunting.Request(KhezuCommand.Sleep);
            Status="Complete all nine Truth Table rounds";yield return FinishTruth(b);
            Check(session.State.stations[1].phase==2&&session.State.stations[1].round==9&&session.ActiveTopic==-1,"Nine native Truth Table rounds release the team reservation only at topic completion");
            Check(!session.State.stations[1].timerRunning&&session.State.stations[1].timerSavedRemaining>0,"Multiplayer T2 completion freezes the remaining team time");
            Status="Start T1 after completion";
            ArchiveTopicPlayChecks.MoveTo(t1,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);session.Request(0,ArchiveAction.Start);yield return .2f;
            double deadline=session.State.stations[0].timerDeadline;
            Check(session.ActiveTopic==0&&session.State.stations[0].timerRunning&&session.State.stations[0].timerLimit==session.propositionTimeLimitSeconds*session.PacingMultiplier,"Completing the previous topic allows T1 and starts its existing six-minute timer");
            var proposition=session.Question(0);yield return Move(a,t1,t1.ItemPosition(proposition.correctChoice,proposition));yield return Send(a,0,ArchiveAction.BeginChoice,proposition.correctChoice);
            Check(session.State.stations[0].channelActor==a.Actor&&session.CanAct(PhotonNetwork.LocalPlayer.ActorNumber),"Guest can cooperate on the reserved topic while the host stays free");
            yield return Wait(()=>session.State.stations[0].round==1&&a.state.stations[0].round==1,"Correct guest rune replicated",8);
            Check(a.state.stations[0].lastAnswerActor==a.Actor&&a.state.stations[0].lastAnswerResult==1&&session.ActiveTopic==0,"Answer receipt and topic reservation survive the next Book gate");
            yield return BlockedBook(b,2,"Another topic is blocked between T1 challenges");
            session.Request(0,ArchiveAction.Start);yield return .2f;
            Check(Math.Abs(session.State.stations[0].timerDeadline-deadline)<.000001,"Next Book activation preserves the shared deadline");
            yield return Move(a,t1,t1.ItemPosition(0,session.Question(0)));yield return Send(a,0,ArchiveAction.BeginChoice,0);yield return Move(a,t1,Vector3.zero);
            yield return Wait(()=>session.State.stations[0].channelActor==0,"Guest channel cancelled",8);
            Check(session.State.mistakes.Count==2,"Cancellation adds no mistake and does not release the reserved topic");
            Status="Authority validation and handoff";
            var fake=JsonUtility.FromJson<ArchiveTeamState>(JsonUtility.ToJson(session.State));fake.revision+=1000;fake.stations[0].phase=2;
            b.client.OpRaiseEvent(ArchiveTopicSession.SnapshotEvent,new object[]{ArchiveTopicSession.Channel,JsonUtility.ToJson(fake)},new RaiseEventOptions{Receivers=ReceiverGroup.All},SendOptions.SendReliable);yield return .3f;
            Check(session.ActiveTopic==0&&a.state.stations[0].phase==1,"Guest snapshot cannot release the reservation by forging completion");
            yield return Move(a,t1,t1.ItemPosition(0,session.Question(0)));yield return Send(a,0,ArchiveAction.BeginChoice,0);
            PhotonNetwork.SetMasterClient(PhotonNetwork.CurrentRoom.GetPlayer(a.Actor));yield return Wait(()=>!PhotonNetwork.IsMasterClient&&a.client.CurrentRoom.MasterClientId==a.Actor,"Temporary master change");
            a.client.CurrentRoom.SetMasterClient(a.client.CurrentRoom.GetPlayer(PhotonNetwork.LocalPlayer.ActorNumber));yield return Wait(()=>PhotonNetwork.IsMasterClient,"Editor regains authority");yield return .3f;
            Check(session.ActiveTopic==0&&session.State.stations[0].round==1&&session.State.stations[0].channelActor==0&&Math.Abs(session.State.stations[0].timerDeadline-deadline)<.000001,"Live master handoff retains the active topic, solved work and exact deadline");
            yield return BlockedBook(b,2,"Master handoff cannot unlock another topic");
            Status="Timeout and retry reservation";int before=session.State.mistakes.Count;
            session.State.stations[0].timerDeadline=ArchiveTopicSession.TimerClock-.01;
            yield return Wait(()=>session.State.stations[0].timerExpired&&b.state.stations[0].timerExpired,"Timeout replicated",8);
            Check(session.ActiveTopic==0&&session.State.stations[0].round==1&&session.State.mistakes.Count==before+1&&session.State.mistakes.Last().actor==0,"Timeout preserves the reservation and solved progress and records one team event");
            yield return BlockedBook(b,2,"Timeout cannot be used to switch topics");
            yield return Move(b,t1,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);yield return Send(b,0,ArchiveAction.Start);
            Check(session.ActiveTopic==0&&session.State.stations[0].timerAttempt==2&&session.TimerRemaining(session.State.stations[0])>355,"Guest can retry the same unfinished topic with a fresh six-minute attempt");
            yield return Move(b,t1,t1.ItemPosition(0,session.Question(0)));yield return Send(b,0,ArchiveAction.BeginChoice,0);session.State.stations[0].timerDeadline=ArchiveTopicSession.TimerClock-.01;
            yield return Wait(()=>session.State.stations[0].timerTimeouts==2&&b.state.stations[0].timerTimeouts==2,"Pending-channel timeout",8);
            Check(session.State.stations[0].channelActor==0&&session.ActiveTopic==0&&session.State.mistakes.Count==before+2&&session.State.mistakes.Last().actor==b.Actor,"Pending channel timeout charges the guest activator once and keeps T1 reserved");
            session.hunting.Request(KhezuCommand.Sleep);
            Status="Finish T1 with both guests";
            while(session.State.stations[0].phase!=2&&session.State.stations[0].phase!=ArchiveTopicSession.T1RelayPhase)
            {
                yield return Move(b,t1,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);yield return Send(b,0,ArchiveAction.Start);
                proposition=session.Question(0);int round=session.State.stations[0].round;
                yield return Move(a,t1,t1.ItemPosition(proposition.correctChoice,proposition));yield return Send(a,0,ArchiveAction.BeginChoice,proposition.correctChoice);
                yield return Wait(()=>session.State.stations[0].round==round+1&&b.state.stations[0].round==round+1,"Shared rune solved",8);
            }
            yield return CompleteRequiredRelay(a,b);
            Check(session.ActiveTopic==-1&&session.State.stations[0].round==5&&!session.State.stations[0].timerRunning,"Five native T1 challenges and the required relay release the reservation with the timer frozen");
            var inference=session.stations[2];yield return Move(a,inference,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);yield return Send(a,2,ArchiveAction.Start);
            Check(session.ActiveTopic==2&&session.State.stations[2].phase==1,"Guest can start a new topic only after the previous topic is complete");
            Status="Owned props and departure cleanup";q=session.Question(2);
            yield return Move(a,inference,inference.CurrentItemPosition(0,q,session.State.stations[2]));yield return Send(a,2,ArchiveAction.Pick,0);
            yield return Move(b,inference,inference.CurrentItemPosition(0,q,session.State.stations[2]));
            revision=session.State.revision;Packet(b,Command(b,2,ArchiveAction.Pick,0));yield return .3f;
            Check(session.State.revision==revision&&session.State.stations[2].carries.Count==1&&session.State.stations[2].carries[0].actor==a.Actor,"Another player cannot take a non-replenishing connector already carried by a teammate");
            int former=a.Actor;a.client.Disconnect();yield return Wait(()=>PhotonNetwork.CurrentRoom.GetPlayer(former)==null,"Guest A departs");yield return .2f;
            Check(session.ActiveTopic==2&&session.State.stations[2].carries.Count==0,"The initiating player can leave without releasing the topic reservation; their carried connector returns");
            Check(session.ActorName(former)=="Archive guest A QA"&&session.State.mistakes[0].actor==former,"Original mistake identity survives disconnect");
            yield return Move(b,inference,inference.CurrentItemPosition(0,q,session.State.stations[2]));yield return Send(b,2,ArchiveAction.Pick,0);
            Check(session.State.stations[2].carries.Count==1&&session.State.stations[2].carries[0].actor==b.Actor,"Remaining teammate can continue the reserved topic after its starter leaves");
            yield return Send(b,2,ArchiveAction.ReturnItem);
            Status="Capture validation";
            var capture=session.hunting.GetSnapshot();capture.state=(int)KhezuHuntState.Caught;capture.target=capture.caught=b.Actor;capture.captureSerial++;capture.huntSerial++;
            capture.capturePosition=session.ActorPlayer(b.Actor).transform.position;capture.captureAnchorValid=true;capture.captureStartedAt=KhezuHuntingController.EncounterClock-3;capture.encounterPhase=4;capture.awake=true;capture.pending=false;capture.animation="Idle";capture.animationSpeed=1;
            session.hunting.ApplySnapshot(capture,true);yield return .25f;
            Check(!session.CanAct(b.Actor)&&session.CanAct(PhotonNetwork.LocalPlayer.ActorNumber),"Capture blocks only the captured player");
            revision=session.State.revision;Packet(b,Command(b,2,ArchiveAction.Pick,0));yield return .3f;
            Check(session.State.revision==revision&&session.ActiveTopic==2,"Captured player's command cannot edit or release the reserved topic");
            session.hunting.Request(KhezuCommand.Sleep);Check(!Player.LocalInstance.IsMovementLocked,"Cleanup preserves native player controls");
            Status="Completed live transport checks";
        }
        static IEnumerator InferencePlace(Peer peer,int slot,int item){
            var st=session.stations[2];var q=session.Question(2);
            yield return Move(peer,st,st.CurrentItemPosition(item,q,session.State.stations[2]));yield return Send(peer,2,ArchiveAction.Pick,item);
            yield return Move(peer,st,st.SlotPosition(slot,q));yield return Send(peer,2,ArchiveAction.Place,slot);
        }
        static IEnumerator InferenceFlow(Peer a,string room){
            var st=session.stations[2];Status="T3 shared premise stage";
            yield return Move(a,st,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);yield return Send(a,2,ArchiveAction.Start);
            var state=session.State.stations[2];double deadline=state.timerDeadline;
            Check(session.ActiveTopic==2&&state.timerRunning&&state.timerLimit==session.inferenceTimeLimitSeconds*session.PacingMultiplier&&state.timerActor==a.Actor,"T3 guest Book starts the authenticated shared six-minute clock");
            var q=session.Question(2);yield return InferencePlace(a,0,ArchiveTopicChecks.Solution(q).placements[0]);
            var b=Connect("Archive guest B QA");yield return Wait(()=>b.client.State==ClientState.ConnectedToMasterServer,"T3 guest B connected");b.client.OpJoinRoom(new EnterRoomParams{RoomName=room});
            yield return Wait(()=>b.client.InRoom&&b.state!=null&&b.state.stations[2].placements.Length>0,"T3 late guest checkpoint");
            Spawn(b,Position(st,Vector3.zero));yield return Wait(()=>session.ActorPlayer(b.Actor)!=null,"T3 guest B avatar");
            Check(b.state.stations[2].placements.SequenceEqual(state.placements)&&Math.Abs(b.state.stations[2].timerDeadline-deadline)<.000001,"Late join restores actual placed words, current stage and exact deadline");
            yield return BlockedBook(b,1,"T3 reserves the team topic from other guest Book starts");
            yield return Move(a,st,Vector3.zero);yield return Send(a,2,ArchiveAction.Submit);
            Check(state.subphase==1&&state.round==0&&b.state.stations[2].subphase==1&&Math.Abs(state.timerDeadline-deadline)<.000001,"Correct guest premises open the shared crystal stage without resetting time");
            q=session.Question(2);int wrong=(q.expected[0]+1)%q.items.Length;
            yield return InferencePlace(a,0,wrong);yield return Send(a,2,ArchiveAction.Submit,spoof:true);
            Check(session.State.mistakes.Count==1&&session.State.mistakes[0].actor==a.Actor&&state.subphase==1&&state.placements[0]==wrong,"Wrong crystal charges its authenticated sender once and retains the altar");
            int revision=session.State.revision;Packet(a,lastCommand);yield return .25f;Check(session.State.revision==revision&&session.State.mistakes.Count==1,"Replayed T3 submission cannot repeat the penalty");
            var fake=JsonUtility.FromJson<ArchiveTeamState>(JsonUtility.ToJson(session.State));fake.revision+=1000;fake.stations[2].phase=2;
            b.client.OpRaiseEvent(ArchiveTopicSession.SnapshotEvent,new object[]{ArchiveTopicSession.Channel,JsonUtility.ToJson(fake)},new RaiseEventOptions{Receivers=ReceiverGroup.All},SendOptions.SendReliable);yield return .25f;
            Check(session.ActiveTopic==2&&a.state.stations[2].phase==1,"Untrusted guest snapshot cannot forge inference completion");
            PhotonNetwork.SetMasterClient(PhotonNetwork.CurrentRoom.GetPlayer(a.Actor));yield return Wait(()=>!PhotonNetwork.IsMasterClient&&a.client.CurrentRoom.MasterClientId==a.Actor,"T3 live master handoff");
            a.client.CurrentRoom.SetMasterClient(a.client.CurrentRoom.GetPlayer(PhotonNetwork.LocalPlayer.ActorNumber));yield return Wait(()=>PhotonNetwork.IsMasterClient,"T3 Editor regains authority");yield return .2f;
            state=session.State.stations[2];Check(state.subphase==1&&state.placements[0]==wrong&&Math.Abs(state.timerDeadline-deadline)<.000001,"Live master handoff preserves solved premises, current crystal and deadline");
            int before=session.State.mistakes.Count;state.timerDeadline=ArchiveTopicSession.TimerClock-.01;
            yield return Wait(()=>state.timerExpired&&b.state.stations[2].timerExpired,"T3 crystal timeout replicates",8);
            Check(state.subphase==1&&state.round==0&&state.placements[0]==wrong&&session.State.mistakes.Count==before+1&&session.State.mistakes.Last().actor==a.Actor,"Crystal timeout preserves the stage and adds one Book-activator mistake");
            yield return Move(b,st,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);yield return Send(b,2,ArchiveAction.Start);
            Check(state.subphase==1&&state.placements[0]==wrong&&state.timerAttempt==2&&session.TimerRemaining(state)>355,"Teammate retries directly at the saved crystal stage");
            yield return InferencePlace(b,0,q.expected[0]);yield return Send(b,2,ArchiveAction.Submit);
            Check(state.round==1&&state.phase==0&&state.reveal==q.successText,"Teammate finishes the restored challenge and receives its original conclusion");
            yield return Move(b,st,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);yield return Send(b,2,ArchiveAction.Start);q=session.Question(2);
            var solution=ArchiveTopicChecks.Solution(q).placements;
            yield return Move(a,st,st.CurrentItemPosition(solution[0],q,state));yield return Send(a,2,ArchiveAction.Pick,solution[0]);
            yield return Move(b,st,st.CurrentItemPosition(solution[0],q,state));revision=session.State.revision;Packet(b,Command(b,2,ArchiveAction.Pick,solution[0]));yield return .25f;
            Check(session.State.revision==revision&&state.carries.Count==1&&state.carries[0].actor==a.Actor,"Guest cannot steal a word tablet held by another teammate");
            int former=a.Actor;a.client.Disconnect();yield return Wait(()=>PhotonNetwork.CurrentRoom.GetPlayer(former)==null,"T3 starter leaves");yield return .2f;
            Check(state.carries.Count==0&&session.ActiveTopic==2&&session.ActorName(former)=="Archive guest A QA"&&session.State.mistakes[0].actor==former,"Departure returns carried words while preserving the reservation and mistake identity");
            yield return InferencePlace(b,0,solution[0]);var partial=state.placements.ToArray();before=session.State.mistakes.Count;state.timerDeadline=ArchiveTopicSession.TimerClock-.01;
            yield return Wait(()=>state.timerExpired&&b.state.stations[2].timerExpired,"T3 premise timeout replicates",8);
            Check(state.round==1&&state.subphase==0&&state.placements.SequenceEqual(partial)&&session.State.mistakes.Count==before+1&&session.State.mistakes.Last().actor==b.Actor,"Premise timeout keeps solved challenges and current words, attributing the current activator");
            yield return Move(b,st,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);yield return Send(b,2,ArchiveAction.Start);
            var capture=session.hunting.GetSnapshot();capture.state=(int)KhezuHuntState.Caught;capture.target=capture.caught=b.Actor;capture.captureSerial++;capture.huntSerial++;capture.capturePosition=session.ActorPlayer(b.Actor).transform.position;capture.captureAnchorValid=true;capture.captureStartedAt=KhezuHuntingController.EncounterClock-3;capture.encounterPhase=4;capture.awake=true;capture.pending=false;capture.animation="Idle";capture.animationSpeed=1;
            session.hunting.ApplySnapshot(capture,true);yield return .25f;Check(!session.CanAct(b.Actor)&&session.CanAct(PhotonNetwork.LocalPlayer.ActorNumber),"Only the captured player loses inference interaction authority");
            revision=session.State.revision;Packet(b,Command(b,2,ArchiveAction.ReturnItem));yield return .2f;Check(session.State.revision==revision&&session.ActiveTopic==2,"Captured guest cannot edit or release T3");session.hunting.Request(KhezuCommand.Sleep);yield return .2f;
            Status="Finish all five T3 challenges over Photon";
            for(int r=state.round;r<5;r++){
                if(state.phase==0){yield return Move(b,st,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);yield return Send(b,2,ArchiveAction.Start);}
                q=session.Question(2);solution=ArchiveTopicChecks.Solution(q).placements;
                for(int slot=0;slot<solution.Length;slot++)if(state.placements[slot]!=solution[slot])yield return InferencePlace(b,slot,solution[slot]);
                yield return Send(b,2,ArchiveAction.Submit);Check(state.subphase==1&&state.round==r,"Photon premise stage "+(r+1)+" solved by the remaining teammate");
                q=session.Question(2);yield return InferencePlace(b,0,q.expected[0]);yield return Send(b,2,ArchiveAction.Submit);
                Check(state.round==r+1&&state.reveal==q.successText&&b.state.stations[2].round==r+1,"Photon Rule Crystal stage "+(r+1)+" shares its conclusion");
            }
            Check(state.phase==2&&!state.timerRunning&&session.ActiveTopic==-1,"All five live T3 challenges complete, freeze time and release the next topic");
            Check(session.State.stations.Where(s=>s.topic!=2).All(s=>s.round==0&&s.timerAttempt==0),"Live T3 work preserves all other topic progress and timers");Status="Completed T3 transport checks";
        }
    }
}
