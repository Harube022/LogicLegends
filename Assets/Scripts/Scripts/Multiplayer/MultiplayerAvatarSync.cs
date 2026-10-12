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

    private void Awake()
    {
        player = GetComponent<Player>();
        animators = GetComponentsInChildren<Animator>(true);
        targetPosition = transform.position; targetRotation = transform.rotation;
    }
    private void Start()
    {
        if (photonView.IsMine) return;
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
        if (photonView.IsMine || !received) return;
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
        if (!stream.IsWriting && info.Sender != photonView.Owner) return;
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position); stream.SendNext(transform.rotation);
            stream.SendNext(player != null && player.IsWalking());
            stream.SendNext(player != null && player.IsJumping());
            stream.SendNext(player != null && player.IsRunning());
        }
        else
        {
            targetPosition = (Vector3)stream.ReceiveNext(); targetRotation = (Quaternion)stream.ReceiveNext();
            walking = (bool)stream.ReceiveNext(); jumping = (bool)stream.ReceiveNext(); running = (bool)stream.ReceiveNext();
            if (!received) transform.SetPositionAndRotation(targetPosition, targetRotation);
            received = true;
        }
    }
}
