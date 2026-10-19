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
    public static class ArchiveTopicNetworkChecks
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
            public int Actor=>client.LocalPlayer.ActorNumber;
        }
        static readonly List<Peer> peers=new List<Peer>();
        static readonly Stack<IEnumerator> stack=new Stack<IEnumerator>();
        static Report report;static ArchiveTopicSession session;static ArchiveCommand lastCommand;
        static bool active,previousSync;static string previousName;static double nextTick;
        public static string Status { get;private set; }="Idle";
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Shared Progress (Live Photon)")]
        public static void Begin()
        {
            if(!Application.isPlaying||PhotonNetwork.InRoom||active)throw new InvalidOperationException("Run in offline Play Mode with no active room.");
            report=new Report();peers.Clear();stack.Clear();previousSync=PhotonNetwork.AutomaticallySyncScene;previousName=PhotonNetwork.NickName;
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
        static void Check(bool pass,string name){report.checks.Add(new CheckResult{name=name,passed=pass});Save();if(!pass)throw new InvalidOperationException(name);}
        static void Save(){Directory.CreateDirectory(ArchiveTopicSetup.Folder+"/Testing/Reports");File.WriteAllText(ArchiveTopicSetup.Folder+"/Testing/Reports/SharedProgressPhoton.json",JsonUtility.ToJson(report,true));}
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
            peer.client.OpRaiseEvent(201,new object[]{PhotonNetwork.ServerTimestamp,null,new object[]{peer.viewId,false,null,position,station.transform.rotation,false,false,false}},new RaiseEventOptions{Receivers=ReceiverGroup.Others},SendOptions.SendReliable);
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
        static IEnumerator Flow()
        {
            Status="Connect private room";
            if(PhotonNetwork.OfflineMode)PhotonNetwork.OfflineMode=false;
            if(!PhotonNetwork.IsConnected)PhotonNetwork.ConnectUsingSettings();
            yield return Wait(()=>PhotonNetwork.IsConnectedAndReady,"Host connected");
            string room="archive-topics-qa-"+Guid.NewGuid().ToString("N");PhotonNetwork.CreateRoom(room,new RoomOptions{MaxPlayers=4,IsVisible=false});
            yield return Wait(()=>PhotonNetwork.InRoom,"Private room created");
            var a=Connect("Archive guest A QA");yield return Wait(()=>a.client.State==ClientState.ConnectedToMasterServer,"Guest A connected");
            a.client.OpJoinRoom(new EnterRoomParams{RoomName=room});yield return Wait(()=>a.client.InRoom&&PhotonNetwork.CurrentRoom.PlayerCount==2,"Guest A join acknowledged by both clients");
            Check(PhotonNetwork.IsMasterClient&&PhotonNetwork.CurrentRoom.PlayerCount==2&&!PhotonNetwork.CurrentRoom.IsVisible,"Two independent clients share an invisible private room");
            Status="Load saved scene online";
            var load=EditorSceneManager.LoadSceneAsyncInPlayMode(ArchiveTopicSetup.ScenePath,new LoadSceneParameters(LoadSceneMode.Single));yield return Wait(()=>load.isDone,"Topic test scene loaded",60);
            yield return Wait(()=>ArchiveTopicSession.Current!=null&&ArchiveTopicSession.Current.Ready&&Player.LocalInstance!=null&&Player.LocalInstance.GetComponent<PhotonView>().ViewID!=0,"Network session and owned avatar ready");
            session=ArchiveTopicSession.Current;session.Initialize(314159);session.hunting.Request(KhezuCommand.Sleep);
            Check(session.ActorPlayer(PhotonNetwork.LocalPlayer.ActorNumber)==Player.LocalInstance,"Existing network player owns the local exploration controls");
            Check(session.stations.Length==10&&session.State.stations.All(s=>s.phase==0),"All ten adapted topics initialize on the authority");
            var station=session.stations[1];ArchiveTopicPlayChecks.MoveTo(station,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);session.Request(1,ArchiveAction.Start);yield return .2f;
            Check(session.State.stations[1].phase==1,"Host starts one shared Truth Table challenge");
            var q=session.Question(1);var solution=ArchiveTopicChecks.Solution(q);
            Spawn(a,Position(station,station.CurrentItemPosition(solution.placements[0],q,session.State.stations[1])));
            yield return Wait(()=>session.ActorPlayer(a.Actor)!=null,"Real guest-owned prefab instantiated");
            Check(session.ActorPlayer(a.Actor)!=Player.LocalInstance&&!session.ActorPlayer(a.Actor).enabled,"Guest replica retains independent ownership and cannot consume host input");
            Status="Guest A contributes a wrong answer";
            for(int slot=0;slot<solution.placements.Length;slot++)
            {
                int item=slot==0?1-solution.placements[slot]:solution.placements[slot];
                yield return Move(a,station,station.CurrentItemPosition(item,q,session.State.stations[1]));yield return Send(a,1,ArchiveAction.Pick,item);
                yield return Move(a,station,station.SlotPosition(slot,q));yield return Send(a,1,ArchiveAction.Place,slot);
            }
            yield return Send(a,1,ArchiveAction.Submit,spoof:true);
            Check(session.State.mistakes.Count==1&&session.State.mistakes[0].actor==a.Actor,"Forged actor field cannot change the Photon sender's mistake attribution");
            Check(session.State.actors.Single(p=>p.actor==a.Actor).mistakes==1&&session.State.stations[1].round==0,"Wrong answer retains shared work and charges exactly one actor");
            Check(session.hunting.Mistakes==1,"Attributed mistake uses the existing monster awakening controller");
            int revision=session.State.revision;Packet(a,lastCommand);yield return .5f;
            Check(session.State.revision==revision&&session.State.mistakes.Count==1,"Replayed sequence cannot double-charge a mistake");
            Status="Late join and cooperative correction";
            var b=Connect("Archive guest B QA");yield return Wait(()=>b.client.State==ClientState.ConnectedToMasterServer,"Guest B connected");
            b.client.OpJoinRoom(new EnterRoomParams{RoomName=room});yield return Wait(()=>b.client.InRoom&&PhotonNetwork.CurrentRoom.PlayerCount==3,"Guest B joins late");
            yield return Wait(()=>b.state!=null&&b.state.mistakes.Count==1,"Late guest receives authoritative progress");
            Check(b.state.stations[1].placements.SequenceEqual(session.State.stations[1].placements)&&b.state.mistakes[0].actor==a.Actor,"Late join preserves filled rows and the original mistake actor");
            Spawn(b,Position(station,station.CurrentItemPosition(solution.placements[0],q,session.State.stations[1])));yield return Wait(()=>session.ActorPlayer(b.Actor)!=null,"Second guest prefab instantiated");
            yield return Move(b,station,station.CurrentItemPosition(solution.placements[0],q,session.State.stations[1]));yield return Send(b,1,ArchiveAction.Pick,solution.placements[0]);
            yield return Move(b,station,station.SlotPosition(0,q));yield return Send(b,1,ArchiveAction.Place,0);yield return Send(b,1,ArchiveAction.Submit);
            yield return Wait(()=>a.state.stations[1].round==1&&b.state.stations[1].round==1,"Corrected round replicated to both guests");
            Check(session.State.stations[1].round==1&&session.State.mistakes.Count==1,"Another player completes the same shared answer without erasing the mistake record");
            Status="Reject forged state and distant edits";
            revision=session.State.revision;yield return Move(a,station,new Vector3(50,0,50));Packet(a,Command(a,1,ArchiveAction.Start));yield return .5f;
            Check(session.State.revision==revision,"Distant guest cannot start or edit a topic");
            var fake=JsonUtility.FromJson<ArchiveTeamState>(JsonUtility.ToJson(session.State));fake.revision+=1000;fake.stations[1].phase=2;
            a.client.OpRaiseEvent(ArchiveTopicSession.SnapshotEvent,new object[]{ArchiveTopicSession.Channel,JsonUtility.ToJson(fake)},new RaiseEventOptions{Receivers=ReceiverGroup.All},SendOptions.SendReliable);yield return .5f;
            Check(session.State.stations[1].phase==0&&b.state.stations[1].phase==0,"Non-authority snapshot is ignored by host and independent observer");
            Status="Live host handoff";
            int savedRound=session.State.stations[1].round;PhotonNetwork.SetMasterClient(PhotonNetwork.CurrentRoom.GetPlayer(a.Actor));
            yield return Wait(()=>!PhotonNetwork.IsMasterClient&&a.client.CurrentRoom.MasterClientId==a.Actor,"Temporary master change delivered");
            a.client.CurrentRoom.SetMasterClient(a.client.CurrentRoom.GetPlayer(PhotonNetwork.LocalPlayer.ActorNumber));yield return Wait(()=>PhotonNetwork.IsMasterClient,"Editor regains authority");yield return .4f;
            Check(session.State.stations[1].round==savedRound&&session.State.mistakes.Count==1&&session.State.mistakes[0].actor==a.Actor,"Actual master change preserves trusted team progress and attribution");
            session.hunting.Request(KhezuCommand.Sleep);
            Status="Concurrent reusable props and disconnect cleanup";
            ArchiveTopicPlayChecks.MoveTo(station,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);session.Request(1,ArchiveAction.Start);yield return .2f;q=session.Question(1);
            yield return Move(a,station,station.CurrentItemPosition(0,q,session.State.stations[1]));yield return Send(a,1,ArchiveAction.Pick,0);
            yield return Move(b,station,station.CurrentItemPosition(0,q,session.State.stations[1]));yield return Send(b,1,ArchiveAction.Pick,0);yield return .2f;
            Check(session.State.stations[1].carries.Count==2&&session.State.stations[1].carries.All(c=>c.item==0),"Two real actors can carry independent copies of a replenishing truth token");
            int formerActor=a.Actor;a.client.Disconnect();yield return Wait(()=>PhotonNetwork.CurrentRoom.GetPlayer(formerActor)==null,"Guest A departure delivered");yield return .2f;
            Check(session.State.stations[1].carries.Count==1&&session.State.stations[1].carries[0].actor==b.Actor,"Departure returns only the disconnected player's carried item");
            Check(session.ActorName(formerActor)=="Archive guest A QA"&&session.State.mistakes[0].actor==formerActor,"Mistake identity and saved name survive disconnect");
            yield return Send(b,1,ArchiveAction.ReturnItem);
            Status="Existing capture blocks only captured actor";
            var capture=session.hunting.GetSnapshot();capture.state=(int)KhezuHuntState.Caught;capture.target=capture.caught=b.Actor;capture.captureSerial++;capture.huntSerial++;
            capture.capturePosition=session.ActorPlayer(b.Actor).transform.position;capture.captureAnchorValid=true;capture.captureStartedAt=KhezuHuntingController.EncounterClock-3;capture.encounterPhase=4;capture.awake=true;capture.pending=false;capture.animation="Idle";capture.animationSpeed=1;
            session.hunting.ApplySnapshot(capture,true);yield return .25f;
            Check(!session.CanAct(b.Actor)&&session.CanAct(PhotonNetwork.LocalPlayer.ActorNumber),"Captured guest cannot work while the uninvolved host stays free");
            revision=session.State.revision;Packet(b,Command(b,1,ArchiveAction.Pick,0));yield return .5f;
            Check(session.State.revision==revision&&session.State.stations[1].carries.Count==0,"Captured actor's real network edit is rejected");
            Check(session.hunting.health==null||session.hunting.health.IsActorAlive(PhotonNetwork.LocalPlayer.ActorNumber),"Topic validation leaves the uninvolved player's health intact");
            session.hunting.Request(KhezuCommand.Sleep);Check(!Player.LocalInstance.IsMovementLocked,"Cleanup releases test capture without changing player systems");
            Status="Completed live transport checks";
        }
    }
}
