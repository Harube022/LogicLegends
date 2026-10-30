using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using ExitGames.Client.Photon;
using LogicLegends.OpenArchive.Hunting;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static partial class ArchiveTopicNetworkChecks
    {
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Team Exit (Live Photon)")]
        public static void BeginExit(){StartChecks(false,exit:true);}
        static void ExitCommit()=>typeof(ArchiveTopicSession).GetMethod("Commit",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(session,null);
        static IEnumerator MoveExit(Peer peer,Vector3 position)
        {
            peer.client.OpRaiseEvent(201,new object[]{PhotonNetwork.ServerTimestamp,null,new object[]{peer.viewId,false,null,position,Quaternion.identity,false,false,false,0f}},new RaiseEventOptions{Receivers=ReceiverGroup.Others},SendOptions.SendReliable);
            // The authority may accept entry during interpolation and anchor the avatar at the near face.
            yield return Wait(()=>session.HasEscapedActor(peer.Actor)||session.ActorPlayer(peer.Actor)!=null&&Vector3.Distance(session.ActorPlayer(peer.Actor).transform.position,position)<.3f,"Remote avatar reaches exit probe",10);
            yield return .22f;
        }
        static Action exitFixtureCleanup;
        static void CleanupExitFixture()
        {
            var cleanup=exitFixtureCleanup;exitFixtureCleanup=null;
            cleanup?.Invoke();
        }
        static IEnumerator ExitFlow(Peer a,string room)
        {
            bool oldHunting=session.hunting.enabled,oldDefeat=session.suppressDefeatForVerification;
            var world=session.worldProgression;bool oldThreats=world!=null&&world.suppressThreatsForVerification;
            exitFixtureCleanup=()=>
            {
                if(session==null)return;
                session.hunting.enabled=oldHunting;session.suppressDefeatForVerification=oldDefeat;
                if(world!=null)world.suppressThreatsForVerification=oldThreats;
            };
            // The World suite verifies night arrival. This suite's nine-topic
            // checkpoint isolates portal transactions from its cinematic/threats.
            session.hunting.enabled=false;session.suppressDefeatForVerification=true;
            if(world!=null)world.suppressThreatsForVerification=true;
            try { yield return ExitPortalFlow(a,room); }
            finally { CleanupExitFixture(); }
        }
        static IEnumerator ExitPortalFlow(Peer a,string room)
        {
            session.hunting.Request(KhezuCommand.Sleep);
            var portal=session.exitPortal;int host=PhotonNetwork.LocalPlayer.ActorNumber;
            Check(portal!=null&&portal.gameObject.scene==session.gameObject.scene,"Saved scene includes the reusable portal bound to its existing team session");
            Check(!session.PortalOpened&&!session.TeamWon&&!portal.aperture.enabled,"Portal starts hidden and nonblocking at zero completed topics");
            Status="Exit unlock and authenticated final source answer";
            for(int i=0;i<9;i++){var s=session.State.stations[i];s.round=ArchiveTopicCatalog.RoundCount(i);s.phase=2;}
            TreeState.round=4;ExitCommit();yield return .15f;
            Check(!session.PortalOpened&&session.State.stations.Count(s=>s.phase==2)==9,"Nine completed topics cannot open the portal");
            // Only the nine prior topics are fixture data. The last completion uses native guest commands and the original tree validator.
            yield return TreeBook(a);yield return TreeArrange(a);
            var fake=JsonUtility.FromJson<ArchiveTeamState>(JsonUtility.ToJson(session.State));fake.revision+=1000;
            fake.stations[9].phase=2;fake.stations[9].round=5;fake.exit.opened=fake.exit.won=true;
            a.client.OpRaiseEvent(ArchiveTopicSession.SnapshotEvent,new object[]{ArchiveTopicSession.Channel,JsonUtility.ToJson(fake)},new RaiseEventOptions{Receivers=ReceiverGroup.All},SendOptions.SendReliable);yield return .2f;
            Check(!session.PortalOpened&&!session.TeamWon&&!a.state.exit.opened,"Guest-forged topic and victory snapshots are rejected by the master and receiving peer");
            yield return Send(a,9,ArchiveAction.Submit);
            Check(session.AllTopicsComplete&&session.PortalOpened&&a.state.exit.opened&&!session.TeamWon,"Original last-challenge submission opens the same shared portal without awarding victory");
            double openedAt=session.State.exit.openedAt;
            Check(!portal.Formed&&session.State.exit.escaped.Count==0,"Ground-up portal formation cannot accept early escapes");
            yield return Wait(()=>portal.Formed&&portal.aperture.enabled,"Portal formation completes",8);
            Check(portal.visuals.gameObject.activeSelf&&portal.motes.main.maxParticles<=48,"Portal becomes visible with a bounded lightweight particle budget");
            Status="Portal bounds and local movement restrictions";
            Vector3 inside=portal.transform.position+Vector3.up*1.1f;
            yield return MoveExit(a,inside+Vector3.right*(portal.radius+3));
            Check(!session.HasEscapedActor(a.Actor),"Standing outside the oval cannot escape through the portal's surrounding stone arch");
            var remote=session.ActorPlayer(a.Actor);
            bool remoteCapsuleEnabled=remote.GetComponent<CharacterController>().enabled;
            var remoteRenderers=remote.GetComponentsInChildren<Renderer>(true);
            var originalRendererStates=remoteRenderers.Select(r=>r.forceRenderingOff).ToArray();
            remote.AcquireMovementLock(portal,"Exit validation capture lock",inside,150);
            yield return .25f;
            Check(!session.HasEscapedActor(a.Actor)&&!session.CanAct(a.Actor),"A captured or stunned actor cannot escape while movement-locked inside the portal");
            remote.AcquireMovementLock(portal,"Exit validation capture lock",inside+Vector3.back*12,150);
            yield return MoveExit(a,inside+Vector3.back*12);remote.ReleaseMovementLock(portal);yield return .15f;
            Check(!session.HasEscapedActor(a.Actor),"Being near the landmark without entering its aperture does not escape");
            yield return MoveExit(a,inside);
            yield return Wait(()=>a.state.exit.escaped.Any(e=>e.actor==a.Actor),"Guest escape replicated");
            Check(session.HasEscapedActor(a.Actor)&&!session.TeamWon&&session.SurvivorCount==2&&session.EscapedSurvivorCount==1,"First survivor escape waits for the living host");
            Check(remote.IsMovementLocked&&!remote.GetComponent<CharacterController>().enabled&&remote.GetComponentsInChildren<Renderer>(true).All(r=>r.forceRenderingOff),"Escaped network avatar is safely locked and clears the aperture for teammates");
            Check(!Player.LocalInstance.IsMovementLocked&&session.CanAct(host),"An unescaped teammate keeps movement and interaction");
            int revision=session.State.revision;ExitCommit();yield return .2f;
            Check(session.State.exit.escaped.Count==1&&session.State.exit.openedAt==openedAt,"Repeated host updates neither duplicate escape receipts nor restart portal formation");
            Check(!(bool)typeof(KhezuHuntingController).GetMethod("EscapedArchivePlayer",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{host})&&
                (bool)typeof(KhezuHuntingController).GetMethod("EscapedArchivePlayer",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{a.Actor}),"Both monster rigs exclude escaped actors while retaining remaining survivors");
            Status="Late survivor join and host handoff";
            var b=Connect("Archive portal guest B QA");yield return Wait(()=>b.client.State==ClientState.ConnectedToMasterServer,"Exit late peer connected");
            b.client.OpJoinRoom(new EnterRoomParams{RoomName=room});yield return Wait(()=>b.client.InRoom&&b.state!=null&&b.state.exit.escaped.Any(e=>e.actor==a.Actor),"Late join gets portal and escape checkpoint");
            Check(session.SurvivorCount==3&&!session.TeamWon&&b.state.exit.openedAt==openedAt,"A connected living late join is required even before its avatar spawns");
            Spawn(b,inside+Vector3.back*12);yield return Wait(()=>session.ActorPlayer(b.Actor)!=null,"Late avatar ready");
            PhotonNetwork.SetMasterClient(PhotonNetwork.CurrentRoom.GetPlayer(a.Actor));yield return Wait(()=>!PhotonNetwork.IsMasterClient&&a.client.CurrentRoom.MasterClientId==a.Actor,"Exit host handoff");
            a.client.CurrentRoom.SetMasterClient(a.client.CurrentRoom.GetPlayer(host));yield return Wait(()=>PhotonNetwork.IsMasterClient,"Exit host regains authority");yield return .2f;
            Check(session.HasEscapedActor(a.Actor)&&!session.TeamWon&&session.State.exit.openedAt==openedAt&&remote.IsMovementLocked,"Authority handoff preserves escaped roster, player safety and original portal activation time");
            yield return MoveExit(b,inside);
            Check(session.HasEscapedActor(b.Actor)&&session.EscapedSurvivorCount==2&&!session.TeamWon,"Two escaped peers cannot win before the last living teammate enters");
            Status="Final survivor and shared victory";
            var player=Player.LocalInstance;var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.position=inside+Vector3.back*9;cc.enabled=true;player.ResetMotionAfterTeleport();
            // Walk the actual CharacterController through the plane instead of calling a win API.
            float until=Time.realtimeSinceStartup+3;
            while(!session.TeamWon&&Time.realtimeSinceStartup<until){cc.Move(Vector3.forward*1.2f);yield return .035f;}
            yield return Wait(()=>session.TeamWon&&a.state.exit.won&&b.state.exit.won,"Every survivor receives team victory",8);
            Check(session.State.exit.escaped.Select(e=>e.actor).Distinct().Count()==3&&session.EscapedSurvivorCount==3,"Last survivor's physical portal entry wins for the whole team exactly once");
            Check(Player.LocalInstance.IsMovementLocked&&!cc.enabled&&session.hunting.State==KhezuHuntState.Sleeping,"Victory locks escaped local movement and sends the existing active monster to sleep");
            double wonAt=session.State.exit.wonAt;ExitCommit();yield return .2f;
            Check(session.State.exit.wonAt==wonAt&&!PhotonNetwork.CurrentRoom.IsOpen,"Shared victory is monotonic and closes further room joins");
            session.Initialize(271828);yield return .2f;
            Check(!session.PortalOpened&&!session.TeamWon&&!remote.IsMovementLocked&&remote.GetComponent<CharacterController>().enabled==remoteCapsuleEnabled&&cc.enabled&&remoteRenderers.Select(r=>r.forceRenderingOff).SequenceEqual(originalRendererStates),"New test session restores exact original avatar renderer and controller states without retaining escape locks");
            Status="Departure, elimination and empty survivor set";
            // Isolated completed-match fixtures cover roster policy without rerunning the puzzle content suite.
            // Move every avatar out of the previous aperture before opening a fresh match.
            yield return MoveExit(a,inside+Vector3.back*12);yield return MoveExit(b,inside+Vector3.back*12);
            cc.enabled=false;player.transform.position=inside+Vector3.back*12;cc.enabled=true;player.ResetMotionAfterTeleport();
            foreach(var s in session.State.stations){s.phase=2;s.round=ArchiveTopicCatalog.RoundCount(s.topic);}
            ExitCommit();yield return Wait(()=>portal.Formed,"Second portal formation",8);
            Check(!session.TeamWon&&session.State.exit.escaped.Count==0,"A new portal does not inherit the previous match's escapes");
            cc.enabled=false;player.transform.position=inside;cc.enabled=true;player.ResetMotionAfterTeleport();
            yield return Wait(()=>session.HasEscapedActor(host),"Remaining survivor physically enters the new portal",8);
            b.client.OpSetCustomPropertiesOfActor(b.Actor,new ExitGames.Client.Photon.Hashtable{{"ll.eliminated",true}});
            yield return Wait(()=>PhotonNetwork.CurrentRoom.GetPlayer(b.Actor).CustomProperties.TryGetValue("ll.eliminated",out var eliminated)&&Equals(eliminated,true),"Elimination property arrives");
            Check(session.SurvivorCount==3&&!session.TeamWon,"Owner-writable eliminated flags cannot bypass the portal while authoritative hearts remain");
            var health=session.hunting.health;var savedHealth=health.ExportActorHealth();
            for(int i=0;i<health.MaximumHealth;i++)health.DamageAuthoritativeActor(b.Actor);
            Check(health.ActorHearts(b.Actor)==0&&session.SurvivorCount==2&&!session.TeamWon,"Only actual authoritative elimination removes the late survivor from the requirement");
            a.client.Disconnect();yield return Wait(()=>PhotonNetwork.CurrentRoom.GetPlayer(a.Actor)==null,"Unescaped teammate departure");
            yield return Wait(()=>session.TeamWon,"Eliminated and departed teammates no longer block the survivor's victory",8);
            Check(session.TeamWon&&session.SurvivorCount==1,"Eliminated or disconnected players do not have to enter the portal");
            session.Initialize(31415);foreach(var s in session.State.stations){s.phase=2;s.round=ArchiveTopicCatalog.RoundCount(s.topic);}
            for(int i=0;i<health.MaximumHealth;i++)health.DamageAuthoritativeActor(host);ExitCommit();
            yield return Wait(()=>portal.Formed,"Empty roster portal formation",8);yield return .3f;
            Check(session.SurvivorCount==0&&!session.TeamWon,"Zero living survivors cannot produce a free victory");
            health.ApplyActorHealthSnapshot(savedHealth);
            session.Initialize(314159);session.hunting.Request(KhezuCommand.Sleep);
            Status="Completed portal transport checks";
        }
    }
}
