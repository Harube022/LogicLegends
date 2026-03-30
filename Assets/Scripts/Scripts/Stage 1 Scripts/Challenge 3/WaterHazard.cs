using UnityEngine;
using Photon.Pun;
public class WaterHazard : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Check if what fell in the water was a player
        if (other.CompareTag("Player"))
        {
            PhotonView view = other.GetComponent<PhotonView>();
            
            // Check if we are offline, OR if the network says this player belongs to us
            bool isLocalPlayer = (view == null || !PhotonNetwork.InRoom || view.IsMine);

            if (isLocalPlayer)
            {
                if (LevelManager.Instance != null)
                {
                    // 1. Teleport the player and lose a heart
                    LevelManager.Instance.LoseHeartAndRespawn(other.transform);

                    // 2. SAFETY RESET: Instantly cure the player of the LilyPadTrap effects!
                    Player playerScript = other.GetComponent<Player>();
                    Rigidbody playerRb = other.GetComponent<Rigidbody>();
                    CharacterController cc = other.GetComponent<CharacterController>(); // <-- Add this
                    
                    if (cc != null) cc.enabled = true; // <-- Add this
                    if (playerScript != null) playerScript.enabled = true; 
                    
                    if (playerRb != null) 
                    {
                        playerRb.isKinematic = true; 
                        playerRb.linearVelocity = Vector3.zero; 
                    }
                }
            }
        }
    }
}