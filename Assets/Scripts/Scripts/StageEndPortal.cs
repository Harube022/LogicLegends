using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun; // ---> NEW: Added Photon <---

// ---> FIX: Changed to MonoBehaviourPun <---
public class StageEndPortal : MonoBehaviourPun 
{
    [Header("Stage Transition")]
    public string nextSceneName;
    
    // Safety lock so it doesn't try to load the scene 50 times if they stand in it
    private bool isLoading = false; 

    private void OnTriggerEnter(Collider other)
    {
        if (isLoading) return;

        if (other.CompareTag("Player"))
        {
            // Make sure it's the actual player touching it, not a network ghost
            PhotonView view = other.GetComponent<PhotonView>();
            if (view != null && !view.IsMine && PhotonNetwork.InRoom) return;

            isLoading = true;
            Debug.Log("Loading next stage: " + nextSceneName);
            
            if (PhotonNetwork.InRoom)
            {
                // Tell the Master Client to pull everyone into the new scene
                photonView.RPC("RPC_LoadStage", RpcTarget.MasterClient);
            }
            else
            {
                // Fallback for playing Solo/Offline
                SceneManager.LoadScene(nextSceneName);
            }
        }
    }

    [PunRPC]
    public void RPC_LoadStage()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            // ---> THE MULTIPLAYER FIX <---
            // PhotonNetwork.LoadLevel automatically syncs the loading screen for the whole team!
            PhotonNetwork.LoadLevel(nextSceneName);
        }
    }
}