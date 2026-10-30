using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        static bool worldOnly;
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Night Hunt (Live Photon)")]
        public static void BeginWorld(){StartChecks(false,world:true);}
        static IEnumerator WorldComplete(int topic)
        {
            var station=session.stations[topic];ArchiveTopicPlayChecks.MoveTo(station,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);
            session.Request(topic,ArchiveAction.Start);yield return .1f;
            Check(session.RequestTopicAdmin(topic,session.State.stations[topic].revision,true),"Authorized Admin fixture completes topic "+(topic+1));yield return .1f;
        }
        static IEnumerator WorldFlow(Peer a,string room)
        {
            report.scope="Live private Photon room with rendered Editor host and independent Realtime peers. Admin completion fixtures exercise day/night thresholds, shared entrance clocks, namespace isolation, protected players, real second-agent movement, late join and host transfer. This is not a played seven-topic academic journey or physical Android test.";
            var world=session.worldProgression;var secondary=world.secondary;int host=PhotonNetwork.LocalPlayer.ActorNumber;
            var frames=new Dictionary<string,KhezuSnapshot>();
            a.client.EventReceived+=ev=>
            {
                if(ev.Code!=KhezuPhotonSync.SnapshotEvent||!a.client.InRoom||ev.Sender!=a.client.CurrentRoom.MasterClientId||!(ev.CustomData is object[] p)||p.Length!=4||!(p[0] is string key)||!(p[3] is string data))return;
                if(key!=KhezuPhotonSync.Channel&&key!=KhezuPhotonSync.Channel+".night-secondary")return;
                frames[key]=JsonUtility.FromJson<KhezuSnapshot>(data);
            };
            Check(world!=null&&session.State.worldTime.phase==0&&!secondary.gameObject.activeSelf,"Online match starts in daylight with one guardian");
            var fake=JsonUtility.FromJson<ArchiveTeamState>(JsonUtility.ToJson(session.State));fake.revision+=10000;fake.worldTime.phase=3;fake.worldTime.secondaryReleased=true;
            a.client.OpRaiseEvent(ArchiveTopicSession.SnapshotEvent,new object[]{ArchiveTopicSession.Channel,JsonUtility.ToJson(fake)},new RaiseEventOptions{Receivers=ReceiverGroup.All},SendOptions.SendReliable);yield return .25f;
            Check(session.State.worldTime.phase==0&&!secondary.gameObject.activeSelf,"Guest snapshot cannot forge night or a second monster");
            for(int topic=0;topic<7;topic++)
            {
                yield return WorldComplete(topic);
                int phase=ArchiveTopicSession.WorldPhaseForTopics(topic+1);
                yield return Wait(()=>a.state!=null&&a.state.worldTime.phase==phase,"Phase snapshot reaches guest");
                Check(session.State.worldTime.phase==phase&&a.state.worldTime.changedAt==session.State.worldTime.changedAt,"Host and guest share exact phase and transition start at "+(topic+1)+" topics");
            }
            double arrival=session.State.worldTime.arrivalStartedAt;Status="Shared night entrance and warning";
            yield return Wait(()=>secondary.gameObject.activeInHierarchy&&secondary.Initialized&&secondary.EntranceHeld,"Second existing rig begins entrance");
            Check(a.state.worldTime.arrivalStartedAt==arrival&&!a.state.worldTime.secondaryReleased,"Guest sees the same one-time entrance clock before release");
            yield return Move(a,session.stations[0],ArchiveTopicStation.BookPosition+Vector3.back*1.7f);
            var guest=session.ActorPlayer(a.Actor);var lockOwner=new GameObject("Night_QA_Protected_Actor");guest.AcquireMovementLock(lockOwner,"Night protected-player fixture",guest.transform.position,100);
            var secondaryChannel=secondary.GetComponent<KhezuPhotonSync>().SyncChannel;
            a.client.OpRaiseEvent(KhezuPhotonSync.CommandEvent,new object[]{secondaryChannel,999,(int)KhezuCommand.StartHunting},new RaiseEventOptions{Receivers=ReceiverGroup.MasterClient},SendOptions.SendReliable);yield return .25f;
            Check(secondary.EntranceHeld&&secondary.TargetPlayerId==0&&!session.State.worldTime.secondaryReleased,"Guest cannot skip the second guardian's warning through an AI command");
            yield return Wait(()=>session.State.worldTime.secondaryReleased,"Host warning countdown completes",20);yield return 1.1f;
            Check(secondary.TargetPlayerId!=a.Actor,"Movement-locked player is excluded from the night guardian's targets");
            guest.ReleaseMovementLock(lockOwner);UnityEngine.Object.Destroy(lockOwner);
            session.hunting.Request(KhezuCommand.ResetHunt);session.hunting.Request(KhezuCommand.StartHunting);
            secondary.Request(KhezuCommand.ResetHunt);secondary.Request(KhezuCommand.StartHunting);
            yield return Wait(()=>session.hunting.TargetPlayerId!=0&&secondary.TargetPlayerId!=0,"Both guardians acquire distinct eligible runners",12);
            Check(session.hunting.TargetPlayerId!=secondary.TargetPlayerId,"Two guardians reserve different eligible players");
            Check(secondary.health==session.hunting.health&&secondary.GetSnapshot().healthStates.Length==0,"Only the original snapshot owns the shared heart checkpoint");
            Vector3 position=secondary.transform.position;yield return 1.1f;
            Check(Vector3.Distance(position,secondary.transform.position)>.1f,"Second existing NavMesh agent really moves after the warning");
            yield return Wait(()=>frames.ContainsKey(KhezuPhotonSync.Channel)&&frames.ContainsKey(secondaryChannel),"Guest receives both monster snapshot namespaces");
            Check(frames[secondaryChannel].healthStates.Length==0,"Secondary snapshots cannot roll shared health backwards");
            var b=Connect("Night late join QA");yield return Wait(()=>b.client.State==ClientState.ConnectedToMasterServer,"Late peer connects");b.client.OpJoinRoom(new EnterRoomParams{RoomName=room});yield return Wait(()=>b.client.InRoom,"Late peer joins");
            yield return Wait(()=>b.state!=null&&b.state.worldTime.secondaryReleased,"Late peer restores released night phase");
            Check(b.state.worldTime.arrivalStartedAt==arrival&&b.state.worldTime.phase==3,"Late join does not restart the cinematic or warning");
            PhotonNetwork.SetMasterClient(PhotonNetwork.CurrentRoom.GetPlayer(a.Actor));yield return Wait(()=>!PhotonNetwork.IsMasterClient&&a.client.CurrentRoom.MasterClientId==a.Actor,"Night authority transfer");
            a.client.CurrentRoom.SetMasterClient(a.client.CurrentRoom.GetPlayer(host));yield return Wait(()=>PhotonNetwork.IsMasterClient,"Night host regains authority");yield return .4f;
            Check(session.State.worldTime.secondaryReleased&&session.State.worldTime.arrivalStartedAt==arrival,"Host transfer preserves released state without replaying arrival");
            Check(session.hunting.GetComponent<KhezuPhotonSync>().StateKey!=secondary.GetComponent<KhezuPhotonSync>().StateKey,"Both AI checkpoints remain isolated after authority transfer");
            session.hunting.Request(KhezuCommand.Sleep);secondary.Request(KhezuCommand.Sleep);
        }
    }
}
