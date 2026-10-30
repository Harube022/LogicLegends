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
        public KhezuHuntingController LearningHunting => learningEncounter!=null&&learningEncounter.Hunting!=null?learningEncounter.Hunting:hunting;
        ArchiveBarrierProblem cachedBarrierProblem;
        string barrierProblemKey;
        void NormalizeLearning()
        {
            if (State.barrier == null) State.barrier = new ArchiveBarrierChallengeState();
            if (State.learningHints == null) State.learningHints = new System.Collections.Generic.List<ArchiveLearningHint>();
            // Completed questions need no diagnostic prose in every mobile room snapshot.
            // Personal hint/assistance totals stay on the existing actor records.
            State.learningHints.RemoveAll(h=>h==null||h.topic<0||h.topic>=State.stations.Length||
                State.stations[h.topic].phase==2||h.round!=State.stations[h.topic].round||
                h.subphase!=State.stations[h.topic].subphase||h.resetSerial!=State.stations[h.topic].testResetSerial);
        }
        void BindLearningEncounter()
        {
            if (!enableBarrierMath) { UnbindLearningEncounter(); return; }
            var secondary=worldProgression==null?null:worldProgression.secondary;
            if(secondary!=null&&secondary.isActiveAndEnabled&&(secondary.State==KhezuHuntState.Caught||secondary.FailureStunActive))learningEncounter=secondary.GetComponent<KhezuCaptureEncounterController>();
            else if(hunting!=null&&(hunting.State==KhezuHuntState.Caught||learningEncounter==null||LearningHunting==null||LearningHunting.State!=KhezuHuntState.AttackRecovery))learningEncounter=hunting.GetComponent<KhezuCaptureEncounterController>();
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
            if (LearningHunting.State == KhezuHuntState.Caught && LearningHunting.EncounterPhase == 4 && !LearningHunting.BarrierResolving &&
                LearningHunting.BarrierSecondsRemaining > 0 && KhezuHuntingController.EncounterClock - LearningHunting.CaptureStartedAt >= KhezuCinematicJumpscareController.Duration)
            {
                if (b.actor == LearningHunting.CaughtPlayerId && b.captureSerial == LearningHunting.CaptureSerial && b.huntSerial == LearningHunting.HuntSerial &&
                    BarrierEncounterMatches(b,LearningHunting)) return;
                int kind = learningEncounter.inferenceBarrier ? 1 : 0;
                int draw=State.monsterChallengeDrawIndex;
                int nextDraw=checked(draw+1);
                if(ArchiveMonsterChallengeBank.Count!=MonsterChallengeBankSize)
                    throw new InvalidOperationException("The shared monster challenge bank must contain exactly 50 validated questions.");
                int index=MonsterChallengeIndex(State.seed,draw);
                var entry=ArchiveMonsterChallengeBank.Get(index);
                int seed = unchecked(State.seed * 397 ^ LearningHunting.CaptureSerial * 10007 ^ LearningHunting.HuntSerial * 7919 ^ kind * 331 ^
                    draw*104729 ^ StableMonsterEncounterHash(LearningHunting.encounterId));
                State.barrier = new ArchiveBarrierChallengeState
                {
                    actor = LearningHunting.CaughtPlayerId, captureSerial = LearningHunting.CaptureSerial, huntSerial = LearningHunting.HuntSerial,
                    encounterId=LearningHunting.encounterId??"",bankVersion=ArchiveMonsterChallengeBank.CurrentVersion,questionIndex=index,
                    kind = kind, sourceTopic = entry.topic, sourceRound = index,
                    seed = seed, revision = b.revision + 1
                };
                // The single match cursor is shared by both monsters and committed with the
                // selected question. Late join and authority handoff never reroll a capture.
                State.monsterChallengeDrawIndex=nextDraw;
                Commit();
            }
            else if (b.actor != 0 && b.outcome == 0 && BarrierEncounterMatches(b,LearningHunting) &&
                LearningHunting.ResolvedActor == b.actor && LearningHunting.ResolvedSerial == b.captureSerial && LearningHunting.ResolvedOutcome != 0)
            {
                b.outcome = LearningHunting.ResolvedOutcome; b.revision++;
                b.feedback = LearningHunting.BarrierTimedOut ? "Time expired. The monster attacks once, then you are stunned for five seconds." : "The encounter has resolved.";
                Commit();
            }
        }
        public ArchiveBarrierProblem BarrierProblem()
        {
            if (!Ready || State.barrier == null || State.barrier.actor == 0) return null;
            var b = State.barrier;
            string key = b.bankVersion + ":" + b.questionIndex + ":" + b.encounterId + ":" + b.kind + ":" + b.sourceRound + ":" + b.seed;
            if (cachedBarrierProblem == null || barrierProblemKey != key)
            { barrierProblemKey = key; cachedBarrierProblem = new ArchiveBarrierProblem(catalog, b); }
            return cachedBarrierProblem;
        }
        public bool IsLocalBarrierQuestion => Ready && enableBarrierMath && learningEncounter != null && hunting != null &&
            State.barrier != null && State.barrier.actor == LocalActor && BarrierEncounterMatches(State.barrier,LearningHunting) && State.barrier.captureSerial == LearningHunting.CaptureSerial &&
            State.barrier.huntSerial == LearningHunting.HuntSerial && LearningHunting.State == KhezuHuntState.Caught && LearningHunting.EncounterPhase == 4 &&
            LearningHunting.CaughtPlayerId == LocalActor && (learningEncounter.cinematic == null || !learningEncounter.cinematic.IsPlaying);

        public bool RequestBarrierAnswer(int choice) => RequestBarrierLearning(ArchiveAction.BarrierAnswer, choice);
        public bool RequestBarrierHint() => RequestBarrierLearning(ArchiveAction.BarrierHint, -1);
        bool RequestBarrierLearning(ArchiveAction action, int choice)
        {
            if (!IsLocalBarrierQuestion || !learningEncounter.LocalChallengeActive) return false;
            var b = State.barrier;
            return SendLearningCommand(new ArchiveCommand { action = action, topic = b.sourceTopic, revision = b.revision,
                captureSerial = b.captureSerial, encounterHuntSerial = b.huntSerial, encounterId=b.encounterId, a = choice });
        }
        bool SendLearningCommand(ArchiveCommand command)
        {
            var actor = State.actors.FirstOrDefault(a => a.actor == LocalActor);
            localSequence = Math.Max(localSequence, actor == null ? 0 : actor.lastSequence);
            command.sequence = ++localSequence;
            command.matchId=State.matchId;
            return DispatchCommand(command);
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
                LearningHunting.State != KhezuHuntState.Caught || LearningHunting.EncounterPhase != 4 || LearningHunting.BarrierResolving ||
                LearningHunting.BarrierSecondsRemaining <= 0 || actor != LearningHunting.CaughtPlayerId || ActorPlayer(actor) == null ||
                KhezuHuntingController.EncounterClock - LearningHunting.CaptureStartedAt < KhezuCinematicJumpscareController.Duration) return false;
            if (!ActorPlayer(actor).gameObject.activeInHierarchy || HasEscapedActor(actor) || LearningHunting.health == null || !LearningHunting.health.IsActorAlive(actor)) return false;
            if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.GetPlayer(actor).IsInactive) return false;
            var b = State.barrier;
            if (b == null || b.outcome != 0 || b.actor != actor || b.captureSerial != command.captureSerial || b.huntSerial != command.encounterHuntSerial ||
                b.captureSerial != LearningHunting.CaptureSerial || b.huntSerial != LearningHunting.HuntSerial || b.sourceTopic != command.topic || b.revision != command.revision ||
                !BarrierEncounterMatches(b,LearningHunting)||!string.Equals(command.encounterId??"",b.encounterId??"",StringComparison.Ordinal)||!FreshLearningSequence(actor, command)) return false;
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
        public string LearningHintText(int topic,int actor,int level)
        {
            if(!Ready||topic<0||topic>=10||level<1||level>3)return "";
            var s=State.stations[topic];
            var hint=State.learningHints?.FirstOrDefault(h=>h.actor==actor&&h.topic==topic&&h.round==s.round&&h.subphase==s.subphase&&h.resetSerial==s.testResetSerial);
            if(hint==null||level>hint.level)return "";
            if(level!=2)return ArchiveLearningFeedback.Hint(topic,Question(topic),s,level);
            return !string.IsNullOrEmpty(hint.diagnosis)?hint.diagnosis:
                "This hint was requested before the current match update. Use the concept guide to review your work.";
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
            if (!CanAct(actor) || !Near(actor, command.topic, Vector3.zero, 14) || s.phase != 1 ||
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
            // Diagnosis describes the work at request time. Moving a block cannot turn an open
            // hint panel into a live answer oracle without making a deliberate new hint request.
            if(hint.level==1)hint.diagnosis="AT THE TIME YOU REQUESTED THIS HINT\n"+ArchiveLearningFeedback.Hint(s.topic,Question(s.topic),s,2);
            RememberLearningSequence(actor, command); hint.level++;
            State.actors.First(a=>a.actor==actor).hintsUsed++;
            Commit(); return true;
        }
    }
}
