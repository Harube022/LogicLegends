using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class MultiplayerMenuManager : MonoBehaviourPunCallbacks
{
    // A flag to remember which mode the player selected
    private bool isPlayingSolo = false; 

    private void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        
        Debug.Log("Connecting to Photon...");
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Photon Master Server!");
        PhotonNetwork.JoinLobby();
    }

    // --- YOUR EXISTING MULTIPLAYER BUTTON ---
    public void OnClickMultiplayerButton()
    {
        isPlayingSolo = false; 
        PhotonNetwork.OfflineMode = false; // Make sure we are in online mode

        if (PhotonNetwork.IsConnectedAndReady)
        {
            Debug.Log("Joining or Creating LogicLegendsRoom...");
            RoomOptions roomOptions = new RoomOptions { MaxPlayers = 4 }; 
            PhotonNetwork.JoinOrCreateRoom("LogicLegendsRoom", roomOptions, TypedLobby.Default);
        }
        else
        {
            Debug.LogWarning("Not connected to Photon yet. Retrying connection...");
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    // --- NEW: YOUR SOLO BUTTON ---
    public void OnClickSoloButton()
    {
        isPlayingSolo = true;
        
        // Disconnect from the live server if we are connected
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect(); 
        }

        // Enable magic offline mode!
        PhotonNetwork.OfflineMode = true; 
        
        // Creating a room in offline mode happens instantly and locally
        PhotonNetwork.CreateRoom("OfflineSoloRoom"); 
    }

    // --- SCENE LOADING ROUTER ---
    public override void OnJoinedRoom()
    {
        Debug.Log("Successfully joined a room!");
        
        if (isPlayingSolo)
        {
            Debug.Log("Loading Solo Stage...");
            // Replace "Stage 1" with the exact name of your single-player scene!
            PhotonNetwork.LoadLevel("Stage 1"); 
        }
        else if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("Loading Multiplayer Stage...");
            PhotonNetwork.LoadLevel("Stage 1 Multiplayer"); 
        }
    }
}