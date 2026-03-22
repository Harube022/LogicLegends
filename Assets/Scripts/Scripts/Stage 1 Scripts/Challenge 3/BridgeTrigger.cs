using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;

public class BridgeTrigger : MonoBehaviour
{ 
    [Header("Debug")]
    [Tooltip("Watch this checkmark in the Inspector to see when it fires!")]
    [SerializeField] private bool hasFinished = false; 

    private HashSet<int> finishedPlayers = new HashSet<int>();
    private PhotonView view;

    private void Awake()
    {
        view = GetComponent<PhotonView>();
    }

    // Handles it if the bridge is a trigger (Is Trigger is CHECKED)
    private void OnTriggerEnter(Collider other)
    {
        if (hasFinished) return; 
        if (other.CompareTag("Player")) CheckPlayerCrossed(other.gameObject);
    }

    // Handles it if the bridge is solid (Is Trigger is UNCHECKED)
    private void OnCollisionEnter(Collision collision)
    {
        if (hasFinished) return;
        if (collision.gameObject.CompareTag("Player")) CheckPlayerCrossed(collision.gameObject);
    }

    private void CheckPlayerCrossed(GameObject playerObj)
    {
        PhotonView playerView = playerObj.GetComponent<PhotonView>();
        
        // ---> THE SOLO MODE FIX <---
        // Assume it's our player if we are offline, otherwise ask Photon
        bool isOurPlayer = true;
        if (playerView != null && PhotonNetwork.InRoom)
        {
            isOurPlayer = playerView.IsMine;
        }

        if (isOurPlayer)
        {
            if (view != null && PhotonNetwork.InRoom)
            {
                // ONLINE: Send the player's specific network ID
                view.RPC("RPC_PlayerCrossed", RpcTarget.All, playerView.ViewID);
            }
            else
            {
                // OFFLINE: Send a dummy ID since we are the only one playing
                RPC_PlayerCrossed(0); 
            }
        }
    }

    [PunRPC]
    public void RPC_PlayerCrossed(int playerViewID)
    {
        finishedPlayers.Add(playerViewID);

        int totalPlayersExpected = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.PlayerCount : 1;

        if (finishedPlayers.Count >= totalPlayersExpected && !hasFinished)
        {
            CompleteChallenge();
        }
    }

    private void CompleteChallenge()
    {
        hasFinished = true; 

        if (LevelManager.Instance != null) 
        {
            LevelManager.Instance.StopTimer(); 
            LevelManager.Instance.HideTimer(); 
            Debug.Log("Challenge 3 Complete! Timer Stopped."); 
        }

        // ---> THE INVISIBLE WALL FIX <---
        // Instead of Destroy() which breaks over the network, or disabling the collider which leaves invisible meshes,
        // we turn off the entire GameObject safely so it stops blocking you!
        gameObject.SetActive(false);
    }
}