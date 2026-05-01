using UnityEngine;
using Photon.Pun; 
using TMPro; 
using UnityEngine.Events;

public class GateController : MonoBehaviour
{
    [Header("Pressure Plates")]
    [Tooltip("Drag the Left Plate object (the one with the script) here")]
    [SerializeField] private PressurePlate leftPlate;
    [Tooltip("Drag the Right Plate object (the one with the script) here")]
    [SerializeField] private PressurePlate rightPlate;

    [Header("Gate Objects")]
    [SerializeField] private GameObject closeGateObj;
    [SerializeField] private GameObject openGateObj;

    [Header("Transition")]
    [SerializeField] private GameObject teleportZoneToCh2;

    // ---> NEW: UI References <---
    [Header("Objectives UI")]
    [SerializeField] private TextMeshProUGUI taskText;

    // ---> NEW: The intermediate Boulder task <---
    [Tooltip("Drag the 'Find a boulder...' GameObject here")]
    [SerializeField] private GameObject findBoulderTaskObj;

    [Tooltip("Drag the follow-up task GameObject here")]
    [SerializeField] private GameObject proceedToNextTaskObj;

    [Header("Events & Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip crossOutTaskClip;
    [SerializeField] private AudioClip wizardCongratulationClip;
    [SerializeField] private AudioSource wizardVoiceSource;
    public UnityEvent OnPuzzleSolved;
    private string originalTaskString;

    private bool isSolved = false;
    private bool wasEitherPressed = false; // Tracks if we need to show the boulder task
    private PhotonView view; // 2. Add PhotonView reference

    private void Awake()
    {
        view = GetComponent<PhotonView>();
    }

    private void Start()
    {
        // Ensure the teleport zone is off when the game starts
        if (teleportZoneToCh2 != null) teleportZoneToCh2.SetActive(false);

        if (taskText != null) originalTaskString = taskText.text;
        if (proceedToNextTaskObj != null) proceedToNextTaskObj.SetActive(false);
        if (findBoulderTaskObj != null) findBoulderTaskObj.SetActive(false);
    }

    private void Update()
    {
        if (isSolved) return;
        // Check if at least one plate is currently pressed
        bool isEitherPressed = leftPlate.isPressed || rightPlate.isPressed;
        
        // ---> UPDATED: If a plate is pressed and we haven't shown the task yet, turn it on permanently! <---
        if (isEitherPressed && !wasEitherPressed)
        {
            if (findBoulderTaskObj != null) findBoulderTaskObj.SetActive(true);
            wasEitherPressed = true; // Remembers that it has been shown so it doesn't run this block again
        }

        // 3. ONLY the Master Client checks the puzzle state to prevent multiple people solving it at once
        if (leftPlate.isPressed && rightPlate.isPressed)
        {
            if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
            {
                view.RPC("RPC_OpenGate", RpcTarget.All);
            }
            else if (!PhotonNetwork.InRoom)
            {
                // Fallback for solo testing
                RPC_OpenGate(); 
            }
        }
        else
        {
            // Keep it closed (or close it if someone steps off)
            closeGateObj.SetActive(true);
            openGateObj.SetActive(false);
            
        }
    }

    // 4. Move the solving logic into this networked method
    [PunRPC]
    public void RPC_OpenGate()
    {
        isSolved = true;
        leftPlate.LockPlateOn();
        rightPlate.LockPlateOn();
        
        // Open the gate
        closeGateObj.SetActive(false);
        openGateObj.SetActive(true);

        // Turn on the teleport zone <---
        if (teleportZoneToCh2 != null)
        {
            teleportZoneToCh2.SetActive(true);
        }

        // STOP TIMER
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.StopTimer();
            LevelManager.Instance.ResetTimer();
        }

        if (taskText != null && !taskText.text.Contains("<s>"))
        {
            
            taskText.text = "<color=#008000><s>" + originalTaskString + "</s></color>";

            if (findBoulderTaskObj != null) findBoulderTaskObj.SetActive(false);
            
            if (proceedToNextTaskObj != null) proceedToNextTaskObj.SetActive(true);

            // ---> NEW: Play the Cross Out Sound <---
            if (audioSource != null && crossOutTaskClip != null)
            {
                audioSource.PlayOneShot(crossOutTaskClip);
            }
            if (wizardVoiceSource != null && wizardCongratulationClip != null)
            {
                wizardVoiceSource.Stop(); // prevents overlap
                wizardVoiceSource.PlayOneShot(wizardCongratulationClip);
            }
        }

        OnPuzzleSolved?.Invoke();
    }

    public void ResetGate()
    {
        isSolved = false;
        wasEitherPressed = false;
        leftPlate.ResetPlate();
        rightPlate.ResetPlate();
        closeGateObj.SetActive(true);
        openGateObj.SetActive(false);

        //Turn the teleport zone back off when resetting
        if (teleportZoneToCh2 != null)
        {
            teleportZoneToCh2.SetActive(false);
        }

        // ---> FIXED: Cleaned up the duplicated if statement <---
        if (taskText != null && !string.IsNullOrEmpty(originalTaskString))
        {
            taskText.text = originalTaskString;
        }

        // Hide extra tasks on reset
        if (findBoulderTaskObj != null) findBoulderTaskObj.SetActive(false);
        if (proceedToNextTaskObj != null) proceedToNextTaskObj.SetActive(false);
    }
}