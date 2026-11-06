using LogicLegends.OpenArchive.Cinematics;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace LogicLegends.OpenArchive.Hunting
{
    public enum MonsterHuntingPersonalityPhase { None, Warning, Active, Recovery }

    public sealed partial class KhezuHuntingController
    {
        [Header("Readable guardian pursuit")]
        public bool huntingPersonalityEnabled = true;
        public MonsterHuntingPersonalityPhase HuntingPersonalityPhase { get; private set; }
        public int HuntingPersonalitySerial { get; private set; }
        public double HuntingPersonalityStartedAt { get; private set; }
        public double HuntingPersonalityCooldownUntil { get; private set; }
        public Vector3 HuntingPersonalityAimPoint { get; private set; }
        // The existing field authority can suspend pursuit tells while a beacon owns the route.
        public bool HuntingPersonalitySuppressed { get; set; }
        public string HuntingPersonalityHint => !PersonalityVisible ? "" : IsRajangCounter ?
            (HuntingPersonalityPhase == MonsterHuntingPersonalityPhase.Warning ? "RAJANG braces for a short rush — turn aside" :
             HuntingPersonalityPhase == MonsterHuntingPersonalityPhase.Active ? "RAJANG rush — change direction" : "RAJANG is recovering") :
            (HuntingPersonalityPhase == MonsterHuntingPersonalityPhase.Warning ? "KHEZU reads your route — change direction" :
             HuntingPersonalityPhase == MonsterHuntingPersonalityPhase.Active ? "KHEZU pressures the marked route" : "KHEZU is recovering");

        int personalityTarget, movementSampleTarget, presentedPersonalitySerial;
        Vector3 previousPersonalityTargetPosition, sampledTargetVelocity;
        double previousPersonalitySampleAt, nextPersonalityAimAt;
        bool hasPersonalityMovementSample;
        LineRenderer personalityWarning;
        Material personalityWarningMaterial;
        AudioSource personalityVoice;
        readonly Vector3[] personalityWarningPoints = new Vector3[21];
        const float PersonalityCooldownSeconds = 11;
        float PersonalityWarningSeconds => IsRajangCounter ? 1.25f : 1.4f;
        float PersonalityActiveSeconds => IsRajangCounter ? 1.4f : 1.8f;
        float PersonalityTotalSeconds => PersonalityWarningSeconds + PersonalityActiveSeconds + 1.1f;
        bool PersonalityHunting => State == KhezuHuntState.Hunting || State == KhezuHuntState.NavigationBlocked;
        bool PersonalityAllowed => Initialized && huntingPersonalityEnabled && !HuntingPersonalitySuppressed &&
            !IsTranquilized && !EntranceHeld && !CarryingRescueActor && !MultiplayerLobbyService.IsGameplayRecovering &&
            PersonalityHunting && CloseCounterPhase == MonsterCloseCounterPhase.None &&
            (Topics.ArchiveTopicSession.Current == null || (!Topics.ArchiveTopicSession.Current.NightEntranceActive &&
                !Topics.ArchiveTopicSession.Current.TeamDefeated && !Topics.ArchiveTopicSession.Current.TeamWon));
        bool PersonalityVisible => PersonalityAllowed && HuntingPersonalityPhase != MonsterHuntingPersonalityPhase.None &&
            personalityTarget == TargetPlayerId && EncounterClock >= HuntingPersonalityStartedAt &&
            EncounterClock < HuntingPersonalityStartedAt + PersonalityTotalSeconds;

        public void CancelHuntingPersonality()
        {
            HuntingPersonalityPhase = MonsterHuntingPersonalityPhase.None;
            personalityTarget = 0;
            hasPersonalityMovementSample = false;
            sampledTargetVelocity = Vector3.zero;
            // Keep the already earned cooldown when capture, sedation or a beacon interrupts a tell.
            // A resumed hunt must not immediately repeat the same warning.
            if (personalityWarning != null) personalityWarning.enabled = false;
            if (personalityVoice != null) personalityVoice.Stop();
        }

        void UpdateHuntingPersonality(Target target)
        {
            if (!IsAuthority || !PersonalityAllowed || target == null || target.transform == null ||
                !agent.enabled || !agent.isOnNavMesh || FollowingWaterTarget ||
                (waterPursuit != null && waterPursuit.ContainsTarget(target.transform, target.capsule)))
            { CancelHuntingPersonality(); return; }
            double now = EncounterClock;
            SamplePersonalityTarget(target, now);
            if (HuntingPersonalityPhase != MonsterHuntingPersonalityPhase.None)
            {
                double elapsed = now - HuntingPersonalityStartedAt;
                if (personalityTarget != TargetPlayerId || elapsed < 0 || elapsed >= PersonalityTotalSeconds)
                {
                    CancelHuntingPersonality(); nextDestination = 0; SnapshotChanged?.Invoke(); return;
                }
                var phase = elapsed < PersonalityWarningSeconds ? MonsterHuntingPersonalityPhase.Warning :
                    elapsed < PersonalityWarningSeconds + PersonalityActiveSeconds ? MonsterHuntingPersonalityPhase.Active :
                    MonsterHuntingPersonalityPhase.Recovery;
                if (!IsRajangCounter && phase == MonsterHuntingPersonalityPhase.Warning && now >= nextPersonalityAimAt)
                {
                    nextPersonalityAimAt = now + .15;
                    Vector3 prediction = target.transform.position + Vector3.ClampMagnitude(sampledTargetVelocity * .4f, 5);
                    Vector3 sampled; NavMeshPath route;
                    if (TryPersonalityPoint(target, prediction, out sampled, out route)) HuntingPersonalityAimPoint = sampled;
                }
                if (HuntingPersonalityPhase != phase)
                { HuntingPersonalityPhase = phase; nextDestination = 0; SnapshotChanged?.Invoke(); }
                return;
            }
            float gap = TargetSurfaceGap(target);
            // Start outside ordinary close pursuit, with time for both the tell and a lateral dodge.
            if (now < HuntingPersonalityCooldownUntil || !agent.hasPath || agent.pathPending ||
                agent.pathStatus != NavMeshPathStatus.PathComplete || gap < Mathf.Max(12, closePressureDistance + 4) ||
                gap > Mathf.Max(16, catchUpDistance - 3) || Mathf.Abs(TargetFeet(target) - transform.position.y) > 1 ||
                !KhezuLandApproach.ClearLine(transform.position, target.transform, TargetFeet(target), catchObstructionMask)) return;
            Vector3 point = target.transform.position;
            if (!IsRajangCounter)
            {
                // Read a small lead during the warning, then freeze it when active pressure begins.
                Vector3 prediction = Vector3.ClampMagnitude(sampledTargetVelocity * .4f, 5);
                if (prediction.sqrMagnitude < .5f) return;
                point += prediction;
                NavMeshPath route;
                Vector3 sampled;
                if (!TryPersonalityPoint(target, point, out sampled, out route)) return;
                point = sampled;
            }
            HuntingPersonalityAimPoint = point;
            HuntingPersonalitySerial++; personalityTarget = TargetPlayerId;
            HuntingPersonalityStartedAt = now;
            nextPersonalityAimAt = now + .15;
            HuntingPersonalityCooldownUntil = now + PersonalityTotalSeconds + PersonalityCooldownSeconds;
            HuntingPersonalityPhase = MonsterHuntingPersonalityPhase.Warning;
            SnapshotChanged?.Invoke();
        }

        void SamplePersonalityTarget(Target target, double now)
        {
            Vector3 position = target.transform.position;
            double dt = now - previousPersonalitySampleAt;
            if (hasPersonalityMovementSample && movementSampleTarget == TargetPlayerId && dt > 0 && dt <= .25)
            {
                Vector3 velocity = (position - previousPersonalityTargetPosition) / (float)dt; velocity.y = 0;
                // Ignore teleport/recovery samples and bound prediction to ordinary player movement.
                sampledTargetVelocity = velocity.magnitude > normalPlayerRunSpeed * 1.8f ? Vector3.zero :
                    Vector3.Lerp(sampledTargetVelocity, Vector3.ClampMagnitude(velocity, normalPlayerRunSpeed), .35f);
            }
            else sampledTargetVelocity = Vector3.zero;
            hasPersonalityMovementSample = true; movementSampleTarget = TargetPlayerId;
            previousPersonalitySampleAt = now; previousPersonalityTargetPosition = position;
        }

        float PersonalityPursuitSpeed(float ordinarySpeed)
        {
            if (!PersonalityVisible) return ordinarySpeed;
            if (HuntingPersonalityPhase == MonsterHuntingPersonalityPhase.Warning) return ordinarySpeed * .72f;
            if (HuntingPersonalityPhase == MonsterHuntingPersonalityPhase.Recovery) return ordinarySpeed * .8f;
            if (!IsRajangCounter) return ordinarySpeed;
            // The burst uses the established catch-up ceiling; the existing speed balance remains authoritative.
            return Mathf.Min(normalPlayerRunSpeed * Mathf.Max(1, chaseSpeedMultiplier), ordinarySpeed * 1.18f);
        }

        float PersonalityPursuitTurn(float ordinaryTurn) => PersonalityVisible && IsRajangCounter &&
            HuntingPersonalityPhase == MonsterHuntingPersonalityPhase.Active ? ordinaryTurn * .7f : ordinaryTurn;

        bool TryPersonalityPoint(Target target, Vector3 point, out Vector3 sampled, out NavMeshPath path)
        {
            sampled = point; path = null;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(point, out hit, 1, filter) || Mathf.Abs(hit.position.y - TargetFeet(target)) > .75f ||
                Vector3.Distance(hit.position, point) > 1 ||
                !KhezuLandApproach.ClearLine(hit.position, target.transform, TargetFeet(target), catchObstructionMask)) return false;
            var route = new NavMeshPath();
            if (!NavMesh.CalculatePath(transform.position, hit.position, filter, route) || route.status != NavMeshPathStatus.PathComplete) return false;
            sampled = hit.position; path = route; return true;
        }

        bool TryHuntingPersonalityRoute(Target target, out NavMeshPath path)
        {
            path = null;
            if (!PersonalityVisible || IsRajangCounter || HuntingPersonalityPhase != MonsterHuntingPersonalityPhase.Active ||
                Vector3.Distance(target.transform.position, HuntingPersonalityAimPoint) > 11 ||
                Vector3.Distance(transform.position, HuntingPersonalityAimPoint) < 2) return false;
            Vector3 sampled;
            return TryPersonalityPoint(target, HuntingPersonalityAimPoint, out sampled, out path);
        }

        void FillHuntingPersonalitySnapshot(KhezuSnapshot snapshot)
        {
            bool active = PersonalityVisible;
            snapshot.personalityPhase = active ? (int)HuntingPersonalityPhase : 0;
            snapshot.personalitySerial = HuntingPersonalitySerial;
            snapshot.personalityTarget = active ? personalityTarget : 0;
            snapshot.personalityStartedAt = HuntingPersonalityStartedAt;
            snapshot.personalityCooldownUntil = HuntingPersonalityCooldownUntil;
            snapshot.personalityAimPoint = HuntingPersonalityAimPoint;
        }

        void RestoreHuntingPersonalitySnapshot(KhezuSnapshot snapshot)
        {
            HuntingPersonalityPhase = (MonsterHuntingPersonalityPhase)snapshot.personalityPhase;
            HuntingPersonalitySerial = snapshot.personalitySerial; personalityTarget = snapshot.personalityTarget;
            HuntingPersonalityStartedAt = snapshot.personalityStartedAt;
            HuntingPersonalityCooldownUntil = snapshot.personalityCooldownUntil;
            HuntingPersonalityAimPoint = snapshot.personalityAimPoint;
            if (!PersonalityAllowed) CancelHuntingPersonality();
        }

        void UpdateHuntingPersonalityPresentation()
        {
            bool visible = PersonalityVisible && HuntingPersonalityPhase != MonsterHuntingPersonalityPhase.Recovery;
            if (!visible)
            { if (personalityWarning != null) personalityWarning.enabled = false; if (personalityVoice != null) personalityVoice.Stop(); return; }
            if (personalityWarning == null)
            {
                var source = Resources.Load<Material>("Multiplayer/CloseCounterWarning");
                if (source == null) return;
                personalityWarningMaterial = new Material(source);
                var go = new GameObject("Guardian_Pursuit_Tell"); go.transform.SetParent(transform, false);
                var scale = transform.lossyScale;
                go.transform.localScale = new Vector3(1 / Mathf.Max(.001f, Mathf.Abs(scale.x)),
                    1 / Mathf.Max(.001f, Mathf.Abs(scale.y)), 1 / Mathf.Max(.001f, Mathf.Abs(scale.z)));
                personalityWarning = go.AddComponent<LineRenderer>(); personalityWarning.sharedMaterial = personalityWarningMaterial;
                personalityWarning.useWorldSpace = true; personalityWarning.positionCount = personalityWarningPoints.Length;
                personalityWarning.startColor = personalityWarning.endColor = Color.white;
                personalityWarning.widthMultiplier = .35f; personalityWarning.numCornerVertices = 2;
                personalityWarning.shadowCastingMode = ShadowCastingMode.Off; personalityWarning.receiveShadows = false;
                personalityVoice = go.AddComponent<AudioSource>(); personalityVoice.playOnAwake = false; personalityVoice.spatialBlend = 1;
                personalityVoice.rolloffMode = AudioRolloffMode.Linear; personalityVoice.minDistance = 12; personalityVoice.maxDistance = 70;
                AudioVolumeSettings.Route(personalityVoice, GameAudioChannel.SoundFX);
            }
            personalityWarning.enabled = true;
            bool warning = HuntingPersonalityPhase == MonsterHuntingPersonalityPhase.Warning;
            Color color = IsRajangCounter ? new Color(1, .55f, .04f) : new Color(1, .1882353f, .1882353f);
            personalityWarningMaterial.SetColor("_BaseColor", color);
            personalityWarning.widthMultiplier = warning ? .3f + .1f * Mathf.Sin((float)(EncounterClock - HuntingPersonalityStartedAt) * 12) : .45f;
            Vector3 center = CounterCenter + Vector3.up * .22f;
            if (IsRajangCounter)
            {
                // A forward chevron communicates the rush; its reduced turn rate leaves a side dodge.
                float front = Mathf.Max(body.bounds.extents.x, body.bounds.extents.z) + 1;
                for (int i = 0; i < personalityWarningPoints.Length; i++)
                {
                    float fraction = i / (float)(personalityWarningPoints.Length - 1);
                    float side = Mathf.Lerp(-1, 1, fraction);
                    personalityWarningPoints[i] = center + transform.right * side * 6 + transform.forward * (front + (1 - Mathf.Abs(side)) * 6);
                }
            }
            else
            {
                // The destination ring shows the read, then remains frozen during active pressure.
                Vector3 point = HuntingPersonalityAimPoint + Vector3.up * .25f;
                personalityWarningPoints[0] = center;
                for (int i = 1; i < personalityWarningPoints.Length; i++)
                {
                    float angle = (i - 1) * Mathf.PI * 2 / (personalityWarningPoints.Length - 2);
                    personalityWarningPoints[i] = point + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 2;
                }
            }
            personalityWarning.SetPositions(personalityWarningPoints);
            if (presentedPersonalitySerial != HuntingPersonalitySerial)
            {
                presentedPersonalitySerial = HuntingPersonalitySerial;
                var cinematic = GetComponent<KhezuCinematicJumpscareController>();
                if (cinematic != null && cinematic.tenseGrowl != null && warning)
                    personalityVoice.PlayOneShot(cinematic.tenseGrowl, .3f);
            }
        }

        void DisposeHuntingPersonalityPresentation()
        { if (personalityWarningMaterial != null) Destroy(personalityWarningMaterial); }
    }
}
