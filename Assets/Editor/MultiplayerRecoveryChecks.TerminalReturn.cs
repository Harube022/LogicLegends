using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using ExitGames.Client.Photon;
using LogicLegends.OpenArchive.Topics;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>Live transport regression for a terminal return requested during a real SDK outage.</summary>
public static partial class MultiplayerRecoveryChecks
{
    const string RotationPreference = "ll.multiplayer.next-monster.v1";
    static bool terminalRotationHadPreference;
    static int terminalRotationPreference;
    static bool terminalBriefingHadPreference;
    static int terminalBriefingPreference;

    [MenuItem("Logic Legends/Multiplayer/Run Terminal Return Recovery Check (Play Mode)")]
    public static void RunTerminalReturn()
    {
        if (!Application.isPlaying || SceneManager.GetActiveScene().name != "Main Menu" || PhotonNetwork.InRoom || active)
            throw new InvalidOperationException("Run from Main Menu in Play Mode, outside a room.");
        report = new Report { scope = "Rendered PUN client plus independent live Realtime peer. Explicit terminal fixture; 100% SDK loss while requesting return to the same party. No physical Android/carrier testing." };
        terminalRotationHadPreference = PlayerPrefs.HasKey(RotationPreference);
        terminalRotationPreference = PlayerPrefs.GetInt(RotationPreference);
        terminalBriefingHadPreference=PlayerPrefs.HasKey(MultiplayerMatchBriefing.SeenPreference);
        terminalBriefingPreference=PlayerPrefs.GetInt(MultiplayerMatchBriefing.SeenPreference);
        stack.Clear(); nextTick = 0; active = true; Status = "Starting terminal return recovery";
        Directory.CreateDirectory(Folder); Application.logMessageReceived += Log;
        EditorApplication.update += Tick; EditorApplication.update += RestoreTerminalRotationWhenFinished;
        EditorApplication.playModeStateChanged += PlayState; stack.Push(TerminalReturnFlow()); Save();
    }

    static void RestoreTerminalRotationWhenFinished()
    {
        if (active) return;
        EditorApplication.update -= RestoreTerminalRotationWhenFinished;
        if (terminalRotationHadPreference) PlayerPrefs.SetInt(RotationPreference, terminalRotationPreference);
        else PlayerPrefs.DeleteKey(RotationPreference);
        if(terminalBriefingHadPreference)PlayerPrefs.SetInt(MultiplayerMatchBriefing.SeenPreference,terminalBriefingPreference);
        else PlayerPrefs.DeleteKey(MultiplayerMatchBriefing.SeenPreference);
        PlayerPrefs.Save();
    }

