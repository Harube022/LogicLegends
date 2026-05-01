using UnityEngine;
using UnityEngine.Events;
using Photon.Pun;
using TMPro;
public class HarvestMatrixManager : MonoBehaviourPun
{
    [SerializeField] private SoilMound[] soilMounds;

    // ---> NEW: UI Task Objects <---
    [Header("UI Task Objects")]
    [Tooltip("Drag the 'Plant the seeds' TextMeshPro object here")]
    [SerializeField] private TextMeshProUGUI plantSeedsTaskText;
    
    [Tooltip("Keep this exact format: {0} is current, {1} is total")]
    [SerializeField] private string plantSeedsBaseText = "Plant the seeds {0}/{1}";

    [Tooltip("Drag the 'Hit the watering can...' GameObject here")]
    [SerializeField] private GameObject waterSeedsTaskObj;

    [Tooltip("Drag the 'Go through the gate...' GameObject here")]
    [SerializeField] private GameObject proceedToGateTaskObj;
    [Header("Audio")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip gateOpenClip;

    [Header("Wizard Voice")]
    [SerializeField] private AudioSource wizardAudioSource;
    [SerializeField] private AudioClip wizardCongratsClip;

    [Header("Puzzle Events (Drag & Drop)")]
    public UnityEvent OnPuzzleSolved;
    public UnityEvent OnPuzzleFailed;
    public UnityEvent OnPuzzleReset;

    // ---> NEW: The Anti-Spam Lock! <---
    private bool isGrading = false;

    // ---> NEW: State trackers for the UI <---
    private bool isSolved = false;
    private int previousSeedCount = -1;

    private void Start()
    {
        // Make sure extra tasks are hidden when the game starts
        if (waterSeedsTaskObj != null) waterSeedsTaskObj.SetActive(false);
        if (proceedToGateTaskObj != null) proceedToGateTaskObj.SetActive(false);
    }

    private void Update()
    {
        // Stop checking if the puzzle is already solved OR if it's currently animating a failure
        if (isSolved || isGrading) return;

        // 1. COUNT THE SEEDS (Runs for everyone so UI updates smoothly)
        int currentSeedCount = 0;
        foreach (var mound in soilMounds)
        {
            if (mound != null && mound.HasSeed()) currentSeedCount++;
        }

        // 2. UPDATE UI IF THE COUNT CHANGED
        if (currentSeedCount != previousSeedCount)
        {
            // Update the 0/4 text
            if (plantSeedsTaskText != null)
            {
                plantSeedsTaskText.text = string.Format(plantSeedsBaseText, currentSeedCount, soilMounds.Length);
            }

            // Show the Watering Can task ONLY if all 4 mounds have seeds!
            if (waterSeedsTaskObj != null)
            {
                waterSeedsTaskObj.SetActive(currentSeedCount == soilMounds.Length);
            }

            previousSeedCount = currentSeedCount;
        }
    }

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
            isSolved = true; // Stop the Update loop from messing with the UI

            foreach (var mound in soilMounds)
            {
                if (mound.currentSeed != null) mound.currentSeed.gameObject.SetActive(false);
            }

            // ---> NEW: Cross out the seed text and hide the watering task <---
            if (plantSeedsTaskText != null)
            {
                string finalString = string.Format(plantSeedsBaseText, soilMounds.Length, soilMounds.Length);
                plantSeedsTaskText.text = "<color=#008000><s>" + finalString + "</s></color>";
            }
            if (waterSeedsTaskObj != null) waterSeedsTaskObj.SetActive(false);

            // ---> NEW: Show the final gate task <---
            if (proceedToGateTaskObj != null) proceedToGateTaskObj.SetActive(true);

            // Audio
            if (sfxSource != null)
            {
                if (gateOpenClip != null) sfxSource.PlayOneShot(gateOpenClip);
            }

            if (wizardAudioSource != null && wizardCongratsClip != null)
            {
                wizardAudioSource.Stop(); 
                wizardAudioSource.PlayOneShot(wizardCongratsClip);
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
        isGrading = false;
        isSolved = false;
        previousSeedCount = -1;
        // ---> 4. The fail animation is done. Unlock the watering can for the next attempt! <---
        // Hide extra tasks immediately on reset
        if (proceedToGateTaskObj != null) proceedToGateTaskObj.SetActive(false);
        if (waterSeedsTaskObj != null) waterSeedsTaskObj.SetActive(false);
        OnPuzzleReset?.Invoke();
    }

}