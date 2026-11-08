using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;

[DefaultExecutionOrder(100)] // Read Player's movement flags after its Update.
public class PlayerAnimator : MonoBehaviour
{
    private const string IS_WALKING = "IsWalking";
    private const string IS_JUMPING = "IsJumping";
    private const string IS_RUNNING = "IsRunning";
    private const string IS_STUNNED = "IsStunned";
    public const string WALK_PLAYBACK = "WalkPlayback";
    public const string RUN_PLAYBACK = "RunPlayback";
    public const string JUMP_PLAYBACK = "JumpPlayback";
    public const string STUN_TIME = "StunTime";
    public const string IS_CARRYING = "IsCarrying";
    private static readonly int StunState = Animator.StringToHash("Base Layer.Stun");

    [SerializeField] private Player player;

    [Header("Foot speed at clip playback 1 (measured on this costume's rig)")]
    [SerializeField, Min(0.00001f)] private float sourceWalkSpeed = 4f;
    [SerializeField, Min(0.00001f)] private float sourceRunSpeed = 6f;
    [SerializeField, Min(.01f)] private float sourceJumpDuration = 1f;
    [Header("Natural cadence limits (movement speed is unchanged)")]
    [SerializeField] private Vector2 walkPlaybackLimits = new Vector2(.8f, 1.65f);
    [SerializeField] private Vector2 runPlaybackLimits = new Vector2(.8f, 1.5f);
    [SerializeField, Min(.01f)] private float playbackSmoothing = .16f;
    [Header("Prepared skin cadence (steps per second at normal movement)")]
    [SerializeField] private bool useNaturalCadence;
    [SerializeField, Min(.01f)] private float walkClipDuration=1, runClipDuration=.7f;
    [SerializeField, Min(1)] private int walkCycles=1, runCycles=1;
    [SerializeField, Range(1.5f,3.6f)] private float walkStepsPerSecond=2.2f;
    [SerializeField, Range(2.5f,4.2f)] private float runStepsPerSecond=3f;
    public float WalkStrideRatio { get; private set; } = 1;
    public float RunStrideRatio { get; private set; } = 1;
    public float PlanarAnimationSpeed { get; private set; }
    [Header("One-shot stun: fall, grounded hold, recovery")]
    [SerializeField, Min(.1f)] private float sourceStunDuration = 3f;
    [SerializeField, Range(.1f,.9f)] private float stunRestPoint = .55f;
    private bool hasStunTime;
    private float stunStarted, stunDuration = 4.35f;
    private bool wasStunned;

