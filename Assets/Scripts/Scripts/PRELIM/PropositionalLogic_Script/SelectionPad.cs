// using System.Collections;
// using UnityEngine;

// public class SelectionPad : MonoBehaviour
// {
//     [Header("Pad Settings")]
//     [Tooltip("0 for Door 1, 1 for Door 2, etc. Must match the door index configuration.")]
//     [SerializeField] private int padIndex;
//     [SerializeField] private float timeRequiredToSelect = 2.0f;
    
//     [Tooltip("Drag the physical Door GameObject that blocks the pathway here")]
//     [SerializeField] private GameObject doorVisualObject;

//     [Header("World UI Positioning")]
//     [Tooltip("An empty GameObject positioned slightly above the center of this pad where the loader should float")]
//     [SerializeField] private Transform uiAnchorPoint;

//     private float currentChargeTime = 0f;
//     private Coroutine chargeCoroutine;
//     private QuizManager quizManager;

//     public int PadIndex => padIndex;

//     private void Start()
//     {
//         // Cache our manager so we don't have to find it constantly
//         quizManager = Object.FindAnyObjectByType<QuizManager>();
//     }

//     private void OnTriggerEnter(Collider other)
//     {
//         if (other.CompareTag("Player") && quizManager != null)
//         {
//             // NEW CONDITION: Only prepare and begin charging if the quiz is actively showing!
//             if (quizManager.IsQuizActive)
//             {
//                 quizManager.PrepareSharedLoader(uiAnchorPoint != null ? uiAnchorPoint : transform);
                
//                 if (chargeCoroutine != null) StopCoroutine(chargeCoroutine);
//                 chargeCoroutine = StartCoroutine(ChargeSelection());
//             }
//         }
//     }

//     private void OnTriggerExit(Collider other)
//     {
//         if (other.CompareTag("Player"))
//         {
//             StopChargingSequence();
//         }
//     }

//     private IEnumerator ChargeSelection()
//     {
//         currentChargeTime = 0f;
//         while (currentChargeTime < timeRequiredToSelect)
//         {
//             // SAFETY: If the player is standing on the pad but the question UI vanishes, abort charging immediately
//             if (quizManager == null || !quizManager.IsQuizActive)
//             {
//                 StopChargingSequence();
//                 yield break;
//             }
            
//             currentChargeTime += Time.deltaTime;
            
//             if (quizManager != null)
//             {
//                 quizManager.UpdateSharedLoaderFill(currentChargeTime / timeRequiredToSelect);
//             }
//             yield return null;
//         }

//         // 1. Immediately hide the quiz panel when the pad finishes charging
//         if (quizManager != null)
//         {
//             quizManager.ClearQuizUI();
//         }

//         // 2. Open the physical door pathway obstruction
//         if (doorVisualObject != null)
//         {
//             doorVisualObject.SetActive(false);
//         }

//         StopChargingSequence();
//     }

//     private void StopChargingSequence()
//     {
//         if (chargeCoroutine != null) StopCoroutine(chargeCoroutine);
//         currentChargeTime = 0f;
        
//         if (quizManager != null)
//         {
//             quizManager.HideSharedLoader();
//         }
//     }
// }

using System.Collections;
using UnityEngine;

public class SelectionPad : MonoBehaviour
{
    [Header("Pad Settings")]
    [Tooltip("0 for Door 1, 1 for Door 2, etc. Must match the door index configuration.")]
    [SerializeField] private int padIndex;
    [SerializeField] private float timeRequiredToSelect = 2.0f;

    [Header("Confirmed Door Guidance")]
    [Tooltip("Movement speed while the player is automatically guided through the confirmed door.")]
    [SerializeField] private float guidedMovementSpeed = 8.0f;
    
    [Tooltip("Drag the physical Door GameObject that blocks the pathway here")]
    [SerializeField] private GameObject doorVisualObject;

    [Header("World UI Positioning")]
    [Tooltip("An empty GameObject positioned slightly above the center of this pad where the loader should float")]
    [SerializeField] private Transform uiAnchorPoint;

    private float currentChargeTime = 0f;
    private Coroutine chargeCoroutine;
    private QuizManager quizManager;

    public int PadIndex => padIndex;

    private void Start()
    {
        quizManager = Object.FindFirstObjectByType<QuizManager>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && quizManager != null)
        {
            if (quizManager.IsQuizActive)
            {
                quizManager.PrepareSharedLoader(uiAnchorPoint != null ? uiAnchorPoint : transform);
                
                if (chargeCoroutine != null) StopCoroutine(chargeCoroutine);
                chargeCoroutine = StartCoroutine(ChargeSelection(other.gameObject));
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            StopChargingSequence();
        }
    }

    private IEnumerator ChargeSelection(GameObject playerObject)
    {
        currentChargeTime = 0f;
        while (currentChargeTime < timeRequiredToSelect)
        {
            if (quizManager == null || !quizManager.IsQuizActive)
            {
                StopChargingSequence();
                yield break;
            }
            
            currentChargeTime += Time.deltaTime;
            
            if (quizManager != null)
            {
                quizManager.UpdateSharedLoaderFill(currentChargeTime / timeRequiredToSelect);
            }
            yield return null;
        }

        if (quizManager == null || !quizManager.IsQuizActive)
        {
            StopChargingSequence();
            yield break;
        }

        // The answer is confirmed when the selection pad fills, before guided
        // movement to the doorway. Play success here once for this selection.
        bool correct = quizManager.IsChoiceCorrect(padIndex);
        quizManager.ShowAnswerFeedback(correct);
        if (correct) quizManager.PlayCorrectDoorSound();
        quizManager.ClearQuizUI();

        DynamicDoorTrigger selectedDoorTrigger = FindSelectedDoorTrigger();
        Player player = playerObject != null ? playerObject.GetComponent<Player>() : null;

        if (selectedDoorTrigger == null || player == null)
        {
            Debug.LogWarning(
                $"{name} could not start the selected door route because its matching door trigger or Player component was not found.",
                this);
        }
        else
        {
            // Door models in PRELIM are static and have no open animation. The
            // existing doorway-open behavior removes the selected model so the
            // player can pass through. The wrong-answer trigger restores it after
            // the hammer/retry sequence.
            if (doorVisualObject != null)
            {
                doorVisualObject.SetActive(false);
            }

            // Correctness is resolved by DynamicDoorTrigger only after the player
            // reaches the selected doorway. Both routes use the same movement path;
            // only a wrong trigger runs the hammer trap.
            player.BeginGuidedMovement(selectedDoorTrigger.transform, guidedMovementSpeed);
        }

        StopChargingSequence();
    }

    private DynamicDoorTrigger FindSelectedDoorTrigger()
    {
        DynamicDoorTrigger closestMatch = null;
        float closestDistance = float.PositiveInfinity;

        DynamicDoorTrigger[] doorTriggers = Object.FindObjectsByType<DynamicDoorTrigger>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (DynamicDoorTrigger doorTrigger in doorTriggers)
        {
            if (doorTrigger.DoorIndex != padIndex || !doorTrigger.gameObject.activeInHierarchy)
            {
                continue;
            }

            float distance = (doorTrigger.transform.position - transform.position).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestMatch = doorTrigger;
            }
        }

        return closestMatch;
    }

    private void StopChargingSequence()
    {
        if (chargeCoroutine != null) StopCoroutine(chargeCoroutine);
        currentChargeTime = 0f;
        
        if (quizManager != null)
        {
            quizManager.HideSharedLoader();
        }
    }
}
