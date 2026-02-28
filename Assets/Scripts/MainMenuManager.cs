using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("Menu Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject playMenuPanel;
    [SerializeField] private GameObject shopMenuPanel;
    [SerializeField] private GameObject customizationMenuPanel;
    [SerializeField] private GameObject settingsMenuPanel;

    public void ShowMainMenu()
    {
        mainMenuPanel.SetActive(true);
        playMenuPanel.SetActive(false);
        shopMenuPanel.SetActive(false);
        customizationMenuPanel.SetActive(false);
        settingsMenuPanel.SetActive(false);
    }

    public void ShowPlayMenu()
    {
        mainMenuPanel.SetActive(false);
        playMenuPanel.SetActive(true);
        shopMenuPanel.SetActive(false);
        customizationMenuPanel.SetActive(false);
        settingsMenuPanel.SetActive(false);
    }

    public void ShowShopMenu()
    {
        mainMenuPanel.SetActive(false);
        playMenuPanel.SetActive(false);
        shopMenuPanel.SetActive(true);
        customizationMenuPanel.SetActive(false);
        settingsMenuPanel.SetActive(false);
    }

    public void ShowCustomizationMenu()
    {
        mainMenuPanel.SetActive(false);
        playMenuPanel.SetActive(false);
        shopMenuPanel.SetActive(false);
        customizationMenuPanel.SetActive(true);
        settingsMenuPanel.SetActive(false);
    }

    public void ShowSettingsMenu()
    {
        mainMenuPanel.SetActive(false);
        playMenuPanel.SetActive(false);
        shopMenuPanel.SetActive(false);
        customizationMenuPanel.SetActive(false);
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