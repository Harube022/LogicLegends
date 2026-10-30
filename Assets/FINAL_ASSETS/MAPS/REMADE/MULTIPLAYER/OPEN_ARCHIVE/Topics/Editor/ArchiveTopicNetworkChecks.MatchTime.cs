using System;
using System.Collections;
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
        static bool matchTimeOnly;
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Shared Match Clock (Live Photon)")]
        public static void BeginMatchTime() => StartChecks(false, matchTime: true);

        static bool SameMatchTime(ArchiveTeamState state)
        {
            var expected = session.State.matchTime; var actual = state?.matchTime;
            return actual != null && state.matchId == session.State.matchId && actual.phase == expected.phase &&
                Math.Abs(actual.startedAt - expected.startedAt) < .000001 && Math.Abs(actual.deadline - expected.deadline) < .000001 &&
                Math.Abs(actual.escapeStartedAt - expected.escapeStartedAt) < .000001 && Math.Abs(actual.escapeDeadline - expected.escapeDeadline) < .000001 &&
                actual.awardedTopics.SequenceEqual(expected.awardedTopics) && actual.awardedActors.SequenceEqual(expected.awardedActors);
        }
        static IEnumerator MatchTimeFlow(Peer a, string room)
        {
            report.scope = "Live private Photon room: rendered Editor host and independent Realtime peers. Five genuine guest T1 rune answers verify the one-time +120s completion award. Tests frozen timer replication, forged peer snapshots, replay rejection, late join, live Master handoff and two-minute escape/timeout replication. Remaining nine topics and shortened timeout are explicitly Admin/clock fixtures. Remote rendered UI and Android devices are unverified.";
            bool originalHunt = session.hunting.enabled, originalDefeat = session.suppressDefeatForVerification;
            var world = session.worldProgression; bool originalThreats = world != null && world.suppressThreatsForVerification;
            exitFixtureCleanup = () =>
            {
                if (session == null) return;
                session.hunting.enabled = originalHunt; session.suppressDefeatForVerification = originalDefeat;
                if (world != null) world.suppressThreatsForVerification = originalThreats;
            };
            session.hunting.Request(KhezuCommand.Sleep); session.hunting.enabled = false;
            session.suppressDefeatForVerification = true; if (world != null) world.suppressThreatsForVerification = true;
            var clock = session.State.matchTime; int host = PhotonNetwork.LocalPlayer.ActorNumber;
            Status = "Shared fixed start and native guest topic completion";
            yield return Wait(() => SameMatchTime(a.state), "Guest receives the same initial active match clock", 8);
            Check(clock.phase == ArchiveMatchTimePhase.Active && Math.Abs(clock.deadline - clock.startedAt - 2400) < .000001,
                "Host starts one exactly forty-minute clock after the shared readiness countdown");
            Check(SameMatchTime(a.state) && Math.Abs(clock.startedAt - clock.countdownStartedAt - 5) < .000001,
                "Guest receives the exact host start, deadline and five-second countdown timestamps");
            double originalDeadline = clock.deadline, originalStart = clock.startedAt;
            var station = session.stations[0];
            for (int round = 0; round < 5; round++)
            {
                yield return Move(a, station, ArchiveTopicStation.BookPosition + Vector3.back * 1.7f);
                yield return Send(a, 0, ArchiveAction.Start);
                var question = session.Question(0);
                yield return Move(a, station, station.ItemPosition(question.correctChoice, question));
                yield return Send(a, 0, ArchiveAction.BeginChoice, question.correctChoice);
                int completedRound = round + 1;
                yield return Wait(() => session.State.stations[0].round == completedRound, "Guest's actual correct rune is accepted", 8);
                if (round < 4) Check(clock.deadline == originalDeadline && clock.awardedTopics.Count == 0,
                    "An intermediate accepted T1 answer cannot award topic-completion time (round " + completedRound + ")");
            }
            yield return Wait(() => SameMatchTime(a.state), "Genuine final guest completion bonus reaches sender", 8);
            clock = session.State.matchTime;
            Check(session.State.stations[0].phase == 2 && session.State.stations[0].lastAnswerActor == a.Actor &&
                Math.Abs(clock.deadline - originalDeadline - 120) < .000001 && clock.startedAt == originalStart,
                "Genuine final guest T1 answer adds exactly two minutes without resetting the match start");
            Check(clock.awardedTopics.SequenceEqual(new[] { 0 }) && clock.awardedActors.SequenceEqual(new[] { a.Actor }) &&
                a.state.matchTime.lastBonusTopic == 0 && a.state.matchTime.lastBonusActor == a.Actor,
                "Host and guest retain one completed-topic award and the authenticated final solver");
            double awardedDeadline = clock.deadline;
            Packet(a, lastCommand); yield return .25f;
            Check(session.State.matchTime.deadline == awardedDeadline && session.MatchTimeBonusCount == 1,
                "Replaying the final rune command cannot award the same topic twice");
            var forged = JsonUtility.FromJson<ArchiveTeamState>(JsonUtility.ToJson(session.State));
            forged.revision += 10000; forged.matchTime.deadline += 86400; forged.matchTime.awardedTopics.Add(1);
            a.client.OpRaiseEvent(ArchiveTopicSession.SnapshotEvent,
                new object[] { ArchiveTopicSession.Channel, JsonUtility.ToJson(forged) },
                new RaiseEventOptions { Receivers = ReceiverGroup.All }, SendOptions.SendReliable);
            yield return .3f;
            Check(session.State.matchTime.deadline == awardedDeadline && SameMatchTime(a.state) && session.MatchTimeBonusCount == 1,
                "A guest-authored timer snapshot cannot refill time or manufacture a topic award");

            Status = "Late join and live Master handoff preserve deadlines";
            var b = Connect("Match clock late guest QA");
            yield return Wait(() => b.client.State == ClientState.ConnectedToMasterServer, "Timer late peer connected");
            b.client.OpJoinRoom(new EnterRoomParams { RoomName = room });
            yield return Wait(() => b.client.InRoom && SameMatchTime(b.state), "Late peer receives the original timer checkpoint");
            Spawn(b, Position(station, Vector3.zero)); yield return Wait(() => session.ActorPlayer(b.Actor) != null, "Late timer avatar exists");
            Check(session.State.matchTime.startedAt == originalStart && session.State.matchTime.deadline == awardedDeadline &&
                b.state.matchTime.awardedTopics.SequenceEqual(new[] { 0 }), "Late join preserves elapsed time and the one earned bonus without refilling either clock");
            string match = session.State.matchId; float beforeHandoff = session.MatchTimeRemaining;
            PhotonNetwork.SetMasterClient(PhotonNetwork.CurrentRoom.GetPlayer(a.Actor));
            yield return Wait(() => !PhotonNetwork.IsMasterClient && a.client.CurrentRoom.MasterClientId == a.Actor, "Independent peer takes timer authority");
            yield return .6f;
            Check(session.State.matchTime.deadline == awardedDeadline && session.MatchTimeRemaining < beforeHandoff - .25f,
                "Shared Photon time keeps counting while the rendered client is no longer Master");
            a.client.CurrentRoom.SetMasterClient(a.client.CurrentRoom.GetPlayer(host));
            yield return Wait(() => PhotonNetwork.IsMasterClient, "Rendered host regains timer authority");
            yield return Wait(() => SameMatchTime(a.state) && SameMatchTime(b.state), "All peers receive restored timer authority checkpoint", 8);
            Check(session.State.matchId == match && session.State.matchTime.startedAt == originalStart &&
                session.State.matchTime.deadline == awardedDeadline && session.MatchTimeBonusCount == 1,
                "Master handoff preserves exact match identity, original start, deadline and one-time awards");

            Status = "Shared final escape window and timeout";
            Check(session.RequestAdminTest(ArchiveAdminTestAction.CompleteAllTopics), "Explicit Admin fixture completes the remaining nine topics");
            yield return Wait(() => session.MatchTimePhase == ArchiveMatchTimePhase.Escape && SameMatchTime(a.state) && SameMatchTime(b.state),
                "Final escape clock replicates to both peers", 8);
            clock = session.State.matchTime; double escapeDeadline = clock.escapeDeadline, escapeStart = clock.escapeStartedAt;
            Check(Math.Abs(escapeDeadline - escapeStart - 120) < .000001 && clock.deadline == awardedDeadline && session.MatchTimeBonusCount == 1,
                "All topics replace normal remaining time with exactly two minutes; Admin completion awards no extra normal time");
            CommitLearningFixture(); yield return .25f;
            Check(session.State.matchTime.escapeDeadline == escapeDeadline && session.State.matchTime.escapeStartedAt == escapeStart &&
                !session.TeamWon, "Repeated completion/checkpoint updates cannot restart the escape window or award victory");
            var hearts = session.hunting.health.ExportActorHealth(); int mistakes = session.State.mistakes.Count;
            // Only the stored authoritative deadline is shortened. Normal production
            // expiration, defeat propagation, movement locks and cleanup still run.
            session.State.matchTime.escapeDeadline = ArchiveTopicSession.Clock + .7;
            double forcedDeadline = session.State.matchTime.escapeDeadline; CommitLearningFixture();
            yield return Wait(() => SameMatchTime(a.state) && SameMatchTime(b.state), "Peers receive explicit near-expiry clock fixture", 8);
            yield return Wait(() => session.TeamDefeated && a.state?.survival?.defeated == true && b.state?.survival?.defeated == true &&
                a.state.matchTime.phase == ArchiveMatchTimePhase.Expired && b.state.matchTime.phase == ArchiveMatchTimePhase.Expired,
                "Authoritative timeout and expired phase replicate to all peers", 8);
            Check(session.MatchTimeExpired && !session.MatchTimeAllowsGameplay && !session.TeamWon && session.State.survival.defeatReason == ArchiveTopicSession.MatchTimeoutReason &&
                Math.Abs(session.State.matchTime.expiredAt - forcedDeadline) < .000001,
                "Expiration commits the exact shared deadline as team timeout, never a late victory");
            Check(hearts.All(h => session.hunting.health.ActorHearts(h.actor) == h.hearts) && session.State.mistakes.Count == mistakes,
                "Global timeout deducts no hearts and records no academic mistake");
            Check(a.state.survival.defeatReason == ArchiveTopicSession.MatchTimeoutReason && b.state.survival.defeatReason == ArchiveTopicSession.MatchTimeoutReason &&
                !session.CanAct(a.Actor) && !session.CanAct(b.Actor) && !session.CanAct(host),
                "Every peer receives the same timeout reason and all gameplay actions close");
            Status = "Shared match clock transport verified";
        }
    }
}
