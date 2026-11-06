using UnityEngine;

/// <summary>Allows only the fitted entrance slopes; normal terrain keeps the player's original slope limit.</summary>
[DefaultExecutionOrder(-100), DisallowMultipleComponent]
public sealed class TruthTableStairAccess : MonoBehaviour
{
    public MeshCollider[] ramps;
    CharacterController controller;
    float originalSlope, originalStep;
    void Update()
    {
        var next = Player.LocalInstance != null ? Player.LocalInstance.GetComponent<CharacterController>() : null;
        if (next != controller)
        { Restore(); controller = next; if (controller != null) { originalSlope = controller.slopeLimit; originalStep = controller.stepOffset; } }
        if (controller == null) return;
        bool onStairs = false;
        foreach (var ramp in ramps)
        {
            if (ramp == null || !ramp.enabled) continue;
            Vector3 point = ramp.transform.InverseTransformPoint(controller.transform.position);
            var bounds = ramp.sharedMesh.bounds;
            float feet = controller.bounds.min.y;
            // The capsule contacts a tread before its center reaches the ramp.
            float contactMargin = controller.radius + controller.skinWidth + .12f;
            if (Mathf.Abs(point.x) <= bounds.extents.x + contactMargin && point.z >= bounds.min.z - contactMargin && point.z <= bounds.max.z + contactMargin &&
                feet >= ramp.bounds.min.y - 1.5f && feet <= ramp.bounds.max.y + .5f) { onStairs = true; break; }
        }
        controller.slopeLimit = onStairs ? Mathf.Max(originalSlope, 65) : originalSlope;
        // Uneven risers can contact the capsule before its ramp contact resolves.
        // Limit the extra step allowance to the real entrance footprint.
        controller.stepOffset = onStairs ? Mathf.Max(originalStep, Mathf.Min(2, controller.height * .4f)) : originalStep;
    }
    void Restore() { if (controller != null) { controller.slopeLimit = originalSlope; controller.stepOffset = originalStep; } }
    void OnDisable() { Restore(); controller = null; }
}
