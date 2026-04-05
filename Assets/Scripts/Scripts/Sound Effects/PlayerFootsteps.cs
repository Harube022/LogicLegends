using UnityEngine;

public class PlayerFootsteps : MonoBehaviour
{
    [Header("Audio Setup - Surfaces")]
    [SerializeField] private AudioClip grassSound;
    [SerializeField] private AudioClip concreteSound;
    [SerializeField] private AudioClip woodSound;
    [SerializeField] private AudioClip defaultSound;

    [Header("Audio Polish (Like the Video!)")]
    [SerializeField, Range(0.7f, 1.3f)] private float minPitch = 0.9f;
    [SerializeField, Range(0.7f, 1.3f)] private float maxPitch = 1.1f;
    [SerializeField, Range(0f, 1f)] private float baseVolume = 0.8f;

    [Header("Footstep Settings")]
    [SerializeField] private float stepInterval = 0.5f;
    [SerializeField] private float rayDistance = 1.2f;

    private float stepTimer;
    private Vector3 lastPosition;

    void Start()
    {
        lastPosition = transform.position;
    }

    void Update()
    {
        float distanceMoved = Vector3.Distance(transform.position, lastPosition);
        lastPosition = transform.position;

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
            stepTimer = 0f; 
        }
    }

    void CheckGroundAndPlaySound()
    {
        // 1. Try to get the CharacterController
        CharacterController controller = GetComponent<CharacterController>();
        if (controller == null) return; // Fail safe

        // 2. Calculate the exact bottom of the capsule
        Vector3 capsuleBottom = transform.position + controller.center - (Vector3.up * (controller.height / 2f));
        
        // 3. Start the ray slightly ABOVE the bottom to prevent clipping
        Vector3 rayStart = capsuleBottom + (Vector3.up * 0.5f);
        
        // 4. Shoot the ray down just far enough to clear the bottom (0.5 to reach bottom + 0.5 to hit floor)
        float castDistance = 1.0f; 

        RaycastHit hit;
        if (Physics.Raycast(rayStart, Vector3.down, out hit, castDistance))
        {
            Debug.Log($"<color=cyan>Footstep hit: {hit.collider.gameObject.name} | Tag: {hit.collider.tag}</color>");

            AudioClip clipToPlay = defaultSound;

            switch (hit.collider.tag)
            {
                case "Grass": clipToPlay = grassSound; break;
                case "Concrete": clipToPlay = concreteSound; break;
                case "Wood": clipToPlay = woodSound; break;
            }

            if (clipToPlay != null)
            {
                SpawnFootstepAudio(clipToPlay, hit.point);
            }
        }
        else
        {
             // If we miss, print this so we know the raycast fired but hit nothing!
             Debug.Log("<color=red>Footstep missed the ground entirely!</color>");
        }
    }

    // --- THIS IS THE MAGIC FROM THE VIDEO ---
    void SpawnFootstepAudio(AudioClip clip, Vector3 spawnPosition)
    {
        // 1. Create a temporary, invisible GameObject at the foot's impact point
        GameObject audioObj = new GameObject("TempFootstepAudio");
        audioObj.transform.position = spawnPosition;

        // 2. Add a speaker (AudioSource) to it
        AudioSource source = audioObj.AddComponent<AudioSource>();
        source.clip = clip;
        
        // 3. Add random variation to fix the "robotic" sound
        source.pitch = Random.Range(minPitch, maxPitch);
        source.volume = baseVolume * Random.Range(0.9f, 1.1f); // Slight volume variation too
        
        // 4. Make it full 3D Spatial Audio
        source.spatialBlend = 1f; 
        source.minDistance = 1f;
        source.maxDistance = 15f; 

        // 5. Play the sound
        source.Play();

        // 6. Destroy the temporary object immediately after the sound finishes playing!
        Destroy(audioObj, clip.length + 0.1f);
    }
}