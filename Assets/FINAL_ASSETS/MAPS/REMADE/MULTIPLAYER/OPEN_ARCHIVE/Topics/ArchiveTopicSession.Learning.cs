using System;
using System.Linq;
using ExitGames.Client.Photon;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Hunting;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    public sealed partial class ArchiveTopicSession
    {
        [Header("Learning support")]
        public bool enableBarrierMath;
        public KhezuCaptureEncounterController learningEncounter;
        KhezuCaptureEncounterController boundLearningEncounter;
        ArchiveBarrierProblem cachedBarrierProblem;
        string barrierProblemKey;
        void NormalizeLearning()
        {
            if (State.barrier == null) State.barrier = new ArchiveBarrierChallengeState();
            if (State.learningHints == null) State.learningHints = new System.Collections.Generic.List<ArchiveLearningHint>();
        }
        void BindLearningEncounter()
        {
            if (!enableBarrierMath) { UnbindLearningEncounter(); return; }
            if (learningEncounter == null && hunting != null) learningEncounter = hunting.GetComponent<KhezuCaptureEncounterController>();
            if (boundLearningEncounter == learningEncounter) return;
            UnbindLearningEncounter();
            boundLearningEncounter = learningEncounter;
            if (boundLearningEncounter == null) return;
            // Real validation and the development boolean selector must never both own this scene.
            boundLearningEncounter.developmentTestOutcomes = false;
            boundLearningEncounter.hostBarrierActivated.AddListener(OnLearningBarrier);
        }
        void UnbindLearningEncounter()
        {
            if (boundLearningEncounter != null) boundLearningEncounter.hostBarrierActivated.RemoveListener(OnLearningBarrier);
            boundLearningEncounter = null;
        }
        void OnLearningBarrier(int actor, int serial) { if (IsAuthority && Ready) TickLearningEncounter(); }
        void TickLearningEncounter()
        {
            BindLearningEncounter();
            if (!enableBarrierMath || !Ready || !IsAuthority || learningEncounter == null || hunting == null) return;
            NormalizeLearning();
            var b = State.barrier;
            if (hunting.State == KhezuHuntState.Caught && hunting.EncounterPhase == 4 && !hunting.BarrierResolving &&
                hunting.BarrierSecondsRemaining > 0 && KhezuHuntingController.EncounterClock - hunting.CaptureStartedAt >= KhezuCinematicJumpscareController.Duration)
            {
                if (b.actor == hunting.CaughtPlayerId && b.captureSerial == hunting.CaptureSerial && b.huntSerial == hunting.HuntSerial) return;
                int kind = learningEncounter.inferenceBarrier ? 1 : 0;
                int seed = unchecked(State.seed * 397 ^ hunting.CaptureSerial * 10007 ^ hunting.HuntSerial * 7919 ^ kind * 331);
                State.barrier = new ArchiveBarrierChallengeState
                {
                    actor = hunting.CaughtPlayerId, captureSerial = hunting.CaptureSerial, huntSerial = hunting.HuntSerial,
                    kind = kind, sourceTopic = kind == 1 ? 2 : 1, sourceRound = new System.Random(seed).Next(kind == 1 ? 5 : 9),
                    seed = seed, revision = b.revision + 1
                };
                Commit();
            }
            else if (b.actor != 0 && b.outcome == 0 && hunting.ResolvedActor == b.actor && hunting.ResolvedSerial == b.captureSerial && hunting.ResolvedOutcome != 0)
            {
                b.outcome = hunting.ResolvedOutcome; b.revision++;
                b.feedback = hunting.BarrierTimedOut ? "Time expired. The monster attacks once, then you are stunned for five seconds." : "The encounter has resolved.";
                Commit();
            }
        }
        public ArchiveBarrierProblem BarrierProblem()
        {
            if (!Ready || State.barrier == null || State.barrier.actor == 0) return null;
            var b = State.barrier;
            string key = b.kind + ":" + b.sourceRound + ":" + b.seed;
            if (cachedBarrierProblem == null || barrierProblemKey != key)
            { barrierProblemKey = key; cachedBarrierProblem = new ArchiveBarrierProblem(catalog, b); }
            return cachedBarrierProblem;
        }
        public bool IsLocalBarrierQuestion => Ready && enableBarrierMath && learningEncounter != null && hunting != null &&
            State.barrier != null && State.barrier.actor == LocalActor && State.barrier.captureSerial == hunting.CaptureSerial &&
            State.barrier.huntSerial == hunting.HuntSerial && hunting.State == KhezuHuntState.Caught && hunting.EncounterPhase == 4 &&
            hunting.CaughtPlayerId == LocalActor && (learningEncounter.cinematic == null || !learningEncounter.cinematic.IsPlaying);

        public bool RequestBarrierAnswer(int choice) => RequestBarrierLearning(ArchiveAction.BarrierAnswer, choice);
        public bool RequestBarrierHint() => RequestBarrierLearning(ArchiveAction.BarrierHint, -1);
        bool RequestBarrierLearning(ArchiveAction action, int choice)
        {
            if (!IsLocalBarrierQuestion || !learningEncounter.LocalChallengeActive) return false;
            var b = State.barrier;
            return SendLearningCommand(new ArchiveCommand { action = action, topic = b.sourceTopic, revision = b.revision,
                captureSerial = b.captureSerial, encounterHuntSerial = b.huntSerial, a = choice });
        }
        bool SendLearningCommand(ArchiveCommand command)
        {
            var actor = State.actors.FirstOrDefault(a => a.actor == LocalActor);
            localSequence = Math.Max(localSequence, actor == null ? 0 : actor.lastSequence);
            command.sequence = ++localSequence;
            if (IsAuthority) return Accept(LocalActor, command);
            return PhotonNetwork.RaiseEvent(CommandEvent, new object[] { Channel, JsonUtility.ToJson(command) },
                new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
        }
        bool FreshLearningSequence(int actor, ArchiveCommand command)
        {
            var record = State.actors.FirstOrDefault(a => a.actor == actor);
            return command.sequence > 0 && (record == null || command.sequence > record.lastSequence);
        }
        void RememberLearningSequence(int actor, ArchiveCommand command)
        {
            var record = State.actors.FirstOrDefault(a => a.actor == actor);
            if (record == null) { record = new ArchiveActor { actor = actor, displayName = ActorName(actor) }; State.actors.Add(record); }
            record.lastSequence = command.sequence;
        }
        bool AcceptBarrierLearning(int actor, ArchiveCommand command)
        {
            if (!enableBarrierMath || hunting == null || learningEncounter == null || !learningEncounter.isActiveAndEnabled || TeamWon ||
                hunting.State != KhezuHuntState.Caught || hunting.EncounterPhase != 4 || hunting.BarrierResolving ||
                hunting.BarrierSecondsRemaining <= 0 || actor != hunting.CaughtPlayerId || ActorPlayer(actor) == null ||
                KhezuHuntingController.EncounterClock - hunting.CaptureStartedAt < KhezuCinematicJumpscareController.Duration) return false;
            if (!ActorPlayer(actor).gameObject.activeInHierarchy || HasEscapedActor(actor) || hunting.health == null || !hunting.health.IsActorAlive(actor)) return false;
            if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.GetPlayer(actor).IsInactive) return false;
            var b = State.barrier;
            if (b == null || b.outcome != 0 || b.actor != actor || b.captureSerial != command.captureSerial || b.huntSerial != command.encounterHuntSerial ||
                b.captureSerial != hunting.CaptureSerial || b.huntSerial != hunting.HuntSerial || b.sourceTopic != command.topic || b.revision != command.revision ||
                !FreshLearningSequence(actor, command)) return false;
            if (command.action == ArchiveAction.BarrierHint)
            {
                if (b.hintLevel >= 3) return false;
                RememberLearningSequence(actor, command); b.hintLevel++; b.revision++; Commit(); return true;
            }
            var problem = BarrierProblem();
            if (problem == null || command.a < 0 || command.a >= problem.options.Length) return false;
            bool correct = problem.Validate(command.a, out string explanation);
            if (!learningEncounter.ResolveValidatedMathOutcome(actor, b.captureSerial, correct)) return false;
            RememberLearningSequence(actor, command); b.selected = command.a; b.outcome = correct ? 1 : 2;
            b.feedback = explanation; b.revision++; Commit(); return true;
        }

        public int HintLevel(int topic, int actor)
        {
            if (!Ready || topic < 0 || topic >= 10) return 0;
            var s = State.stations[topic];
            return State.learningHints?.FirstOrDefault(h => h.actor == actor && h.topic == topic && h.round == s.round && h.subphase == s.subphase && h.resetSerial == s.testResetSerial)?.level ?? 0;
        }
        public bool CanRequestHint(int topic) => Ready && topic >= 0 && topic < 10 && State.stations[topic].phase == 1 &&
            CanAct(LocalActor) && Near(LocalActor, topic, Vector3.zero, 14) && HintLevel(topic, LocalActor) < 3 &&
            TimerRemaining(State.stations[topic]) > 0;
        public bool RequestLearningHint(int topic)
        {
            if (!CanRequestHint(topic)) return false;
            var s = State.stations[topic];
            return SendLearningCommand(new ArchiveCommand { action = ArchiveAction.RequestHint, topic = topic, revision = s.revision,
                a = s.round, b = s.subphase, captureSerial = s.testResetSerial });
        }
        bool AcceptLearningHint(int actor, ArchiveCommand command)
        {
            var s = State.stations[command.topic];
            if (!CanAct(actor) || !Near(actor, command.topic, Vector3.zero, 14) || s.phase != 1 || command.revision != s.revision ||
                command.a != s.round || command.b != s.subphase || command.captureSerial != s.testResetSerial ||
                TimerRemaining(s) <= 0 || HintLevel(command.topic, actor) >= 3 || !FreshLearningSequence(actor, command)) return false;
            if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.GetPlayer(actor).IsInactive) return false;
            NormalizeLearning();
            var hint = State.learningHints.FirstOrDefault(h => h.actor == actor && h.topic == s.topic && h.round == s.round && h.subphase == s.subphase && h.resetSerial == s.testResetSerial);
            if (hint == null)
            {
                // Older-round hints are no longer actionable; keep the checkpoint bounded by actors × topics.
                State.learningHints.RemoveAll(h => h.actor == actor && h.topic == s.topic);
                hint = new ArchiveLearningHint { actor = actor, topic = s.topic, round = s.round, subphase = s.subphase, resetSerial = s.testResetSerial };
                State.learningHints.Add(hint);
            }
            RememberLearningSequence(actor, command); hint.level++; Commit(); return true;
        }
    }
}
