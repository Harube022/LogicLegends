using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;

/// <summary>
/// Carries the player's stage choice across the Main Menu -> PRELIM scene load.
/// Progress remains owned by the existing Firebase unlockedStage value.
/// </summary>
public static class StageSelectionState
{
    public const int FirstStage = 1;
    public const int LastStage = 3;

    public static int SelectedStage { get; private set; } = FirstStage;

    public static void Select(int stageNumber)
    {
        SelectedStage = Mathf.Clamp(stageNumber, FirstStage, LastStage);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewSession()
    {
        SelectedStage = FirstStage;
    }
}

public class MainMenuManager : MonoBehaviour
{
    [Header("Menu Panels")]
    [SerializeField] private GameObject loginMenuPanel;
    [SerializeField] private GameObject signUpMenuPanel;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject playMenuPanel;
    [SerializeField] private GameObject shopMenuPanel;
    [SerializeField] private GameObject customizationMenuPanel;
    [SerializeField] private GameObject achievementsMenuPanel;
    [SerializeField] private GameObject settingsMenuPanel;
    [SerializeField] private GameObject stageSelectionPanel;

    [Header("Stage Selection")]
    [SerializeField] private UnityEngine.UI.Button stage1Button;
    [SerializeField] private UnityEngine.UI.Button stage2Button;
    [SerializeField] private UnityEngine.UI.Button stage3Button;
    [SerializeField] private GameObject stage2LockedUI;
    [SerializeField] private GameObject stage3LockedUI;
    [SerializeField] private string prelimSceneName = "PRELIM";

    // ---> NEW: Variable to remember the player's progress <---
    private int highestUnlockedStage = 1; 
    private bool isLoadingStage;

    private void Start()
    {
        RefreshStageSelectionUI();
    }

    public void ShowMainMenu()
    {
        loginMenuPanel.SetActive(false);
        signUpMenuPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
        playMenuPanel.SetActive(false);
        shopMenuPanel.SetActive(false);
        customizationMenuPanel.SetActive(false);
        achievementsMenuPanel.SetActive(false);
        settingsMenuPanel.SetActive(false);
        SetStageSelectionVisible(false);
    }

    public void ShowLoginMenu()
    {
        loginMenuPanel.SetActive(true);
        signUpMenuPanel.SetActive(false);
        mainMenuPanel.SetActive(false);
        playMenuPanel.SetActive(false);
        shopMenuPanel.SetActive(false);
        customizationMenuPanel.SetActive(false);
        achievementsMenuPanel.SetActive(false);
        settingsMenuPanel.SetActive(false);
        SetStageSelectionVisible(false);
    }

    public void ShowSignUpMenu()
    {
        loginMenuPanel.SetActive(false);
        signUpMenuPanel.SetActive(true);
        mainMenuPanel.SetActive(false);
        playMenuPanel.SetActive(false);
        shopMenuPanel.SetActive(false);
        customizationMenuPanel.SetActive(false);
        achievementsMenuPanel.SetActive(false);
        settingsMenuPanel.SetActive(false);
        SetStageSelectionVisible(false);
    }

    public void ShowPlayMenu()
    {
        loginMenuPanel.SetActive(false);
        signUpMenuPanel.SetActive(false);
        mainMenuPanel.SetActive(false);
        playMenuPanel.SetActive(true);
        shopMenuPanel.SetActive(false);
        customizationMenuPanel.SetActive(false);
        achievementsMenuPanel.SetActive(false);
        settingsMenuPanel.SetActive(false);
        SetStageSelectionVisible(false);
    }

    public void ShowShopMenu()
    {
        loginMenuPanel.SetActive(false);
        signUpMenuPanel.SetActive(false);
        mainMenuPanel.SetActive(false);
        playMenuPanel.SetActive(false);
        shopMenuPanel.SetActive(true);
        customizationMenuPanel.SetActive(false);
        achievementsMenuPanel.SetActive(false);
        settingsMenuPanel.SetActive(false);
        SetStageSelectionVisible(false);
    }

    public void ShowCustomizationMenu()
    {
        loginMenuPanel.SetActive(false);
        signUpMenuPanel.SetActive(false);
        mainMenuPanel.SetActive(false);
        playMenuPanel.SetActive(false);
        shopMenuPanel.SetActive(false);
        customizationMenuPanel.SetActive(true);
        achievementsMenuPanel.SetActive(false);
        settingsMenuPanel.SetActive(false);
        SetStageSelectionVisible(false);
    }

