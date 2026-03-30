using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCameraController : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private float sensitivity = 0.15f;
    
    [Header("Zoom & Collision Settings")]
    [SerializeField] private float defaultDistance = 6f;
    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float maxDistance = 10f; 
    [SerializeField] private float zoomSmoothness = 10f;
    [SerializeField] private float collisionRadius = 0.3f;
    [SerializeField] private LayerMask collisionLayers; 

    [Header("Zoom Speeds")]
    [SerializeField] private float pcZoomSpeed = 0.5f;     
    [SerializeField] private float mobileZoomSpeed = 0.01f;

    private float userDistance; 
    private float currentDistance;
    private float targetDistance;
    private float yaw;
    private float pitch;

    [Header("Pitch Limits")]
    [SerializeField] private float minY = -30f;
    [SerializeField] private float maxY = 70f;

    [Header("Follow Smoothing")]
    [SerializeField] private float positionSmoothTime = 0.1f; 
    private Vector3 currentFollowPosition;
    private Vector3 followVelocity = Vector3.zero;

    private Vector2 lastMousePosition;
    private bool isDraggingMouse;

    private void Start()
    {
        userDistance = defaultDistance;
        currentDistance = defaultDistance;
        targetDistance = defaultDistance;

        if (player != null) currentFollowPosition = player.position;
    }

    private void Update()
    {
        HandleRotation();
        HandleZoom(); 
    }

    private void LateUpdate()
    {
        FollowPlayer();
    }

    private void HandleZoom()
    {
        float zoomDelta = 0f;

        // ===== 1. PC SCROLL WHEEL =====
        if (Mouse.current != null)
        {
            float scrollY = Mouse.current.scroll.ReadValue().y;
            
            if (Mathf.Abs(scrollY) > 0.01f)
            {
                zoomDelta = Mathf.Sign(scrollY) * pcZoomSpeed;
            }
        }

        // ===== 2. MOBILE PINCH TO ZOOM =====
        UnityEngine.InputSystem.Controls.TouchControl touch0 = null;
        UnityEngine.InputSystem.Controls.TouchControl touch1 = null;

        if (Touchscreen.current != null)
        {
            // NEW: Only grab active touches that are on the RIGHT HALF of the screen
            foreach (var touch in Touchscreen.current.touches)
            {
                if (touch.isInProgress && touch.position.ReadValue().x > Screen.width / 2f)
                {
                    if (touch0 == null) touch0 = touch;
                    else if (touch1 == null) touch1 = touch;
                }
            }
        }

        if (touch0 != null && touch1 != null)
        {
            Vector2 touch0Pos = touch0.position.ReadValue();
            Vector2 touch1Pos = touch1.position.ReadValue();
            
            Vector2 touch0PrevPos = touch0Pos - touch0.delta.ReadValue();
            Vector2 touch1PrevPos = touch1Pos - touch1.delta.ReadValue();

            float prevMagnitude = (touch0PrevPos - touch1PrevPos).magnitude;
            float currentMagnitude = (touch0Pos - touch1Pos).magnitude;

            zoomDelta = (currentMagnitude - prevMagnitude) * mobileZoomSpeed;
        }

        // ===== APPLY ZOOM =====
        if (zoomDelta != 0)
        {
            userDistance -= zoomDelta; 
            userDistance = Mathf.Clamp(userDistance, minDistance, maxDistance);
        }
    }

    private void HandleRotation()
    {
        Vector2 lookInput = Vector2.zero;
        lookInput += MobileLookInput.LookDelta;

#if UNITY_EDITOR
        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                isDraggingMouse = true;
                lastMousePosition = Mouse.current.position.ReadValue();
            }
            if (Mouse.current.leftButton.wasReleasedThisFrame) isDraggingMouse = false;

            if (isDraggingMouse)
            {
                Vector2 currentPos = Mouse.current.position.ReadValue();
                lookInput += currentPos - lastMousePosition;
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

        currentFollowPosition = Vector3.SmoothDamp(currentFollowPosition, player.position, ref followVelocity, positionSmoothTime);
        Vector3 targetPosition = currentFollowPosition + Vector3.up * 1.5f;
        Vector3 direction = -transform.forward;

        if (Physics.SphereCast(targetPosition, collisionRadius, direction, out RaycastHit hit, userDistance, collisionLayers))
        {
            targetDistance = Mathf.Clamp(hit.distance - collisionRadius, minDistance, userDistance);
        }
        else
        {
            targetDistance = userDistance; 
        }

        currentDistance = Mathf.Lerp(currentDistance, targetDistance, Time.deltaTime * zoomSmoothness);
        Vector3 finalPosition = targetPosition + (direction * currentDistance);

        float minHeightAboveGround = 1f; 
        if (Physics.Raycast(finalPosition + Vector3.up * 5f, Vector3.down, out RaycastHit groundHit, 10f, collisionLayers))
        {
            if (finalPosition.y < groundHit.point.y + minHeightAboveGround)
            {
                finalPosition.y = groundHit.point.y + minHeightAboveGround;
            }
        }

        transform.position = finalPosition;
    }

    public void SetPlayerTarget(Transform newTarget) => player = newTarget;

    public void WarpCamera(Transform targetTransform)
    {
        currentFollowPosition = targetTransform.position;
        followVelocity = Vector3.zero;
        yaw = targetTransform.eulerAngles.y;
        pitch = 15f; 
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 targetPosition = currentFollowPosition + Vector3.up * 1.5f;
        Vector3 direction = -transform.forward;
        transform.position = targetPosition + (direction * currentDistance);
    }
}
// using UnityEngine;
// using UnityEngine.InputSystem;

