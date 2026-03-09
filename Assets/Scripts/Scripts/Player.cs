using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
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
    private float groundSnapDistance = 0.3f;
    private float jumpBufferTimer;

    private void Awake()
    {
        if (gameInput == null)
        {
            gameInput = FindObjectOfType<GameInput>();
        }
    }

    private void Start()
    {
        gameInput.OnInteractAction += GameInput_OnInteractAction;
        gameInput.OnJumpAction += GameInput_OnJumpAction;
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
        Vector3 rayStart = transform.position + Vector3.up * 1f;

        // ===== CHECK PORTAL FIRST =====
        if (Physics.Raycast(rayStart, transform.forward, out RaycastHit portalHit, interactionDistance))
        {
            if (portalHit.transform.TryGetComponent(out Portal portal))
            {
                portal.TryEnterPortal();
                return;
            }
        }

        // ===== IF HOLDING OBJECT =====
        if (heldObject != null)
        {
            TowerPiece piece = heldObject.GetComponent<TowerPiece>();

            if (Physics.Raycast(rayStart, transform.forward, out RaycastHit hit, interactionDistance))
            {
                if (hit.transform.TryGetComponent(out PuzzleSlot slot))
                {
                    if (slot.TryPlace(piece))
                    {
                        heldObject.Drop();
                        heldObject = null;
                        return;
                    }
                }
            }

            heldObject.Drop();
            heldObject = null;
            return;
        }

        // ===== IF NOT HOLDING, TRY GRAB =====
        int mask = ~LayerMask.GetMask("PuzzleSlot");

        if (Physics.Raycast(rayStart, transform.forward, out RaycastHit grabHit, interactionDistance, mask))
        {
            if (grabHit.transform.TryGetComponent(out GrabbableObject grabbable))
            {
                heldObject = grabbable;
                grabbable.Grab(holdPoint);
            }
        }
    }

    private void Update()
    {
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

        // // ===== NORMAL MOVE =====
        // if (!Physics.CapsuleCast(capsuleBottom, capsuleTop, playerRadius, moveDir, moveDistance))
        // {
        //     transform.position += moveDir * moveDistance;
        // }
        // else
        // {
        //     // ===== TRY STEP UP =====
        //     float stepHeight = 0.6f; // how high we can climb

        //     Vector3 stepUp = Vector3.up * stepHeight;

        //     Vector3 newBottom = capsuleBottom + stepUp;
        //     Vector3 newTop = capsuleTop + stepUp;

        //     if (!Physics.CapsuleCast(newBottom, newTop, playerRadius, moveDir, moveDistance))
        //     {
        //         transform.position += stepUp;                 // lift
        //         transform.position += moveDir * moveDistance; // move
        //     }
        //     // else → real wall → stop
        // }
        // ===== NORMAL MOVE =====
        // We add 'out RaycastHit hit' so we can get information about what we bumped into
        if (!Physics.CapsuleCast(capsuleBottom, capsuleTop, playerRadius, moveDir, out RaycastHit hit, moveDistance))
        {
            transform.position += moveDir * moveDistance;
        }
        else
        {
            // ---> NEW PUSH LOGIC START <---
            // Check if the object blocking us has a Rigidbody attached
            Rigidbody hitRb = hit.collider.attachedRigidbody;

            // If it does, and it's allowed to be moved by physics (!isKinematic), push it!
            if (hitRb != null && !hitRb.isKinematic)
            {
                float pushForce = 200f; // Tweak this number until the pushing feels right!
                
                // Apply force in the direction the player is walking
                hitRb.AddForce(moveDir * pushForce, ForceMode.Force);
            }
            // ---> NEW PUSH LOGIC END <---

            // ===== TRY STEP UP =====
            float stepHeight = 0.6f; // how high we can climb

            Vector3 stepUp = Vector3.up * stepHeight;

            Vector3 newBottom = capsuleBottom + stepUp;
            Vector3 newTop = capsuleTop + stepUp;

            if (!Physics.CapsuleCast(newBottom, newTop, playerRadius, moveDir, moveDistance))
            {
                transform.position += stepUp;                 // lift
                transform.position += moveDir * moveDistance; // move
            }
            // else → real wall → stop
        }

        // ===== FACE DIRECTION =====
        float rotateSpeed = 10f;
        transform.forward = Vector3.Slerp(transform.forward, moveDir, Time.deltaTime * rotateSpeed);

        isWalking = true;
    }
    // -----------------------------------------------
    private void HandleGravity()
    {
        jumpBufferTimer -= Time.deltaTime;

        // bigger + safer values
        float rayStartOffset = 1.0f;
        float rayDistance = 3.0f;

        // jump
        if (isGrounded && jumpBufferTimer > 0f && !isJumping)
        {
            verticalVelocity = jumpForce;
            isJumping = true;
            isGrounded = false;
            jumpBufferTimer = 0f;
        }

        bool hitGround = Physics.Raycast(
            transform.position + Vector3.up * rayStartOffset,
            Vector3.down,
            out RaycastHit hit,
            rayDistance
        );

        float maxSlopeAngle = 45f;
        bool validGround = false;

        if (validGround)
        {
            float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);

            if (slopeAngle <= maxSlopeAngle)
            {
                validGround = true;
            }
        }

        // ===== GOING UP =====
        if (verticalVelocity > 0f)
        {
            isGrounded = false;
            verticalVelocity += gravity * Time.deltaTime;

            float moveY = verticalVelocity * Time.deltaTime;

            float playerRadius = 0.7f;
            float playerHeight = 2f;

            Vector3 capsuleBottom = transform.position;
            Vector3 capsuleTop = transform.position + Vector3.up * playerHeight;

            if (!Physics.CapsuleCast(
                    capsuleBottom,
                    capsuleTop,
                    playerRadius,
                    Vector3.up,
                    Mathf.Abs(moveY)))
            {
                transform.position += Vector3.up * moveY;
            }
            else
            {
                verticalVelocity = 0f;
            }

            return;
        }

        // ===== ON GROUND =====
        if (hitGround)
        {
            isGrounded = true;
            isJumping = false;

            // small downward force so we stay glued
            verticalVelocity = -5f;

            float moveY = verticalVelocity * Time.deltaTime;

            float playerRadius = 0.7f;
            float playerHeight = 2f;

            Vector3 capsuleBottom = transform.position;
            Vector3 capsuleTop = transform.position + Vector3.up * playerHeight;

            if (!Physics.CapsuleCast(
                    capsuleBottom,
                    capsuleTop,
                    playerRadius,
                    Vector3.down,
                    Mathf.Abs(moveY)))
            {
                transform.position += Vector3.up * moveY;
            }
        }
        // ===== FALLING =====
        else
        {
            isGrounded = false;
            verticalVelocity += gravity * Time.deltaTime;

            float moveY = verticalVelocity * Time.deltaTime;

            float playerRadius = 0.7f;
            float playerHeight = 2f;

            Vector3 capsuleBottom = transform.position;
            Vector3 capsuleTop = transform.position + Vector3.up * playerHeight;

            if (!Physics.CapsuleCast(
                    capsuleBottom,
                    capsuleTop,
                    playerRadius,
                    Vector3.down,
                    Mathf.Abs(moveY)))
            {
                transform.position += Vector3.up * moveY;
            }
            else
            {
                verticalVelocity = 0f;
            }
        }
    }

    private void OnDestroy()
    {
        if (gameInput != null)
        {
            gameInput.OnInteractAction -= GameInput_OnInteractAction;
            gameInput.OnJumpAction -= GameInput_OnJumpAction;
        }
    }
}