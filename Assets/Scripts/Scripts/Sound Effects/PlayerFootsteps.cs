using UnityEngine;
using UnityEngine.Audio;

public class PlayerFootsteps : MonoBehaviour
{
    [Header("Audio Setup - Surfaces")]
    [SerializeField] private AudioClip grassSound;
    [SerializeField] private AudioClip concreteSound;
    [SerializeField] private AudioClip woodSound;
    [SerializeField] private AudioClip lilypadSound;
    [SerializeField] private AudioClip defaultSound;

    [Header("Audio Setup - Actions")]
    [SerializeField] private AudioClip jumpSound;
    [SerializeField] private AudioClip runningSound;
    [Tooltip("Optional sound-effects mixer routing for movement audio.")]
    [SerializeField] private AudioMixerGroup outputMixerGroup;

    [Header("Movement Audio")]
    [SerializeField, Range(0.7f, 1.3f)] private float minPitch = 0.9f;
    [SerializeField, Range(0.7f, 1.3f)] private float maxPitch = 1.1f;
    [SerializeField, Range(0f, 1f)] private float baseVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float runningVolumeMultiplier = 0.85f;

    [Header("Footstep Settings")]
    [SerializeField, Min(0.05f)] private float stepInterval = 0.5f;
    [SerializeField, Min(0.1f)] private float rayDistance = 1.2f;

    private Player player;
    private CharacterController controller;
    private AudioSource actionSource;
    private AudioSource runningSource;
    private float stepTimer;
    private Vector3 lastPosition;

    private void Awake()
    {
        player = GetComponent<Player>();
        controller = GetComponent<CharacterController>();
        actionSource = CreateSource("MovementActions2D", false);
        runningSource = CreateSource("RunningSteps2D", true);
    }

    private AudioSource CreateSource(string objectName, bool loop)
    {
        var audioObject = new GameObject(objectName);
        audioObject.transform.SetParent(transform, false);
        var source = audioObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.loop = loop;
        source.outputAudioMixerGroup = outputMixerGroup;
        if (outputMixerGroup == null) AudioVolumeSettings.Route(source, GameAudioChannel.SoundFX);
        return source;
    }

    private void OnEnable()
    {
        lastPosition = transform.position;
        if (player != null) player.OnJumpStarted += PlayJumpSound;
    }

    private void OnDisable()
    {
        if (player != null) player.OnJumpStarted -= PlayJumpSound;
        StopMovementAudio();
    }

    // Runs after Player.Update so movement and grounding describe this frame.
    private void LateUpdate()
    {
        Vector3 movement = transform.position - lastPosition;
        lastPosition = transform.position;
        movement.y = 0f;
        if (!CanPlay() || !controller.isGrounded || !player.IsWalking() ||
            movement.sqrMagnitude <= 0.000001f ||
            movement.magnitude > Mathf.Max(1f, player.moveSpeed * Time.deltaTime * 4f))
        {
            StopMovementAudio();
            return;
        }

        if (player.IsRunning() && runningSound != null)
        {
            if (!runningSource.isPlaying)
            {
                actionSource.Stop();
                runningSource.clip = runningSound;
                runningSource.pitch = 1f;
                runningSource.Play();
            }
            runningSource.volume = baseVolume * runningVolumeMultiplier;
            stepTimer = 0f;
            return;
        }

        runningSource.Stop();
        stepTimer -= Time.deltaTime;
        if (stepTimer <= 0f)
        {
            CheckGroundAndPlaySound();
            stepTimer = Mathf.Max(0.05f, stepInterval) / (player.IsRunning() ? 1.5f : 1f);
        }
    }

    private bool CanPlay()
    {
        // A remote player's movement must not play full-volume 2D sounds locally.
        return player != null && Player.LocalInstance == player && player.enabled &&
            controller != null && controller.enabled && Time.timeScale > 0f &&
            (GameInput.Instance == null || !GameInput.Instance.GameplayInputBlocked);
    }

    private void CheckGroundAndPlaySound()
    {
        Vector3 capsuleBottom = transform.TransformPoint(controller.center) -
            Vector3.up * (controller.height * transform.lossyScale.y / 2f);
        Vector3 rayStart = capsuleBottom + Vector3.up * 0.5f;
        int playerLayer = LayerMask.NameToLayer("Player");
        int layerMask = playerLayer >= 0 ? ~(1 << playerLayer) : ~0;
        RaycastHit hit;
        AudioClip clip = defaultSound;
        if (Physics.Raycast(rayStart, Vector3.down, out hit, rayDistance, layerMask, QueryTriggerInteraction.Ignore))
        {
            switch (hit.collider.tag)
            {
                case "Grass": clip = grassSound; break;
                case "Concrete": clip = concreteSound; break;
                case "Wood": clip = woodSound; break;
                case "Lilypad": clip = lilypadSound; break;
            }
        }
        PlayAction(clip != null ? clip : defaultSound);
    }

    private void PlayJumpSound()
    {
        if (!CanPlay()) return;
        runningSource.Stop();
        actionSource.Stop();
        stepTimer = 0f;
        PlayAction(jumpSound);
    }

    private void PlayAction(AudioClip clip)
    {
        if (clip == null) return;
        actionSource.pitch = Random.Range(minPitch, maxPitch);
        actionSource.volume = baseVolume;
        actionSource.PlayOneShot(clip);
    }

    private void StopMovementAudio()
    {
        stepTimer = 0f;
        if (runningSource != null) runningSource.Stop();
        // Let an actual jump finish in the air, but silence all audio during a modal pause.
        if (actionSource != null && !CanPlay()) actionSource.Stop();
    }

    public void SetVolume(float volume)
    {
        baseVolume = Mathf.Clamp01(volume);
        if (actionSource != null) actionSource.volume = baseVolume;
        if (runningSource != null) runningSource.volume = baseVolume * runningVolumeMultiplier;
    }
}