using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using ExitGames.Client.Photon;
using LogicLegends.OpenArchive.Hunting;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static partial class ArchiveTopicNetworkChecks
    {
        static bool survivalOnly;
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Supplies And Rescue (Live Photon)")]
        public static void BeginSurvival() { StartChecks(false, survival: true); }
        static void SurvivalCommit()
        { typeof(ArchiveTopicSession).GetMethod("Commit", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, null); }
        static ArchiveCommand SurvivalCommand(Peer peer, int topic, ArchiveAction action, int item = -1, int target = 0)
        {
            var cmd = Command(peer, topic, action, item, target);
            cmd.matchId = session.State.matchId; cmd.contextVersion = 1;
            cmd.resetSerial = session.State.stations[topic].testResetSerial; return cmd;
        }
        static IEnumerator SurvivalSend(Peer peer, ArchiveCommand cmd, bool expected, string name)
        {
            peer.receipts.Remove(cmd.sequence); Packet(peer, cmd);
            yield return Wait(() => peer.receipts.ContainsKey(cmd.sequence), name + " receipt", 8);
            Check(peer.receipts[cmd.sequence].accepted == expected, name);
            if (expected) report.acceptedRemoteCommands++;
            yield return .12f;
        }
        static IEnumerator SurvivalMove(Peer peer, Vector3 position)
        {
            peer.client.OpRaiseEvent(201, new object[] { PhotonNetwork.ServerTimestamp, null,
                new object[] { peer.viewId, false, null, position, Quaternion.identity, false, false, false, 0f } },
                new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
            yield return Wait(() => session.ActorPlayer(peer.Actor) != null && Vector3.Distance(session.ActorPlayer(peer.Actor).transform.position, position) < .3f,
                "Remote rescuer reaches a real interaction position", 8);
        }
        static IEnumerator SurvivalPhoto(string name, ArchiveTopicStation station)
        {
            var chest = ArchiveRewardView.Current.ChestObject(station.topic);
            var cam = new GameObject("Survival_Reward_QA_Camera").AddComponent<Camera>();
            cam.CopyFrom(Camera.main); cam.depth = 100; cam.tag = "Untagged";
            Vector3 focus = chest.transform.position + Vector3.up * 1.4f;
            Vector3 offset = station.transform.forward * 11 + station.transform.right * 7 + Vector3.up * 5;
            cam.transform.SetPositionAndRotation(focus + offset, Quaternion.LookRotation(-offset));
            yield return .25f; Directory.CreateDirectory("Temp/ArchiveSurvival");
            ScreenCapture.CaptureScreenshot("Temp/ArchiveSurvival/" + name + ".png");
            yield return .25f; UnityEngine.Object.Destroy(cam.gameObject);
        }
        static IEnumerator SurvivalFlow(Peer a, string room)
        {
            report.scope = "Live invisible private Photon room: rendered Unity host plus independent Realtime peer. Actual five T1 rune answers earn a chest for the authenticated final solver. Validates ownership, distance, duplicate receipts, inventory checkpoints, medkit and two-player nest revival. Authority-only health/item/at-nest fixtures are explicitly used for consumable tests; actual carrying is covered by separate native tests. No physical Android or second rendered client is verified.";
            session.suppressDefeatForVerification = true;
            if (session.worldProgression != null) session.worldProgression.suppressThreatsForVerification = true;
            int host = PhotonNetwork.LocalPlayer.ActorNumber; var health = session.hunting.health;
            var station = session.stations[0]; session.hunting.Request(KhezuCommand.Sleep);
            Status = "Actual final-solver chest";
            for (int round = 0; round < 5; round++) yield return CrystalChoice(round == 1 || round == 4 ? a : null, round);
            var chest = session.Chest(0); var drop = chest.item;
            Check(session.State.stations[0].phase == 2 && chest.owner == a.Actor && !chest.claimed && (int)drop >= 0 && (int)drop < 6,
                "Real accepted final guest answer creates one reserved random item");
            Check(session.State.survival.chests.Count == 1, "A completed topic creates exactly one chest");
            yield return Wait(() => ArchiveRewardView.Current != null && ArchiveRewardView.Current.BoardHidden(0) && ArchiveRewardView.Current.ChestObject(0) != null,
                "Completed native boards disappear and actual chest appears", 8);
            Check(ArchiveRewardView.Current.BoardHidden(0), "Completed native question boards remain hidden after farewell FX");
            ArchiveTopicPlayChecks.MoveTo(station, ArchiveRewardView.ChestLocalPosition + Vector3.forward * 2);
            yield return SurvivalPhoto("Chest_Reserved", station);
            Check(!session.RequestChest(0) && !chest.claimed, "Host cannot steal the final guest solver's reserved chest");
            yield return Move(a, station, new Vector3(0, 0, 8));
            yield return SurvivalSend(a, SurvivalCommand(a, 0, ArchiveAction.ClaimChest), false, "Authenticated owner cannot claim from outside chest range");
            yield return Move(a, station, ArchiveRewardView.ChestLocalPosition + Vector3.right);
            var claim = SurvivalCommand(a, 0, ArchiveAction.ClaimChest);
            yield return SurvivalSend(a, claim, true, "Final solver claims through the real reliable command route");
            int inventoryTotal = session.Inventory(a.Actor).items.Sum();
            Check(chest.claimed && session.CountItem(a.Actor, drop) == 1 && inventoryTotal == (drop == ArchiveSurvivalItem.Tranquilizer ? 2 : 1),
                "One drop is granted once; reusable gun includes its documented first dart");
            Packet(a, claim); yield return .25f;
            Check(session.State.survival.chests.Count == 1 && session.Inventory(a.Actor).items.Sum() == inventoryTotal,
                "Duplicate reliable chest packet cannot grant another item");
            yield return SurvivalSend(a, SurvivalCommand(a, 0, ArchiveAction.ClaimChest), false, "A new claim command also rejects the opened chest");
            yield return Wait(() => a.state?.survival != null && a.state.survival.chests.Any(c => c.topic == 0 && c.claimed) && a.state.survival.players.Any(p => p.actor == a.Actor && p.items.Sum() == inventoryTotal),
                "Claim and private inventory reach the remote checkpoint");
            yield return .5f; yield return SurvivalPhoto("Chest_Claimed", station);
            var fake = JsonUtility.FromJson<ArchiveTeamState>(JsonUtility.ToJson(session.State)); fake.revision += 10000;
            fake.survival.players.First(p => p.actor == a.Actor).items[(int)ArchiveSurvivalItem.Medkit] += 99;
            a.client.OpRaiseEvent(ArchiveTopicSession.SnapshotEvent, new object[] { ArchiveTopicSession.Channel, JsonUtility.ToJson(fake) },
                new RaiseEventOptions { Receivers = ReceiverGroup.All }, SendOptions.SendReliable); yield return .2f;
            Check(session.Inventory(a.Actor).items.Sum() == inventoryTotal, "Guest cannot forge an inventory snapshot");

            Status = "Remote consumable authority fixtures";
            var supplies = session.Inventory(a.Actor); supplies.items[(int)ArchiveSurvivalItem.Medkit] = 2; supplies.items[(int)ArchiveSurvivalItem.RevivalKit] = 1;
            health.RegisterActors(new[] { host, a.Actor });
            Check(health.DamageAuthoritativeActor(a.Actor), "Authority fixture removes one guest heart through the real health owner");
            session.hunting.PublishRescueHealth(); SurvivalCommit();
            int beforeHearts = health.ActorHearts(a.Actor);
            yield return SurvivalSend(a, SurvivalCommand(a, 0, ArchiveAction.UseSurvivalItem, (int)ArchiveSurvivalItem.Medkit), true, "Remote medkit restores a missing heart");
            Check(health.ActorHearts(a.Actor) == beforeHearts + 1 && supplies.items[(int)ArchiveSurvivalItem.Medkit] == 1,
                "Medkit restores exactly one heart and consumes one item");
            yield return .5f;
            yield return SurvivalSend(a, SurvivalCommand(a, 0, ArchiveAction.UseSurvivalItem, (int)ArchiveSurvivalItem.Medkit), false,
                "Full-health medkit use is rejected");
            Check(supplies.items[(int)ArchiveSurvivalItem.Medkit] == 1, "Rejected full-health use preserves the medkit");

            // Confirmed-at-nest fixture: carrying/NavMesh delivery has a separate native test.
            Status = "Remote teammate rescue at nest";
            while (health.ActorHearts(host) > 0) Check(health.DamageAuthoritativeActor(host), "Authority fixture applies a real depletion heart");
            Vector3 anchor = session.hunting.monsterSpawnPoint.position + session.hunting.monsterSpawnPoint.right * 7 + Vector3.up * 1.1f;
            session.State.survival.downed.RemoveAll(d => d.actor == host);
            session.State.survival.downed.Add(new ArchiveDownedActor { actor = host, phase = 2, serial = 1, origin = anchor, anchor = anchor,
                createdAt = ArchiveTopicSession.Clock - 3, arrivedAt = ArchiveTopicSession.Clock - 2 });
            session.hunting.PublishRescueHealth(); SurvivalCommit(); yield return .25f;
            Check(!session.CanAct(host) && session.ActorPlayer(host).IsMovementLocked && session.CanAct(a.Actor),
                "Confirmed downed victim is movement-locked while the real remote rescuer stays free");
            yield return SurvivalSend(a, SurvivalCommand(a, 0, ArchiveAction.UseSurvivalItem, (int)ArchiveSurvivalItem.RevivalKit, host), false,
                "Revival kit cannot rescue a teammate across the map");
            yield return SurvivalMove(a, anchor + session.hunting.monsterSpawnPoint.forward * 2);
            var revive = SurvivalCommand(a, 0, ArchiveAction.UseSurvivalItem, (int)ArchiveSurvivalItem.RevivalKit, host);
            yield return SurvivalSend(a, revive, true, "Authenticated nearby teammate revives through the real item command");
            yield return .2f;
            Check(health.ActorHearts(host) == 1 && supplies.items[(int)ArchiveSurvivalItem.RevivalKit] == 0 && session.Downed(host) == null,
                "Revival restores one heart, closes the downed record and consumes exactly one kit");
            Check(session.CanAct(host) && !session.ActorPlayer(host).IsMovementLocked && health.IsActorProtected(host),
                "Revived player regains control and receives the six-second rescue protection");
            Check(!health.DamageAuthoritativeActor(host) && health.ActorHearts(host) == 1, "Immediate damage cannot bypass rescue protection");
            Packet(a, revive); yield return .2f;
            Check(health.ActorHearts(host) == 1 && supplies.items[(int)ArchiveSurvivalItem.RevivalKit] == 0,
                "Replayed revival command cannot heal twice or consume another kit");
            yield return Wait(() => a.state.survival.downed.Any(d => d.actor == host && d.phase == 3) && a.state.survival.players.First(p => p.actor == a.Actor).items[(int)ArchiveSurvivalItem.RevivalKit] == 0,
                "Rescue result and consumption checkpoint reach the remote rescuer");

            Status = "Late join and authority handoff";
            var b = Connect("Supplies late join QA"); yield return Wait(() => b.client.State == ClientState.ConnectedToMasterServer, "Late peer connects");
            b.client.OpJoinRoom(new EnterRoomParams { RoomName = room }); yield return Wait(() => b.client.InRoom && b.state?.survival != null, "Late peer joins with supply state");
            Check(b.state.survival.chests.Single(c => c.topic == 0).claimed && b.state.survival.chests.Single(c => c.topic == 0).owner == a.Actor &&
                b.state.survival.players.First(p => p.actor == a.Actor).items.SequenceEqual(supplies.items), "Late join retains exclusive owner, opened chest and exact inventory");
            PhotonNetwork.SetMasterClient(PhotonNetwork.CurrentRoom.GetPlayer(a.Actor));
            yield return Wait(() => !PhotonNetwork.IsMasterClient && a.client.CurrentRoom.MasterClientId == a.Actor, "Supply checkpoint authority transfer");
            a.client.CurrentRoom.SetMasterClient(a.client.CurrentRoom.GetPlayer(host)); yield return Wait(() => PhotonNetwork.IsMasterClient, "Rendered host regains supply authority"); yield return .3f;
            Check(session.Chest(0).claimed && session.Chest(0).owner == a.Actor && session.Inventory(a.Actor).items.SequenceEqual(supplies.items),
                "Host migration preserves the claim and consumable inventory without reroll");

            Status = "Permanent departure salvage fixture";
            int departedOwner=a.Actor; // Photon clears LocalPlayer.ActorNumber after leaving the room.
            session.State.stations[1].phase = 2;
            session.State.survival.chests.Add(new ArchiveTopicChest { topic = 1, owner = departedOwner, item = ArchiveSurvivalItem.Battery, completedAt = ArchiveTopicSession.Clock - 3 });
            SurvivalCommit();
            a.client.OpLeaveRoom(false); yield return Wait(() => PhotonNetwork.CurrentRoom.GetPlayer(departedOwner) == null, "Reward owner permanently leaves private room", 10);
            var orphan = session.Chest(1); orphan.ownerAbsentSince = ArchiveTopicSession.Clock - 40; SurvivalCommit(); yield return 1.1f;
            Check(!orphan.orphaned && orphan.owner == departedOwner, "Permanent departure keeps exclusive reservation through its grace period");
            orphan.ownerAbsentSince = ArchiveTopicSession.Clock - 46; SurvivalCommit();
            yield return Wait(() => session.Chest(1).orphaned, "Authority applies elapsed 45-second salvage fixture", 5);
            ArchiveTopicPlayChecks.MoveTo(session.stations[1], ArchiveRewardView.ChestLocalPosition + Vector3.right);
            Check(session.CanClaimChest(host, 1) && session.Chest(1).owner == departedOwner, "Only a permanently departed owner exposes team salvage after the grace period");
            while (health.ActorHearts(host) < health.MaximumHealth) health.RestoreAuthoritativeActorHeart(host);
            session.hunting.PublishRescueHealth(); session.hunting.Request(KhezuCommand.Sleep); SurvivalCommit();
            session.suppressDefeatForVerification = false;
            if (session.worldProgression != null) session.worldProgression.suppressThreatsForVerification = false;
        }
    }
}
