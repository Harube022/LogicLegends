using UnityEngine;

public class FaceCamera : MonoBehaviour
{
    private Transform mainCameraTransform;

    void Start()
    {
        // Find the main camera in the scene automatically
        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }
    }

    // We use LateUpdate so it rotates AFTER the camera finishes moving for the frame
    void LateUpdate()
    {
        if (mainCameraTransform != null)
        {
            // This specific math ensures the text perfectly faces the camera 
            // without flipping backwards!
            transform.forward = mainCameraTransform.forward;
        }
    }
}