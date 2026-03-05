using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class InteractPortal : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject teleportButton; // Now private, but visible in Inspector

    void Start()
    {
        // Make sure the button is hidden when the game starts
        if (teleportButton != null)
        {
            teleportButton.SetActive(false);
        }
    }

    // When the player enters the portal's trigger area
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Something touched the portal: " + other.gameObject.name);

        if (other.CompareTag("Player"))
        {
            Debug.Log("Player detected! Turning on button.");
            teleportButton.SetActive(true); 
        }
    }

    // When the player leaves the portal's trigger area
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            teleportButton.SetActive(false); // Hide the button
        }
    }

    // Method to call when the button is clicked
    // This MUST remain public so the UI Button can trigger it
    public void LoadTutorialScene()
    {
        SceneManager.LoadScene("TUTORIAL");
    }
}