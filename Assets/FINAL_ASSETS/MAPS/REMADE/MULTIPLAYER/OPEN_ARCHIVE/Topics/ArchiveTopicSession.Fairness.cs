using System;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    public sealed partial class ArchiveTopicSession
    {
        public const byte CommandReceiptEvent = 176;
        [Header("Cooperative pacing")]
        [Tooltip("Learning: 50% more topic time and the original three-minute barrier. Normal: 90-second barrier. Chosen by the host before a fresh match.")]
        public bool learningPace;
        public bool suggestTeamRoles = true;
        public bool CommandPending => pendingCommands.Count > 0;
        public float PacingMultiplier => State == null || State.pacingVersion == 0 ? 1 :
            TeamTimeMultiplier(State.pacingPlayers) * (State.learningPace ? 1.5f : 1);
        public static float TeamTimeMultiplier(int players) => players <= 2 ? 1.25f : players == 3 ? 1.1f : 1;

        sealed class PendingCommand
        {
            public ArchiveCommand command;
            public string json;
            public float sentAt;
            public int attempts;
        }
        readonly Dictionary<int, PendingCommand> pendingCommands = new Dictionary<int, PendingCommand>();
        readonly Dictionary<string, ArchiveCommandReceipt> commandReceipts = new Dictionary<string, ArchiveCommandReceipt>();
        readonly Queue<string> receiptOrder = new Queue<string>();
        float noticeUntil;
        string commandRejection;

        void InitializeFairness()
        {
            State.pacingVersion = 1;
            State.matchId = Guid.NewGuid().ToString("N");
            // Freeze at match creation: a disconnect cannot refill a running clock or change its budget.
            State.pacingPlayers = PhotonNetwork.InRoom ? Mathf.Clamp(PhotonNetwork.CurrentRoom.PlayerCount, 2, 4) : 4;
            State.learningPace = learningPace;
            pendingCommands.Clear(); commandReceipts.Clear(); receiptOrder.Clear();
            ConfigureEncounterPacing();
        }
        void ConfigureEncounterPacing()
        {
            if (!Ready || !enableBarrierMath || hunting == null || State.pacingVersion == 0) return;
            hunting.barrierTimeLimitSeconds = State.learningPace ? 180 : 90;
            hunting.counterWarningSeconds = Mathf.Max(.9f, hunting.counterWarningSeconds);
        }
        void StampCommandContext(ArchiveCommand cmd)
        {
            var s = State.stations[cmd.topic];
            cmd.contextVersion = 1; cmd.round = s.round; cmd.subphase = s.subphase;
            cmd.matchId = State.matchId;
            cmd.workStamp = WorkStamp(s);
            cmd.resetSerial = s.testResetSerial; cmd.phase = s.phase;
            cmd.expectedHeld = s.carries.FirstOrDefault(c => c.actor == LocalActor)?.item ?? -1;
            if (cmd.action == ArchiveAction.Place && cmd.a >= 0 && cmd.a < s.placements.Length)
                cmd.expectedSlot = s.placements[cmd.a];
        }
        bool CanApplyCommandContext(ArchiveCommand cmd, ArchiveStationState s)
        {
            // Legacy tools retain their strict revision contract. Current clients name the exact question.
            if (cmd.contextVersion == 0) return cmd.revision == s.revision;
            if (cmd.contextVersion != 1 || cmd.matchId != State.matchId || cmd.round != s.round || cmd.subphase != s.subphase ||
                cmd.resetSerial != s.testResetSerial || cmd.phase != s.phase || cmd.revision > s.revision)
            { commandRejection = "The challenge has changed. Review the current question and try again."; return false; }
            if (cmd.revision == s.revision) return true;
            // Revalidate independent edits against the live authoritative item/slot, distance and ownership.
            // Confirmation requires the work the player saw; unrelated actor edits and route
            // position samples must not invalidate an otherwise unchanged answer.
            switch (cmd.action)
            {
                case ArchiveAction.Start: case ArchiveAction.Pick: case ArchiveAction.Place:
                case ArchiveAction.ReturnItem: case ArchiveAction.BeginChoice:
                case ArchiveAction.Connect: case ArchiveAction.Remove:
                    return true;
                case ArchiveAction.Select: case ArchiveAction.ClearSelection:
                    var q = Question(s.topic);
                    if (q.tree != null && q.tree.Editable) return true;
                    if (cmd.action == ArchiveAction.Select && q.mapping != null &&
                        q.mapping.questionKind == MappingQuestionKind.BuildExactRelation) return true;
                    if(cmd.workStamp==WorkStamp(s))return true;
                    break;
                case ArchiveAction.Submit: case ArchiveAction.Undo: case ArchiveAction.RetryRoute:
                    if(cmd.workStamp==WorkStamp(s))return true;
                    break;
            }
            commandRejection = "A teammate updated the work. Review it, then confirm again. No mistake charged.";
            return false;
        }
        public static string WorkStamp(ArchiveStationState s) => s.selection+"|"+s.flags+"|"+
            string.Join(",",s.placements)+"|"+string.Join(",",s.links)+"|"+string.Join(",",s.route)+"|"+s.continuous;
        bool DispatchCommand(ArchiveCommand command)
        {
            if (IsAuthority) return AcceptWithReceipt(LocalActor, command);
            if (!PhotonNetwork.InRoom || MultiplayerLobbyService.IsGameplayRecovering) return false;
            if (pendingCommands.Count >= 8) { ShowLocalNotice("Waiting for the host. Please wait before trying again.", 5); return false; }
            var pending = new PendingCommand { command = command, json = JsonUtility.ToJson(command), sentAt = Time.unscaledTime, attempts = 1 };
            pendingCommands[command.sequence] = pending;
            ShowLocalNotice("Sending to host…", 5);
            return SendCommandPacket(pending.json);
        }
        static bool SendCommandPacket(string json) => PhotonNetwork.RaiseEvent(CommandEvent,
            new object[] { Channel, json }, new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);

        bool AcceptWithReceipt(int actor, ArchiveCommand command)
        {
            if (!Ready || !IsAuthority || command == null || command.sequence <= 0) return false;
            string key = (command.matchId ?? State.matchId) + ":" + actor + ":" + command.sequence;
            if (!commandReceipts.TryGetValue(key, out var receipt))
            {
                commandRejection = "That action was not applied. Check your distance, carried item and the team's latest work.";
                if(command.action==ArchiveAction.BarrierAnswer||command.action==ArchiveAction.BarrierHint)
                    commandRejection="The barrier question updated or closed. Review its current state before confirming again.";
                bool accepted = Accept(actor, command);
                receipt = new ArchiveCommandReceipt { sequence = command.sequence, matchId=command.matchId??State.matchId, accepted = accepted,
                    message = accepted ? actor==LocalActor&&(command.action==ArchiveAction.AdminCompleteTopic||command.action==ArchiveAction.AdminResetTopic)?LocalNotice:"" : commandRejection };
                commandReceipts[key] = receipt; receiptOrder.Enqueue(key);
                while (receiptOrder.Count > 256) commandReceipts.Remove(receiptOrder.Dequeue());
            }
            if (actor == LocalActor) ApplyCommandReceipt(receipt);
            else if (PhotonNetwork.InRoom)
                PhotonNetwork.RaiseEvent(CommandReceiptEvent, new object[] { Channel, JsonUtility.ToJson(receipt) },
                    new RaiseEventOptions { TargetActors = new[] { actor } }, SendOptions.SendReliable);
            return receipt.accepted;
        }
        void ReceiveCommandReceipt(string json)
        {
            if (json.Length > 1024) return;
            try
            {
                var receipt = JsonUtility.FromJson<ArchiveCommandReceipt>(json);
                if (Ready && receipt != null && receipt.matchId==State.matchId && pendingCommands.ContainsKey(receipt.sequence)) ApplyCommandReceipt(receipt);
            }
            catch (Exception) { Debug.LogWarning("Ignored malformed topic acknowledgement."); }
        }
        void ApplyCommandReceipt(ArchiveCommandReceipt receipt)
        {
            pendingCommands.Remove(receipt.sequence);
            string message=receipt.message;
            if(receipt.accepted&&string.IsNullOrEmpty(message))message=pendingCommands.Count>0?"Waiting for host…":"";
            ShowLocalNotice(message,receipt.accepted?3:6);
        }
        public void ShowLocalNotice(string message, float seconds = 5)
        { LocalNotice = message; noticeUntil = Time.unscaledTime + seconds; }
        void UpdateCommandReceipts()
        {
            if (!string.IsNullOrEmpty(LocalNotice) && Time.unscaledTime >= noticeUntil && !CommandPending) LocalNotice = "";
            if (!Ready || !PhotonNetwork.InRoom || MultiplayerLobbyService.IsGameplayRecovering)
            { pendingCommands.Clear(); return; }
            foreach (var pair in pendingCommands.ToArray())
            {
                var p = pair.Value;
                if (Time.unscaledTime - p.sentAt < 3) continue;
                if (p.attempts >= 3)
                { pendingCommands.Remove(pair.Key); ShowLocalNotice("Host response delayed. Check your connection and the latest work before retrying.", 8); continue; }
                // Same sequence and same intent: receipt replay never repeats a penalty or a placement.
                p.sentAt = Time.unscaledTime; p.attempts++; SendCommandPacket(p.json);
                ShowLocalNotice("Waiting for host… your action is pending.", 5);
            }
        }
        public string CooperationGuide(int topic)
        {
            if (!Ready || topic < 0 || topic >= 10) return "";
            var peers = State.actors.Where(a => CanAct(a.actor) && Near(a.actor, topic, Vector3.zero, 18)).Select(a => a.actor)
                .Concat(PhotonNetwork.InRoom ? PhotonNetwork.PlayerList.Where(p => !p.IsInactive && CanAct(p.ActorNumber) && Near(p.ActorNumber,topic,Vector3.zero,18)).Select(p => p.ActorNumber) : new[] { LocalActor })
                .Distinct().OrderBy(a => a).ToArray();
            string roles = "";
            if (suggestTeamRoles && peers.Length > 1)
            {
                int rotation = State.stations[topic].round % peers.Length;
                roles = "TEAM ROLES · rotate each problem\nReader: " + ActorName(peers[rotation]) +
                    " · Builder: " + ActorName(peers[(rotation + 1) % peers.Length]) +
                    " · Checker: " + ActorName(peers[(rotation + 2) % peers.Length]) +
                    (peers.Length>3 ? " · Runner: " + ActorName(peers[(rotation + 3) % peers.Length]) : "") +
                    "\nRead aloud, build together, then explain why before confirming. Roles are suggestions; unavailable teammates never block progress.\n\n";
            }
            var mine = State.actors.FirstOrDefault(a => a.actor == LocalActor);
            return "PACE: " + (State.learningPace ? "Learning" : "Normal") + " · " + State.pacingPlayers + "-player starting team\n\n" + roles + "YOUR PARTICIPATION\n" + (mine == null ? "No recorded contributions yet." :
                mine.qualityMilestones + " unique accepted contributions\n" + mine.contributions + " puzzle edits (not points) · " + mine.correctAnswers + " correct / " + mine.answers + " confirmed attempts\n" +
                mine.assistedAnswers + " correct attempts with team hints · " + mine.hintsUsed + " free hints used") +
                "\nTeam completion is shared. These counts describe participation, not individual mastery.";
        }
    }
}
