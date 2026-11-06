using System;
using System.Collections;
using System.Linq;
using ExitGames.Client.Photon;
using LogicLegends.Tests.Monsters;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static partial class ArchiveTopicNetworkChecks
    {
        // Called last: returning to the retained party deliberately destroys the
        // finished gameplay scene, then confirms a fresh second Start flow.
        static IEnumerator RematchPartyFlow(Peer requester)
        {
            Status="Retained party rejects active-match return requests";
            var service=MultiplayerLobbyService.EnsureExists();
            string room=PhotonNetwork.CurrentRoom.Name,oldMatch=session.State.matchId;
            int[] actors=PhotonNetwork.PlayerList.Select(p=>p.ActorNumber).OrderBy(a=>a).ToArray();
            int beforeEpoch=service.PartyReturnEpoch;
            var previous=(DevelopmentMonster)(int)PhotonNetwork.CurrentRoom.CustomProperties[MultiplayerMonsterRotation.RoomKey];
            void Request(string match)=>requester.client.OpRaiseEvent(MultiplayerLobbyService.PartyReturnRequestEvent,match,
                new RaiseEventOptions{Receivers=ReceiverGroup.MasterClient},SendOptions.SendReliable);
            Request(oldMatch);yield return .25f;
            Check(service.PartyReturnEpoch==beforeEpoch&&MultiplayerSceneRoutes.IsGameplay(SceneManager.GetActiveScene()),
                "A guest cannot return an unfinished match to the party");
            session.suppressDefeatForVerification=true;
            if(session.worldProgression!=null)session.worldProgression.suppressThreatsForVerification=true;
            // Explicit terminal fixture; no real academic completion is claimed.
            session.State.exit.won=true;session.State.exit.wonAt=ArchiveTopicSession.Clock;
            CommitLearningFixture();
            yield return Wait(()=>PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ArchiveTopicSession.CheckpointKey,out var raw)&&raw is string json&&
                JsonUtility.FromJson<ArchiveTeamState>(json)?.exit?.won==true,"Terminal host checkpoint exists before party return",8);
            Check(!service.MaySpawnGameplayAvatar,"A terminal match blocks a delayed profile coroutine from spawning another avatar");
            Status="One guest return keeps the same room and party";
            Request(oldMatch);
            yield return Wait(()=>SceneManager.GetActiveScene().name=="Main Menu"&&service.RetainingPartyLobby,
                "Host synchronizes retained Main Menu party",25);
            yield return Wait(()=>UnityEngine.Object.FindFirstObjectByType<MultiplayerMenuManager>()?.LobbyUI?.IsOpen==true,
                "Existing party lobby UI reopens",8);
            int epoch=service.PartyReturnEpoch;
            Check(PhotonNetwork.InRoom&&PhotonNetwork.CurrentRoom.Name==room&&epoch==beforeEpoch+1&&
                PhotonNetwork.PlayerList.Select(p=>p.ActorNumber).OrderBy(a=>a).SequenceEqual(actors),
                "Return preserves the Photon room, invite code and original actor identities");
            Check(peers.Where(p=>p.client.InRoom).All(p=>p.client.CurrentRoom.Name==room)&&service.RoomState==LobbyRules.Waiting&&PhotonNetwork.CurrentRoom.IsOpen,
                "Independent peers stay in the same reopened waiting party");
            Check(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(l=>l.isActiveAndEnabled)==1,
                "Retained Main Menu has one active audio listener, including preview cameras");
            var lobby=UnityEngine.Object.FindFirstObjectByType<MultiplayerMenuManager>().LobbyUI;
            lobby.Briefing.Dismiss();
            yield return .15f;
            var how=lobby.Briefing.EntryButton.GetComponent<RectTransform>();
            Rect Bounds(RectTransform rect){var corners=new Vector3[4];rect.GetWorldCorners(corners);return Rect.MinMaxRect(corners.Min(c=>c.x),corners.Min(c=>c.y),corners.Max(c=>c.x),corners.Max(c=>c.y));}
            var footer=how.parent.GetComponentsInChildren<UnityEngine.UI.Button>(true).Where(button=>button.gameObject.activeInHierarchy&&
                (button.name=="ToggleReady"||button.name=="StartGame"||button.name=="LeaveLobby")).ToArray();
            bool overlaps=footer.Any(button=>Bounds(how).Overlaps(Bounds((RectTransform)button.transform)));
            Check(footer.Length==3&&lobby.Briefing.EntryButton.gameObject.activeInHierarchy&&!overlaps,"HOW TO PLAY remains accessible without overlapping ready, start or leave controls");
            int host=PhotonNetwork.LocalPlayer.ActorNumber;
            PhotonNetwork.SetMasterClient(PhotonNetwork.CurrentRoom.GetPlayer(requester.Actor));
            yield return Wait(()=>!PhotonNetwork.IsMasterClient,"Retained lobby transfers host authority");
            requester.client.CurrentRoom.IsOpen=false;
            yield return Wait(()=>!PhotonNetwork.CurrentRoom.IsOpen,"Replacement-host closed-room fixture arrives");
            requester.client.CurrentRoom.SetMasterClient(requester.client.CurrentRoom.GetPlayer(host));
            yield return Wait(()=>PhotonNetwork.IsMasterClient&&PhotonNetwork.CurrentRoom.IsOpen,"Returning host reopens the retained waiting party");
            Check(service.PartyReturnEpoch==epoch&&PhotonNetwork.CurrentRoom.Name==room,"Lobby host handoff reopens the same party without a second return epoch");
            Check(!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ArchiveTopicSession.CheckpointKey,out var checkpoint)||checkpoint==null,
                "Finished topic, timer, inventory and capture checkpoint is cleared before the next match");
            Check(!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(MultiplayerLobbyService.AvatarCheckpointKey,out var avatars)||avatars==null,
                "Old avatar recovery anchors cannot be reused by the next match");
            Check((int)PhotonNetwork.CurrentRoom.CustomProperties[MultiplayerMonsterRotation.RoomKey]==(int)(previous==DevelopmentMonster.Rajang?DevelopmentMonster.Khezu:DevelopmentMonster.Rajang),
                "Retained party selects the opposite starting guardian once without starting automatically");
            Request(oldMatch);yield return .2f;
            Check(service.PartyReturnEpoch==epoch,"Replayed return requests do not reset the party or alternate the guardian twice");
            Check(!service.MaySpawnGameplayAvatar,"The retained waiting lobby cannot spawn gameplay avatars");
            // Reproduce a teammate's old Instantiate arriving after the first
            // cleanup. Its subsequent ready-epoch acknowledgement is the barrier
            // that lets ConfirmStart safely clear this late cache entry.
            int lateView=requester.Actor*PhotonNetwork.MAX_VIEW_IDS+17;
            requester.client.OpRaiseEvent(202,new Hashtable{[(byte)0]="Multiplayer_Female",[(byte)1]=Vector3.zero,
                [(byte)6]=PhotonNetwork.ServerTimestamp,[(byte)7]=lateView},
                new RaiseEventOptions{Receivers=ReceiverGroup.Others,CachingOption=EventCaching.AddToRoomCache},SendOptions.SendReliable);
            yield return Wait(()=>PhotonView.Find(lateView)!=null,"Late old avatar fixture reaches the retained lobby",8);
            yield return Wait(()=>PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(MultiplayerLobbyService.ReadyEpochKey,out var ack)&&ack is int value&&value==epoch,
                "Rendered host acknowledges the new lobby epoch",8);
            service.ToggleReady();
            foreach(var peer in peers.Where(p=>p.client.InRoom))peer.client.LocalPlayer.SetCustomProperties(new Hashtable{
                [LobbyRules.ReadyKey]=true,[MultiplayerLobbyService.ReadyEpochKey]=Math.Max(0,epoch-1)});
            yield return Wait(()=>PhotonNetwork.PlayerList.All(LobbyRules.IsReady),"All old ready flags are visible",8);
            Check(!service.CanStart,"Stale ready flags cannot bypass arriving at the new party lobby");
            foreach(var peer in peers.Where(p=>p.client.InRoom))peer.client.LocalPlayer.SetCustomProperties(new Hashtable{
                [LobbyRules.ReadyKey]=true,[MultiplayerLobbyService.ReadyEpochKey]=epoch});
            yield return Wait(()=>service.CanStart,"Every actor explicitly readies in the new lobby epoch",8);
            Status="Confirmed rematch starts a fresh scene and checkpoint";
            service.StartGame();
            yield return Wait(()=>MultiplayerSceneRoutes.IsGameplay(SceneManager.GetActiveScene())&&ArchiveTopicSession.Current!=null&&
                ArchiveTopicSession.Current.Ready&&Player.LocalInstance!=null,"Fresh rematch gameplay loads",35);
            session=ArchiveTopicSession.Current;
            Check(service.MaySpawnGameplayAvatar&&PhotonView.Find(lateView)==null&&
                UnityEngine.Object.FindObjectsByType<Player>(FindObjectsSortMode.None).Count(p=>p.GetComponent<PhotonView>()?.IsMine==true)==1,
                "Confirmed rematch clears a late old avatar and creates exactly one owned player");
            var probe=Connect("Rematch cache late join QA");bool replayedOldAvatar=false;
            probe.client.EventReceived+=ev=>{if(ev.Code==202&&ev.CustomData is Hashtable packet&&packet.TryGetValue((byte)7,out var id)&&id is int view&&view==lateView)replayedOldAvatar=true;};
            yield return Wait(()=>probe.client.State==ClientState.ConnectedToMasterServer,"Rematch cache probe connected");
            probe.client.OpJoinRoom(new EnterRoomParams{RoomName=room});
            yield return Wait(()=>probe.client.InRoom&&probe.state?.matchId==session.State.matchId,"Rematch late join receives fresh checkpoint",8);
            yield return .25f;
            Check(!replayedOldAvatar,"A new peer does not replay the delayed finished-match avatar from the room cache");
            var selector=UnityEngine.Object.FindFirstObjectByType<DevelopmentMonsterSelection>();
            Check(selector!=null&&selector.Selected==(DevelopmentMonster)(int)PhotonNetwork.CurrentRoom.CustomProperties[MultiplayerMonsterRotation.RoomKey]&&selector.Selected!=previous,
                "Fresh scene actually activates the opposite guardian rig for the rematch");
            Check(PhotonNetwork.CurrentRoom.Name==room&&session.State.matchId!=oldMatch&&session.State.stations.All(s=>s.phase==0&&s.round==0)&&
                session.State.mistakes.Count==0&&session.State.matchTime.awardedTopics.Count==0&&!session.TeamWon&&!session.TeamDefeated,
                "Confirmed Start preserves the party while creating fresh topics, mistakes, awards and match identity");
            Check(session.hunting.health.ActorHearts(PhotonNetwork.LocalPlayer.ActorNumber)==session.hunting.health.MaximumHealth&&
                session.State.survival.players.Count==0&&session.State.survival.downed.Count==0,
                "Second match starts with full health, empty inventories and no old downed players");
            string nextMatch=session.State.matchId;Request(oldMatch);yield return .25f;
            Check(session.State.matchId==nextMatch&&service.PartyReturnEpoch==epoch&&MultiplayerSceneRoutes.IsGameplay(SceneManager.GetActiveScene()),
                "An old terminal request cannot return or reset the new active match");
            Status="Explicit Leave still leaves independently";
            service.LeaveLobby();
            yield return Wait(()=>!PhotonNetwork.InRoom&&SceneManager.GetActiveScene().name=="Main Menu","Host explicitly leaves retained party",15);
            Check(requester.client.InRoom&&requester.client.CurrentRoom.Name==room,
                "Explicit Leave exits only its actor and does not force the remaining party to leave");
        }
    }
}
