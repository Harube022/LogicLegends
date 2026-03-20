using UnityEngine;

public class BridgeTrigger : MonoBehaviour
{ 
    [Header("Debug")]
    [Tooltip("Watch this checkmark in the Inspector to see when it fires!")]
    [SerializeField] private bool hasFinished = false; 

    // This handles it if the bridge is solid (Is Trigger is UNCHECKED)
    private void OnCollisionEnter(Collision collision)
    {
        if (!hasFinished && collision.gameObject.CompareTag("Player"))
        {
            CompleteChallenge();
        }
    }

    // This handles it if the bridge is a trigger (Is Trigger is CHECKED)
    private void OnTriggerEnter(Collider other)
    {
        if (!hasFinished && other.CompareTag("Player"))
        {
            CompleteChallenge();
        }
    }

    private void CompleteChallenge()
    {
        hasFinished = true; // Make sure this only triggers once!

        // 1. Stop the timer!
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.StopTimer();
            LevelManager.Instance.HideTimer();
            Debug.Log("Challenge 3 Complete! Timer Stopped.");
        }

        // 2. Destroy the object so the player won't bump into it anymore!
        Destroy(gameObject);
    }
}