    public void ShowAchievementsMenu()
    {
        loginMenuPanel.SetActive(false);
        signUpMenuPanel.SetActive(false);
        mainMenuPanel.SetActive(false);
        playMenuPanel.SetActive(false);
        shopMenuPanel.SetActive(false);
        customizationMenuPanel.SetActive(false);
        achievementsMenuPanel.SetActive(true);
        settingsMenuPanel.SetActive(false);
        SetStageSelectionVisible(false);
    }

    public void ShowSettingsMenu()
    {
        loginMenuPanel.SetActive(false);
        signUpMenuPanel.SetActive(false);
        mainMenuPanel.SetActive(false);
        playMenuPanel.SetActive(false);
        shopMenuPanel.SetActive(false);
        customizationMenuPanel.SetActive(false);
        achievementsMenuPanel.SetActive(false);
        settingsMenuPanel.SetActive(true);
        SetStageSelectionVisible(false);
    }

    public void LoadSolo()
    {
        ShowStageSelection();
    }

    public void ShowStageSelection()
    {
        if (stageSelectionPanel == null)
        {
            Debug.LogError("MainMenuManager: Stage Selection Panel is not assigned.");
            return;
        }

        loginMenuPanel.SetActive(false);
        signUpMenuPanel.SetActive(false);
        mainMenuPanel.SetActive(false);
        playMenuPanel.SetActive(false);
        shopMenuPanel.SetActive(false);
        customizationMenuPanel.SetActive(false);
        achievementsMenuPanel.SetActive(false);
        settingsMenuPanel.SetActive(false);
        SetStageSelectionVisible(true);

        // Stage 1 is always available while the persisted value is loading.
        highestUnlockedStage = StageSelectionState.FirstStage;
        isLoadingStage = false;
        RefreshStageSelectionUI();
        LoadUnlockedStage();
    }

    public void SelectStage1() => SelectStage(1);
    public void SelectStage2() => SelectStage(2);
    public void SelectStage3() => SelectStage(3);

    public void SelectStage(int stageNumber)
    {
        if (isLoadingStage || stageNumber < StageSelectionState.FirstStage ||
            stageNumber > StageSelectionState.LastStage || stageNumber > highestUnlockedStage)
        {
            return;
        }

        isLoadingStage = true;
        RefreshStageSelectionUI();
        StageSelectionState.Select(stageNumber);
        SceneManager.LoadScene(prelimSceneName);
    }

    private void LoadUnlockedStage()
    {
        FirebaseAuth auth = FirebaseAuth.DefaultInstance;
        if (auth == null || auth.CurrentUser == null)
        {
            Debug.LogWarning("No signed-in Firebase user. Only Stage 1 is available.");
            return;
        }

        string userId = auth.CurrentUser.UserId;
        FirebaseDatabase.DefaultInstance.RootReference
            .Child("users").Child(userId).Child("unlockedStage")
            .GetValueAsync().ContinueWithOnMainThread(task =>
            {
                // The player may choose Stage 1 before Firebase finishes and leave this scene.
                if (this == null) return;

                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning("Could not load unlockedStage. Keeping Stage 1 available.");
                    return;
                }

                if (task.Result.Exists && task.Result.Value != null &&
                    int.TryParse(task.Result.Value.ToString(), out int savedStage))
                {
                    highestUnlockedStage = Mathf.Clamp(
                        savedStage,
                        StageSelectionState.FirstStage,
                        StageSelectionState.LastStage);
                }

                RefreshStageSelectionUI();
            });
    }

    private void RefreshStageSelectionUI()
    {
        if (stage1Button != null) stage1Button.interactable = !isLoadingStage;
        if (stage2Button != null) stage2Button.interactable = !isLoadingStage && highestUnlockedStage >= 2;
        if (stage3Button != null) stage3Button.interactable = !isLoadingStage && highestUnlockedStage >= 3;
        if (stage2LockedUI != null) stage2LockedUI.SetActive(highestUnlockedStage < 2);
        if (stage3LockedUI != null) stage3LockedUI.SetActive(highestUnlockedStage < 3);
    }

    private void SetStageSelectionVisible(bool visible)
    {
        if (stageSelectionPanel != null) stageSelectionPanel.SetActive(visible);
    }

    public void LoadLogicGarden()
    {
        SceneManager.LoadScene("LogicGarden");
    }

    public void QuitGame()
    {
        Application.Quit();
        

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
