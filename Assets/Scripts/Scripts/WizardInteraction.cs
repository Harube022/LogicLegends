using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Photon.Pun;

public class WizardInteraction : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private GameObject gameplayInterfacePanel;
    [SerializeField] private TextMeshProUGUI dialogueText; 
    [SerializeField] private GameObject interactButton;
    [SerializeField] private GameObject nextDialogueButton;
    [SerializeField] private GameObject choicesPanel;
    [SerializeField] private Behaviour playerControlScript;

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

    [Header("Events (Drag & Drop in Inspector)")]
    [Tooltip("Fires the moment the player presses E to talk")]
    public UnityEvent OnWizardInteract; // <--- NEW EVENT
    public UnityEvent OnDialogueComplete;
    public UnityEvent OnPuzzleSuccess;
    public UnityEvent OnPuzzleFail;

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
        dialoguePanel.SetActive(false);
        interactButton.SetActive(false);
        if (choicesPanel != null) choicesPanel.SetActive(false);
        if (wizardObjectiveText != null) originalObjectiveString = wizardObjectiveText.text;
    }

    private void Update()
    {
        if (Keyboard.current != null) 
        {
            bool isWaitingForChoice = choicesPanel != null && choicesPanel.activeSelf;

            if (playerInRange && !isDisplaying && Keyboard.current.eKey.wasPressedThisFrame) 
                TriggerInteraction(); // <--- CHANGED
            else if (isDisplaying && !isWaitingForChoice && Keyboard.current.eKey.wasPressedThisFrame) 
                AdvanceDialogue();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // ---> MULTIPLAYER FIX <---
            // Grab the PhotonView from the player that just entered
            PhotonView view = other.GetComponent<PhotonView>();

            // If this player belongs to someone else, ignore them!
            if (view != null && !view.IsMine) return; 
            // -------------------------

            playerInRange = true;
            if (!isDisplaying) interactButton.SetActive(true); 

            if (interactButton != null) 
            {
                Button btn = interactButton.GetComponent<Button>();
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(TriggerInteraction);
            }
            if (nextDialogueButton != null) 
            {
                Button nextBtn = nextDialogueButton.GetComponent<Button>();
                nextBtn.onClick.RemoveAllListeners();
                nextBtn.onClick.AddListener(AdvanceDialogue);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // ---> MULTIPLAYER FIX <---
            PhotonView view = other.GetComponent<PhotonView>();
            if (view != null && !view.IsMine) return; 
            // -------------------------
            
            playerInRange = false;
            interactButton.SetActive(false);
            if (isDisplaying) EndDialogue();
        }
    }

    // --- THE NEW INTERACTION FLOW ---
    public void TriggerInteraction()
    {
        // If we attached a Validator in the Inspector, let the Validator decide what to do!
        if (OnWizardInteract != null && OnWizardInteract.GetPersistentEventCount() > 0)
        {
            OnWizardInteract.Invoke();
        }
        else
        {
            // If there's no Validator (like Challenge 1), just read normal lines
            StartStandardDialogue();
        }
    }

    // We changed this name so the Validator can call it directly
    public void StartStandardDialogue()
    {
        isDisplaying = true;
        gameplayInterfacePanel.SetActive(false);
        interactButton.SetActive(false); 
        dialoguePanel.SetActive(true);
        if (choicesPanel != null) choicesPanel.SetActive(false);
        if (playerControlScript != null) playerControlScript.enabled = false;

        currentLineIndex = 0;
        isReadingFinalDialogue = areAllTasksCompleted;
        
        string[] activeLines = isReadingFinalDialogue ? finalDialogueLines : dialogueLines;
        if (activeLines.Length > 0) dialogueText.text = activeLines[currentLineIndex];
    }

    public void AdvanceDialogue()
    {
        currentLineIndex++;
        
        string[] activeLines;
        if (isPlayingFeedback) activeLines = isSuccessFeedback ? successLines : failLines;
        else activeLines = isReadingFinalDialogue ? finalDialogueLines : dialogueLines;

        if (currentLineIndex < activeLines.Length) dialogueText.text = activeLines[currentLineIndex];
        else
        {
            if (isReadingFinalDialogue && !isPlayingFeedback) ShowChoices();
            else EndDialogue();
        }
    }

    private void EndDialogue()
    {
        isDisplaying = false;
        dialoguePanel.SetActive(false);      
        currentLineIndex = 0;
        
        gameplayInterfacePanel.SetActive(true);
        if (playerControlScript != null) playerControlScript.enabled = true;

        if (isPlayingFeedback)
        {
            isPlayingFeedback = false;
            if (isSuccessFeedback) OnPuzzleSuccess?.Invoke();
            else OnPuzzleFail?.Invoke();
            return; 
        }

        OnDialogueComplete?.Invoke();

        // if (!hasTalkedToWizard && wizardObjectiveText != null && !isReadingFinalDialogue)
        // {
        //     hasTalkedToWizard = true; 
        //     wizardObjectiveText.text = "<color=#008000><s>" + wizardObjectiveText.text + "</s></color>";
        //     if (taskToActivate != null) taskToActivate.SetActive(true); 
            
        //     if (startsTimer && LevelManager.Instance != null) 
        //         LevelManager.Instance.StartCustomTimer(timerDuration);
        // }
        // ---> MULTIPLAYER FIX: Sync objectives and timer <---
        if (!hasTalkedToWizard && wizardObjectiveText != null && !isReadingFinalDialogue)
        {
            PhotonView view = GetComponent<PhotonView>();
            if (view != null && PhotonNetwork.InRoom)
            {
                // Tell EVERYONE in the room to start the timer and update UI
                view.RPC("RPC_StartWizardTasks", RpcTarget.All);
            }
            else
            {
                // Fallback for Solo mode if not connected to Photon
                RPC_StartWizardTasks(); 
            }
        }
    }

    public void PlaySuccessDialogue()
    {
        if (successLines.Length > 0) StartFeedback(successLines, true);
        else OnPuzzleSuccess?.Invoke(); 
    }

    public void PlayFailDialogue()
    {
        if (failLines.Length > 0) StartFeedback(failLines, false);
        else OnPuzzleFail?.Invoke(); 
    }

    private void StartFeedback(string[] lines, bool isSuccess)
    {
        isPlayingFeedback = true;
        isSuccessFeedback = isSuccess;
        currentLineIndex = 0;
        
        isDisplaying = true;
        gameplayInterfacePanel.SetActive(false);
        interactButton.SetActive(false); 
        dialoguePanel.SetActive(true);
        if (playerControlScript != null) playerControlScript.enabled = false;
        
        dialogueText.text = lines[currentLineIndex];
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

    // ---> NEW MULTIPLAYER RPC <---
    [PunRPC]
    public void RPC_StartWizardTasks()
    {
        hasTalkedToWizard = true; 
        
        // 1. Cross out the objective text
        if (wizardObjectiveText != null && !wizardObjectiveText.text.Contains("<s>"))
        {
            wizardObjectiveText.text = "<color=#008000><s>" + wizardObjectiveText.text + "</s></color>";
        }
        
        // 2. Turn on the next task/gate
        if (taskToActivate != null) 
        {
            taskToActivate.SetActive(true); 
        }
        
        // 3. Start the LevelManager timer!
        if (startsTimer && LevelManager.Instance != null) 
        {
            LevelManager.Instance.StartCustomTimer(timerDuration);
        }
    }
}

// using UnityEngine;
// using System.Collections.Generic;
// using UnityEngine.SceneManagement;
// using TMPro;
// using UnityEngine.InputSystem;
// using UnityEngine.UI; 

// public class WizardInteraction : MonoBehaviour
// {
//     [Header("UI References")]
//     [SerializeField] private GameObject dialoguePanel;
//     [SerializeField] private GameObject gameplayInterfacePanel;
//     [SerializeField] private TextMeshProUGUI dialogueText; 
//     [SerializeField] private GameObject interactButton;
//     [SerializeField] private GameObject rulesButton;
//     [SerializeField] private GameObject scrollPanel;
//     [Tooltip("Drag the NextDialogue_Button from your Canvas here")]
//     [SerializeField] private GameObject nextDialogueButton;

//     [Header("Puzzle Management")]
//     [SerializeField] private PuzzleManager puzzleManager;

//     [Header("Final Choice UI")]
//     [SerializeField] private GameObject choicesPanel;
    
//     [Header("Player Control")]
//     [SerializeField] private Behaviour playerControlScript;

//     [Header("Timer Settings")]
//     public bool startsTimer = false;
//     [SerializeField] private float timerDuration = 180f;

//     [Header("Objectives")]
//     [SerializeField] private TextMeshProUGUI wizardObjectiveText;
//     [SerializeField] private GameObject taskToActivate;
//     [SerializeField] private GameObject finalObjectiveObject;
//     [SerializeField] private TextMeshProUGUI finalObjectiveText;

//     [Header("Dialogue Content")]
//     [TextArea(2, 3)] [SerializeField] private string[] dialogueLines; 
//     [Header("Final Dialogue Content")]
//     [TextArea(2, 3)] [SerializeField] private string[] finalDialogueLines;
//     [SerializeField] private string nextSceneName;

//     // ---> NEW: CHALLENGE 2 OR GATE VARIABLES <---
//     [Header("Challenge 2: OR Gate Check")]
//     [Tooltip("Drag the FruitBasket object here")]
//     public FruitBasket challenge2Basket;
//     [TextArea(2, 3)] public string[] fruitSuccessLines;
//     [TextArea(2, 3)] public string[] fruitFailLines;
//     public GameObject gate3Open;
//     public GameObject gate3Closed;
//     private bool isCheckingFruit = false;
//     private bool isFruitCorrect = false;
//     // --------------------------------------------

//     public bool areAllTasksCompleted = false; 
//     private bool isReadingFinalDialogue = false; 
    
//     private int currentLineIndex = 0;
//     private bool playerInRange = false;
//     private bool isDisplaying = false;
//     private bool hasTalkedToWizard = false;
//     private string originalObjectiveString;

//     private void Start()
//     {
//         dialoguePanel.SetActive(false);
//         interactButton.SetActive(false);
//         if (choicesPanel != null) choicesPanel.SetActive(false);
//         if (finalObjectiveObject != null) finalObjectiveObject.SetActive(false);

//         if (wizardObjectiveText != null) originalObjectiveString = wizardObjectiveText.text;
//     }

//     private void Update()
//     {
//         bool isWaitingForChoice = choicesPanel != null && choicesPanel.activeSelf;
//         // if (playerInRange && !isDisplaying && Input.GetKeyDown(KeyCode.E)) StartDialogue();
//         // else if (isDisplaying && !isWaitingForChoice && Input.GetKeyDown(KeyCode.E)) AdvanceDialogue();
        
//         // Check if a keyboard is connected to prevent null reference errors
//         if (Keyboard.current != null) 
//         {
//             // Use the New Input System's way of checking for a specific key press
//             if (playerInRange && !isDisplaying && Keyboard.current.eKey.wasPressedThisFrame) 
//             {
//                 StartDialogue();
//             }
//             else if (isDisplaying && !isWaitingForChoice && Keyboard.current.eKey.wasPressedThisFrame) 
//             {
//                 AdvanceDialogue();
//             }
//         }
//     }

//     private void OnTriggerEnter(Collider other)
//     {
//         if (other.CompareTag("Player"))
//         {
//             playerInRange = true;
//             if (!isDisplaying) interactButton.SetActive(true); 

//             if (interactButton != null)
//             {
//                 Button intBtn = interactButton.GetComponent<Button>();
//                 if (intBtn != null)
//                 {
//                     intBtn.onClick.RemoveAllListeners();
//                     intBtn.onClick.AddListener(StartDialogue);
//                 }
//             }

//             if (nextDialogueButton != null)
//             {
//                 Button nextBtn = nextDialogueButton.GetComponent<Button>();
//                 if (nextBtn != null)
//                 {
//                     nextBtn.onClick.RemoveAllListeners();
//                     nextBtn.onClick.AddListener(AdvanceDialogue);
//                 }
//             }
//         }
//     }

//     private void OnTriggerExit(Collider other)
//     {
//         if (other.CompareTag("Player"))
//         {
//             playerInRange = false;
//             interactButton.SetActive(false);
//             if (isDisplaying) EndDialogue();
//         }
//     }

//     public void UnlockFinalDialogue()
//     {
//         areAllTasksCompleted = true;
//         if (finalObjectiveObject != null) finalObjectiveObject.SetActive(true);
//     }

//     public void StartDialogue()
//     {
//         isDisplaying = true;
//         gameplayInterfacePanel.SetActive(false);
//         interactButton.SetActive(false); 
//         dialoguePanel.SetActive(true);
//         if (choicesPanel != null) choicesPanel.SetActive(false);
//         if (playerControlScript != null) playerControlScript.enabled = false;

//         currentLineIndex = 0;

//         // ---> NEW: Check if there's fruit in the basket to evaluate! <---
//         if (challenge2Basket != null && challenge2Basket.HasFruit())
//         {
//             isCheckingFruit = true;
//             isFruitCorrect = challenge2Basket.CheckORGate();
//             if (isFruitCorrect && fruitSuccessLines.Length > 0) dialogueText.text = fruitSuccessLines[currentLineIndex];
//             else if (!isFruitCorrect && fruitFailLines.Length > 0) dialogueText.text = fruitFailLines[currentLineIndex];
//         }
//         else if (areAllTasksCompleted)
//         {
//             isReadingFinalDialogue = true;
//             if (finalDialogueLines.Length > 0) dialogueText.text = finalDialogueLines[currentLineIndex];
//         }
//         else
//         {
//             isReadingFinalDialogue = false;
//             if (dialogueLines.Length > 0) dialogueText.text = dialogueLines[currentLineIndex];
//         }
//     }

//     public void AdvanceDialogue()
//     {
//         currentLineIndex++;
//         string[] currentActiveDialogue;

//         // ---> NEW: Route the "Next" button to the correct text array <---
//         if (isCheckingFruit) currentActiveDialogue = isFruitCorrect ? fruitSuccessLines : fruitFailLines;
//         else if (isReadingFinalDialogue) currentActiveDialogue = finalDialogueLines;
//         else currentActiveDialogue = dialogueLines;

//         if (currentLineIndex < currentActiveDialogue.Length)
//         {
//             dialogueText.text = currentActiveDialogue[currentLineIndex];
//         }
//         else
//         {
//             if (isReadingFinalDialogue) ShowChoices();
//             else EndDialogue();
//         }
//     }

//     private void ShowChoices()
//     {
//         if (choicesPanel != null) choicesPanel.SetActive(true);
//         else EndDialogue(); 
//     }

//     public void ChooseStayHere()
//     {
//         choicesPanel.SetActive(false);
//         EndDialogue();
        
//         if (finalObjectiveText != null && !finalObjectiveText.text.Contains("<s>"))
//         {
//             finalObjectiveText.text = "<color=#008000><s>" + finalObjectiveText.text + "</s></color>";
//         }

//         if (puzzleManager != null) puzzleManager.RestartPuzzles();
//         areAllTasksCompleted = false;
//     }

//     public void ChooseLetsGo() { SceneManager.LoadScene(nextSceneName); }

//     private void EndDialogue()
//     {
//         isDisplaying = false;
//         dialoguePanel.SetActive(false);      
//         if (choicesPanel != null) choicesPanel.SetActive(false); 
//         currentLineIndex = 0;
        
//         gameplayInterfacePanel.SetActive(true);
//         if (playerControlScript != null) playerControlScript.enabled = true;

//         // ---> NEW: Handle the consequences of the Fruit Check! <---
//         if (isCheckingFruit)
//         {
//             if (isFruitCorrect)
//             {
//                 // Correct! Open the gate!
//                 if (gate3Open != null) gate3Open.SetActive(true);
//                 if (gate3Closed != null) gate3Closed.SetActive(false);

//                 // --- ADD THESE LINES TO STOP AND RESET THE TIMER ---
//                 if (LevelManager.Instance != null)
//                 {
//                     LevelManager.Instance.StopTimer();
//                     LevelManager.Instance.ResetTimer();
//                 }
//                 // Cross out the fruit objective text
//                 if (wizardObjectiveText != null && !wizardObjectiveText.text.Contains("<s>"))
//                 {
//                     wizardObjectiveText.text = "<color=#008000><s>" + wizardObjectiveText.text + "</s></color>";
//                 }
//             }
//             else
//             {
//                 // Wrong! Lose a heart and clear the basket!
//                 if (challenge2Basket != null) challenge2Basket.ClearBasket();
//                 if (LevelManager.Instance != null) LevelManager.Instance.LoseHeartAndRespawn();
//             }

//             isCheckingFruit = false; // Reset for next time
//             return; // Exit out so it doesn't trigger the basic Challenge 1 setup
//         }

//         if (!hasTalkedToWizard && wizardObjectiveText != null && !isReadingFinalDialogue)
//         {
//             hasTalkedToWizard = true; 
//             wizardObjectiveText.text = "<color=#008000><s>" + wizardObjectiveText.text + "</s></color>";
//             if (taskToActivate != null) taskToActivate.SetActive(true); 
//             if (startsTimer && LevelManager.Instance != null) 
//             {
//                 LevelManager.Instance.StartCustomTimer(timerDuration);
//             }    
//         }
//     }

//     public void ResetWizardStatus()
//     {
//         hasTalkedToWizard = false;
//         if (wizardObjectiveText != null && !string.IsNullOrEmpty(originalObjectiveString))
//         {
//             wizardObjectiveText.text = originalObjectiveString;
//         }
//         if (taskToActivate != null) taskToActivate.SetActive(false);

//         if (gate3Open != null) gate3Open.SetActive(false);
//         if (gate3Closed != null) gate3Closed.SetActive(true);
//     }
// }