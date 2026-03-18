using UnityEngine;

public class HarvestMatrixManager : MonoBehaviour
{
    [SerializeField] private SoilMound[] soilMounds;
    
    [Header("Win State")]
    [SerializeField] private GameObject giantBeanstalk;

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
            if (giantBeanstalk != null) giantBeanstalk.SetActive(true);

            // ---> NEW: Hide all the seeds so it looks like they transformed! <---
            foreach (var mound in soilMounds)
            {
                if (mound.currentSeed != null)
                {
                    mound.currentSeed.gameObject.SetActive(false);
                }
            }
        }
        else
        {
            Debug.Log("Incorrect logic! Spitting seeds out!");
            foreach (var mound in soilMounds)
            {
                mound.SpitOutSeed();
            }
            
            Invoke(nameof(TriggerRespawn), 1.5f);
        }
    }
    private void TriggerRespawn()
    {
        if (LevelManager.Instance != null) LevelManager.Instance.LoseHeartAndRespawn();
    }

}