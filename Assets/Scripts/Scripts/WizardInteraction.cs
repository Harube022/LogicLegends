using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.UI; 

public class WizardInteraction : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private GameObject gameplayInterfacePanel;
    [SerializeField] private TextMeshProUGUI dialogueText; 
    [SerializeField] private GameObject interactButton;
    [SerializeField] private GameObject rulesButton;
    [SerializeField] private GameObject scrollPanel;
    [Tooltip("Drag the NextDialogue_Button from your Canvas here")]
    [SerializeField] private GameObject nextDialogueButton;

    [Header("Puzzle Management")]
    [SerializeField] private PuzzleManager puzzleManager;

    [Header("Final Choice UI")]
    [SerializeField] private GameObject choicesPanel;
    
    [Header("Player Control")]
    [SerializeField] private Behaviour playerControlScript;

    [Header("Timer Settings")]
    public bool startsTimer = false;

    [Header("Objectives")]
    [SerializeField] private TextMeshProUGUI wizardObjectiveText;
    [SerializeField] private GameObject taskToActivate;
    [SerializeField] private GameObject finalObjectiveObject;
    [SerializeField] private TextMeshProUGUI finalObjectiveText;

    [Header("Dialogue Content")]
    [TextArea(2, 3)] [SerializeField] private string[] dialogueLines; 
    [Header("Final Dialogue Content")]
    [TextArea(2, 3)] [SerializeField] private string[] finalDialogueLines;
    [SerializeField] private string nextSceneName;

    // ---> NEW: CHALLENGE 2 OR GATE VARIABLES <---
    [Header("Challenge 2: OR Gate Check")]
    [Tooltip("Drag the FruitBasket object here")]
    public FruitBasket challenge2Basket;
    [TextArea(2, 3)] public string[] fruitSuccessLines;
    [TextArea(2, 3)] public string[] fruitFailLines;
    public GameObject gate3Open;
    public GameObject gate3Closed;
    private bool isCheckingFruit = false;
    private bool isFruitCorrect = false;
    // --------------------------------------------

    public bool areAllTasksCompleted = false; 
    private bool isReadingFinalDialogue = false; 
    
    private int currentLineIndex = 0;
    private bool playerInRange = false;
    private bool isDisplaying = false;
    private bool hasTalkedToWizard = false;
    private string originalObjectiveString;

    private void Start()
    {
        dialoguePanel.SetActive(false);
        interactButton.SetActive(false);
        if (choicesPanel != null) choicesPanel.SetActive(false);
        if (finalObjectiveObject != null) finalObjectiveObject.SetActive(false);

        if (wizardObjectiveText != null) originalObjectiveString = wizardObjectiveText.text;
    }

    private void Update()
    {
        bool isWaitingForChoice = choicesPanel != null && choicesPanel.activeSelf;
        if (playerInRange && !isDisplaying && Input.GetKeyDown(KeyCode.E)) StartDialogue();
        else if (isDisplaying && !isWaitingForChoice && Input.GetKeyDown(KeyCode.E)) AdvanceDialogue();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            if (!isDisplaying) interactButton.SetActive(true); 

            if (interactButton != null)
            {
                Button intBtn = interactButton.GetComponent<Button>();
                if (intBtn != null)
                {
                    intBtn.onClick.RemoveAllListeners();
                    intBtn.onClick.AddListener(StartDialogue);
                }
            }

            if (nextDialogueButton != null)
            {
                Button nextBtn = nextDialogueButton.GetComponent<Button>();
                if (nextBtn != null)
                {
                    nextBtn.onClick.RemoveAllListeners();
                    nextBtn.onClick.AddListener(AdvanceDialogue);
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            interactButton.SetActive(false);
            if (isDisplaying) EndDialogue();
        }
    }

    public void UnlockFinalDialogue()
    {
        areAllTasksCompleted = true;
        if (finalObjectiveObject != null) finalObjectiveObject.SetActive(true);
    }

    public void StartDialogue()
    {
        isDisplaying = true;
        gameplayInterfacePanel.SetActive(false);
        interactButton.SetActive(false); 
        dialoguePanel.SetActive(true);
        if (choicesPanel != null) choicesPanel.SetActive(false);
        if (playerControlScript != null) playerControlScript.enabled = false;

        currentLineIndex = 0;

        // ---> NEW: Check if there's fruit in the basket to evaluate! <---
        if (challenge2Basket != null && challenge2Basket.HasFruit())
        {
            isCheckingFruit = true;
            isFruitCorrect = challenge2Basket.CheckORGate();
            if (isFruitCorrect && fruitSuccessLines.Length > 0) dialogueText.text = fruitSuccessLines[currentLineIndex];
            else if (!isFruitCorrect && fruitFailLines.Length > 0) dialogueText.text = fruitFailLines[currentLineIndex];
        }
        else if (areAllTasksCompleted)
        {
            isReadingFinalDialogue = true;
            if (finalDialogueLines.Length > 0) dialogueText.text = finalDialogueLines[currentLineIndex];
        }
        else
        {
            isReadingFinalDialogue = false;
            if (dialogueLines.Length > 0) dialogueText.text = dialogueLines[currentLineIndex];
        }
    }

    public void AdvanceDialogue()
    {
        currentLineIndex++;
        string[] currentActiveDialogue;

        // ---> NEW: Route the "Next" button to the correct text array <---
        if (isCheckingFruit) currentActiveDialogue = isFruitCorrect ? fruitSuccessLines : fruitFailLines;
        else if (isReadingFinalDialogue) currentActiveDialogue = finalDialogueLines;
        else currentActiveDialogue = dialogueLines;

        if (currentLineIndex < currentActiveDialogue.Length)
        {
            dialogueText.text = currentActiveDialogue[currentLineIndex];
        }
        else
        {
            if (isReadingFinalDialogue) ShowChoices();
            else EndDialogue();
        }
    }

    private void ShowChoices()
    {
        if (choicesPanel != null) choicesPanel.SetActive(true);
        else EndDialogue(); 
    }

    public void ChooseStayHere()
    {
        choicesPanel.SetActive(false);
        EndDialogue();
        
        if (finalObjectiveText != null && !finalObjectiveText.text.Contains("<s>"))
        {
            finalObjectiveText.text = "<color=#008000><s>" + finalObjectiveText.text + "</s></color>";
        }

        if (puzzleManager != null) puzzleManager.RestartPuzzles();
        areAllTasksCompleted = false;
    }

    public void ChooseLetsGo() { SceneManager.LoadScene(nextSceneName); }

    private void EndDialogue()
    {
        isDisplaying = false;
        dialoguePanel.SetActive(false);      
        if (choicesPanel != null) choicesPanel.SetActive(false); 
        currentLineIndex = 0;
        
        gameplayInterfacePanel.SetActive(true);
        if (playerControlScript != null) playerControlScript.enabled = true;

        // ---> NEW: Handle the consequences of the Fruit Check! <---
        if (isCheckingFruit)
        {
            if (isFruitCorrect)
            {
                // Correct! Open the gate!
                if (gate3Open != null) gate3Open.SetActive(true);
                if (gate3Closed != null) gate3Closed.SetActive(false);

                // Cross out the fruit objective text
                if (wizardObjectiveText != null && !wizardObjectiveText.text.Contains("<s>"))
                {
                    wizardObjectiveText.text = "<color=#008000><s>" + wizardObjectiveText.text + "</s></color>";
                }
            }
            else
            {
                // Wrong! Lose a heart and clear the basket!
                if (challenge2Basket != null) challenge2Basket.ClearBasket();
                if (LevelManager.Instance != null) LevelManager.Instance.LoseHeartAndRespawn();
            }

            isCheckingFruit = false; // Reset for next time
            return; // Exit out so it doesn't trigger the basic Challenge 1 setup
        }

        if (!hasTalkedToWizard && wizardObjectiveText != null && !isReadingFinalDialogue)
        {
            hasTalkedToWizard = true; 
            wizardObjectiveText.text = "<color=#008000><s>" + wizardObjectiveText.text + "</s></color>";
            if (taskToActivate != null) taskToActivate.SetActive(true); 
            if (startsTimer && LevelManager.Instance != null) LevelManager.Instance.StartTimer();
        }
    }

    public void ResetWizardStatus()
    {
        hasTalkedToWizard = false;
        if (wizardObjectiveText != null && !string.IsNullOrEmpty(originalObjectiveString))
        {
            wizardObjectiveText.text = originalObjectiveString;
        }
        if (taskToActivate != null) taskToActivate.SetActive(false);
    }
}