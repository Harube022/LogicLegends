using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public sealed partial class MultiplayerLobbyService
{
    public const string AvatarCheckpointKey = "ll.recovery.avatars.v1";
    [Serializable] public sealed class AvatarCheckpoint
    {
        public int actor, view;
        public Vector3 position;
        public Quaternion rotation;
    }
    [Serializable] public sealed class AvatarCheckpoints { public List<AvatarCheckpoint> avatars = new List<AvatarCheckpoint>(); }
    public bool IsRecovering => recovering;
    public int RecoveryAttempt => recoveryAttempt;
    public float RecoveryRemaining => recovering ? Mathf.Max(0, recoveryUntil - Time.realtimeSinceStartup) : 0;
    // During a transport transition callbacks may run before OnDisconnected. Never
    // let the retained online scene temporarily become an offline authority.
    public static bool IsGameplayRecovering => Instance != null && (Instance.onlineSession || !string.IsNullOrEmpty(Instance.recoveryTerminalNotice)) &&
        MultiplayerSceneRoutes.IsGameplay(SceneManager.GetActiveScene()) &&
        (Instance.recovering || !string.IsNullOrEmpty(Instance.recoveryTerminalNotice) || !PhotonNetwork.InRoom || !PhotonNetwork.IsConnectedAndReady);
    bool recovering, restoring, recoveryOwnsInput, recoveryPreviousInput, retryDisconnect, recoveryWasGameplay;
    int recoveryActor, recoveryAttempt;
    string recoveryRoom, recoveryUser, recoveryTerminalNotice;
    float recoveryUntil, nextRecoveryAttempt, nextAvatarCheckpoint;
    Coroutine restoreRoutine;
    GameInput recoveryInput;
    MultiplayerRecoveryUI recoveryUI;

    void InitializeConnectionRecovery() { PhotonNetwork.NetworkingClient.StateChanged += ObserveTransportState; }
    void DisposeConnectionRecovery()
    {
        PhotonNetwork.NetworkingClient.StateChanged -= ObserveTransportState;
        EndRecoveryPresentation();
    }
    void ConfigureOnlineTransport()
    {
        // PUN normally dispatches in FixedUpdate, which stops at timeScale zero.
        PhotonNetwork.MinimalTimeScaleToDispatchInFixedUpdate = 0;
        if (!PhotonNetwork.IsConnected && string.IsNullOrEmpty(PhotonNetwork.NetworkingClient.UserId))
            PhotonNetwork.AuthValues = new AuthenticationValues("ll-" + Guid.NewGuid().ToString("N"));
    }
    void ClearRecoverySession()
    {
        EndRecoveryPresentation(); recovering = restoring = false; recoveryAttempt = 0;
        retryDisconnect = recoveryWasGameplay = false;
        recoveryRoom = recoveryUser = recoveryTerminalNotice = null; recoveryActor = 0;
    }
    void RememberRecoveryRoom()
    {
        recoveryRoom = PhotonNetwork.CurrentRoom.Name;
        recoveryActor = PhotonNetwork.LocalPlayer.ActorNumber;
        recoveryUser = PhotonNetwork.NetworkingClient.UserId;
        nextAvatarCheckpoint = 0;
    }
    static bool Recoverable(DisconnectCause cause) => cause == DisconnectCause.ClientTimeout ||
        cause == DisconnectCause.ServerTimeout || cause == DisconnectCause.Exception ||
        cause == DisconnectCause.ReceiveException || cause == DisconnectCause.SendException || cause == DisconnectCause.ExceptionOnConnect ||
        cause == DisconnectCause.DisconnectByServerReasonUnknown;
    bool ContinueOrBeginRecovery(DisconnectCause cause)
    {
        if (recovering)
        {
            if (retryDisconnect) { retryDisconnect = false; nextRecoveryAttempt = Time.realtimeSinceStartup + 2; return true; }
            if (!Recoverable(cause)) { RecoveryFailed(DisconnectExplanation(cause)); return true; }
            restoring = false; nextRecoveryAttempt = Time.realtimeSinceStartup + Mathf.Min(5, recoveryAttempt * 2);
            Notify("Connection interrupted. Reconnecting…"); return true;
        }
        var scene = SceneManager.GetActiveScene();
        if (!onlineSession || leaving || (!MultiplayerSceneRoutes.IsGameplay(scene) && scene.name != "Main Menu") ||
            string.IsNullOrEmpty(recoveryRoom) || recoveryActor <= 0 || !Recoverable(cause)) return false;
        recoveryWasGameplay = MultiplayerSceneRoutes.IsGameplay(scene);
        recovering = true; restoring = false; recoveryAttempt = 0;
        recoveryUntil = Time.realtimeSinceStartup + LobbyRules.RecoverySeconds;
        nextRecoveryAttempt = Time.realtimeSinceStartup + .5f; Busy = true; deadline = 0;
        FindFirstObjectByType<MultiplayerGameChat>()?.Close();
        FindFirstObjectByType<LevelMenu>()?.ClosePause();
        recoveryInput = GameInput.Instance;
        if (recoveryInput != null)
        {
            recoveryPreviousInput = recoveryInput.GameplayInputBlocked; recoveryOwnsInput = true;
            recoveryInput.SetMobileMovement(Vector2.zero); recoveryInput.SetGameplayInputBlocked(true);
        }
        MobileInputUI.FindJoystick()?.ResetJoystick(); MobileLookInput.ResetDelta();
        recoveryUI = GetComponent<MultiplayerRecoveryUI>();
        if (recoveryUI == null) recoveryUI = gameObject.AddComponent<MultiplayerRecoveryUI>();
        recoveryUI.Show(this);
        Notify("Connection interrupted. Reconnecting…"); return true;
    }
    void TickConnectionRecovery()
    {
        SampleConnectionDiagnostics();
        if (!recovering)
        {
            if (onlineSession && InLobby && MultiplayerSceneRoutes.IsGameplay(SceneManager.GetActiveScene()) &&
                PhotonNetwork.IsMasterClient && Time.unscaledTime >= nextAvatarCheckpoint)
            {
                nextAvatarCheckpoint = Time.unscaledTime + 1;
                SaveAvatarCheckpoints();
            }
            return;
        }
        // UI destruction during a synchronized scene change can release its own
        // older input block. Recovery continues to own the connection guard.
        GameInput.Instance?.SetGameplayInputBlocked(true);
        if (Time.realtimeSinceStartup >= recoveryUntil)
        { RecoveryFailed("Could not reconnect within 30 seconds. Check your connection, then host or join again."); return; }
        if (restoring || Time.realtimeSinceStartup < nextRecoveryAttempt) return;
        if (PhotonNetwork.NetworkClientState != ClientState.Disconnected) return;
        if (recoveryAttempt >= LobbyRules.RecoveryAttempts)
        { RecoveryFailed("Could not reconnect after several attempts. Check your connection, then host or join again."); return; }
        recoveryAttempt++; nextRecoveryAttempt = Time.realtimeSinceStartup + 2;
        ConfigureOnlineTransport(); PhotonNetwork.IsMessageQueueRunning = true;
        if (!PhotonNetwork.ReconnectAndRejoin() && !PhotonNetwork.Reconnect())
            nextRecoveryAttempt = Time.realtimeSinceStartup + Mathf.Min(5, recoveryAttempt * 2);
        Notify("Reconnecting… Attempt " + recoveryAttempt + " / " + LobbyRules.RecoveryAttempts);
    }
    void RejoinFromMaster()
    {
        if (Time.realtimeSinceStartup >= recoveryUntil) { RecoveryFailed("Your reconnect time expired."); return; }
        if (!PhotonNetwork.RejoinRoom(recoveryRoom)) RecoveryFailed("Could not resume the reserved player slot.");
    }
    void RecoveryJoinFailed(short code)
    {
        if (code == ErrorCode.JoinFailedFoundActiveJoiner)
        {
            retryDisconnect = true;
            PhotonNetwork.Disconnect();
            nextRecoveryAttempt = Time.realtimeSinceStartup + 2;
            return;
        }
        RecoveryFailed("The original match or reserved player slot is no longer available. Return to the lobby to play again.");
    }
    void AcceptRecoveredRoom()
    {
        var room = PhotonNetwork.CurrentRoom;
        if (room.Name != recoveryRoom || PhotonNetwork.LocalPlayer.ActorNumber != recoveryActor ||
            PhotonNetwork.NetworkingClient.UserId != recoveryUser ||
            !Equals(room.CustomProperties[LobbyRules.VersionKey], LobbyRules.Protocol) ||
            !MultiplayerMonsterRotation.TryRead(room.CustomProperties, out _) ||
            !Equals(room.CustomProperties[LobbyRules.SceneKey], TargetScene))
        { RecoveryFailed("Could not confirm your original player slot. Please join a new match."); return; }
        if (RoomState == LobbyRules.Waiting && SceneManager.GetActiveScene().name == "Main Menu")
        {
            recovering = restoring = false; Busy = false; deadline = 0; onlineSession = true; committed = false;
            EndRecoveryPresentation(); Notify("Reconnected to the same lobby. Your ready status was kept."); RecordRecoveryResult(true); return;
        }
        restoring = true; Notify("Connection restored. Restoring your player…");
        restoreRoutine = StartCoroutine(RestoreRecoveredPlayer());
    }
    IEnumerator RestoreRecoveredPlayer()
    {
        while (recovering && Time.realtimeSinceStartup < recoveryUntil && !MultiplayerSceneRoutes.IsGameplay(SceneManager.GetActiveScene())) yield return null;
        // PUN replays the original cached Instantiate event. Never instantiate a
        // second avatar: its actor/view identity is part of the encounter state.
        while (recovering && Time.realtimeSinceStartup < recoveryUntil && Player.LocalInstance == null) yield return null;
        if (!recovering) yield break;
        var player = Player.LocalInstance;
        var view = player != null ? player.GetComponent<PhotonView>() : null;
        if (view == null || view.OwnerActorNr != recoveryActor || !view.Owner.IsLocal)
        { RecoveryFailed("The original player could not be restored."); yield break; }
        yield return null;
        var topics = LogicLegends.OpenArchive.Topics.ArchiveTopicSession.Current;
        var sync = FindFirstObjectByType<LogicLegends.OpenArchive.Hunting.KhezuPhotonSync>();
        if (topics == null || !topics.RestoreRecoveryCheckpoint() || sync == null || !sync.RestoreRecoveryCheckpoint())
        { RecoveryFailed("The host's match checkpoint is unavailable. Please return to the lobby."); yield break; }
        var checkpoint = ReadAvatarCheckpoints().avatars.FirstOrDefault(a => a.actor == recoveryActor && a.view == view.ViewID);
        while (checkpoint == null && recovering && Time.realtimeSinceStartup < recoveryUntil)
        { yield return null; checkpoint = ReadAvatarCheckpoints().avatars.FirstOrDefault(a => a.actor == recoveryActor && a.view == view.ViewID); }
        if (checkpoint == null) { RecoveryFailed("Your saved player position is unavailable."); yield break; }
        var capsule = player.GetComponent<CharacterController>(); bool enabled = capsule.enabled;
        capsule.enabled = false; player.transform.SetPositionAndRotation(checkpoint.position, checkpoint.rotation); capsule.enabled = enabled;
        player.ResetMotionAfterTeleport();
        FindFirstObjectByType<PlayerSpawner>()?.BindRecoveredPlayer(player);
        FindFirstObjectByType<LogicLegends.Tests.Khezu.KhezuMultiplayerTestSession>()?.BindOwnedPlayer(player);
        recovering = restoring = false; Busy = false; deadline = 0; onlineSession = true; committed = RoomState == LobbyRules.Loading;
        topics.hunting.AuthorityChanged();
        topics.hunting.GetComponent<LogicLegends.OpenArchive.Cinematics.KhezuCaptureEncounterController>()?.SynchronizeBarrierMovement();
        TeleportCameraSync.ResetAfterMove(player.transform, Vector3.zero);
        restoreRoutine = null; EndRecoveryPresentation();
        Notify("Reconnected. Your player and team progress were restored.");
        RecordRecoveryResult(true);
    }
    void RecoveryFailed(string explanation)
    {
        recoveryTerminalNotice = explanation + " Reason: " + LastDisconnectCause + ".";
        RecordRecoveryResult(false); recovering = restoring = false; onlineSession = false;
        EndRecoveryPresentation();
        messages.Clear(); chatMessages.Clear(); lastReceived.Clear();
        Fail(recoveryTerminalNotice);
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom(false);
        else if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();
        StartCoroutine(ReturnAfterDisconnect());
    }
    void EndRecoveryPresentation()
    {
        if (restoreRoutine != null) { StopCoroutine(restoreRoutine); restoreRoutine = null; }
        if (recoveryUI != null) recoveryUI.Hide();
        if (recoveryOwnsInput && recoveryInput != null)
        {
            recoveryInput.SetMobileMovement(Vector2.zero);
            bool originalBlock = recoveryPreviousInput && (recoveryWasGameplay || !MultiplayerSceneRoutes.IsGameplay(SceneManager.GetActiveScene()));
            recoveryInput.SetGameplayInputBlocked(originalBlock);
        }
        recoveryOwnsInput = false; recoveryInput = null; MobileLookInput.ResetDelta();
    }
    AvatarCheckpoints ReadAvatarCheckpoints()
    {
        if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(AvatarCheckpointKey, out var value) && value is string json && json.Length < 8192)
        {
            try
            {
                var data = JsonUtility.FromJson<AvatarCheckpoints>(json);
                if (data != null && data.avatars != null && data.avatars.Count <= 4 && data.avatars.All(a => a != null &&
                    a.actor > 0 && a.view > 0 && Finite(a.position) && Finite(new Vector3(a.rotation.x, a.rotation.y, a.rotation.z)) &&
                    !float.IsNaN(a.rotation.w) && !float.IsInfinity(a.rotation.w))) return data;
            }
            catch (ArgumentException) { }
        }
        return new AvatarCheckpoints();
    }
    static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
        !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
    void SaveAvatarCheckpoints()
    {
        var data = ReadAvatarCheckpoints();
        data.avatars.RemoveAll(a => PhotonNetwork.CurrentRoom.GetPlayer(a.actor) == null);
        foreach (var avatar in FindObjectsByType<MultiplayerAvatarSync>(FindObjectsSortMode.None))
        {
            var view = avatar.GetComponent<PhotonView>();
            if (view.Owner == null) continue;
            var player = avatar.GetComponent<Player>();
            // Preserve authoritative capture/stun/death anchors even while the
            // owner's connection is absent; an inactive avatar never drives itself.
            if (view.Owner.IsInactive && (player == null || !player.IsMovementLocked)) continue;
            var record = data.avatars.FirstOrDefault(a => a.actor == view.OwnerActorNr);
            if (record == null) { record = new AvatarCheckpoint { actor = view.OwnerActorNr }; data.avatars.Add(record); }
            record.view = view.ViewID; record.position = player != null && player.IsMovementLocked ? player.MovementLockPosition : avatar.transform.position;
            record.rotation = avatar.transform.rotation;
        }
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [AvatarCheckpointKey] = JsonUtility.ToJson(data) });
    }
}
