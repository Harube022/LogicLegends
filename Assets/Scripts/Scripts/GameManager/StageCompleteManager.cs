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
    [SerializeField] private int baseGems = 40;
    [SerializeField] private int coinPenaltyPerHeart = 50;
    [SerializeField] private int gemPenaltyPerHeart = 5;

    [Header("Stage Progression")]
    [Tooltip("What stage number is this? (e.g., 1)")]
    [SerializeField] private int thisStageNumber = 1; 
    [Tooltip("The exact scene name of the NEXT stage")]
    [SerializeField] private string nextSceneName;
    [SerializeField] private string mainMenuSceneName = "Main Menu";

    public void TriggerStageComplete()
    {
        // 1. Freeze the game clock
        if (LevelManager.Instance != null) LevelManager.Instance.StopStageTimer();

        // 2. Swap the UI Panels
        if (gameplayInterfacePanel != null) gameplayInterfacePanel.SetActive(false);
        if (stageCompletePanel != null) stageCompletePanel.SetActive(true);

        // 3. Retrieve the Stats
        float timeTaken = LevelManager.Instance != null ? LevelManager.Instance.totalStageTime : 0f;
        int heartsLost = LevelManager.Instance != null ? LevelManager.Instance.totalHeartsLostThisStage : 0;

        // 4. Calculate Final Rewards (Mathf.Max ensures it never drops below 0!)
        int finalCoins = Mathf.Max(0, baseCoins - (heartsLost * coinPenaltyPerHeart));
        int finalGems = Mathf.Max(0, baseGems - (heartsLost * gemPenaltyPerHeart));

        // 5. Update the Text on Screen
        int minutes = Mathf.FloorToInt(timeTaken / 60);
        int seconds = Mathf.FloorToInt(timeTaken % 60);
        if (timeText != null) timeText.text = string.Format("{0:00}:{1:00}", minutes, seconds);

        if (heartsLostText != null) heartsLostText.text = heartsLost.ToString();
        if (coinsRewardText != null) coinsRewardText.text = "+" + finalCoins.ToString();
        if (gemsRewardText != null) gemsRewardText.text = "+" + finalGems.ToString();

        // 6. Push to Google Firebase
        SaveProgressToFirebase(finalCoins, finalGems);
    }

    private void SaveProgressToFirebase(int coinsEarned, int gemsEarned)
    {
        if (FirebaseAuth.DefaultInstance == null || FirebaseAuth.DefaultInstance.CurrentUser == null) return;

        string userId = FirebaseAuth.DefaultInstance.CurrentUser.UserId;
        DatabaseReference dbRef = FirebaseDatabase.DefaultInstance.RootReference;

        // A. Save the highest stage unlocked
        int nextStageUnlock = thisStageNumber + 1;
        dbRef.Child("users").Child(userId).Child("unlockedStage").GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted && task.Result.Exists)
            {
                int currentUnlocked = int.Parse(task.Result.Value.ToString());
                if (nextStageUnlock > currentUnlocked)
                {
                    dbRef.Child("users").Child(userId).Child("unlockedStage").SetValueAsync(nextStageUnlock);
                }
            }
            else
            {
                dbRef.Child("users").Child(userId).Child("unlockedStage").SetValueAsync(nextStageUnlock);
            }
        });

        // B. Add the newly earned Coins and Gems
        dbRef.Child("users").Child(userId).GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted && task.Result.Exists)
            {
                DataSnapshot snap = task.Result;
                int currentCoins = snap.HasChild("coins") ? int.Parse(snap.Child("coins").Value.ToString()) : 0;
                int currentGems = snap.HasChild("gems") ? int.Parse(snap.Child("gems").Value.ToString()) : 0;

                dbRef.Child("users").Child(userId).Child("coins").SetValueAsync(currentCoins + coinsEarned);
                dbRef.Child("users").Child(userId).Child("gems").SetValueAsync(currentGems + gemsEarned);
            }
        });
    }

    // --- BUTTON ACTIONS ---
    public void LoadNextStage()
    {
        if (PhotonNetwork.InRoom) PhotonNetwork.LoadLevel(nextSceneName);
        else SceneManager.LoadScene(nextSceneName);
    }

    public void ReturnToMenu()
    {
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
        SceneManager.LoadScene(mainMenuSceneName);
    }
}