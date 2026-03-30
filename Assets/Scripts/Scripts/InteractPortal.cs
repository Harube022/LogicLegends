using UnityEngine;
using UnityEngine.Events; // ---> NEW: Required for Unity Events! <---

public class InteractPortal : MonoBehaviour
{
    [Header("Teleport Settings")]
    [Tooltip("Drag the target Spawn Point empty GameObject here.")]
    [SerializeField] private Transform teleportDestination;

    // ---> NEW: An event that fires the exact moment the player uses this portal! <---
    [Header("Portal Events")]
    public UnityEvent onTeleport; 

    private void OnTriggerEnter(Collider other)
    {
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