using UnityEngine;
using Photon.Pun;

// Changed to MonoBehaviourPun for easier RPC access
public class GrabbableObject : MonoBehaviourPun 
{
    [Header("Current State")]
    [SerializeField] private Transform holdPoint;
    [SerializeField] private bool isHeld;

    [Header("Active Puzzle Connections")]
    [SerializeField] private PuzzleSlot currentSlot;
    [SerializeField] private FruitBasket currentBasket;
    [SerializeField] private TorchPedestal currentPedestal;
    [SerializeField] private TutorialORGateBasket currentTutorialBasket;

    private Rigidbody rb;
    private Collider col;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }
    
    public void SetSlot(PuzzleSlot slot) { currentSlot = slot; }
    public void SetBasket(FruitBasket basket) { currentBasket = basket; }
    public void SetPedestal(TorchPedestal pedestal) { currentPedestal = pedestal; }
    public void SetTutorialBasket(TutorialORGateBasket tutBasket) { currentTutorialBasket = tutBasket; }

    public void Grab(Transform holdPoint)
    {
        // ---> FIXED: Only request ownership and notify others IF we are online <---
        if (PhotonNetwork.InRoom && photonView != null)
        {
            photonView.RequestOwnership(); 
            // Tell other players to turn off physics AND clear puzzle references
            photonView.RPC("RPC_SetGrabState", RpcTarget.Others, true);
        }

        this.holdPoint = holdPoint;
        isHeld = true;

        ClearPuzzleConnections(); // Run locally for the grabber

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        if (col != null) col.enabled = false;
    }

    public void Drop()
    {
        // ---> FIXED: Only tell others we dropped it IF we are online <---
        if (PhotonNetwork.InRoom && photonView != null)
        {
            photonView.RPC("RPC_SetGrabState", RpcTarget.Others, false);
        }

        isHeld = false;
        holdPoint = null;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        if (col != null) col.enabled = true;
    }

    // Extracted the cleanup logic so it can be called by the RPC too
    private void ClearPuzzleConnections()
    {
        if (currentSlot != null)
        {
            if (TryGetComponent(out TowerPiece piece)) currentSlot.RemovePiece(piece);
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

        if (currentTutorialBasket != null)
        {
            currentTutorialBasket.RemoveFruitExplicit(this.gameObject);
            currentTutorialBasket = null;
        }
    }

    [PunRPC]
    public void RPC_SetGrabState(bool isGrabbed)
    {
        if (isGrabbed)
        {
            // If someone else grabbed this, clear our local puzzle references!
            ClearPuzzleConnections(); 
        }

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