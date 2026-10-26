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
        FindFirstObjectByType<MultiplayerGameChat>()?.Close();
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
        // A pause menu opened over a quiz must receive UI touches above that
        // modal. Gameplay stays blocked until the nested menus close in order.
        var menuCanvas = pausePanel.GetComponent<Canvas>();
        if (menuCanvas == null) menuCanvas = pausePanel.AddComponent<Canvas>();
        menuCanvas.overrideSorting = true;
        menuCanvas.sortingOrder = 1000;
        if (pausePanel.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            pausePanel.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        pausePanel.transform.SetAsLastSibling();
        pausePanel.SetActive(true);
        // Settings blocks this player's controls while the online match keeps running.
        if (!PhotonNetwork.InRoom) Time.timeScale = 0f;
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
            if (MultiplayerLobbyService.Instance != null && MultiplayerLobbyService.Instance.InLobby)
                MultiplayerLobbyService.Instance.LeaveLobby();
            else PhotonNetwork.LeaveRoom();
        }
        else
        {
            // Solo stages return through the same covered scene transition.
            TeleportManager.Navigate("Main Menu");
        }
    }

    // 4. This is a built-in Photon method. It fires automatically the exact moment 
    // the server confirms we have successfully left the room.
    public override void OnLeftRoom()
    {
        // The persistent online session already owns returning to the menu.
        if (MultiplayerLobbyService.Instance != null && !PhotonNetwork.OfflineMode) return;
        TeleportManager.Navigate("Main Menu");
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
