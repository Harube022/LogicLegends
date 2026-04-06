using UnityEngine;
using UnityEngine.EventSystems;

public class MobileInputUI : MonoBehaviour, IDragHandler, IPointerUpHandler, IPointerDownHandler
{
    
    [SerializeField] private RectTransform joystickBackground;
    [SerializeField] private RectTransform joystickHandle;

    private Vector2 inputVector;
    private GameInput gameInput;

    // ---> NEW: A lock to ignore touches during cutscenes <---
    // private bool isJoystickActive = true;

    private void Awake()
    {
        gameInput = FindFirstObjectByType<GameInput>();
    }

    // public void ToggleJoystick(bool state)
    // {
    //     isJoystickActive = state;

    //     // If we are locking it, instantly snap it back to the center
    //     if (!isJoystickActive)
    //     {
    //         inputVector = Vector2.zero;
    //         if (joystickHandle != null) joystickHandle.anchoredPosition = Vector2.zero;
    //         if (gameInput != null) gameInput.SetMobileMovement(inputVector);
    //     }
    // }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 position;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            joystickBackground,
            eventData.position,
            eventData.pressEventCamera,
            out position
        );

        // convert to -1 to 1 range
        position.x = position.x / (joystickBackground.sizeDelta.x / 2);
        position.y = position.y / (joystickBackground.sizeDelta.y / 2);

        inputVector = new Vector2(position.x, position.y);
        inputVector = Vector2.ClampMagnitude(inputVector, 1f);

        // move handle
        joystickHandle.anchoredPosition = new Vector2(
            inputVector.x * (joystickBackground.sizeDelta.x / 2),
            inputVector.y * (joystickBackground.sizeDelta.y / 2)
        );

        gameInput.SetMobileMovement(inputVector);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        inputVector = Vector2.zero;
        joystickHandle.anchoredPosition = Vector2.zero;
        gameInput.SetMobileMovement(inputVector);
    }

    // ===== BUTTONS =====

    public void Jump()
    {
        Debug.Log("MOBILE JUMP PRESSED");
        gameInput.MobileJump();
    }

    public void Interact()
    {
        gameInput.MobileInteract();
    }

    // ---> NEW: Force the joystick to snap back to the center <---
    public void ResetJoystick()
    {
        inputVector = Vector2.zero;
        
        if (joystickHandle != null)
        {
            joystickHandle.anchoredPosition = Vector2.zero;
        }

        if (gameInput != null)
        {
            gameInput.SetMobileMovement(inputVector);
        }
    }
}