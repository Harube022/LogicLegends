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

    private Collider interactionArea;
    private Player trackedPlayer;
    private bool playerInside;

    private void Awake()
    {
        interactionArea = GetComponent<Collider>();
    }

    private void Update()
    {
        // Trigger exit is not sent when the player is teleported or its
        // CharacterController is temporarily disabled. Keep the existing zone
        // authoritative for both the camera and the countdown pause.
        if (interactionArea == null || !interactionArea.enabled) return;
        if (trackedPlayer == null) trackedPlayer = Player.LocalInstance;
        if (trackedPlayer == null) return;

        Vector3 center = trackedPlayer.transform.position + Vector3.up * 1.5f;
        CharacterController controller = trackedPlayer.GetComponent<CharacterController>();
        float margin = controller != null ? controller.radius : 0.4f;
        bool inside = (interactionArea.ClosestPoint(center) - center).sqrMagnitude <= margin * margin;
        SetPlayerInside(inside);
    }

    private void OnTriggerEnter(Collider other)
    {
        // When player walks into the interaction zone
        if (other.CompareTag(playerTag))
        {
            SetPlayerInside(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // When player steps away from the truth table
        if (other.CompareTag(playerTag))
        {
            SetPlayerInside(false);
        }
    }

    private void OnDisable()
    {
        SetPlayerInside(false);
    }

    private void SetPlayerInside(bool inside)
    {
        if (playerInside == inside) return;
        playerInside = inside;
        if (focusVirtualCamera != null) focusVirtualCamera.SetActive(inside);
        if (dynamicLogicPuzzle != null) dynamicLogicPuzzle.SetPlayerProximity(inside);
    }
}
