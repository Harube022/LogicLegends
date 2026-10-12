using System.Collections;
using System.Linq;
using Firebase.Auth;
using Firebase.Database;
using Photon.Pun;
using UnityEngine;

/// <summary>Spawns exactly one owned avatar, with a bounded profile lookup and grounded room slots.</summary>
public class PlayerSpawner : MonoBehaviour
{
    [Header("Resources Prefab Names")]
    [SerializeField] private string malePrefabName = "Multiplayer_Male";
    [SerializeField] private string femalePrefabName = "Multiplayer_Female";
    [Header("Spawn Settings")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField, Min(2f)] private float slotSpacing = 6f;
    [SerializeField, Range(-30f, 60f)] private float initialCameraPitch = 18f;
    public GameObject SpawnedPlayer { get; private set; }
    public string Status { get; private set; } = "Waiting for the multiplayer room.";

    private IEnumerator Start()
    {
        float until = Time.realtimeSinceStartup + 10f;
        while (!PhotonNetwork.InRoom && Time.realtimeSinceStartup < until) yield return null;
        if (!PhotonNetwork.InRoom) { Status = "Open this stage through Play > Multiplayer."; Debug.LogWarning(Status, this); yield break; }
        if (Player.LocalInstance != null) { SpawnedPlayer = Player.LocalInstance.gameObject; yield break; }

        string prefabName = malePrefabName;
        // Never access Firebase before dependency initialization or wait indefinitely for a profile.
        if (FirebaseManager.IsReady && FirebaseAuth.DefaultInstance.CurrentUser != null)
        {
            string uid = FirebaseAuth.DefaultInstance.CurrentUser.UserId;
            var task = FirebaseDatabase.DefaultInstance.RootReference.Child("users").Child(uid).Child("base_character").GetValueAsync();
            until = Time.realtimeSinceStartup + 3f;
            while (!task.IsCompleted && Time.realtimeSinceStartup < until) yield return null;
            if (task.IsCompleted && !task.IsFaulted && !task.IsCanceled &&
                FirebaseAuth.DefaultInstance.CurrentUser?.UserId == uid &&
                (task.Result.Value?.ToString() ?? "").ToLowerInvariant().Contains("female")) prefabName = femalePrefabName;
        }
        if (!PhotonNetwork.InRoom || Player.LocalInstance != null) yield break;
        var prefab = Resources.Load<GameObject>(prefabName);
        if (prefab == null || prefab.GetComponent<PhotonView>() == null)
        {
            Status = "The multiplayer character prefab is unavailable.";
            Debug.LogError(Status + " Resources/" + prefabName, this); yield break;
        }

        int[] actors = PhotonNetwork.PlayerList.Select(p => p.ActorNumber).OrderBy(n => n).ToArray();
        int slot = Mathf.Max(0, System.Array.IndexOf(actors, PhotonNetwork.LocalPlayer.ActorNumber)) % 4;
        Vector3 origin = spawnPoint != null ? spawnPoint.position : transform.position;
        var offset = new Vector3(slot % 2 == 0 ? -0.5f : 0.5f, 0f, slot < 2 ? -0.5f : 0.5f) * slotSpacing;
        Quaternion facing = spawnPoint != null ? spawnPoint.rotation : transform.rotation;
        Vector3 position = origin + facing * offset;
        if (Physics.Raycast(position + Vector3.up * 30f, Vector3.down, out var ground, 60f, ~(1 << 7), QueryTriggerInteraction.Ignore))
        {
            var controller = prefab.GetComponent<CharacterController>();
            float clearance = controller == null ? 0.2f : Mathf.Max(0.2f,
                (controller.height * 0.5f - controller.center.y + controller.skinWidth) * prefab.transform.localScale.y);
            position.y = ground.point.y + clearance;
        }
        Status = "Spawning " + prefabName + ".";
        SpawnedPlayer = PhotonNetwork.Instantiate(prefabName, position, facing);
        // Let Player.Start bind the inherited CameraTarget before snapping Cinemachine.
        yield return null;
        if (SpawnedPlayer != null && SpawnedPlayer.GetComponent<PhotonView>().IsMine)
        {
            var look = SpawnedPlayer.GetComponentInChildren<CameraTargetController>();
            if (look != null) look.SetViewAngles(initialCameraPitch, facing.eulerAngles.y);
            TeleportCameraSync.ResetAfterMove(SpawnedPlayer.transform, Vector3.zero);
            Status = "Your player is ready.";
        }
    }
}
