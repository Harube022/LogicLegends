using UnityEngine;
using UnityEngine.Video;

/// <summary>Retains the existing Play-menu callback and opens the private lobby UI.</summary>
public class MultiplayerMenuManager : MonoBehaviour
{
    [SerializeField] private MultiplayerLobbyUI lobbyUI;
    [SerializeField] private string multiplayerScene = MultiplayerSceneRoutes.GameplayPath;
    [SerializeField] private VideoClip multiplayerStory;
    public VideoClip MultiplayerStory { get => multiplayerStory; set => multiplayerStory = value; }
    public MultiplayerLobbyUI LobbyUI { get => lobbyUI; set => lobbyUI = value; }
    public void OnClickMultiplayerButton()
    {
        EnterMultiplayer(null);
    }
    public void EnterMultiplayer(System.Action afterOpen)
    {
        System.Action open = () =>
        {
            if (this == null) return;
            OpenMultiplayerMenu();
            afterOpen?.Invoke();
        };
        if (!ArchiveFirstMultiplayerIntro.TryPlay(this, multiplayerStory, open)) open();
    }
    private void OpenMultiplayerMenu()
    {
        var service = MultiplayerLobbyService.EnsureExists();
        // Older menu scenes serialized the temporary Topics/Testing scene, which
        // was moved to Assets/Scenes. The production mode has one canonical route.
        service.TargetScene = MultiplayerSceneRoutes.GameplayPath;
        if (lobbyUI != null) lobbyUI.Open();
        else Debug.LogError("The multiplayer lobby UI has not been assigned.");
    }
    public void OnClickSoloButton() { MultiplayerLobbyService.EnsureExists().StartSolo(); }
}
