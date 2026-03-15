using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCameraController : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private float sensitivity = 0.15f;
    
    [Header("Zoom & Collision Settings")]
    [SerializeField] private float defaultDistance = 6f;
    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float zoomSmoothness = 10f;
    [SerializeField] private float collisionRadius = 0.3f;
    [SerializeField] private LayerMask collisionLayers; // Assign your Environment layer here!

    private float currentDistance;
    private float targetDistance;
    private float yaw;
    private float pitch;

    [Header("Pitch Limits")]
    [SerializeField] private float minY = -30f;
    [SerializeField] private float maxY = 70f;

    private Vector2 lastMousePosition;
    private bool isDraggingMouse;

    private void Start()
    {
        // Initialize distances
        currentDistance = defaultDistance;
        targetDistance = defaultDistance;
    }

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

        // Calculate the base target position (slightly above the player's feet)
        Vector3 targetPosition = player.position + Vector3.up * 1.5f;
        
        // The direction the camera is looking backward from the player
        Vector3 direction = -transform.forward;

        // ===== COLLISION CHECK =====
        // Shoot a sphere backward from the player to see if walls block the camera
        if (Physics.SphereCast(targetPosition, collisionRadius, direction, out RaycastHit hit, defaultDistance, collisionLayers))
        {
            // If a wall is hit, calculate how far the camera CAN go without clipping
            // We subtract collisionRadius so the camera doesn't clip slightly into the wall
            targetDistance = Mathf.Clamp(hit.distance - collisionRadius, minDistance, defaultDistance);
        }
        else
        {
            // If the path is completely clear, return to the normal distance
            targetDistance = defaultDistance;
        }

        // ===== SMOOTH ZOOM =====
        // Smoothly transition the current distance to the new target distance
        currentDistance = Mathf.Lerp(currentDistance, targetDistance, Time.deltaTime * zoomSmoothness);

        // Apply final position
        transform.position = targetPosition + (direction * currentDistance);
    }

    public void SetPlayerTarget(Transform newTarget)
    {
        player = newTarget;
    }
}