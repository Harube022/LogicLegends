using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun; 

// This forces Unity to automatically add a CharacterController if one is missing!
[RequireComponent(typeof(CharacterController))]
public class Player : MonoBehaviourPun 
{
    // ---> ADD THIS SINGLETON INSTANCE TRACKING <---
    public static Player LocalInstance { get; private set; }
    [SerializeField] private Transform holdPoint;
    public Transform HoldPoint => holdPoint;
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
    private bool isJumping;
    private float jumpBufferTimer;
    private bool tutorialMovementDone = false;

    // ---> NEW: Unity's Built-in Physics Controller <---
    private CharacterController controller;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (gameInput == null) gameInput = FindFirstObjectByType<GameInput>();

        // ---> ADD THIS PIECE <---
        if (IsLocalPlayer())
        {
            LocalInstance = this;
        }
    }

    private void Start()
    {
        if (IsLocalPlayer())
        {
            gameInput.OnInteractAction += GameInput_OnInteractAction;
            gameInput.OnJumpAction += GameInput_OnJumpAction;

            ThirdPersonCameraController cam = FindFirstObjectByType<ThirdPersonCameraController>();
            if (cam != null) cam.SetPlayerTarget(this.transform);
        }
    }

    private void GameInput_OnJumpAction(object sender, System.EventArgs e)
    {
        jumpBufferTimer = jumpBufferTime;
    }

    private void GameInput_OnInteractAction(object sender, System.EventArgs e)
    {
        float interactionDistance = 2f;
        Vector3 rayStart = transform.position + Vector3.up * 0.5f; 
        float castRadius = 0.5f; 

        // ===== CHECK PORTAL =====
        if (Physics.SphereCast(rayStart, castRadius, transform.forward, out RaycastHit portalHit, interactionDistance))
        {
            if (portalHit.transform.TryGetComponent(out Portal portal)) { portal.TryEnterPortal(); return; }
        }

        // ===== CHECK LEVER =====
        if (Physics.SphereCast(rayStart, castRadius, transform.forward, out RaycastHit leverHit, interactionDistance))
        {
            if (leverHit.transform.TryGetComponent(out LeverController lever)) { lever.ToggleLever(); return; }
        }

        // ===== CHECK WATERING CAN MANAGER =====
        if (Physics.SphereCast(rayStart, castRadius, transform.forward, out RaycastHit canHit, interactionDistance))
        {
            if (canHit.transform.TryGetComponent(out HarvestMatrixManager manager)) { manager.WaterGarden(); return; }
        }

        // ===== CHECK PLACED TORCH WITH EMPTY HANDS =====
        if (Physics.SphereCast(rayStart, castRadius, transform.forward, out RaycastHit emptyHandHit, interactionDistance))
        {
            if (emptyHandHit.transform.TryGetComponent(out TorchPedestal fullPed) && fullPed.CurrentTorch != null)
            {
                fullPed.OpenTorchUI(); 
                return; 
            }
        }

        // ===== IF HOLDING OBJECT =====
        if (heldObject != null)
        {
            if (Physics.SphereCast(rayStart, castRadius, transform.forward, out RaycastHit hit, interactionDistance))
            {
                if (hit.transform.TryGetComponent(out FruitBasket basket) && !basket.HasFruit())
                {
                    GameObject fruitObj = heldObject.gameObject;
                    heldObject.Drop(); 
                    basket.PlaceFruit(fruitObj); 
                    heldObject = null; return;
                }
                if (hit.transform.TryGetComponent(out TutorialORGateBasket tutorialBasket))
                {
                    GameObject fruitObj = heldObject.gameObject;
                    heldObject.Drop(); 
                    tutorialBasket.PlaceFruitInteractive(fruitObj);
                    heldObject = null; return;
                }
                if (hit.transform.TryGetComponent(out PuzzleSlot slot))
                {
                    TowerPiece piece = heldObject.GetComponent<TowerPiece>();
                    if (piece != null && slot.TryPlace(piece))
                    {
                        heldObject.Drop(); heldObject = null; return;
                    }
                }
                if (hit.transform.TryGetComponent(out TorchPedestal ped) && ped.CurrentTorch == null) 
                {
                    GameObject torchObj = heldObject.gameObject;
                    heldObject.Drop(); 
                    ped.PlaceTorchNetworked(torchObj); 
                    heldObject = null; return;
                }
                if (Physics.SphereCast(rayStart, castRadius, transform.forward, out RaycastHit holdingHit, interactionDistance))
                {
                    if (holdingHit.transform.TryGetComponent(out TorchPedestal holdingPed) && holdingPed.CurrentTorch != null) 
                    {
                        holdingPed.OpenTorchUI(); return; 
                    }
                } 
                if (hit.transform.TryGetComponent(out SoilMound mound) && !mound.HasSeed()) 
                {
                    GameObject seedObj = heldObject.gameObject;
                    heldObject.Drop(); 
                    mound.PlaceSeedNetworked(seedObj); 
                    heldObject = null; return;
                }
            }

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
                grabbable.Grab(holdPoint); return; 
            }
        }
    }

    private void Update()
    {
        if (!IsLocalPlayer()) return; 

        HandleMovementAndGravity();
        HandleInteractions();
    }

    public bool IsWalking() => isWalking;
    public bool IsJumping() => isJumping;

    private void HandleInteractions()
    {
        Vector2 inputVector = gameInput.GetMovementVectorNormalized();
        Vector3 moveDir = new Vector3(inputVector.x, 0, inputVector.y);
        if (moveDir != Vector3.zero) lastInteractions = moveDir;

        float interactionDistance = 2f;
        if (Physics.Raycast(transform.position, lastInteractions, out RaycastHit raycastHit, interactionDistance, Modules))
        {
            if (raycastHit.transform.TryGetComponent(out Modules module)) { }
        }
    }

    // ---> MASSIVE CLEANUP: 100+ lines reduced to this! <---
    private void HandleMovementAndGravity()
    {

        if (Camera.main == null || gameInput == null) return;
        // 1. Get Camera Direction
        Vector2 inputVector = gameInput.GetMovementVectorNormalized();
        Vector3 camForward = Camera.main.transform.forward;
        Vector3 camRight = Camera.main.transform.right;

        camForward.y = 0f; camRight.y = 0f;
        camForward.Normalize(); camRight.Normalize();

        Vector3 moveDir = camForward * inputVector.y + camRight * inputVector.x;

        // 2. Horizontal Movement & Rotation
        if (moveDir != Vector3.zero)
        {
            transform.forward = Vector3.Slerp(transform.forward, moveDir, Time.deltaTime * 10f);
            isWalking = true;

            if (!tutorialMovementDone)
            {
                TutorialManager tutorial = FindFirstObjectByType<TutorialManager>();
                if (tutorial != null) { tutorial.CompleteMovementStep(); tutorialMovementDone = true; }
            }
        }
        else
        {
            isWalking = false;
        }

        // 3. Gravity & Jumping (Controller automatically handles floor detection!)
        if (controller.isGrounded)
        {
            verticalVelocity = -5f; // Stick slightly to the ground

            if (jumpBufferTimer > 0f)
            {
                verticalVelocity = jumpForce;
                isJumping = true;
                jumpBufferTimer = 0f;
            }
            else { isJumping = false; }
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
            if (verticalVelocity < -25f) verticalVelocity = -25f; // Terminal velocity
        }

        jumpBufferTimer -= Time.deltaTime;

        // 4. Combine and Move! (This automatically calculates stairs, walls, and slopes)
        Vector3 finalMovement = (moveDir * moveSpeed) + (Vector3.up * verticalVelocity);
        controller.Move(finalMovement * Time.deltaTime);
    }

    // ---> NEW PUSH LOGIC <---
    // This built-in function triggers when the CharacterController bumps into something
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;

        if (body != null && !body.isKinematic)
        {
            // Don't push objects we are standing on top of
            if (hit.moveDirection.y < -0.3f) return;

            Vector3 pushDir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);
            body.AddForce(pushDir * 5f, ForceMode.VelocityChange);
        }
    }

    private bool IsLocalPlayer()
    {
        if (photonView == null || !PhotonNetwork.InRoom) return true;
        return photonView.IsMine;
    }

    private void OnDestroy()
    {
        if (gameInput != null && IsLocalPlayer())
        {
            gameInput.OnInteractAction -= GameInput_OnInteractAction;
            gameInput.OnJumpAction -= GameInput_OnJumpAction;
        }
    }

    public void ToggleControl(bool hasControl)
    {
        this.enabled = hasControl;
        
        // If we are freezing the player, force the animation variables to false
        if (!hasControl)
        {
            isWalking = false; 
            isJumping = false;

            MobileInputUI mobileJoystick = FindFirstObjectByType<MobileInputUI>();
            if (mobileJoystick != null)
            {
                mobileJoystick.ResetJoystick();
            }
        }
    }

    // ---> ADD THESE THREE HELPER METHODS TO THE BOTTOM OF PLAYER.CS <---
    public Transform GetHoldPoint() => holdPoint;

    public void SetHeldObjectSilently(GrabbableObject obj)
    {
        heldObject = obj;
    }
    public GrabbableObject GetHeldObject()
    {
        return heldObject;
    }
    
}