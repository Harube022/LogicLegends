using System;
using System.Collections;
using System.Linq;
using ExitGames.Client.Photon;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Hunting;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static partial class ArchiveTopicNetworkChecks
    {
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Learning Sprint (Live Photon)")]
        public static void BeginLearning() { StartChecks(false, learning: true); }
        static void CommitLearningFixture() => typeof(ArchiveTopicSession).GetMethod("Commit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(session, null);
        static ArchiveCommand HintCommand(Peer peer)
        {
            var s = session.State.stations[1]; var cmd = Command(peer, 1, ArchiveAction.RequestHint, s.round, s.subphase);
            cmd.captureSerial = s.testResetSerial; return cmd;
        }
        static ArchiveCommand BarrierCommand(Peer peer, ArchiveAction action, int choice = -1)
        {
            var b = session.State.barrier; var cmd = Command(peer, b.sourceTopic, action, choice);
            cmd.revision = b.revision; cmd.captureSerial = b.captureSerial; cmd.encounterHuntSerial = b.huntSerial; return cmd;
        }
        static IEnumerator LearningPacket(Peer peer, ArchiveCommand cmd)
        {
            int before = session.State.revision; Packet(peer, cmd);
            yield return Wait(() => session.State.revision > before, "Learning command accepted", 8);
            yield return Wait(() => peer.state != null && peer.state.revision >= session.State.revision, "Learning receipt replicated", 8);
            report.acceptedRemoteCommands++;
        }
        static IEnumerator LearningCapture(int actor, bool inference)
        {
            var h = session.hunting; h.Request(KhezuCommand.Sleep); session.learningEncounter.inferenceBarrier = inference;
            var capture = h.GetSnapshot(); capture.state = (int)KhezuHuntState.Caught; capture.target = capture.caught = actor;
            capture.captureSerial++; capture.huntSerial++; capture.capturePosition = session.ActorPlayer(actor).transform.position;
            capture.captureAnchorValid = true; capture.captureStartedAt = KhezuHuntingController.EncounterClock - 3;
            capture.encounterPhase = 4; capture.awake = true; capture.pending = false; capture.animation = "Idle"; capture.animationSpeed = 1;
            capture.resolvedOutcome = 0; capture.barrierTimerSerial = 0;
            h.ApplySnapshot(capture, true);
            yield return Wait(() => session.State.barrier.actor == actor && session.State.barrier.captureSerial == h.CaptureSerial && h.BarrierSecondsRemaining > 0, "Host generated the confirmed capture problem", 8);
            yield return .15f;
        }
        static IEnumerator LearningFlow(Peer a, string room)
        {
            var ui = ArchiveLearningUI.Current; var h = session.hunting; int host = PhotonNetwork.LocalPlayer.ActorNumber;
            Status = "Tutorial and personal hints";
            yield return Wait(() => ui != null, "Learning UI exists in saved scene");
            var t2 = session.stations[1]; ArchiveTopicPlayChecks.MoveTo(t2, ArchiveTopicStation.BookPosition + Vector3.back * 1.7f);
            int revision = session.State.revision; ui.OpenGuide(1); yield return .2f;
            Check(ui.GuideVisible && !Player.LocalInstance.IsMovementLocked && session.State.revision == revision && !session.State.stations[1].timerRunning,
                "Optional tutorial opens without movement locking, starting a topic, or starting its clock");
            var scrolls = ui.GetComponentsInChildren<ScrollRect>(true);
            Check(scrolls.Length == 3 && scrolls.All(s => s.verticalScrollbar != null), "Tutorial, barrier reading and answer lists have visible native scrollbars");
            // Preserve the tester's first-visit preference while verifying explicit acknowledgement.
            string seenKey = "OpenArchive.Learning.v1.Seen.1"; bool hadSeen = PlayerPrefs.HasKey(seenKey); int seen = PlayerPrefs.GetInt(seenKey, 0);
            ui.DismissGuide(); Check(PlayerPrefs.GetInt(seenKey, 0) == 1, "Only explicit tutorial acknowledgement marks it seen");
            if (hadSeen) PlayerPrefs.SetInt(seenKey, seen); else PlayerPrefs.DeleteKey(seenKey); PlayerPrefs.Save();
            session.Request(1, ArchiveAction.Start); yield return Wait(() => session.State.stations[1].phase == 1, "Original Book activates T2", 8);
            double deadline = session.State.stations[1].timerDeadline; int mistakes = session.State.mistakes.Count;
            yield return Move(a, t2, Vector3.zero);
            yield return LearningPacket(a, HintCommand(a));
            var staleHint = HintCommand(a);
            yield return LearningPacket(a, HintCommand(a));
            Check(session.HintLevel(1, a.Actor) == 2 && session.HintLevel(1, host) == 0 && a.state.learningHints.Any(x => x.actor == a.Actor && x.level == 2), "Personal hint levels checkpoint for the requesting authenticated player");
            revision = session.State.revision; Packet(a, staleHint); yield return .2f;
            // Hints intentionally do not revise physical puzzle work, so sequence provides the stale-message gate.
            Check(session.State.revision == revision, "Delayed lower-sequence hint request is rejected");
            yield return LearningPacket(a, HintCommand(a));
            revision = session.State.revision; Packet(a, HintCommand(a)); yield return .2f;
            Check(session.HintLevel(1, a.Actor) == 3 && session.State.revision == revision && session.State.mistakes.Count == mistakes && session.State.stations[1].timerDeadline == deadline,
                "Three hint levels are capped and never add mistakes, pause, extend or reset the team deadline");
            yield return Move(a, t2, new Vector3(25, 0, 0)); revision = session.State.revision;
            Packet(a, HintCommand(a)); yield return .2f;
            Check(session.State.revision == revision, "Out-of-arena hint packet cannot mutate progress");
            var oldHint = HintCommand(a); int reset = session.State.stations[1].testResetSerial;
            session.RequestTopicAdmin(1, session.State.stations[1].revision, false); yield return .1f;
            Check(session.HintLevel(1, a.Actor) == 0 && session.State.stations[1].testResetSerial > reset, "Admin reset discards only the current problem's hint applicability");
            Packet(a, oldHint); yield return .2f; Check(session.HintLevel(1, a.Actor) == 0, "Pre-reset hint request cannot populate a fresh problem");
            ArchiveTopicPlayChecks.MoveTo(t2, ArchiveTopicStation.BookPosition + Vector3.back * 1.7f); session.Request(1, ArchiveAction.Start);
            yield return .15f; var q = session.Question(1); var state = session.State.stations[1]; state.placements = q.expected.ToArray(); state.placements[0] = 1 - state.placements[0]; CommitLearningFixture();
            yield return Move(a, t2, Vector3.zero); yield return Send(a, 1, ArchiveAction.Submit);
            Check(state.feedback.Contains(q.slots[0]) && session.State.mistakes.Count == mistakes + 1 && session.State.mistakes.Last().actor == a.Actor,
                "Real wrong-answer submission identifies the incorrect truth-table row and retains mistake attribution");
            h.Request(KhezuCommand.Sleep); deadline = state.timerDeadline; string work = JsonUtility.ToJson(state); mistakes = session.State.mistakes.Count;

            Status = "Rajang capture, authenticated answers and handoff";
            yield return LearningCapture(a.Actor, true);
            Check(!ui.BarrierVisible && !session.CanAct(a.Actor) && session.CanAct(host), "Remote capture hides math controls on the host and locks only the captured actor");
            Check(session.State.barrier.kind == 1 && session.State.barrier.sourceTopic == 2 && !session.learningEncounter.DevelopmentOutcomesEnabled,
                "Rajang uses original inference rules and the old development outcome selector is disabled");
            double barrierDeadline = h.BarrierDeadline;
            var b = Connect("Archive learning guest B QA"); yield return Wait(() => b.client.State == ClientState.ConnectedToMasterServer, "Learning late peer connected");
            b.client.OpJoinRoom(new EnterRoomParams { RoomName = room }); yield return Wait(() => b.client.InRoom && b.state != null && b.state.barrier.actor == a.Actor, "Late peer receives same barrier problem");
            Spawn(b, Position(t2, new Vector3(3, 0, 0))); yield return Wait(() => session.ActorPlayer(b.Actor) != null, "Learning late avatar ready");
            Check(b.state.barrier.seed == session.State.barrier.seed && b.state.barrier.captureSerial == h.CaptureSerial, "Late join does not regenerate the captured player's question");
            revision = session.State.revision; int hearts = h.health.ActorHearts(a.Actor);
            Packet(b, BarrierCommand(b, ArchiveAction.BarrierAnswer, 0), true);
            a.client.OpRaiseEvent(KhezuCaptureEncounterController.DevelopmentOutcomeEvent, new object[] { KhezuCaptureEncounterController.DevelopmentOutcomeChannel, h.CaptureSerial, true }, new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
            yield return .25f;
            Check(session.State.revision == revision && h.ResolvedOutcome == 0 && h.health.ActorHearts(a.Actor) == hearts,
                "Another actor cannot spoof the captured player and a client boolean outcome cannot bypass math validation");
            var stale = BarrierCommand(a, ArchiveAction.BarrierAnswer, 0);
            yield return LearningPacket(a, BarrierCommand(a, ArchiveAction.BarrierHint));
            revision = session.State.revision; Packet(a, stale); yield return .15f;
            var bad = BarrierCommand(a, ArchiveAction.BarrierAnswer, 999); Packet(a, bad); yield return .15f;
            bad = BarrierCommand(a, ArchiveAction.BarrierAnswer, 0); bad.encounterHuntSerial--; Packet(a, bad); yield return .15f;
            Check(session.State.revision == revision && h.ResolvedOutcome == 0 && h.BarrierDeadline == barrierDeadline,
                "Stale revision, malformed option and wrong hunt serial cannot resolve capture or alter its clock");
            int seed = session.State.barrier.seed; int hint = session.State.barrier.hintLevel;
            PhotonNetwork.SetMasterClient(PhotonNetwork.CurrentRoom.GetPlayer(a.Actor)); yield return Wait(() => !PhotonNetwork.IsMasterClient && a.client.CurrentRoom.MasterClientId == a.Actor, "Learning host handoff");
            a.client.CurrentRoom.SetMasterClient(a.client.CurrentRoom.GetPlayer(host)); yield return Wait(() => PhotonNetwork.IsMasterClient, "Learning host regains authority"); yield return .2f;
            Check(session.State.barrier.seed == seed && session.State.barrier.hintLevel == hint && h.BarrierDeadline == barrierDeadline,
                "Authority handoff preserves problem identity, personal hints and the exact original barrier deadline");
            var problem = session.BarrierProblem(); int correct = Enumerable.Range(0, problem.options.Length).First(i => problem.Validate(i, out _));
            var accepted = BarrierCommand(a, ArchiveAction.BarrierAnswer, correct); yield return LearningPacket(a, accepted);
            Check(session.State.barrier.outcome == 1 && h.ResolvedOutcome == 1 && h.health.ActorHearts(a.Actor) == hearts,
                "Captured guest's correct inference answer enters the existing success outcome without damage");
            Packet(a, accepted); yield return .15f;
            yield return Wait(() => h.State == KhezuHuntState.NestSleeping, "Existing monster returns to nest and sleeps", 20);
            Check(Math.Abs(h.NestSleepEndsAt - h.NestArrivedAt - 30) < .001 && session.State.stations[1].timerDeadline == deadline && JsonUtility.ToJson(session.State.stations[1]) == work && session.State.mistakes.Count == mistakes,
                "Success preserves arena work and its clock, causes no puzzle completion, and retains thirty-second nest sleep");

            Status = "Khezu counterexample failure, attack contact and five-second stun";
            yield return LearningCapture(a.Actor, false); problem = session.BarrierProblem();
            Check(session.State.barrier.kind == 0 && problem.options.Length == 5, "Khezu uses a counterexample problem with an explicit no-counterexample option");
            int wrong = Enumerable.Range(0, problem.options.Length).First(i => !problem.Validate(i, out _)); hearts = h.health.ActorHearts(a.Actor);
            accepted = BarrierCommand(a, ArchiveAction.BarrierAnswer, wrong); yield return LearningPacket(a, accepted);
            Check(h.ResolvedOutcome == 2 && session.State.barrier.outcome == 2 && h.health.ActorHearts(a.Actor) == hearts,
                "Incorrect confirmed math answer holds the prison and delays damage until attack contact");
            yield return Wait(() => h.FailureHitApplied, "Existing attack reaches captured player", 8);
            Check(h.health.ActorHearts(a.Actor) == hearts - 1 && h.FailureStunActive && session.ActorPlayer(a.Actor).IsMovementLocked && Math.Abs(h.StunEndsAt - h.AttackHitAt - 5) < .001,
                "Attack deducts exactly one heart at contact and transfers movement lock into the original five-second knockback/stun");
            Packet(a, accepted); yield return .2f;
            Check(h.health.ActorHearts(a.Actor) == hearts - 1, "Replaying a failed answer cannot deduct a second heart");
            yield return Wait(() => !h.FailureStunActive && !session.ActorPlayer(a.Actor).IsMovementLocked, "Existing stun expires", 10);
            Check(h.TargetPlayerId != a.Actor, "Failure resumes the existing eligible-target flow without immediately rehunting its victim");

            Status = "Local capture presentation and late-answer timeout";
            yield return LearningCapture(host, true); yield return Wait(() => ui.BarrierVisible, "Captured local player sees actual math controls", 8);
            Check(Player.LocalInstance.IsMovementLocked && !session.CanAct(host) && session.CanAct(b.Actor), "Local barrier retains complete movement lock while another living player remains free");
            var overdue = h.GetSnapshot(); overdue.barrierDeadline = KhezuHuntingController.EncounterClock - .01;
            h.ApplySnapshot(overdue, true); problem = session.BarrierProblem(); correct = Enumerable.Range(0, problem.options.Length).First(i => problem.Validate(i, out _));
            Check(!session.RequestBarrierAnswer(correct), "Correct answer after the original deadline is rejected");
            hearts = h.health.ActorHearts(host);
            yield return Wait(() => h.FailureHitApplied, "Existing timeout attacks once", 8); yield return .15f;
            Check(h.BarrierTimedOut && h.health.ActorHearts(host) == hearts - 1 && session.State.barrier.feedback.Contains("Time expired"),
                "Expired barrier uses its existing failure timer, exactly one heart and explanatory timeout feedback");
            Check(session.State.stations[1].timerDeadline == deadline && session.State.mistakes.Count == mistakes, "Barrier hints, answers and timeout never change topic mistake totals or its shared deadline");
            session.Initialize(314159); h.Request(KhezuCommand.Sleep);
            Status = "Completed learning transport checks";
        }
    }
}
