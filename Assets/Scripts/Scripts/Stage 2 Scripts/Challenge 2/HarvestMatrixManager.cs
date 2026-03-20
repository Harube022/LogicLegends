using UnityEngine;
using UnityEngine.Events;
public class HarvestMatrixManager : MonoBehaviour
{
    [SerializeField] private SoilMound[] soilMounds;
    
    [Header("Puzzle Events (Drag & Drop)")]
    public UnityEvent OnPuzzleSolved;
    public UnityEvent OnPuzzleFailed;
    public UnityEvent OnPuzzleReset;

    // The player interacts with this object to submit their answer
    public void WaterGarden()
    {
        // 1. Check if the player filled all 4 holes first
        foreach (var mound in soilMounds)
        {
            if (!mound.HasSeed())
            {
                Debug.Log("The garden isn't fully planted yet!");
                return; 
            }
        }

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
            
            Invoke(nameof(TriggerFailEvent), 1.5f);
        }
    }
    private void TriggerFailEvent()
    {
        OnPuzzleFailed?.Invoke();
    }

    public void ResetPuzzle()
    {
        OnPuzzleReset?.Invoke();
    }

}