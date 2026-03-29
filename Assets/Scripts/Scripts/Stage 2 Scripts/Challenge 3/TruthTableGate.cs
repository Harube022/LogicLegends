using UnityEngine;
using UnityEngine.Events;
using Photon.Pun;

public class TruthTableGate : MonoBehaviourPun
{
    [Header("Puzzle Requirements")]
    [Tooltip("Drag the 3 RED CRYSTALS from the gate in here")]
    [SerializeField] private GateIndicator[] requiredIndicators;
    
    [Header("Puzzle Events")]
    public UnityEvent OnPuzzleSolved;
    public UnityEvent OnPuzzleFailed;
    public UnityEvent OnPuzzleReset;

    // ---> NEW: The Anti-Spam lock! <---
    public bool isLocked = false;

    // Called by the indicators whenever they light up
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

        // If all 3 are correct, open the gate!
        if (allPowered)
        {
            isLocked = true; // Lock the puzzle so they can't mess it up after winning!
            Debug.Log("The OR Gate is solved!");
            OnPuzzleSolved?.Invoke();
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

        // ---> FIX 3: Find EVERY crystal on the door (both True and False) and wipe them all! <---
        GateIndicator[] allIndicators = GetComponentsInChildren<GateIndicator>();
        foreach (GateIndicator indicator in allIndicators)
        {
            if (indicator != null) indicator.ResetIndicator();
        }

        OnPuzzleReset?.Invoke();
    }
}