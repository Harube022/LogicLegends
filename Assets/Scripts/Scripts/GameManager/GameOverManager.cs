using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun; // 1. Added Photon

public class GameOverManager : MonoBehaviourPun
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

    }

    // Hook this up to your "Retry" Button
    public void RetryChallenge()
    {
        // 3. Send a message to EVERYONE to retry the challenge
        if (PhotonNetwork.InRoom && photonView != null)
        {
            photonView.RPC("RPC_RetryChallenge", RpcTarget.All);
        }
        else
        {
            RPC_RetryChallenge(); // Fallback for solo testing
        }
    }

    [PunRPC]
    public void RPC_RetryChallenge()
    {
        // Hide Game Over, Show Mobile Controls again for all players
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (gameplayInterfacePanel != null) gameplayInterfacePanel.SetActive(true);

        // Tell LevelManager to restart the challenge!
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.ResetFromGameOver();
        }
    }

    public void ReturnToMenu()
    {
        // In a real multiplayer game, you should disconnect from Photon before loading the main menu!
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
        SceneManager.LoadScene(mainMenuSceneName);
    }
}