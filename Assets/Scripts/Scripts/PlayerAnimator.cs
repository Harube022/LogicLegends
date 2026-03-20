using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class PlayerAnimator : MonoBehaviour
{
    private const string IS_WALKING = "IsWalking";
    private const string IS_JUMPING = "IsJumping";

    [SerializeField] private Player player;

    private Animator animator;
    private PhotonView view;

    private void Awake()
    {
        animator = GetComponent<Animator>();

        // Grab the PhotonView from this object or the parent object
        view = GetComponentInParent<PhotonView>();
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
    }
}