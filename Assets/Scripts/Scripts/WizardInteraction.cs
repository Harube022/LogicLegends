using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.InputSystem;

public class WizardInteraction : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private GameObject gameplayInterfacePanel;
    [SerializeField] private TextMeshProUGUI dialogueText; // Drag your TMP text here
    [SerializeField] private GameObject interactButton;
    [SerializeField] private GameObject rulesButton;
    [SerializeField] private GameObject scrollPanel;

    [Header("Puzzle Management")]
    [Tooltip("Drag the PuzzleManager object here")]
    [SerializeField] private PuzzleManager puzzleManager;

    // Button CHOICES
    [Header("Final Choice UI")]
    [Tooltip("Drag the parent GameObject containing your 'Stay Here' and 'Let's Go' buttons here")]
    [SerializeField] private GameObject choicesPanel;
    
    [Header("Player Control")]
    [Tooltip("Drag your Player's movement script or PlayerInput here to disable it while talking.")]
    [SerializeField] private Behaviour playerControlScript;

    [Header("Objectives")]
    [Tooltip("Drag the 'Talk to Wizard' text from the Objectives Scroll here")]
    [SerializeField] private TextMeshProUGUI wizardObjectiveText;

    [Tooltip("Drag the specific task GameObject to activate after talking here")]
    [SerializeField] private GameObject taskToActivate;

    // Talk Talk to Wizard Lefyahj again
    [Tooltip("Drag the new 'Talk to Wizard Lefyahj again' GameObject here")]
    [SerializeField] private GameObject finalObjectiveObject;
    [Tooltip("Drag the TextMeshPro component of the new objective here so we can cross it out")]
    [SerializeField] private TextMeshProUGUI finalObjectiveText;

    [Header("Dialogue Content")]
    [TextArea(2, 3)]
    [Tooltip("Type each page of dialogue into a new element here.")]
    [SerializeField] private string[] dialogueLines; 

    [Header("Final Dialogue Content")]
    [TextArea(2, 3)]
    [SerializeField] private string[] finalDialogueLines;

    [Tooltip("Type the name of the scene to load when 'Let's Go' is clicked")]
    [SerializeField] private string nextSceneName;

    public bool areAllTasksCompleted = false; // Tells the wizard the tables are done
    private bool isReadingFinalDialogue = false; // Tracks which dialogue array to read from
    
    private int currentLineIndex = 0;
    private bool playerInRange = false;
    private bool isDisplaying = false;
    private bool hasTalkedToWizard = false;

    private void Start()
    {
        dialoguePanel.SetActive(false);
        interactButton.SetActive(false);

        if (choicesPanel != null) choicesPanel.SetActive(false);
        if (finalObjectiveObject != null) finalObjectiveObject.SetActive(false);
    }

    private void Update()
    {
        // NEW: Checks if the choice buttons are currently visible
        bool isWaitingForChoice = choicesPanel != null && choicesPanel.activeSelf;

        // Start dialogue with 'E' if we aren't already talking
        if (playerInRange && !isDisplaying && Input.GetKeyDown(KeyCode.E))
        {
            StartDialogue();
        }
        // Allow desktop users to also advance text with 'E'
        else if (isDisplaying && !isWaitingForChoice && Input.GetKeyDown(KeyCode.E))
        {
            AdvanceDialogue();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            if (!isDisplaying) interactButton.SetActive(true); 
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            interactButton.SetActive(false);
            
            // Instantly close dialogue if the player walks away
            if (isDisplaying) EndDialogue();
        }
    }

    // Call this from your PuzzleManager when the Biconditional table is completed!
    public void UnlockFinalDialogue()
    {
        areAllTasksCompleted = true;
        if (finalObjectiveObject != null) finalObjectiveObject.SetActive(true);
    }

    // Call this from your Interact UI Button
    public void StartDialogue()
    {
        // if (dialogueLines.Length == 0) return;

        isDisplaying = true;
        gameplayInterfacePanel.SetActive(false);
        interactButton.SetActive(false); // Hide the interact button so it's not in the way
        dialoguePanel.SetActive(true);
        if (choicesPanel != null) choicesPanel.SetActive(false);
        
        if (playerControlScript != null) 
        {
            playerControlScript.enabled = false;
        }

        currentLineIndex = 0;
        // --- THIS IS THE MISSING CODE ---
        // Check which set of dialogue to display
        if (areAllTasksCompleted)
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

    // Call this from a Button on your Dialogue Panel to progress when tapped
    public void AdvanceDialogue()
    {
        currentLineIndex++;
        string[] currentActiveDialogue = isReadingFinalDialogue ? finalDialogueLines : dialogueLines;
        if (currentLineIndex < currentActiveDialogue.Length)
        {
            // Show the next line in the array
            dialogueText.text = currentActiveDialogue[currentLineIndex];
        }
        else
        {
            // If we reached the end of the FINAL dialogue, show choices instead of ending
            if (isReadingFinalDialogue)
            {
                ShowChoices();
            }
            else
            {
                EndDialogue();
            }
        }
    }

    private void ShowChoices()
    {
        if (choicesPanel != null)
        {
            choicesPanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning("Choices Panel is not assigned in the Inspector!");
            EndDialogue(); // Fallback just in case
        }
    }

    // --- BUTTON METHODS FOR CHOICES ---

    // Link this to your "Stay Here" Button's OnClick event
    public void ChooseStayHere()
    {
        choicesPanel.SetActive(false);
        EndDialogue();
        
        // Cross out the final objective
        if (finalObjectiveText != null && !finalObjectiveText.text.Contains("<s>"))
        {
            finalObjectiveText.text = "<color=#008000><s>" + finalObjectiveText.text + "</s></color>";
        }

        // --- NEW: Reset the puzzles so the player can play them again ---
        if (puzzleManager != null)
        {
            puzzleManager.RestartPuzzles();

        }

        // --- NEW: Reset the wizard's state so he doesn't repeat the final dialogue! ---
        areAllTasksCompleted = false;
    }

    // Link this to your "Let's Go!" Button's OnClick event
    public void ChooseLetsGo()
    {
        // Make sure your Main Menu scene is added to your Build Settings!
        SceneManager.LoadScene(nextSceneName);
    }

    // ----------------------------------

    private void EndDialogue()
    {
        isDisplaying = false;
        dialoguePanel.SetActive(false);      
        if (choicesPanel != null) choicesPanel.SetActive(false); // Safety check to ensure choices hide
        currentLineIndex = 0;
        
        // Show the interact button again if the player is still standing there
        // scrollPanel.SetActive(true);
        gameplayInterfacePanel.SetActive(true);
        // rulesButton.SetActive(true);

        // --- YOU MISSED THIS PART! This gives you your movement back ---
        if (playerControlScript != null) 
        {
            playerControlScript.enabled = true;
        }


    if (!hasTalkedToWizard && wizardObjectiveText != null && !isReadingFinalDialogue)
        {
            hasTalkedToWizard = true; 
            wizardObjectiveText.text = "<color=#008000><s>" + wizardObjectiveText.text + "</s></color>";
            
            // Now it activates whatever puzzle you dragged into the Inspector!
            if (taskToActivate != null) taskToActivate.SetActive(true); 
            
            if (LevelManager.Instance != null) LevelManager.Instance.StartTimer();
        }
    }
}