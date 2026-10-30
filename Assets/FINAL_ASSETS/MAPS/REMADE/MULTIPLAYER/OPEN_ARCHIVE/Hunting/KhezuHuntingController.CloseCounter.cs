using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.Tests.Monsters;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace LogicLegends.OpenArchive.Hunting
{
    public enum MonsterCloseCounterPhase { None, Warning, Strike, Recovery }

    public sealed partial class KhezuHuntingController
    {
        [Header("Dodgeable close-range counter")]
        public bool closeCounterEnabled = true;
        [Min(150)] public float closeTurnSpeed = 300;
        [Min(.5f)] public float circleSecondsBeforeCounter = 1.8f;
        [Min(30)] public float circleDegreesBeforeCounter = 80;
        [Min(5)] public float minimumCircleDegreesPerSecond = 25;
        [Range(.5f, 1.5f)] public float counterWarningSeconds = .9f;
        [Range(.15f, .6f)] public float counterStrikeSeconds = .35f;
        [Range(.5f, 1.5f)] public float counterRecoverySeconds = .8f;
        [Min(2)] public float counterCooldownSeconds = 3.5f;
        [Tooltip("Additional world-space reach beyond the existing body surface. Regular captures keep their current range.")]
        [Range(.5f, 5)] public float counterSurfaceReach = 3;
        [Range(90, 180)] public float rajangCounterArc = 160;
        [Range(45, 120)] public float khezuCounterArc = 90;
        [Range(0, 5)] public float khezuCounterLunge = 3;
        public MonsterCloseCounterPhase CloseCounterPhase { get; private set; }
        public int CloseCounterSerial { get; private set; }
        public double CloseCounterStartedAt { get; private set; }
        public double CloseCounterCooldownUntil { get; private set; }
        public Vector3 CloseCounterAim { get; private set; }
        int counterTarget, orbitTarget;
        float orbitSeconds, orbitDegrees, counterLungeMoved;
        Vector3 previousOrbitPlayer;
        double previousOrbitAt;
        bool hasOrbitSample;
        LineRenderer counterWarning;
        Material counterWarningMaterial;
        AudioSource counterVoice;
        int presentedCounterSerial;
        DevelopmentMonsterSelection counterSelection;
        readonly Vector3[] warningPoints = new Vector3[35];
        bool IsRajangCounter
        {
            get
            {
                if(rajangCounterProfile)return true;
                if (counterSelection == null) counterSelection = FindFirstObjectByType<DevelopmentMonsterSelection>();
                return counterSelection != null && counterSelection.ActiveTest != null &&
                    counterSelection.ActiveTest.gameObject == gameObject && counterSelection.Selected == DevelopmentMonster.Rajang;
            }
        }
        float CounterTotalSeconds => counterWarningSeconds + counterStrikeSeconds + counterRecoverySeconds;
        Vector3 CounterCenter => new Vector3(body.bounds.center.x, transform.position.y, body.bounds.center.z);

        void ResetOrbitTracking()
        {
            orbitSeconds = orbitDegrees = 0; hasOrbitSample = false; orbitTarget = 0;
        }
        void ResetCloseCounter(bool clearCooldown)
        {
            CloseCounterPhase = MonsterCloseCounterPhase.None; counterTarget = 0; counterLungeMoved = 0;
            ResetOrbitTracking();
            if (clearCooldown) CloseCounterCooldownUntil = 0;
        }
        bool UpdateCloseCounter(Target target)
        {
            if (CloseCounterPhase != MonsterCloseCounterPhase.None)
            {
                if (counterTarget != TargetPlayerId || !closeCounterEnabled)
                { FinishCloseCounter(); return true; }
                HoldCounterNavigation();
                double elapsed = EncounterClock - CloseCounterStartedAt;
                // An authority handover must never apply a hit from an already elapsed strike window.
                if (elapsed >= CounterTotalSeconds) { FinishCloseCounter(); return true; }
                if (elapsed >= counterWarningSeconds + counterStrikeSeconds)
                { SetCounterPhase(MonsterCloseCounterPhase.Recovery); Status = "Counter missed — recovering"; return true; }
                if (elapsed < counterWarningSeconds)
                {
                    Vector3 toward = target.transform.position - transform.position; toward.y = 0;
                    if (toward.sqrMagnitude > .01f)
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toward), closeTurnSpeed * Time.deltaTime);
                    CloseCounterAim = transform.forward;
                    Status = "Counter warning — move away or dodge to the side";
                    return true;
                }
                // The aim freezes at the end of the warning; the attack cannot home onto a dodge.
                SetCounterPhase(MonsterCloseCounterPhase.Strike);
                if (!IsRajangCounter && agent.enabled && agent.isOnNavMesh && counterLungeMoved < khezuCounterLunge)
                {
                    float step = Mathf.Min(khezuCounterLunge - counterLungeMoved, khezuCounterLunge / counterStrikeSeconds * Time.deltaTime);
                    NavMeshHit edge;
                    Vector3 front = body.ClosestPoint(body.bounds.center + CloseCounterAim * (body.bounds.size.magnitude + step));
                    RaycastHit wall;
                    if (!agent.Raycast(transform.position + CloseCounterAim * step, out edge) &&
                        !Physics.CheckSphere(front, .2f, catchObstructionMask, QueryTriggerInteraction.Ignore) &&
                        !Physics.SphereCast(front, .2f, CloseCounterAim, out wall, step, catchObstructionMask, QueryTriggerInteraction.Ignore))
                    {
                        agent.Move(CloseCounterAim * step);
                        counterLungeMoved += step;
                    }
                }
                if (CanCounterHit(target)) CatchTarget(target);
                return true;
            }
            if (!closeCounterEnabled || !agent.enabled || !agent.isOnNavMesh || EncounterClock < CloseCounterCooldownUntil ||
                TargetSurfaceGap(target) > closePressureDistance || !CounterSight(target) ||
                (waterPursuit != null && waterPursuit.ContainsTarget(target.transform, target.capsule)))
            { ResetOrbitTracking(); return false; }
            double now = EncounterClock;
            if (!hasOrbitSample || orbitTarget != TargetPlayerId)
            {
                hasOrbitSample = true; orbitTarget = TargetPlayerId;
                previousOrbitPlayer = target.transform.position; previousOrbitAt = now;
                return false;
            }
            float dt = (float)(now - previousOrbitAt);
            // Compare two player positions around the CURRENT center. The monster's
            // own movement/turning cannot manufacture a circling trigger.
            Vector3 from = previousOrbitPlayer - CounterCenter, to = target.transform.position - CounterCenter;
            from.y = to.y = 0;
            float degrees = from.sqrMagnitude > .1f && to.sqrMagnitude > .1f ? Mathf.Abs(Vector3.SignedAngle(from, to, Vector3.up)) : 0;
            previousOrbitPlayer = target.transform.position; previousOrbitAt = now;
            if (dt <= 0 || dt > .25f) { orbitSeconds = orbitDegrees = 0; return false; }
            if (degrees / dt >= minimumCircleDegreesPerSecond)
            { orbitSeconds += dt; orbitDegrees += degrees; }
            else { orbitSeconds = Mathf.Max(0, orbitSeconds - dt * 2); orbitDegrees = Mathf.Max(0, orbitDegrees - dt * 90); }
            if (orbitSeconds < circleSecondsBeforeCounter || orbitDegrees < circleDegreesBeforeCounter) return false;
            BeginCloseCounter(target);
            return true;
        }
        void BeginCloseCounter(Target target)
        {
            HoldCounterNavigation();
            CloseCounterSerial++; counterTarget = TargetPlayerId; counterLungeMoved = 0;
            CloseCounterStartedAt = EncounterClock; CloseCounterAim = transform.forward;
            CloseCounterCooldownUntil = CloseCounterStartedAt + CounterTotalSeconds + counterCooldownSeconds;
            ResetOrbitTracking(); CloseCounterPhase = MonsterCloseCounterPhase.Warning;
            // Both original Attack clips place contact near one third of the motion.
            animator.CrossFadeInFixedTime("Base Layer.Attack", .1f, 0, 0);
            animator.speed = ClipSeconds("Attack") / CounterTotalSeconds;
            Status = "Counter warning — move away or dodge to the side";
            SnapshotChanged?.Invoke();
        }
        void HoldCounterNavigation()
        {
            if (!agent.enabled && IsAuthority)
            {
                // AuthorityChanged stops navigation. Resume a retained counter at
                // its existing pose only; never warp onto a nearby path or replay its warning.
                var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
                NavMeshHit current;
                if (NavMesh.SamplePosition(transform.position, out current, .35f, filter) &&
                    Vector3.Distance(current.position, transform.position) < .35f) agent.enabled = true;
            }
            if (!agent.enabled || !agent.isOnNavMesh) return;
            agent.isStopped = true; agent.ResetPath(); agent.updateRotation = false;
        }
        void SetCounterPhase(MonsterCloseCounterPhase phase)
        {
            if (CloseCounterPhase == phase) return;
            CloseCounterPhase = phase; SnapshotChanged?.Invoke();
        }
        void FinishCloseCounter()
        {
            ResetCloseCounter(false); animator.speed = 1;
            nextDestination = nextCatch = 0; Blend("Idle");
            Status = "Hunting " + TargetName; SnapshotChanged?.Invoke();
        }
        bool CounterSight(Target target)
        {
            if (TargetFeet(target) > body.bounds.max.y || target.transform.position.y < transform.position.y - 3) return false;
            Vector3 center = target.capsule == null ? target.transform.position + Vector3.up : target.transform.TransformPoint(target.capsule.center);
            foreach (var bone in upperReachBones)
            {
                if (bone == null || bone.position.y < body.bounds.min.y + body.bounds.size.y * .25f) continue;
                if (Physics.CheckSphere(bone.position, .08f, catchObstructionMask, QueryTriggerInteraction.Ignore)) continue;
                if (ClearUpperReach(bone.position, center, target.transform)) return true;
            }
            return false;
        }
        bool CanCounterHit(Target target)
        {
            if (!IsAuthority || !agent.enabled || !agent.isOnNavMesh || CloseCounterPhase != MonsterCloseCounterPhase.Strike ||
                EncounterClock < CloseCounterStartedAt + counterWarningSeconds ||
                EncounterClock >= CloseCounterStartedAt + counterWarningSeconds + counterStrikeSeconds ||
                TargetSurfaceGap(target) > counterSurfaceReach || !CounterSight(target)) return false;
            Vector3 toward = target.transform.position - CounterCenter; toward.y = 0;
            return toward.sqrMagnitude < .01f || Vector3.Angle(CloseCounterAim, toward) <= (IsRajangCounter ? rajangCounterArc : khezuCounterArc) * .5f;
        }
        void CatchTarget(Target target)
        {
            if(Topics.ArchiveTopicSession.Current!=null&&Topics.ArchiveTopicSession.Current.NightEntranceActive)return;
            if(!CaptureSlotAvailable()||ReservedByAnotherMonster(TargetPlayerId))return;
            if(health!=null&&(!health.IsActorAlive(TargetPlayerId)||health.IsActorProtected(TargetPlayerId)))return;
            ResetCloseCounter(false);
            CapturePosition = target.transform.position; CaptureAnchorValid = true;
            StopNavigation(); Blend("Idle"); animator.speed = 1; CancelBarrierSequence();
            State = KhezuHuntState.Caught; CaughtPlayerId = TargetPlayerId; CaptureSerial=NextSharedCaptureSerial();
            CaptureStartedAt = EncounterClock; EncounterPhase = 0; Status = "Caught " + TargetName;
            var encounter = GetComponent<KhezuCaptureEncounterController>();
            if (encounter != null) encounter.SynchronizeBarrierMovement();
            PlayerCaught?.Invoke(CaughtPlayerId); SnapshotChanged?.Invoke();
        }
        void FillCloseCounterSnapshot(KhezuSnapshot snapshot)
        {
            snapshot.counterPhase = (int)(IsTranquilized?MonsterCloseCounterPhase.None:CloseCounterPhase); snapshot.counterSerial = CloseCounterSerial;
            snapshot.counterTarget = IsTranquilized?0:counterTarget; snapshot.counterStartedAt = CloseCounterStartedAt;
            snapshot.counterCooldownUntil = CloseCounterCooldownUntil; snapshot.counterAim = CloseCounterAim;
            snapshot.counterLungeMoved = counterLungeMoved;
            if (IsTranquilized || CloseCounterPhase == MonsterCloseCounterPhase.None) return;
            snapshot.animation = "Attack";
            snapshot.normalizedTime = Mathf.Clamp01((float)(EncounterClock - CloseCounterStartedAt) / CounterTotalSeconds);
            snapshot.animationSpeed = ClipSeconds("Attack") / CounterTotalSeconds;
        }
        void RestoreCloseCounterSnapshot(KhezuSnapshot snapshot)
        {
            // An in-flight pursuit packet must not restore a counter over checkpoint-owned sleep.
            if(IsTranquilized){ResetCloseCounter(true);return;}
            CloseCounterPhase = (MonsterCloseCounterPhase)snapshot.counterPhase; CloseCounterSerial = snapshot.counterSerial;
            counterTarget = snapshot.counterTarget; CloseCounterStartedAt = snapshot.counterStartedAt;
            CloseCounterCooldownUntil = snapshot.counterCooldownUntil; CloseCounterAim = snapshot.counterAim;
            counterLungeMoved = snapshot.counterLungeMoved; ResetOrbitTracking();
        }

        void UpdateCloseCounterPresentation()
        {
            bool visible = Initialized && !IsTranquilized && !MultiplayerLobbyService.IsGameplayRecovering &&
                (State == KhezuHuntState.Hunting || State == KhezuHuntState.NavigationBlocked) &&
                (CloseCounterPhase == MonsterCloseCounterPhase.Warning || CloseCounterPhase == MonsterCloseCounterPhase.Strike) &&
                EncounterClock < CloseCounterStartedAt + counterWarningSeconds + counterStrikeSeconds;
            if (!visible) { if (counterWarning != null) counterWarning.enabled = false; if (counterVoice != null) counterVoice.Stop(); return; }
            if (counterWarning == null)
            {
                var source = Resources.Load<Material>("Multiplayer/CloseCounterWarning");
                if (source == null) return;
                counterWarningMaterial = new Material(source);
                var go = new GameObject("Close_Counter_Warning"); go.transform.SetParent(transform, false);
                var scale = transform.lossyScale;
                go.transform.localScale = new Vector3(1 / Mathf.Max(.001f, Mathf.Abs(scale.x)), 1 / Mathf.Max(.001f, Mathf.Abs(scale.y)), 1 / Mathf.Max(.001f, Mathf.Abs(scale.z)));
                counterWarning = go.AddComponent<LineRenderer>(); counterWarning.sharedMaterial = counterWarningMaterial;
                counterWarning.useWorldSpace = true; counterWarning.positionCount = warningPoints.Length;
                counterWarning.startColor = counterWarning.endColor = Color.white;
                counterWarning.widthMultiplier = .28f; counterWarning.numCornerVertices = 2;
                counterWarning.shadowCastingMode = ShadowCastingMode.Off; counterWarning.receiveShadows = false;
                counterVoice = go.AddComponent<AudioSource>(); counterVoice.playOnAwake = false; counterVoice.spatialBlend = 1;
                counterVoice.rolloffMode = AudioRolloffMode.Linear; counterVoice.minDistance = 10; counterVoice.maxDistance = 65;
                AudioVolumeSettings.Route(counterVoice, GameAudioChannel.SoundFX);
            }
            counterWarning.enabled = true;
            Color color = IsRajangCounter ? new Color(1, .34f, .025f, 1) : new Color(1, .08f, .16f, 1);
            float pulse = CloseCounterPhase == MonsterCloseCounterPhase.Strike ? 1.7f : 1 + .35f * Mathf.Sin((float)(EncounterClock - CloseCounterStartedAt) * 22);
            counterWarningMaterial.SetColor("_BaseColor", color);
            counterWarning.widthMultiplier = .28f * pulse;
            Vector3 center = CounterCenter + Vector3.up * .18f;
            Vector3 aim = CloseCounterPhase == MonsterCloseCounterPhase.Warning ? transform.forward : CloseCounterAim;
            float arc = IsRajangCounter ? rajangCounterArc : khezuCounterArc;
            warningPoints[0] = center;
            float radius = Mathf.Max(body.bounds.extents.x, body.bounds.extents.z) + counterSurfaceReach +
                (IsRajangCounter ? 0 : Mathf.Max(0, khezuCounterLunge - counterLungeMoved));
            for (int i = 0; i <= 32; i++)
                warningPoints[i + 1] = center + Quaternion.AngleAxis(Mathf.Lerp(-arc * .5f, arc * .5f, i / 32f), Vector3.up) * aim * radius;
            warningPoints[34] = center; counterWarning.SetPositions(warningPoints);
            if (presentedCounterSerial != CloseCounterSerial)
            {
                presentedCounterSerial = CloseCounterSerial;
                var cinematic = GetComponent<KhezuCinematicJumpscareController>();
                if (cinematic != null && cinematic.tenseGrowl != null && EncounterClock - CloseCounterStartedAt < counterWarningSeconds)
                    counterVoice.PlayOneShot(cinematic.tenseGrowl, .35f);
            }
        }
        void DisposeCloseCounterPresentation()
        {
            if (counterWarningMaterial != null) Destroy(counterWarningMaterial);
        }
    }
}
