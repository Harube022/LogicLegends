using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
[DisallowMultipleComponent]
public sealed class SetPlacementZone : MonoBehaviour
{
    [SerializeField] private SetZone zone;
    [SerializeField] private Transform placementPoint;
    [SerializeField] private int zonePriority;

    private readonly HashSet<SetElement> reservedElements = new HashSet<SetElement>();
    private BoxCollider trigger;

    public SetZone Zone => zone;
    public int Priority => zone == SetZone.INTERSECTION ? int.MaxValue : zonePriority;
    public BoxCollider Trigger => trigger != null ? trigger : (trigger = GetComponent<BoxCollider>());

    public void Configure(SetZone zoneType, Transform point, int priority = 0)
    {
        zone = zoneType;
        placementPoint = point;
        zonePriority = priority;
        Trigger.isTrigger = true;
    }

    private void Awake()
    {
        Trigger.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        NotifyPlayerEntered(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // CharacterController trigger callbacks can be missed if the zone is enabled around the player.
        NotifyPlayerEntered(other);
    }

    private void NotifyPlayerEntered(Collider other)
    {
        Player player = other.GetComponentInParent<Player>();
        if (player == null || (Player.LocalInstance != null && player != Player.LocalInstance)) return;

        SetsStageManager manager = FindFirstObjectByType<SetsStageManager>();
        if (manager != null) manager.PlayerEnteredZone(this, player);
    }

    private void OnTriggerExit(Collider other)
    {
        Player player = other.GetComponentInParent<Player>();
        if (player == null || (Player.LocalInstance != null && player != Player.LocalInstance)) return;

        SetsStageManager manager = FindFirstObjectByType<SetsStageManager>();
        if (manager != null) manager.PlayerExitedZone(this, player);
    }

    public Vector3 ReservePlacementPoint(SetElement element)
    {
        if (element != null) reservedElements.Add(element);

        Transform anchor = placementPoint != null ? placementPoint : transform;
        int slot = Mathf.Max(0, reservedElements.Count - 1);
        const float spacing = 0.75f;
        int column = slot % 3;
        int row = slot / 3;
        Vector3 offset = new Vector3((column - 1) * spacing, 0f, row * spacing);
        return anchor.position + offset;
    }

    public void ReleasePlacementPoint(SetElement element)
    {
        if (element != null) reservedElements.Remove(element);
    }

    private void Reset()
    {
        Trigger.isTrigger = true;
    }
}
