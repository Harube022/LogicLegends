using UnityEngine;

public class CameraTargetController : MonoBehaviour
{
    [Header("Look Settings")]
    [SerializeField] private float sensitivity = 0.15f;
    [SerializeField] private float topClamp = 60.0f;//89.0f;
    [SerializeField] private float bottomClamp = -30.0f;//-89.0f;

    private float cameraPitch = 0f;
    private float cameraYaw = 0f;

    private void Start()
    {
        Vector3 currentRot = transform.eulerAngles;
        cameraYaw = currentRot.y;
        cameraPitch = currentRot.x;
    }

    private void LateUpdate()
    {
        Vector2 lookDelta = MobileLookInput.LookDelta;

        cameraYaw += lookDelta.x * sensitivity;
        cameraPitch -= lookDelta.y * sensitivity;
        cameraPitch = Mathf.Clamp(cameraPitch, bottomClamp, topClamp);

        // Apply rotation in world space so it isn't affected by the player body's WASD rotation
        transform.rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);

        // Clear input so camera stops moving when drag stops
        MobileLookInput.ResetDelta();
    }
}