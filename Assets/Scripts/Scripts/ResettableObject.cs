using UnityEngine;

public class ResettableObject : MonoBehaviour
{
    private Vector3 startPos;
    private Quaternion startRot;
    private Rigidbody rb;

    // ---> CHANGED from Start() to Awake() <---
    // This guarantees it perfectly captures the starting position before the player can touch it!
    private void Awake()
    {
        startPos = transform.position;
        startRot = transform.rotation;
        rb = GetComponent<Rigidbody>();
    }

    public void ResetPosition()
    {      
        // 1. Force the object to drop out of the player's hand just in case!
        GrabbableObject grabbable = GetComponent<GrabbableObject>();
        if (grabbable != null)
        {
            grabbable.Drop();
        }

        // ---> NEW: Temporarily turn off physics so Unity doesn't fight the teleport! <---
        if (rb != null) rb.isKinematic = true;

        // 2. Snap back to the start
        transform.position = startPos;
        transform.rotation = startRot;

        // 3. Reset all physics so it doesn't get stuck floating
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = false; // Turn gravity back on!
        }
    }
}