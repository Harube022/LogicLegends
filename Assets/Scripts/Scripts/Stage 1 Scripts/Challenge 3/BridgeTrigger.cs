using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;
using TMPro;

public class BridgeTrigger : MonoBehaviour
{
    [Header("Debug")]
    [Tooltip("Watch this checkmark in the Inspector to see when it fires!")]
    [SerializeField] private bool hasFinished = false;

    // ---> NEW: UI References <---
    [Header("Objectives UI")]
    [SerializeField] private TextMeshProUGUI taskText;

    // ---> NEW: Field for the follow-up task <---
    [Tooltip("Drag the follow-up task GameObject here")]
    [SerializeField] private GameObject proceedToPortalTaskObj;
    private string originalTaskString;

    // ---> NEW: Audio Settings <---
    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip crossOutTaskClip;
    [SerializeField, Range(0f, 1f)] private float audioVolume = 0.8f;

    private HashSet<int> finishedPlayers = new HashSet<int>();
    private PhotonView view;

    private Collider myCollider;
    private MeshRenderer myRenderer;

    private void Awake()
    {
        view = GetComponent<PhotonView>();
    }

    // ---> NEW: Save the clean text <---
    private void Start()
    {
        if (taskText != null) originalTaskString = taskText.text;
        if (proceedToPortalTaskObj != null) proceedToPortalTaskObj.SetActive(false);
    }

    // Handles it if the bridge is a trigger (Is Trigger is CHECKED)
    private void OnTriggerEnter(Collider other)
    {
        if (hasFinished) return;
        if (other.CompareTag("Player")) CheckPlayerCrossed(other.gameObject);
    }

    // Handles it if the bridge is solid (Is Trigger is UNCHECKED)
    private void OnCollisionEnter(Collision collision)
    {
        if (hasFinished) return;
        if (collision.gameObject.CompareTag("Player")) CheckPlayerCrossed(collision.gameObject);
    }

    private void CheckPlayerCrossed(GameObject playerObj)
    {
        PhotonView playerView = playerObj.GetComponent<PhotonView>();

        // ---> THE SOLO MODE FIX <---
        // Assume it's our player if we are offline, otherwise ask Photon
        bool isOurPlayer = true;
        if (playerView != null && PhotonNetwork.InRoom)
        {
            isOurPlayer = playerView.IsMine;
        }

        if (isOurPlayer)
        {
            if (view != null && PhotonNetwork.InRoom)
            {
                // ONLINE: Send the player's specific network ID
                view.RPC("RPC_PlayerCrossed", RpcTarget.All, playerView.ViewID);
            }
            else
            {
                // OFFLINE: Send a dummy ID since we are the only one playing
                RPC_PlayerCrossed(0);
            }
        }
    }

    [PunRPC]
    public void RPC_PlayerCrossed(int playerViewID)
    {
        finishedPlayers.Add(playerViewID);

        int totalPlayersExpected = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.PlayerCount : 1;

        if (finishedPlayers.Count >= totalPlayersExpected && !hasFinished)
        {
            CompleteChallenge();
        }
    }

    private void CompleteChallenge()
    {
        hasFinished = true;

        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.StopTimer();
            LevelManager.Instance.HideTimer();
            Debug.Log("Challenge 3 Complete! Timer Stopped.");
        }
        // ---> NEW: Cross out the text! <---
        if (taskText != null)
        {
            taskText.text = "<color=#008000><s>" + originalTaskString + "</s></color>";
            
            // ---> UPDATED: Play in 2D using the assigned AudioSource <---
            if (audioSource != null && crossOutTaskClip != null)
            {
                audioSource.PlayOneShot(crossOutTaskClip, audioVolume);
            }
        }

        // ---> THE INVISIBLE WALL FIX <---
        // Instead of Destroy() which breaks over the network, or disabling the collider which leaves invisible meshes,
        // we turn off the entire GameObject safely so it stops blocking you!
        // gameObject.SetActive(false);

        // ---> UPDATED: Disable the collider and renderer instead of the whole GameObject <---
        if (myCollider != null) myCollider.enabled = false;
        if (myRenderer != null) myRenderer.enabled = false;
    }

    // ---> NEW: Reset the Bridge and the Text on Game Over! <---
    public void ResetBridge()
    {
        hasFinished = false;
        finishedPlayers.Clear();
        gameObject.SetActive(true); // Turn the invisible wall trigger back on!

        // ---> UPDATED: Turn the collider and renderer back on! <---
        if (myCollider != null) myCollider.enabled = true;
        if (myRenderer != null) myRenderer.enabled = true;

        if (taskText != null && !string.IsNullOrEmpty(originalTaskString))
        {
            taskText.text = originalTaskString;
        }
        if (proceedToPortalTaskObj != null) proceedToPortalTaskObj.SetActive(false);
    }

}