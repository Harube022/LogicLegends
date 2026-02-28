using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelMenu : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;

    public void OpenPause()
    {
        pausePanel.SetActive(true);
        Time.timeScale = 0f; // pause game
    }

    public void ClosePause()
    {
        pausePanel.SetActive(false);
        Time.timeScale = 1f; // resume game
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Main Menu");
    }

    public void QuitGame()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}