    static IEnumerator TerminalReturnFlow()
    {
        var entry = UnityEngine.Object.FindFirstObjectByType<MultiplayerMenuManager>();
        UnityEngine.Object.FindFirstObjectByType<MainMenuManager>().ShowPlayMenu(); entry.OnClickMultiplayerButton();
        service = MultiplayerLobbyService.Instance; service.HostGame();
        yield return Wait(() => service.InLobby, "Terminal-return host room created");
        string room = PhotonNetwork.CurrentRoom.Name;
        int actor = PhotonNetwork.LocalPlayer.ActorNumber, epoch = service.PartyReturnEpoch;
        var settings = PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings());
        settings.AppVersion = PhotonNetwork.NetworkingClient.AppVersion; settings.FixedRegion = PhotonNetwork.CloudRegion;
        guest = new LoadBalancingClient { NickName = "Terminal return guest QA" };
        guest.LoadBalancingPeer.DisconnectTimeout = 60000;
        var keeper = new GameObject("Terminal return peer heartbeat"); UnityEngine.Object.DontDestroyOnLoad(keeper);
        heartbeat = keeper.AddComponent<ConnectionHandler>(); heartbeat.Client = guest;
        heartbeat.KeepAliveInBackground = 180000; heartbeat.StartFallbackSendAckThread();
        guest.ConnectUsingSettings(settings);
        yield return Wait(() => guest.State == ClientState.ConnectedToMasterServer, "Terminal-return guest connected");
        guest.OpJoinRoom(new EnterRoomParams { RoomName = room });
        yield return Wait(() => guest.InRoom && service.Players.Length == 2, "Terminal-return guest joined");
        int guestActor = guest.LocalPlayer.ActorNumber;
        guest.LocalPlayer.SetCustomProperties(new Hashtable { [LobbyRules.ReadyKey] = true });
        if (!LobbyRules.IsReady(PhotonNetwork.LocalPlayer)) service.ToggleReady();
        yield return Wait(() => service.CanStart, "Terminal-return party ready"); service.StartGame();
        yield return Wait(() => MultiplayerSceneRoutes.IsOpenArchive(SceneManager.GetActiveScene()) && Player.LocalInstance != null &&
            ArchiveTopicSession.Current != null && ArchiveTopicSession.Current.Ready, "Terminal-return Archive loaded", 70);
        yield return Wait(() => TeleportManager.Instance != null && !TeleportManager.Instance.IsTransitioning, "Terminal-return spawn fade complete");
        var session = ArchiveTopicSession.Current;
        session.suppressDefeatForVerification = true;
        if (session.worldProgression != null) session.worldProgression.suppressThreatsForVerification = true;
        int guestView = guestActor * PhotonNetwork.MAX_VIEW_IDS + 1;
        var spawner = UnityEngine.Object.FindFirstObjectByType<PlayerSpawner>();
        guest.OpRaiseEvent(202, new Hashtable { [(byte)0] = "Multiplayer_Female",
            [(byte)1] = spawner.ResolveSpawnPosition(Resources.Load<GameObject>("Multiplayer_Female"), guestActor),
            [(byte)6] = PhotonNetwork.ServerTimestamp, [(byte)7] = guestView },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others, CachingOption = EventCaching.AddToRoomCache }, SendOptions.SendReliable);
        yield return Wait(() => PhotonView.Find(guestView) != null, "Terminal-return guest avatar instantiated");
        yield return Wait(() => PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(MultiplayerLobbyService.AvatarCheckpointKey) &&
            PhotonNetwork.CurrentRoom.CustomProperties.Keys.OfType<string>().Any(k => k.EndsWith(".snapshot", StringComparison.Ordinal)),
            "Terminal-return avatar and encounter recovery checkpoints exist");
        yield return 1.3f;
        string match = session.State.matchId;
        session.State.survival.defeated = true; session.State.survival.defeatedAt = ArchiveTopicSession.Clock;
        session.State.survival.defeatReason = "timeout";
        session.State.rewards.adminUsed=true;
        session.State.mistakes.Add(new ArchiveMistake{actor=actor,topic=1,round=0,subphase=0,reviewVersion=1,reason="answer",reviewFeedback="Explicit terminal-return review QA fixture."});
        typeof(ArchiveTopicSession).GetMethod("Commit", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(session, null);
        yield return Wait(() => PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ArchiveTopicSession.CheckpointKey, out var raw) &&
            raw is string json && JsonUtility.FromJson<ArchiveTeamState>(json)?.survival?.defeated == true, "Terminal checkpoint published", 8);
        Check(session.TeamDefeated && !session.TeamWon, "Trusted terminal timeout fixture is published through the existing state owner");
        ArchiveLearningRecapUI.Observe(session);ArchiveLearningRecapUI.OpenLastReview();yield return .2f;
        string learningReview=JsonUtility.ToJson(ArchiveLearningRecapUI.Snapshot);
        Check(ArchiveLearningRecapUI.IsVisible&&ArchiveLearningRecapUI.Snapshot.actor==actor&&ArchiveLearningRecapUI.Snapshot.practice,
            "The terminal player opens a clearly marked personal practice review before returning");
        Status = "Terminal return requested during transport recovery";
        Loss(true); yield return Wait(() => service.IsRecovering, "Terminal-return SDK loss detected", 40);
        Check(!service.InLobby && GameInput.Instance.GameplayInputBlocked,
            "Real SDK loss leaves the terminal match outside the room with recovery owning input");
        service.ReturnToLobbyAfterMatch();
        var queued = typeof(MultiplayerLobbyService).GetField("requestedPartyReturn", BindingFlags.NonPublic | BindingFlags.Instance);
        var requestedMatch = typeof(MultiplayerLobbyService).GetField("requestedPartyMatch", BindingFlags.NonPublic | BindingFlags.Instance);
        Check((bool)queued.GetValue(service) && (string)requestedMatch.GetValue(service) == match,
            "Return during recovery queues the same terminal match instead of leaving the party");
        yield return .4f;
        Check(service.IsRecovering && !service.InLobby && (bool)queued.GetValue(service) && (string)requestedMatch.GetValue(service) == match,
            "Multiple disconnected gameplay frames preserve the pending terminal return");
        Loss(false);
        yield return Wait(() => !service.IsRecovering && PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer.ActorNumber == actor,
            "Terminal actor rejoined after SDK loss", 35);
        Check(PhotonNetwork.CurrentRoom.Name == room && guest.InRoom && guest.LocalPlayer.ActorNumber == guestActor,
            "Recovery preserves the original room and both actor identities");
        // The independent Realtime peer validates transport but does not run the
        // application's event-79 handler. Hand authority back only after proving
        // recovery; the queued request must then complete without a second click.
        if (!PhotonNetwork.IsMasterClient)
            guest.CurrentRoom.SetMasterClient(guest.CurrentRoom.GetPlayer(actor));
        yield return Wait(() => SceneManager.GetActiveScene().name == "Main Menu" && service.RetainingPartyLobby,
            "Recovered pending return automatically synchronizes the retained lobby", 30);
        yield return Wait(() => UnityEngine.Object.FindFirstObjectByType<MultiplayerMenuManager>()?.LobbyUI?.IsOpen == true,
            "Recovered retained party UI opens", 8);
        Check(PhotonNetwork.CurrentRoom.Name == room && PhotonNetwork.LocalPlayer.ActorNumber == actor &&
            PhotonNetwork.PlayerList.Select(p => p.ActorNumber).OrderBy(a => a).SequenceEqual(new[] { actor, guestActor }.OrderBy(a => a)),
            "Completed return keeps the same invite room and original party members");
        Check(service.PartyReturnEpoch == epoch + 1 && service.RoomState == LobbyRules.Waiting &&
            !LobbyRules.IsReady(PhotonNetwork.LocalPlayer) && !service.CanStart,
            "Recovered return advances one epoch and requires fresh readiness before another match");
        Check(!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ArchiveTopicSession.CheckpointKey, out var checkpoint) || checkpoint == null,
            "Recovered terminal state cannot leak into the next match");
        Check(!UnityEngine.Object.FindObjectsByType<MultiplayerRecoveryUI>(FindObjectsSortMode.None).Any(ui => ui.IsVisible),
            "Successful return removes the recovery overlay");
        Check(ArchiveTopicSession.Current==null&&ArchiveLearningRecapUI.IsVisible&&JsonUtility.ToJson(ArchiveLearningRecapUI.Snapshot)==learningReview,
            "The detached personal review survives actual gameplay unload and retained-party recovery without changing its records");
        ArchiveLearningRecapUI.Current.CloseReview();
        var returnedLobby=UnityEngine.Object.FindFirstObjectByType<MultiplayerMenuManager>().LobbyUI;
        returnedLobby.Briefing.Dismiss();yield return .2f;
        yield return Wait(()=>TeleportManager.Instance==null||!TeleportManager.Instance.IsTransitioning&&TeleportManager.Instance.FadeAlpha<=.001f,
            "The actual retained lobby finishes its screen transition before native interaction",12);
        var reviewButton=returnedLobby.LastReviewButton;
        Check(reviewButton!=null&&reviewButton.isActiveAndEnabled&&reviewButton.interactable,
            "The retained lobby offers Review Last Match after its optional briefing closes");
        var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,((RectTransform)reviewButton.transform).TransformPoint(((RectTransform)reviewButton.transform).rect.center))};
        var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count>0&&(hits[0].gameObject==reviewButton.gameObject||hits[0].gameObject.transform.IsChildOf(reviewButton.transform)),
            "The retained lobby review entry receives the real topmost native raycast (top="+(hits.Count==0?"none":hits[0].gameObject.name+" under "+hits[0].gameObject.transform.parent?.name)+")");
        UnityEngine.EventSystems.ExecuteEvents.Execute(reviewButton.gameObject,pointer,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);yield return .1f;
        Check(ArchiveLearningRecapUI.IsVisible&&PhotonNetwork.CurrentRoom.Name==room&&JsonUtility.ToJson(ArchiveLearningRecapUI.Snapshot)==learningReview,
            "Reopening the last review keeps the same party, actor and immutable records");
        ArchiveLearningRecapUI.Current.CloseReview();
        service.LeaveLobby();
        yield return Wait(() => !PhotonNetwork.InRoom && SceneManager.GetActiveScene().name == "Main Menu", "Terminal-return fixture leaves cleanly", 15);
        Status = "Passed terminal return recovery";
    }
}
