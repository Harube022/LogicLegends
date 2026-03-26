using UnityEngine;

public class TutorialSafeCube : MonoBehaviour
{
    [Header("Tutorial Link")]
    [SerializeField] private PuzzleTutorialManager myTutorialManager;

    [Header("Setup")]
    [Tooltip("Drag the main parent Cube here so the manager recognizes it!")]
    [SerializeField] private Transform mainCubeTransform;

    private bool hasTriggered = false;

    // ---> FIXED: Changed to OnTriggerEnter to match the "Is Trigger" box in your Inspector! <---
    private void OnTriggerEnter(Collider other)
    {
        if (!hasTriggered && other.CompareTag("Player"))
        {
            hasTriggered = true;

            // Tell the manager we landed on the correct cube!
            if (myTutorialManager != null)
            {
                // We pass the main cube's transform so it perfectly matches your Manager's list
                Transform targetToSend = mainCubeTransform != null ? mainCubeTransform : this.transform;
                
                // ---> FIXED: Now actually sends the correct target! <---
                myTutorialManager.AdvanceTutorial(targetToSend);
            }
        }
    }
}