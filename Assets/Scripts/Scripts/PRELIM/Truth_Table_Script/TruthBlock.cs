using UnityEngine;

public class TruthBlock : MonoBehaviour
{
    public bool value; // T = true, F = false

    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Rigidbody rb;
    private TruthBlockSpawner spawnOwner;
    private Transform currentSpawnPoint;
    private Transform lastCollectedSpawnPoint;
    private bool isAtSpawnPoint;
    private bool isPlacedInColumn;

    public Transform CurrentSpawnPoint => currentSpawnPoint;
    public Transform LastCollectedSpawnPoint => lastCollectedSpawnPoint;
    public bool IsPlacedInColumn => isPlacedInColumn;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        originalPosition = transform.position;
        originalRotation = transform.rotation;
    }

    public void InitializeSpawn(TruthBlockSpawner owner, Transform point)
    {
        spawnOwner = owner;
        currentSpawnPoint = point;
        isAtSpawnPoint = true;
        isPlacedInColumn = false;
    }

    public void MarkCollectedFromSpawn()
    {
        if (!isAtSpawnPoint) return;
        lastCollectedSpawnPoint = currentSpawnPoint;
        isAtSpawnPoint = false;
        if (spawnOwner != null) spawnOwner.ReleaseSpawn(this);
    }

    public void MarkPlacedInColumn()
    {
        isPlacedInColumn = true;
        isAtSpawnPoint = false;
        if (spawnOwner != null) spawnOwner.ReleaseSpawn(this);
    }

    public void ReturnToOrigin(bool smooth = true)
    {
        // Preserve the existing API, but remove the timed return from every path.
        if (spawnOwner != null)
        {
            spawnOwner.RespawnBlock(this);
            return;
        }
        RestoreInWorld(originalPosition, originalRotation);
    }

    public void RespawnAt(Transform point)
    {
        currentSpawnPoint = point;
        isAtSpawnPoint = true;
        isPlacedInColumn = false;
        RestoreInWorld(point.position, point.rotation);
    }

    public void RestoreWithoutSpawn()
    {
        isAtSpawnPoint = false;
        isPlacedInColumn = false;
        RestoreInWorld(transform.position, transform.rotation);
    }

    public void PrepareForDespawn()
    {
        if (spawnOwner != null) spawnOwner.ReleaseSpawn(this);
        ClearInventoryAndHand();
    }

    private void RestoreInWorld(Vector3 position, Quaternion rotation)
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        ClearInventoryAndHand();
        transform.SetParent(null, true);
        transform.SetPositionAndRotation(position, rotation);
        gameObject.SetActive(true);
        if (TryGetComponent(out MeshRenderer renderer)) renderer.enabled = true;
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = false;
            rb.useGravity = true;
        }
        if (col != null) col.enabled = true;
        Physics.SyncTransforms();
    }

    private void ClearInventoryAndHand()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.TryRemoveBlock(this);
        if (TryGetComponent(out GrabbableObject grabbable))
        {
            if (Player.LocalInstance != null && Player.LocalInstance.GetHeldObject() == grabbable)
                Player.LocalInstance.SetHeldObjectSilently(null);
            grabbable.ConfigureInventoryState(false, null, false);
        }
    }

    // Call this via a mobile interaction button or OnTriggerEnter
    public void CollectBlock()
    {
        // Pass 'this' so the inventory slot can remember this specific block object
        int slotIndex = InventoryManager.Instance.TryPickupBlock(value, this);

        // -1 means the pickup failed (inventory was full)
        if (slotIndex != -1)
        {
            Debug.Log("Block data logged into inventory! Model remains visible in world until slot is tapped.");
            
            // Auto-select the slot the item was placed into
            InventoryManager.Instance.SetSelectedSlotDirectly(slotIndex);
        }
    }
}
