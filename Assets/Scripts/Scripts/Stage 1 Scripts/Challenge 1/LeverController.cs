using UnityEngine;

public class LeverController : MonoBehaviour
{
    [Header("Lever Objects")]
    [SerializeField] private GameObject leverOffObj;
    [SerializeField] private GameObject leverOnObj;

    [Header("Cave Objects")]
    [SerializeField] private GameObject caveClosedObj;
    [SerializeField] private GameObject caveOpenObj;

    [Header("Vine Visuals")]
    [SerializeField] private Renderer vineRenderer;
    [SerializeField] private Material vineOffMaterial;
    [SerializeField] private Material vineOnMaterial;

    [Header("UI Elements")]
    [Tooltip("The red 0 GameObject")]
    [SerializeField] private GameObject uiRedZero;
    [Tooltip("The green 1 GameObject")]
    [SerializeField] private GameObject uiGreenOne;

    private bool isOn = false;

    private void Start()
    {
        // Ensure everything is in the correct starting state
        UpdateVisuals();
    }

    // Your player script will call this method!
    public void ToggleLever()
    {
        isOn = !isOn;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        // Swap Lever GameObjects
        leverOffObj.SetActive(!isOn);
        leverOnObj.SetActive(isOn);

        // Swap Cave GameObjects
        caveClosedObj.SetActive(!isOn);
        caveOpenObj.SetActive(isOn);

        // Update UI (Checking for null just in case you haven't assigned them yet)
        if (uiRedZero != null) uiRedZero.SetActive(!isOn);
        if (uiGreenOne != null) uiGreenOne.SetActive(isOn);

        // Update Vine Material
        if (vineRenderer != null)
        {
            vineRenderer.material = isOn ? vineOnMaterial : vineOffMaterial;
        }
    }

    public void ResetLever()
    {
        isOn = false;
        UpdateVisuals();
    }
}