using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCameraController : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private float sensitivity = 0.15f;
    [SerializeField] private float distance = 6f;
    [SerializeField] private float minY = -30f;
    [SerializeField] private float maxY = 70f;

    private float yaw;
    private float pitch;

    private Vector2 lastMousePosition;
    private bool isDraggingMouse;

    private void Update()
    {
        HandleRotation();
    }

    private void LateUpdate()
    {
        FollowPlayer();
    }

    private void HandleRotation()
    {
        Vector2 lookInput = Vector2.zero;

        // ===== MOBILE LOOK =====
        lookInput += MobileLookInput.LookDelta;

#if UNITY_EDITOR
        // ===== EDITOR MOUSE DRAG =====
        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                isDraggingMouse = true;
                lastMousePosition = Mouse.current.position.ReadValue();
            }

            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                isDraggingMouse = false;
            }

            if (isDraggingMouse)
            {
                Vector2 currentPos = Mouse.current.position.ReadValue();
                Vector2 delta = currentPos - lastMousePosition;

                lookInput += delta;

                lastMousePosition = currentPos;
            }
        }
#endif

        yaw += lookInput.x * sensitivity;
        pitch -= lookInput.y * sensitivity;

        pitch = Mathf.Clamp(pitch, minY, maxY);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void FollowPlayer()
    {
        if (player == null) return;

        Vector3 targetPosition = player.position + Vector3.up * 1.5f;
        transform.position = targetPosition - transform.forward * distance;
    }
}