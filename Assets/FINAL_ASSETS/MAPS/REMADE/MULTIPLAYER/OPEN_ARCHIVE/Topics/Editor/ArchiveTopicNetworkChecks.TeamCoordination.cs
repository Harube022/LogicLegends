using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static partial class ArchiveTopicNetworkChecks
    {
        static bool teamCoordinationOnly;
        static bool savedRotationExists,restoreRotation;
        static int savedRotation;
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Team Coordination and Rematch (Live Photon)")]
        public static void BeginTeamCoordination()
        {
            const string key="ll.multiplayer.next-monster.v1";
            savedRotationExists=PlayerPrefs.HasKey(key);savedRotation=PlayerPrefs.GetInt(key,0);restoreRotation=true;
            StartChecks(false,teamCoordination:true);
        }
        static void Save()
        {
            if(fieldEventsOnly){SaveFieldEvents();return;}
            if(matchRewardsOnly){SaveMatchRewards();return;}
            if(!teamCoordinationOnly){SaveNormal();return;}
            if(restoreRotation&&(report.passed||report.errors.Count>0))
            {
                const string key="ll.multiplayer.next-monster.v1";
                if(savedRotationExists)PlayerPrefs.SetInt(key,savedRotation);else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();restoreRotation=false;
            }
            Directory.CreateDirectory("Temp/ArchiveTeamCoordination");
            File.WriteAllText("Temp/ArchiveTeamCoordination/Photon.json",JsonUtility.ToJson(report,true));
        }
        static ArchiveCommand PingCommand(Peer peer,ArchiveTeamPingKind kind,int target=0)
        {
            var command=Command(peer,0,ArchiveAction.TeamPing,(int)kind,target);
            typeof(ArchiveTopicSession).GetMethod("StampCommandContext",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(session,new object[]{command});
            return command;
        }
        static IEnumerator Signal(Peer peer,ArchiveTeamPingKind kind,int target=0)
        {
            lastCommand=PingCommand(peer,kind,target);int before=session.State.revision;Packet(peer,lastCommand);
            yield return Wait(()=>session.State.revision>before&&peer.state?.teamPings?.Any(p=>p.actor==peer.Actor&&p.sequence==lastCommand.sequence)==true,"Authenticated guest signal replicated",8);
            report.acceptedRemoteCommands++;
        }
        static void ExpireSignalCooldown(int actor)
        {var ping=session.State.teamPings.FirstOrDefault(p=>p.actor==actor);if(ping!=null)ping.createdAt=ArchiveTopicSession.Clock-4.2;CommitLearningFixture();}
        static IEnumerator TeamCoordinationFlow(Peer a,string room)
        {
            report.scope="Live private Photon room with one rendered Editor and two independent Realtime peers. Verifies guest ping ownership/cooldown/stale commands/forged snapshots, late join, host handoff, medkit and revive context, read-only academic state, and a genuine retained-party second Start. Explicit Admin health/downed/terminal fixtures are labeled. Multiple rendered phones and physical Android are unverified.";
            Status="Validated team signals across independent peers";
            session.suppressDefeatForVerification=true;session.worldProgression.suppressThreatsForVerification=true;
            session.hunting.Request(Hunting.KhezuCommand.Sleep);
            var health=session.hunting.health.ExportActorHealth();double deadline=session.State.matchTime.deadline;
            int mistakes=session.State.mistakes.Count;int solved=session.State.solved.Count;
            yield return Signal(a,ArchiveTeamPingKind.HelpHere);
            var ping=session.State.teamPings.Single(p=>p.actor==a.Actor);
            var body=session.ActorPlayer(a.Actor).GetComponent<CharacterController>();
            Check(ping.actor==a.Actor&&Mathf.Abs(ping.position.y-body.bounds.min.y)<.3f&&ping.topic==0,"Guest help signal uses its real owned avatar, feet and nearby topic");
            Check(a.state.teamPings.Single().actor==a.Actor&&session.GetTeamRoster().Length==2,"Guest receives the same ping owner and party roster derives two actual actors");
            int revision=session.State.revision;Packet(a,lastCommand);yield return .25f;
            Check(session.State.revision==revision&&session.State.teamPings.Count==1,"Replaying a signal command cannot duplicate its receipt");
            var spam=PingCommand(a,ArchiveTeamPingKind.HelpHere);Packet(a,spam);
            yield return Wait(()=>a.receipts.ContainsKey(spam.sequence),"Host acknowledges cooldown rejection",8);
            Check(!a.receipts[spam.sequence].accepted&&session.State.revision==revision,"A remote sender cannot bypass the four-second cooldown");
            ExpireSignalCooldown(a.Actor);
            var stale=PingCommand(a,ArchiveTeamPingKind.HelpHere);stale.matchId="previous-match";Packet(a,stale);yield return .25f;
            Check(!session.State.teamPings.Any(p=>p.sequence==stale.sequence),"Stale match commands cannot manufacture a fresh signal");
            var forged=JsonUtility.FromJson<ArchiveTeamState>(JsonUtility.ToJson(session.State));forged.revision+=1000;
            forged.teamPings[0].position+=Vector3.one*9999;forged.teamPings[0].actor=PhotonNetwork.LocalPlayer.ActorNumber;
            a.client.OpRaiseEvent(ArchiveTopicSession.SnapshotEvent,new object[]{ArchiveTopicSession.Channel,JsonUtility.ToJson(forged)},new RaiseEventOptions{Receivers=ReceiverGroup.All},SendOptions.SendReliable);yield return .25f;
            Check(session.State.teamPings[0].actor==a.Actor&&(session.State.teamPings[0].position-ping.position).sqrMagnitude<.001f,"Guest-authored snapshots cannot spoof marker positions or another player's identity");
            Check(session.State.matchTime.deadline==deadline&&session.State.mistakes.Count==mistakes&&session.State.solved.Count==solved&&health.All(h=>session.hunting.health.ActorHearts(h.actor)==h.hearts),"Signals do not grant academic credit, add mistakes, consume hearts or alter the shared deadline");
            Status="Contextual requests, late join and host handoff";
            ExpireSignalCooldown(a.Actor);session.RequestAdminTest(ArchiveAdminTestAction.SetHearts,a.Actor,2);
            yield return Signal(a,ArchiveTeamPingKind.NeedMedkit);
            Check(session.State.teamPings.Single().targetActor==a.Actor&&session.TryGetTeamPingPosition(session.State.teamPings.Single(),out _),"Injured remote teammate can request a medkit at their real current position");
            var b=Connect("Team signals late guest QA");yield return Wait(()=>b.client.State==ClientState.ConnectedToMasterServer,"Team signal late peer connected");
            b.client.OpJoinRoom(new EnterRoomParams{RoomName=room});
            yield return Wait(()=>b.client.InRoom&&b.state?.teamPings?.Any(p=>p.actor==a.Actor)==true,"Late peer receives bounded signal checkpoint",8);
            Spawn(b,Position(session.stations[0],Vector3.zero));yield return Wait(()=>session.ActorPlayer(b.Actor)!=null,"Late team avatar ready");
            Check(session.GetTeamRoster().Length==3&&session.GetTeamRoster().Single(p=>p.actor==a.Actor).hearts==2,"Roster updates to three members with authoritative remote hearts");
            double signalAt=session.State.teamPings.Single().createdAt;string match=session.State.matchId;
            PhotonNetwork.SetMasterClient(PhotonNetwork.CurrentRoom.GetPlayer(a.Actor));yield return Wait(()=>!PhotonNetwork.IsMasterClient,"Signal authority handed off");
            a.client.CurrentRoom.SetMasterClient(a.client.CurrentRoom.GetPlayer(PhotonNetwork.LocalPlayer.ActorNumber));yield return Wait(()=>PhotonNetwork.IsMasterClient,"Rendered host regains signal authority");yield return .2f;
            Check(session.State.matchId==match&&Math.Abs(session.State.teamPings.Single().createdAt-signalAt)<.002,"Host handoff preserves original ping timestamps instead of refreshing their lifetime");
            ExpireSignalCooldown(a.Actor);session.RequestAdminTest(ArchiveAdminTestAction.DownAtNest,b.Actor);
            yield return Signal(a,ArchiveTeamPingKind.ReviveTeammate,b.Actor);
            Check(session.GetTeamRoster().Single(p=>p.actor==b.Actor).status=="Downed at nest"&&session.State.teamPings.Single().targetActor==b.Actor,"Revival request names the actual downed teammate and uses the existing nest state");
            session.RequestAdminTest(ArchiveAdminTestAction.ReviveAtNest,b.Actor);
            Check(!session.TryGetTeamPingPosition(session.State.teamPings.Single(),out _),"Revived teammate immediately invalidates their obsolete rescue marker");
            // Keep one test's terminal party return as the final destructive fixture.
            session.RequestAdminTest(ArchiveAdminTestAction.SetHearts,a.Actor,3);
            yield return RematchPartyFlow(a);
        }
    }
}
