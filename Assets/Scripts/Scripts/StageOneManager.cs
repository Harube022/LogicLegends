using System.Collections; // Required for Coroutines (the delay timer)
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro; 

public class StageOneManager : MonoBehaviour
{
    [Header("Dialogue Setup")]
    [SerializeField] private float startDelay = 2f; // Wait 5 seconds before starting

    [Header("Dialogue UI Elements")]
    [SerializeField] private GameObject dialoguePanel; 
    [SerializeField] private TextMeshProUGUI dialogueText; 
    
    [TextArea(3, 5)] 
    [SerializeField] private string[] dialogueLines; 
    private int currentLineIndex = 0;

    [Header("HUD Elements to Hide")]
    [SerializeField] private GameObject gameplayInterface; // Drag Gameplay_Interface here
    [SerializeField] private GameObject objectivesPanel;   // Drag ObjectivesPanel here

    [Header("Rules Scroll UI (Late Game)")]
    [SerializeField] private GameObject rulesScrollPanel;     // Drag Scroll_Objectives here
    [SerializeField] private GameObject truthTableRulesGroup; // Drag Truth_Table_rules here

    [Header("Player Reference")]
    [SerializeField] private Behaviour playerInput;

    private void Start()
    {
        // 1. Hide the late-game objectives/scroll UI
        if (truthTableRulesGroup != null) truthTableRulesGroup.SetActive(false);
        if (rulesScrollPanel != null) rulesScrollPanel.SetActive(false);

    // Ensure dialogue panel is hidden at the very beginning
        if (dialoguePanel != null) dialoguePanel.SetActive(false);

        // 2. Start the delay countdown
        StartCoroutine(StartDialogueAfterDelay());
    }

    // --- The Delay Timer ---
    private IEnumerator StartDialogueAfterDelay()
    {
        // Wait for exactly 'startDelay' seconds (5 seconds by default)
        yield return new WaitForSeconds(startDelay);

        // After 5 seconds, trigger the dialogue
        StartDialogue();
    }

    public void StartDialogue()
    {
        dialoguePanel.SetActive(true);
        currentLineIndex = 0;
        
        // Hide the HUD
        if (gameplayInterface != null) gameplayInterface.SetActive(false);
        if (objectivesPanel != null) objectivesPanel.SetActive(false);

        // Freeze player
        if (playerInput != null) playerInput.enabled = false;
        
        ShowNextDialogueLine();
    }

    // This is called by the "Next" button
    public void ShowNextDialogueLine()
    {
        if (currentLineIndex < dialogueLines.Length)
        {
            dialogueText.text = dialogueLines[currentLineIndex];
            currentLineIndex++;
        }
        else
        {
            // No more lines left, finish the dialogue
            EndDialogue();
        }
    }

    private void EndDialogue()
    {
        dialoguePanel.SetActive(false);
        
        // Show the HUD again
        if (gameplayInterface != null) gameplayInterface.SetActive(true);
        if (objectivesPanel != null) objectivesPanel.SetActive(true);
        
        // Unfreeze player
        if (playerInput != null) playerInput.enabled = true;
    }

    // --- Rules Scroll Functions ---

    public void ShowRulesPanel()
    {
        rulesScrollPanel.SetActive(true);
        if (playerInput != null) playerInput.enabled = false;
    }

    public void HideRulesPanel()
    {
        rulesScrollPanel.SetActive(false);
        if (playerInput != null) playerInput.enabled = true;
    }
}