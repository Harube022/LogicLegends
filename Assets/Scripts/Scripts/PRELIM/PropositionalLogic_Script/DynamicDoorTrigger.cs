using System.Collections;
using UnityEngine;
using TMPro;

public class DynamicDoorTrigger : MonoBehaviour
{
    [Header("Door Configuration")]
    [Tooltip("0 for Door 1, 1 for Door 2, 2 for Door 3, 3 for Door 4")]
    [SerializeField] private int doorIndex; 
    [SerializeField] private QuizManager quizManager;

    [Header("Success Route (Correct Answer)")]
    [SerializeField] private Transform successDestination;

    [Header("Hammer Trap Route (Wrong Answer)")]
    [SerializeField] private LevelTimerManager timerManager;
    [SerializeField] private float timeToDeduct = 10f;

    // NEW UI REFERENCES FOR PENALTY TEXT
    [Header("Penalty UI Overlay")]
    [Tooltip("Drag the TextMeshProUGUI component that will display the -10 text here")]
    [SerializeField] private TextMeshProUGUI penaltyTextUI;
    [Tooltip("How long the -10 text stays on screen before disappearing completely")]
    [SerializeField] private float fadeDuration = 1.5f;
    [Tooltip("How high the text floats upwards while fading")]
    [SerializeField] private float floatSpeed = 30f;
    
    [Tooltip("Drag the Animator component belonging to this door's hammer here")]
    [SerializeField] private Animator hammerAnimator;
    [SerializeField] private float knockbackDelay = 0.4f;
    [Tooltip("Total wrong-door recovery time, including hammer wind-up and knockback.")]
    [SerializeField, Min(0.1f)] private float stunDuration = 5f;
    [SerializeField] private float knockbackDistance = 3f;

    private bool isProcessingTrap = false;
    private static DynamicDoorTrigger activeTrap;
    private GameObject trappedPlayer;
    private Player trappedController;
    private CharacterController trappedCharacterController;
    private GameInput trappedInput;
    private bool playerControlBeforeTrap;
    private bool inputBlockedBeforeTrap;
    private bool stunAnimationActive;

    public int DoorIndex => doorIndex;

