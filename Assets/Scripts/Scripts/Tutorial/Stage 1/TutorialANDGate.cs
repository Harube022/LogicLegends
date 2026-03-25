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

            // ---> UPDATED: Spam the manager! The manager will ignore it if it's the wrong step.
            PuzzleTutorialManager tutorialManager = FindFirstObjectByType<PuzzleTutorialManager>();
            if (tutorialManager != null)
            {
                tutorialManager.AdvanceTutorial(this.transform); // Pass this specific plate!
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