using UnityEngine;
using Photon.Pun;
using System.Collections;

public class WaterHazard : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float teleportDelay = 1.0f;

    private bool isProcessingFall = false;

    private void OnTriggerEnter(Collider other)
    {
        // ---> NEW: If we are already processing a fall, completely ignore this collision!
        if (isProcessingFall) return;

        if (other.CompareTag("Player"))
        {
            PhotonView view = other.GetComponent<PhotonView>();
            bool isLocalPlayer = (view == null || !PhotonNetwork.InRoom || view.IsMine);

            if (isLocalPlayer)
            {
                // Lock the trap so it can't be triggered again!
                isProcessingFall = true; 
                StartCoroutine(DelayedRespawn(other.gameObject));
            }
        }
    }

    private IEnumerator DelayedRespawn(GameObject playerObj)
    {
        Player playerScript = playerObj.GetComponent<Player>();
        CharacterController cc = playerObj.GetComponent<CharacterController>(); 
        
        if (playerScript != null) playerScript.enabled = false;
        if (cc != null) cc.enabled = false; 

        yield return new WaitForSeconds(teleportDelay);

        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.LoseHeartAndRespawn(playerObj.transform);
        }

        if (cc != null) cc.enabled = true;

        CapsuleCollider backupCollider = playerObj.GetComponent<CapsuleCollider>();
        if (backupCollider != null) backupCollider.enabled = false; 
        
        if (playerScript != null) playerScript.enabled = true; 

        Rigidbody playerRb = playerObj.GetComponent<Rigidbody>();
        if (playerRb != null) 
        {
            playerRb.isKinematic = true; 
            playerRb.linearVelocity = Vector3.zero; 
        }

        // ---> THE FIX: The Physics Cooldown Buffer <---
        // Wait a tiny moment for the physics engine to settle before unlocking the trap.
        // This absorbs the accidental re-trigger that happens during a Game Over!
        yield return new WaitForSeconds(0.1f); 

        isProcessingFall = false; 
    }
}