using UnityEngine;
using System.Collections.Generic;

public partial class TruthBlock : MonoBehaviour
{
    static readonly List<TruthBlock> activeBlocks = new List<TruthBlock>();
    public static IReadOnlyList<TruthBlock> ActiveBlocks => activeBlocks;
    HandThoughtBubble pickupBubble;
    public bool BelongsTo(TruthTableDoorInteraction owner)=>owner!=null&&spawnOwner!=null&&
        spawnOwner.gameObject.scene==owner.gameObject.scene&&spawnOwner.transform.root==owner.transform.root;
    public bool UsesHandBubble => TruthTableDoorInteraction.Active != null && TruthTableDoorInteraction.Active.UsesHandBubbles && BelongsTo(TruthTableDoorInteraction.Active);
    public bool CanCollectNow => !IsRecovering && (!UsesHandBubble || TruthTableDoorInteraction.Active.IsBlockAvailable(this));
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetBlocks() { activeBlocks.Clear(); }
    void OnEnable() { if(!activeBlocks.Contains(this))activeBlocks.Add(this); }
    void OnDisable() { CancelWaterRecovery(); activeBlocks.Remove(this);if(pickupBubble!=null)pickupBubble.gameObject.SetActive(false); }
    void LateUpdate()
    {
        UpdateWaterRecovery(Time.deltaTime);
        if(!UsesHandBubble){if(pickupBubble!=null)pickupBubble.gameObject.SetActive(false);return;}
        bool available=CanCollectNow;
        if(pickupBubble==null&&available)
        {
            pickupBubble=HandThoughtBubble.Create(transform,"Element Interaction Bubble",yellowMarker:true);
        }
        if(pickupBubble==null)return;
        var mesh=GetComponent<MeshRenderer>();
        var at=mesh!=null?new Vector3(mesh.bounds.center.x,mesh.bounds.max.y+.7f,mesh.bounds.center.z):transform.position+Vector3.up*1.2f;
        bool near=available&&TruthTableDoorInteraction.Active.NearestAvailableBlock()==this;
        pickupBubble.Show(available,near,at);
    }
    public float InteractionDistanceSquared(Player player)
    {
        var center=player.transform.position+Vector3.up*1.5f;
        var collider=GetComponent<Collider>();
        return ((collider!=null&&collider.enabled?collider.ClosestPoint(center):transform.position)-center).sqrMagnitude;
    }
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
        // A genuine new pickup/drop must not inherit the previous recovery cooldown.
        nextRecovery = 0;
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

    public bool ReturnToInventory()
    {
        if (InventoryManager.Instance == null ||
            !InventoryManager.Instance.TryStoreReturnedBlock(this)) return false;
        isPlacedInColumn = false;
        isAtSpawnPoint = false;
        if (spawnOwner != null) spawnOwner.ReleaseSpawn(this);
        return true;
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
        CancelWaterRecovery();
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        if (rb != null)
        {
            if(!rb.isKinematic){rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;}
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        ClearInventoryAndHand();
        transform.SetParent(null, true);
        transform.SetPositionAndRotation(position, rotation);
        gameObject.SetActive(true);
        if (TryGetComponent(out MeshRenderer renderer)) renderer.enabled = true;
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
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
        if (!CanCollectNow || InventoryManager.Instance == null) return;
        InventoryManager.Instance.TryPickupBlock(value, this);
    }
}
