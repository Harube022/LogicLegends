using UnityEngine;
using Photon.Pun;
public class GrabbableObject : MonoBehaviour
{
    [Header("Current State")]
    [SerializeField] private Transform holdPoint;
    [SerializeField] private bool isHeld;

    [Header("Active Puzzle Connections")]
    [SerializeField] private PuzzleSlot currentSlot;
    [SerializeField] private FruitBasket currentBasket;
    [SerializeField] private TorchPedestal currentPedestal;

    private Rigidbody rb;
    private Collider col;
    private PhotonView view; // 2. Network view reference

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        view = GetComponent<PhotonView>();
    }
    
    public void SetSlot(PuzzleSlot slot)
    {
        currentSlot = slot;
    }

    public void SetBasket(FruitBasket basket)
    {
        currentBasket = basket;
    }

    public void SetPedestal(TorchPedestal pedestal)
    {
        currentPedestal = pedestal;
    }

    public void Grab(Transform holdPoint)
    {
        // 3. Take ownership of this item so our screen dictates its movement
        if (view != null)
        {
            view.RequestOwnership(); 
            // Tell other players to turn off the physics on THEIR end so it doesn't fight us
            view.RPC("RPC_SetGrabState", RpcTarget.Others, true);
        }

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
        
        if (currentBasket != null)
        {
            currentBasket.RemoveFruit();
            currentBasket = null;
        }

        if (currentPedestal != null)
        {
            currentPedestal.RemoveTorch();
            currentPedestal = null;
        }

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
        // 4. Tell other players to turn physics back on
        if (view != null)
        {
            view.RPC("RPC_SetGrabState", RpcTarget.Others, false);
        }

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

    // 5. This RPC only runs on the remote players' screens
    [PunRPC]
    public void RPC_SetGrabState(bool isGrabbed)
    {
        if (rb != null) rb.isKinematic = isGrabbed;
        if (col != null) col.enabled = !isGrabbed;
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