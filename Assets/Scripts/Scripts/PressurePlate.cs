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

    private void OnTriggerEnter(Collider other)
    {
        // Check if the object stepping on the plate is the Player OR the Boulder
        // (Assuming your boulder has a Rigidbody, it will trigger this too)
        if (other.CompareTag("Player") || other.attachedRigidbody != null)
        {
            objectsOnPlate++;
            UpdateVineVisuals();
        }
    }

    private void OnTriggerExit(Collider other)
    {
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
        }
        else // Otherwise, turn it off
        {
            vineRenderer.material = offMaterial;
        }
    }
}