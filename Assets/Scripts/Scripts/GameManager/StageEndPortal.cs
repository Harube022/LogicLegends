using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun; // ---> NEW: Added Photon <---

// ---> FIX: Changed to MonoBehaviourPun <---
public class StageEndPortal : MonoBehaviourPun 
{
    [Header("Stage Manager Link")]
    [Tooltip("Drag your StageCompleteManager here!")]
    [SerializeField] private StageCompleteManager stageCompleteManager;
    
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
            Debug.Log("Stage Completed! Showing results...");
            
            if (PhotonNetwork.InRoom)
            {
                // Tell the Master Client to pull everyone into the new scene
                photonView.RPC("RPC_ShowResults", RpcTarget.MasterClient);
            }
            else
            {
                RPC_ShowResults();
            }
        }
    }

    [PunRPC]
    public void RPC_ShowResults()
    {
        if (stageCompleteManager != null)
        {
            stageCompleteManager.TriggerStageComplete();
        }
    }
}