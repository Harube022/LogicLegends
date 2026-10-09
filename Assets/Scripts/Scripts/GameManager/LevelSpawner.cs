using UnityEngine;
using Unity.Cinemachine;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;

#if UNITY_EDITOR
[DefaultExecutionOrder(-1000)]
#endif
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

    private void Awake()
    {
#if UNITY_EDITOR
        // Direct Play in PRELIM has no menu selection. Match its active area to
        // the spawn point assigned for debugging before area/quiz startup runs.
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "PRELIM" ||
            Time.frameCount > 1 ||
            StageSelectionState.HasExplicitSelection || spawnPoint == null) return;

        AreaVisibilityManager area = FindFirstObjectByType<AreaVisibilityManager>(FindObjectsInactive.Include);
        if (area != null && area.TryGetStageForSpawnPoint(spawnPoint, out int stageNumber))
            StageSelectionState.SelectForEditorSpawn(stageNumber);
#endif
    }

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

            // Accept both short names and saved IDs such as Female_Character.
            if (selectedCharacter.IndexOf("female", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (femalePrefab != null)
                {
                    SpawnAndSetupPlayer(femalePrefab);
                }
                else
                {
                    Debug.LogError("LevelSpawner: Female Prefab is missing in the Inspector!");
                    SpawnAndSetupPlayer(malePrefab);
                }
            }
            else
            {
                SpawnAndSetupPlayer(malePrefab);
            }
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
        {
            cmCamera.Target.TrackingTarget = targetToFollow;
            // A spawned target has no valid follow history at the scene camera's old position.
            if (TeleportManager.UsesCoveredTransitions) cmCamera.PreviousStateIsValid = false;
        }

        if (LevelManager.Instance != null)
            LevelManager.Instance.player = playerObject.transform;
    }
}

