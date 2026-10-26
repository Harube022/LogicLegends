using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Firebase.Auth;
using Firebase.Database;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using NetPlayer = Photon.Realtime.Player;

/// <summary>Persistent room owner; exposes bounded room chat for future gameplay UI too.</summary>
[DefaultExecutionOrder(-400)]
public sealed partial class MultiplayerLobbyService : MonoBehaviourPunCallbacks, IOnEventCallback
{
    public static MultiplayerLobbyService Instance { get; private set; }
    public event Action Changed;
    public event Action<RoomChatMessage> ChatReceived;
    public IReadOnlyList<string> Messages => messages;
    public IReadOnlyList<RoomChatMessage> ChatMessages => chatMessages;
    public bool LastChatWasCensored { get; private set; }
    public string Status { get; private set; } = "Host a private game or enter a friend's code.";
    public bool Busy { get; private set; }
    public bool InLobby => PhotonNetwork.InRoom && !PhotonNetwork.OfflineMode;
    public bool Starting => InLobby && RoomState != LobbyRules.Waiting;
    public string RoomState => InLobby && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(LobbyRules.StateKey, out var state) ? state as string : "";
    public NetPlayer[] Players => InLobby ? PhotonNetwork.PlayerList.Where(p => !p.IsInactive).OrderBy(p => p.ActorNumber).ToArray() : Array.Empty<NetPlayer>();
    public bool WaitingForReconnect => InLobby && PhotonNetwork.PlayerList.Any(p => p.IsInactive);
    // Reserved teammates have not left the party. Starting without their lobby avatar
    // would strand recovery in a scene that never received their original spawn.
    public bool CanStart => InLobby && PhotonNetwork.IsMasterClient && !Starting && !leaving && LobbyRules.CanStart(PhotonNetwork.PlayerList);
    public string Region => PhotonNetwork.CloudRegion ?? PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion;
    public string TargetScene { get; set; } = MultiplayerSceneRoutes.GameplayPath;

