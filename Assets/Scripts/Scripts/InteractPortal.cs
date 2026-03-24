using UnityEngine;

public class InteractPortal : MonoBehaviour
{
    [Header("Teleport Settings")]
    [Tooltip("Drag the target Spawn Point empty GameObject here.")]
    [SerializeField] private Transform teleportDestination;

    private void OnTriggerEnter(Collider other)
    {
        // If the player touches the portal, tell the Manager to do its job
        if (other.CompareTag("Player"))
        {
            if (TeleportManager.Instance != null)
            {
                TeleportManager.Instance.StartTeleport(other.gameObject, teleportDestination);
            }
            else
            {
                Debug.LogError("No TeleportManager found in the scene!");
            }
        }
    }
}