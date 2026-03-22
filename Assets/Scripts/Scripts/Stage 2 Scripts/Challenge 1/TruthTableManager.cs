using UnityEngine;
using UnityEngine.Events;
using Photon.Pun;

public class TruthTableManager : MonoBehaviourPun
{
    [SerializeField] private TorchPedestal[] answerPedestals;

    [Header("Puzzle Events (Drag & Drop in Inspector)")]
    [Tooltip("What happens when the player gets all torches correct?")]
    public UnityEvent OnPuzzleSolved;

    [Tooltip("What happens if the player fails and we need to reset the room?")]
    public UnityEvent OnPuzzleReset;
    
    private bool isSolved = false;

    private void Update()
    {
        if (isSolved) return;

        // ONLY the Master Client evaluates the puzzle to prevent double-firing
        if (!PhotonNetwork.IsMasterClient) return;

        bool allCorrect = true;
        foreach (var ped in answerPedestals)
        {
            if (!ped.IsCorrect())
            {
                allCorrect = false;
                break;
            }
        }

        if (allCorrect)
        {
            photonView.RPC("RPC_PuzzleSolved", RpcTarget.All);
        }
    }

    [PunRPC]
    private void RPC_PuzzleSolved()
    {
        isSolved = true;
        Debug.Log("NOT Gate Solved Networked!");
        OnPuzzleSolved?.Invoke();
    }

// ---> FIXED: Networked the reset so all players see the torches drop <---
    public void ResetPuzzle()
    {
        // Tell everyone in the room to reset the puzzle
        if (photonView != null)
        {
            photonView.RPC("RPC_ResetPuzzle", RpcTarget.All);
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