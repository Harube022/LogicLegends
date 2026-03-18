using UnityEngine;
using Photon.Pun;
using System.Collections;

public class PlayerSpawner : MonoBehaviour
{
    [Tooltip("The exact name of your player prefab inside the Resources folder.")]
    [SerializeField] private string playerPrefabName = "Multiplayer_Player"; 

    [Tooltip("Where should the player spawn?")]
    [SerializeField] private Transform spawnPoint;

    private void Start()
    {
        // Start a Coroutine to wait safely until Photon is 100% ready
        StartCoroutine(SpawnWhenReady());
        // Changed to InRoom to guarantee we don't spawn before the room is fully loaded
        // if (PhotonNetwork.InRoom)
        // {
        //     Vector3 basePosition = spawnPoint != null ? spawnPoint.position : Vector3.zero;
            
        //     // Add a random offset so players don't spawn inside each other
        //     Vector2 randomCircle = Random.insideUnitCircle * 2f; 
            
        //     // NEW FIX: Add Vector3.up * 3f to spawn them in the air so they don't fall through the floor!
        //     Vector3 finalSpawnPosition = basePosition + new Vector3(randomCircle.x, 3f, randomCircle.y);
            
        //     Debug.Log("Spawning Player...");
        //     PhotonNetwork.Instantiate(playerPrefabName, finalSpawnPosition, Quaternion.identity);
        // }
    }
    private IEnumerator SpawnWhenReady()
    {
        // This loop pauses the script and waits until InRoom becomes true
        while (!PhotonNetwork.InRoom)
        {
            yield return null; 
        }

        // Once we are officially in the room, execute the spawn logic!
        Vector3 basePosition = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        Vector2 randomCircle = Random.insideUnitCircle * 2f; 
        
        Vector3 finalSpawnPosition = basePosition + new Vector3(randomCircle.x, 3f, randomCircle.y);
        
        Debug.Log("Spawning Player...");
        PhotonNetwork.Instantiate(playerPrefabName, finalSpawnPosition, Quaternion.identity);
    }
}