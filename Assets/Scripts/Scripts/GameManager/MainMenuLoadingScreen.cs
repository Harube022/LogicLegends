using System.Collections;
using Photon.Pun;
using UnityEngine;

/// <summary>Opt-in binding saved only in Main Menu. Never installs itself in gameplay.</summary>
[DisallowMultipleComponent]
public sealed class MainMenuLoadingScreen : MonoBehaviour
{
    [SerializeField] private SharedLoadingScreen screen;
    private PrelimTeleportTransition transition;
    private MultiplayerLobbyService lobby;
    private bool startup = true, multiplayerLoading;

    private IEnumerator Start()
    {
        if (gameObject.scene.path != "Assets/Scenes/Main Menu.unity" || screen == null) { enabled = false; yield break; }
        transition = TeleportManager.EnsureExists().GetComponent<PrelimTeleportTransition>();
        if (transition != null) transition.LoadingScreen = screen;
        if (!screen.IsVisible) screen.Show("Loading", -1f, false);
        // AuthManager owns readiness, including slow or failed internet requests.
        yield return null;
        startup = false;
    }

    private void Update()
    {
        var current = MultiplayerLobbyService.Instance;
        if (lobby != current)
        {
            if (lobby != null) lobby.Changed -= RefreshLobby;
            lobby = current;
            if (lobby != null) lobby.Changed += RefreshLobby;
        }
        if (startup) return;
        RefreshLobby();
        if (multiplayerLoading && PhotonNetwork.LevelLoadingProgress > 0f)
            screen.SetProgress(PhotonNetwork.LevelLoadingProgress);
    }

    private void RefreshLobby()
    {
        if (startup || screen == null) return;
        bool loading = lobby != null && lobby.InLobby && lobby.RoomState == LobbyRules.Loading;
        if (loading && !multiplayerLoading) screen.Show("Loading your shared adventure");
        else if (!loading && multiplayerLoading) screen.HideImmediate();
        multiplayerLoading = loading;
    }

    public IEnumerator RevealAfterIntro(System.Action prepareMenu)
    {
        if (screen == null || !isActiveAndEnabled)
        {
            prepareMenu();
            yield break;
        }
        screen.Show("Loading", 0f);
        yield return null;
        yield return RevealLoadedContent(prepareMenu);
    }

    public void ShowAccountLoading()
    {
        screen.Show("Loading");
    }

    public void ShowConnectionError(System.Action retry, string title = null, string message = null)
    {
        screen.ShowConnectionError(retry, CancelApplication, title, message);
    }

    public static void CancelApplication()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    public IEnumerator RevealLoadedContent(System.Action prepareContent)
    {
        screen.SetProgress(1f);
        yield return null;
        yield return TeleportManager.EnsureExists().RevealAfterLoading(screen, prepareContent);
    }

    private void OnDisable()
    {
        if (lobby != null) lobby.Changed -= RefreshLobby;
        if (transition != null && transition.LoadingScreen == screen) transition.LoadingScreen = null;
        if (screen != null) screen.HideImmediate();
    }
}
