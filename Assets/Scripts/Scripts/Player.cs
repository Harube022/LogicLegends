using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun; // 1. Added Photon namespace

// 2. Changed from MonoBehaviour to MonoBehaviourPun
public class Player : MonoBehaviourPun 
{
    [SerializeField] private Transform holdPoint;
    private GrabbableObject heldObject;

    [SerializeField] public float moveSpeed = 8f;
    [SerializeField] private GameInput gameInput;
    [SerializeField] private LayerMask countersLayerMask;
    [SerializeField] private LayerMask Modules;

    private bool isWalking;
    private Vector3 lastInteractions;

    [SerializeField] private float jumpForce = 15f;
    [SerializeField] private float gravity = -30f;
    [SerializeField] private float jumpBufferTime = 0.15f;

    private float verticalVelocity;
    private bool isGrounded;
    private bool isJumping;
    // private float groundSnapDistance = 0.3f;
    private float jumpBufferTimer;
    private bool tutorialMovementDone = false;

    private void Awake()
    {
        if (gameInput == null)
        {
            gameInput = FindFirstObjectByType<GameInput>();
        }
    }

    private void Start()
    {
        // ONLY subscribe to input events if this character belongs to us
        if (IsLocalPlayer())
        {
            gameInput.OnInteractAction += GameInput_OnInteractAction;
            gameInput.OnJumpAction += GameInput_OnJumpAction;

            // ---> NEW CAMERA LINK LOGIC <---
            // Find the camera in the scene and tell it to follow THIS specific player
            ThirdPersonCameraController cam = FindFirstObjectByType<ThirdPersonCameraController>();
            if (cam != null)
            {
                cam.SetPlayerTarget(this.transform);
            }
        }
    }
    private void GameInput_OnJumpAction(object sender, System.EventArgs e)
    {
        Debug.Log("JUMP EVENT RECEIVED, grounded = " + isGrounded);
        jumpBufferTimer = jumpBufferTime;
    }

    private void GameInput_OnInteractAction(object sender, System.EventArgs e)
    {
        Debug.Log("INTERACT PRESSED");

        float interactionDistance = 2f;
        Vector3 rayStart = transform.position + Vector3.up * 0.5f; 
        float castRadius = 0.5f; 

        // ===== CHECK PORTAL FIRST =====
        if (Physics.SphereCast(rayStart, castRadius, transform.forward, out RaycastHit portalHit, interactionDistance))
        {
            if (portalHit.transform.TryGetComponent(out Portal portal))
            {
                portal.TryEnterPortal();
                return;
            }
        }

        // ===== CHECK LEVER =====
        if (Physics.SphereCast(rayStart, castRadius, transform.forward, out RaycastHit leverHit, interactionDistance))
        {
            if (leverHit.transform.TryGetComponent(out LeverController lever))
            {
                lever.ToggleLever(); 
                return; 
            }
        }

        // ---> NEW: CHECK WATERING CAN MANAGER <---
        if (Physics.SphereCast(rayStart, castRadius, transform.forward, out RaycastHit canHit, interactionDistance))
        {
            if (canHit.transform.TryGetComponent(out HarvestMatrixManager manager))
            {
                manager.WaterGarden(); 
                return; 
            }
        }

        // ---> FIXED: CHECK PLACED TORCH WITH EMPTY HANDS <---
        if (Physics.SphereCast(rayStart, castRadius, transform.forward, out RaycastHit emptyHandHit, interactionDistance))
        {
            if (emptyHandHit.transform.TryGetComponent(out TorchPedestal fullPed) && fullPed.CurrentTorch != null)
            {
                fullPed.OpenTorchUI(); // Open UI to choose Lit/Unlit!
                return; 
            }
        }

        // ===== IF HOLDING OBJECT =====
        if (heldObject != null)
        {
            if (Physics.SphereCast(rayStart, castRadius, transform.forward, out RaycastHit hit, interactionDistance))
            {
                // 1. Try to put it in the Fruit Basket
                if (hit.transform.TryGetComponent(out FruitBasket basket) && !basket.HasFruit())
                {
                    GameObject fruitObj = heldObject.gameObject;
                    heldObject.Drop(); 
                    basket.PlaceFruit(fruitObj); 
                    heldObject = null;
                    return;
                }

                // ---> NEW: Try to put it in the Tutorial OR Gate Basket <---
                if (hit.transform.TryGetComponent(out TutorialORGateBasket tutorialBasket))
                {
                    GameObject fruitObj = heldObject.gameObject;
                    heldObject.Drop(); 
                    tutorialBasket.PlaceFruitInteractive(fruitObj);
                    heldObject = null;
                    return;
                }

                // 2. Try to put it in a Puzzle Slot
                if (hit.transform.TryGetComponent(out PuzzleSlot slot))
                {
                    TowerPiece piece = heldObject.GetComponent<TowerPiece>();
                    if (piece != null && slot.TryPlace(piece))
                    {
                        heldObject.Drop();
                        heldObject = null;
                        return;
                    }
                }

                // 3. Try to put it in a Torch Pedestal
                if (hit.transform.TryGetComponent(out TorchPedestal ped) && ped.CurrentTorch == null) 
                {
                    GameObject torchObj = heldObject.gameObject;
                    heldObject.Drop(); 
                    ped.PlaceTorchNetworked(torchObj); 
                    heldObject = null;
                    return;
                }

                // ---> FIXED: CHECK PLACED TORCH WHILE HOLDING SOMETHING <---
                if (Physics.SphereCast(rayStart, castRadius, transform.forward, out RaycastHit holdingHit, interactionDistance))
                {
                    if (holdingHit.transform.TryGetComponent(out TorchPedestal holdingPed) && holdingPed.CurrentTorch != null) 
                    {
                        holdingPed.OpenTorchUI(); 
                        return; 
                    }
                } 

                // ---> NEW: Try to put it in a Soil Mound <---
                if (hit.transform.TryGetComponent(out SoilMound mound) && !mound.HasSeed()) 
                {
                    GameObject seedObj = heldObject.gameObject;
                    heldObject.Drop(); 
                    mound.PlaceSeedNetworked(seedObj); 
                    heldObject = null;
                    return;
                }
            }

            // Otherwise, just drop it on the ground
            heldObject.Drop();
            heldObject = null;
            return;
        }

        // ===== IF NOT HOLDING, TRY GRAB =====
        Vector3 grabCenter = transform.position + transform.forward * 1f + Vector3.up * 0.5f;
        Collider[] hitColliders = Physics.OverlapSphere(grabCenter, 1.2f); 

        foreach (Collider col in hitColliders)
        {
            if (col.TryGetComponent(out GrabbableObject grabbable))
            {
                heldObject = grabbable;
                grabbable.Grab(holdPoint);
                return; 
            }
        }
    }

