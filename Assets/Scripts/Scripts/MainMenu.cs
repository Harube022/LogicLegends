using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject playPanel;
    [SerializeField] private GameObject settingsPanel;

    public void OpenPlayPanel()
    {
        mainPanel.SetActive(false);
        playPanel.SetActive(true);
    }

    public void BackToMain()
    {
        playPanel.SetActive(false);
        settingsPanel.SetActive(false);
        mainPanel.SetActive(true);
    }

    public void OpenSettingsPanel()
    {
        mainPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void LoadSolo()
    {
        SceneManager.LoadScene("LogicGarden");
    }

    public void LoadCoop()
    {
        SceneManager.LoadScene("MAP1_LEVEL1");
    }
}