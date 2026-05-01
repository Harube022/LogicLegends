using UnityEngine;

public class WaterSplashTrigger : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip splashSound;
    [SerializeField, Range(0f, 1f)] private float splashVolume = 0.8f;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // ---> UPDATED: Play in 2D using the assigned AudioSource <---
            if (audioSource != null && splashSound != null)
            {
                audioSource.PlayOneShot(splashSound, splashVolume);
            }
        }
    }
}