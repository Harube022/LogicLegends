using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverManager : MonoBehaviour
{
    [Header("UI Panels")]
    [Tooltip("Drag your Game Over Canvas Panel here")]
    [SerializeField] private GameObject gameOverPanel;
    
    [Tooltip("Drag your Mobile Controls/Gameplay Interface Panel here")]
    [SerializeField] private GameObject gameplayInterfacePanel;

    [Header("Scene Settings")]
    [Tooltip("Type the exact name of your Main Menu scene here")]
    [SerializeField] private string mainMenuSceneName = "Main Menu";

    // Called by LevelManager when hearts hit 0
    public void ShowGameOver()
    {
        Debug.Log("Game Over! Showing panel and hiding controls.");
        
        // Show Game Over, Hide Mobile Controls
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (gameplayInterfacePanel != null) gameplayInterfacePanel.SetActive(false);

        // Pause the game for mobile
        Time.timeScale = 0f;
    }

    // Hook this up to your "Retry" Button
    public void RetryChallenge()
    {
        // 1. Unpause the game 
        Time.timeScale = 1f;

        // 2. Hide Game Over, Show Mobile Controls again
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (gameplayInterfacePanel != null) gameplayInterfacePanel.SetActive(true);

        // 3. Tell LevelManager to restart the challenge!
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.RestartCurrentChallenge();
        }
    }

    // Hook this up to your "Main Menu" Button
    public void ReturnToMenu()
    {
        // Always unpause before changing scenes!
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}