using UnityEngine;

public class TutorialNOTGateTrap : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Since this is an offline tutorial, we don't need network checks!
        // If the player touches the trigger, they immediately take damage.
        if (other.CompareTag("Player"))
        {
            DamagePlayer(other.transform);
        }
    }

    private void DamagePlayer(Transform playerTransform)
    {
        // Subtract 1 heart, update the UI, and respawn them at the stairs!
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.LoseHeartAndRespawn(playerTransform);
        }
    }
}