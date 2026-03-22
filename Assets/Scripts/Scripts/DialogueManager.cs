using UnityEngine;
using TMPro;
using System;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;

    [Header("UI Panels")]
    public GameObject dialoguePanel;
    public GameObject gameplayInterfacePanel;
    public GameObject choicesPanel;
    public GameObject interactButton;

    [Header("Text Fields")]
    public TextMeshProUGUI dialogueText;

    // ---> NEW: This event acts as a megaphone when the panel is tapped <---
    public Action OnPanelTapped; 

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    // ---> NEW: You will link your Dialogue Panel's Button to this method! <---
    public void OnDialoguePanelClicked()
    {
        // If choices are on the screen, don't let them skip by tapping the background!
        if (choicesPanel != null && choicesPanel.activeSelf) return;

        OnPanelTapped?.Invoke();
    }

    public void ShowDialoguePanel(string startingText, bool hideControls = true)
    {
        dialoguePanel.SetActive(true);
        if (hideControls) gameplayInterfacePanel.SetActive(false);
        interactButton.SetActive(false);
        if (choicesPanel != null) choicesPanel.SetActive(false);
        
        UpdateText(startingText);
    }

    public void HideDialoguePanel()
    {
        dialoguePanel.SetActive(false);
        if (choicesPanel != null) choicesPanel.SetActive(false);
        gameplayInterfacePanel.SetActive(true);
    }

    public void UpdateText(string newText)
    {
        dialogueText.text = newText;
    }

    public void ShowChoices()
    {
        if (choicesPanel != null) choicesPanel.SetActive(true);
    }

    public void ToggleInteractButton(bool isOn)
    {
        if (interactButton != null && !dialoguePanel.activeSelf) 
        {
            interactButton.SetActive(isOn);
        }
    }
}