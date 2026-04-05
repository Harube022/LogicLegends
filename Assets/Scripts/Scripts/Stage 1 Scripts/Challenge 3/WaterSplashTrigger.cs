using UnityEngine;

public class WaterSplashTrigger : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioClip splashSound;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (splashSound != null)
            {
                // Spawns the sound exactly where the player hit the water
                AudioSource.PlayClipAtPoint(splashSound, other.transform.position);
            }
        }
    }
}