// public class ThirdPersonCameraController : MonoBehaviour
// {
//     [SerializeField] private Transform player;
//     [SerializeField] private float sensitivity = 0.15f;
    
//     [Header("Zoom & Collision Settings")]
//     [SerializeField] private float defaultDistance = 6f;
//     [SerializeField] private float minDistance = 1f;
//     [SerializeField] private float zoomSmoothness = 10f;
//     [SerializeField] private float collisionRadius = 0.3f;
//     [SerializeField] private LayerMask collisionLayers; // Assign your Environment layer here!

//     private float currentDistance;
//     private float targetDistance;
//     private float yaw;
//     private float pitch;

//     [Header("Pitch Limits")]
//     [SerializeField] private float minY = -30f;
//     [SerializeField] private float maxY = 70f;

//     [Header("Follow Smoothing")]
//     [SerializeField] private float positionSmoothTime = 0.1f; // Adjust this to make it more/less springy
//     private Vector3 currentFollowPosition;
//     private Vector3 followVelocity = Vector3.zero;

//     private Vector2 lastMousePosition;
//     private bool isDraggingMouse;

//     private void Start()
//     {
//         // Initialize distances
//         currentDistance = defaultDistance;
//         targetDistance = defaultDistance;

//         // Initialize the smooth follow position
//         if (player != null) currentFollowPosition = player.position;
//     }

//     private void Update()
//     {
//         HandleRotation();
//     }

//     private void LateUpdate()
//     {
//         FollowPlayer();
//     }

//     private void HandleRotation()
//     {
//         Vector2 lookInput = Vector2.zero;

//         // ===== MOBILE LOOK =====
//         lookInput += MobileLookInput.LookDelta;

// #if UNITY_EDITOR
//         // ===== EDITOR MOUSE DRAG =====
//         if (Mouse.current != null)
//         {
//             if (Mouse.current.leftButton.wasPressedThisFrame)
//             {
//                 isDraggingMouse = true;
//                 lastMousePosition = Mouse.current.position.ReadValue();
//             }

//             if (Mouse.current.leftButton.wasReleasedThisFrame)
//             {
//                 isDraggingMouse = false;
//             }

//             if (isDraggingMouse)
//             {
//                 Vector2 currentPos = Mouse.current.position.ReadValue();
//                 Vector2 delta = currentPos - lastMousePosition;
//                 lookInput += delta;
//                 lastMousePosition = currentPos;
//             }
//         }
// #endif

//         yaw += lookInput.x * sensitivity;
//         pitch -= lookInput.y * sensitivity;
//         pitch = Mathf.Clamp(pitch, minY, maxY);

//         transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
//     }

// private void FollowPlayer()
//     {
//         if (player == null) return;

//         // Smoothly move our tracking position towards the player's actual position
//         currentFollowPosition = Vector3.SmoothDamp(currentFollowPosition, player.position, ref followVelocity, positionSmoothTime);
        
//         // Calculate the base target position (slightly above the player's feet)
//         Vector3 targetPosition = currentFollowPosition + Vector3.up * 1.5f;
        
//         // The direction the camera is looking backward from the player
//         Vector3 direction = -transform.forward;

//         // ===== WALL & OBSTACLE COLLISION =====
//         if (Physics.SphereCast(targetPosition, collisionRadius, direction, out RaycastHit hit, defaultDistance, collisionLayers))
//         {
//             // If a wall is hit, calculate how far the camera CAN go without clipping
//             targetDistance = Mathf.Clamp(hit.distance - collisionRadius, minDistance, defaultDistance);
//         }
//         else
//         {
//             // If the path is clear, return to the normal distance
//             targetDistance = defaultDistance;
//         }

//         // Smoothly transition the current distance to the new target distance
//         currentDistance = Mathf.Lerp(currentDistance, targetDistance, Time.deltaTime * zoomSmoothness);

//         // Calculate the intended position BEFORE applying it
//         Vector3 finalPosition = targetPosition + (direction * currentDistance);

//         // ===== FLOOR AVOIDANCE (GROUND CHECK) =====
//         float minHeightAboveGround = 1f; 
        
//         // Shoot a ray down from above the intended position to find the floor
//         if (Physics.Raycast(finalPosition + Vector3.up * 5f, Vector3.down, out RaycastHit groundHit, 10f, collisionLayers))
//         {
//             // If the intended position dips below our minimum height, push the Y value up
//             if (finalPosition.y < groundHit.point.y + minHeightAboveGround)
//             {
//                 finalPosition.y = groundHit.point.y + minHeightAboveGround;
//             }
//         }

//         // Apply final position once to prevent jittering
//         transform.position = finalPosition;
//     }

//     public void SetPlayerTarget(Transform newTarget)
//     {
//         player = newTarget;
//     }

// public void WarpCamera(Transform targetTransform)
//     {
//         // 1. Instantly reset the smooth follow tracking to the new position
//         currentFollowPosition = targetTransform.position;
//         followVelocity = Vector3.zero;

//         // 2. Set the yaw so the camera looks in the exact same direction the player is facing
//         yaw = targetTransform.eulerAngles.y;
        
//         // 3. Reset the pitch to a default forward-facing angle (e.g., 15 degrees)
//         // This fixes the bug where you stay looking at the sky/floor!
//         pitch = 15f; 
        
//         // 4. Apply the rotation immediately 
//         transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

//         // 5. Instantly snap the camera's physical position so it doesn't "fly"
//         Vector3 targetPosition = currentFollowPosition + Vector3.up * 1.5f;
//         Vector3 direction = -transform.forward;
//         transform.position = targetPosition + (direction * currentDistance);
//     }
// }