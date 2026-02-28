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
        OnInteractAction?.Invoke(this, EventArgs.Empty);
    }
}