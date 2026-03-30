using UnityEngine;
using UnityEngine.UI; // ---> NEW: Required to change UI Images! <---

public class ObjectivesToggle : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject objectivesParchment; 

    [Header("Button Visuals")]
    [Tooltip("Drag the Button's Image component here")]
    [SerializeField] private Image buttonImage; 
    
    [Tooltip("The sprite to show when the panel is ON")]
    [SerializeField] private Sprite toggleOnSprite;
    
    [Tooltip("The sprite to show when the panel is OFF")]
    [SerializeField] private Sprite toggleOffSprite;

    // We assume it starts visible (true)
    private bool isVisible = true; 

    private void Start()
    {
        // Make sure the button shows the correct sprite the moment the game starts!
        UpdateButtonVisual();
    }

    public void ToggleVisibility()
    {
        isVisible = !isVisible;
        
        if (objectivesParchment != null)
        {
            objectivesParchment.SetActive(isVisible);
        }

        // ---> NEW: Update the image after flipping the switch <---
        UpdateButtonVisual();
    }

    private void UpdateButtonVisual()
    {
        if (buttonImage != null)
        {
            // If visible is true, use OnSprite. If false, use OffSprite.
            buttonImage.sprite = isVisible ? toggleOnSprite : toggleOffSprite;
        }
    }
}