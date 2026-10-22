using Photon.Pun;
using UnityEngine;

/// <summary>Network-only addition on the character variants; inherited character content stays untouched.</summary>
[RequireComponent(typeof(PhotonView))]
public sealed class MultiplayerAvatarSync : MonoBehaviourPun, IPunObservable
{
    private Player player;
    private Animator[] animators;
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private bool received, walking, jumping, running;
    public float AnimationMovementSpeed { get; private set; }

    private void Awake()
    {
        player = GetComponent<Player>();
        animators = GetComponentsInChildren<Animator>(true);
        targetPosition = transform.position; targetRotation = transform.rotation;
    }
    private void Start()
    {
        if (PhotonNetwork.InRoom && GetComponent<MultiplayerAvatarNameplate>() == null)
            gameObject.AddComponent<MultiplayerAvatarNameplate>();
        if (photonView.Owner != null && photonView.Owner.IsLocal) return;
        // Remote movement is received from its owner; it never consumes local input/physics.
        if (player != null) player.enabled = false;
        var controller = GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        var footsteps = GetComponent<PlayerFootsteps>();
        if (footsteps != null) footsteps.enabled = false;
        foreach (var look in GetComponentsInChildren<CameraTargetController>(true)) look.enabled = false;
        var visibility = GetComponent<PlayerVisibilityController>();
        if (visibility != null) visibility.enabled = false;
    }
    private void Update()
    {
        if ((photonView.Owner != null && photonView.Owner.IsLocal) || !received || MultiplayerLobbyService.IsGameplayRecovering) return;
        if (player != null && player.IsMovementLocked)
        {
            targetPosition = player.MovementLockPosition;
            walking = jumping = running = false;
            AnimationMovementSpeed = 0f;
        }
        float blend = 1f - Mathf.Exp(-15f * Time.unscaledDeltaTime);
        transform.position = Vector3.Distance(transform.position, targetPosition) > 12f
            ? targetPosition : Vector3.Lerp(transform.position, targetPosition, blend);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, blend);
        foreach (var animator in animators)
        {
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) continue;
            foreach (var parameter in animator.parameters)
            {
                if (parameter.type != AnimatorControllerParameterType.Bool) continue;
                if (parameter.name == "IsWalking") animator.SetBool(parameter.nameHash, walking);
                else if (parameter.name == "IsJumping") animator.SetBool(parameter.nameHash, jumping);
                else if (parameter.name == "IsRunning") animator.SetBool(parameter.nameHash, running);
            }
        }
    }
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting && (MultiplayerLobbyService.IsGameplayRecovering || photonView.Owner == null || !photonView.Owner.IsLocal || photonView.Owner.IsInactive)) return;
        if (!stream.IsWriting && info.Sender != photonView.Owner) return;
        if (stream.IsWriting)
        {
            if (player != null && player.IsMovementLocked) player.EnforceMovementLock();
            var controller = GetComponent<CharacterController>();
            bool canMove = player != null && player.enabled && !player.IsMovementLocked && controller != null && controller.enabled && Time.timeScale > 0;
            Vector3 velocity = canMove ? controller.velocity : Vector3.zero;
            velocity.y = 0f;
            bool moving = canMove && player.IsWalking() && velocity.sqrMagnitude > .0025f;
            stream.SendNext(transform.position); stream.SendNext(transform.rotation);
            stream.SendNext(moving);
            stream.SendNext(canMove && player.IsJumping());
            stream.SendNext(moving && player.IsRunning());
            stream.SendNext(velocity.magnitude);
        }
        else
        {
            targetPosition = (Vector3)stream.ReceiveNext(); targetRotation = (Quaternion)stream.ReceiveNext();
            walking = (bool)stream.ReceiveNext(); jumping = (bool)stream.ReceiveNext(); running = (bool)stream.ReceiveNext();
            AnimationMovementSpeed = (float)stream.ReceiveNext();
            // Consume the full packet before applying the Master-confirmed capture anchor.
            if (player != null && player.IsMovementLocked)
            {
                targetPosition = player.MovementLockPosition;
                walking = jumping = running = false;
                AnimationMovementSpeed = 0f;
                player.EnforceMovementLock();
            }
            if (!received) transform.SetPositionAndRotation(targetPosition, targetRotation);
            received = true;
        }
    }
    private void LateUpdate()
    {
        if (player != null && player.IsMovementLocked) player.EnforceMovementLock();
    }
}
