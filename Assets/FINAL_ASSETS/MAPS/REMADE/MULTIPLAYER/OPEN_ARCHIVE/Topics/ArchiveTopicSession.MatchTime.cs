using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    public enum ArchiveMatchTimePhase { WaitingForPlayers, Countdown, Active, Escape, Expired }

    /// <summary>All deadlines and one-time awards travel with the existing authoritative team checkpoint.</summary>
    [Serializable] public sealed class ArchiveMatchTimeState
    {
        public int version = 1;
        public ArchiveMatchTimePhase phase;
        public double createdAt, readinessDeadline;
        public double countdownStartedAt = -1, startedAt = -1, deadline = -1;
        public double escapeStartedAt = -1, escapeDeadline = -1, expiredAt = -1;
        public bool readinessTimedOut;
        public List<int> awardedTopics = new List<int>(), awardedActors = new List<int>();
        public double lastBonusAt = -1;
        public int lastBonusTopic = -1, lastBonusActor;
    }

    public sealed partial class ArchiveTopicSession
    {
        public const float MatchBaseSeconds = 40 * 60, MatchTopicBonusSeconds = 2 * 60, MatchEscapeSeconds = 2 * 60;
        public const float MatchReadinessGraceSeconds = 15, MatchCountdownSeconds = 5;
        public const float MatchMaximumPlaySeconds = MatchBaseSeconds + 10 * MatchTopicBonusSeconds + MatchEscapeSeconds;
        public const string MatchTimeoutReason = "timeout";

        public ArchiveMatchTimePhase MatchTimePhase => State?.matchTime?.phase ?? ArchiveMatchTimePhase.WaitingForPlayers;
        public int MatchTimeBonusCount => State?.matchTime?.awardedTopics?.Distinct().Count(t => t >= 0 && t < 10) ?? 0;
        public float MatchTimeReadinessRemaining => State?.matchTime == null ? MatchReadinessGraceSeconds :
            Mathf.Max(0, (float)(State.matchTime.readinessDeadline - Clock));
        public float MatchTimeCountdownRemaining => State?.matchTime == null || MatchTimePhase != ArchiveMatchTimePhase.Countdown ? 0 :
            Mathf.Clamp((float)(State.matchTime.countdownStartedAt + MatchCountdownSeconds - Clock), 0, MatchCountdownSeconds);
        public double MatchTimeAbsoluteEnd => State?.matchTime == null || State.matchTime.startedAt < 0 ? -1 :
            State.matchTime.startedAt + MatchMaximumPlaySeconds;
        public float MatchTimeRemaining
        {
            get
            {
                var time = State?.matchTime;
                if (time == null || time.phase == ArchiveMatchTimePhase.WaitingForPlayers || time.phase == ArchiveMatchTimePhase.Countdown)
                    return MatchBaseSeconds;
                if (time.phase == ArchiveMatchTimePhase.Expired) return 0;
                return Mathf.Max(0, (float)(EffectiveMatchDeadline(time) - Clock));
            }
        }
        public bool MatchTimeExpired => State?.matchTime != null && (State.matchTime.phase == ArchiveMatchTimePhase.Expired ||
            !TeamWon && !TeamDefeated && MatchClockRunning(State.matchTime) && Clock >= EffectiveMatchDeadline(State.matchTime));
        public bool MatchTimeAllowsGameplay => Ready && State.matchTime != null && !TeamWon && !TeamDefeated &&
            !MultiplayerLobbyService.IsGameplayRecovering && MatchClockRunning(State.matchTime) && Clock < EffectiveMatchDeadline(State.matchTime);

        static bool MatchClockRunning(ArchiveMatchTimeState time) => time.phase == ArchiveMatchTimePhase.Active || time.phase == ArchiveMatchTimePhase.Escape;
        static double EffectiveMatchDeadline(ArchiveMatchTimeState time) => time.phase == ArchiveMatchTimePhase.Escape ?
            Math.Min(time.escapeDeadline, time.startedAt + MatchMaximumPlaySeconds) :
            Math.Min(time.deadline, time.startedAt + MatchBaseSeconds + 10 * MatchTopicBonusSeconds);

        void InitializeMatchTime()
        {
            if (!IsAuthority || State == null) return;
            double now = Clock;
            State.matchTime = new ArchiveMatchTimeState { createdAt = now, readinessDeadline = now + MatchReadinessGraceSeconds };
        }
        void NormalizeMatchTime()
        {
            if (State == null) return;
            // New protocol matches initialize this before their first checkpoint.
            // Replica normalization must never invent a later local deadline.
            if (State.matchTime == null) { if (IsAuthority) InitializeMatchTime(); return; }
            var time = State.matchTime;
            if (time.awardedTopics == null) time.awardedTopics = new List<int>();
            if (time.awardedActors == null) time.awardedActors = new List<int>();
        }
        bool MatchPlayersReady()
        {
            if (hunting == null || !hunting.Initialized || hunting.health == null || !hunting.health.HasAuthoritativeActorHealth) return false;
            var registered = hunting.health.ExportActorHealth();
            bool Spawned(int actor)
            {
                var player = ActorPlayer(actor);
                if (player == null || !player.gameObject.activeInHierarchy || !registered.Any(h => h.actor == actor)) return false;
                var capsule = player.GetComponent<CharacterController>();
                return capsule != null && capsule.enabled;
            }
            if (!PhotonNetwork.InRoom) return Spawned(-1);
            var players = PhotonNetwork.CurrentRoom.Players.Values.Where(p => !p.IsInactive).ToArray();
            int expected = Mathf.Clamp(State.pacingPlayers, 2, 4);
            return players.Length >= expected && players.All(p => Spawned(p.ActorNumber));
        }
        /// <summary>Authority-only state transitions. Caller commits changes through the existing session owner.</summary>
        bool TickMatchTime()
        {
            if (!Ready || !IsAuthority) return false;
            bool missing = State.matchTime == null;
            NormalizeMatchTime();
            var time = State.matchTime;
            if (time == null || TeamWon || TeamDefeated || time.phase == ArchiveMatchTimePhase.Expired) return missing;
            bool changed = missing; double now = Clock;
            if (time.phase == ArchiveMatchTimePhase.WaitingForPlayers)
            {
                // The fallback is anchored to the stored deadline, rather than the
                // next Master's update time. Loading cannot postpone the match forever.
                if (now >= time.readinessDeadline)
                {
                    time.readinessTimedOut = true; time.countdownStartedAt = time.readinessDeadline;
                    time.phase = ArchiveMatchTimePhase.Countdown; changed = true;
                }
                else if (MatchPlayersReady())
                {
                    time.countdownStartedAt = now; time.phase = ArchiveMatchTimePhase.Countdown; changed = true;
                }
            }
            if (time.phase == ArchiveMatchTimePhase.Countdown && now >= time.countdownStartedAt + MatchCountdownSeconds)
            {
                time.startedAt = time.countdownStartedAt + MatchCountdownSeconds;
                time.deadline = time.startedAt + MatchBaseSeconds;
                time.phase = ArchiveMatchTimePhase.Active; changed = true;
            }
            if (!MatchClockRunning(time)) return changed;
            // Expiration wins over completion/portal checks on the exact boundary.
            if (now >= EffectiveMatchDeadline(time)) return ExpireMatchTime() || changed;
            if (time.phase == ArchiveMatchTimePhase.Active && AllTopicsComplete)
            {
                // Discard the normal clock. The team now has exactly two minutes
                // for every surviving player to escape; bonuses cannot refill it.
                time.escapeStartedAt = now;
                time.escapeDeadline = Math.Min(now + MatchEscapeSeconds, time.startedAt + MatchMaximumPlaySeconds);
                time.phase = ArchiveMatchTimePhase.Escape; changed = true;
            }
            return changed;
        }
        /// <summary>Only the accepted final academic Submit calls this, before Commit checks the exit.</summary>
        bool AwardMatchTimeForTopic(int topic, int actor)
        {
            if (!Ready || !IsAuthority || TeamWon || TeamDefeated || topic < 0 || topic >= 10 || actor == 0) return false;
            var time = State.matchTime;
            if (time == null || time.phase != ArchiveMatchTimePhase.Active || Clock >= EffectiveMatchDeadline(time)) return false;
            var station = State.stations[topic];
            if (station.topic != topic || station.phase != 2 || station.round != ArchiveTopicCatalog.RoundCount(topic) ||
                station.lastAnswerResult != 1 || station.lastAnswerActor != actor || station.lastAnswerRound != station.round - 1 ||
                station.lastAnswerAt < time.startedAt || station.lastAnswerAt >= EffectiveMatchDeadline(time)) return false;
            if (time.awardedTopics == null) time.awardedTopics = new List<int>();
            if (time.awardedActors == null) time.awardedActors = new List<int>();
            if (time.awardedTopics.Contains(topic) || time.awardedTopics.Count >= 10) return false;
            time.awardedTopics.Add(topic); time.awardedActors.Add(actor);
            time.lastBonusAt = Clock; time.lastBonusTopic = topic; time.lastBonusActor = actor;
            time.deadline = Math.Min(time.deadline + MatchTopicBonusSeconds, time.startedAt + MatchBaseSeconds + 10 * MatchTopicBonusSeconds);
            return true;
        }
        bool ExpireMatchTime()
        {
            if (!Ready || !IsAuthority || TeamWon || TeamDefeated || State.matchTime == null) return false;
            var time = State.matchTime;
            if (!MatchClockRunning(time) || Clock < EffectiveMatchDeadline(time)) return false;
            double expiredDeadline = EffectiveMatchDeadline(time);
            time.phase = ArchiveMatchTimePhase.Expired; time.expiredAt = expiredDeadline;
            NormalizeSurvival();
            State.survival.defeated = true; State.survival.defeatedAt = Clock;
            State.survival.defeatReason = MatchTimeoutReason;
            if (PhotonNetwork.InRoom) PhotonNetwork.CurrentRoom.IsOpen = false;
            return true;
        }
    }
}
