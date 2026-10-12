using UnityEngine;
using UnityEngine.Events; // ---> NEW: Required for Unity Events! <---

public class InteractPortal : MonoBehaviour
{
    [Header("Teleport Settings")]
    [Tooltip("Drag the target Spawn Point empty GameObject here.")]
    [SerializeField] private Transform teleportDestination;

    [Header("Optional explicit interaction")]
    [SerializeField] private bool requireInteraction;
    [SerializeField] private bool interactionUnlocked = true;
    [SerializeField, Min(0.5f)] private float interactionRange = 4f;

    public bool InteractionUnlocked => interactionUnlocked;
    public Transform TeleportDestination => teleportDestination;
    public void SetInteractionUnlocked(bool unlocked) => interactionUnlocked = unlocked;

    public bool TryInteract(GameObject player)
    {
        if (!requireInteraction || !interactionUnlocked || player == null || !player.CompareTag("Player") ||
            teleportDestination == null || TeleportManager.Instance == null ||
            Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z),
                new Vector2(transform.position.x, transform.position.z)) > interactionRange ||
            TeleportManager.Instance.IsTeleporting) return false;

        TeleportManager.Instance.StartTeleport(player, teleportDestination);
        onTeleport?.Invoke();
        return true;
    }

    // ---> NEW: An event that fires the exact moment the player uses this portal! <---
    [Header("Portal Events")]
    public UnityEvent onTeleport; 

    private void OnTriggerEnter(Collider other)
    {
        if (requireInteraction || !interactionUnlocked) return;
        if (other.CompareTag("Player"))
        {
            if (TeleportManager.Instance != null)
            {
                TeleportManager.Instance.StartTeleport(other.gameObject, teleportDestination);
                
                // ---> NEW: Fire our custom events! (Like hiding/showing arrows) <---
                onTeleport?.Invoke(); 
            }
            else
            {
                Debug.LogError("No TeleportManager found in the scene!");
            }
        }
    }
}
