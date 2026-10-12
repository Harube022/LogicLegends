using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using UnityEngine;

namespace LogicLegends.Graph
{
    /// <summary>Graph owns its completion and persistence; no legacy stage manager is needed.</summary>
    public sealed class GraphCompletion : MonoBehaviour
    {
        public int stageNumber = 9;
        public int baseCoins = 500, baseGems = 40;
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
            var auth = FirebaseAuth.DefaultInstance;
            if (auth == null || auth.CurrentUser == null) return;
            var user = FirebaseDatabase.DefaultInstance.RootReference.Child("users").Child(auth.CurrentUser.UserId);
            int coins = Mathf.Max(0, baseCoins - mistakes * 50);
            int gems = Mathf.Max(0, baseGems - mistakes * 5);
            // Match the existing account schema and update rewards/progression atomically.
            user.RunTransaction(data =>
            {
                if (data.Value == null) return TransactionResult.Abort();
                long currentCoins = Number(data.Child("coins").Value);
                long currentGems = Number(data.Child("gems").Value);
                long unlocked = Number(data.Child("unlockedStage").Value);
                data.Child("coins").Value = currentCoins + coins;
                data.Child("gems").Value = currentGems + gems;
                data.Child("unlockedStage").Value = System.Math.Max(unlocked, Mathf.Min(stageNumber + 1, StageSelectionState.LastStage));
                return TransactionResult.Success(data);
            }).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled) Debug.LogWarning("Graph progress could not be saved.");
            });
        }
        private static long Number(object value)
        { long result; return value != null && long.TryParse(value.ToString(), out result) ? result : 0; }
    }
}
