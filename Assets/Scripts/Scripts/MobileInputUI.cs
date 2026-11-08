using UnityEngine;
using UnityEngine.EventSystems;

public class MobileInputUI : MonoBehaviour, IDragHandler, IPointerUpHandler, IPointerDownHandler
{
    
    [SerializeField] private RectTransform joystickBackground;
    [SerializeField] private RectTransform joystickHandle;

    private Vector2 inputVector;
    public Vector2 CurrentInput => inputVector;
    private GameInput gameInput;
    private int pointer;
    private bool dragging;
    private bool IsJoystick => joystickBackground != null && joystickHandle != null;
    public bool HasJoystick => IsJoystick;
    private void OnEnable()
    { if (IsJoystick) SoloTouchSettings.RegisterControl(transform as RectTransform); }

    /// <summary>Find the configured joystick, excluding button-only MobileInputUI objects.</summary>
    public static MobileInputUI FindJoystick()
    {
        MobileInputUI fallback = null;
        foreach (MobileInputUI candidate in FindObjectsByType<MobileInputUI>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate.joystickBackground == null || candidate.joystickHandle == null ||
                !(candidate.transform is RectTransform)) continue;
            if (candidate.isActiveAndEnabled) return candidate;
            if (fallback == null) fallback = candidate;
        }
        return fallback;
    }

    // ---> NEW: A lock to ignore touches during cutscenes <---
    // private bool isJoystickActive = true;

    private void Awake()
    {
        gameInput = GameInput.Instance != null ? GameInput.Instance : FindFirstObjectByType<GameInput>();
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
        if (!IsJoystick || !dragging || pointer != eventData.pointerId) return;
        if (gameInput == null) gameInput = GameInput.Instance;
        if (gameInput == null || gameInput.GameplayActionsBlocked) { ResetJoystick(); return; }
        Vector2 position;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            joystickBackground,
            eventData.position,
            eventData.pressEventCamera,
            out position
        );

        // convert to -1 to 1 range
        position.x = position.x / Mathf.Max(1, joystickBackground.rect.width / 2);
        position.y = position.y / Mathf.Max(1, joystickBackground.rect.height / 2);

        inputVector = new Vector2(position.x, position.y);
        inputVector = Vector2.ClampMagnitude(inputVector, 1f);

        // move handle
        joystickHandle.anchoredPosition = new Vector2(
            inputVector.x * (joystickBackground.rect.width / 2),
            inputVector.y * (joystickBackground.rect.height / 2)
        );

        gameInput.SetMobileMovement(inputVector);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!IsJoystick || dragging && pointer != eventData.pointerId) return;
        pointer = eventData.pointerId; dragging = true;
        OnDrag(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (dragging && pointer == eventData.pointerId) ResetJoystick();
    }

    // ===== BUTTONS =====

    public void Jump()
    {
        if (gameInput == null) gameInput = GameInput.Instance;
        gameInput?.MobileJump();
    }

    public void Interact()
    {
        if (gameInput == null) gameInput = GameInput.Instance;
        gameInput?.MobileInteract();
    }

    // ---> NEW: Force the joystick to snap back to the center <---
    public void ResetJoystick()
    {
        dragging = false;
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
    private void OnDisable() { if (IsJoystick) ResetJoystick(); }
    private void OnApplicationFocus(bool focus) { if (!focus && IsJoystick) ResetJoystick(); }
    private void OnApplicationPause(bool paused) { if (paused && IsJoystick) ResetJoystick(); }
}
