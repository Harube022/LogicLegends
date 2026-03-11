using UnityEngine;

public class ResettableObject : MonoBehaviour
{
    private Vector3 startPos;
    private Quaternion startRot;
    private Rigidbody rb;

    private void Start()
    {
        // Remember our starting spot when the game loads!
        startPos = transform.position;
        startRot = transform.rotation;
        rb = GetComponent<Rigidbody>();
    }

    public void ResetPosition()
    {
        // Snap back to the start
        transform.position = startPos;
        transform.rotation = startRot;

        // Kill any momentum so the boulder doesn't keep rolling after teleporting
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}