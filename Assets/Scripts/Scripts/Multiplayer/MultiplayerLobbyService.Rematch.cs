using System;
using System.Collections;
using System.Linq;
using ExitGames.Client.Photon;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.OpenArchive.Topics;
using LogicLegends.Tests.Monsters;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public sealed partial class MultiplayerLobbyService
{
    public const string PartyReturnEpochKey="ll.party-return.v1",PartyReturnMatchKey="ll.party-return.match",ReadyEpochKey="ll.ready-epoch";
    public const byte PartyReturnRequestEvent=79;
    public int PartyReturnEpoch => InLobby&&PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(PartyReturnEpochKey,out var value)&&value is int epoch?Math.Max(0,epoch):0;
    public bool RetainingPartyLobby => InLobby&&RoomState==LobbyRules.Waiting&&PartyReturnEpoch>0;
    // Check again after asynchronous profile loading: a finished match must never
    // add a late avatar to the room cache while its party is returning.
    public bool MaySpawnGameplayAvatar => !leaving&&!IsRecovering&&!requestedPartyReturn&&!partyReturnOperationPending&&
        (PhotonNetwork.OfflineMode||InLobby&&RoomState==LobbyRules.Loading&&!GameplayFinishedForSpawn());
    public bool PartyReadyEpochConfirmed => !InLobby||PartyReturnEpoch==0||PhotonNetwork.PlayerList.All(p=>
        p.CustomProperties.TryGetValue(ReadyEpochKey,out var value)&&value is int epoch&&epoch==PartyReturnEpoch);
    bool requestedPartyReturn,partyLoadQueued,partyReturnOperationPending;
    int observedPartyEpoch;
    string observedPartyRoom,requestedPartyMatch;
    float nextPartyRequest;
    Coroutine partyLobbyRoutine;

    bool TryRequestRetainedPartyReturn()
    {
        if(IsRecovering)
        {
            var retained=ArchiveTopicSession.Current;
            if(retained!=null&&retained.Ready&&(retained.TeamWon||retained.TeamDefeated))
            {requestedPartyReturn=true;requestedPartyMatch=retained.State.matchId;}
            return true;
        }
        if(!InLobby||leaving)return false;
        if(RetainingPartyLobby){TickRetainedPartyReturn();return true;}
        string match;
        if(!TryReadFinishedMatch(out match))
        {
            // A reliable result event can arrive just before its room-property
            // checkpoint. Queue that same terminal match rather than leaving it.
            var session=ArchiveTopicSession.Current;
            if(session!=null&&session.Ready&&(session.TeamWon||session.TeamDefeated))match=session.State.matchId;
            else{Notify("Finish the current match before returning the whole party. Use Leave to exit on your own.");return true;}
        }
        requestedPartyReturn=true;requestedPartyMatch=match;nextPartyRequest=0;
        TickRetainedPartyReturn();return true;
    }
    bool GameplayFinishedForSpawn()
    {
        var current=ArchiveTopicSession.Current;
        return current!=null&&current.Ready&&(current.TeamWon||current.TeamDefeated)||
            TryReadFinishedMatch(out _,requireSessionAgreement:false);
    }
    bool TryReadFinishedMatch(out string match,bool requireSessionAgreement=true)
    {
        match=null;
        if(!InLobby||!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ArchiveTopicSession.CheckpointKey,out var raw)||!(raw is string json)||json.Length>262144)return false;
        try
        {
            var state=JsonUtility.FromJson<ArchiveTeamState>(json);
            if(state==null||state.version!=1||state.stations==null||state.stations.Length!=10||string.IsNullOrEmpty(state.matchId)||!(state.exit!=null&&state.exit.won)&&!(state.survival!=null&&state.survival.defeated))return false;
            var current=ArchiveTopicSession.Current;
            if(requireSessionAgreement&&current!=null&&current.Ready&&(current.State.matchId!=state.matchId||!current.TeamWon&&!current.TeamDefeated))return false;
            match=state.matchId;return true;
        }
        catch(ArgumentException){return false;}
    }
    void TickRetainedPartyReturn()
    {
        if(IsRecovering)return;
        if(!InLobby||leaving){ResetPartyReturnPresentation();return;}
        if(requestedPartyReturn&&!RetainingPartyLobby&&Time.unscaledTime>=nextPartyRequest)
        {
            nextPartyRequest=Time.unscaledTime+1;
            if(PhotonNetwork.IsMasterClient)BeginRetainedPartyReturn(requestedPartyMatch);
            else PhotonNetwork.RaiseEvent(PartyReturnRequestEvent,requestedPartyMatch,new RaiseEventOptions{Receivers=ReceiverGroup.MasterClient},SendOptions.SendReliable);
        }
        if(!RetainingPartyLobby){partyLoadQueued=false;return;}
        requestedPartyReturn=false;
        partyReturnOperationPending=false;
        if(SceneManager.GetActiveScene().name!="Main Menu")
        {
            FreezeFinishedMatchForPartyReturn();
            // The Master publishes Main Menu through PUN. A recovering guest may
            // also finish that already-published load if its old scene survived.
            if(PhotonNetwork.IsMasterClient||MainMenuWasSynchronized())LoadRetainedMainMenu();
            return;
        }
        string room=PhotonNetwork.CurrentRoom.Name;int epoch=PartyReturnEpoch;
        // A guest may finish loading before the departing Master. The replacement
        // host still reopens this waiting room even if it already acknowledged it.
        if(PhotonNetwork.IsMasterClient&&!PhotonNetwork.CurrentRoom.IsOpen)PhotonNetwork.CurrentRoom.IsOpen=true;
        if(observedPartyRoom==room&&observedPartyEpoch==epoch)return;
        observedPartyRoom=room;observedPartyEpoch=epoch;partyLoadQueued=false;
        committed=false;Busy=false;deadline=0;pending=Request.None;onlineSession=true;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable{[LobbyRules.ReadyKey]=false,[ReadyEpochKey]=epoch});
        RememberRecoveryRoom();
        if(PhotonNetwork.IsMasterClient)PhotonNetwork.CurrentRoom.IsOpen=true;
        Notify("Your party is together. Mark ready again for the next match; the other guardian will start.");
        if(partyLobbyRoutine!=null)StopCoroutine(partyLobbyRoutine);
        partyLobbyRoutine=StartCoroutine(OpenRetainedPartyLobby(room,epoch));
    }
    bool HandlePartyReturnEvent(EventData data)
    {
        if(data.Code!=PartyReturnRequestEvent)return false;
        if(!InLobby||!PhotonNetwork.IsMasterClient||leaving||!(data.CustomData is string match)||match.Length>80)return true;
        var member=PhotonNetwork.CurrentRoom.GetPlayer(data.Sender);
        if(member==null||member.IsInactive||RetainingPartyLobby)return true;
        if(TryReadFinishedMatch(out var current)&&current==match)BeginRetainedPartyReturn(match);
        return true;
    }
    bool BeginRetainedPartyReturn(string expectedMatch)
    {
        if(!InLobby||!PhotonNetwork.IsMasterClient||leaving||partyReturnOperationPending||RetainingPartyLobby||!TryReadFinishedMatch(out var current)||current!=expectedMatch)return false;
        var room=PhotonNetwork.CurrentRoom;
        if(!MultiplayerMonsterRotation.TryRead(room.CustomProperties,out var previous))return false;
        var changes=new Hashtable{
            [PartyReturnEpochKey]=PartyReturnEpoch+1,[PartyReturnMatchKey]=current,
            [LobbyRules.StateKey]=LobbyRules.Waiting,
            [MultiplayerMonsterRotation.RoomKey]=(int)(previous==DevelopmentMonster.Rajang?DevelopmentMonster.Khezu:DevelopmentMonster.Rajang),
            [ArchiveTopicSession.CheckpointKey]=null,[AvatarCheckpointKey]=null
        };
        foreach(var key in room.CustomProperties.Keys.OfType<string>())
            if(key.StartsWith(KhezuPhotonSync.Channel,StringComparison.Ordinal)&&key.EndsWith(".snapshot",StringComparison.Ordinal))changes[key]=null;
        // PUN queues cache removal, object destruction and this CAS on the same
        // reliable channel. Clear first so this Master's cleanup precedes its
        // Waiting transaction. ConfirmStart also clears late teammate events
        // after every actor acknowledges arrival in the new lobby epoch.
        // If the Master leaves before publication, the terminal checkpoint stays
        // available and its successor can safely repeat the idempotent cleanup.
        FreezeFinishedMatchForPartyReturn();
        PhotonNetwork.DestroyAll();
        bool sent=room.SetCustomProperties(changes,new Hashtable{[LobbyRules.StateKey]=room.CustomProperties[LobbyRules.StateKey]});
        if(!sent){Busy=false;Notify("Could not return the party yet. Please try again.");return false;}
        partyReturnOperationPending=true;
        room.IsOpen=false;Busy=true;deadline=0;
        return true;
    }
    static void FreezeFinishedMatchForPartyReturn()
    {
        FindFirstObjectByType<MultiplayerGameChat>()?.Close();
        FindFirstObjectByType<LevelMenu>()?.ClosePause();
        var session=ArchiveTopicSession.Current;if(session!=null){session.CaptureMatchReward();session.enabled=false;}
        foreach(var monster in KhezuHuntingController.ActiveMonsters.ToArray())
        {
            var sync=monster.GetComponent<KhezuPhotonSync>();if(sync!=null)sync.enabled=false;
            monster.GetComponent<KhezuCinematicJumpscareController>()?.Cancel();
            if(monster.IsAuthority){monster.EndRescueCarry(monster.RescueActor);monster.HoldTranquilized(false);monster.Request(KhezuCommand.Sleep);}
            monster.enabled=false;
        }
        Time.timeScale=1;
    }
    bool MainMenuWasSynchronized()
    {
        if(!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("curScn",out var scene))return false;
        int index=SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/Main Menu.unity");
        return scene is int id&&id==index||scene is string name&&(name=="Main Menu"||name=="Assets/Scenes/Main Menu.unity");
    }
    void LoadRetainedMainMenu()
    {
        if(partyLoadQueued||!PhotonNetwork.IsMessageQueueRunning)return;
        partyLoadQueued=true;PhotonNetwork.AutomaticallySyncScene=true;
        int index=SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/Main Menu.unity");
        if(index>=0)PhotonNetwork.LoadLevel(index);else PhotonNetwork.LoadLevel("Main Menu");
    }
    IEnumerator OpenRetainedPartyLobby(string room,int epoch)
    {
        yield return null;
        if(InLobby&&!leaving&&RetainingPartyLobby&&PhotonNetwork.CurrentRoom.Name==room&&PartyReturnEpoch==epoch)
            FindFirstObjectByType<MultiplayerMenuManager>()?.LobbyUI?.Open();
        partyLobbyRoutine=null;
    }
    void ResetPartyReturnPresentation()
    {
        requestedPartyReturn=false;requestedPartyMatch=null;partyLoadQueued=partyReturnOperationPending=false;
        observedPartyRoom=null;observedPartyEpoch=0;
        if(partyLobbyRoutine!=null){StopCoroutine(partyLobbyRoutine);partyLobbyRoutine=null;}
    }
    bool RestoreRetainedPartyAfterRecovery()
    {
        if(!RetainingPartyLobby)return false;
        recovering=restoring=false;Busy=false;deadline=0;onlineSession=true;committed=false;
        EndRecoveryPresentation();TickRetainedPartyReturn();
        RecordRecoveryResult(true);return true;
    }
}
