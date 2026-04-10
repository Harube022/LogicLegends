using UnityEngine;
using Photon.Pun;
using System.Collections;
public class LeverController : MonoBehaviour
{
    [Header("Lever Objects")]
    [SerializeField] private GameObject leverOffObj;
    [SerializeField] private GameObject leverOnObj;

    [Header("Cave Objects")]
    [SerializeField] private GameObject caveClosedObj;
    [SerializeField] private GameObject caveOpenObj;

    [Header("Vine Visuals")]
    [SerializeField] private Renderer vineRenderer;
    [SerializeField] private Material vineOffMaterial;
    [SerializeField] private Material vineOnMaterial;

    [Header("UI Elements")]
    [Tooltip("The red 0 GameObject")]
    [SerializeField] private GameObject uiRedZero;
    [Tooltip("The green 1 GameObject")]
    [SerializeField] private GameObject uiGreenOne;

    // ---> NEW: Optional Tutorial Link! <---
    [Header("Tutorial Link (Optional)")]
    [Tooltip("Leave this EMPTY in the main game. Only assign a manager if this lever is in a tutorial!")]
    [SerializeField] private PuzzleTutorialManager myTutorialManager;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip switchClip;
    [SerializeField] private AudioClip electricityClip;
    [SerializeField] private AudioClip powerDownClip;

    [SerializeField] private AudioClip gateOpenClip;

    public bool isOn = false;
    private PhotonView view; // 2. Network view reference
    private Coroutine audioCoroutine;

    private void Awake()
    {
        view = GetComponent<PhotonView>();
    }

    private void Start()
    {
        // Ensure everything is in the correct starting state
        UpdateVisuals();
    }

    // 3. Your player script still calls this locally!
    public void ToggleLever()
    {
        if (view != null && PhotonNetwork.InRoom)
        {
            // Tell everyone in the room to flip the lever
            view.RPC("RPC_ToggleLever", RpcTarget.All);
        }
        else
        {
            RPC_ToggleLever(); // Fallback for testing offline
        }
    }

    // 4. The actual logic now lives in the network method
    [PunRPC]
    public void RPC_ToggleLever()
    {
        isOn = !isOn;
        UpdateVisuals();
        if (isOn)
        {
            PlayGateOpenSound();
        }

        PlayAudioState();
    }

    private void PlayAudioState()
    {
        if (audioSource == null) return;

        // Stop any currently running sequence so they don't overlap if mashed quickly
        if (audioCoroutine != null) StopCoroutine(audioCoroutine);

        // Determine which sound plays second based on if we are turning it on or off
        AudioClip secondarySound = isOn ? electricityClip : powerDownClip;
        
        // Start the new unified sequence
        audioCoroutine = StartCoroutine(PlaySwitchSequence(secondarySound));
    }

    private IEnumerator PlaySwitchSequence(AudioClip followUpClip)
    {
        // 1. Play the initial switch clack sound
        if (switchClip != null)
        {
            audioSource.PlayOneShot(switchClip);
        }

        // 2. Wait exactly 1 second
        yield return new WaitForSeconds(0.1f);

        // 3. Play the follow up sound (Electricity up OR Power down)
        if (followUpClip != null)
        {
            audioSource.clip = followUpClip;
            audioSource.loop = false; // Ensure looping is fully disabled
            audioSource.Play();
        }
    }

    private void UpdateVisuals()
    {
        // Swap Lever GameObjects
        if (leverOffObj != null) leverOffObj.SetActive(!isOn);
        if (leverOnObj != null) leverOnObj.SetActive(isOn);

        // Swap Cave GameObjects
        if (caveClosedObj != null) caveClosedObj.SetActive(!isOn);
        if (caveOpenObj != null) caveOpenObj.SetActive(isOn);

        // Update UI (Checking for null just in case you haven't assigned them yet)
        if (uiRedZero != null) uiRedZero.SetActive(!isOn);
        if (uiGreenOne != null) uiGreenOne.SetActive(isOn);

        // Update Vine Material
        if (vineRenderer != null)
        {
            vineRenderer.material = isOn ? vineOnMaterial : vineOffMaterial;
        }

        // ---> FIXED: Talk ONLY to the assigned manager (if one exists) <---
        if (myTutorialManager != null)
        {
            myTutorialManager.AdvanceTutorial(this.transform); 
        }
    }

    public void ResetLever()
    {
        isOn = false;
        UpdateVisuals();

        if (audioSource != null) audioSource.Stop();
    }
    private void PlayGateOpenSound()
    {
        if (audioSource == null || gateOpenClip == null) return;

        audioSource.PlayOneShot(gateOpenClip);
    }

}