    private readonly List<string> messages = new List<string>();
    private readonly List<RoomChatMessage> chatMessages = new List<RoomChatMessage>();
    private int chatSequence;
    private readonly Dictionary<int, float> lastReceived = new Dictionary<int, float>();
    private enum Request { None, Host, Join, Solo }
    private Request pending;
    private string pendingCode, disconnectNotice;
    private int createAttempts, requestVersion;
    private bool profileReady, leaving, committed, onlineSession;
    private float deadline, lastSent = -100f;
    private Coroutine startRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }
    public static MultiplayerLobbyService EnsureExists()
    {
        if (Instance != null) return Instance;
        return new GameObject("MultiplayerLobbyService").AddComponent<MultiplayerLobbyService>();
    }
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += SceneLoaded;
        InitializeConnectionRecovery();
    }
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        DisposeConnectionRecovery();
        if (Instance == this) Instance = null;
    }
    private void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Main Menu" && InLobby) LeaveLobby();
        // Solo selection uses SceneManager directly. An idle Master Server connection
        // must not later interrupt that solo stage when it disconnects.
        else if (scene.name != "Main Menu" && !PhotonNetwork.InRoom && pending == Request.None && PhotonNetwork.IsConnected)
            PhotonNetwork.Disconnect();
    }
    private void Update()
    {
        TickConnectionRecovery();
        if (IsRecovering) return;
        if (Busy && !Starting && deadline > 0 && Time.realtimeSinceStartup > deadline)
        {
            pending = Request.None; requestVersion++;
            disconnectNotice = "Connection timed out. Please try again.";
            PhotonNetwork.Disconnect();
            Fail("Connection timed out. Please try again.");
        }
    }
    public void Notify(string text) { Status = text; Changed?.Invoke(); }
    private void Fail(string text) { Busy = false; deadline = 0; pending = Request.None; Notify(text); }
    public void HostGame() { Begin(Request.Host, null); }
    public void JoinGame(string code)
    {
        code = LobbyRules.NormalizeCode(code);
        if (!LobbyRules.IsValidCode(code)) { Notify("Enter a six-character code, such as AB3D7K. Avoid I, O, 0 and 1."); return; }
        Begin(Request.Join, code);
    }
    public void StartSolo() { Begin(Request.Solo, null); }
    private void Begin(Request request, string code)
    {
        if (Busy || PhotonNetwork.InRoom) return;
        ClearRecoverySession();
        Busy = true; leaving = false; committed = false; disconnectNotice = null;
        pending = request; pendingCode = code; createAttempts = 0;
        deadline = Time.realtimeSinceStartup + 30f; requestVersion++;
        PhotonNetwork.AutomaticallySyncScene = request != Request.Solo;
        if (request == Request.Solo)
        {
            Notify("Opening solo game...");
            if (PhotonNetwork.NetworkClientState != ClientState.Disconnected && !PhotonNetwork.OfflineMode) PhotonNetwork.Disconnect();
            else EnterSolo();
            return;
        }
        if (PhotonNetwork.OfflineMode) PhotonNetwork.OfflineMode = false;
        ConfigureOnlineTransport();
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [LobbyRules.ReadyKey] = false });
        profileReady = false;
        StartCoroutine(ResolveProfile(requestVersion));
        Notify("Connecting to multiplayer...");
        // Keep the project's fixed Asia region; do not select different best regions for invitees.
        if (PhotonNetwork.IsConnected && PhotonNetwork.GameVersion != LobbyRules.Protocol) { PhotonNetwork.Disconnect(); return; }
        ConnectOrDispatch();
    }
    private IEnumerator ResolveProfile(int version)
    {
        string displayName = "Adventurer";
        if (FirebaseManager.IsReady)
        {
            FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
            if (user != null)
            {
                if (PlayerUsername.TryGetVerified(user.UserId, out string savedName)) displayName = savedName;
                else
                {
                    var task = FirebaseDatabase.DefaultInstance.RootReference.Child("users").Child(user.UserId)
                        .Child("username").GetValueAsync();
                    float until = Time.realtimeSinceStartup + 3f;
                    while (!task.IsCompleted && Time.realtimeSinceStartup < until && version == requestVersion) yield return null;
                    if (task.IsCompleted && !task.IsCanceled && !task.IsFaulted && FirebaseAuth.DefaultInstance.CurrentUser?.UserId == user.UserId &&
                        PlayerUsername.TryNormalize(task.Result.Value?.ToString(), out string username))
                    {
                        PlayerUsername.SetVerified(user.UserId, username);
                        displayName = username;
                    }
                }
            }
        }
        if (version != requestVersion) yield break;
        PhotonNetwork.NickName = LobbyRules.CleanText(displayName, 32);
        profileReady = true; ConnectOrDispatch();
    }
    private void ConnectOrDispatch()
    {
        if (pending == Request.None || pending == Request.Solo || leaving) return;
        if (PhotonNetwork.IsConnected && PhotonNetwork.GameVersion != LobbyRules.Protocol)
        {
            if (PhotonNetwork.NetworkClientState != ClientState.Disconnecting) PhotonNetwork.Disconnect();
            return;
        }
        if (PhotonNetwork.IsConnectedAndReady && (PhotonNetwork.NetworkClientState == ClientState.ConnectedToMasterServer || PhotonNetwork.InLobby))
        {
            if (!profileReady) return;
            Request request = pending; pending = Request.None;
            if (request == Request.Host) CreatePrivateRoom();
            else
            {
                Notify("Joining your friend's lobby...");
                if (!PhotonNetwork.JoinRoom(pendingCode)) Fail("Couldn't send the join request. Please try again.");
            }
        }
        else if (PhotonNetwork.NetworkClientState == ClientState.Disconnected || PhotonNetwork.NetworkClientState == ClientState.PeerCreated)
        {
            var settings = PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings());
            settings.AppVersion = LobbyRules.Protocol;
            if (!PhotonNetwork.ConnectUsingSettings(settings)) Fail("Couldn't connect to multiplayer. Please try again.");
        }
    }
    public override void OnConnectedToMaster() { if (IsRecovering) RejoinFromMaster(); else ConnectOrDispatch(); }
    private void CreatePrivateRoom()
    {
        createAttempts++;
        var options = new RoomOptions
        {
            MaxPlayers = LobbyRules.Capacity, IsVisible = false, IsOpen = true,
            PlayerTtl = LobbyRules.RecoverySeatMilliseconds, EmptyRoomTtl = LobbyRules.RecoverySeatMilliseconds,
            CleanupCacheOnLeave = true, BroadcastPropsChangeToAll = true,
            CustomRoomProperties = new Hashtable
            {
                [LobbyRules.VersionKey] = LobbyRules.Protocol, [LobbyRules.StateKey] = LobbyRules.Waiting,
                [LobbyRules.SceneKey] = TargetScene,
                [MultiplayerMonsterRotation.RoomKey] = (int)MultiplayerMonsterRotation.NextHostedMonster
            }
        };
        Notify("Creating your private lobby...");
        if (!PhotonNetwork.CreateRoom(LobbyRules.NewCode(), options, TypedLobby.Default)) Fail("Couldn't create a lobby. Please try again.");
    }
    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        if (returnCode == ErrorCode.GameIdAlreadyExists && createAttempts < 5) { CreatePrivateRoom(); return; }
        Fail(returnCode == ErrorCode.GameIdAlreadyExists ? "Couldn't reserve a code. Please try again." : LobbyRules.JoinError(returnCode));
    }
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        if (IsRecovering) RecoveryJoinFailed(returnCode);
        else Fail(LobbyRules.JoinError(returnCode));
    }
    public override void OnJoinedRoom()
    {
        if (PhotonNetwork.OfflineMode) { Busy = false; pending = Request.None; PhotonNetwork.LoadLevel("Stage 1"); return; }
        if (IsRecovering) { AcceptRecoveredRoom(); return; }
        var room = PhotonNetwork.CurrentRoom;
        if (leaving || !room.CustomProperties.TryGetValue(LobbyRules.VersionKey, out var protocol) || !Equals(protocol, LobbyRules.Protocol) ||
            !MultiplayerMonsterRotation.TryRead(room.CustomProperties, out _))
        {
            Notify("This lobby uses an incompatible game version. Update to the same version as the host.");
            leaving = true; PhotonNetwork.LeaveRoom(); return;
        }
        if (RoomState != LobbyRules.Waiting) { leaving = true; Notify("This game is already starting."); PhotonNetwork.LeaveRoom(); return; }
        Busy = false; deadline = 0; pending = Request.None; committed = false; onlineSession = true;
        RememberRecoveryRoom();
        messages.Clear(); chatMessages.Clear(); lastReceived.Clear(); lastSent = -100f;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [LobbyRules.ReadyKey] = false });
        AddSystem(LobbyRules.DisplayName(PhotonNetwork.LocalPlayer) + " joined the lobby.");
        Notify("Share the code, then mark yourself ready. Everyone must be ready to start.");
    }
    public void ToggleReady()
    {
        if (!InLobby || Starting || leaving) return;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [LobbyRules.ReadyKey] = !LobbyRules.IsReady(PhotonNetwork.LocalPlayer) });
    }
    public void StartGame()
    {
        if (!CanStart || startRoutine != null || committed)
        { Notify(WaitingForReconnect ? "A teammate is reconnecting. Wait for their return or for their reserved slot to expire." : "The host can start when 2–4 players are all ready."); return; }
        string target = PhotonNetwork.CurrentRoom.CustomProperties[LobbyRules.SceneKey] as string;
        if (string.IsNullOrEmpty(target) || !Application.CanStreamedLevelBeLoaded(target)) { Notify("The multiplayer scene is unavailable in this build."); return; }
        if (!MultiplayerMonsterRotation.TryRead(PhotonNetwork.CurrentRoom.CustomProperties, out _))
        { Notify("The lobby's monster selection is unavailable. Please host a new party."); return; }
        Busy = true; deadline = 0;
        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [LobbyRules.StateKey] = LobbyRules.Locking },
            new Hashtable { [LobbyRules.StateKey] = LobbyRules.Waiting });
        startRoutine = StartCoroutine(ConfirmStart(target));
        Notify("Checking players before starting...");
    }
    private IEnumerator ConfirmStart(string scene)
    {
        float timeout = Time.realtimeSinceStartup + 5f;
        while (InLobby && PhotonNetwork.IsMasterClient && RoomState != LobbyRules.Locking && Time.realtimeSinceStartup < timeout) yield return null;
        yield return new WaitForSecondsRealtime(0.5f);
        if (!InLobby || !PhotonNetwork.IsMasterClient) { startRoutine = null; Busy = false; yield break; }
        if (RoomState != LobbyRules.Locking || !LobbyRules.CanStart(PhotonNetwork.PlayerList)) { AbortStart(); yield break; }
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [LobbyRules.StateKey] = LobbyRules.Loading },
            new Hashtable { [LobbyRules.StateKey] = LobbyRules.Locking });
        while (InLobby && PhotonNetwork.IsMasterClient && RoomState != LobbyRules.Loading && Time.realtimeSinceStartup < timeout) yield return null;
        if (!InLobby || !PhotonNetwork.IsMasterClient) { startRoutine = null; Busy = false; yield break; }
        if (RoomState != LobbyRules.Loading || !LobbyRules.CanStart(PhotonNetwork.PlayerList)) { AbortStart(); yield break; }
        committed = true; startRoutine = null; Busy = false;
        if (MultiplayerMonsterRotation.TryRead(PhotonNetwork.CurrentRoom.CustomProperties, out var matchMonster))
            MultiplayerMonsterRotation.RecordStartedMatch(matchMonster);
        Notify("Starting game...");
        // PUN compares synchronized strings to Scene.name, so a full asset path would
        // reload on later room-property updates. Resolve that unambiguous path once
        // and synchronize its build index instead. Legacy name destinations still work.
        int buildIndex = SceneUtility.GetBuildIndexByScenePath(scene);
        if (buildIndex >= 0) PhotonNetwork.LoadLevel(buildIndex);
        else PhotonNetwork.LoadLevel(scene);
    }
    private void AbortStart()
    {
        if (startRoutine != null) { StopCoroutine(startRoutine); startRoutine = null; }
        Busy = false;
        if (InLobby && PhotonNetwork.IsMasterClient && !committed)
        {
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [LobbyRules.StateKey] = LobbyRules.Waiting });
            PhotonNetwork.CurrentRoom.IsOpen = true;
        }
        Notify("Start cancelled because the lobby changed. Check that everyone is ready.");
    }
    public void LeaveLobby()
    {
        if (leaving) return;
        EndRecoveryPresentation(); onlineSession = false; recovering = false; recoveryTerminalNotice = null;
        leaving = true; pending = Request.None; requestVersion++;
        if (startRoutine != null) { StopCoroutine(startRoutine); startRoutine = null; }
        Busy = true; deadline = Time.realtimeSinceStartup + 10f;
        messages.Clear(); chatMessages.Clear(); lastReceived.Clear(); Changed?.Invoke();
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom(false);
        else if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();
        else FinishLeaving();
    }
    public override void OnLeftRoom()
    {
        // Some SDK transports deliver this before OnDisconnected. Only an
        // explicit leave should discard a live gameplay session here.
        if (!leaving && onlineSession) return;
        FinishLeaving();
    }
    private void FinishLeaving()
    {
        Busy = false; deadline = 0; committed = false; pending = Request.None; onlineSession = false;
        messages.Clear(); chatMessages.Clear(); lastReceived.Clear();
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [LobbyRules.ReadyKey] = false });
        bool requested = leaving; leaving = false;
        if (!string.IsNullOrEmpty(recoveryTerminalNotice)) Status = recoveryTerminalNotice;
        else if (requested && !Status.Contains("incompatible") && !Status.Contains("already starting")) Status = "You left the lobby. Host or join another game.";
        Changed?.Invoke();
        if (SceneManager.GetActiveScene().name != "Main Menu") SceneManager.LoadScene("Main Menu");
    }
    public override void OnDisconnected(DisconnectCause cause)
    {
        if (ConnectionHandler.AppQuits || cause == DisconnectCause.ApplicationQuit)
        { onlineSession = false; ClearRecoverySession(); return; }
        RecordConnectionLoss(cause);
        if (!string.IsNullOrEmpty(recoveryTerminalNotice)) { Fail(recoveryTerminalNotice); return; }
        if (ContinueOrBeginRecovery(cause)) return;
        if (pending == Request.Solo) { EnterSolo(); return; }
        if (pending != Request.None && !leaving && cause == DisconnectCause.DisconnectByClientLogic) { ConnectOrDispatch(); return; }
        bool wasOnlineSession = onlineSession; onlineSession = false;
        messages.Clear(); chatMessages.Clear(); lastReceived.Clear(); committed = false;
        if (startRoutine != null) { StopCoroutine(startRoutine); startRoutine = null; }
        bool requested = leaving; leaving = false;
        Fail(disconnectNotice ?? (requested ? "You left the lobby." : DisconnectExplanation(cause)));
        disconnectNotice = null;
        if (wasOnlineSession && SceneManager.GetActiveScene().name != "Main Menu") StartCoroutine(ReturnAfterDisconnect());
    }
    private IEnumerator ReturnAfterDisconnect()
    {
        if (SceneManager.GetActiveScene().name != "Main Menu") yield return SceneManager.LoadSceneAsync("Main Menu");
        yield return null;
        var menu = FindFirstObjectByType<MultiplayerMenuManager>();
        if (menu != null && menu.LobbyUI != null) menu.LobbyUI.Open();
    }
    private void EnterSolo()
    {
        PhotonNetwork.OfflineMode = true;
        if (!PhotonNetwork.CreateRoom("OfflineSoloRoom")) Fail("Couldn't open the solo game. Please try again.");
    }
    public override void OnPlayerEnteredRoom(NetPlayer player)
    {
        AddSystem(LobbyRules.DisplayName(player) + (player.HasRejoined ? " reconnected." : " joined the lobby."));
        if (Starting && PhotonNetwork.IsMasterClient && !committed && SceneManager.GetActiveScene().name == "Main Menu") AbortStart();
        Changed?.Invoke();
    }
    public override void OnPlayerLeftRoom(NetPlayer player)
    {
        AddSystem(LobbyRules.DisplayName(player) + (player.IsInactive ? " lost connection. Their place is reserved." : " left the lobby.")); lastReceived.Remove(player.ActorNumber);
        if (Starting && PhotonNetwork.IsMasterClient && !committed && SceneManager.GetActiveScene().name == "Main Menu") AbortStart();
        Changed?.Invoke();
    }
    public override void OnMasterClientSwitched(NetPlayer player)
    {
        AddSystem(LobbyRules.DisplayName(player) + " is now the host.");
        if (PhotonNetwork.IsMasterClient && Starting && !committed && SceneManager.GetActiveScene().name == "Main Menu") AbortStart();
        Changed?.Invoke();
    }
    public override void OnPlayerPropertiesUpdate(NetPlayer player, Hashtable changes) { Changed?.Invoke(); }
    public override void OnRoomPropertiesUpdate(Hashtable changes) { Changed?.Invoke(); }
    public bool SendChat(string value)
    {
        LastChatWasCensored = false;
        if (!InLobby || leaving) return false;
        value = LobbyRules.CleanText(value, int.MaxValue);
        if (value.Length == 0) { Notify("Write a message first."); return false; }
        if (value.Length > LobbyRules.ChatLimit) { Notify("Messages can contain up to 200 characters."); return false; }
        if (Time.realtimeSinceStartup - lastSent < LobbyRules.ChatCooldown) { Notify("Please wait a moment before sending another message."); return false; }
        string censored = InappropriateTextFilter.Censor(value);
        if (!PhotonNetwork.RaiseEvent(LobbyRules.ChatEvent, censored, new RaiseEventOptions { Receivers = ReceiverGroup.All }, SendOptions.SendReliable)) return false;
        LastChatWasCensored = !string.Equals(value, censored, StringComparison.Ordinal);
        lastSent = Time.realtimeSinceStartup; return true;
    }
    public void OnEvent(EventData data)
    {
        if (!InLobby || data.Code != LobbyRules.ChatEvent || !(data.CustomData is string text)) return;
        var sender = PhotonNetwork.CurrentRoom.GetPlayer(data.Sender);
        if (sender == null || text.Length > LobbyRules.ChatLimit) return;
        text = LobbyRules.CleanText(text, LobbyRules.ChatLimit);
        if (text.Length == 0) return;
        if (lastReceived.TryGetValue(data.Sender, out var time) && Time.realtimeSinceStartup - time < LobbyRules.ChatCooldown * 0.8f) return;
        lastReceived[data.Sender] = Time.realtimeSinceStartup;
        AddMessage(text, sender.ActorNumber, LobbyRules.DisplayName(sender));
    }
    private void AddSystem(string text) { AddMessage(text, 0, "ROOM"); }
    private void AddMessage(string text, int actor, string name)
    {
        text = InappropriateTextFilter.Censor(text);
        if (actor != 0) name = InappropriateTextFilter.Censor(name);
        var entry = new RoomChatMessage(++chatSequence, actor, name, text);
        chatMessages.Add(entry);
        messages.Add(actor == 0 ? "• " + text : name + ": " + text);
        if (messages.Count > LobbyRules.HistoryLimit) { messages.RemoveAt(0); chatMessages.RemoveAt(0); }
        ChatReceived?.Invoke(entry);
        Changed?.Invoke();
    }
}

public sealed class RoomChatMessage
{
    public readonly int Sequence, Actor;
    public readonly string Name, Text;
    public bool IsSystem => Actor == 0;
    public RoomChatMessage(int sequence, int actor, string name, string text)
    { Sequence = sequence; Actor = actor; Name = name; Text = text; }
}
