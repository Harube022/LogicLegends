using UnityEngine;

public class PlayerFootsteps : MonoBehaviour
{
    [Header("Audio Setup - Surfaces")]
    [SerializeField] private AudioClip grassSound;
    [SerializeField] private AudioClip concreteSound;
    [SerializeField] private AudioClip woodSound;
    [SerializeField] private AudioClip lilypadSound;
    [SerializeField] private AudioClip defaultSound;

    // NEW (Jump sound, same pattern as others)
    [Header("Audio Setup - Actions")]
    [SerializeField] private AudioClip jumpSound;

    [Header("Audio Polish (Like the Video!)")]
    [SerializeField, Range(0.7f, 1.3f)] private float minPitch = 0.9f;
    [SerializeField, Range(0.7f, 1.3f)] private float maxPitch = 1.1f;
    [SerializeField, Range(0f, 1f)] private float baseVolume = 0.8f;

    [Header("Footstep Settings")]
    [SerializeField] private float stepInterval = 0.5f;
    [SerializeField] private float rayDistance = 1.2f;

    private float stepTimer;
    private Vector3 lastPosition;

    // NEW (tracks grounded state)
    private bool wasGrounded;

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

        // NEW (Jump detection using same logic style)
        CharacterController controller = GetComponent<CharacterController>();
        if (controller != null)
        {
            // Detect jump: was grounded  now NOT grounded
            if (wasGrounded && !controller.isGrounded)
            {
                PlayJumpSound();
            }

            wasGrounded = controller.isGrounded;
        }
    }

    void CheckGroundAndPlaySound()
    {
        CharacterController controller = GetComponent<CharacterController>();
        if (controller == null) return;

        Vector3 capsuleBottom = transform.position + controller.center - (Vector3.up * (controller.height / 2f));
        Vector3 rayStart = capsuleBottom + (Vector3.up * 0.5f);
        float castDistance = 1.0f;

        // Create a layermask that excludes the "Player" layer
        int playerLayer = LayerMask.NameToLayer("Player");
        int layerMask = ~(1 << playerLayer); // Everything EXCEPT Player

        RaycastHit hit;
        if (Physics.Raycast(rayStart, Vector3.down, out hit, castDistance, layerMask))
        {
            Debug.Log($"<color=cyan>Footstep hit: {hit.collider.gameObject.name} | Tag: {hit.collider.tag}</color>");

            AudioClip clipToPlay = defaultSound;

            switch (hit.collider.tag)
            {
                case "Grass": clipToPlay = grassSound; break;
                case "Concrete": clipToPlay = concreteSound; break;
                case "Wood": clipToPlay = woodSound; break;
                case "Lilypad": clipToPlay = lilypadSound; break;
            }

            if (clipToPlay != null)
            {
                SpawnFootstepAudio(clipToPlay, hit.point);
            }
        }
        else
        {
            Debug.Log("<color=red>Footstep missed the ground entirely!</color>");
        }
    }

    //  NEW (Jump sound trigger using SAME audio system)
    void PlayJumpSound()
    {
        if (jumpSound == null) return;

        Vector3 spawnPosition = transform.position;
        SpawnFootstepAudio(jumpSound, spawnPosition);
    }

    // --- EXISTING AUDIO SYSTEM (UNCHANGED) ---
    void SpawnFootstepAudio(AudioClip clip, Vector3 spawnPosition)
    {
        GameObject audioObj = new GameObject("TempFootstepAudio");
        audioObj.transform.position = spawnPosition;

        AudioSource source = audioObj.AddComponent<AudioSource>();
        source.clip = clip;

        source.pitch = Random.Range(minPitch, maxPitch);
        source.volume = baseVolume * Random.Range(0.9f, 1.1f);

        source.spatialBlend = 1f;
        source.minDistance = 1f;
        source.maxDistance = 15f;

        source.Play();

        Destroy(audioObj, clip.length + 0.1f);
    }
}