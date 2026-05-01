using UnityEngine;
using UnityEngine.Events;
using Photon.Pun;
using TMPro;

public class TruthTableGate : MonoBehaviourPun
{
    [Header("Puzzle Requirements")]
    [Tooltip("Drag the 3 RED CRYSTALS from the gate in here")]
    [SerializeField] private GateIndicator[] requiredIndicators;

    // ---> NEW: UI Task Objects <---
    [Header("UI Task Objects")]
    [Tooltip("Drag the 'Rotate the Crystals' TextMeshPro object here")]
    [SerializeField] private TextMeshProUGUI rotateCrystalsTaskText;
    
    [Tooltip("Keep this exact format: {0} is current, {1} is total")]
    [SerializeField] private string rotateCrystalsBaseText = "Rotate the Crystals {0}/{1}";

    [Tooltip("Drag the 'Activate all gate indicators...' GameObject here")]
    [SerializeField] private GameObject activateAllIndicatorsTaskObj;

    [Tooltip("Drag the 'Go through the portal...' GameObject here")]
    [SerializeField] private GameObject proceedToPortalTaskObj;

    [Header("Gate Audio")]
    [SerializeField] private AudioSource gateAudioSource;
    [SerializeField] private AudioClip gateOpenClip;

    [Header("Wizard Voice")]
    [SerializeField] private AudioSource wizardAudioSource;
    [SerializeField] private AudioClip wizardCongratsClip;

    [Header("Puzzle Events")]
    public UnityEvent OnPuzzleSolved;
    public UnityEvent OnPuzzleFailed;
    public UnityEvent OnPuzzleReset;

    // ---> NEW: The Anti-Spam lock! <---
    public bool isLocked = false;

    // ---> NEW: State trackers for the UI <---
    private bool isSolved = false;
    private int previousPoweredCount = -1;

    // Called by the indicators whenever they light up

    private void Start()
    {
        // Hide the final task when the game starts
        if (activateAllIndicatorsTaskObj != null) activateAllIndicatorsTaskObj.SetActive(false);
        if (proceedToPortalTaskObj != null) proceedToPortalTaskObj.SetActive(false);
    }

    // ---> NEW: Constantly count how many indicators are hit by a Green Beam! <---
    private void Update()
    {
        // Stop checking if the puzzle is solved or currently locked in a fail animation
        if (isSolved || isLocked) return;

        int currentPoweredCount = 0;
        foreach (GateIndicator indicator in requiredIndicators)
        {
            if (indicator != null && indicator.isPoweredCorrectly)
            {
                currentPoweredCount++;
            }
        }

        // Update the UI only if the number changed
        if (currentPoweredCount != previousPoweredCount)
        {
            if (rotateCrystalsTaskText != null)
            {
                rotateCrystalsTaskText.text = string.Format(rotateCrystalsBaseText, currentPoweredCount, requiredIndicators.Length);
            }

            // ---> NEW: Show the intermediate task ONLY if at least 1 crystal is powered! <---
            if (activateAllIndicatorsTaskObj != null)
            {
                activateAllIndicatorsTaskObj.SetActive(currentPoweredCount > 0);
            }

            previousPoweredCount = currentPoweredCount;
        }
    }
    public void EvaluateGate()
    {
        bool allPowered = true;

        // Loop through all 3 indicators to see if they are green
        foreach (GateIndicator indicator in requiredIndicators)
        {
            if (!indicator.isPoweredCorrectly)
            {
                allPowered = false;
                break; // One is wrong, stop checking
            }
        }

        if (allPowered)
        {
            isLocked = true; 
            isSolved = true; // ---> NEW: Stop the UI loop from running
            Debug.Log("The OR Gate is solved!");
            
            // ---> NEW: Cross out the task text <---
            if (rotateCrystalsTaskText != null)
            {
                // Force it to say 3/3 before crossing out
                string finalString = string.Format(rotateCrystalsBaseText, requiredIndicators.Length, requiredIndicators.Length);
                rotateCrystalsTaskText.text = "<color=#008000><s>" + finalString + "</s></color>";
            }

            // ---> NEW: Hide the intermediate task because they finished the puzzle! <---
            if (activateAllIndicatorsTaskObj != null) activateAllIndicatorsTaskObj.SetActive(false);

            // ---> NEW: Show the final portal task <---
            if (proceedToPortalTaskObj != null) proceedToPortalTaskObj.SetActive(true);

            OnPuzzleSolved?.Invoke();

            // Play Audio
            if (gateAudioSource != null)
            {
                if (gateOpenClip != null) gateAudioSource.PlayOneShot(gateOpenClip);
            }

            if (wizardAudioSource != null && wizardCongratsClip != null)
            {
                wizardAudioSource.Stop(); 
                wizardAudioSource.PlayOneShot(wizardCongratsClip);
            }
        }
    }

    // ---> NEW: THE PENALTY LOGIC <---
    public void TriggerFailSequence()
    {
        if (isLocked) return;
        
        if (PhotonNetwork.InRoom) photonView.RPC("RPC_TriggerFail", RpcTarget.All);
        else RPC_TriggerFail();
    }

    [PunRPC]
    public void RPC_TriggerFail()
    {
        if (isLocked) return;
        isLocked = true; // Lock the network!

        // Drop 1 heart (Only the Master Client does this so it doesn't double-drop)
        if (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient)
        {
            if (LevelManager.Instance != null) LevelManager.Instance.LoseHeart();
        }

        // Wait 1.5 seconds so the players can see the red crystal before the map resets!
        Invoke(nameof(ExecuteFailEvent), 1.5f);
    }

    private void ExecuteFailEvent()
    {
        OnPuzzleFailed?.Invoke();
    }

    // ---> NEW: Unlocks the puzzle on a reset <---
    public void ResetGate()
    {
        RPC_ResetGate();
    }

    [PunRPC]
    public void RPC_ResetGate()
    {
        isLocked = false;

        isSolved = false;
        previousPoweredCount = -1;

        // Hide the portal task immediately
        if (activateAllIndicatorsTaskObj != null) activateAllIndicatorsTaskObj.SetActive(false);
        if (proceedToPortalTaskObj != null) proceedToPortalTaskObj.SetActive(false);

        // ---> FIX 3: Find EVERY crystal on the door (both True and False) and wipe them all! <---
        GateIndicator[] allIndicators = GetComponentsInChildren<GateIndicator>();
        foreach (GateIndicator indicator in allIndicators)
        {
            if (indicator != null) indicator.ResetIndicator();
        }

        OnPuzzleReset?.Invoke();
    }
}