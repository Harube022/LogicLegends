using UnityEngine;
using Photon.Pun;

// Changed to MonoBehaviourPun for easier RPC access
public class GrabbableObject : MonoBehaviourPun 
{
    [Header("Current State")]
    [SerializeField] private Transform holdPoint;
    [SerializeField] private bool isHeld;
    public bool IsHeld => isHeld;

    // ---> NEW LIFECYCLE FLAG <---
    public bool isStoredInInventory = false;

    [Header("Active Puzzle Connections")]
    [SerializeField] private PuzzleSlot currentSlot;
    [SerializeField] private FruitBasket currentBasket;
    [SerializeField] private TorchPedestal currentPedestal;
    [SerializeField] private TutorialORGateBasket currentTutorialBasket;

    [Header("Interaction HUD Settings")]
    [SerializeField] private GameObject interactionHUD; // Drag and drop your Canvas child here
    [SerializeField] private float detectionRange = 2.0f; // Max range to display the HUD

    private Rigidbody rb;
    private Collider col;
    private RigidbodyInterpolation worldInterpolation;
    private readonly System.Collections.Generic.Dictionary<Collider,bool> truthColliders=new System.Collections.Generic.Dictionary<Collider,bool>();

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        if(rb!=null)worldInterpolation=rb.interpolation;
        if(GetComponent<TruthBlock>()!=null)foreach(var c in GetComponentsInChildren<Collider>(true))truthColliders[c]=c.enabled;
        // Ensure HUD starts hidden
        if (interactionHUD != null)
        {
            interactionHUD.SetActive(false);
        }
    }

    private void Update()
    {
        HandleHUDVisibility();
    }

    private void HandleHUDVisibility()
    {
        // Truth Table uses the hand-only bubble; never expose its old pickup HUD through a wall.
        if (TryGetComponent<TruthBlock>(out var truthElement) && truthElement.UsesHandBubble)
        { if(interactionHUD!=null)interactionHUD.SetActive(false);return; }
        if(gameObject.scene.name=="RulesOfInference"&&GetComponent<LogicLegends.Inference.InferenceCrystal>()!=null)
        {if(interactionHUD!=null)interactionHUD.SetActive(false);return;}
        // Don't show the prompt if this block is already held or stored in the inventory
        if (isHeld || isStoredInInventory)
        {
            if (interactionHUD != null && interactionHUD.activeSelf)
            {
                interactionHUD.SetActive(false);
            }
            return;
        }

        // Check if the local player is close enough to interact
        if (Player.LocalInstance != null)
        {
            float distance = Vector3.Distance(transform.position, Player.LocalInstance.transform.position);
            bool isCloseEnough = distance <= detectionRange;

            // Only change activation state if it is different to save CPU performance
            if (interactionHUD != null && interactionHUD.activeSelf != isCloseEnough)
            {
                interactionHUD.SetActive(isCloseEnough);
            }
        }
        else
        {
            // Safety fallback if the player is not fully spawned yet
            if (interactionHUD != null && interactionHUD.activeSelf)
            {
                interactionHUD.SetActive(false);
            }
        }
    }
    
    public void SetSlot(PuzzleSlot slot) { currentSlot = slot; }
    public void SetBasket(FruitBasket basket) { currentBasket = basket; }
    public void SetPedestal(TorchPedestal pedestal) { currentPedestal = pedestal; }
    public void SetTutorialBasket(TutorialORGateBasket tutBasket) { currentTutorialBasket = tutBasket; }

    public void Grab(Transform holdPoint)
    {
        if (TryGetComponent<TruthBlock>(out var element) && !element.CanCollectNow) return;
        if (isStoredInInventory) return; // Ignore grabs if securely stored in inventory UI
        if(element!=null&&InventoryManager.Instance!=null)
        {InventoryManager.Instance.TryPickupBlock(element.value,element);return;}

        // --- FIX: Ensure current item is handled/stored before grabbing new one ---
        if (Player.LocalInstance != null && Player.LocalInstance.GetHeldObject() != null && Player.LocalInstance.GetHeldObject() != this)
        {
            GrabbableObject currentHeld = Player.LocalInstance.GetHeldObject();
            // Store a previous Truth Table block when inventory is available;
            // otherwise release the previous object before holding this one.
            if (currentHeld.TryGetComponent(out TruthBlock heldBlock) &&
                InventoryManager.Instance != null)
            {
                InventoryManager.Instance.TryPickupBlock(heldBlock.value, heldBlock);
            }
            else
            {
                currentHeld.Drop();
            }
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
        CarriedObjectAlignment.Follow(transform, holdPoint);

        ClearPuzzleConnections(); // Run locally for the grabber

        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
        }

        if (col != null) col.enabled = false;

        // --- UPDATED INVENTORY COUPLING ---
        if (TryGetComponent(out TruthBlock truthBlock) && InventoryManager.Instance != null)
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
        GetComponent<CarriedObjectAlignment>()?.StopFollowing();
        var carrier = holdPoint != null ? holdPoint.GetComponentInParent<Player>() : Player.LocalInstance;

        var inferenceCrystal = GetComponent<LogicLegends.Inference.InferenceCrystal>();
        if (inferenceCrystal != null && isHeld && !inferenceCrystal.PrepareAccessibleDrop())
        {
            inferenceCrystal.ReturnToOrigin();
            return;
        }

        isHeld = false;
        holdPoint = null;
        transform.SetParent(null);
        PlayerItemRelease.Protect(transform, carrier, true);

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
        if (TryGetComponent(out TruthBlock truthBlock) && InventoryManager.Instance != null)
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
        bool releasing = (isHeld || isStoredInInventory) && !held && !stored;
        var carrier = holdPoint != null ? holdPoint.GetComponentInParent<Player>() : Player.LocalInstance;
        isHeld = held;
        holdPoint = point;
        isStoredInInventory = stored;
        if (held) CarriedObjectAlignment.Follow(transform, point);
        else GetComponent<CarriedObjectAlignment>()?.StopFollowing();
        if (releasing) PlayerItemRelease.Protect(transform, carrier, false);

        // ---> NEW: Immediately disable the HUD to prevent the 1-frame flash when equipping <---
        if ((held || stored) && interactionHUD != null)
        {
            interactionHUD.SetActive(false);
        }

        if (rb != null)
        {
            if(held||stored)
            {
                if(!rb.isKinematic){rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;}
                rb.interpolation=RigidbodyInterpolation.None;
            }
            else rb.interpolation=worldInterpolation;
            // Physics should be disabled if the object is held in hand or hidden in inventory
            rb.isKinematic = held || stored;
            rb.useGravity = !(held || stored);
        }
        
        if(truthColliders.Count>0)
        {
            foreach(var entry in truthColliders)if(entry.Key!=null)entry.Key.enabled=!(held||stored)&&entry.Value;
        }
        else if (col != null)
        {
            // Disable collisions if held/stored so it doesn't cause physics bugs with the player
            col.enabled = !(held || stored);
        }
    }

    // Placement is atomic: detach and stop following before moving to a board slot.
    public void ReleaseForPlacement()
    {
        var carrier = holdPoint != null ? holdPoint.GetComponentInParent<Player>() : Player.LocalInstance;
        GetComponent<CarriedObjectAlignment>()?.StopFollowing();
        PlayerItemRelease.Protect(transform, carrier, false);
        isHeld = false; isStoredInInventory = false; holdPoint = null;
        transform.SetParent(null, true);
        if (rb != null) { if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; } rb.isKinematic = true; rb.useGravity = false; }
        if (col != null) col.enabled = false;
        foreach(var entry in truthColliders)if(entry.Key!=null)entry.Key.enabled=false;
        if (carrier != null && carrier.GetHeldObject() == this) carrier.SetHeldObjectSilently(null);
    }
}
