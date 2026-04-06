using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class TutorialIntroTrigger : MonoBehaviour
{
    [Header("The Guide")]
    [Tooltip("Drag the Wizard you want to speak here")]
    [SerializeField] private WizardInteraction guideWizard;

    [Header("Extra Actions")]
    public UnityEvent onPlayerEnter;

    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        // When the player teleports into this invisible box, start the dialogue!
        if (!hasTriggered && other.CompareTag("Player"))
        {
            hasTriggered = true;
            
            // Fire the custom event (like showing the health bar!)
            onPlayerEnter?.Invoke();

            if (guideWizard != null)
            {
                // We add a half-second delay so the camera can settle after teleporting
                guideWizard.LockPlayer(other.gameObject);
                StartCoroutine(StartDialogueWithDelay());
            }
        }
    }

    private IEnumerator StartDialogueWithDelay()
    {
        yield return new WaitForSeconds(0.5f); 
        guideWizard.StartStandardDialogue(); // Forces the wizard to start talking!
    }
}