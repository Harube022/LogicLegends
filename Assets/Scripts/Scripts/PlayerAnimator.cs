using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

[DefaultExecutionOrder(100)] // Read Player's movement flags after its Update.
public class PlayerAnimator : MonoBehaviour
{
    private const string IS_WALKING = "IsWalking";
    private const string IS_JUMPING = "IsJumping";
    private const string IS_RUNNING = "IsRunning";
    private const string IS_STUNNED = "IsStunned";

    [SerializeField] private Player player;

    private Animator animator;
    private PhotonView view;
    private bool hasRunParameter;
    private bool hasStunParameter;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == IS_RUNNING && parameter.type == AnimatorControllerParameterType.Bool)
                hasRunParameter = true;
            if (parameter.name == IS_STUNNED && parameter.type == AnimatorControllerParameterType.Bool)
                hasStunParameter = true;
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
        // 3. The crucial check: If this isn't our player, ignore this script!
        // The PhotonAnimatorView will take over and play the synced animations.
        if (view != null && !view.IsMine)
        {
            return;
        }

        animator.SetBool(IS_WALKING, player.IsWalking());
        animator.SetBool(IS_JUMPING, player.IsJumping());
        if (hasRunParameter) animator.SetBool(IS_RUNNING, player.IsRunning());
    }
}
