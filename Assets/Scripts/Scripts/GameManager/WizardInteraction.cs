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

    [Header("Audio")]
    [SerializeField] private AudioClip taskScribbleClip;
    [SerializeField, Range(0f, 1f)] private float scribbleVolume = 0.5f; // Set this lower in the inspector!

    [Header("Wizard Voice")]
    [SerializeField] private AudioClip wizardTalkClip;
    [SerializeField, Range(0f, 1f)] private float wizardTalkVolume = 0.7f;

    [Header("Player Control")]
    // [SerializeField] private Behaviour playerControlScript;
    private Player localPlayer;

    private GameInput gameInput;
    private int currentLineIndex = 0;
    private bool playerInRange = false;
    private bool isDisplaying = false;
    public bool areAllTasksCompleted = false; 
    private bool isReadingFinalDialogue = false; 
    private bool hasTalkedToWizard = false;
    private string originalObjectiveString;
    private bool hasCrossedOutText = false;

    private bool isPlayingFeedback = false;
    private bool isSuccessFeedback = false;


    private void Awake()
    {
        // Find the input system early so it's ready when the environment turns on
        gameInput = FindFirstObjectByType<GameInput>();
    }

    private void Start()
    {
        // Save the original text once at the very beginning
        if (wizardObjectiveText != null) originalObjectiveString = wizardObjectiveText.text;
    }

    // ---> NEW: Subscribe ONLY when the environment is turned ON <---
    private void OnEnable()
    {
        if (gameInput != null) gameInput.OnInteractAction += GameInput_OnInteractAction;
    }

    // ---> NEW: Unsubscribe and wipe memory when the environment turns OFF <---
    private void OnDisable()
    {
        if (gameInput != null) gameInput.OnInteractAction -= GameInput_OnInteractAction;
        
        // This prevents the "Stuck Trigger" bug if the map turns off while you are standing here!
        playerInRange = false;
        isDisplaying = false;

        // Clean up the UI just in case
        if (DialogueManager.Instance != null) 
            DialogueManager.Instance.ToggleInteractButton(false);
    }
    // private void Start()
    // {
    //     gameInput = FindFirstObjectByType<GameInput>();
    //     if (gameInput != null) gameInput.OnInteractAction += GameInput_OnInteractAction;
    //     if (wizardObjectiveText != null) originalObjectiveString = wizardObjectiveText.text;
    // }

    private void GameInput_OnInteractAction(object sender, System.EventArgs e)
    {
        if (playerInRange && !isDisplaying) 
        {
            TriggerInteraction();
        }
        else if (isDisplaying) 
        {
            AdvanceDialogue();
        }
    }

    // private void OnDestroy()
    // {
    //     if (gameInput != null) gameInput.OnInteractAction -= GameInput_OnInteractAction;
    // }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PhotonView view = other.GetComponent<PhotonView>();
            if (view != null && !view.IsMine) return; 

            playerInRange = true;
            DialogueManager.Instance.ToggleInteractButton(true);
            localPlayer = other.GetComponent<Player>();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PhotonView view = other.GetComponent<PhotonView>();
            if (view != null && !view.IsMine) return; 
            
            // ---> NEW: Bulletproof math check! <---
            // Only consider the player "gone" if they are actually far away.
            // Adjust the '4f' to match the radius of your SphereCollider!
            float distance = Vector3.Distance(transform.position, other.transform.position);
            if (distance > 4f) 
            {
                playerInRange = false;
                DialogueManager.Instance.ToggleInteractButton(false);
                if (isDisplaying) EndDialogue();
            }
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
        if (localPlayer != null) localPlayer.ToggleControl(false);

        currentLineIndex = 0;
        isReadingFinalDialogue = areAllTasksCompleted;

        // ---> NEW: Start listening for screen taps! <---
        DialogueManager.Instance.OnPanelTapped -= AdvanceDialogue; // Safety clear
        DialogueManager.Instance.OnPanelTapped += AdvanceDialogue;
        
        string[] activeLines = isReadingFinalDialogue ? finalDialogueLines : dialogueLines;
        if (activeLines.Length > 0) 
        {
            DialogueManager.Instance.ShowDialoguePanel(activeLines[0]);
            PlayWizardVoice();
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
            PlayWizardVoice();
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

        if (localPlayer != null) localPlayer.ToggleControl(true);

        if (isPlayingFeedback)
        {
            isPlayingFeedback = false;
            if (PhotonNetwork.InRoom) photonView.RPC("RPC_TriggerPuzzleFeedback", RpcTarget.All, isSuccessFeedback);
            else RPC_TriggerPuzzleFeedback(isSuccessFeedback);
            return;
        }

        // OnDialogueComplete?.Invoke();

        if (!hasTalkedToWizard && !isReadingFinalDialogue)
        {
            if (PhotonNetwork.InRoom) photonView.RPC("RPC_StartWizardTasks", RpcTarget.All);
            else RPC_StartWizardTasks(); 
        }
        else if (isReadingFinalDialogue)
        {
            // We still want to fire the event locally if they are reading the final "Goodbye" text!
            OnDialogueComplete?.Invoke();
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
        if (localPlayer != null) localPlayer.ToggleControl(false);

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

// ---> FIX: We now check the network before resetting <---
    public void ResetWizardStatus()
    {

        RPC_ResetWizard();

        // ---> THE FIX: Added 'gameObject.activeInHierarchy' so Photon doesn't crash the loop! <---
        if (PhotonNetwork.InRoom && photonView != null && gameObject.activeInHierarchy)
        {
            // Tell EVERY computer to reset this specific wizard
            photonView.RPC("RPC_ResetWizard", RpcTarget.Others);
        }
    }

    // ---> NEW: The Networked Reset Logic <---
    [PunRPC]
    public void RPC_ResetWizard()
    {
        // 1. Wipe the wizard's memory entirely
        hasTalkedToWizard = false;
        areAllTasksCompleted = false; 
        isReadingFinalDialogue = false;
        isPlayingFeedback = false;
        currentLineIndex = 0;
        isDisplaying = false;
        hasCrossedOutText = false; // ---> NEW: Reset our text flag! <---

        // ---> FIX 2: A bulletproof text reset! <---
        if (wizardObjectiveText != null)
        {
            // If the original string is somehow lost, just strip the rich text tags manually!
            if (string.IsNullOrEmpty(originalObjectiveString))
            {
                originalObjectiveString = wizardObjectiveText.text.Replace("<color=#008000><s>", "").Replace("</s></color>", "");
            }
            wizardObjectiveText.text = originalObjectiveString;
        }

        // 3. Turn off the tasks (Dynamic arrows, puzzle pieces, etc.)
        if (taskToActivate != null) 
        {
            taskToActivate.SetActive(false);
        }
    }

    [PunRPC]
    public void RPC_StartWizardTasks()
    {
        hasTalkedToWizard = true; 
        // ---> FIX 4: Use our boolean instead of checking the text for <s> tags <---
        if (wizardObjectiveText != null && !hasCrossedOutText)
        {
            hasCrossedOutText = true;
            wizardObjectiveText.text = "<color=#008000><s>" + wizardObjectiveText.text + "</s></color>";
        }
        
        if (taskToActivate != null) taskToActivate.SetActive(true); 
        
        if (startsTimer && LevelManager.Instance != null) 
            LevelManager.Instance.StartCustomTimer(timerDuration);

        // ---> NEW: Play the Scribble Sound <---
        if (taskScribbleClip != null)
        {
            Spawn3DTaskAudio(taskScribbleClip, transform.position, scribbleVolume);
        }

        OnDialogueComplete?.Invoke();
    }
    
    [PunRPC]
    public void RPC_TriggerPuzzleFeedback(bool success)
    {
        if (success) OnPuzzleSuccess?.Invoke();
        else OnPuzzleFail?.Invoke();
    }

    public void LockPlayer(GameObject playerObject)
    {
        localPlayer = playerObject.GetComponent<Player>();
        if (localPlayer != null)
        {
            localPlayer.ToggleControl(false);
        }
    }

    // ---> NEW: The "Footstep Magic" adapted for UI Sounds! <---
    private void Spawn3DTaskAudio(AudioClip clip, Vector3 spawnPosition, float volume)
    {
        GameObject audioObj = new GameObject("TempTaskAudio");
        audioObj.transform.position = spawnPosition;

        AudioSource source = audioObj.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume; // Applies your lowered volume
        
        // Add slight random pitch so it sounds natural every time
        source.pitch = Random.Range(0.95f, 1.05f);
        
        // Make it 3D Spatial Audio
        source.spatialBlend = 1f; 
        source.minDistance = 2f;
        source.maxDistance = 15f; 

        source.Play();
        Destroy(audioObj, clip.length + 0.1f);
    }

    private void PlayWizardVoice()
    {
        if (wizardTalkClip == null) return;

        Spawn3DTaskAudio(wizardTalkClip, transform.position, wizardTalkVolume);
    }
}
