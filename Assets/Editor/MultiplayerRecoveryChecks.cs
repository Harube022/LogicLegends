using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ExitGames.Client.Photon;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.OpenArchive.Topics;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>Private-room transport checks with real loss simulation, rejoin and authoritative checkpoints.</summary>
public static class MultiplayerRecoveryChecks
{
    [Serializable] sealed class Report
    {
        public bool passed;
        public string scope;
        public List<string> checks = new List<string>(), errors = new List<string>(), transportTimeouts = new List<string>();
    }
    static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
    static Report report;
    static LoadBalancingClient guest;
    static ConnectionHandler heartbeat;
    static MultiplayerLobbyService service;
    static double nextTick;
    static bool active, lossApplied;
    public static string Status { get; private set; } = "Idle";
    const string Folder = "Temp/MultiplayerRecovery";
    [MenuItem("Logic Legends/Multiplayer/Run Connection Recovery Checks (Play Mode)")]
    public static void Run()
    {
        if (!Application.isPlaying || SceneManager.GetActiveScene().name != "Main Menu" || PhotonNetwork.InRoom || active)
            throw new InvalidOperationException("Run from Main Menu in Play Mode, outside a room.");
        report = new Report { scope = "Rendered PUN client plus independent live Realtime peer. Transport: " +
            PhotonNetwork.PhotonServerSettings.AppSettings.Protocol + ". Simulated 100% SDK packet loss; no physical Android/carrier testing." };
        stack.Clear(); nextTick = 0; active = true; Status = "Starting";
        Directory.CreateDirectory(Folder); Application.logMessageReceived += Log;
        EditorApplication.update += Tick; EditorApplication.playModeStateChanged += PlayState; stack.Push(Flow()); Save();
    }
    static void Log(string message, string trace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception) return;
        if (message.StartsWith("Connection lost. OnStatusChanged to TimeoutDisconnect.")) report.transportTimeouts.Add(message);
        else report.errors.Add(message + "\n" + trace);
    }
    static void Tick()
    {
        if (!active) return;
        try
        {
            EditorApplication.QueuePlayerLoopUpdate(); guest?.Service();
            if (EditorApplication.timeSinceStartup < nextTick) return;
            if (stack.Count == 0) { Finish(true); return; }
            var routine = stack.Peek();
            if (!routine.MoveNext()) { stack.Pop(); return; }
            if (routine.Current is IEnumerator nested) stack.Push(nested);
            else nextTick = EditorApplication.timeSinceStartup + (routine.Current is float delay ? delay : 0);
        }
        catch (Exception error) { report.errors.Add(Status + ": " + error); Finish(false); }
    }
    static void Check(bool condition, string description)
    {
        report.checks.Add((condition ? "PASS " : "FAIL ") + description); Save();
        if (!condition) throw new InvalidOperationException(description);
    }
    static IEnumerator Wait(Func<bool> ready, string label, float seconds = 35)
    {
        double deadline = EditorApplication.timeSinceStartup + seconds;
        while (!ready())
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException(label + "; PUN=" + PhotonNetwork.NetworkClientState + "; status=" + service?.Status);
            yield return .025f;
        }
    }
    static void Loss(bool enabled)
    {
        lossApplied = enabled;
        var peer = PhotonNetwork.NetworkingClient.LoadBalancingPeer;
        peer.NetworkSimulationSettings.IncomingLossPercentage = enabled ? 100 : 0;
        peer.NetworkSimulationSettings.OutgoingLossPercentage = enabled ? 100 : 0;
        peer.IsSimulationEnabled = enabled;
    }
    static IEnumerator Flow()
    {
        var entry = UnityEngine.Object.FindFirstObjectByType<MultiplayerMenuManager>();
        UnityEngine.Object.FindFirstObjectByType<MainMenuManager>().ShowPlayMenu(); entry.OnClickMultiplayerButton();
        service = MultiplayerLobbyService.Instance; service.HostGame();
        yield return Wait(() => service.InLobby, "Host room created");
        Check(PhotonNetwork.CurrentRoom.PlayerTtl == 45000 && PhotonNetwork.CurrentRoom.EmptyRoomTtl == 45000, "Real lobby reserves player and room for 45 seconds around the 30-second recovery window");
        var settings = PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings());
        settings.AppVersion = PhotonNetwork.NetworkingClient.AppVersion; settings.FixedRegion = PhotonNetwork.CloudRegion;
        guest = new LoadBalancingClient(); guest.NickName = "Recovery guest QA";
        // This non-rendered control peer is serviced by EditorApplication.update.
        // Give it room for editor stalls; the game client's real timeout is unchanged.
        guest.LoadBalancingPeer.DisconnectTimeout = 60000;
        var keeper = new GameObject("Recovery peer heartbeat"); UnityEngine.Object.DontDestroyOnLoad(keeper);
        heartbeat = keeper.AddComponent<ConnectionHandler>(); heartbeat.Client = guest; heartbeat.KeepAliveInBackground = 180000; heartbeat.StartFallbackSendAckThread();
        guest.ConnectUsingSettings(settings);
        yield return Wait(() => guest.State == ClientState.ConnectedToMasterServer, "Guest connected");
        guest.OpJoinRoom(new EnterRoomParams { RoomName = PhotonNetwork.CurrentRoom.Name });
        yield return Wait(() => guest.InRoom && service.Players.Length == 2, "Guest joined");
        Status = "Waiting lobby recovery";
        int lobbyActor = PhotonNetwork.LocalPlayer.ActorNumber;
        Loss(true); yield return Wait(() => service.IsRecovering, "Waiting lobby timeout", 40);
        Check(SceneManager.GetActiveScene().name == "Main Menu" && GameInput.Instance.GameplayInputBlocked, "Waiting lobby also presents bounded recovery instead of discarding its room");
        Loss(false); yield return Wait(() => !service.IsRecovering && PhotonNetwork.InRoom, "Waiting lobby rejoined", 35);
        Check(PhotonNetwork.LocalPlayer.ActorNumber == lobbyActor && !LobbyRules.IsReady(PhotonNetwork.LocalPlayer), "Lobby rejoin preserves the same actor and original readiness");
        yield return Wait(() => guest.InRoom, "Independent lobby control peer remains connected");
        guest.CurrentRoom.SetMasterClient(guest.CurrentRoom.GetPlayer(lobbyActor));
        yield return Wait(() => PhotonNetwork.IsMasterClient && !service.IsRecovering && guest.InRoom, "Lobby authority restored for gameplay checks");
        if (!LobbyRules.IsReady(guest.LocalPlayer)) guest.LocalPlayer.SetCustomProperties(new Hashtable { [LobbyRules.ReadyKey] = true });
        if (!LobbyRules.IsReady(PhotonNetwork.LocalPlayer)) service.ToggleReady();
        yield return Wait(() => service.CanStart, "Room ready"); service.StartGame();
        yield return Wait(() => MultiplayerSceneRoutes.IsOpenArchive(SceneManager.GetActiveScene()) && Player.LocalInstance != null && ArchiveTopicSession.Current.Ready, "Archive ready", 70);
        yield return Wait(() => TeleportManager.Instance != null && !TeleportManager.Instance.IsTransitioning, "Spawn fade complete");
        var session = ArchiveTopicSession.Current; var hunting = session.hunting;
        int hostActor = PhotonNetwork.LocalPlayer.ActorNumber, guestActor = guest.LocalPlayer.ActorNumber;
        int hostView = Player.LocalInstance.GetComponent<PhotonView>().ViewID;
        int guestView = guestActor * PhotonNetwork.MAX_VIEW_IDS + 1;
        var spawner = UnityEngine.Object.FindFirstObjectByType<PlayerSpawner>();
        guest.OpRaiseEvent(202, new Hashtable { [(byte)0] = "Multiplayer_Female", [(byte)1] = spawner.ResolveSpawnPosition(Resources.Load<GameObject>("Multiplayer_Female"), guestActor), [(byte)6] = PhotonNetwork.ServerTimestamp, [(byte)7] = guestView },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others, CachingOption = EventCaching.AddToRoomCache }, SendOptions.SendReliable);
        yield return Wait(() => PhotonView.Find(guestView) != null, "Guest avatar instantiated"); yield return 1.3f;
        hunting.health.DamageAuthoritativeActor(hostActor); hunting.health.DamageAuthoritativeActor(guestActor);
        var fixture = hunting.GetSnapshot(); fixture.hunted = new[] { guestActor }; hunting.ApplySnapshot(fixture, true); hunting.Request(KhezuCommand.Sleep);
        // Trusted QA fixture commits through the existing state owner, never a gameplay bypass.
        var state = session.State.stations[0]; state.round = 1; state.phase = 1; state.timerRunning = true;
        state.timerLimit = state.timerSavedRemaining = 360; state.timerDeadline = ArchiveTopicSession.Clock + 360; state.timerAttempt = 1;
        session.State.mistakes.Add(new ArchiveMistake { actor = hostActor, topic = 0, round = 0, reason = "Recovery QA fixture" });
        typeof(ArchiveTopicSession).GetMethod("Commit", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(session, null);
        int seed = session.State.seed;
        yield return Wait(() => PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(MultiplayerLobbyService.AvatarCheckpointKey), "Avatar checkpoint exists");
        yield return 1.2f;
        Check(hunting.health.ActorHearts(hostActor) == 2 && hunting.health.ActorHearts(guestActor) == 2, "Both original actors begin with two authoritative hearts");
        Status = "Settings and paused dispatch";
        var menu = UnityEngine.Object.FindFirstObjectByType<LevelMenu>(); menu.OpenPause();
        Check(Time.timeScale == 1 && GameInput.Instance.GameplayInputBlocked, "Online Settings blocks local controls without pausing the match");
        Chat("Settings network probe"); yield return Wait(() => service.ChatMessages.Any(m => m.Text == "Settings network probe"), "Chat received while Settings open");
        Check(true, "Incoming traffic is dispatched while Settings is open"); menu.ClosePause();
        yield return 1.1f; Time.timeScale = 0; Chat("Zero-scale network probe");
        yield return Wait(() => service.ChatMessages.Any(m => m.Text == "Zero-scale network probe"), "Paused dispatch works");
        Check(PhotonNetwork.InRoom && PhotonNetwork.MinimalTimeScaleToDispatchInFixedUpdate == 0, "Even explicit timeScale zero keeps Photon receiving messages"); Time.timeScale = 1;
        Status = "Inactive guest identity and retained state";
        guest.Disconnect(); yield return Wait(() => PhotonNetwork.CurrentRoom.GetPlayer(guestActor).IsInactive, "Guest becomes inactive"); yield return 1.2f;
        Check(hunting.health.ActorHearts(guestActor) == 2 && hunting.GetSnapshot().hunted.Contains(guestActor), "Inactive slot retains hearts and no-repeat hunting history");
        Check(!session.CanAct(guestActor) && !PhotonView.Find(guestView).GetComponent<Player>().enabled && Player.LocalInstance.GetComponent<PhotonView>().OwnerActorNr == hostActor,
            "Inactive avatar cannot take local input or submit challenge actions");
        Check(session.SurvivorCount == 2, "A reconnecting survivor remains part of the team escape requirement");
        yield return Wait(() => guest.State == ClientState.Disconnected, "Guest disconnected");
        Check(guest.ReconnectAndRejoin(), "Independent guest starts token-based rejoin");
        yield return Wait(() => guest.InRoom && !PhotonNetwork.CurrentRoom.GetPlayer(guestActor).IsInactive, "Guest rejoined");
        Check(guest.LocalPlayer.ActorNumber == guestActor && hunting.health.ActorHearts(guestActor) == 2 && hunting.GetSnapshot().hunted.Contains(guestActor), "Guest reclaims same actor, hearts and hunting history");
        Check(UnityEngine.Object.FindObjectsByType<MultiplayerAvatarSync>(FindObjectsSortMode.None).Count(a => a.GetComponent<PhotonView>().OwnerActorNr == guestActor) == 1, "Guest rejoin leaves exactly one guest avatar");
        int handle = SceneManager.GetActiveScene().handle;
        yield return DropAndRecover("Former host", hostActor, hostView, seed, handle);
        Check(!PhotonNetwork.IsMasterClient && guest.CurrentRoom.MasterClientId == guestActor && !PhotonNetwork.CurrentRoom.IsOpen,
            "Host loss migrates authority; returning former host does not steal it or reopen the match");
        Status = "All players briefly inactive";
        guest.Disconnect();
        yield return Wait(() => PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom.GetPlayer(guestActor).IsInactive, "Remaining peer inactive");
        yield return 1.3f;
        yield return DropAndRecover("Empty room", hostActor, hostView, seed, handle);
        Check(PhotonNetwork.IsMasterClient && !PhotonView.Find(guestView).GetComponent<Player>().enabled &&
            UnityEngine.Object.FindObjectsByType<Player>(FindObjectsSortMode.None).Count(p => p.enabled) == 1,
            "Empty-room rejoin restores authority without giving an inactive remote avatar local controls");
        yield return Wait(() => guest.State == ClientState.Disconnected, "Second peer disconnected");
        Check(guest.ReconnectAndRejoin(), "Other inactive teammate starts rejoin after the room resumes");
        yield return Wait(() => guest.InRoom && !PhotonNetwork.CurrentRoom.GetPlayer(guestActor).IsInactive, "Both players restored");
        Status = "Barrier recovery fixture";
        guest.CurrentRoom.SetMasterClient(guest.CurrentRoom.GetPlayer(hostActor));
        yield return Wait(() => PhotonNetwork.IsMasterClient, "Rendered host regains authority for capture fixture");
        var captured = hunting.GetSnapshot(); captured.state = (int)KhezuHuntState.Caught;
        captured.target = captured.caught = hostActor; captured.captureSerial++; captured.huntSerial++;
        captured.awake = true; captured.pending = false; captured.animation = "Idle"; captured.animationSpeed = 1;
        captured.mistakes = captured.stage = 4; captured.capturePosition = Player.LocalInstance.transform.position; captured.captureAnchorValid = true;
        captured.captureStartedAt = KhezuHuntingController.EncounterClock - 10; captured.encounterPhase = 4;
        captured.barrierTimerSerial = captured.captureSerial; captured.barrierStartedAt = KhezuHuntingController.EncounterClock;
        captured.barrierDeadline = captured.barrierStartedAt + 180;
        hunting.ApplySnapshot(captured, true);
        yield return Wait(() => Player.LocalInstance.IsMovementLocked && session.State.barrier.actor == hostActor, "Captured player has a real host-confirmed barrier challenge");
        yield return 2.4f;
        Check(session.State.barrier.outcome == 0 && hunting.State == KhezuHuntState.Caught, "Barrier remains unresolved before loss");
        yield return DropAndRecover("Captured former host", hostActor, hostView, seed, handle, true);
        Status = "Sustained outage and bounded failure";
        Loss(true); yield return Wait(() => service.IsRecovering, "Outage starts recovery", 40);
        yield return Wait(() => SceneManager.GetActiveScene().name == "Main Menu" && !service.IsRecovering, "Recovery budget expires", 40);
        Loss(false);
        Check(!service.Busy && service.Status.Contains("Reason:") && service.Status.Contains("30 seconds"), "Unrecoverable outage exits after the bounded window with a concrete reason");
        bool recoveryBlock = (bool)typeof(MultiplayerLobbyService).GetField("recoveryOwnsInput", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(service);
        Check(!recoveryBlock && !UnityEngine.Object.FindObjectsByType<MultiplayerRecoveryUI>(FindObjectsSortMode.None).Any(ui => ui.IsVisible), "Failure releases recovery's input ownership and removes its overlay; the existing lobby owns menu input");
        Check(File.Exists(service.LastDisconnectReportPath), "Device-local diagnostic report exists");
        var text = File.ReadAllText(service.LastDisconnectReportPath);
        Check(text.Contains("Failed") && !text.Contains("UserId") && !text.Contains("AppId") && !text.Contains("Token") && !text.Contains("RoomName"), "Bounded diagnostics record failure without account, room or authentication secrets");
        yield return Wait(() => PhotonNetwork.NetworkClientState == ClientState.Disconnected || PhotonNetwork.NetworkClientState == ClientState.ConnectedToMasterServer, "Transport shutdown", 10);
        Status = "Passed";
    }
    static void Chat(string text) => guest.OpRaiseEvent(LobbyRules.ChatEvent, text, new RaiseEventOptions { Receivers = ReceiverGroup.All }, SendOptions.SendReliable);
    static IEnumerator DropAndRecover(string label, int actor, int viewId, int seed, int handle, bool captured = false)
    {
        Status = label + " packet loss"; Loss(true);
        yield return Wait(() => service.IsRecovering, label + " timeout detected", 40);
        Check(MultiplayerLobbyService.IsGameplayRecovering && GameInput.Instance.GameplayInputBlocked && SceneManager.GetActiveScene().handle == handle,
            label + ": reconnect UI blocks input while preserving the scene");
        Check(ArchiveTopicSession.Current.State != null && !ArchiveTopicSession.Current.IsAuthority && !ArchiveTopicSession.Current.hunting.IsAuthority,
            label + ": disconnected gameplay never becomes offline authority");
        ScreenCapture.CaptureScreenshot(Folder + "/" + label.Replace(' ', '-') + "-reconnecting.png");
        Loss(false);
        yield return Wait(() => !service.IsRecovering && PhotonNetwork.InRoom && Player.LocalInstance != null, label + " recovery completes", 35);
        yield return .6f;
        Check(PhotonNetwork.LocalPlayer.ActorNumber == actor && Player.LocalInstance.GetComponent<PhotonView>().ViewID == viewId,
            label + ": cached avatar and original actor identity return");
        var session = ArchiveTopicSession.Current;
        Check(SceneManager.GetActiveScene().handle == handle && session.State.seed == seed && session.State.stations[0].round == 1 &&
            session.State.mistakes.Any(m => m.actor == actor && m.reason == "Recovery QA fixture") && session.hunting.health.ActorHearts(actor) == 2,
            label + ": same scene, solved progress, mistake attribution and two hearts survive");
        Check(!GameInput.Instance.GameplayInputBlocked && UnityEngine.Object.FindObjectsByType<MultiplayerAvatarSync>(FindObjectsSortMode.None).Count(a => a.GetComponent<PhotonView>().OwnerActorNr == actor) == 1,
            label + ": exactly one owned avatar regains controls");
        var bootstrap = UnityEngine.Object.FindFirstObjectByType<LogicLegends.Tests.Khezu.KhezuMultiplayerTestSession>();
        Check(bootstrap.exploration.Explorer == Player.LocalInstance && !bootstrap.offlineExplorer.activeSelf,
            label + ": existing exploration binds the recovered network avatar");
        Check(Player.LocalInstance.IsMovementLocked == captured, label + ": original capture restrictions are restored without adding a free movement window");
        Check(File.ReadAllText(service.LastDisconnectReportPath).Contains("Recovered"), label + ": diagnostic report records successful recovery");
    }
    static void Save()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Folder + "/checks.json", JsonUtility.ToJson(report, true));
        File.WriteAllText(Folder + "/status.txt", Status);
    }
    static void PlayState(PlayModeStateChange state)
    { if (state == PlayModeStateChange.ExitingPlayMode && active) { report.errors.Add("Play Mode interrupted"); Finish(false); } }
    static void Finish(bool success)
    {
        active = false; Loss(false); Time.timeScale = 1; stack.Clear();
        EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= PlayState; Application.logMessageReceived -= Log;
        guest?.Disconnect(); guest?.Service(); guest = null;
        if (heartbeat != null) { heartbeat.StopFallbackSendAckThread(); UnityEngine.Object.Destroy(heartbeat.gameObject); heartbeat = null; }
        report.passed = success && report.errors.Count == 0 && report.checks.All(c => c.StartsWith("PASS "));
        Status = report.passed ? "Passed" : "Failed"; Save();
        if (!success && MultiplayerLobbyService.Instance != null) MultiplayerLobbyService.Instance.LeaveLobby();
        Debug.Log("Connection recovery checks: " + Status + ", " + report.checks.Count + " checks.");
    }
}
