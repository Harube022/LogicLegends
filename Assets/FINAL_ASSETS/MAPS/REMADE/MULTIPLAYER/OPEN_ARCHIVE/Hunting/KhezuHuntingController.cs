using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

namespace LogicLegends.OpenArchive.Hunting
{
    public enum KhezuHuntState { Sleeping, Waking, Ready, Hunting, Caught, WaitingForPlayers, NavigationBlocked, ReturningToNest, NestSleeping, AttackRecovery, CarryingToNest }
    public enum KhezuCommand { AddMistake, ForceFourMistakes, StartHunting, ResetHunt, ResetRotation, Sleep }

    [Serializable]
    public sealed class KhezuSnapshot
    {
        public int state, mistakes, stage, target, caught, huntSerial, captureSerial;
        public int[] hunted = new int[0], excluded = new int[0];
        public Vector3 position;
        public Quaternion rotation;
        public string animation;
        public float normalizedTime, animationSpeed;
        public bool pending, awake;
        public double captureStartedAt;
        public int encounterPhase;
        public Vector3 capturePosition;
        public bool captureAnchorValid;
        public int resolvedSerial,resolvedActor,resolvedOutcome,avoidNextTarget;
        public double resolvedAt,releaseAt,sleepEndsAt,nestArrivedAt,attackEndsAt;
        public int barrierTimerSerial;
        public double barrierStartedAt,barrierDeadline,attackHitAt,barrierDissolveAt,stunEndsAt;
        public bool barrierTimedOut,failureHitApplied;
        public Vector3[] knockbackPath=new Vector3[0];
        public HealthManager.ActorHealth[] healthStates=new HealthManager.ActorHealth[0];
        public int counterPhase, counterSerial, counterTarget;
        public double counterStartedAt, counterCooldownUntil;
        public Vector3 counterAim;
        public float counterLungeMoved;
        public int rescueActor;
        public bool rescueAtNest;
        public bool rescuePickedUp;
    }

    [DefaultExecutionOrder(50), DisallowMultipleComponent]
    [RequireComponent(typeof(Animator), typeof(NavMeshAgent), typeof(BoxCollider))]
    public sealed partial class KhezuHuntingController : MonoBehaviour
    {
        [SerializeField] MonoBehaviour awakeningDriver;
        [Header("World-space movement tuning")]
        [Min(1)] public float normalPlayerRunSpeed = 19.5f;
        [Tooltip("Maximum catch-up speed when the runner has opened a large gap.")]
        [Range(1.01f, 1.6f)] public float chaseSpeedMultiplier = 1.3685f;
        [Header("Close pursuit pressure")]
        [Tooltip("A small advantage over running speed keeps pressure without an immediate capture.")]
        [Range(1, 1.15f)] public float closeChaseSpeedMultiplier = 1.04f;
        [Min(0)] public float closePressureDistance = 8;
        [Min(1)] public float catchUpDistance = 35;
        [Range(.05f, .25f)] public float closeDestinationInterval = .1f;
        [Min(10)] public float turnSpeed = 150;
        [Min(1)] public float acceleration = 16;
        [Min(.1f)] public float destinationInterval = .25f;
        [Min(.2f)] public float rosterInterval = .5f;
        [Min(.1f)] public float catchInterval = .1f;
        [Min(0)] public float catchSurfaceDistance = .65f;
        [Min(1)] public float chaseAnimationReferenceSpeed = 18;
        [Min(.1f)] public float postAwakeningIdleSeconds = .3f;
        [Header("Shoreline pursuit")]
        [Tooltip("Distance to pace along reachable dry shoreline while the selected player is in water.")]
        [Min(6)] public float shorelinePaceDistance = 18;
        [Tooltip("Choose the next bank destination before reaching the previous one, avoiding an arrival stop.")]
        [Min(2)] public float shorelineTurnDistance = 6;
        public bool FollowingWaterTarget { get { return waterPursuit != null && waterPursuit.Active; } }
        public LayerMask catchObstructionMask = ~(1 << 7);
        public event Action<int> PlayerCaught;
        public event Action<int> HuntStarted;
        public event Action SnapshotChanged;
        public event Action<KhezuCommand> NetworkCommandRequested;
        public KhezuHuntState State { get; private set; }
        public int TargetPlayerId { get; private set; }
        public int CaughtPlayerId { get; private set; }
        public int HuntSerial { get; private set; }
        public int CaptureSerial { get; private set; }
        public double CaptureStartedAt { get; private set; }
        public int EncounterPhase { get; private set; }
        // Optional encounter adapter owns Caught until a host reset/result releases it.
        public bool CaptureSequenceLocked { get; set; }
        public static double EncounterClock { get { return PhotonNetwork.InRoom ? PhotonNetwork.Time : Time.unscaledTimeAsDouble; } }
        public string Status { get; private set; } = "Sleeping in nest";
        public IReadOnlyCollection<int> HuntedPlayerIds { get { return rotation.HuntedPlayerIds; } }
        public bool IsAuthority { get { return !MultiplayerLobbyService.IsGameplayRecovering && (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient); } }
        public bool Initialized { get; private set; }
        public NavMeshAgent Agent { get { return agent != null ? agent : GetComponent<NavMeshAgent>(); } }
        public int Mistakes { get { return driver == null ? 0 : driver.Mistakes; } }
        public string TargetName { get { Target t; return targets.TryGetValue(TargetPlayerId, out t) ? t.name : TargetPlayerId == 0 ? "None" : "Actor " + TargetPlayerId; } }
        public int EligiblePlayerCount { get { return targets.Count; } }
        readonly KhezuHuntRotation rotation = new KhezuHuntRotation();
        readonly Dictionary<int, Target> targets = new Dictionary<int, Target>();
        readonly HashSet<int> excluded = new HashSet<int>();
        readonly Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
        IKhezuAwakeningDriver driver;
        Animator animator;
        NavMeshAgent agent;
        BoxCollider body;
        float nextRoster, nextDestination, nextCatch, idleReadyAt = -1;
        bool pending, fullyAwake, previousAuthority;
        KhezuSnapshot replica;
        KhezuWaterPursuit waterPursuit;
        readonly KhezuLandApproach landApproach = new KhezuLandApproach();
        static KhezuHuntingController active;
        sealed class Target { public Transform transform; public CharacterController capsule; public string name; }

