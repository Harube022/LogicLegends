using UnityEngine;

public class PressurePlate : MonoBehaviour
{
    [Header("Vine Visuals")]
    [Tooltip("Drag the Vine object here")]
    [SerializeField] private Renderer vineRenderer; 
    
    [Tooltip("The normal, unlit material")]
    [SerializeField] private Material offMaterial;  
    
    [Tooltip("The glowing, lit up material")]
    [SerializeField] private Material onMaterial;   

    // This keeps track of how many things are on the plate
    // (useful so the plate doesn't turn off if the player and boulder are BOTH on it, and the player steps off)
    private int objectsOnPlate = 0; 
    private bool isLocked = false; // ---> NEW: Keeps track of if the puzzle is solved
    public bool isPressed = false;

    private void OnTriggerEnter(Collider other)
    {
        // If the puzzle is already solved, don't do anything else!
        if (isLocked) return;

        if (other.CompareTag("Player") || other.attachedRigidbody != null)
        {   
            objectsOnPlate++;
            UpdateVineVisuals();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // If the puzzle is already solved, don't do anything else!
        if (isLocked) return;

        if (other.CompareTag("Player") || other.attachedRigidbody != null)
        {
            objectsOnPlate--;
            
            // Prevent the counter from going below zero just in case
            if (objectsOnPlate < 0) objectsOnPlate = 0; 
            
            UpdateVineVisuals();
        }
    }

    private void UpdateVineVisuals()
    {
        // If 1 or more valid objects are on the plate, light it up!
        if (objectsOnPlate > 0)
        {
            vineRenderer.material = onMaterial;
            isPressed = true;
        }
        else // Otherwise, turn it off
        {
            vineRenderer.material = offMaterial;
            isPressed = false;
        }
    }

    // ---> NEW: The GateController will call this when both plates are pressed
    public void LockPlateOn()
    {
        isLocked = true;
        isPressed = true;
        vineRenderer.material = onMaterial; // Force it to stay yellow
    }
}