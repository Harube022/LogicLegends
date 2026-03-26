using UnityEngine;

public class TutorialANDGate : MonoBehaviour
{
    [Header("Plate Visuals (The Vines)")]
    [SerializeField] private Renderer vineRenderer; 
    [SerializeField] private Material offMaterial;  
    [SerializeField] private Material onMaterial;   

    [Header("Outputs (Door or Bulb)")]
    [SerializeField] private GameObject outputOffVisual;
    [SerializeField] private GameObject outputOnVisual;

    // ---> NEW: Explicitly link the manager! <---
    [Header("Tutorial Link")]
    [SerializeField] private PuzzleTutorialManager myTutorialManager;

    private int objectsOnPlate = 0; 

    private void Start()
    {
        UpdateVisuals();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.attachedRigidbody != null)
        {   
            objectsOnPlate++;
            UpdateVisuals();

            // ---> FIXED: Talk ONLY to our assigned manager! <---
            if (myTutorialManager != null)
            {
                myTutorialManager.AdvanceTutorial(this.transform); 
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.attachedRigidbody != null)
        {
            objectsOnPlate--;
            if (objectsOnPlate < 0) objectsOnPlate = 0; 
            UpdateVisuals(); 
        }
    }

    private void UpdateVisuals()
    {
        bool isPressed = objectsOnPlate > 0;

        if (vineRenderer != null) vineRenderer.material = isPressed ? onMaterial : offMaterial;
        
        if (outputOnVisual != null) outputOnVisual.SetActive(isPressed);
        if (outputOffVisual != null) outputOffVisual.SetActive(!isPressed);
    }
}