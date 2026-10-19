using UnityEngine;
using UnityEngine.SceneManagement;

public partial class HealthManager : MonoBehaviour
{
    [Header("Health UI")]
    [SerializeField] private GameObject[] hearts; // Drag heart_1, heart_2, heart_3 here
    [SerializeField] private GameObject gameOverPanel; // Drag your Game Over UI Panel here

    private int currentHealth;

    private void Start()
    {
        // Set health to the number of hearts (3) and hide Game Over screen
        InitializeHealth();
        if (gameOverPanel != null) gameOverPanel.SetActive(currentHealth<=0);
    }

    public void TakeDamage()
    {
        InitializeHealth();
        if(useAuthoritativeActorHealth){DamageAuthoritativeActor(LocalActor);return;}
        if (currentHealth > 0)
        {
            currentHealth--;
            hearts[currentHealth].SetActive(false); // Hides the heart starting from the last one

            if (currentHealth <= 0)
            {
                TriggerGameOver();
            }
        }
    }

    private void TriggerGameOver()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        
        // Optional: Pause the game so they can't keep playing
        if(pauseOnGameOver && !useAuthoritativeActorHealth)Time.timeScale = 0f;
    }

    // --- Button Functions ---

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f; // Unpause before loading
        SceneManager.LoadScene("Main Menu"); // Replace with your actual Main Menu scene name
    }

    public void GoToLogicGarden()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("LogicGarden"); // Replace with your actual Logic Garden scene name
    }
}
