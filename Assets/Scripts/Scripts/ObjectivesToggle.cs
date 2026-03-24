using UnityEngine;

public class ObjectivesToggle : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject objectivesParchment; // Drag your Scroll_Image here

    // We assume it starts visible (true). Change to false if you want it hidden by default.
    private bool isVisible = true; 

    // This method will be called by our new Button
    public void ToggleVisibility()
    {
        // Flip the boolean (If true, it becomes false. If false, it becomes true)
        isVisible = !isVisible;
        
        // Turn the parchment on or off based on the new boolean value
        objectivesParchment.SetActive(isVisible);
    }
}