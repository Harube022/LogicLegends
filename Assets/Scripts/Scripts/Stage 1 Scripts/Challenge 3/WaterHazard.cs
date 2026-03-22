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
            
            // ONLY the player who actually fell in should send the command to lose a heart
            // This prevents the game from dropping 2 hearts if both computers detect the splash
            if (view != null && view.IsMine)
            {
                if (LevelManager.Instance != null)
                {
                    // Pass THIS specific player's transform to the LevelManager
                    LevelManager.Instance.LoseHeartAndRespawn(other.transform);
                }
            }
            // Fallback for solo testing
            else if (view == null && !PhotonNetwork.InRoom)
            {
                if (LevelManager.Instance != null)
                {
                    LevelManager.Instance.LoseHeartAndRespawn(other.transform);
                }
            }
        }
    }
}