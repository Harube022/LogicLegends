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
    [SerializeField] private float stunDuration = 5f;
    [SerializeField] private float knockbackDistance = 3f;

    private bool isProcessingTrap = false;

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
        if (other.CompareTag("Player") && !isProcessingTrap)
        {
            if (quizManager == null) return;

            if (quizManager.IsChoiceCorrect(doorIndex))
            {
                quizManager.FinalizeChallengeCompletion();
                quizManager.AdvanceToNextChallenge();
                
                if (successDestination != null) 
                {
                    TeleportPlayer(other.gameObject, successDestination);
                }
            }
            else
            {
                StartCoroutine(HammerTrapSequence(other.gameObject));
            }
        }
    }

    private IEnumerator HammerTrapSequence(GameObject player)
    {
        isProcessingTrap = true;

        if (timerManager != null) 
        {
            timerManager.DeductTime(timeToDeduct);

            // --- NEW: Trigger the fading -10 visual response ---
            if (penaltyTextUI != null)
            {
                StartCoroutine(FadeOutPenaltyText());
            }
        }

        if (hammerAnimator != null)
        {
            hammerAnimator.SetTrigger("Swing");
        }

        CharacterController charController = player.GetComponent<CharacterController>();
        if (charController != null) charController.enabled = false;

        yield return new WaitForSeconds(knockbackDelay);

        Vector3 knockbackDirection = -transform.forward; 
        knockbackDirection.y = 0; 
        knockbackDirection.Normalize();

        float knockbackDuration = 0.25f; 
        float elapsed = 0f;
        Vector3 startPosition = player.transform.position;
        Vector3 targetPosition = startPosition + (knockbackDirection * knockbackDistance);

        while (elapsed < knockbackDuration)
        {
            elapsed += Time.deltaTime;
            player.transform.position = Vector3.Lerp(startPosition, targetPosition, elapsed / knockbackDuration);
            yield return null; 
        }

        float remainingStunTime = stunDuration - knockbackDelay - knockbackDuration;
        if (remainingStunTime > 0)
        {
            yield return new WaitForSeconds(remainingStunTime);
        }

        if (hammerAnimator != null)
        {
            hammerAnimator.SetTrigger("Reset");
        }

        if (charController != null) charController.enabled = true;

        if (quizManager != null) quizManager.ResetCurrentChallengeDoors();

        ResetAllBooks();

        isProcessingTrap = false;
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
        CharacterController charController = player.GetComponent<CharacterController>();
        if (charController != null) charController.enabled = false;

        player.transform.position = target.position;
        player.transform.rotation = target.rotation;

        if (charController != null) charController.enabled = true;
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