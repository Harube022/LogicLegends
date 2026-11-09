using UnityEngine;

namespace LogicLegends.OpenArchive.Hunting
{
    public sealed partial class KhezuHuntingController
    {
        public const float PursuitEnrageSeconds = 30, PursuitCaptureSeconds = 40;
        [Header("Timed pursuit pressure")]
        [Tooltip("At 30 active chase seconds, speed grows from the ordinary catch-up ceiling to this multiple. Capture resolves at 40 seconds.")]
        [Range(1.6f,4)] public float enragedRunMultiplier = 2.8f;
        public float PursuitElapsedSeconds { get; private set; }
        public bool PursuitEnraged => (State == KhezuHuntState.Hunting || State == KhezuHuntState.NavigationBlocked) &&
            TargetPlayerId != 0 && PursuitElapsedSeconds >= PursuitEnrageSeconds && !IsTranquilized && !IsBeaconDiverted && !EntranceHeld;
        public float PursuitSecondsRemaining => Mathf.Max(0, PursuitCaptureSeconds - PursuitElapsedSeconds);
        public Color PursuitWarningTint => IsRajangCounter ? new Color(1,.52f,.16f) : new Color(1,.30f,.34f);

        void ResetTimedPursuit() => PursuitElapsedSeconds = 0;
        void RestoreTimedPursuit(float seconds) => PursuitElapsedSeconds = float.IsNaN(seconds) || float.IsInfinity(seconds) ? 0 :
            Mathf.Clamp(seconds, 0, PursuitCaptureSeconds);

        bool UpdateTimedPursuit(Target target, float seconds)
        {
            // The authority advances one timer on its existing target. No timer is
            // inferred from a client's position, and no second capture bypasses the shared slot.
            if (!IsAuthority || (State != KhezuHuntState.Hunting && State != KhezuHuntState.NavigationBlocked) ||
                target == null || target.transform == null || seconds <= 0 || !CaptureSlotAvailable()) return false;
            var session = Topics.ArchiveTopicSession.Current;
            if (IsTranquilized || EntranceHeld || IsBeaconDiverted || MultiplayerLobbyService.IsGameplayRecovering ||
                session != null && (session.TeamWon || session.TeamDefeated || session.NightEntranceActive || Topics.ArchiveMatchCinematic.Covers(session))) return false;
            bool wasEnraged = PursuitElapsedSeconds >= PursuitEnrageSeconds;
            PursuitElapsedSeconds = Mathf.Min(PursuitCaptureSeconds, PursuitElapsedSeconds + seconds);
            if (!wasEnraged && PursuitElapsedSeconds >= PursuitEnrageSeconds) SnapshotChanged?.Invoke();
            if (PursuitElapsedSeconds < PursuitCaptureSeconds) return false;
            // Deadline capture intentionally uses the existing validated encounter adapter.
            // The original monster never warps: its local cinematic already stages a visual-only rig.
            CatchTarget(target);
            return State == KhezuHuntState.Caught;
        }

        void ApplyTimedPursuitSpeed()
        {
            if (!PursuitEnraged || agent == null) return;
            float ramp = Mathf.InverseLerp(PursuitEnrageSeconds, PursuitCaptureSeconds, PursuitElapsedSeconds);
            float maximum = Mathf.Max(chaseSpeedMultiplier, enragedRunMultiplier);
            agent.speed = normalPlayerRunSpeed * Mathf.Lerp(chaseSpeedMultiplier, maximum, ramp);
            agent.acceleration = Mathf.Max(agent.acceleration, Mathf.Lerp(70, 180, ramp));
            agent.angularSpeed = Mathf.Max(agent.angularSpeed, Mathf.Lerp(360, 900, ramp));
        }
    }
}
