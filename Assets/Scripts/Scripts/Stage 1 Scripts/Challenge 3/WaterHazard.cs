using UnityEngine;

public class WaterHazard : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Check if the thing falling in the water is the player
        if (other.CompareTag("Player"))
        {
            // ---> NEW: Turn their custom movement back on so they aren't stuck! <---
            Player playerScript = other.GetComponent<Player>();
            if (playerScript != null) playerScript.enabled = true;

            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
            
            // Tell the Level Manager to deduct a heart and teleport them!
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.LoseHeartAndRespawn();
            }
        }
    }
}