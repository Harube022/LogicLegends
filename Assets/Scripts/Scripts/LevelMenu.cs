using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun; // 1. Added Photon namespace

public class LevelMenu : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject pausePanel;

    public void OpenPause()
    {
        pausePanel.SetActive(true);
        // Time.timeScale = 0f; // pause game
    }

    public void ClosePause()
    {
        pausePanel.SetActive(false);
        // Time.timeScale = 1f; // resume game
    }

    public void ReturnToMainMenu()
    {
        // 3. If we are in a multiplayer room, tell the server we are leaving
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
        else
        {
            // If we are just testing solo and aren't connected, load the menu immediately
            SceneManager.LoadScene("Main Menu");
        }
    }

    // 4. This is a built-in Photon method. It fires automatically the exact moment 
    // the server confirms we have successfully left the room.
    public override void OnLeftRoom()
    {
        SceneManager.LoadScene("Main Menu");
    }

    public void QuitGame()
    {
        // 5. If they close the app completely, sever the entire connection to Photon
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}