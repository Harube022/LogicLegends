using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun; // 1. Added Photon namespace

public class LevelMenu : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject pausePanel;
    private bool pauseOpen;
    private float previousTimeScale;
    private bool previousInputBlocked;
    private Player pausedPlayer;
    private bool previousPlayerEnabled;
    private readonly System.Collections.Generic.List<Behaviour> pausedLookControllers = new System.Collections.Generic.List<Behaviour>();

    public void OpenPause()
    {
        if (pauseOpen || pausePanel == null) return;
        pauseOpen = true;
        previousTimeScale = Time.timeScale;
        previousInputBlocked = GameInput.Instance != null && GameInput.Instance.GameplayInputBlocked;
        pausedPlayer = Player.LocalInstance;
        previousPlayerEnabled = pausedPlayer != null && pausedPlayer.enabled;
        GameInput.Instance?.SetGameplayInputBlocked(true);
        pausedPlayer?.ToggleControl(false);
        foreach (var look in FindObjectsByType<CameraTargetController>(FindObjectsSortMode.None)) PauseLook(look);
        foreach (var look in FindObjectsByType<Unity.Cinemachine.CinemachineInputAxisController>(FindObjectsSortMode.None)) PauseLook(look);
        MobileLookInput.ResetDelta();
        pausePanel.transform.SetAsLastSibling();
        pausePanel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void ClosePause()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        RestorePause();
    }

    private void PauseLook(Behaviour look)
    {
        if (!look.enabled) return;
        pausedLookControllers.Add(look); look.enabled = false;
    }

    private void RestorePause()
    {
        if (!pauseOpen) return;
        pauseOpen = false;
        GameInput.Instance?.SetGameplayInputBlocked(previousInputBlocked);
        if (pausedPlayer != null) pausedPlayer.ToggleControl(previousPlayerEnabled);
        foreach (var look in pausedLookControllers) if (look != null) look.enabled = true;
        pausedLookControllers.Clear();
        MobileLookInput.ResetDelta();
        Time.timeScale = previousTimeScale;
        AudioVolumeSettings.Save();
    }

    public void ReturnToMainMenu()
    {
        RestorePause();
        AudioVolumeSettings.Save();
        // Add this to unfreeze global time before transitioning scenes
        Time.timeScale = 1f; 

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
        AudioVolumeSettings.Save();
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