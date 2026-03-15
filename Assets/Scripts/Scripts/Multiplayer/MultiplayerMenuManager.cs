using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class MultiplayerMenuManager : MonoBehaviourPunCallbacks
{
    private void Start()
    {
        // Ensures that all players in a room load the same scene automatically
        PhotonNetwork.AutomaticallySyncScene = true;
        
        Debug.Log("Connecting to Photon...");
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Photon Master Server!");
        PhotonNetwork.JoinLobby();
    }

    public void OnClickMultiplayerButton()
    {
        if (PhotonNetwork.IsConnectedAndReady)
        {
            Debug.Log("Joining or Creating LogicLegendsRoom...");
            
            // THE FIX: We force both players to join the exact same room by name
            RoomOptions roomOptions = new RoomOptions { MaxPlayers = 4 }; 
            PhotonNetwork.JoinOrCreateRoom("LogicLegendsRoom", roomOptions, TypedLobby.Default);
        }
        else
        {
            Debug.LogWarning("Not connected to Photon yet. Retrying connection...");
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("Successfully joined a room! Loading Stage 1...");
        
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.LoadLevel("Stage 1 Multiplayer"); 
        }
    }
}