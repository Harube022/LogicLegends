using UnityEngine;
using UnityEngine.Events;
public class TruthTableManager : MonoBehaviour
{
    [SerializeField] private TorchPedestal[] answerPedestals;

    [Header("Puzzle Events (Drag & Drop in Inspector)")]
    [Tooltip("What happens when the player gets all torches correct?")]
    public UnityEvent OnPuzzleSolved;

    [Tooltip("What happens if the player fails and we need to reset the room?")]
    public UnityEvent OnPuzzleReset;
    // [Header("Portals")]
    // [Tooltip("Drag the CLOSED portal asset here")]
    // [SerializeField] private GameObject closedPortal;
    // [Tooltip("Drag the OPEN portal asset here")]
    // [SerializeField] private GameObject openPortal;
    
    private bool isSolved = false;

    private void Update()
    {
        if (isSolved) return;

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
            isSolved = true;
            Debug.Log("NOT Gate Solved!");
            
            OnPuzzleSolved?.Invoke();
            // // ---> NEW: Swap the portals! <---
            // if (closedPortal != null) closedPortal.SetActive(false);
            // if (openPortal != null) openPortal.SetActive(true);

            // // STOP TIMER
            // if (LevelManager.Instance != null) LevelManager.Instance.StopTimer();
        }
    }

    // ---> NEW: Reset Method for Replayability <---
    public void ResetPuzzle()
    {
        // 1. Un-solve the puzzle so it can be checked again
        isSolved = false;

        // Fire the custom reset event
        OnPuzzleReset?.Invoke();
    }
}