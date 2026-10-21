using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExitGames.Client.Photon;
using LogicLegends.OpenArchive.Topics;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>Exercises the saved Join UI and PUN scene synchronization as a non-host.</summary>
public static class MultiplayerArchiveGuestChecks
{
    static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
    static readonly List<string> checks = new List<string>();
    static LoadBalancingClient host;
    static ConnectionHandler heartbeat;
    static double nextTick;
    static bool active;
    public static string Status { get; private set; } = "Idle";

    [MenuItem("Logic Legends/Multiplayer/Run Open Archive Guest Checks (Play Mode)")]
    public static void Run()
    {
        if (!Application.isPlaying || SceneManager.GetActiveScene().name != "Main Menu" || PhotonNetwork.InRoom || active)
            throw new InvalidOperationException("Run from Main Menu in Play Mode outside a room.");
        checks.Clear(); stack.Clear(); nextTick = 0; active = true; Status = "Running";
        EditorApplication.update += Tick; EditorApplication.playModeStateChanged += PlayState;
        stack.Push(Flow()); Save();
    }
    static void Tick()
    {
        if (!active) return;
        try
        {
            EditorApplication.QueuePlayerLoopUpdate(); host?.Service();
            if (EditorApplication.timeSinceStartup < nextTick) return;
            if (stack.Count == 0) { Finish(true); return; }
            var routine = stack.Peek();
            if (!routine.MoveNext()) { stack.Pop(); return; }
            if (routine.Current is IEnumerator nested) stack.Push(nested);
            else nextTick = EditorApplication.timeSinceStartup + (routine.Current is float delay ? delay : 0);
        }
        catch (Exception error) { checks.Add("FAIL " + error.Message); Finish(false); }
    }
    static IEnumerator Wait(Func<bool> ready, string label, float seconds = 40)
    {
        double deadline = EditorApplication.timeSinceStartup + seconds;
        while (!ready())
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException(label);
            yield return .03f;
        }
    }
    static void Check(bool condition, string label)
    {
        checks.Add((condition ? "PASS " : "FAIL ") + label); Save();
        if (!condition) throw new InvalidOperationException(label);
    }
    static IEnumerator Flow()
    {
        var entry = UnityEngine.Object.FindFirstObjectByType<MultiplayerMenuManager>();
        UnityEngine.Object.FindFirstObjectByType<MainMenuManager>().ShowPlayMenu();
        entry.OnClickMultiplayerButton();
        var service = MultiplayerLobbyService.Instance;
        var settings = PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings());
        settings.AppVersion = LobbyRules.Protocol + "_" + PhotonNetwork.PunVersion;
        host = new LoadBalancingClient(); host.NickName = "Archive routing host QA";
        var keeper = new GameObject("Archive guest checks host heartbeat"); UnityEngine.Object.DontDestroyOnLoad(keeper);
        heartbeat = keeper.AddComponent<ConnectionHandler>(); heartbeat.Client = host; heartbeat.StartFallbackSendAckThread();
        host.ConnectUsingSettings(settings);
        yield return Wait(() => host.State == ClientState.ConnectedToMasterServer, "Independent host connects");
        string code = LobbyRules.NewCode();
        var checkpoint = new ArchiveTeamState { seed = 314159, stations = Enumerable.Range(0, 10).Select(i => new ArchiveStationState { topic = i }).ToArray() };
        host.OpCreateRoom(new EnterRoomParams { RoomName = code, RoomOptions = new RoomOptions {
            MaxPlayers = 4, IsVisible = false, IsOpen = true, PlayerTtl = 0, EmptyRoomTtl = 0,
            CustomRoomProperties = new Hashtable {
                [LobbyRules.VersionKey] = LobbyRules.Protocol, [LobbyRules.StateKey] = LobbyRules.Waiting,
                [LobbyRules.SceneKey] = MultiplayerSceneRoutes.GameplayPath,
                [ArchiveTopicSession.CheckpointKey] = JsonUtility.ToJson(checkpoint)
            } } });
        yield return Wait(() => host.InRoom, "Private host room created");
        host.LocalPlayer.SetCustomProperties(new Hashtable { [LobbyRules.ReadyKey] = true });
        entry.LobbyUI.ShowJoin(); entry.LobbyUI.InviteInput.text = "  " + code.ToLowerInvariant() + "  ";
        entry.LobbyUI.InviteInput.onSubmit.Invoke(entry.LobbyUI.InviteInput.text);
        yield return Wait(() => service.InLobby && service.Players.Length == 2, "Join UI reaches existing room");
        Check(!PhotonNetwork.IsMasterClient && !service.CanStart, "Invite submission joins as a non-host; guest cannot start");
        service.ToggleReady();
        yield return Wait(() => host.CurrentRoom.Players.Values.All(LobbyRules.IsReady), "Both clients ready");
        host.CurrentRoom.IsOpen = false;
        host.CurrentRoom.SetCustomProperties(new Hashtable { [LobbyRules.StateKey] = LobbyRules.Loading,
            ["curScn"] = SceneUtility.GetBuildIndexByScenePath(MultiplayerSceneRoutes.GameplayPath) });
        yield return Wait(() => MultiplayerSceneRoutes.IsOpenArchive(SceneManager.GetActiveScene()) && Player.LocalInstance != null, "PUN automatically loads Open Archive for guest", 70);
        yield return Wait(() => TeleportManager.Instance != null && !TeleportManager.Instance.IsTransitioning, "Guest startup fade finishes");
        yield return .5f;
        var player = Player.LocalInstance;
        var spawner = UnityEngine.Object.FindFirstObjectByType<PlayerSpawner>();
        var expected = spawner.ResolveSpawnPosition(Resources.Load<GameObject>("Multiplayer_Male"), PhotonNetwork.LocalPlayer.ActorNumber);
        var difference = player.transform.position - expected; difference.y = 0;
        Check(!PhotonNetwork.IsMasterClient && player.GetComponent<PhotonView>().IsMine && player.enabled, "Guest keeps owned local movement after automatic scene synchronization");
        Check(difference.magnitude < .1f && player.GetComponent<CharacterController>().isGrounded, "Guest lands in its own grounded plaza slot");
        Check(!UnityEngine.Object.FindFirstObjectByType<LogicLegends.Tests.Khezu.KhezuMultiplayerTestSession>().offlineExplorer.activeSelf, "Guest never enables a duplicate offline character");
        Check(GameObject.Find("CM_ThirdPersonCam").GetComponent<Unity.Cinemachine.CinemachineCamera>().Target.TrackingTarget == player.transform.Find("CameraTarget"), "Guest camera tracks its owned avatar");
        var session = ArchiveTopicSession.Current;
        Check(session.Ready && session.State.seed == 314159 && !session.IsAuthority, "Guest restores the existing host topic checkpoint without becoming authority");
        int handle = SceneManager.GetActiveScene().handle;
        host.CurrentRoom.SetCustomProperties(new Hashtable { ["oa.routing.qa"] = 1 });
        yield return 1.5f;
        Check(SceneManager.GetActiveScene().handle == handle && ArchiveTopicSession.Current == session && Player.LocalInstance == player,
            "Later room-property updates preserve the scene, topic session and avatar without reloading");
        Vector3 before = player.transform.position; GameInput.Instance.SetMobileMovement(Vector2.up);
        yield return .4f; GameInput.Instance.SetMobileMovement(Vector2.zero);
        Check(Vector3.Distance(before, player.transform.position) > .15f && !GameInput.Instance.GameplayInputBlocked, "Guest moves through the original shared controls");
        var chat = UnityEngine.Object.FindFirstObjectByType<MultiplayerGameChat>();
        chat.Open(); Check(chat.IsOpen && GameInput.Instance.GameplayInputBlocked, "Guest party chat opens with local input isolation");
        chat.Close(); Check(!GameInput.Instance.GameplayInputBlocked, "Closing guest chat restores controls");
        service.LeaveLobby();
        yield return Wait(() => !PhotonNetwork.InRoom && SceneManager.GetActiveScene().name == "Main Menu", "Guest leaves cleanly");
        Check(UnityEngine.Object.FindFirstObjectByType<MultiplayerGameChat>() == null, "Guest exit returns to Main Menu and removes scene chat");
    }
    static void Save()
    {
        Directory.CreateDirectory("Temp/ArchiveLobbyRouting");
        File.WriteAllText("Temp/ArchiveLobbyRouting/guest-checks.txt", string.Join("\n", checks) + "\nStatus: " + Status);
    }
    static void PlayState(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode && active) { checks.Add("FAIL Play Mode interrupted"); Finish(false); }
    }
    static void Finish(bool passed)
    {
        active = false; Status = passed ? "Passed" : "Failed"; stack.Clear();
        EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= PlayState;
        host?.Disconnect(); host?.Service(); host = null;
        if (heartbeat != null) { heartbeat.StopFallbackSendAckThread(); UnityEngine.Object.Destroy(heartbeat.gameObject); heartbeat = null; }
        GameInput.Instance?.SetMobileMovement(Vector2.zero); Save();
        if (!passed && MultiplayerLobbyService.Instance != null) MultiplayerLobbyService.Instance.LeaveLobby();
    }
}
