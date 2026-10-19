using UnityEngine;

namespace LogicLegends.Graph
{
    /// <summary>Graph owns its completion and persistence; no legacy stage manager is needed.</summary>
    public sealed class GraphCompletion : MonoBehaviour
    {
        public int stageNumber = 9;
        public int baseCoins = 500;
        // Legacy scenes may still serialize 40 here. This value is never granted.
        [HideInInspector] public int baseGems = 0;
        private bool completed;
#if UNITY_EDITOR
        public static bool SuppressPersistenceForValidation { get; set; }
#endif
        public void Complete(int mistakes)
        {
            if (completed) return;
            completed = true;
#if UNITY_EDITOR
            if (SuppressPersistenceForValidation) return;
#endif
            int coins = Mathf.Max(0, baseCoins - mistakes * 50);
            // Update only the permitted gameplay fields. A whole-account
            // transaction would also attempt to rewrite server-owned gems.
            StageCompleteManager.AwardCoins(coins);
            StageCompleteManager.UnlockStage(Mathf.Min(stageNumber + 1, StageSelectionState.LastStage));
        }
    }
}
