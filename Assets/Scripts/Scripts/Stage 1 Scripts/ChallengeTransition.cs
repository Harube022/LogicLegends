using UnityEngine;
using Photon.Pun;

public class ChallengeTransition : MonoBehaviourPun
{
    [Header("UI Transition")]
    [SerializeField] private GameObject oldObjectiveUI;
    [SerializeField] private GameObject newObjectiveUI;

    [Header("Next Challenge Setup")]
    [Tooltip("Drag the NEW ChallengeModule for the next area here")]
    [SerializeField] private ChallengeModule nextChallengeModule;

    [Header("Visibility Settings")]
    [SerializeField] private GameObject challengeToHide;
    [SerializeField] private GameObject challengeToShow;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PhotonView view = other.GetComponent<PhotonView>();

            // 1. Check if offline OR if it's our networked player
            bool isLocalPlayer = (view == null || !PhotonNetwork.InRoom) || view.IsMine;

            // 2. USE the variable here!
            if (isLocalPlayer)
            {
                // Ensure we have a PhotonView on this trigger object to send the RPC
                if (PhotonNetwork.InRoom && photonView != null)
                {
                    photonView.RPC("RPC_TransitionEveryone", RpcTarget.All);
                }
                else
                {
                    // Fallback for solo testing
                    RPC_TransitionEveryone();
                }
            }
        }
    }

    [PunRPC]
    public void RPC_TransitionEveryone()
    {
        // 1. Find the LOCAL player on this specific device and teleport them
        Player[] allPlayers = FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (Player p in allPlayers)
        {
            PhotonView pv = p.GetComponent<PhotonView>();
            
            // Check if offline OR if it's our networked player
            bool isLocalPlayer = (pv == null || !PhotonNetwork.InRoom) || pv.IsMine;

            if (isLocalPlayer)
            {
                p.transform.position = nextChallengeModule.GetRespawnPoint().position;
                
                Rigidbody playerRb = p.GetComponent<Rigidbody>();
                if (playerRb != null) playerRb.linearVelocity = Vector3.zero;
                
                break; // Found our player, stop looping
            }
        }

        // 2. Swap visibility
        if (challengeToHide != null) challengeToHide.SetActive(false);
        if (challengeToShow != null) challengeToShow.SetActive(true);

        // 3. Swap UI
        if (oldObjectiveUI != null) oldObjectiveUI.SetActive(false);
        if (newObjectiveUI != null) newObjectiveUI.SetActive(true);

        // 4. Update LevelManager with the new Module!
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.SetNewChallenge(nextChallengeModule);
            LevelManager.Instance.HideTimer();
        }
    }
}