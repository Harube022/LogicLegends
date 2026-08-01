using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

[RequireComponent(typeof(CinemachineCamera))]
public class CinemachinePinchZoom : MonoBehaviour
{
    [Header("Zoom Settings")]
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 10f;
    [SerializeField] private float zoomSpeed = 0.01f;

    private CinemachineThirdPersonFollow thirdPersonFollow;

    private void Awake()
    {
        thirdPersonFollow = GetComponent<CinemachineThirdPersonFollow>();
    }

    private void Update()
    {
        HandlePinchZoom();
    }

    private void HandlePinchZoom()
    {
        if (thirdPersonFollow == null || Touchscreen.current == null) return;

        UnityEngine.InputSystem.Controls.TouchControl touch0 = null;
        UnityEngine.InputSystem.Controls.TouchControl touch1 = null;

        // Find active touches on the right half of the screen
        foreach (var touch in Touchscreen.current.touches)
        {
            if (touch.isInProgress && touch.position.ReadValue().x > Screen.width / 2f)
            {
                if (touch0 == null) touch0 = touch;
                else if (touch1 == null) touch1 = touch;
            }
        }

        // Apply pinch zoom if two touches are detected
        if (touch0 != null && touch1 != null)
        {
            Vector2 touch0Pos = touch0.position.ReadValue();
            Vector2 touch1Pos = touch1.position.ReadValue();

            Vector2 touch0PrevPos = touch0Pos - touch0.delta.ReadValue();
            Vector2 touch1PrevPos = touch1Pos - touch1.delta.ReadValue();

            float prevMagnitude = (touch0PrevPos - touch1PrevPos).magnitude;
            float currentMagnitude = (touch0Pos - touch1Pos).magnitude;

            float deltaMagnitudeDiff = currentMagnitude - prevMagnitude;

            // Update Cinemachine Camera Distance
            thirdPersonFollow.CameraDistance -= deltaMagnitudeDiff * zoomSpeed;
            thirdPersonFollow.CameraDistance = Mathf.Clamp(thirdPersonFollow.CameraDistance, minDistance, maxDistance);
        }
    }
}