using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

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
        SceneManager.LoadScene("MAP1_LEVEL1");
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