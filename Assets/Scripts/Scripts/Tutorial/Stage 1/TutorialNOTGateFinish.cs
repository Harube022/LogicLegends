using UnityEngine;
using UnityEngine.Events;

public class TutorialNOTGateFinish : MonoBehaviour
{
    [Header("Tutorial Link")]
    [SerializeField] private PuzzleTutorialManager myTutorialManager;

    [Header("Extra Actions")]
    public UnityEvent onFinish;
    private bool hasFinished = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!hasFinished && other.CompareTag("Player"))
        {
            hasFinished = true;

            // Fire the custom event (like hiding the health bar!)
            onFinish?.Invoke();
            
            // Tell the manager we reached the end!
            if (myTutorialManager != null)
            {
                myTutorialManager.AdvanceTutorial(this.transform);
            }
        }
    }
}