using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;

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

    // ---> NEW: Variable to remember the player's progress <---
    private int highestUnlockedStage = 1; 

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
    }

    public void LoadSolo()
    {
        // ---> THE FIX: Fetch the database the exact moment they click the button! <---
        if (FirebaseAuth.DefaultInstance != null && FirebaseAuth.DefaultInstance.CurrentUser != null)
        {
            string userId = FirebaseAuth.DefaultInstance.CurrentUser.UserId;
            DatabaseReference dbRef = FirebaseDatabase.DefaultInstance.RootReference;

            dbRef.Child("users").Child(userId).Child("unlockedStage").GetValueAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsCompleted && task.Result.Exists)
                {
                    highestUnlockedStage = int.Parse(task.Result.Value.ToString());
                }
                
                string sceneToLoad = "Stage " + highestUnlockedStage;
                Debug.Log("Loading saved progress: " + sceneToLoad);
                SceneManager.LoadScene(sceneToLoad);
            });
        }
        else
        {
            // Fallback just in case they are offline
            SceneManager.LoadScene("Stage 1");
        }
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