using System.Collections.Generic;
using UnityEngine;

/// <summary>Temporarily separates a released item from its carrier, never from the world.</summary>
[DisallowMultipleComponent]
public sealed class PlayerItemRelease : MonoBehaviour
{
    struct Pair { public Collider item, player; }
    readonly List<Pair> ignored = new List<Pair>();

    public static void Protect(Transform item, Player carrier, bool moveOutsideCapsule)
    {
        if (item == null || carrier == null) return;
        var guard = item.GetComponent<PlayerItemRelease>();
        if (guard == null) guard = item.gameObject.AddComponent<PlayerItemRelease>();
        guard.Restore();
        var controller = carrier.GetComponent<CharacterController>();
        if (moveOutsideCapsule && controller != null)
        {
            // Use the original mesh bounds, including off-centre pivots and large element blocks.
            bool found = false; Bounds bounds = new Bounds(item.position, Vector3.zero);
            foreach (var r in item.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer || r.GetComponentInParent<TMPro.TMP_Text>() != null) continue;
                if (!found) { bounds = r.bounds; found = true; } else bounds.Encapsulate(r.bounds);
            }
            Vector3 center = controller.transform.TransformPoint(controller.center);
            Vector3 direction = bounds.center - center; direction.y = 0;
            if (direction.sqrMagnitude < .001f) direction = carrier.transform.forward;
            direction.Normalize();
            float radius = controller.radius * Mathf.Max(Mathf.Abs(controller.transform.lossyScale.x), Mathf.Abs(controller.transform.lossyScale.z));
            float itemRadius = Mathf.Abs(direction.x) * bounds.extents.x + Mathf.Abs(direction.z) * bounds.extents.z;
            float distance = Vector3.Dot(bounds.center - center, direction);
            float clearance = radius + itemRadius + controller.skinWidth + .15f;
            if (distance < clearance) item.position += direction * (clearance - distance);
        }
        foreach (var own in item.GetComponentsInChildren<Collider>(true))
            foreach (var body in carrier.GetComponentsInChildren<Collider>(true))
            {
                if (own == body || own.isTrigger || body.isTrigger || body.transform.IsChildOf(item)) continue;
                if (Physics.GetIgnoreCollision(own, body)) continue; // Respect an existing restriction.
                Physics.IgnoreCollision(own, body, true);
                guard.ignored.Add(new Pair { item = own, player = body });
            }
    }
    void FixedUpdate()
    {
        for (int i = ignored.Count - 1; i >= 0; i--)
        {
            var p = ignored[i];
            if (p.item == null || p.player == null) { ignored.RemoveAt(i); continue; }
            if (!p.item.enabled || !p.player.enabled) continue;
            var bounds = p.item.bounds; bounds.Expand(.12f);
            if (bounds.Intersects(p.player.bounds)) continue;
            Physics.IgnoreCollision(p.item, p.player, false); ignored.RemoveAt(i);
        }
    }
    void Restore()
    {
        foreach (var p in ignored) if (p.item != null && p.player != null) Physics.IgnoreCollision(p.item, p.player, false);
        ignored.Clear();
    }
    void OnDisable() { Restore(); }
}
