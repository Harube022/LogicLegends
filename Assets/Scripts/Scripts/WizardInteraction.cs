using UnityEngine;
using TMPro;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Photon.Pun;

public class WizardInteraction : MonoBehaviourPun
{
    [Header("Objectives & Timers")]
    [SerializeField] private TextMeshProUGUI wizardObjectiveText;
    [SerializeField] private GameObject taskToActivate;
    public bool startsTimer = false;
    [SerializeField] private float timerDuration = 180f;

    [Header("Standard Dialogue")]
    [TextArea(2, 3)] [SerializeField] private string[] dialogueLines; 
    [TextArea(2, 3)] [SerializeField] private string[] finalDialogueLines;
    [SerializeField] private string nextSceneName;

    [Header("Puzzle Feedback Dialogue")]
    [TextArea(2, 3)] public string[] successLines;
    [TextArea(2, 3)] public string[] failLines;

    [Header("Events")]
    public UnityEvent OnWizardInteract; 
    public UnityEvent OnDialogueComplete;
    public UnityEvent OnPuzzleSuccess;
    public UnityEvent OnPuzzleFail;

    [Header("Player Control")]
    [SerializeField] private Behaviour playerControlScript;

    private GameInput gameInput;
    private int currentLineIndex = 0;
    private bool playerInRange = false;
    private bool isDisplaying = false;
    public bool areAllTasksCompleted = false; 
    private bool isReadingFinalDialogue = false; 
    private bool hasTalkedToWizard = false;
    private string originalObjectiveString;

    private bool isPlayingFeedback = false;
    private bool isSuccessFeedback = false;

    private void Start()
    {
        gameInput = FindFirstObjectByType<GameInput>();
        if (gameInput != null) gameInput.OnInteractAction += GameInput_OnInteractAction;
        if (wizardObjectiveText != null) originalObjectiveString = wizardObjectiveText.text;
    }

    private void GameInput_OnInteractAction(object sender, System.EventArgs e)
    {
        if (playerInRange && !isDisplaying) TriggerInteraction();
        else if (isDisplaying) AdvanceDialogue();
    }

    private void OnDestroy()
    {
        if (gameInput != null) gameInput.OnInteractAction -= GameInput_OnInteractAction;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PhotonView view = other.GetComponent<PhotonView>();
            if (view != null && !view.IsMine) return; 

            playerInRange = true;
            DialogueManager.Instance.ToggleInteractButton(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PhotonView view = other.GetComponent<PhotonView>();
            if (view != null && !view.IsMine) return; 
            
            playerInRange = false;
            DialogueManager.Instance.ToggleInteractButton(false);
            if (isDisplaying) EndDialogue();
        }
    }

    public void TriggerInteraction()
    {
        if (OnWizardInteract != null && OnWizardInteract.GetPersistentEventCount() > 0)
            OnWizardInteract.Invoke();
        else
            StartStandardDialogue();
    }

    public void StartStandardDialogue()
    {
        isDisplaying = true;
        if (playerControlScript != null) playerControlScript.enabled = false;

        currentLineIndex = 0;
        isReadingFinalDialogue = areAllTasksCompleted;

        // ---> NEW: Start listening for screen taps! <---
        DialogueManager.Instance.OnPanelTapped -= AdvanceDialogue; // Safety clear
        DialogueManager.Instance.OnPanelTapped += AdvanceDialogue;
        
        string[] activeLines = isReadingFinalDialogue ? finalDialogueLines : dialogueLines;
        if (activeLines.Length > 0) 
        {
            DialogueManager.Instance.ShowDialoguePanel(activeLines[0]);
        }
    }

    public void AdvanceDialogue()
    {
        currentLineIndex++;
        
        string[] activeLines = isPlayingFeedback ? (isSuccessFeedback ? successLines : failLines) 
                             : (isReadingFinalDialogue ? finalDialogueLines : dialogueLines);

        if (currentLineIndex < activeLines.Length) 
        {
            DialogueManager.Instance.UpdateText(activeLines[currentLineIndex]);
        }
        else
        {
            if (isReadingFinalDialogue && !isPlayingFeedback) DialogueManager.Instance.ShowChoices();
            else EndDialogue();
        }
    }

    private void EndDialogue()
    {
        isDisplaying = false;
        
        // ---> NEW: Stop listening for screen taps so wizards don't talk over each other! <---
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.OnPanelTapped -= AdvanceDialogue;
            DialogueManager.Instance.HideDialoguePanel();      
        }

        if (playerControlScript != null) playerControlScript.enabled = true;

        if (isPlayingFeedback)
        {
            isPlayingFeedback = false;
            if (PhotonNetwork.InRoom) photonView.RPC("RPC_TriggerPuzzleFeedback", RpcTarget.All, isSuccessFeedback);
            else RPC_TriggerPuzzleFeedback(isSuccessFeedback);
            return;
        }

        OnDialogueComplete?.Invoke();

        if (!hasTalkedToWizard && wizardObjectiveText != null && !isReadingFinalDialogue)
        {
            if (PhotonNetwork.InRoom) photonView.RPC("RPC_StartWizardTasks", RpcTarget.All);
            else RPC_StartWizardTasks(); 
        }
    }

    public void PlaySuccessDialogue() { StartFeedback(successLines, true); }
    public void PlayFailDialogue() { StartFeedback(failLines, false); }

    private void StartFeedback(string[] lines, bool isSuccess)
    {
        if (lines.Length == 0)
        {
            if (isSuccess) OnPuzzleSuccess?.Invoke();
            else OnPuzzleFail?.Invoke();
            return;
        }

        isPlayingFeedback = true;
        isSuccessFeedback = isSuccess;
        currentLineIndex = 0;
        
        isDisplaying = true;
        if (playerControlScript != null) playerControlScript.enabled = false;

        // ---> NEW: Start listening for screen taps! <---
        DialogueManager.Instance.OnPanelTapped -= AdvanceDialogue; // Safety clear
        DialogueManager.Instance.OnPanelTapped += AdvanceDialogue;
        
        DialogueManager.Instance.ShowDialoguePanel(lines[0]);
    }

    public void ChooseStayHere()
    {
        EndDialogue();
        areAllTasksCompleted = false;
    }

    public void ChooseLetsGo() { SceneManager.LoadScene(nextSceneName); }

    public void ResetWizardStatus()
    {
        hasTalkedToWizard = false;
        if (wizardObjectiveText != null && !string.IsNullOrEmpty(originalObjectiveString))
            wizardObjectiveText.text = originalObjectiveString;
        if (taskToActivate != null) taskToActivate.SetActive(false);
    }

    [PunRPC]
    public void RPC_StartWizardTasks()
    {
        hasTalkedToWizard = true; 
        if (wizardObjectiveText != null && !wizardObjectiveText.text.Contains("<s>"))
            wizardObjectiveText.text = "<color=#008000><s>" + wizardObjectiveText.text + "</s></color>";
        
        if (taskToActivate != null) taskToActivate.SetActive(true); 
        
        if (startsTimer && LevelManager.Instance != null) 
            LevelManager.Instance.StartCustomTimer(timerDuration);
    }
    
    [PunRPC]
    public void RPC_TriggerPuzzleFeedback(bool success)
    {
        if (success) OnPuzzleSuccess?.Invoke();
        else OnPuzzleFail?.Invoke();
    }
}