        public void Configure(MonoBehaviour source) { awakeningDriver = source; }
        public void RefreshAnimationRig()
        {
            if (animator == null) animator = GetComponent<Animator>();
            clips.Clear();
            foreach (var clip in animator.runtimeAnimatorController.animationClips) clips[clip.name] = clip;
            CacheUpperBodyReach();
        }
        void Awake()
        {
            if (!RegisterMonster())
            { Debug.LogError("Duplicate monster encounter identity disabled.",this);gameObject.SetActive(false);return; }
            if(string.IsNullOrEmpty(encounterId))active=this;
            animator = GetComponent<Animator>(); agent = GetComponent<NavMeshAgent>(); body = GetComponent<BoxCollider>();
            CacheUpperBodyReach();
            agent.autoBraking = false;
            agent.enabled = false;
            driver = awakeningDriver as IKhezuAwakeningDriver;
            RefreshAnimationRig();
            var water = FindFirstObjectByType<LogicLegends.OpenArchive.Water.OpenArchiveWater>();
            if (water != null) waterPursuit = new KhezuWaterPursuit(water,sceneMonsters.FirstOrDefault(m=>m!=this&&m.waterPursuit!=null)?.waterPursuit);
        }
        void Start()
        {
            if (driver == null) { Debug.LogError("Assign the existing awakening driver.", this); enabled = false; return; }
            driver.MistakeRequested += RequestMistake;
            driver.SleepRequested += RequestSleep;
            driver.SetHuntingControl(true, !IsAuthority);
            previousAuthority = IsAuthority;
            Initialized = true;
            RefreshPlayers();
        }
        void OnDestroy()
        {
            DisposeCloseCounterPresentation();
            if (driver != null) { driver.MistakeRequested -= RequestMistake; driver.SleepRequested -= RequestSleep; }
            if (active == this) active = null;
            sceneMonsters.Remove(this);
        }
        void RequestMistake() { Request(KhezuCommand.AddMistake); }
        void RequestSleep() { Request(KhezuCommand.Sleep); }
        public void Request(KhezuCommand command)
        {
            if (!Application.isPlaying || !Initialized) return;
            if (IsAuthority) ApplyCommand(command); else NetworkCommandRequested?.Invoke(command);
        }
        public void ApplyCommand(KhezuCommand command)
        {
            if (!IsAuthority || !Initialized) return;
            if (IsTranquilized || CarryingRescueActor) return;
            if(EntranceHeld&&command!=KhezuCommand.Sleep&&command!=KhezuCommand.ResetHunt)return;
            if ((State==KhezuHuntState.ReturningToNest || State==KhezuHuntState.NestSleeping || State==KhezuHuntState.AttackRecovery) &&
                command!=KhezuCommand.ResetHunt && command!=KhezuCommand.ResetRotation && command!=KhezuCommand.Sleep)return;
            switch (command)
            {
                case KhezuCommand.AddMistake:
                case KhezuCommand.ForceFourMistakes:
                    int count = command == KhezuCommand.AddMistake ? Mathf.Min(4, driver.Mistakes + 1) : 4;
                    driver.ApplySharedMistakes(count);
                    if (!fullyAwake && count > 0) { State = KhezuHuntState.Waking; pending = count == 4; }
                    break;
                case KhezuCommand.StartHunting:
                    if (CaptureSequenceLocked && State == KhezuHuntState.Caught) return;
                    if (State == KhezuHuntState.Hunting || State == KhezuHuntState.NavigationBlocked) return;
                    if (!fullyAwake) { driver.ApplySharedMistakes(4); State = KhezuHuntState.Waking; pending = true; }
                    else BeginHunt();
                    break;
                case KhezuCommand.ResetHunt:
                    ResetCloseCounter(true);
                    CancelBarrierSequence();
                    StopNavigation(); animator.speed = 1; TargetPlayerId = CaughtPlayerId = 0; pending = false;
                    State = fullyAwake ? KhezuHuntState.Ready : driver.Mistakes > 0 ? KhezuHuntState.Waking : KhezuHuntState.Sleeping;
                    if (fullyAwake) Blend("Idle");
                    break;
                case KhezuCommand.ResetRotation:
                    ResetCloseCounter(true);
                    CancelBarrierSequence();
                    StopNavigation(); animator.speed = 1; TargetPlayerId = CaughtPlayerId = 0; pending = false;
                    rotation.HuntedPlayerIds.Clear();
                    State = fullyAwake ? KhezuHuntState.Ready : driver.Mistakes > 0 ? KhezuHuntState.Waking : KhezuHuntState.Sleeping;
                    if (fullyAwake) Blend("Idle");
                    break;
                case KhezuCommand.Sleep:
                    EntranceHeld=false;
                    ResetCloseCounter(true);
                    CancelBarrierSequence();
                    StopNavigation(); TargetPlayerId = CaughtPlayerId = 0; pending = fullyAwake = false;
                    State = KhezuHuntState.Sleeping; animator.speed = 1; driver.ApplySharedMistakes(0);
                    idleReadyAt = -1;
                    break;
            }
            Status = State.ToString();
            SnapshotChanged?.Invoke();
        }
        public void SetPlayerEligibility(int actorId, bool eligible)
        {
            if (!IsAuthority) return;
            if (eligible) excluded.Remove(actorId); else excluded.Add(actorId);
            nextRoster = 0; SnapshotChanged?.Invoke();
        }
        void Update()
        {
            if (!Initialized) return;
            if(Topics.ArchiveTopicSession.Current!=null&&Topics.ArchiveTopicSession.Current.TeamDefeated){StopNavigation();return;}
            if(EntranceHeld){StopNavigation();return;}
            if(Topics.ArchiveTopicSession.Current!=null&&Topics.ArchiveTopicSession.Current.NightEntranceActive){StopNavigation();return;}
            if (MultiplayerLobbyService.IsGameplayRecovering) { StopNavigation(); return; }
            if (previousAuthority != IsAuthority) AuthorityChanged();
            if (Time.time >= nextRoster) { nextRoster = Time.time + rosterInterval; RefreshPlayers(); }
            if (IsTranquilized) { MaintainTranquilizedSleep(); return; }
            if (!IsAuthority) return;
            if (UpdateRescueCarry()) return;
            if (UpdateBarrierOutcome()) return;
            if (pending && driver.Mistakes == 4 && driver.PlayingStage == 4 && driver.CurrentState == "Idle" && !animator.IsInTransition(0))
            {
                if (idleReadyAt < 0) idleReadyAt = Time.time + postAwakeningIdleSeconds;
                if (Time.time >= idleReadyAt) { idleReadyAt = -1; pending = false; fullyAwake = true; State = KhezuHuntState.Ready; BeginHunt(); }
            }
            else idleReadyAt = -1;
            if (State == KhezuHuntState.WaitingForPlayers && targets.Count > 0) BeginHunt();
            if (State != KhezuHuntState.Hunting && State != KhezuHuntState.NavigationBlocked) return;
            Target target;
            if (!targets.TryGetValue(TargetPlayerId, out target) || target.transform == null || !target.transform.gameObject.activeInHierarchy || EscapedArchivePlayer(TargetPlayerId))
            { ResetCloseCounter(false); StopNavigation(); TargetPlayerId = 0; BeginHunt(); return; }
            if (UpdateCloseCounter(target)) return;
            float gap = TargetSurfaceGap(target);
            float catchUp = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(closePressureDistance, Mathf.Max(closePressureDistance + 1, catchUpDistance), gap));
            float closeMultiplier = Mathf.Clamp(closeChaseSpeedMultiplier, 1, Mathf.Max(1, chaseSpeedMultiplier));
            agent.speed = normalPlayerRunSpeed * Mathf.Lerp(closeMultiplier, chaseSpeedMultiplier, catchUp);
            float pursuitTurn = Mathf.Lerp(Mathf.Max(turnSpeed, closeTurnSpeed), turnSpeed, catchUp);
            agent.angularSpeed = pursuitTurn; agent.acceleration = acceleration;
            // A live pursuit must not brake to a stop at the target's last sampled position.
            agent.autoBraking = false;
            if (Time.time >= nextDestination)
            {
                nextDestination = Time.time + Mathf.Lerp(Mathf.Min(closeDestinationInterval, destinationInterval), destinationInterval, catchUp);
                UpdateDestination(target);
            }
            // A projected destination can be beside the player rather than directly on them.
            // Finish turning toward the actual target so the existing front reach can connect.
            if (agent.enabled && agent.isOnNavMesh)
            {
                bool arrived = agent.isStopped || (!agent.pathPending && (!agent.hasPath || agent.remainingDistance < .85f));
                bool close = gap <= closePressureDistance;
                agent.updateRotation = !arrived && !close;
                Vector3 toward = target.transform.position - transform.position; toward.y = 0;
                if ((arrived || close) && toward.sqrMagnitude > .1f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toward), pursuitTurn * Time.deltaTime);
            }
            bool moving = agent.enabled && agent.isOnNavMesh && !agent.isStopped && agent.velocity.sqrMagnitude > .09f;
            Blend(moving ? "Chase" : "Idle");
            animator.speed = moving ? Mathf.Clamp(agent.velocity.magnitude / chaseAnimationReferenceSpeed, .65f, 1.6f) : 1;
            if (Time.time >= nextCatch)
            {
                nextCatch = Time.time + catchInterval;
                if (CanCatch(target))
                {
                    CatchTarget(target);
                }
            }
        }
        void LateUpdate()
        {
            UpdateCloseCounterPresentation();
            if (IsTranquilized) { HoldTranquilizedRoot(); return; }
            if (!Initialized || IsAuthority || replica == null || MultiplayerLobbyService.IsGameplayRecovering) return;
            float alpha = 1 - Mathf.Exp(-18 * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, replica.position, alpha);
            transform.rotation = Quaternion.Slerp(transform.rotation, replica.rotation, alpha);
        }
        public void AuthorityChanged()
        {
            previousAuthority = IsAuthority;
            driver.SetHuntingControl(true, !IsAuthority || IsTranquilized);
            StopNavigation();
            nextRoster = nextDestination = nextCatch = 0;
            ResetOrbitTracking();
        }
        void RefreshPlayers()
        {
            targets.Clear();
            var connected = PhotonNetwork.InRoom ? PhotonNetwork.PlayerList.Where(p => !p.IsInactive).Select(p => p.ActorNumber).ToArray() : Topics.ArchiveTopicSession.Current!=null?Topics.ArchiveTopicSession.Current.DebugPracticeActors:new[] { -1 };
            var reserved = PhotonNetwork.InRoom ? PhotonNetwork.PlayerList.Select(p => p.ActorNumber).ToArray() : connected;
            if (IsAuthority) rotation.RemoveDisconnected(reserved);
            if(IsAuthority && health!=null)health.RegisterActors(reserved);
            foreach (var p in FindObjectsByType<global::Player>(FindObjectsSortMode.None))
            {
                var view = p.GetComponent<PhotonView>();
                int id = -1; string name = "Local explorer";
                if (PhotonNetwork.InRoom)
                {
                    if (view == null || view.ViewID == 0 || view.Owner == null || view.Owner.IsInactive || !connected.Contains(view.OwnerActorNr)) continue;
                    var props = view.Owner.CustomProperties;
                    if ((props.TryGetValue("ll.eliminated", out var dead) && dead is bool && (bool)dead) ||
                        (props.TryGetValue("ll.khezu.available", out var available) && available is bool && !(bool)available)) continue;
                    id = view.OwnerActorNr; name = LobbyRules.DisplayName(view.Owner);
                }
                else if(p.GetComponent<Topics.ArchivePracticeAvatar>()!=null){id=-2;name="TEST TEAMMATE";}
                else if (p != global::Player.LocalInstance) continue;
                var eligibility = p.GetComponent<KhezuHuntEligibility>();
                if (p.IsMovementLocked||ReservedByAnotherMonster(id)||excluded.Contains(id) || EscapedArchivePlayer(id) || (health!=null && (!health.IsActorAlive(id)||health.IsActorProtected(id))) || (eligibility != null && !eligibility.IsEligible)) continue;
                targets[id] = new Target { transform = p.transform, capsule = p.GetComponent<CharacterController>(), name = name };
            }
        }
        static bool EscapedArchivePlayer(int actor) => Topics.ArchiveTopicSession.Current != null &&
            (Topics.ArchiveTopicSession.Current.TeamWon || Topics.ArchiveTopicSession.Current.HasEscapedActor(actor));
        void BeginHunt()
        {
            if (!IsAuthority || !fullyAwake) return;
            ResetCloseCounter(false);
            RefreshPlayers();
            TargetPlayerId = rotation.Select(targets.Keys, n => UnityEngine.Random.Range(0, n),avoidNextTarget);
            if (TargetPlayerId == 0) { State = KhezuHuntState.WaitingForPlayers; Status = "Waiting for an eligible player"; Blend("Idle"); SnapshotChanged?.Invoke(); return; }
            avoidNextTarget=0;
            CaughtPlayerId = 0; State = KhezuHuntState.Hunting; HuntSerial++;
            nextDestination = nextCatch = 0; Status = "Hunting " + TargetName;
            HuntStarted?.Invoke(TargetPlayerId); SnapshotChanged?.Invoke();
        }
        void UpdateDestination(Target target)
        {
            if (!agent.enabled) agent.enabled = true;
            if (!agent.isOnNavMesh) { State = KhezuHuntState.NavigationBlocked; Status = "Monster is outside its baked NavMesh; movement stopped"; return; }
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            NavMeshHit hit;
            var path = new NavMeshPath();
            bool direct = NavMesh.SamplePosition(target.transform.position, out hit, agent.radius + 1, filter) &&
                Mathf.Abs(hit.position.y - TargetFeet(target)) <= 2 && NavMesh.CalculatePath(transform.position, hit.position, filter, path) && path.status == NavMeshPathStatus.PathComplete;
            if (direct && Vector3.Distance(hit.position, target.transform.position) > 2 &&
                !KhezuLandApproach.ClearLine(hit.position, target.transform, TargetFeet(target), catchObstructionMask)) direct = false;
            if (!direct)
            {
                bool wet = waterPursuit != null && waterPursuit.ContainsTarget(target.transform, target.capsule);
                if (wet &&
                    waterPursuit.TryRoute(target.transform, agent, shorelinePaceDistance, shorelineTurnDistance, out path))
                {
                    State = KhezuHuntState.Hunting; Status = "Following " + TargetName + " along the river bank";
                    agent.isStopped = false;
                    landApproach.Reset();
                    if (path == null || agent.SetPath(path)) return;
                }
                if (!wet && landApproach.TryRoute(target.transform, TargetFeet(target), agent, catchObstructionMask,
                    point => ElevatedApproachPenalty(target, point), out path))
                {
                    if (waterPursuit != null) waterPursuit.Reset();
                    State = KhezuHuntState.Hunting; Status = "Approaching " + TargetName + " around obstacles";
                    agent.isStopped = false;
                    if (path == null || agent.SetPath(path)) return;
                }
                agent.isStopped = true; agent.ResetPath(); State = KhezuHuntState.NavigationBlocked;
                Status = "No complete dry route to selected player"; return;
            }
            if (waterPursuit != null) waterPursuit.Reset();
            landApproach.Reset();
            State = KhezuHuntState.Hunting; Status = "Hunting " + TargetName;
            agent.isStopped = false;
            if (!agent.SetPath(path)) { agent.isStopped = true; State = KhezuHuntState.NavigationBlocked; Status = "Navigation could not accept the complete route"; }
        }
        static float TargetFeet(Target t)
        {
            return t.capsule == null ? t.transform.position.y : t.transform.TransformPoint(t.capsule.center).y - t.capsule.height * Mathf.Abs(t.transform.lossyScale.y) * .5f;
        }
        bool CanCatch(Target t)
        {
            if(!CaptureSlotAvailable())return false;
            if (!agent.enabled || !agent.isOnNavMesh) return false;
            float gap = TargetSurfaceGap(t);
            // A runner on rubble can be within the rig's reach without being on the
            // broad ground agent's route. Test actual upper-body sight lines there.
            if (TargetFeet(t) - transform.position.y > elevatedFeetThreshold)
                return CanReachElevatedTarget(t);
            // The navigation clearance can keep a supported runner outside the
            // rectangular torso even while the monster can turn and reach them.
            if (gap > catchSurfaceDistance) return CanReachGroundTarget(t);
            Vector3 center = t.capsule == null ? t.transform.position + Vector3.up : t.transform.TransformPoint(t.capsule.center);
            Vector3 from = transform.position, to = center;
            from.y = to.y = Mathf.Max(transform.position.y, TargetFeet(t)) + 1.5f;
            RaycastHit obstruction;
            if (!Physics.Linecast(from, to, out obstruction, catchObstructionMask, QueryTriggerInteraction.Ignore) ||
                obstruction.transform == t.transform || obstruction.transform.IsChildOf(t.transform)) return true;
            // A low rock can block this foot-level ray while the arms have clear
            // access to the torso. Solid walls still block every rig-to-player ray.
            return CanReachGroundTarget(t);
        }
        float TargetSurfaceGap(Target t)
        {
            Vector3 center = t.capsule == null ? t.transform.position + Vector3.up : t.transform.TransformPoint(t.capsule.center);
            float radius = t.capsule == null ? .8f : t.capsule.radius * Mathf.Max(Mathf.Abs(t.transform.lossyScale.x), Mathf.Abs(t.transform.lossyScale.z));
            float halfSegment = t.capsule == null ? 1 : Mathf.Max(0, t.capsule.height * Mathf.Abs(t.transform.lossyScale.y) * .5f - radius);
            var nearest = body.ClosestPoint(center);
            var axis = center; axis.y = Mathf.Clamp(nearest.y, center.y - halfSegment, center.y + halfSegment);
            nearest = body.ClosestPoint(axis);
            return Mathf.Max(0, Vector3.Distance(nearest, axis) - radius);
        }
        void StopNavigation()
        {
            if (waterPursuit != null) waterPursuit.Reset();
            landApproach.Reset();
            if (agent == null || !agent.enabled) return;
            agent.updateRotation = true;
            if (agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
            agent.enabled = false;
        }
        void Blend(string name)
        {
            if (animator.GetCurrentAnimatorStateInfo(0).IsName(name) || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(name))) return;
            animator.CrossFadeInFixedTime("Base Layer." + name, .25f, 0, 0);
        }
        public KhezuSnapshot GetSnapshot()
        {
            // Checkpoint the destination pose during a crossfade. A new Master must not
            // resume stage N with stage N-1's held pose and wait forever for completion.
            var animationState = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
            string animationName = driver.CurrentState;
            foreach (var name in clips.Keys) if (animationState.IsName(name)) { animationName = name; break; }
            // CrossFade is evaluated after Update. An immediate outcome checkpoint must
            // already describe Attack, even before the Animator has evaluated this frame.
            float animationTime=animationState.normalizedTime;
            if((BarrierResolving && ResolvedOutcome==2) || State==KhezuHuntState.AttackRecovery){
                bool attacking=EncounterClock<attackEndsAt;
                animationName=attacking?"Attack":"Idle";
                animationTime=attacking?Mathf.Clamp01((float)(EncounterClock-ResolvedAt)/ClipSeconds("Attack")):0;
            }
            if(IsTranquilized)
            {
                animationName="Sleep";
                animationTime=animationState.IsName("Sleep")?animationState.normalizedTime:0;
            }
            else if(State!=KhezuHuntState.Sleeping&&State!=KhezuHuntState.NestSleeping&&
                animationName=="Sleep"&&EncounterClock-tranquilizedResumeAt<.5)
            {
                // The first wake snapshot can precede Animator's CrossFade evaluation.
                animationName=tranquilizedResumeAnimation;animationTime=0;
            }
            var snapshot = new KhezuSnapshot { state = (int)State, mistakes = driver.Mistakes, stage = driver.PlayingStage,
                target = TargetPlayerId, caught = CaughtPlayerId, hunted = rotation.HuntedPlayerIds.OrderBy(i => i).ToArray(), excluded = excluded.OrderBy(i => i).ToArray(),
                position = transform.position, rotation = transform.rotation, animation = animationName,
                normalizedTime = animationTime, animationSpeed = animator.speed,
                pending = pending, awake = fullyAwake, huntSerial = HuntSerial, captureSerial = CaptureSerial,
                captureStartedAt = CaptureStartedAt, encounterPhase = EncounterPhase,
                rescueActor = RescueActor, rescueAtNest = RescueAtNest, rescuePickedUp = RescuePickedUp };
            FillBarrierSnapshot(snapshot);
            FillCloseCounterSnapshot(snapshot);
            return snapshot;
        }
        public void SetEncounterPhase(int value)
        {
            if (!IsAuthority || State != KhezuHuntState.Caught || value < 0 || value > 4 || EncounterPhase == value) return;
            EncounterPhase = value;
            if(value==4)EnsureBarrierTimer();
            SnapshotChanged?.Invoke();
        }
        public void ApplySnapshot(KhezuSnapshot s, bool initial)
        {
            if (!Initialized) return;
            RestoreBarrierSnapshot(s);
            RestoreCloseCounterSnapshot(s);
            bool notifyCatch = !initial && s.captureSerial > CaptureSerial;
            State = (KhezuHuntState)s.state; TargetPlayerId = s.target; CaughtPlayerId = s.caught;
            HuntSerial = s.huntSerial; CaptureSerial = s.captureSerial; pending = s.pending; fullyAwake = s.awake;
            CaptureStartedAt = s.captureStartedAt; EncounterPhase = s.encounterPhase;
            RestoreRescueCarry(s.rescueActor,s.rescueAtNest,s.rescuePickedUp);
            rotation.Restore(s.hunted); excluded.Clear(); foreach (int id in s.excluded) excluded.Add(id);
            driver.ApplyReplicaCounters(s.mistakes, s.stage);
            replica = s;
            if (initial)
            {
                transform.SetPositionAndRotation(s.position, s.rotation);
                if(IsTranquilized){TranquilizedAnchorPosition=s.position;TranquilizedAnchorRotation=s.rotation;}
            }
            // The shared supply checkpoint owns sedation. A coarse movement packet must
            // never replace the Sleep pose while that same checkpoint is still active.
            if (!IsTranquilized && clips.TryGetValue(s.animation, out var clip))
            {
                var info = animator.GetCurrentAnimatorStateInfo(0);
                if (initial) animator.Play("Base Layer." + s.animation, 0, s.normalizedTime);
                else if (!info.IsName(s.animation) && !(animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(s.animation)))
                    animator.CrossFadeInFixedTime("Base Layer." + s.animation, .15f, 0, s.normalizedTime * clip.length);
                animator.speed = s.animationSpeed;
            }
            Status = State + (TargetPlayerId == 0 ? "" : " — actor " + TargetPlayerId);
            var encounter=GetComponent<LogicLegends.OpenArchive.Cinematics.KhezuCaptureEncounterController>();
            if(encounter!=null)encounter.SynchronizeBarrierMovement();
            if (notifyCatch) PlayerCaught?.Invoke(CaughtPlayerId);
        }
    }
}
