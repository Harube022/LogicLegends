using UnityEngine;
using Photon.Pun;
using System; // Required to use Actions

public class ChallengeTransition : MonoBehaviourPun
{
    [Header("UI Transition")]
    [SerializeField] private GameObject oldObjectiveUI;
    [SerializeField] private GameObject newObjectiveUI;

    [Header("Next Challenge Setup")]
    [Tooltip("Drag the NEW ChallengeModule for the next area here")]
    [SerializeField] private ChallengeModule nextChallengeModule;

    // ---> NEW: Added a checkbox to control the heart baselining <---
    [Header("Stage Settings")]
    [Tooltip("Check this ONLY if this transition moves the player to a completely new Stage (resets hearts to 3)")]
    [SerializeField] private bool isTransitionToNewStage = false;

    [Header("Visibility Settings")]
    [SerializeField] private GameObject challengeToHide;
    [SerializeField] private GameObject challengeToShow;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PhotonView view = other.GetComponent<PhotonView>();
            bool isLocalPlayer = (view == null || !PhotonNetwork.InRoom) || view.IsMine;

            if (isLocalPlayer)
            {
                if (PhotonNetwork.InRoom && photonView != null)
                {
                    photonView.RPC("RPC_TransitionEveryone", RpcTarget.All);
                }
                else
                {
                    RPC_TransitionEveryone();
                }
            }
        }
    }

    [PunRPC]
    public void RPC_TransitionEveryone()
    {
        Player[] allPlayers = FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (Player p in allPlayers)
        {
            PhotonView pv = p.GetComponent<PhotonView>();
            bool isLocalPlayer = (pv == null || !PhotonNetwork.InRoom) || pv.IsMine;

            if (isLocalPlayer)
            {
                // 1. Package the visibility and UI swaps into a neat Action
                Action environmentSwapAction = () => 
                {
                    if (challengeToHide != null) challengeToHide.SetActive(false);
                    if (challengeToShow != null) challengeToShow.SetActive(true);
                    
                    if (oldObjectiveUI != null) oldObjectiveUI.SetActive(false);
                    if (newObjectiveUI != null) newObjectiveUI.SetActive(true);

                    if (LevelManager.Instance != null && nextChallengeModule != null)
                    {
                        // ---> NEW: We now pass your checkbox setting to the LevelManager <---
                        LevelManager.Instance.SetNewChallenge(nextChallengeModule, isTransitionToNewStage);
                        LevelManager.Instance.HideTimer();
                    }
                };

                // 2. Pass the action to the TeleportManager to execute when black
                if (TeleportManager.Instance != null)
                {
                    TeleportManager.Instance.StartTeleport(p.gameObject, nextChallengeModule.GetRespawnPoint(), environmentSwapAction);
                }
                else
                {
                    // Fallback
                    p.transform.position = nextChallengeModule.GetRespawnPoint().position;
                    Rigidbody playerRb = p.GetComponent<Rigidbody>();
                    if (playerRb != null) playerRb.linearVelocity = Vector3.zero;
                    
                    environmentSwapAction.Invoke(); // Execute instantly if no manager exists
                }
                
                break; 
            }
        }
    }
}