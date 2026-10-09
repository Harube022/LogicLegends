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

    public static GameInput Instance => instance;

    // Modal gameplay UI can temporarily suppress controls without disabling the
    // shared input component or changing input behavior in other stages.
    public bool GameplayInputBlocked { get; private set; }

    public void SetGameplayInputBlocked(bool blocked)
    {
        GameplayInputBlocked = blocked;
        if (blocked) mobileMovementVector = Vector2.zero;
    }

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
        if (GameplayInputBlocked) return;
        TruthTableControlsOnboarding truthPractice = TruthTableControlsOnboarding.Active;
        if (truthPractice != null && truthPractice.IsActive)
        {
            if (!truthPractice.AllowsJump) return;
            truthPractice.NoteJumpInput();
        }
        PropositionalControlsOnboarding practice = PropositionalControlsOnboarding.Active;
        if (practice != null && practice.IsActive)
        {
            if (!practice.AllowsJump) return;
            practice.NoteJumpInput();
        }
        OnJumpAction?.Invoke(this, EventArgs.Empty);
    }

    private void Interact_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    {
        if (GameplayInputBlocked) return;
        TruthTableControlsOnboarding truthPractice = TruthTableControlsOnboarding.Active;
        if (truthPractice != null && truthPractice.IsActive)
        {
            truthPractice.TryPracticeInteraction();
            return;
        }
        PropositionalControlsOnboarding practice = PropositionalControlsOnboarding.Active;
        if (practice != null && practice.IsActive)
        {
            practice.TryPracticeInteraction();
            return;
        }
        OnInteractAction?.Invoke(this, EventArgs.Empty);
    }

    public Vector2 GetMovementVectorNormalized()
    {
        return GetMovementVectorNormalized(0f);
    }

    public Vector2 GetMovementVectorNormalized(float deadZone)
    {
        if (GameplayInputBlocked) return Vector2.zero;
        if (PropositionalControlsOnboarding.Active != null &&
            PropositionalControlsOnboarding.Active.SuppressMovement) return Vector2.zero;
        if (TruthTableControlsOnboarding.Active != null &&
            TruthTableControlsOnboarding.Active.SuppressMovement) return Vector2.zero;
        Vector2 inputVector = playerInputActions.Player.Move.ReadValue<Vector2>();

        float deadZoneSquared = deadZone * deadZone;
        if (mobileMovementVector.sqrMagnitude > deadZoneSquared)
        {
            inputVector = mobileMovementVector;
        }

        return inputVector.sqrMagnitude > deadZoneSquared ? inputVector.normalized : Vector2.zero;
    }

    // ===== MOBILE =====

    public void SetMobileMovement(Vector2 movement)
    {
        mobileMovementVector = movement;
    }

    public void MobileJump()
    {
        if (GameplayInputBlocked) return;
        TruthTableControlsOnboarding truthPractice = TruthTableControlsOnboarding.Active;
        if (truthPractice != null && truthPractice.IsActive)
        {
            if (!truthPractice.AllowsJump) return;
            truthPractice.NoteJumpInput();
        }
        PropositionalControlsOnboarding practice = PropositionalControlsOnboarding.Active;
        if (practice != null && practice.IsActive)
        {
            if (!practice.AllowsJump) return;
            practice.NoteJumpInput();
        }
        OnJumpAction?.Invoke(this, EventArgs.Empty);
    }

    public void MobileInteract()
    {
        if (GameplayInputBlocked) return;
        TruthTableControlsOnboarding truthPractice = TruthTableControlsOnboarding.Active;
        if (truthPractice != null && truthPractice.IsActive)
        {
            truthPractice.TryPracticeInteraction();
            return;
        }
        PropositionalControlsOnboarding practice = PropositionalControlsOnboarding.Active;
        if (practice != null && practice.IsActive)
        {
            practice.TryPracticeInteraction();
            return;
        }
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
