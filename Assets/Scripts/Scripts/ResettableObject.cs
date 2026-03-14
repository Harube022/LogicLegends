using UnityEngine;

public class ResettableObject : MonoBehaviour
{
    private Vector3 startPos;
    private Quaternion startRot;
    private Rigidbody rb;

    private void Awake()
    {
        // TRICK 1: Add a tiny bit of height (0.5f) so the fruit spawns slightly in the air 
        // and drops cleanly instead of getting wedged in the floor's collider!
        startPos = transform.position + new Vector3(0, 0.5f, 0);
        startRot = transform.rotation;
        rb = GetComponent<Rigidbody>();
    }

    public void ResetPosition()
    {      
        // 1. Force the object to drop out of the player's hand and forget the basket
        GrabbableObject grabbable = GetComponent<GrabbableObject>();
        if (grabbable != null)
        {
            grabbable.enabled = true;
            grabbable.Drop();
            grabbable.SetBasket(null); // Completely erase its memory of the basket
        }

        // ---> NEW: If this is a Torch, reset its flame! <---
        TorchItem torch = GetComponent<TorchItem>();
        if (torch != null)
        {
            torch.ResetFlame();
        }
        
        // 2. Shut off physics temporarily
        if (rb != null) 
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 3. Teleport the fruit
        transform.position = startPos;
        transform.rotation = startRot;

        // TRICK 2: Force Unity's physics engine to instantly update its spatial maps. 
        // This stops it from accidentally snapping the fruit back to the basket!
        Physics.SyncTransforms(); 

        // 4. Turn gravity back on so it falls nicely to the grass
        if (rb != null)
        {
            rb.isKinematic = false; 
            rb.useGravity = true;
        }
    }
}