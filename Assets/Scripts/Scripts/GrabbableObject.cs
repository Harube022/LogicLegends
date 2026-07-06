using UnityEngine;
using Photon.Pun;

// Changed to MonoBehaviourPun for easier RPC access
public class GrabbableObject : MonoBehaviourPun 
{
    [Header("Current State")]
    [SerializeField] private Transform holdPoint;
    [SerializeField] private bool isHeld;

    // ---> NEW LIFECYCLE FLAG <---
    public bool isStoredInInventory = false;

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
        if (isStoredInInventory) return; // Ignore grabs if securely stored in inventory UI

        // --- FIX: Ensure current item is handled/stored before grabbing new one ---
        if (Player.LocalInstance != null && Player.LocalInstance.GetHeldObject() != null)
        {
            GrabbableObject currentHeld = Player.LocalInstance.GetHeldObject();
            // Force the currently held item into the inventory first
            InventoryManager.Instance.TryPickupBlock(currentHeld.GetComponent<TruthBlock>().value, currentHeld.GetComponent<TruthBlock>());
            }

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

        // --- UPDATED INVENTORY COUPLING ---
        if (TryGetComponent(out TruthBlock truthBlock))
        {
           int slotIndex = InventoryManager.Instance.TryPickupBlock(truthBlock.value, truthBlock);
            if (slotIndex != -1)
            {
                InventoryManager.Instance.SetSelectedSlotDirectly(slotIndex);
            }
        }
    }

    public void Drop()
    {
        // ---> CRITICAL GHOST THROW FIX <---
        // If the block is stored, ignore any drop requests from your player interaction controller
        if (isStoredInInventory) return;

        isHeld = false;
        holdPoint = null;
        transform.SetParent(null);

        // ---> FIXED: Only tell others we dropped it IF we are online <---
        if (PhotonNetwork.InRoom && photonView != null)
        {
            photonView.RPC("RPC_SetGrabState", RpcTarget.Others, false);
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        if (col != null) col.enabled = true;

        // ---> NEW CLEANUP WORKFLOW <---
        // If it leaves your hand (dropped or placed into a puzzle element), remove it from UI data
        if (TryGetComponent(out TruthBlock truthBlock))
        {
            InventoryManager.Instance.TryRemoveBlock(truthBlock);
            InventoryManager.Instance.ClearSelectionSilently();
        }
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
        
    // Add this helper method inside GrabbableObject.cs
    public void ConfigureInventoryState(bool held, Transform point, bool stored)
    {
        isHeld = held;
        holdPoint = point;
        isStoredInInventory = stored;

        if (rb != null)
        {
            // Physics should be disabled if the object is held in hand or hidden in inventory
            rb.isKinematic = held || stored;
            rb.useGravity = !(held || stored);
        }
        
        if (col != null)
        {
            // Disable collisions if held/stored so it doesn't cause physics bugs with the player
            col.enabled = !(held || stored);
        }
    }
    }