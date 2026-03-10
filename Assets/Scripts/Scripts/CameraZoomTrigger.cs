using UnityEngine;
using Unity.Cinemachine; // 1. Updated namespace for Cinemachine 3

public class CameraZoomTrigger : MonoBehaviour
{
    [Header("Assign your cameras here")]
    public CinemachineCamera mainCamera; // 2. Updated component name
    public CinemachineCamera zoomCamera;

    [Header("Priority Settings")]
    public int activePriority = 15;
    public int inactivePriority = -1;

    private void OnTriggerEnter(Collider other)
    {
        // Check if the object entering the trigger is the Player
        if (other.CompareTag("Player"))
        {
            // 3. In CM3, we must access the Priority's 'Value' property
            zoomCamera.Priority.Value = activePriority;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // When the player leaves the collider area
        if (other.CompareTag("Player"))
        {
            zoomCamera.Priority.Value = inactivePriority;
        }
    }
}