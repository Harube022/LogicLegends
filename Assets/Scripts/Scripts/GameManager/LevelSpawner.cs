using UnityEngine;
using Unity.Cinemachine;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;

public class LevelSpawner : MonoBehaviour
{
    [Header("Player Prefabs")]
    [SerializeField] private GameObject malePrefab;
    [SerializeField] private GameObject femalePrefab;

    [Header("Spawn Location")]
    [SerializeField] private Transform spawnPoint;

    [Header("Camera Reference")]
    [SerializeField] private CinemachineCamera cmCamera;

    private void Start()
    {
        // Make sure we are logged in before trying to spawn
        if (FirebaseAuth.DefaultInstance != null && FirebaseAuth.DefaultInstance.CurrentUser != null)
        {
            SpawnPlayerFromDatabase();
        }
        else
        {
            Debug.LogWarning("No user logged in. Spawning default Male character for testing.");
            SpawnAndSetupPlayer(malePrefab);
        }
    }

    private void SpawnPlayerFromDatabase()
    {
        string userId = FirebaseAuth.DefaultInstance.CurrentUser.UserId;
        DatabaseReference dbRef = FirebaseDatabase.DefaultInstance.RootReference;

        Debug.Log("Checking database for base character...");

        dbRef.Child("users").Child(userId).Child("base_character").GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("Failed to get character data. Spawning default.");
                SpawnAndSetupPlayer(malePrefab);
                return;
            }

            DataSnapshot snapshot = task.Result;
            string selectedCharacter = "";

            if (snapshot.Exists && snapshot.Value != null)
            {
                selectedCharacter = snapshot.Value.ToString();
            }

            // Spawn the correct prefab based on the exact string!
            if (selectedCharacter == "Female_Character")
            {
                SpawnAndSetupPlayer(femalePrefab);
                Debug.Log("Spawned Female Character!");
            }
            else 
            {
                // Defaults to male if it is "Male_Character" or if the data is completely missing
                SpawnAndSetupPlayer(malePrefab);
                Debug.Log("Spawned Male Character!");
            }
        });
    }

    // --- NEW: The Setup Manager ---
    private void SpawnAndSetupPlayer(GameObject prefabToSpawn)
    {
        // 1. Spawn the physical player into the world
        GameObject spawnedPlayer = Instantiate(prefabToSpawn, spawnPoint.position, spawnPoint.rotation);

        // 2. Find the child "CameraTarget" inside the spawned player prefab
        Transform cameraTarget = spawnedPlayer.transform.Find("CameraTarget");
        Transform targetToFollow = cameraTarget != null ? cameraTarget : spawnedPlayer.transform;

        // 3. Fallback to find camera if not assigned in Inspector
        if (cmCamera == null)
        {
            cmCamera = FindFirstObjectByType<CinemachineCamera>();
        }

        // 4. Assign target to Cinemachine
        if (cmCamera != null)
        {
            cmCamera.Target.TrackingTarget = targetToFollow;
        }

        // 2. Find the Camera Pivot and tell it to follow our new player!
        // ThirdPersonCameraController camController = Object.FindFirstObjectByType<ThirdPersonCameraController>();
        // if (camController != null)
        // {
        //     camController.SetPlayerTarget(spawnedPlayer.transform);
            
        //     // We Warp it so the camera snaps instantly behind the player instead of flying across the map!
        //     camController.WarpCamera(spawnedPlayer.transform); 
        // }

        // 5. Keep the LevelManager from breaking! (See Step 2 below)
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.player = spawnedPlayer.transform;
        }
    }
}