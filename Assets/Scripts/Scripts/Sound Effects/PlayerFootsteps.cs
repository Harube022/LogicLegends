using UnityEngine;

public class PlayerFootsteps : MonoBehaviour
{
    [Header("Audio Setup")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip grassSound;

    [Header("Footstep Settings")]
    [SerializeField] private float stepInterval = 0.5f;
    [SerializeField] private float rayDistance = 1.2f;

    private float stepTimer;
    private Vector3 lastPosition;

    void Start()
    {
        // Record our starting position
        lastPosition = transform.position;
    }

    void Update()
    {
        // 1. Calculate how far the player actually moved this frame
        float distanceMoved = Vector3.Distance(transform.position, lastPosition);
        
        // 2. Update the last position for the next frame
        lastPosition = transform.position;

        // 3. If the distance moved is greater than a tiny threshold, we are walking!
        // (We use 0.001f to ignore tiny physics jitters)
        bool isMoving = distanceMoved > 0.001f;

        if (isMoving)
        {
            stepTimer -= Time.deltaTime;

            if (stepTimer <= 0f)
            {
                CheckGroundAndPlaySound();
                stepTimer = stepInterval; 
            }
        }
        else
        {
            // Reset timer so the first step happens immediately when moving again
            stepTimer = 0f; 
        }
    }

    void CheckGroundAndPlaySound()
    {
        RaycastHit hit;
        Vector3 rayStart = transform.position;
        
        if (Physics.Raycast(rayStart, Vector3.down, out hit, rayDistance))
        {
            if (hit.collider.CompareTag("Grass"))
            {
                audioSource.PlayOneShot(grassSound);
            }
        }
    }
}