    private void Update()
    {
        // 4. STOP the script here if this character is NOT ours
        if (!IsLocalPlayer()) 
        {
            return; 
        }

        HandleMovement();
        HandleInteractions();
        HandleGravity();
    }

    public bool IsWalking()
    {
        return isWalking;
    }

    public bool IsJumping()
    {
        return isJumping;
    }

    private void HandleInteractions()
    {
        Vector2 inputVector = gameInput.GetMovementVectorNormalized();
        Vector3 moveDir = new Vector3(inputVector.x, 0, inputVector.y);

        if (moveDir != Vector3.zero)
        {
            lastInteractions = moveDir;
        }

        float interactionDistance = 2f;

        if (Physics.Raycast(transform.position, lastInteractions, out RaycastHit raycastHit, interactionDistance, Modules))
        {
            if (raycastHit.transform.TryGetComponent(out Modules module))
            {
            }
        }
    }

    private void HandleMovement()
    {
        Vector2 inputVector = gameInput.GetMovementVectorNormalized();
        Vector3 camForward = Camera.main.transform.forward;
        Vector3 camRight = Camera.main.transform.right;

        // remove vertical tilt
        camForward.y = 0f;
        camRight.y = 0f;

        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDir = camForward * inputVector.y + camRight * inputVector.x;

        if (moveDir == Vector3.zero)
        {
            isWalking = false;
            return;
        }

        float moveDistance = moveSpeed * Time.deltaTime;

        float playerRadius = 0.7f;
        float playerHeight = 2f;

        Vector3 capsuleBottom = transform.position;
        Vector3 capsuleTop = transform.position + Vector3.up * playerHeight;

        // ---> FIX 1: Added QueryTriggerInteraction.Ignore so the player doesn't bump into Triggers
        if (!Physics.CapsuleCast(capsuleBottom, capsuleTop, playerRadius, moveDir, out RaycastHit hit, moveDistance, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            transform.position += moveDir * moveDistance;
        }
        else
        {
            // ---> NEW PUSH LOGIC START <---
            Rigidbody hitRb = hit.collider.attachedRigidbody;

            if (hitRb != null && !hitRb.isKinematic)
            {
                float pushForce = 500f; 
                hitRb.AddForce(moveDir * pushForce, ForceMode.Force);
            }
            // ---> NEW PUSH LOGIC END <---

            // // ===== TRY STEP UP =====
            float stepHeight = 0.6f; 

            Vector3 stepUp = Vector3.up * stepHeight;

            Vector3 newBottom = capsuleBottom + stepUp;
            Vector3 newTop = capsuleTop + stepUp;

            // ---> FIX 2: Added QueryTriggerInteraction.Ignore
            if (!Physics.CapsuleCast(newBottom, newTop, playerRadius, moveDir, moveDistance, Physics.AllLayers, QueryTriggerInteraction.Ignore))
            {
                transform.position += stepUp;                 
                transform.position += moveDir * moveDistance; 
            }
            else
            {
                // ===== NEW: WALL SLIDING =====
                Vector3 slideDir = Vector3.ProjectOnPlane(moveDir, hit.normal).normalized;

                // ---> FIX 3: Added QueryTriggerInteraction.Ignore
                if (slideDir != Vector3.zero && !Physics.CapsuleCast(capsuleBottom, capsuleTop, playerRadius, slideDir, moveDistance, Physics.AllLayers, QueryTriggerInteraction.Ignore))
                {
                    transform.position += slideDir * moveDistance;
                }
            }
            
        }

        // ===== FACE DIRECTION =====
        float rotateSpeed = 10f;
        transform.forward = Vector3.Slerp(transform.forward, moveDir, Time.deltaTime * rotateSpeed);

        isWalking = true;

        // ---> TUTORIAL TRIGGER START <---
        if (isWalking && !tutorialMovementDone)
        {
            TutorialManager tutorial = FindFirstObjectByType<TutorialManager>();
            if (tutorial != null)
            {
                tutorial.CompleteMovementStep();
                tutorialMovementDone = true; 
            }
        }
    }
    
    private void HandleGravity()
    {
        jumpBufferTimer -= Time.deltaTime;

        float rayStartOffset = 1.0f;
        float rayDistance = 3.0f;

        // ---> FIX 4: Added QueryTriggerInteraction.Ignore so gravity doesn't detect triggers as the floor!
        bool hitGround = Physics.Raycast(
            transform.position + Vector3.up * rayStartOffset,
            Vector3.down,
            out RaycastHit hit,
            rayDistance,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore
        );

        float maxSlopeAngle = 45f;
        
        bool validGround = hitGround; 
        if (hitGround)
        {
            float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
            if (slopeAngle > maxSlopeAngle) validGround = false;
        }

        if (validGround && jumpBufferTimer > 0f && !isJumping)
        {
            verticalVelocity = jumpForce;
            isJumping = true;
            isGrounded = false;
            jumpBufferTimer = 0f;
        }

        if (!validGround)
        {
            isGrounded = false;
            
            float currentGravity = (verticalVelocity < 0f) ? gravity * 1.5f : gravity;
            verticalVelocity += currentGravity * Time.deltaTime;

            if (verticalVelocity < -25f) verticalVelocity = -25f;
        }
        else if (!isJumping) 
        {
            isGrounded = true;
            verticalVelocity = -5f; 
        }

        float moveY = verticalVelocity * Time.deltaTime;
        float playerRadius = 0.7f;
        float playerHeight = 2f;

        Vector3 capsuleBottom = transform.position;
        Vector3 capsuleTop = transform.position + Vector3.up * playerHeight;

        Vector3 checkDirection = (moveY > 0) ? Vector3.up : Vector3.down;

        // ---> FIX 5: Added QueryTriggerInteraction.Ignore
        if (!Physics.CapsuleCast(capsuleBottom, capsuleTop, playerRadius, checkDirection, out RaycastHit yHit, Mathf.Abs(moveY), Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            transform.position += Vector3.up * moveY;
        }
        else
        {
            verticalVelocity = 0f;
            
            if (moveY < 0) 
            {
                isJumping = false;
            }
        }
    }

    // This helper checks if we are offline OR if the network says the player is ours
    private bool IsLocalPlayer()
    {
        // If there is no PhotonView component, or we are not connected to the internet, assume this is the Solo mode player
        if (photonView == null || !PhotonNetwork.InRoom)
        {
            return true;
        }
        
        // Otherwise, rely on Photon to tell us if we own it in multiplayer
        return photonView.IsMine;
    }
    private void OnDestroy()
    {
        // 5. Only unsubscribe if this character belonged to us
        if (gameInput != null && IsLocalPlayer())
        {
            gameInput.OnInteractAction -= GameInput_OnInteractAction;
            gameInput.OnJumpAction -= GameInput_OnJumpAction;
        }
    }
}