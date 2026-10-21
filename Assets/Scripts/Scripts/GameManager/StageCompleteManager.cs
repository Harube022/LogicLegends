using UnityEngine;
using TMPro;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using UnityEngine.SceneManagement;
using Photon.Pun; 

public class StageCompleteManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The parent object of your Stage Complete Canvas")]
    [SerializeField] private GameObject stageCompletePanel;
    [Tooltip("Drag the UI holding the joystick here to hide it")]
    [SerializeField] private GameObject gameplayInterfacePanel; 
    
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text heartsLostText;
    [SerializeField] private TMP_Text coinsRewardText;
    [SerializeField] private TMP_Text gemsRewardText;

    [Header("Reward Settings")]
    [SerializeField] private int baseCoins = 500;
    // Retained for serialized scene compatibility. Stage completions award no gems.
    [SerializeField, HideInInspector] private int baseGems = 0;
    [SerializeField] private int coinPenaltyPerHeart = 50;
    [SerializeField, HideInInspector] private int gemPenaltyPerHeart = 0;
    private bool completed;
#if UNITY_EDITOR
    public static bool SuppressPersistenceForValidation { get; set; }
#endif

    [Header("Stage Progression")]
    [Tooltip("What stage number is this? (e.g., 1)")]
    [SerializeField] private int thisStageNumber = 1; 
    [Tooltip("The exact scene name of the NEXT stage")]
    [SerializeField] private string nextSceneName;
    [SerializeField] private string mainMenuSceneName = "Main Menu";

    public void TriggerStageComplete()
    {
        if (completed) return;
        completed = true;
        // 1. Freeze the game clock
        if (LevelManager.Instance != null) LevelManager.Instance.StopStageTimer();

        // 2. Swap the UI Panels
        if (gameplayInterfacePanel != null) gameplayInterfacePanel.SetActive(false);
        if (stageCompletePanel != null) stageCompletePanel.SetActive(true);
        else
        {
            SetsUIController setsUI = FindFirstObjectByType<SetsUIController>();
            if (setsUI != null) setsUI.ShowStageComplete();
        }

        // 3. Retrieve the Stats
        float timeTaken = LevelManager.Instance != null ? LevelManager.Instance.totalStageTime : 0f;
        int heartsLost = LevelManager.Instance != null ? LevelManager.Instance.totalHeartsLostThisStage : 0;

        // 4. Calculate Final Rewards (Mathf.Max ensures it never drops below 0!)
        int finalCoins = Mathf.Max(0, baseCoins - (heartsLost * coinPenaltyPerHeart));
        // Ignore legacy nonzero scene values, including negative penalties.
        const int finalGems = 0;

        // 5. Update the Text on Screen
        int minutes = Mathf.FloorToInt(timeTaken / 60);
        int seconds = Mathf.FloorToInt(timeTaken % 60);
        if (timeText != null) timeText.text = string.Format("{0:00}:{1:00}", minutes, seconds);

        if (heartsLostText != null) heartsLostText.text = heartsLost.ToString();
        if (coinsRewardText != null) coinsRewardText.text = "+" + finalCoins.ToString();
        if (gemsRewardText != null) gemsRewardText.text = "+" + finalGems.ToString();

        // 6. Push to Google Firebase
        SaveProgressToFirebase(finalCoins);
    }

    private void SaveProgressToFirebase(int coinsEarned)
    {
#if UNITY_EDITOR
        if (SuppressPersistenceForValidation) return;
#endif

        // A. Save the highest stage unlocked through the shared progression path.
        UnlockStage(Mathf.Min(thisStageNumber + 1, StageSelectionState.LastStage));

        // Only coins are awarded by gameplay. Never rewrite the gem balance,
        // even with +0: a stale write could erase a confirmed top-up.
        AwardCoins(coinsEarned);
    }

    public static void AwardCoins(int amount)
    {
        if (amount <= 0) return;
        FirebaseAuth auth = FirebaseAuth.DefaultInstance;
        if (auth == null || auth.CurrentUser == null) return;
        var coins = FirebaseDatabase.DefaultInstance.RootReference
            .Child("users").Child(auth.CurrentUser.UserId).Child("coins");
        coins.RunTransaction(data =>
        {
            long current = 0;
            if (data.Value != null && (!long.TryParse(data.Value.ToString(), out current) || current < 0))
                return TransactionResult.Abort();
            if (current > int.MaxValue - (long)amount) return TransactionResult.Abort();
            data.Value = current + amount;
            return TransactionResult.Success(data);
        }).ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled || task.IsFaulted) Debug.LogWarning("Your coin reward could not be saved. Check your connection.");
        });
    }

    public static void UnlockStage(int stageNumber)
    {
        int requestedStage = Mathf.Clamp(
            stageNumber,
            StageSelectionState.FirstStage,
            StageSelectionState.LastStage);

        FirebaseAuth auth = FirebaseAuth.DefaultInstance;
        if (auth == null || auth.CurrentUser == null)
        {
            Debug.LogWarning($"Cannot persist Stage {requestedStage}: no Firebase user is signed in.");
            return;
        }

        DatabaseReference unlockedStageReference = FirebaseDatabase.DefaultInstance.RootReference
            .Child("users").Child(auth.CurrentUser.UserId).Child("unlockedStage");

        unlockedStageReference.RunTransaction(data =>
        {
            int currentUnlocked = StageSelectionState.FirstStage;
            if (data.Value != null && !int.TryParse(data.Value.ToString(), out currentUnlocked))
                return TransactionResult.Abort();
            data.Value = Mathf.Max(currentUnlocked, requestedStage);
            return TransactionResult.Success(data);
        }).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
                Debug.LogWarning($"Could not save Stage {requestedStage}. Check your connection.");
        });
    }

    // --- BUTTON ACTIONS ---
    public void LoadNextStage()
    {
        // Check if TeleportManager exists in the scene to prevent null reference errors
        if (TeleportManager.Instance != null)
        {
            // Use the TeleportManager to handle the fade and scene load
            TeleportManager.Instance.LoadSceneWithFade(nextSceneName);
        }
        else
        {
            // Fallback just in case the TeleportManager is missing
            Debug.LogWarning("TeleportManager not found. Using the scene navigation fallback.");
            if (PhotonNetwork.InRoom) PhotonNetwork.LoadLevel(nextSceneName);
            else TeleportManager.Navigate(nextSceneName);
        }
    }

    public void ReturnToMenu()
    {
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
        TeleportManager.Navigate(mainMenuSceneName);
    }
}
