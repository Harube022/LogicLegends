using UnityEngine;
using UnityEngine.Events;
using Photon.Pun;
using TMPro;

public class TruthTableManager : MonoBehaviourPun
{
    [SerializeField] private TorchPedestal[] answerPedestals;

    // ---> NEW: UI Task Objects <---
    [Header("UI Task Objects")]
    [Tooltip("Drag the 'Find Torch' TextMeshPro object here")]
    [SerializeField] private TextMeshProUGUI findTorchTaskText;
    
    [Tooltip("Keep this exact format: {0} is current, {1} is total")]
    [SerializeField] private string findTorchBaseText = "Find torch {0}/{1}";

    [Tooltip("Drag the 'Lit/Unlit the torch' GameObject here")]
    [SerializeField] private GameObject litUnlitTaskObj;

    [Tooltip("Drag the 'Go through the gate...' GameObject here")]
    [SerializeField] private GameObject proceedToGateTaskObj;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip gateOpenClip;
    [Header("Wizard Voice")]
    [SerializeField] private AudioSource wizardAudioSource;
    [SerializeField] private AudioClip wizardCongratsClip;

    [Header("Puzzle Events (Drag & Drop in Inspector)")]
    [Tooltip("What happens when the player gets all torches correct?")]
    public UnityEvent OnPuzzleSolved;

    [Tooltip("What happens if the player fails and we need to reset the room?")]
    public UnityEvent OnPuzzleReset;
    
    private bool isSolved = false;
    private int previousTorchCount = -1;

    private void Start()
    {
        // Make sure extra tasks are hidden when the game starts
        if (litUnlitTaskObj != null) litUnlitTaskObj.SetActive(false);
        if (proceedToGateTaskObj != null) proceedToGateTaskObj.SetActive(false);
    }

    private void Update()
    {
        if (isSolved) return;

        // 1. COUNT TORCHES & CHECK CORRECTNESS (Runs for EVERYONE so UI updates on all screens)
        int currentTorchCount = 0;
        bool allCorrect = true;

        foreach (var ped in answerPedestals)
        {
            if (ped != null)
            {
                if (ped.CurrentTorch != null) currentTorchCount++;
                if (!ped.IsCorrect()) allCorrect = false;
            }
        }

        // 2. UPDATE UI IF THE COUNT CHANGED
        if (currentTorchCount != previousTorchCount)
        {
            // Update the 0/4 text
            if (findTorchTaskText != null)
            {
                findTorchTaskText.text = string.Format(findTorchBaseText, currentTorchCount, answerPedestals.Length);
            }

            // Show the Lit/Unlit task ONLY if they have placed at least 1 torch
            if (litUnlitTaskObj != null)
            {
                litUnlitTaskObj.SetActive(currentTorchCount > 0);
            }

            previousTorchCount = currentTorchCount;
        }

        // 3. STOP HERE IF CLIENT (Only the Master Client triggers the final win state)
        if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient) return;

        // 4. SOLVE PUZZLE
        if (allCorrect)
        {
            if (PhotonNetwork.InRoom)
            {
                photonView.RPC("RPC_PuzzleSolved", RpcTarget.All);
            }
            else
            {
                RPC_PuzzleSolved(); 
            }
        }
    }

    [PunRPC]
    private void RPC_PuzzleSolved()
    {
        isSolved = true;
        
        // ---> NEW: Cross out the torch text and hide the Lit/Unlit task <---
        if (findTorchTaskText != null)
        {
            // It uses the total amount so it perfectly says "4/4" when crossed out
            string finalString = string.Format(findTorchBaseText, answerPedestals.Length, answerPedestals.Length);
            findTorchTaskText.text = "<color=#008000><s>" + finalString + "</s></color>";
        }
        if (litUnlitTaskObj != null) litUnlitTaskObj.SetActive(false);

        // ---> NEW: Show the final gate task <---
        if (proceedToGateTaskObj != null) proceedToGateTaskObj.SetActive(true);

        // Play Audio
        if (audioSource != null)
        {
            if (gateOpenClip != null) audioSource.PlayOneShot(gateOpenClip);
        }

        if (wizardAudioSource != null && wizardCongratsClip != null)
        {
            wizardAudioSource.Stop(); 
            wizardAudioSource.PlayOneShot(wizardCongratsClip);
        }

        OnPuzzleSolved?.Invoke(); 
    }

    public void ResetPuzzle()
    {
        // ---> FIXED: Networked reset if online, direct reset if offline <---
        if (PhotonNetwork.InRoom && photonView != null)
        {
            photonView.RPC("RPC_ResetPuzzle", RpcTarget.All);
        }
        else
        {
            RPC_ResetPuzzle();
        }
    }

    [PunRPC]
    private void RPC_ResetPuzzle()
    {
        // 1. Un-solve the puzzle so it can be checked again on everyone's screen
        isSolved = false;
        previousTorchCount = -1;

        // 2. Force every pedestal to drop its torch
        foreach (var ped in answerPedestals)
        {
            if (ped != null)
            {
                // This clears the pedestal and tells the torch's ResettableObject script to reset
                ped.ClearPedestal();
            }
        }

        // Hide UI immediately on reset
        if (proceedToGateTaskObj != null) proceedToGateTaskObj.SetActive(false);
        if (litUnlitTaskObj != null) litUnlitTaskObj.SetActive(false);

        // 3. Fire the custom reset event (like closing doors, resetting timers, etc.)
        OnPuzzleReset?.Invoke();
    }
}