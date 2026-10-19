using System;
using LogicLegends.OpenArchive.Hunting;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.Events;

namespace LogicLegends.OpenArchive.Cinematics
{
    public enum KhezuEncounterPhase { None, Capture, CinematicJumpscare, BarrierCast, BarrierChallenge }
    [Serializable] public sealed class KhezuEncounterEvent : UnityEvent<int, int> { }

    /// <summary>Host capture adapter. Presentation never confirms captures or resolves a challenge.</summary>
    [DefaultExecutionOrder(70), RequireComponent(typeof(KhezuHuntingController))]
    public sealed partial class KhezuCaptureEncounterController : MonoBehaviourPunCallbacks, IOnEventCallback
    {
        public KhezuCinematicJumpscareController cinematic;
        [Tooltip("Connect a Phase 3 barrier service here. Actor ID and capture serial are an idempotency key. Host only.")]
        public KhezuEncounterEvent hostBarrierActivated = new KhezuEncounterEvent();
        [Tooltip("Caught client only. Attach the real mathematical challenge interface here.")]
        public KhezuEncounterEvent localChallengeRequested = new KhezuEncounterEvent();
        public UnityEvent localEncounterEnded = new UnityEvent();
        public KhezuEncounterPhase Phase { get { return hunting == null || hunting.State != KhezuHuntState.Caught ? KhezuEncounterPhase.None : (KhezuEncounterPhase)hunting.EncounterPhase; } }
        public int ActiveCaptureSerial { get { return hunting == null ? 0 : hunting.CaptureSerial; } }
        KhezuHuntingController hunting;
        Animator animator;
        int presentedSerial, barrierSerial, challengeSerial, worldSerial, worldPhase;
        bool localEncounter;
        bool disconnected;
        public static bool IsCaughtLocalPlayer(int actor)
        {
            if (actor == 0) return false;
            if (PhotonNetwork.InRoom) return actor == PhotonNetwork.LocalPlayer.ActorNumber;
            return actor == -1 && global::Player.LocalInstance != null;
        }
        void Awake() { hunting = GetComponent<KhezuHuntingController>(); animator = GetComponent<Animator>(); }
        public override void OnEnable() { base.OnEnable(); if (hunting != null) hunting.CaptureSequenceLocked = true; }
        void Update()
        {
            if (!hunting.Initialized) return;
            SynchronizeBarrierMovement();
            if (disconnected) { EndLocal(); return; }
            if (hunting.State != KhezuHuntState.Caught) { EndLocal(); return; }
            float elapsed = Mathf.Max(0, (float)(KhezuHuntingController.EncounterClock - hunting.CaptureStartedAt));
            float timeline = KhezuCinematicJumpscareController.TimelineTime(elapsed);
            int serial = hunting.CaptureSerial, actor = hunting.CaughtPlayerId;
            if (hunting.IsAuthority)
            {
                bool challengeAlreadyConfirmed = hunting.EncounterPhase == 4;
                int phase = timeline < 1.2f ? 1 : timeline < 2.8f ? 2 : elapsed < KhezuCinematicJumpscareController.Duration ? 3 : 4;
                if (worldSerial != serial || worldPhase != phase)
                {
                    worldSerial = serial; worldPhase = phase;
                    string clip = phase == 1 ? "Capture" : phase == 2 ? "Jumpscare" : phase == 3 ? "Barrier_Cast" : null;
                    if (clip != null)
                    {
                        float offset = KhezuCinematicJumpscareController.WorldClipTime(phase, elapsed);
                        animator.speed = KhezuCinematicJumpscareController.WorldClipSpeed(phase, elapsed);
                        animator.CrossFadeInFixedTime("Base Layer." + clip, phase == 1 ? .02f : phase == 2 ? .035f : .08f, 0, offset);
                    }
                    hunting.SetEncounterPhase(phase);
                }
                if (phase < 4) animator.speed = KhezuCinematicJumpscareController.WorldClipSpeed(phase, elapsed);
                if (phase == 4 && barrierSerial != serial)
                {
                    barrierSerial = serial;
                    BarrierLog("Barrier activation confirmed for actor "+actor+", capture "+serial);
                    // A checkpoint/new host resumes the confirmed barrier, never spawns it again.
                    if (!challengeAlreadyConfirmed) hostBarrierActivated.Invoke(actor, serial);
                }
                if (phase == 4) animator.speed = animator.GetCurrentAnimatorStateInfo(0).IsName("Idle") ? 1 : KhezuCinematicJumpscareController.PlaybackSpeed;
            }
            if (!IsCaughtLocalPlayer(actor)) { EndLocal(); return; }
            if (presentedSerial != serial)
            {
                presentedSerial = serial; localEncounter = true;
                // A late checkpoint resumes time; it never replays an expired scare.
                if (elapsed < KhezuCinematicJumpscareController.Duration && cinematic != null)
                {
                    if (!cinematic.IsPlaying || cinematic.ActiveCaptureSerial != serial)
                    { cinematic.Cancel(); cinematic.BeginConfirmedCapture(actor, serial, hunting.CaptureStartedAt); }
                }
            }
            if (hunting.EncounterPhase == 4 && elapsed >= KhezuCinematicJumpscareController.Duration && challengeSerial != serial &&
                (cinematic == null || !cinematic.IsPlaying))
            {
                challengeSerial = serial; localChallengeRequested.Invoke(actor, serial);
            }
        }
        void EndLocal()
        {
            if (!localEncounter) return;
            if (cinematic != null) cinematic.Cancel();
            localEncounter = false; localEncounterEnded.Invoke();
        }
        public override void OnDisable()
        {
            ReleaseBarrierMovement();
            EndLocal(); if (cinematic != null) cinematic.Cancel();
            if (hunting != null) hunting.CaptureSequenceLocked = false; base.OnDisable();
        }
        public override void OnDisconnected(DisconnectCause cause) { disconnected = true; ReleaseBarrierMovement(); EndLocal(); }
        public override void OnLeftRoom() { disconnected = true; ReleaseBarrierMovement(); EndLocal(); }
        public override void OnJoinedRoom() { disconnected = false; }
        public override void OnPlayerLeftRoom(Photon.Realtime.Player other)
        {
            if (hunting.IsAuthority && hunting.State == KhezuHuntState.Caught && other.ActorNumber == hunting.CaughtPlayerId)
            {hunting.Request(KhezuCommand.ResetHunt);hunting.Request(KhezuCommand.StartHunting);}
        }
        // Called only by a host-owned challenge service after it has decided the outcome.
        // No damage, success/failure decision, or rotation mutation is performed here.
        public bool ReleaseAfterHostChallenge(int actor, int serial)
        {
            if (!hunting.IsAuthority || Phase != KhezuEncounterPhase.BarrierChallenge || actor != hunting.CaughtPlayerId || serial != hunting.CaptureSerial) return false;
            hunting.Request(KhezuCommand.ResetHunt); return true;
        }
    }
}
