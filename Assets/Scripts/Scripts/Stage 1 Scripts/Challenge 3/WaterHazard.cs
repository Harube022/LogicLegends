using UnityEngine;

public class WaterHazard : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Check if the thing falling in the water is the player
        if (other.CompareTag("Player"))
        {
            // Tell the Level Manager to deduct a heart and teleport them!
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.LoseHeartAndRespawn();
            }
        }
    }
}