using UnityEngine;
using UnityEngine.Events;
using Photon.Pun;
public class HarvestMatrixManager : MonoBehaviourPun
{
    [SerializeField] private SoilMound[] soilMounds;
    
    [Header("Puzzle Events (Drag & Drop)")]
    public UnityEvent OnPuzzleSolved;
    public UnityEvent OnPuzzleFailed;
    public UnityEvent OnPuzzleReset;

    // ---> NEW: The Anti-Spam Lock! <---
    private bool isGrading = false;

    // The player interacts with this object to submit their answer
    public void WaterGarden()
    {
        // 1. If we are already grading, completely ignore the click!
        if (isGrading) return;

        // Tell everyone on the network to grade the puzzle!
        if (PhotonNetwork.InRoom) photonView.RPC("RPC_WaterGarden", RpcTarget.All);
        else RPC_WaterGarden();
    }

    [PunRPC]
    public void RPC_WaterGarden()
    {
        // 2. Lock the network too, just in case Player 2 clicked at the exact same time
        if (isGrading) return;

        // 1. Check if the player filled all 4 holes first
        foreach (var mound in soilMounds)
        {
            if (!mound.HasSeed())
            {
                Debug.Log("The garden isn't fully planted yet!");
                return; 
            }
        }

        // ---> 3. The garden is full! Lock the watering can so they can't spam it <---
        isGrading = true;

        // 2. Grade the Truth Table
        bool allCorrect = true;
        foreach (var mound in soilMounds)
        {
            if (!mound.IsCorrect())
            {
                allCorrect = false;
                break;
            }
        }

        // 3. Win or Lose!
        if (allCorrect)
        {
            Debug.Log("Harvest Matrix Solved! Growing Beanstalk!");
            foreach (var mound in soilMounds)
            {
                if (mound.currentSeed != null) mound.currentSeed.gameObject.SetActive(false);
            }

            OnPuzzleSolved?.Invoke();
        }
        else
        {
            Debug.Log("Incorrect logic! Spitting seeds out!");
            foreach (var mound in soilMounds)
            {
                mound.SpitOutSeed();
            }
            
            // ---> FIX 2: PUNISH WITHOUT TELEPORTING! <---
            // We use IsMasterClient to prevent double-penalties over the network
            if (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient)
            {
                if (LevelManager.Instance != null)
                {
                    LevelManager.Instance.LoseHeart(); // The heart drops, but the players don't move!
                }
            }

            Invoke(nameof(TriggerFailEvent), 1.5f);
        }
    }
    private void TriggerFailEvent()
    {
        OnPuzzleFailed?.Invoke();
        
        // ---> 4. The fail animation is done. Unlock the watering can for the next attempt! <---
        isGrading = false;
    }

    public void ResetPuzzle()
    {
        if (PhotonNetwork.InRoom) photonView.RPC("RPC_ResetPuzzle", RpcTarget.All);
        else RPC_ResetPuzzle();
    }

    [PunRPC]
    public void RPC_ResetPuzzle()
    {
        // ---> 4. The fail animation is done. Unlock the watering can for the next attempt! <---
        isGrading = false;
        OnPuzzleReset?.Invoke();
    }

}