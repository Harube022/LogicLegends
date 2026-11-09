using LogicLegends.OpenArchive.Topics;
using UnityEngine;

namespace LogicLegends.OpenArchive.Exploration
{
    /// <summary>Retired serialized component. Retained only so older scenes can release its sprint restriction.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(50)]
    public sealed partial class ArchiveSprintStamina : MonoBehaviour
    {
        [Header("Open Archive sprint stamina")]
        [Min(1)] public float capacity = 100;
        [Min(.1f)] public float fullSprintSeconds = 6;
        [Min(.1f)] public float fullRecoverySeconds = 8;
        [Min(0)] public float recoveryDelaySeconds = 1.5f;
        [Range(.1f,.8f)] public float resumeFraction = .35f;
        public Player BoundPlayer => player;
        public float Current => current;
        public float Fraction => Mathf.Clamp01(current / Mathf.Max(1,capacity));
        public bool Exhausted { get; private set; }
        public bool Sprinting { get; private set; }
        public bool Suspended { get; private set; }
        Player player;
        CharacterController capsule;
        ArchiveTopicSession session;
        float current, recoveryDelay;
        bool suppressionApplied;
        string match;

        void Awake() { current = Mathf.Max(1,capacity); enabled = false; }
        public void Bind(Player localPlayer)
        {
            if (player == localPlayer) return;
            ReleaseSuppression();
            player = localPlayer;
            capsule = player == null ? null : player.GetComponent<CharacterController>();
            ApplySuppression();
        }
        void Update()
        {
            if (session == null) session = ArchiveTopicSession.Current;
            if (session != null && session.Ready && match != session.State.matchId)
            {
                match = session.State.matchId;
                current = Mathf.Max(1,capacity); recoveryDelay = 0; Exhausted = false;
            }
            Suspended = gameObject.scene.path != "Assets/Scenes/MultiplayerGameplay.unity" ||
                player == null || player != Player.LocalInstance || !player.enabled ||
                capsule == null || !capsule.enabled || player.IsMovementLocked || Time.timeScale <= 0 ||
                GameInput.Instance == null || GameInput.Instance.GameplayActionsBlocked ||
                session == null || !session.Ready || !session.CanAct(session.LocalActor) ||
                ArchiveMatchCinematic.Covers(session) || session.NightEntranceActive ||
                session.worldProgression != null && session.worldProgression.FullscreenVisible;
            Sprinting = !Suspended && player.IsRunning() && player.IsWalking();
            if (!Suspended) Step(Time.deltaTime,Sprinting);
            ApplySuppression();
            PresentStamina();
        }
        void Step(float seconds, bool sprinting)
        {
            if (seconds <= 0) return;
            float maximum = Mathf.Max(1,capacity);
            if (sprinting && !Exhausted)
            {
                current = Mathf.Max(0,current - maximum / Mathf.Max(.1f,fullSprintSeconds) * seconds);
                recoveryDelay = Mathf.Max(0,recoveryDelaySeconds);
                if (current <= 0) Exhausted = true;
            }
            else
            {
                // Consume only the part of this frame beyond the delay, independent of frame rate.
                float recoveryTime = Mathf.Max(0,seconds-recoveryDelay);
                recoveryDelay = Mathf.Max(0,recoveryDelay-seconds);
                current = Mathf.Min(maximum,current+maximum/Mathf.Max(.1f,fullRecoverySeconds)*recoveryTime);
                if (Exhausted && current >= maximum*Mathf.Clamp(resumeFraction,.1f,.8f)) Exhausted = false;
            }
            current = Mathf.Clamp(current,0,maximum);
        }
        void ApplySuppression()
        {
            bool suppress = isActiveAndEnabled && player != null && player == Player.LocalInstance && Exhausted;
            if (player == null || suppressionApplied == suppress) return;
            player.SetRunningSuppressed(this,suppress); suppressionApplied = suppress;
        }
        void ReleaseSuppression()
        {
            if (player != null) player.SetRunningSuppressed(this,false);
            suppressionApplied = false;
        }
        void OnEnable() { ReleaseSuppression(); enabled = false; }
        void OnDisable() { ReleaseSuppression(); if (staminaRoot != null) staminaRoot.gameObject.SetActive(false); }
        void OnDestroy() { ReleaseSuppression(); if (staminaRoot != null) Destroy(staminaRoot.gameObject); }
    }
}
