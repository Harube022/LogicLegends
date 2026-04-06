using UnityEngine;
using Photon.Pun;
public class TorchItem : MonoBehaviourPun
{
    [SerializeField] private bool isLit = false;
    private bool startingState;

    [Header("Torch Models")]
    [SerializeField] private GameObject litModel;
    [SerializeField] private GameObject unlitModel;

    // ---> NEW: Audio Variables <---
    [Header("Audio Settings")]
    [SerializeField] private AudioSource fireLoopSource;
    [Tooltip("Lit/Unlit Sound")]
    [SerializeField] private AudioClip lightUpClip; 
    [Tooltip("Hold Fire Sound")]
    [SerializeField] private AudioClip fireLoopClip;

    [SerializeField, Range(0f, 1f)] private float impactVolume = 0.8f;

    public bool IsLit => isLit;

    private void Awake()
    {
        startingState = isLit;
    }

    private void Start()
    {
        // Keep this local for initialization
        UpdateModels(isLit); 

        if (isLit && fireLoopSource != null && fireLoopClip != null)
        {
            fireLoopSource.clip = fireLoopClip;
            fireLoopSource.loop = true;
            fireLoopSource.Play();
        }
    }

    // Call this to sync the state across the network
    public void SetState(bool state)
    {
        if (isLit == state) return;

        // If we are playing multiplayer, tell everyone to change the flame
        if (PhotonNetwork.InRoom)
        {
            photonView.RPC("RPC_SetFlameState", RpcTarget.All, state);
        }
        else
        {
            // If we are playing solo, just change it locally right now!
            isLit = state;
            UpdateModels(isLit);
            UpdateAudio(isLit);
        }
    }

    [PunRPC]
    private void RPC_SetFlameState(bool state)
    {
        isLit = state;
        UpdateModels(isLit);
        UpdateAudio(isLit);
    }

    // ---> NEW: The Audio Logic <---
    private void UpdateAudio(bool state)
    {
        if (state) // Turning ON
        {
            // 1. Spawn the impact sound (Footstep Magic!)
            if (lightUpClip != null) Spawn3DAudio(lightUpClip, transform.position, impactVolume);
            
            // 2. Start the looping fire on the attached source
            if (fireLoopSource != null && fireLoopClip != null)
            {
                fireLoopSource.clip = fireLoopClip;
                fireLoopSource.loop = true;
                fireLoopSource.Play();
            }
        }
        else // Turning OFF
        {
            // 1. Stop the looping fire
            if (fireLoopSource != null) fireLoopSource.Stop(); 
            
            // 2. Spawn the impact sound for turning off
            if (lightUpClip != null) Spawn3DAudio(lightUpClip, transform.position, impactVolume);
        }
    }

    private void UpdateModels(bool state)
    {
        if (litModel != null) litModel.SetActive(state);
        if (unlitModel != null) unlitModel.SetActive(!state);
    }

    public void ResetFlame()
    {
        SetState(startingState); // This now networks the reset automatically!
    }

    // ---> NEW: The "Footstep Magic" Spawner <---
    private void Spawn3DAudio(AudioClip clip, Vector3 spawnPosition, float volume)
    {
        GameObject audioObj = new GameObject("TempTorchImpact");
        audioObj.transform.position = spawnPosition;

        AudioSource source = audioObj.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume; 
        
        // ---> THIS IS THE FIX! <---
        // Slight random pitch fixes the "loud distortion" issue if multiple torches light up at the exact same millisecond!
        source.pitch = Random.Range(0.9f, 1.1f);
        
        source.spatialBlend = 1f; 
        source.minDistance = 2f;
        source.maxDistance = 15f; 

        source.Play();
        Destroy(audioObj, clip.length + 0.1f);
    }
}