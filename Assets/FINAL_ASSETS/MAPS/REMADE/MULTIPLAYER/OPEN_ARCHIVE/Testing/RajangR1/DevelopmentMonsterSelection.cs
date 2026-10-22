using LogicLegends.Tests.Khezu;
using Photon.Pun;
using UnityEngine;

namespace LogicLegends.Tests.Monsters
{
    public enum DevelopmentMonster { Khezu, Rajang }

    /// <summary>Initializes the existing encounter's room-selected rig. Manual swaps remain offline tests.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-10000)]
    public sealed partial class DevelopmentMonsterSelection : MonoBehaviour
    {
        [SerializeField] DevelopmentMonster selected = DevelopmentMonster.Rajang;
        [SerializeField] KhezuAwakeningTestController khezu;
        [SerializeField] KhezuAwakeningTestController rajang;

        public DevelopmentMonster Selected => selected;
        public KhezuAwakeningTestController Khezu => khezu;
        public KhezuAwakeningTestController Rajang => rajang;
        public KhezuAwakeningTestController ActiveTest => selected == DevelopmentMonster.Rajang ? rajang : khezu;

        public void Configure(KhezuAwakeningTestController khezuTest, KhezuAwakeningTestController rajangTest)
        {
            khezu = khezuTest; rajang = rajangTest;
            Select(DevelopmentMonster.Rajang);
        }

        void Awake()
        {
            if (HasSharedEncounter && PhotonNetwork.InRoom)
            {
                if (MultiplayerMonsterRotation.TryRead(PhotonNetwork.CurrentRoom.CustomProperties, out var matchMonster))
                {
                    // Apply before the hunting/animation components initialize. Rejoins
                    // and Master changes do not call this or reset an active encounter.
                    SelectSharedEncounter(matchMonster, true);
                    return;
                }
                if (!PhotonNetwork.OfflineMode)
                {
                    Debug.LogError("The multiplayer room has no valid monster selection. Update all players and host a new party.", this);
                    enabled = false;
                    return;
                }
            }
            Select(selected);
        }

        public void Select(DevelopmentMonster monster)
        {
            if (HasSharedEncounter) { SelectSharedEncounter(monster); return; }
            if (khezu == null || rajang == null || khezu == rajang)
            {
                Debug.LogError("R1 test selection requires the two existing, separate monster test instances.", this);
                enabled = false;
                return;
            }
            selected = monster;
            // Deactivate the other driver before enabling the selected one.
            var active = ActiveTest;
            var inactive = selected == DevelopmentMonster.Rajang ? khezu : rajang;
            inactive.gameObject.SetActive(false);
            active.gameObject.SetActive(true);
            if (Application.isPlaying && active.Animator != null && active.Animator.isInitialized) active.ResetTest();
        }

        [ContextMenu("R1/Select Rajang")]
        public void SelectRajang() { Select(DevelopmentMonster.Rajang); }
        [ContextMenu("R1/Select Khezu")]
        public void SelectKhezu() { Select(DevelopmentMonster.Khezu); }
        [ContextMenu("R1/Reset Rajang")]
        public void ResetRajang() { SelectRajang(); PlaySleep(); }
        [ContextMenu("R1/Play Sleep")]
        public void PlaySleep() { if (Application.isPlaying && ActiveTest != null && (!HasSharedEncounter || CanSwap)) ActiveTest.ResetTest(); }
        [ContextMenu("R1/Mistake 1")]
        public void Mistake1() { TriggerMistake(1); }
        [ContextMenu("R1/Mistake 2")]
        public void Mistake2() { TriggerMistake(2); }
        [ContextMenu("R1/Mistake 3")]
        public void Mistake3() { TriggerMistake(3); }
        [ContextMenu("R1/Mistake 4")]
        public void Mistake4() { TriggerMistake(4); }
        public void TriggerMistake(int stage)
        {
            if (!Application.isPlaying || ActiveTest == null || !ActiveTest.isActiveAndEnabled) return;
            if (HasSharedEncounter && !CanSwap) return;
            var test = ActiveTest;
            stage = Mathf.Clamp(stage, 0, 4);
            if (test.IsPreviewing || test.Mistakes > stage) test.ResetTest();
            for (int i = test.Mistakes; i < stage; i++) test.IncreaseMistakeCount();
        }
        [ContextMenu("R1/Play Idle")]
        public void PlayIdle() { Preview("Idle"); }
        public bool Preview(string animation)
        {
            return Application.isPlaying && ActiveTest != null && ActiveTest.PreviewAnimation(animation);
        }
    }
}
