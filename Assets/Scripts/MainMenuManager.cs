using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;

/// <summary>
/// Carries the player's topic choice across scene loads.
/// Progress remains owned by the existing Firebase unlockedStage value.
/// </summary>
public static class StageSelectionState
{
    public const int FirstStage = 1;
    public const int LastStage = 10;

    public static int SelectedStage { get; private set; } = FirstStage;
    public static bool HasExplicitSelection { get; private set; }
    public static bool UsesEditorSpawnPoint { get; private set; }

    public static void Select(int stageNumber)
    {
        SelectedStage = Mathf.Clamp(stageNumber, FirstStage, LastStage);
        HasExplicitSelection = true;
        UsesEditorSpawnPoint = false;
    }

#if UNITY_EDITOR
    public static void SelectForEditorSpawn(int stageNumber)
    {
        Select(stageNumber);
        UsesEditorSpawnPoint = true;
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewSession()
    {
        SelectedStage = FirstStage;
        HasExplicitSelection = false;
        UsesEditorSpawnPoint = false;
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
    [SerializeField] private TermStageSelectionUI termStageSelection;
    [SerializeField] private UnityEngine.UI.Button stage1Button;
    [SerializeField] private UnityEngine.UI.Button stage2Button;
    [SerializeField] private UnityEngine.UI.Button stage3Button;
    [SerializeField] private GameObject stage2LockedUI;
    [SerializeField] private GameObject stage3LockedUI;
    [SerializeField] private string prelimSceneName = "PRELIM";

    // ---> NEW: Variable to remember the player's progress <---
    private int highestUnlockedStage = 1; 
    private bool isLoadingStage;
    private bool isLoadingProgress;
    private string progressMessage = "Complete each stage to unlock the next.";
    private int progressRequestVersion;

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

        // Keep topic buttons pending until this account's progress has arrived.
        highestUnlockedStage = StageSelectionState.FirstStage;
        isLoadingStage = false;
        isLoadingProgress = true;
        progressMessage = "Checking your progress...";
        if (termStageSelection != null) termStageSelection.ShowTerms();
        RefreshStageSelectionUI();
        LoadUnlockedStage();
    }

    public void SelectStage1() => SelectStage(1);
    public void SelectStage2() => SelectStage(2);
    public void SelectStage3() => SelectStage(3);

    public void SelectStage(int stageNumber)
    {
        if (isLoadingStage || isLoadingProgress || stageNumber < StageSelectionState.FirstStage ||
            stageNumber > StageSelectionState.LastStage || stageNumber > highestUnlockedStage)
        {
            return;
        }

        string targetScene = termStageSelection != null
            ? termStageSelection.GetSceneName(stageNumber)
            : stageNumber == 3 ? "RulesOfInference" : stageNumber <= 2 ? prelimSceneName : string.Empty;
        if (string.IsNullOrWhiteSpace(targetScene) || !Application.CanStreamedLevelBeLoaded(targetScene))
        {
            progressMessage = "This stage is unavailable.";
            RefreshStageSelectionUI();
            return;
        }

        isLoadingStage = true;
        RefreshStageSelectionUI();
        StageSelectionState.Select(stageNumber);

        SceneManager.LoadScene(targetScene);
    }

    private void LoadUnlockedStage()
    {
        int requestVersion = ++progressRequestVersion;
        FirebaseAuth auth = FirebaseAuth.DefaultInstance;
        if (auth == null || auth.CurrentUser == null)
        {
            Debug.LogWarning("No signed-in Firebase user. Only Stage 1 is available.");
            isLoadingProgress = false;
            progressMessage = "Sign in to save your stage progress.";
            RefreshStageSelectionUI();
            return;
        }

        string userId = auth.CurrentUser.UserId;
        FirebaseDatabase.DefaultInstance.RootReference
            .Child("users").Child(userId).Child("unlockedStage")
            .GetValueAsync().ContinueWithOnMainThread(task =>
            {
                // Ignore replies for a closed menu or a different signed-in account.
                if (this == null || requestVersion != progressRequestVersion ||
                    auth.CurrentUser == null || auth.CurrentUser.UserId != userId) return;

                isLoadingProgress = false;

                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning("Could not load unlockedStage. Keeping Stage 1 available.");
                    progressMessage = "Progress unavailable. Reopen this menu to retry.";
                    RefreshStageSelectionUI();
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

                progressMessage = "Complete each stage to unlock the next.";
                RefreshStageSelectionUI();
            });
    }

    private void RefreshStageSelectionUI()
    {
        if (termStageSelection != null)
            termStageSelection.Refresh(highestUnlockedStage, isLoadingProgress, isLoadingStage, progressMessage);
        if (stage1Button != null) stage1Button.interactable = !isLoadingStage;
        if (stage2Button != null) stage2Button.interactable = !isLoadingStage && highestUnlockedStage >= 2;
        if (stage3Button != null) stage3Button.interactable = !isLoadingStage && highestUnlockedStage >= 3;
        if (stage2LockedUI != null) stage2LockedUI.SetActive(highestUnlockedStage < 2);
        if (stage3LockedUI != null) stage3LockedUI.SetActive(highestUnlockedStage < 3);
    }

    private void SetStageSelectionVisible(bool visible)
    {
        if (!visible) progressRequestVersion++;
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
