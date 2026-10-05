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
    [Header("Optional running")]
    [SerializeField, Min(0f)] private float walkSecondsBeforeRun = 3f;
    [SerializeField, Min(1f)] private float runSpeedMultiplier = 1.5f;
    [SerializeField, Range(0f, 0.5f)] private float movementInputDeadZone = 0.12f;
    [SerializeField] private GameInput gameInput;
    [SerializeField] private LayerMask countersLayerMask;
    [SerializeField] private LayerMask Modules;

    private bool isWalking;
    private bool runningEnabled;
    private bool isRunning;
    private float continuousWalkSeconds;
    private Vector3 lastInteractions;

    [SerializeField] private float jumpForce = 15f;
    [SerializeField] private float gravity = -30f;
    [SerializeField] private float jumpBufferTime = 0.15f;

    private float verticalVelocity;
    private bool isJumping;
    private float airborneSeconds;
    private float jumpBufferTimer;
    private bool tutorialMovementDone = false;

    private bool isGuidedMovementActive;
    private Transform guidedMovementTarget;
    private float guidedMovementSpeed;
    private float guidedMovementStoppingDistance;

    // ---> NEW: Unity's Built-in Physics Controller <---
    private CharacterController controller;
    private bool ownsLocalInput;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (gameInput == null) gameInput = FindFirstObjectByType<GameInput>();

        if (IsLocalPlayer())
        {
            // Offline Player components all appear local. Only one may own the shared
            // GameInput and gameplay camera.
            if (LocalInstance != null && LocalInstance != this && LocalInstance.gameObject.activeInHierarchy)
            {
                Debug.LogError($"Duplicate local Player detected: '{name}'. " +
                               $"'{LocalInstance.name}' already owns local input. Disabling duplicate control.");
                enabled = false;
                return;
            }

            LocalInstance = this;
            ownsLocalInput = true;
        }
    }

    private void Start()
    {
        if (ownsLocalInput)
        {
            if (gameInput == null)
            {
                Debug.LogError($"{name} cannot receive input because no GameInput exists.");
                enabled = false;
                return;
            }

            gameInput.OnInteractAction += GameInput_OnInteractAction;
            gameInput.OnJumpAction += GameInput_OnJumpAction;

            // ThirdPersonCameraController cam = FindFirstObjectByType<ThirdPersonCameraController>();
            // if (cam != null) cam.SetPlayerTarget(this.transform);

            // Find Cinemachine camera and set target
            GameObject gameplayCameraObject = GameObject.Find("CM_ThirdPersonCam");
            Unity.Cinemachine.CinemachineCamera cmCam = gameplayCameraObject != null
                ? gameplayCameraObject.GetComponent<Unity.Cinemachine.CinemachineCamera>()
                : FindFirstObjectByType<Unity.Cinemachine.CinemachineCamera>();
            if (cmCam != null)
            {
                // Use child CameraTarget if available, otherwise fall back to player root
                Transform camTarget = transform.Find("CameraTarget");
                cmCam.Target.TrackingTarget = camTarget != null ? camTarget : transform;
            }
        }
    }

    private void GameInput_OnJumpAction(object sender, System.EventArgs e)
    {
        if (!enabled || isGuidedMovementActive) return; // Guided movement owns locomotion until the door transition finishes.
        jumpBufferTimer = jumpBufferTime;
    }

    private void GameInput_OnInteractAction(object sender, System.EventArgs e)
    {
        if (!enabled || isGuidedMovementActive) return; // Event subscriptions still fire on disabled behaviours.
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
        if (!ownsLocalInput || controller == null || !controller.enabled)
        {
            OffRun();
            isWalking = false;
            return;
        }

        if (isGuidedMovementActive)
        {
            HandleGuidedMovement();
            return;
        }

        HandleMovementAndGravity();
        HandleInteractions();
    }

    public bool IsWalking() => isWalking;
    public bool IsJumping() => isJumping;
    public bool IsRunning() => isRunning;

    public void SetRunningEnabled(bool value)
    {
        if (runningEnabled == value) return;
        runningEnabled = value;
        OffRun(); // Enabling starts a fresh three-second walk, never an instant run.
    }

    public void OnRun()
    {
        if (!runningEnabled || !enabled || isGuidedMovementActive ||
            controller == null || !controller.enabled ||
            gameInput == null || gameInput.GameplayInputBlocked || !isWalking)
            return;
        isRunning = true;
    }

    public void OffRun()
    {
        isRunning = false;
        continuousWalkSeconds = 0f;
    }

    private void HandleInteractions()
    {
        Vector2 inputVector = GetMovementInput();
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

        if (Camera.main == null || gameInput == null)
        {
            isWalking = false;
            OffRun();
            return;
        }
        // 1. Get Camera Direction
        Vector2 inputVector = GetMovementInput();
        Vector3 camForward = Camera.main.transform.forward;
        Vector3 camRight = Camera.main.transform.right;

        camForward.y = 0f; camRight.y = 0f;
        camForward.Normalize(); camRight.Normalize();

        Vector3 moveDir = camForward * inputVector.y + camRight * inputVector.x;

        // 2. Horizontal Movement & Rotation
        // The same dead-zone-filtered input drives both movement and the run
        // countdown. Grounding, velocity, and direction changes do not reset it.
        isWalking = inputVector != Vector2.zero;
        if (isWalking && moveDir != Vector3.zero)
        {
            transform.forward = Vector3.Slerp(transform.forward, moveDir, Time.deltaTime * 10f);

            if (!tutorialMovementDone)
            {
                TutorialManager tutorial = FindFirstObjectByType<TutorialManager>();
                if (tutorial != null) { tutorial.CompleteMovementStep(); tutorialMovementDone = true; }
            }
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

            // These controllers have a Jump state but no separate Fall state.
            // Use it for a real fall while avoiding flicker over tiny floor gaps.
            if (runningEnabled && verticalVelocity < 0f)
            {
                airborneSeconds += Time.deltaTime;
                if (airborneSeconds >= 0.1f) isJumping = true;
            }
        }

        if (controller.isGrounded) airborneSeconds = 0f;

        jumpBufferTimer -= Time.deltaTime;

        // Count continuous movement input, including jumping and falling. Only
        // releasing input or disabling gameplay resets the run state.
        if (runningEnabled && isWalking && !gameInput.GameplayInputBlocked)
        {
            if (!isRunning)
            {
                continuousWalkSeconds += Time.deltaTime;
                if (continuousWalkSeconds >= walkSecondsBeforeRun) OnRun();
            }
        }
        else OffRun();

        // The serialized moveSpeed stays the normal walk speed. Multiplying only
        // the current movement prevents repeated activation from stacking speed.
        Vector3 finalMovement = (moveDir * moveSpeed * (isRunning ? runSpeedMultiplier : 1f)) +
                                (Vector3.up * verticalVelocity);
        controller.Move(finalMovement * Time.deltaTime);

        // Move updates CharacterController.isGrounded. Clear the visual jump flag
        // on the landing frame instead of waiting for the next Update.
        if (isJumping && verticalVelocity <= 0f && controller.isGrounded)
            isJumping = false;
    }

    private Vector2 GetMovementInput()
    {
        return gameInput.GetMovementVectorNormalized(runningEnabled ? movementInputDeadZone : 0f);
    }

    private void HandleGuidedMovement()
    {
        if (guidedMovementTarget == null)
        {
            EndGuidedMovement();
            return;
        }

        // Door targets differ slightly in height, so guide only along the floor plane.
        Vector3 toTarget = guidedMovementTarget.position - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude <= guidedMovementStoppingDistance * guidedMovementStoppingDistance)
        {
            EndGuidedMovement();
            return;
        }

        Vector3 moveDirection = toTarget.normalized;
        transform.forward = Vector3.Slerp(transform.forward, moveDirection, Time.deltaTime * 10f);
        isWalking = true;
        isJumping = false;
        jumpBufferTimer = 0f;

        if (controller.isGrounded)
        {
            verticalVelocity = -5f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
            if (verticalVelocity < -25f) verticalVelocity = -25f;
        }

        Vector3 movement = (moveDirection * guidedMovementSpeed) + (Vector3.up * verticalVelocity);
        controller.Move(movement * Time.deltaTime);
    }

    public void BeginGuidedMovement(Transform target, float speed, float stoppingDistance = 0.2f)
    {
        if (!IsLocalPlayer() || target == null) return;

        OffRun();

        guidedMovementTarget = target;
        guidedMovementSpeed = Mathf.Max(0.1f, speed);
        guidedMovementStoppingDistance = Mathf.Max(0.05f, stoppingDistance);
        isGuidedMovementActive = true;
        jumpBufferTimer = 0f;

        MobileInputUI mobileJoystick = FindFirstObjectByType<MobileInputUI>();
        if (mobileJoystick != null)
        {
            mobileJoystick.ResetJoystick();
        }
    }

    public void EndGuidedMovement()
    {
        OffRun();
        airborneSeconds = 0f;
        isGuidedMovementActive = false;
        guidedMovementTarget = null;
        isWalking = false;
        isJumping = false;
    }

    // ---> NEW PUSH LOGIC <---
    // This built-in function triggers when the CharacterController bumps into something
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;

        if (body != null && !body.isKinematic)
        {
             // ---> FIX: Prevent pushing any object that can be grabbed! <---
            if (hit.collider.TryGetComponent(out GrabbableObject _)) 
            return;
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
        if (gameInput != null && ownsLocalInput)
        {
            gameInput.OnInteractAction -= GameInput_OnInteractAction;
            gameInput.OnJumpAction -= GameInput_OnJumpAction;
        }

        if (LocalInstance == this)
            LocalInstance = null;
    }


    public void ToggleControl(bool hasControl)
    {
        if (!hasControl)
        {
            OffRun();
            EndGuidedMovement();
        }

        this.enabled = hasControl;
        
        // If we are freezing the player, force the animation variables to false
        if (!hasControl)
        {
            jumpBufferTimer = 0f;
            isWalking = false; 
            isJumping = false;
            airborneSeconds = 0f;

            MobileInputUI mobileJoystick = FindFirstObjectByType<MobileInputUI>();
            if (mobileJoystick != null)
            {
                mobileJoystick.ResetJoystick();
            }
        }
    }

    private void OnDisable()
    {
        OffRun();
        isWalking = false;
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
