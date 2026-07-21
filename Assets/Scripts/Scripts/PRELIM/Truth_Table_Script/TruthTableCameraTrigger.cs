using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TruthTableCameraTrigger : MonoBehaviour
{
    [Header("Camera Reference")]
    [SerializeField] private GameObject focusVirtualCamera; // Drag VCam_TruthTable here

    [Header("Puzzle Script Reference")]
    [SerializeField] private DynamicLogicPuzzle dynamicLogicPuzzle;

    [Header("Player Filter")]
    [SerializeField] private string playerTag = "Player";

    private void OnTriggerEnter(Collider other)
    {
        // When player walks into the interaction zone
        if (other.CompareTag(playerTag))
        {
            if (focusVirtualCamera != null)
            {
                focusVirtualCamera.SetActive(true); // Cinemachine smoothly blends to focus camera
            }
            if (dynamicLogicPuzzle != null)
            {
                dynamicLogicPuzzle.SetPlayerProximity(true); // Shows indicator on proximity
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // When player steps away from the truth table
        if (other.CompareTag(playerTag))
        {
            if (focusVirtualCamera != null)
            {
                focusVirtualCamera.SetActive(false); // Cinemachine smoothly blends back to player camera
            }
            if (dynamicLogicPuzzle != null)
            {
                dynamicLogicPuzzle.SetPlayerProximity(false); // Hides indicator when walking away
            }
        }
    }
}