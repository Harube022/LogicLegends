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
                chargeCoroutine = StartCoroutine(ChargeSelection());
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

    private IEnumerator ChargeSelection()
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

        if (quizManager != null)
        {
            quizManager.ClearQuizUI();
        }

        if (doorVisualObject != null)
        {
            doorVisualObject.SetActive(false);
        }

        StopChargingSequence();
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