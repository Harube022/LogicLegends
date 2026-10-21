using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExitGames.Client.Photon;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.OpenArchive.Topics;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable=ExitGames.Client.Photon.Hashtable;

namespace LogicLegends.Tests.Khezu.Editor
{
    /// <summary>Opt-in real transport validation; independent Realtime clients do not render remote UI.</summary>
    public static class BarrierTimingNetworkChecks
    {
        [Serializable] sealed class Check{public string name;public bool passed;}
        [Serializable] sealed class Report{
            public bool passed;public string checkedUtc,scope="Private live Photon room, Unity host and two independent Realtime peers. Aged capture fixtures exercise authenticated owner outcomes, deadline/checkpoint replication, actual Master handoff and movement clamping of remote avatars. Independent peers are not rendered Unity/Android clients.";
            public List<Check> checks=new List<Check>();public List<string> errors=new List<string>();
        }
        sealed class Peer{
            public LoadBalancingClient client;public ConnectionHandler heartbeat;public int viewId;public KhezuSnapshot state;
            public int Actor=>client.LocalPlayer.ActorNumber;
        }
        static Report report;static readonly List<Peer> peers=new List<Peer>();static readonly Stack<IEnumerator> stack=new Stack<IEnumerator>();
        static bool active,previousSync;static string previousName;static double next;
        static KhezuHuntingController hunt;static KhezuCaptureEncounterController encounter;
        public static string Status{get;private set;}="Idle";
        public static void Begin(){
            if(!Application.isPlaying || PhotonNetwork.InRoom || active)throw new InvalidOperationException("Offline Play Mode required.");
            report=new Report{checkedUtc=DateTime.UtcNow.ToString("o")};peers.Clear();stack.Clear();
            previousSync=PhotonNetwork.AutomaticallySyncScene;previousName=PhotonNetwork.NickName;PhotonNetwork.AutomaticallySyncScene=false;PhotonNetwork.NickName="Barrier host QA";
            var topic=ArchiveTopicSession.Current;if(topic!=null)topic.enabled=false;
            active=true;next=0;stack.Push(Flow());EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=PlayState;
        }
        static void Tick(){
            if(!active)return;
            try{
                EditorApplication.QueuePlayerLoopUpdate();foreach(var p in peers)p.client.Service();
                if(EditorApplication.timeSinceStartup<next)return;
                if(!Application.isPlaying)throw new InvalidOperationException("Play Mode interrupted.");
                if(stack.Count==0){Finish(true);return;}
                var current=stack.Peek();if(!current.MoveNext()){stack.Pop();return;}
                if(current.Current is IEnumerator child)stack.Push(child);
                else next=EditorApplication.timeSinceStartup+(current.Current is float delay?delay:0);
            }catch(Exception e){report.errors.Add(Status+": "+e);Finish(false);}
        }
        static void PlayState(PlayModeStateChange state){if(active && state==PlayModeStateChange.ExitingPlayMode){report.errors.Add("Play Mode interrupted");Finish(false);}}
        static void CheckThat(bool pass,string name){report.checks.Add(new Check{name=name,passed=pass});if(!pass)throw new InvalidOperationException(name);}
        static IEnumerator Wait(Func<bool> ready,string name,float seconds=25){
            double end=EditorApplication.timeSinceStartup+seconds;while(!ready()){if(EditorApplication.timeSinceStartup>end)throw new TimeoutException(name+"; host="+PhotonNetwork.NetworkClientState);yield return .025f;}
        }
        static Peer Connect(string name){
            var settings=PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings());settings.AppVersion=PhotonNetwork.NetworkingClient.AppVersion;settings.FixedRegion=PhotonNetwork.CloudRegion;
            var p=new Peer{client=new LoadBalancingClient()};p.client.NickName=name;
            p.client.EventReceived+=ev=>{
                if(ev.Code!=KhezuPhotonSync.SnapshotEvent || !p.client.InRoom || ev.Sender!=p.client.CurrentRoom.MasterClientId || !(ev.CustomData is object[] packet) || packet.Length!=4 || !Equals(packet[0],KhezuPhotonSync.Channel))return;
                p.state=JsonUtility.FromJson<KhezuSnapshot>((string)packet[3]);
            };
            var go=new GameObject(name+" heartbeat");UnityEngine.Object.DontDestroyOnLoad(go);p.heartbeat=go.AddComponent<ConnectionHandler>();p.heartbeat.Client=p.client;p.heartbeat.StartFallbackSendAckThread();
            peers.Add(p);p.client.ConnectUsingSettings(settings);return p;
        }
        static Player Avatar(Peer p)=>UnityEngine.Object.FindObjectsByType<Player>(FindObjectsSortMode.None).FirstOrDefault(x=>x.GetComponent<PhotonView>().OwnerActorNr==p.Actor);
        static void Spawn(Peer p,Vector3 position){
            p.viewId=p.Actor*PhotonNetwork.MAX_VIEW_IDS+1;
            p.client.OpRaiseEvent(202,new Hashtable{[(byte)0]="Multiplayer_Female",[(byte)1]=position,[(byte)6]=PhotonNetwork.ServerTimestamp,[(byte)7]=p.viewId},new RaiseEventOptions{Receivers=ReceiverGroup.Others,CachingOption=EventCaching.AddToRoomCache},SendOptions.SendReliable);
        }
        static void Motion(Peer p,Vector3 position){p.client.OpRaiseEvent(201,new object[]{PhotonNetwork.ServerTimestamp,null,new object[]{p.viewId,false,null,position,Quaternion.identity,true,true,true,20f}},new RaiseEventOptions{Receivers=ReceiverGroup.Others},SendOptions.SendReliable);}
        static void Outcome(Peer p,int serial,bool success){p.client.OpRaiseEvent(KhezuCaptureEncounterController.DevelopmentOutcomeEvent,new object[]{KhezuCaptureEncounterController.DevelopmentOutcomeChannel,serial,success},new RaiseEventOptions{Receivers=ReceiverGroup.MasterClient},SendOptions.SendReliable);}
        static void Capture(Peer p,float elapsed){
            hunt.Request(KhezuCommand.Sleep);encounter.SynchronizeBarrierMovement();
            var s=hunt.GetSnapshot();s.state=(int)KhezuHuntState.Caught;s.caught=s.target=p.Actor;s.captureSerial++;s.captureStartedAt=KhezuHuntingController.EncounterClock-KhezuCinematicJumpscareController.Duration-elapsed;
            s.encounterPhase=4;s.capturePosition=Avatar(p).transform.position;s.captureAnchorValid=true;s.awake=true;s.hunted=new[]{p.Actor};s.animation="Idle";s.normalizedTime=0;s.animationSpeed=1;
            hunt.ApplySnapshot(s,true);hunt.AuthorityChanged();
        }
        static IEnumerator Handoff(Peer peer,int host){
            CheckThat(PhotonNetwork.SetMasterClient(PhotonNetwork.CurrentRoom.GetPlayer(peer.Actor)),"Request real Master handoff");
            yield return Wait(()=>!PhotonNetwork.IsMasterClient && peer.client.LocalPlayer.IsMasterClient,"Independent peer became Master",8);
            peer.client.CurrentRoom.SetMasterClient(peer.client.CurrentRoom.GetPlayer(host));
            yield return Wait(()=>PhotonNetwork.IsMasterClient,"Unity host regains Master",8);
            yield return .1f;
        }
        static IEnumerator Flow(){
            Status="Connect private room";if(PhotonNetwork.OfflineMode)PhotonNetwork.OfflineMode=false;if(!PhotonNetwork.IsConnected)PhotonNetwork.ConnectUsingSettings();
            yield return Wait(()=>PhotonNetwork.IsConnectedAndReady,"Host connected");
            string room="barrier-timer-qa-"+Guid.NewGuid().ToString("N");PhotonNetwork.CreateRoom(room,new RoomOptions{IsVisible=false,MaxPlayers=4});yield return Wait(()=>PhotonNetwork.InRoom,"Private room ready");
            var a=Connect("Barrier owner QA");yield return Wait(()=>a.client.State==ClientState.ConnectedToMasterServer,"Owner connected");a.client.OpJoinRoom(new EnterRoomParams{RoomName=room});yield return Wait(()=>a.client.InRoom && PhotonNetwork.CurrentRoom.PlayerCount==2,"Owner joined");
            var b=Connect("Barrier observer QA");yield return Wait(()=>b.client.State==ClientState.ConnectedToMasterServer,"Observer connected");b.client.OpJoinRoom(new EnterRoomParams{RoomName=room});yield return Wait(()=>b.client.InRoom && PhotonNetwork.CurrentRoom.PlayerCount==3,"Observer joined");
            Status="Saved scene online";
            var load=EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Topics/Testing/MultiplayerGameplay.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return Wait(()=>load.isDone,"Scene loaded",60);
            yield return Wait(()=>Player.LocalInstance!=null && Player.LocalInstance.GetComponent<PhotonView>().ViewID!=0 && UnityEngine.Object.FindFirstObjectByType<KhezuHuntingController>()!=null,"Native local player ready");
            hunt=UnityEngine.Object.FindFirstObjectByType<KhezuHuntingController>();encounter=hunt.GetComponent<KhezuCaptureEncounterController>();encounter.developmentTestOutcomes=true;
            yield return Wait(()=>hunt.Initialized,"Hunting initialized");hunt.Request(KhezuCommand.Sleep);
            if(ArchiveTopicSession.Current!=null)ArchiveTopicSession.Current.enabled=false;
            var basePosition=Player.LocalInstance.transform.position;Spawn(a,basePosition+Vector3.right*12);Spawn(b,basePosition+Vector3.left*12);yield return Wait(()=>Avatar(a)!=null && Avatar(b)!=null,"Two remote native avatars");yield return .6f;
            Capture(a,5);yield return .4f;int serial=hunt.CaptureSerial,hp=hunt.health.ActorHearts(a.Actor),host=PhotonNetwork.LocalPlayer.ActorNumber;
            CheckThat(Avatar(a).IsMovementLocked && !Avatar(b).IsMovementLocked && !Player.LocalInstance.IsMovementLocked && !encounter.LocalChallengeActive && !hunt.GetComponent<KhezuBarrierStatusUI>().Visible,"Only captured remote avatar is locked; observer has no challenge UI or timer");
            yield return Wait(()=>a.state!=null && b.state!=null && a.state.barrierTimerSerial==serial && b.state.barrierTimerSerial==serial,"Timer snapshots received");
            double deadline=hunt.BarrierDeadline;
            CheckThat(Math.Abs(a.state.barrierDeadline-deadline)<.001 && Math.Abs(b.state.barrierDeadline-deadline)<.001 && Math.Abs(a.state.barrierDeadline-a.state.barrierStartedAt-180)<.001,"All peers receive one absolute 180-second room-clock deadline");
            Status="Timer host handoff";yield return Handoff(a,host);
            CheckThat(hunt.BarrierDeadline==deadline && !hunt.BarrierResolving && hunt.BarrierSecondsRemaining<175,"Master change preserves elapsed timer instead of restarting it");
            Outcome(b,serial,false);Outcome(a,serial-1,false);yield return .3f;
            CheckThat(!hunt.BarrierResolving && hunt.health.ActorHearts(a.Actor)==hp,"Authenticated wrong-owner and stale results are rejected");
            Status="Remote owner FAILED";Outcome(a,serial,false);Outcome(a,serial,false);Outcome(a,serial,true);
            yield return Wait(()=>hunt.BarrierResolving,"Owner failure accepted",3);
            CheckThat(hunt.ResolvedActor==a.Actor && hunt.ResolvedOutcome==2 && Avatar(a).IsMovementLocked && hunt.health.ActorHearts(a.Actor)==hp,"One remote failure commits attack while retaining barrier and health until contact");
            yield return Wait(()=>hunt.FailureHitApplied,"Authoritative attack contact",3);yield return .15f;
            CheckThat(hunt.health.ActorHearts(a.Actor)==hp-1 && Avatar(a).IsMovementLocked && !Avatar(b).IsMovementLocked && !Player.LocalInstance.IsMovementLocked,"Hit deducts exactly one captured-player heart and stuns only that player");
            var skins=Avatar(a).GetComponentsInChildren<Animator>().Where(x=>x.parameters.Any(p=>p.name=="IsStunned")).ToArray();
            CheckThat(skins.Length>0 && skins.All(x=>x.GetBool("IsStunned")),"Observer renders remote avatar's existing stunned animation");
            double stunEnd=hunt.StunEndsAt;var position=hunt.FailurePlayerPosition;Motion(a,position+Vector3.one*60);yield return .2f;
            CheckThat(Vector3.Distance(Avatar(a).transform.position,hunt.FailurePlayerPosition)<.01f,"Owner movement packets cannot escape the host-confirmed knockback/stun anchor");
            Motion(b,basePosition-Vector3.right*22);yield return Wait(()=>Vector3.Distance(Avatar(b).transform.position,basePosition-Vector3.right*22)<.3f,"Observer remains free",3);
            Status="Stun host handoff";yield return Handoff(b,host);
            CheckThat(hunt.StunEndsAt==stunEnd && hunt.health.ActorHearts(a.Actor)==hp-1,"Master change during stun preserves end time and serial-deduplicated damage");
            yield return Wait(()=>a.state!=null && a.state.failureHitApplied && a.state.resolvedSerial==serial,"Hit snapshot arrives at owner",3);
            CheckThat(a.state.knockbackPath.Length>0 && Math.Abs(a.state.stunEndsAt-a.state.attackHitAt-5)<.001 && a.state.healthStates.Any(x=>x.actor==a.Actor && x.hearts==hp-1),"Checkpoint includes safe trajectory, five-second stun deadline and canonical health");
            Outcome(a,serial,false);yield return .1f;CheckThat(hunt.health.ActorHearts(a.Actor)==hp-1,"Replayed outcome during stun cannot damage twice");
            yield return Wait(()=>!hunt.FailureStunActive,"Full remaining stun expires",7);yield return .1f;
            CheckThat(!Avatar(a).IsMovementLocked && skins.All(x=>!x.GetBool("IsStunned")) && hunt.TargetPlayerId!=a.Actor,"Recovered player is released and no-repeat rotation selects another eligible player");
            hunt.Request(KhezuCommand.Sleep);Status="Authoritative automatic timeout";Capture(a,179.5f);encounter.developmentTestOutcomes=false;
            hp=hunt.health.ActorHearts(a.Actor);serial=hunt.CaptureSerial;yield return Wait(()=>hunt.FailureHitApplied,"Automatic host timeout and attack",4);
            CheckThat(hunt.BarrierTimedOut && hunt.ResolvedOutcome==2 && hunt.health.ActorHearts(a.Actor)==hp-1,"Host expires barrier without owner response or enabled development controls");
            a.client.Disconnect();yield return Wait(()=>PhotonNetwork.CurrentRoom.PlayerCount==2,"Captured owner disconnect",8);
            CheckThat(!Player.LocalInstance.IsMovementLocked && !Avatar(b).IsMovementLocked,"Captured-player disconnect leaves bystanders free");
            hunt.Request(KhezuCommand.Sleep);Status="Completed";
        }
        static void Finish(bool success){
            active=false;stack.Clear();EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=PlayState;
            foreach(var p in peers){p.client.Disconnect();p.client.Service();if(p.heartbeat!=null){p.heartbeat.StopFallbackSendAckThread();UnityEngine.Object.Destroy(p.heartbeat.gameObject);}}
            PhotonNetwork.AutomaticallySyncScene=previousSync;PhotonNetwork.NickName=previousName;if(PhotonNetwork.IsConnected)PhotonNetwork.Disconnect();
            report.passed=success && report.errors.Count==0 && report.checks.All(c=>c.passed);Status=report.passed?"Passed":"Failed";
            Directory.CreateDirectory(BarrierTimingPlayChecks.Folder);File.WriteAllText(BarrierTimingPlayChecks.Folder+"/BarrierTimerAndStunPhoton.json",JsonUtility.ToJson(report,true));Debug.Log("Barrier timer Photon: "+Status+", "+report.checks.Count+" checks");
        }
    }
}
