using UnityEngine;
using Photon.Pun;

[DefaultExecutionOrder(100)] // Read Player's movement flags after its Update.
public class PlayerAnimator : MonoBehaviour
{
    private const string IS_WALKING = "IsWalking";
    private const string IS_JUMPING = "IsJumping";
    private const string IS_RUNNING = "IsRunning";
    private const string IS_STUNNED = "IsStunned";
    public const string WALK_PLAYBACK = "WalkPlayback";
    public const string RUN_PLAYBACK = "RunPlayback";

    [SerializeField] private Player player;

    [Header("Foot speed at clip playback 1 (measured on this costume's rig)")]
    [SerializeField, Min(0.01f)] private float sourceWalkSpeed = 4f;
    [SerializeField, Min(0.01f)] private float sourceRunSpeed = 6f;

    private Animator animator;
    private PhotonView view;
    private CharacterController controller;
    private MultiplayerAvatarSync networkMotion;
    private bool hasRunParameter;
    private bool hasStunParameter;
    private bool hasWalkPlayback;
    private bool hasRunPlayback;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (player == null) player = GetComponentInParent<Player>();
        controller = GetComponentInParent<CharacterController>();
        networkMotion = GetComponentInParent<MultiplayerAvatarSync>();
        // CharacterController owns translation and gravity; animation only poses the mesh.
        animator.applyRootMotion = false;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == IS_RUNNING && parameter.type == AnimatorControllerParameterType.Bool)
                hasRunParameter = true;
            if (parameter.name == IS_STUNNED && parameter.type == AnimatorControllerParameterType.Bool)
                hasStunParameter = true;
            if (parameter.type == AnimatorControllerParameterType.Float)
            {
                if (parameter.name == WALK_PLAYBACK) hasWalkPlayback = true;
                if (parameter.name == RUN_PLAYBACK) hasRunPlayback = true;
            }
        }

        // Grab the PhotonView from this object or the parent object
        view = GetComponentInParent<PhotonView>();
    }

    public void SetStunned(bool stunned)
    {
        if (animator != null && hasStunParameter)
            animator.SetBool(IS_STUNNED, stunned);
    }

    private void Update()
    {
        bool remote = PhotonNetwork.InRoom && view != null && !view.IsMine;
        float speed;
        if (remote)
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

        // Separate state multipliers leave idle, jump, landing and stun timing untouched.
        // Actual velocity also handles guided walks, slopes and pushing against walls.
        float scale = Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.z));
        if (hasWalkPlayback) animator.SetFloat(WALK_PLAYBACK, speed / (sourceWalkSpeed * scale));
        if (hasRunPlayback) animator.SetFloat(RUN_PLAYBACK, speed / (sourceRunSpeed * scale));
    }
}
