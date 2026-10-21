using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    public sealed partial class ArchiveTopicSession
    {
        [Header("Team escape")]
        public ArchiveVictoryPortal exitPortal;
        public bool PortalOpened => Ready && State.exit != null && State.exit.opened;
        public bool TeamWon => Ready && State.exit != null && State.exit.won;
        public bool HasEscapedActor(int actor) => Ready && State.exit != null &&
            State.exit.escaped != null && State.exit.escaped.Any(e => e.actor == actor);
        public bool AllTopicsComplete => Ready && State.stations.Length == 10 &&
            Enumerable.Range(0,10).All(i => State.stations[i].topic == i && State.stations[i].phase == 2 &&
                State.stations[i].round >= ArchiveTopicCatalog.RoundCount(i));
        public int SurvivorCount => SurvivingActors().Count();
        public int EscapedSurvivorCount => SurvivingActors().Count(HasEscapedActor);
        float nextExitSync;

        // All receipts live in the existing team checkpoint. Clients never claim an escape via RPC.
        void NormalizeExit()
        {
            if (State.exit == null) State.exit = new ArchiveExitState();
            if (State.exit.escaped == null) State.exit.escaped = new List<ArchiveEscape>();
        }
        IEnumerable<int> SurvivingActors()
        {
            if (!Ready) yield break;
            if (!PhotonNetwork.InRoom)
            {
                if (Player.LocalInstance != null && ActorAlive(-1)) yield return -1;
                yield break;
            }
            // A connected living player still counts while their avatar is loading.
            foreach (var member in PhotonNetwork.CurrentRoom.Players.Values)
                if (!member.IsInactive && ActorAlive(member.ActorNumber)) yield return member.ActorNumber;
        }
        bool ActorAlive(int actor)
        {
            if (PhotonNetwork.InRoom)
            {
                var member = PhotonNetwork.CurrentRoom.GetPlayer(actor);
                if (member == null || member.IsInactive) return false;
                // Owner-writable lobby flags must never remove a living teammate from the exit requirement.
                if (hunting != null && hunting.health != null && hunting.health.HasAuthoritativeActorHealth)
                    return hunting.health.IsActorAlive(actor);
                if (member.CustomProperties.TryGetValue("ll.eliminated", out var dead) && dead is bool b && b) return false;
            }
            return hunting == null || hunting.health == null || !hunting.health.HasAuthoritativeActorHealth || hunting.health.IsActorAlive(actor);
        }
        bool AdvanceExitProgress()
        {
            if (!Ready || !IsAuthority) return false;
            NormalizeExit();
            var exit = State.exit;
            if (exit.won) return false;
            bool dirty = false;
            if (!exit.opened && AllTopicsComplete)
            {
                exit.opened = true; exit.openedAt = Clock; dirty = true;
            }
            if (!exit.opened || exitPortal == null || !exitPortal.Formed) return dirty;
            var survivors = SurvivingActors().ToArray();
            foreach (int actor in survivors)
            {
                if (HasEscapedActor(actor) || !CanAct(actor)) continue;
                var player = ActorPlayer(actor);
                if (!exitPortal.ContainsPlayer(player)) continue;
                exit.escaped.Add(new ArchiveEscape { actor = actor, anchor = player.transform.position, time = Clock });
                ReleaseCarried(actor); dirty = true;
            }
            // A wipe cannot become a victory just because the survivor set is empty.
            if (survivors.Length > 0 && survivors.All(HasEscapedActor))
            {
                exit.won = true; exit.wonAt = Clock; dirty = true;
                if (hunting != null) hunting.Request(Hunting.KhezuCommand.Sleep);
                if (PhotonNetwork.InRoom) PhotonNetwork.CurrentRoom.IsOpen = false;
            }
            return dirty;
        }

        sealed class EscapedBody
        {
            public Player player;
            public CharacterController capsule;
            public bool capsuleEnabled;
            public Renderer[] renderers;
            public bool[] hidden;
        }
        readonly Dictionary<int, EscapedBody> escapedBodies = new Dictionary<int, EscapedBody>();
        void SyncEscapedPlayers()
        {
            if (!Ready || State.exit == null) { ReleaseEscapedPlayers(); return; }
            foreach (int actor in escapedBodies.Keys.ToArray())
                if (!HasEscapedActor(actor) || escapedBodies[actor].player == null) ReleaseEscapedPlayer(actor);
            foreach (var receipt in State.exit.escaped)
            {
                var player = ActorPlayer(receipt.actor);
                if (player == null) continue;
                if (escapedBodies.TryGetValue(receipt.actor, out var body) && body.player != player)
                { ReleaseEscapedPlayer(receipt.actor); body = null; }
                if (body == null)
                {
                    body = new EscapedBody { player = player, capsule = player.GetComponent<CharacterController>(), renderers = player.GetComponentsInChildren<Renderer>(true) };
                    body.capsuleEnabled = body.capsule != null && body.capsule.enabled;
                    body.hidden = body.renderers.Select(r => r.forceRenderingOff).ToArray();
                    escapedBodies[receipt.actor] = body;
                }
                player.AcquireMovementLock(this, "Escaped through the Archive portal", receipt.anchor, 300);
                // Clear the entrance for teammates without destroying the network avatar.
                if (body.capsule != null) body.capsule.enabled = false;
                foreach (var renderer in body.renderers) if (renderer != null) renderer.forceRenderingOff = true;
            }
        }
        void ReleaseEscapedPlayer(int actor)
        {
            if (!escapedBodies.TryGetValue(actor, out var body)) return;
            if (body.player != null) body.player.ReleaseMovementLock(this);
            if (body.capsule != null) body.capsule.enabled = body.capsuleEnabled;
            for (int i = 0; i < body.renderers.Length; i++) if (body.renderers[i] != null) body.renderers[i].forceRenderingOff = body.hidden[i];
            escapedBodies.Remove(actor);
        }
        void ReleaseEscapedPlayers()
        {
            foreach (int actor in escapedBodies.Keys.ToArray()) ReleaseEscapedPlayer(actor);
        }
    }
}
