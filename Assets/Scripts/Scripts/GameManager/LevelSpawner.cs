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

    private bool playerCommitted;

    private void Start()
    {
        // Scene transitions may already have supplied the local player. Reuse it instead
        // of starting a delayed database spawn that would create a duplicate.
        if (TryUseExistingPlayer()) return;

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
            string selectedCharacter = snapshot.Exists && snapshot.Value != null
                ? snapshot.Value.ToString()
                : string.Empty;

            if (selectedCharacter == "Female_Character")
                SpawnAndSetupPlayer(femalePrefab);
            else
                SpawnAndSetupPlayer(malePrefab);
        });
    }

    private void SpawnAndSetupPlayer(GameObject prefabToSpawn)
    {
        // Firebase returns asynchronously. Recheck at commit time in case another
        // scene system supplied the player while the request was in flight.
        if (playerCommitted || TryUseExistingPlayer()) return;

        if (prefabToSpawn == null || spawnPoint == null)
        {
            Debug.LogError("LevelSpawner cannot create the player: prefab or spawn point is missing.");
            return;
        }

        playerCommitted = true;
        GameObject spawnedPlayer = Instantiate(prefabToSpawn, spawnPoint.position, spawnPoint.rotation);
        SetupPlayer(spawnedPlayer);
        Debug.Log($"LevelSpawner created the single local player '{spawnedPlayer.name}'.");
    }

    private bool TryUseExistingPlayer()
    {
        Player existing = Player.LocalInstance;
        if (existing == null || !existing.gameObject.activeInHierarchy) return false;

        if (!playerCommitted)
            Debug.Log($"LevelSpawner is reusing existing local player '{existing.name}'.");

        playerCommitted = true;
        SetupPlayer(existing.gameObject);
        return true;
    }

    private void SetupPlayer(GameObject playerObject)
    {
        Transform cameraTarget = playerObject.transform.Find("CameraTarget");
        Transform targetToFollow = cameraTarget != null ? cameraTarget : playerObject.transform;

        if (cmCamera == null)
        {
            GameObject gameplayCameraObject = GameObject.Find("CM_ThirdPersonCam");
            cmCamera = gameplayCameraObject != null
                ? gameplayCameraObject.GetComponent<CinemachineCamera>()
                : FindFirstObjectByType<CinemachineCamera>();
        }

        if (cmCamera != null)
            cmCamera.Target.TrackingTarget = targetToFollow;

        if (LevelManager.Instance != null)
            LevelManager.Instance.player = playerObject.transform;
    }
}

