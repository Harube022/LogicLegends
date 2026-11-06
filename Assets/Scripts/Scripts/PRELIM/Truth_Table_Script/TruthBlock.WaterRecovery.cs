using System;
using UnityEngine;

public partial class TruthBlock
{
    public bool IsRecovering { get; private set; }
    Renderer recoveryWater;
    Collider recoveryCollider;
    GrabbableObject recoveryItem;
    Vector3 recoveryStart, recoveryEnd, recoveryExtents, recoveryOffset;
    float recoveryTime, nextRecovery;
    int waterRecoveryCount;

    void CancelWaterRecovery() { IsRecovering = false; recoveryTime = 0; }
    void UpdateWaterRecovery(float delta)
    {
        if (gameObject.scene.name != "TruthTable" || isPlacedInColumn || rb == null) return;
        if (recoveryItem == null) recoveryItem = GetComponent<GrabbableObject>();
        if (recoveryItem == null || recoveryItem.IsHeld || recoveryItem.isStoredInInventory) return;
        if (IsRecovering)
        {
            recoveryTime += delta; float t = Mathf.Clamp01(recoveryTime / .85f);
            transform.position = Vector3.Lerp(recoveryStart, recoveryEnd, Mathf.SmoothStep(0, 1, t)) + Vector3.up * (2.5f * 4 * t * (1 - t));
            if (t < 1) return;
            IsRecovering = false; nextRecovery = Time.time + 2;
            recoveryCollider.enabled = true; rb.isKinematic = true; rb.useGravity = false;
            Physics.SyncTransforms();
            if (!RecoveryLandingClear(recoveryEnd)) ReturnToOrigin(false);
            return;
        }
        if (rb.isKinematic || Time.time < nextRecovery) return;
        if (recoveryWater == null) recoveryWater = GameObject.Find("Ocean50x50W750H750")?.GetComponent<Renderer>();
        if (recoveryWater == null) return;
        if (recoveryCollider == null) recoveryCollider = GetComponent<Collider>();
        if (recoveryCollider == null || !recoveryCollider.enabled) return;
        var water = recoveryWater.bounds; var at = transform.position;
        if (at.x < water.min.x || at.x > water.max.x || at.z < water.min.z || at.z > water.max.z || recoveryCollider.bounds.min.y > water.max.y + .05f) return;
        waterRecoveryCount++;
        rb.linearVelocity = rb.angularVelocity = Vector3.zero;
        transform.rotation = originalRotation;
        Physics.SyncTransforms();
        recoveryExtents = recoveryCollider.bounds.extents; recoveryOffset = recoveryCollider.bounds.center - transform.position;
        recoveryStart = transform.position;
        // Begin at the surface immediately, so the original element never disappears under water.
        recoveryStart.y = Mathf.Max(recoveryStart.y, water.max.y + recoveryExtents.y + .18f - recoveryOffset.y);
        if (!FindRecoveryLand(out recoveryEnd)) { ReturnToOrigin(false); nextRecovery = Time.time + 2; return; }
        rb.isKinematic = true; rb.useGravity = false;
        recoveryCollider.enabled = false; transform.position = recoveryStart;
        IsRecovering = true; recoveryTime = 0;
    }
    bool FindRecoveryLand(out Vector3 landing)
    {
        Vector3 outward = transform.position - recoveryWater.bounds.center; outward.y = 0;
        if (outward.sqrMagnitude < .01f) outward = Vector3.forward;
        outward.Normalize();
        for (float radius = 3; radius <= 27; radius += 3)
            for (int angle = 0; angle < 24; angle++)
            {
                var point = transform.position + Quaternion.Euler(0, angle * 15, 0) * outward * radius;
                RaycastHit hit; if (!RecoveryDryGround(point, out hit)) continue;
                landing = hit.point + Vector3.up * (recoveryExtents.y + .16f) - recoveryOffset;
                if (RecoveryLandingClear(landing)) return true;
            }
        landing = originalPosition; return false;
    }
    bool RecoveryDryGround(Vector3 point, out RaycastHit result)
    {
        var hits = Physics.RaycastAll(new Vector3(point.x, Mathf.Max(recoveryWater.bounds.max.y + 35, originalPosition.y + 15), point.z), Vector3.down, 100, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
            if ((hit.collider is TerrainCollider || hit.collider.name.StartsWith("FLOOR", StringComparison.OrdinalIgnoreCase)) && hit.normal.y >= .8f && hit.point.y > recoveryWater.bounds.max.y + .35f)
            { result = hit; return true; }
        result = default; return false;
    }
    bool RecoveryLandingClear(Vector3 position)
    {
        var center = position + recoveryOffset; RaycastHit ground;
        if (!RecoveryDryGround(center, out ground) || Mathf.Abs(center.y - recoveryExtents.y - .16f - ground.point.y) > .4f) return false;
        foreach (var collider in Physics.OverlapBox(center, recoveryExtents * .9f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            if (collider != recoveryCollider && !collider.transform.IsChildOf(transform)) return false;
        foreach (var offset in new[] { Vector3.left, Vector3.right, Vector3.forward, Vector3.back })
        { RaycastHit edge; if (!RecoveryDryGround(center + offset * Mathf.Max(recoveryExtents.x, recoveryExtents.z) * .7f, out edge) || Mathf.Abs(edge.point.y - ground.point.y) > .45f) return false; }
        return true;
    }
}
