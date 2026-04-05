using UnityEngine;
using Photon.Pun;
using System.Collections;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;

public class PlayerSpawner : MonoBehaviour
{
    [Header("Resources Prefab Names")]
    [Tooltip("Type the exact name of the Male prefab in your Resources folder")]
    [SerializeField] private string malePrefabName = "Multiplayer_Male"; 

    [Tooltip("Type the exact name of the Female prefab in your Resources folder")]
    [SerializeField] private string femalePrefabName = "Multiplayer_Female";

    [Header("Spawn Settings")]
    [SerializeField] private Transform spawnPoint;

    private void Start()
    {
        // 1. Check Firebase to see who is logged in BEFORE spawning
        if (FirebaseAuth.DefaultInstance != null && FirebaseAuth.DefaultInstance.CurrentUser != null)
        {
            DetermineCharacterAndSpawn();
        }
        else
        {
            Debug.LogWarning("No user logged in. Defaulting to Male.");
            StartCoroutine(SpawnWhenReady(malePrefabName));
        }
    }

    private void DetermineCharacterAndSpawn()
    {
        string userId = FirebaseAuth.DefaultInstance.CurrentUser.UserId;
        DatabaseReference dbRef = FirebaseDatabase.DefaultInstance.RootReference;

        Debug.Log("Checking Firebase for multiplayer base character...");

        dbRef.Child("users").Child(userId).Child("base_character").GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                // Fallback to male if the database fails
                StartCoroutine(SpawnWhenReady(malePrefabName));
                return;
            }

            DataSnapshot snapshot = task.Result;
            string selectedCharacter = "";

            if (snapshot.Exists && snapshot.Value != null)
            {
                selectedCharacter = snapshot.Value.ToString();
            }

            // Route the correct string name to the Photon Spawner!
            if (selectedCharacter == "Female_Character")
            {
                StartCoroutine(SpawnWhenReady(femalePrefabName));
            }
            else 
            {
                StartCoroutine(SpawnWhenReady(malePrefabName));
            }
        });
    }
    private IEnumerator SpawnWhenReady(string prefabToSpawn)
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
        
        Debug.Log($"Spawning {prefabToSpawn} over the network...");
        
        // Actually spawn the character!
        GameObject spawnedPlayer = PhotonNetwork.Instantiate(prefabToSpawn, finalSpawnPosition, Quaternion.identity);

        // --- NEW: Snap the camera to YOUR character! ---
        // We check "IsMine" so your camera doesn't accidentally follow someone else who joins the room!
        if (spawnedPlayer.GetComponent<PhotonView>().IsMine)
        {
            ThirdPersonCameraController camController = Object.FindFirstObjectByType<ThirdPersonCameraController>();
            if (camController != null)
            {
                camController.SetPlayerTarget(spawnedPlayer.transform);
                camController.WarpCamera(spawnedPlayer.transform); 
            }
        }
    }
}