    private Animator animator;
    private PhotonView view;
    private CharacterController controller;
    private MultiplayerAvatarSync networkMotion;
    private bool hasRunParameter;
    private bool hasStunParameter;
    private bool hasWalkPlayback;
    private bool hasRunPlayback;
    private bool hasJumpPlayback;
    private bool externalStun;
    private readonly HashSet<Object> stunOwners=new HashSet<Object>();
    private PlayerCarryRig carryRig;
    private int carryLayer = -1;
    private bool hasCarryParameter;
    public bool IsStunned => externalStun || stunOwners.Count > 0;
    private bool studentMotion;
    private float studentSpeed;
    private bool studentRunning;
    // NPCs use the same skin controller and retargeting, without a Player or input subscription.
    public void DriveStudentMotion(float speed, bool running=false) { studentMotion=true;studentSpeed=Mathf.Max(0,speed);studentRunning=running; }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (player == null) player = GetComponentInParent<Player>();
        controller = GetComponentInParent<CharacterController>();
        networkMotion = GetComponentInParent<MultiplayerAvatarSync>();
        carryRig = GetComponentInParent<PlayerCarryRig>();
        carryLayer = animator.GetLayerIndex("Carry Upper Body");
        // CharacterController owns translation and gravity; animation only poses the mesh.
        animator.applyRootMotion = false;
        if(useNaturalCadence)animator.speed=1f;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == IS_RUNNING && parameter.type == AnimatorControllerParameterType.Bool)
                hasRunParameter = true;
            if (parameter.name == IS_STUNNED && parameter.type == AnimatorControllerParameterType.Bool)
                hasStunParameter = true;
            if(parameter.name==IS_CARRYING&&parameter.type==AnimatorControllerParameterType.Bool)hasCarryParameter=true;
            if (parameter.type == AnimatorControllerParameterType.Float)
            {
                if (parameter.name == WALK_PLAYBACK) hasWalkPlayback = true;
                if (parameter.name == RUN_PLAYBACK) hasRunPlayback = true;
                if (parameter.name == JUMP_PLAYBACK) hasJumpPlayback = true;
                if (parameter.name == STUN_TIME) hasStunTime = true;
            }
        }

        // Grab the PhotonView from this object or the parent object
        view = GetComponentInParent<PhotonView>();
        ApplyStun();
    }

    public void SetStunned(bool stunned)
    {
        externalStun=stunned;ApplyStun();
    }
    public float MinimumStunDuration => sourceStunDuration;
    public void BeginStun(float duration)
    {
        // Duplicate hit signals must not restart a fall already in progress.
        if (externalStun) return;
        stunDuration = Mathf.Max(sourceStunDuration, duration);
        SetStunned(true);
        if(animator!=null&&animator.isActiveAndEnabled&&animator.HasState(0,StunState))
        {
            // Evaluate once at impact. Do not wait for a locomotion exit time or
            // transition blend, and never restart this state in Update.
            animator.SetBool(IS_WALKING,false);
            animator.SetBool(IS_JUMPING,false);
            if(hasRunParameter)animator.SetBool(IS_RUNNING,false);
            if(hasCarryParameter)animator.SetBool(IS_CARRYING,false);
            if(hasStunTime)animator.SetFloat(STUN_TIME,0f);
            if(carryLayer>=0)animator.SetLayerWeight(carryLayer,0f);
            animator.Play(StunState,0,0f);
            animator.Update(0f);
        }
    }
    public void AcquireStun(Object owner){if(owner!=null){stunOwners.Add(owner);ApplyStun();}}
    public void ReleaseStun(Object owner){stunOwners.Remove(owner);ApplyStun();}
    private void ApplyStun()
    {
        bool stunned = IsStunned;
        if(stunned && !wasStunned) stunStarted = Time.time;
        wasStunned = stunned;
        if(animator!=null && hasStunParameter) animator.SetBool(IS_STUNNED,stunned);
    }

    private void Update()
    {
        if(stunOwners.Count>0){stunOwners.RemoveWhere(o=>o==null);ApplyStun();}
        bool remote = PhotonNetwork.InRoom && view != null && !view.IsMine;
        float speed;
        if (studentMotion)
        {
            speed=Time.timeScale>0?studentSpeed:0;
            animator.SetBool(IS_WALKING,speed>.05f);
            animator.SetBool(IS_JUMPING,false);
            if(hasRunParameter)animator.SetBool(IS_RUNNING,studentRunning&&speed>.05f);
        }
        else if (remote)
        {
            // Use the owner's measured velocity, not network interpolation/catch-up speed.
            speed = networkMotion != null ? networkMotion.AnimationMovementSpeed : 0f;
        }
        else
        {
            bool canMove = player != null && player.enabled && controller != null && controller.enabled &&
                           Time.timeScale > 0f;
            Vector3 velocity = canMove ? controller.velocity : Vector3.zero;
            velocity.y = 0f;
            speed = velocity.magnitude;
            bool moving = canMove && player.IsWalking() && speed > 0.05f;
            animator.SetBool(IS_WALKING, moving);
            animator.SetBool(IS_JUMPING, canMove && player.IsJumping());
            if (hasRunParameter) animator.SetBool(IS_RUNNING, moving && player.IsRunning());
        }

        if(useNaturalCadence&&player!=null)
        {
            bool running=remote?animator.GetBool(IS_RUNNING):player.IsRunning();
            float permitted=player.moveSpeed*player.MovementSpeedMultiplier*(running?player.RunSpeedMultiplier:1);
            // Collision/teleport corrections are not extra voluntary footsteps.
            speed=Mathf.Min(speed,permitted);
        }
        // Separate state multipliers leave idle, jump, landing and stun timing untouched.
        // Actual velocity also handles guided walks, slopes and pushing against walls.
        float scale = Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.z));
        bool carrying=carryRig!=null&&carryRig.IsCarrying&&!IsStunned;
        if(hasCarryParameter)animator.SetBool(IS_CARRYING,carrying);
        float walk = Mathf.Clamp(speed / (sourceWalkSpeed * scale), carrying?.2f:walkPlaybackLimits.x, walkPlaybackLimits.y);
        float run = Mathf.Clamp(speed / (sourceRunSpeed * scale), runPlaybackLimits.x, runPlaybackLimits.y);
        if(useNaturalCadence)
        {
            // Count gait cycles, not just clip length. A two-cycle clip must not
            // take twice as many steps to cover the same gameplay distance.
            float normal=player!=null?player.moveSpeed:8f;
            walk=walkStepsPerSecond*walkClipDuration/(2*walkCycles)*Mathf.Clamp01(speed/Mathf.Max(.01f,normal));
            float running=normal*(player!=null?player.RunSpeedMultiplier:1.5f);
            run=runStepsPerSecond*runClipDuration/(2*runCycles)*Mathf.Clamp01(speed/Mathf.Max(.01f,running));
        }
        if (hasWalkPlayback) animator.SetFloat(WALK_PLAYBACK, walk, playbackSmoothing, Time.deltaTime);
        if (hasRunPlayback) animator.SetFloat(RUN_PLAYBACK, run, playbackSmoothing, Time.deltaTime);
        PlanarAnimationSpeed=speed;
        // Motion is entirely clip-driven. No stride or pelvis correction follows.
        if (hasJumpPlayback) animator.SetFloat(JUMP_PLAYBACK, player != null ? sourceJumpDuration / player.JumpFlightDuration : 1f);
        if (hasStunTime && IsStunned)
        {
            float elapsed = Mathf.Max(0, Time.time-stunStarted);
            float fall = sourceStunDuration * stunRestPoint;
            float hold = Mathf.Max(0, stunDuration-sourceStunDuration);
            float clipTime = elapsed <= fall ? elapsed : elapsed < fall+hold ? fall : elapsed-hold;
            animator.SetFloat(STUN_TIME, Mathf.Clamp01(clipTime/sourceStunDuration));
        }
        if (carryLayer >= 0)
        {
            float target = carryRig != null && carryRig.IsCarrying && !IsStunned ? 1f : 0f;
            animator.SetLayerWeight(carryLayer, IsStunned ? 0f : Mathf.MoveTowards(animator.GetLayerWeight(carryLayer), target, Time.deltaTime / .18f));
        }
    }
}