    private void Start()
    {
        // Ensure the penalty text starts hidden
        if (penaltyTextUI != null)
        {
            penaltyTextUI.gameObject.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (TeleportManager.Instance != null && TeleportManager.Instance.IsTransitioning) return;
        if (other.CompareTag("Player") && !isProcessingTrap && activeTrap == null)
        {
            if (quizManager == null) return;

            if (quizManager.IsChoiceCorrect(doorIndex))
            {
                Player playerController = other.GetComponent<Player>();
                quizManager.FinalizeChallengeCompletion();
                Transform nextChallengeDestination = quizManager.AdvanceToNextChallenge();

                if (nextChallengeDestination != null)
                {
                    TeleportPlayer(other.gameObject, nextChallengeDestination);
                }
                else if (!quizManager.IsSequenceComplete)
                {
                    Debug.LogError(
                        $"No spawn point is configured for the next randomized challenge. " +
                        $"Legacy fixed destination was '{(successDestination != null ? successDestination.name : "None")}'.",
                        this);
                }

                // The player has crossed the chosen doorway and reached the next challenge spawn.
                playerController?.EndGuidedMovement();
            }
            else
            {
                quizManager.PlayWrongDoorSound();
                StartCoroutine(HammerTrapSequence(other.gameObject));
            }
        }
    }

    private IEnumerator HammerTrapSequence(GameObject player)
    {
        isProcessingTrap = true;
        activeTrap = this;
        trappedPlayer = player;
        trappedController = player.GetComponent<Player>();
        trappedCharacterController = player.GetComponent<CharacterController>();
        trappedInput = GameInput.Instance != null ? GameInput.Instance : FindFirstObjectByType<GameInput>();
        playerControlBeforeTrap = trappedController != null && trappedController.enabled;
        inputBlockedBeforeTrap = trappedInput != null && trappedInput.GameplayInputBlocked;
        trappedInput?.SetGameplayInputBlocked(true);
        trappedController?.ToggleControl(false);

        if (timerManager != null) 
        {
            timerManager.DeductTime(timeToDeduct);

            // --- NEW: Trigger the fading -10 visual response ---
            if (penaltyTextUI != null)
            {
                StartCoroutine(FadeOutPenaltyText());
            }
        }

        if (ShouldAbortTrap())
        {
            FinishTrap(false);
            yield break;
        }

        if (hammerAnimator != null)
        {
            hammerAnimator.SetTrigger("Swing");
        }

        if (trappedCharacterController != null) trappedCharacterController.enabled = false;

        float windupElapsed = 0f;
        while (windupElapsed < knockbackDelay)
        {
            if (ShouldAbortTrap()) { FinishTrap(false); yield break; }
            windupElapsed += Time.deltaTime;
            yield return null;
        }

        Vector3 knockbackDirection = -transform.forward; 
        knockbackDirection.y = 0; 
        knockbackDirection.Normalize();

        float knockbackDuration = 0.25f; 
        float elapsed = 0f;
        Vector3 startPosition = player.transform.position;
        Vector3 targetPosition = startPosition + (knockbackDirection * knockbackDistance);

        while (elapsed < knockbackDuration)
        {
            if (ShouldAbortTrap()) { FinishTrap(false); yield break; }
            elapsed += Time.deltaTime;
            player.transform.position = Vector3.Lerp(startPosition, targetPosition, elapsed / knockbackDuration);
            yield return null; 
        }

        if (ShouldAbortTrap()) { FinishTrap(false); yield break; }

        // The voice is tied to the completed knockback movement, not the door selection or hammer wind-up.
        if (quizManager != null)
        {
            quizManager.PlayKnockbackVoiceSound();
        }

        SetStunAnimation(true);
        float remainingStunTime = Mathf.Max(0.1f, stunDuration - knockbackDelay - knockbackDuration);
        float stunElapsed = 0f;
        while (stunElapsed < remainingStunTime)
        {
            if (ShouldAbortTrap()) { FinishTrap(false); yield break; }
            stunElapsed += Time.deltaTime;
            yield return null;
        }

        FinishTrap(!ShouldAbortTrap());
    }

    private bool ShouldAbortTrap()
    {
        return trappedPlayer == null || (timerManager != null && timerManager.RemainingTime <= 0f);
    }

    private void SetStunAnimation(bool active)
    {
        stunAnimationActive = active;
        if (trappedPlayer == null) return;
        foreach (PlayerAnimator skin in trappedPlayer.GetComponentsInChildren<PlayerAnimator>(true))
            skin.SetStunned(active);
    }

    private void FinishTrap(bool completedNormally)
    {
        if (!isProcessingTrap) return;
        if (stunAnimationActive) SetStunAnimation(false);
        if (hammerAnimator != null) hammerAnimator.SetTrigger("Reset");
        if (trappedCharacterController != null) trappedCharacterController.enabled = true;

        PropositionalLogicTutorial tutorial = FindFirstObjectByType<PropositionalLogicTutorial>();
        if (!completedNormally && tutorial != null && tutorial.IsOpen)
            tutorial.CloseTutorial();
        bool mayRestoreControls = (timerManager == null || timerManager.RemainingTime > 0f) &&
                                  (tutorial == null || !tutorial.IsOpen);
        if (mayRestoreControls)
        {
            if (trappedInput != null) trappedInput.SetGameplayInputBlocked(inputBlockedBeforeTrap);
            if (trappedController != null) trappedController.ToggleControl(playerControlBeforeTrap);
        }

        if (completedNormally && mayRestoreControls)
        {
            quizManager?.ResetCurrentChallengeDoors();
            ResetAllBooks();
        }

        trappedPlayer = null;
        trappedController = null;
        trappedCharacterController = null;
        trappedInput = null;
        isProcessingTrap = false;
        if (activeTrap == this) activeTrap = null;
    }

    private void OnDisable()
    {
        if (!isProcessingTrap) return;
        StopAllCoroutines();
        if (penaltyTextUI != null) penaltyTextUI.gameObject.SetActive(false);
        FinishTrap(false);
    }

    // --- NEW COROUTINE: Handles floating up and fading out the text ---
    private IEnumerator FadeOutPenaltyText()
    {
        penaltyTextUI.gameObject.SetActive(true);
        
        // Dynamically match text to whatever timeToDeduct is set to (e.g., "-10")
        penaltyTextUI.text = $"-{timeToDeduct}";
        penaltyTextUI.color = new Color(1f, 0f, 0f, 1f); // Set to solid red

        // Store standard anchored layout positioning to return to later
        Vector2 originalPosition = penaltyTextUI.rectTransform.anchoredPosition;
        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime; // Use unscaled to ignore game pauses if necessary
            float normalizeTime = elapsedTime / fadeDuration;

            // Float position upwards
            penaltyTextUI.rectTransform.anchoredPosition += Vector2.up * floatSpeed * Time.deltaTime;

            // Smoothly lerp alpha down towards transparent
            Color textColor = penaltyTextUI.color;
            textColor.a = Mathf.Lerp(1f, 0f, normalizeTime);
            penaltyTextUI.color = textColor;

            yield return null;
        }

        // Clean closure cleanup
        penaltyTextUI.gameObject.SetActive(false);
        penaltyTextUI.rectTransform.anchoredPosition = originalPosition;
    }

    private void TeleportPlayer(GameObject player, Transform target)
    {
        TeleportManager.EnsureExists().StartTeleport(player, target);
    }

    private void ResetAllBooks()
    {
        BookInteract[] allBooks = Object.FindObjectsByType<BookInteract>(FindObjectsSortMode.None);
        foreach (BookInteract book in allBooks)
        {
            if (book != null) book.ResetInteraction();
        }
    }
}
