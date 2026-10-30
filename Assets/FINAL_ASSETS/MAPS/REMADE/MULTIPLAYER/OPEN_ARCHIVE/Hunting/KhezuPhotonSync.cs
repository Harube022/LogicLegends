using System;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace LogicLegends.OpenArchive.Hunting
{
    /// <summary>One placed scene monster. PUN Master controls AI; peers receive coarse snapshots, never bones.</summary>
    [DefaultExecutionOrder(100), RequireComponent(typeof(KhezuHuntingController))]
    public sealed class KhezuPhotonSync : MonoBehaviourPunCallbacks, IOnEventCallback
    {
        public const byte SnapshotEvent = 171, CommandEvent = 172;
        public const string Channel = "ll.khezu.phase2.v1";
        public const string SnapshotKey = Channel + ".snapshot";
        public string SyncChannel => string.IsNullOrEmpty(GetComponent<KhezuHuntingController>().encounterId)?Channel:Channel+"."+GetComponent<KhezuHuntingController>().encounterId;
        public string StateKey => SyncChannel+".snapshot";
        [Min(.05f)] public float snapshotInterval = .1f;
        [Min(1)] public float checkpointInterval = 2;
        KhezuHuntingController controller;
        int sequence, receivedSequence = -1, commandSequence;
        float nextSnapshot, nextCheckpoint;
        bool received;
        object[] latest;
        readonly Dictionary<int, int> commands = new Dictionary<int, int>();
        readonly Dictionary<int, float> commandTimes = new Dictionary<int, float>();
        readonly Dictionary<int, int> commandCounts = new Dictionary<int, int>();

        void Start()
        {
            controller = GetComponent<KhezuHuntingController>();
            controller.NetworkCommandRequested += Request;
            controller.SnapshotChanged += Changed;
            LoadCheckpoint();
        }
        void OnDestroy()
        {
            if (controller == null) return;
            controller.NetworkCommandRequested -= Request; controller.SnapshotChanged -= Changed;
        }
        void Update()
        {
            if (controller == null || !controller.Initialized || !PhotonNetwork.InRoom || !controller.IsAuthority) return;
            if (Time.unscaledTime >= nextSnapshot)
            {
                bool moving = controller.State == KhezuHuntState.Hunting || controller.State == KhezuHuntState.NavigationBlocked || controller.State==KhezuHuntState.ReturningToNest || controller.State==KhezuHuntState.CarryingToNest;
                nextSnapshot = Time.unscaledTime + (moving ? snapshotInterval : controller.State == KhezuHuntState.Waking || controller.State == KhezuHuntState.Caught ? .2f : 1f);
                bool checkpoint = Time.unscaledTime >= nextCheckpoint;
                if (checkpoint) nextCheckpoint = Time.unscaledTime + checkpointInterval;
                Broadcast(false, checkpoint);
            }
        }
        void Changed() { if (PhotonNetwork.InRoom && controller.IsAuthority) Broadcast(true, true); }
        void Broadcast(bool reliable, bool checkpoint)
        {
            latest = new object[] { SyncChannel, PhotonNetwork.LocalPlayer.ActorNumber, ++sequence, JsonUtility.ToJson(controller.GetSnapshot()) };
            PhotonNetwork.RaiseEvent(SnapshotEvent, latest, new RaiseEventOptions { Receivers = ReceiverGroup.Others },
                new SendOptions { Reliability = reliable });
            if (checkpoint) PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [StateKey] = latest });
        }
        void Request(KhezuCommand command)
        {
            if (!PhotonNetwork.InRoom) return;
            PhotonNetwork.RaiseEvent(CommandEvent, new object[] { SyncChannel, ++commandSequence, (int)command },
                new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
        }
        public void OnEvent(EventData data)
        {
            if (controller == null || !controller.Initialized || !PhotonNetwork.InRoom) return;
            var payload = data.CustomData as object[];
            if (payload == null || payload.Length < 3 || !(payload[0] is string) || (string)payload[0] != SyncChannel) return;
            if (data.Code == SnapshotEvent)
            {
                if (data.Sender != PhotonNetwork.MasterClient.ActorNumber || controller.IsAuthority) return;
                Receive(payload, false);
            }
            else if (data.Code == CommandEvent && string.IsNullOrEmpty(controller.encounterId) && controller.IsAuthority && PhotonNetwork.CurrentRoom.GetPlayer(data.Sender) != null &&
                payload[1] is int && payload[2] is int && Enum.IsDefined(typeof(KhezuCommand), (int)payload[2]))
            {
                // Topic mistakes and the host Admin Lab already enter through their
                // authoritative services. The old Phase 2 guest debug channel must
                // never let a room member sleep/reset/wake a production guardian.
                if(Topics.ArchiveTopicSession.Current!=null||!Debug.isDebugBuild)return;
                // Remote debug controls may not release a captured actor or skip an outcome.
                if(controller.State==KhezuHuntState.Caught || controller.State==KhezuHuntState.ReturningToNest || controller.State==KhezuHuntState.NestSleeping || controller.State==KhezuHuntState.AttackRecovery || controller.CarryingRescueActor || controller.IsTranquilized)return;
                int last; float when;
                if (commands.TryGetValue(data.Sender, out last) && (int)payload[1] <= last) return;
                // Per-peer rate limit also bounds repeated test-control traffic.
                if (!commandTimes.TryGetValue(data.Sender, out when) || Time.unscaledTime - when >= 1)
                { commandTimes[data.Sender] = Time.unscaledTime; commandCounts[data.Sender] = 0; }
                if (++commandCounts[data.Sender] > 20) return;
                commands[data.Sender] = (int)payload[1];
                controller.ApplyCommand((KhezuCommand)(int)payload[2]);
            }
        }
        void Receive(object[] packet, bool restoreAuthority)
        {
            if (packet.Length != 4 || !(packet[1] is int) || !(packet[2] is int) || !(packet[3] is string)) return;
            if (!restoreAuthority && (int)packet[1] != PhotonNetwork.MasterClient.ActorNumber) return;
            if ((int)packet[2] <= receivedSequence && !restoreAuthority) return;
            KhezuSnapshot state;
            try { state = JsonUtility.FromJson<KhezuSnapshot>((string)packet[3]); }
            catch (ArgumentException) { return; }
            if (state == null || state.hunted == null || state.excluded == null || state.hunted.Length > 4 || state.excluded.Length > 4 ||
                state.mistakes < 0 || state.mistakes > 4 || state.stage < 0 || state.stage > 4 ||
                state.encounterPhase < 0 || state.encounterPhase > 4 || double.IsNaN(state.captureStartedAt) || double.IsInfinity(state.captureStartedAt) ||
                !Enum.IsDefined(typeof(KhezuHuntState), state.state)) return;
            if(!Finite(state.capturePosition) || !Finite(state.resolvedAt) || !Finite(state.releaseAt) || !Finite(state.sleepEndsAt) || !Finite(state.nestArrivedAt) || !Finite(state.attackEndsAt) ||
                state.counterPhase<0 || state.counterPhase>3 || state.counterSerial<0 || !Finite(state.counterStartedAt) || !Finite(state.counterCooldownUntil) ||
                !Finite(state.counterAim) || !Finite(state.counterLungeMoved) || state.counterLungeMoved<0 || state.counterLungeMoved>5.1f ||
                (state.counterPhase>0 && (state.counterTarget!=state.target || state.counterAim.sqrMagnitude<.5f || state.counterAim.sqrMagnitude>1.5f ||
                    (state.state!=(int)KhezuHuntState.Hunting && state.state!=(int)KhezuHuntState.NavigationBlocked))) ||
                !Finite(state.barrierStartedAt) || !Finite(state.barrierDeadline) || !Finite(state.attackHitAt) || !Finite(state.barrierDissolveAt) || !Finite(state.stunEndsAt) ||
                state.barrierTimerSerial<0 || state.barrierTimerSerial>state.captureSerial || state.barrierDeadline<state.barrierStartedAt ||
                state.knockbackPath==null || state.knockbackPath.Length>33 || state.knockbackPath.Any(v=>!Finite(v)) ||
                state.resolvedSerial<0 || state.resolvedSerial>state.captureSerial || state.resolvedOutcome<0 || state.resolvedOutcome>2 ||
                (state.state==(int)KhezuHuntState.Caught && !state.captureAnchorValid) || (state.state==(int)KhezuHuntState.CarryingToNest && state.rescueActor==0) || state.healthStates==null || state.healthStates.Length>4 ||
                state.healthStates.Any(h=>h==null || h.actor==0 || h.hearts<0 || h.hearts>Mathf.Max(3,controller.health!=null?controller.health.MaximumHealth:3) || h.lastPenaltySerial<0 || !Finite(h.revivedUntil)) || state.healthStates.Select(h=>h.actor).Distinct().Count()!=state.healthStates.Length)return;
            bool initial = !received || restoreAuthority;
            received = true; receivedSequence = (int)packet[2]; sequence = Mathf.Max(sequence, receivedSequence); latest = packet;
            controller.ApplySnapshot(state, initial);
        }
        static bool Finite(double value)=>!double.IsNaN(value) && !double.IsInfinity(value);
        static bool Finite(Vector3 value)=>Finite(value.x) && Finite(value.y) && Finite(value.z);
        void LoadCheckpoint()
        {
            if (!PhotonNetwork.InRoom || controller == null || !controller.Initialized) return;
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(StateKey, out var value) && value is object[])
                Receive((object[])value, controller.IsAuthority);
            controller.AuthorityChanged();
        }
        public bool RestoreRecoveryCheckpoint()
        {
            if (!PhotonNetwork.InRoom || controller == null || !controller.Initialized) return false;
            if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(StateKey, out var data) || !(data is object[] packet)) return false;
            // A live snapshot can be newer than the periodic room checkpoint.
            if (received && latest != null && Equals(latest[1], PhotonNetwork.MasterClient.ActorNumber))
            {
                if (packet.Length == 4 && Equals(packet[1], latest[1]) && packet[2] is int newer && newer > receivedSequence) Receive(packet, true);
                return true;
            }
            received = false; receivedSequence = -1;
            // A room checkpoint can have been written by the previous Master before migration.
            Receive(packet, true);
            return received;
        }
        public override void OnJoinedRoom() { received = false; receivedSequence = -1; LoadCheckpoint(); }
        public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
        {
            if (!received && controller != null && !controller.IsAuthority && propertiesThatChanged.TryGetValue(StateKey, out var packet) && packet is object[])
                Receive((object[])packet, false);
        }
        public override void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
        {
            if (controller == null || !controller.Initialized) return;
            if (controller.IsAuthority && latest != null) Receive(latest, true);
            controller.AuthorityChanged();
            if (controller.IsAuthority) Broadcast(true, true);
        }
        public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
        {
            commands.Remove(otherPlayer.ActorNumber); commandTimes.Remove(otherPlayer.ActorNumber); commandCounts.Remove(otherPlayer.ActorNumber);
        }
        public override void OnLeftRoom()
        {
            received = false; receivedSequence = -1; latest = null; commands.Clear(); commandTimes.Clear(); commandCounts.Clear();
            if (controller != null && controller.Initialized) { controller.AuthorityChanged(); controller.ApplyCommand(KhezuCommand.Sleep); }
        }
    }
}
