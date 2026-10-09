using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;

public class GameOverManager : MonoBehaviourPun
{
    [Header("UI Panels")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject gameplayInterfacePanel;

    [Header("Scene Settings")]
    [SerializeField] private string mainMenuSceneName = "Main Menu";

    //  NEW (Audio Setup)
    [Header("Audio")]
    [SerializeField] private AudioClip gameOverSound;
    [SerializeField, Range(0f, 1f)] private float gameOverVolume = 1f;

    // Called by LevelManager when hearts hit 0
    public void ShowGameOver()
    {
        Debug.Log("Game Over! Showing panel and hiding controls.");

        // EXISTING LOGIC (UNCHANGED)
        if (StageJourneyUI.Instance != null && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "RulesOfInference")
            StageJourneyUI.Instance.ShowFailure(3, "You ran out of hearts.\nRestart the stage to build a new argument.", () =>
            {
                Time.timeScale = 1;
                GameInput.Instance?.SetGameplayInputBlocked(false);
                StageSelectionState.Select(3);
                SceneManager.LoadScene("RulesOfInference");
            });
        else if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (gameplayInterfacePanel != null) gameplayInterfacePanel.SetActive(false);

        if (gameOverPanel == null)
        {
            SetsUIController setsUI = FindFirstObjectByType<SetsUIController>();
            if (setsUI != null) setsUI.ShowGameOver();
        }

        // NEW (Play sound when game over appears)
        PlayGameOverSound();
    }

    // NEW (Same audio pattern as your other systems)
    private void PlayGameOverSound()
    {
        if (gameOverSound == null) return;

        Vector3 soundPosition = transform.position;

        SpawnGameOverAudio(gameOverSound, soundPosition);
    }

    private void SpawnGameOverAudio(AudioClip clip, Vector3 position)
    {
        GameObject audioObj = new GameObject("TempGameOverAudio");
        audioObj.transform.position = position;

        AudioSource source = audioObj.AddComponent<AudioSource>();
        AudioVolumeSettings.Route(source, GameAudioChannel.SoundFX);
        source.clip = clip;

        source.pitch = Random.Range(0.98f, 1.02f);
        source.volume = gameOverVolume;

        //  IMPORTANT: Make this 2D so it's ALWAYS heard
        source.spatialBlend = 0f;

        // Optional safety (if timeScale = 0 somewhere)
        source.ignoreListenerPause = true;

        source.Play();

        Destroy(audioObj, clip.length + 0.1f);
    }

    // --- EXISTING METHODS (UNCHANGED) ---

    public void RetryChallenge()
    {
        if (PhotonNetwork.InRoom && photonView != null)
        {
            photonView.RPC("RPC_RetryChallenge", RpcTarget.All);
        }
        else
        {
            RPC_RetryChallenge();
        }
    }

    [PunRPC]
    public void RPC_RetryChallenge()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (gameplayInterfacePanel != null) gameplayInterfacePanel.SetActive(true);

        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.ResetFromGameOver();
        }
    }

    public void ReturnToMenu()
    {
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
