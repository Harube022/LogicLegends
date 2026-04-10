using UnityEngine;
using UnityEngine.Events;
using Photon.Pun;

public class TruthTableManager : MonoBehaviourPun
{
    [SerializeField] private TorchPedestal[] answerPedestals;

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

    private void Update()
    {
        if (isSolved) return;

        // ---> FIXED: Only check for MasterClient IF we are actually in a multiplayer room <---
        if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient) return;

        bool allCorrect = true;
        foreach (var ped in answerPedestals)
        {
            // Make sure the pedestal exists and check if it's correct
            if (ped != null && !ped.IsCorrect())
            {
                allCorrect = false;
                break; // Stop checking, one is wrong
            }
        }

        if (allCorrect)
        {
            // ---> FIXED: Fire RPC if online, or just run the method directly if offline <---
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
        Debug.Log("Puzzle Solved!"); // Works offline now!
        OnPuzzleSolved?.Invoke(); // Teleports you to the next challenge!
        if (audioSource != null && gateOpenClip != null)
        {
            audioSource.PlayOneShot(gateOpenClip);
        }
        if (wizardAudioSource != null && wizardCongratsClip != null)
        {
            wizardAudioSource.Stop(); // prevents overlap if somehow retriggered
            wizardAudioSource.PlayOneShot(wizardCongratsClip);
        }
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

        // 2. Force every pedestal to drop its torch
        foreach (var ped in answerPedestals)
        {
            if (ped != null)
            {
                // This clears the pedestal and tells the torch's ResettableObject script to reset
                ped.ClearPedestal();
            }
        }

        // 3. Fire the custom reset event (like closing doors, resetting timers, etc.)
        OnPuzzleReset?.Invoke();
    }
}