using UnityEngine;

public class GrabbableObject : MonoBehaviour
{
    private Transform holdPoint;
    private bool isHeld;

    private PuzzleSlot currentSlot;
    private FruitBasket currentBasket;

    private Rigidbody rb;
    private Collider col;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    public void SetSlot(PuzzleSlot slot)
    {
        currentSlot = slot;
    }

    // ---> NEW: Method to save the basket <---
    public void SetBasket(FruitBasket basket)
    {
        currentBasket = basket;
    }

    public void Grab(Transform holdPoint)
    {
        this.holdPoint = holdPoint;
        isHeld = true;

        if (currentSlot != null)
        {
            if (TryGetComponent(out TowerPiece piece))
            {
                currentSlot.RemovePiece(piece);
            }

            currentSlot = null;
        }
        
        // ---> NEW: If we are picking this up out of a basket, tell the basket it's empty! <---
        if (currentBasket != null)
        {
            currentBasket.RemoveFruit();
            currentBasket = null;
        }

        // ?? Disable physics completely
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            rb.isKinematic = true;
        }

        if (col != null)
        {
            col.enabled = false;
        }
    }

    public void Drop()
    {
        isHeld = false;
        holdPoint = null;

        // ?? Restore physics
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        if (col != null)
        {
            col.enabled = true;
        }
    }

    private void LateUpdate()
    {
        if (isHeld && holdPoint != null)
        {
            transform.position = holdPoint.position;
            transform.rotation = holdPoint.rotation;
        }
    }
}