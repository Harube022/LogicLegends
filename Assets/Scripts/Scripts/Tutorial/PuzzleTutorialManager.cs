using UnityEngine;
using TMPro;

[System.Serializable] 
public class TutorialStep
{
    [TextArea(2, 4)] 
    public string objectiveText; 
    public Transform targetObject; 
    public Transform completionTrigger; 
}

public class PuzzleTutorialManager : MonoBehaviour
{
    [Header("UI & Indicators")]
    [SerializeField] private WorldIndicator tutorialArrow;
    [SerializeField] private TextMeshProUGUI parchmentText;

    [Header("Tutorial Sequence")]
    [SerializeField] private TutorialStep[] steps; 
    [SerializeField] private string finalCompletionText = "Challenge Complete! Speak to the Wizard.";

    private int currentStepIndex = 0;
    private bool hasStarted = false; 

    private void Start()
    {
        // Stay hidden when the game starts
        if (tutorialArrow != null) tutorialArrow.Hide();
    }

    // ---> NEW: The Wizard will call this when he finishes talking! <---
    public void StartTutorial()
    {
        if (!hasStarted)
        {
            hasStarted = true;
            if (steps.Length > 0) ShowCurrentStep();
        }
    }

    public void AdvanceTutorial(Transform sourceObject)
    {
        if (!hasStarted || currentStepIndex >= steps.Length) return;

        TutorialStep currentStep = steps[currentStepIndex];
        Transform expectedTrigger = currentStep.completionTrigger != null ? currentStep.completionTrigger : currentStep.targetObject;

        if (sourceObject == expectedTrigger)
        {
            currentStepIndex++;

            if (currentStepIndex < steps.Length)
                ShowCurrentStep();
            else
                CompleteTutorial();
        }
    }

    private void ShowCurrentStep()
    {
        TutorialStep currentStep = steps[currentStepIndex];
        if (parchmentText != null) parchmentText.text = currentStep.objectiveText;

        if (tutorialArrow != null && currentStep.targetObject != null)
            tutorialArrow.PointAt(currentStep.targetObject);
        else if (tutorialArrow != null)
            tutorialArrow.Hide();
    }

    private void CompleteTutorial()
    {
        if (parchmentText != null) parchmentText.text = finalCompletionText;
        if (tutorialArrow != null) tutorialArrow.Hide();
    }
}