using UnityEngine;

public class BoulderAudio : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip pushLoopClip;

    [Header("Settings")]
    [SerializeField] private float fadeSpeed = 5f;
    [Tooltip("How fast the boulder needs to move to trigger the sound")]
    [SerializeField] private float minimumMoveSpeed = 0.1f;

    private float targetVolume = 0f;
    private Rigidbody rb;

    private void Start()
    {
        // Automatically grab the Rigidbody attached to the boulder
        rb = GetComponent<Rigidbody>();

        if (audioSource != null)
        {
            audioSource.clip = pushLoopClip;
            audioSource.loop = true;
            audioSource.volume = 0f;
            audioSource.playOnAwake = false;
        }
    }

    private void Update()
    {
        if (audioSource == null || rb == null) return;

        // 1. Check if the boulder is currently moving
        if (rb.linearVelocity.magnitude > minimumMoveSpeed)
        {
            targetVolume = 1f; // Turn volume up
        }
        else
        {
            targetVolume = 0f; // Turn volume down
        }

        // 2. Smoothly fade the volume
        audioSource.volume = Mathf.Lerp(audioSource.volume, targetVolume, Time.deltaTime * fadeSpeed);

        // 3. Start playing the audio clip if the volume is rising
        if (!audioSource.isPlaying && audioSource.volume > 0.01f)
        {
            audioSource.Play();
        }

        // 4. Stop the audio entirely when silent to save system resources
        if (audioSource.volume < 0.01f && targetVolume == 0f)
        {
            audioSource.Stop();
        }
    }
}