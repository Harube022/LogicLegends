using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameInput : MonoBehaviour
{
    public event EventHandler OnInteractAction;
    public event EventHandler OnJumpAction;

    private PlayerInputActions playerInputActions;
    private Vector2 mobileMovementVector;
    private static GameInput instance;

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        playerInputActions = new PlayerInputActions();
        playerInputActions.Player.Enable();

        playerInputActions.Player.Interact.performed += Interact_performed;
        playerInputActions.Player.Jump.performed += Jump_performed;
    }

    private void Jump_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    {
        OnJumpAction?.Invoke(this, EventArgs.Empty);
    }

    private void Interact_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    {
        OnInteractAction?.Invoke(this, EventArgs.Empty);
    }

    public Vector2 GetMovementVectorNormalized()
    {
        Vector2 inputVector = playerInputActions.Player.Move.ReadValue<Vector2>();

        if (mobileMovementVector != Vector2.zero)
        {
            inputVector = mobileMovementVector;
        }

        return inputVector.normalized;
    }

    // ===== MOBILE =====

    public void SetMobileMovement(Vector2 movement)
    {
        mobileMovementVector = movement;
    }

    public void MobileJump()
    {
        OnJumpAction?.Invoke(this, EventArgs.Empty);
    }

    public void MobileInteract()
    {
        // 1. FIRST CHECK: Do we have a block selected in our inventory? If so, drop it!
        if (InventoryManager.Instance != null && InventoryManager.Instance.HasBlockSelected())
        {
            InventoryManager.Instance.DropSelectedBlock();
            Debug.Log("Interact pressed: Dropping selected inventory block!");
            return; // Exit early so we don't immediately try to re-pick it up
        }

        // 2. SECOND CHECK: Scan for nearby TruthBlocks to collect if hand/selection is empty
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, 2.0f);
        bool collectedBlock = false;

        foreach (Collider col in nearbyColliders)
        {
            if (col.TryGetComponent(out TruthBlock block))
            {
                // Skip blocks that are already securely inside your inventory slots
                if (block.TryGetComponent(out GrabbableObject grabbable) && grabbable.isStoredInInventory)
                    continue;

                // Trigger inventory collection and auto-selection
                block.CollectBlock();
                Debug.Log("Successfully collected a block through MobileInteract!");
                collectedBlock = true;
                break; // Stop evaluating after collecting one block
            }
        }

        // 2. Only invoke general player interactions (buttons, levers, or grabbing non-inventory items)
        // if we DID NOT perform an inventory collection this frame
        if (!collectedBlock)
        {
            OnInteractAction?.Invoke(this, EventArgs.Empty);
        }
    }
}