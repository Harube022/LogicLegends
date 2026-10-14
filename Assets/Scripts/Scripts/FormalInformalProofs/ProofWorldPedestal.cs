using TMPro;
using UnityEngine;

/// <summary>A lightweight, reusable proof tablet/slot with the four visual states.</summary>
[DisallowMultipleComponent]
public sealed class ProofWorldPedestal : MonoBehaviour
{
    public enum VisualState { Normal, Selected, Correct, Incorrect }

    private static readonly string[] TabletVisualParts =
    {
        "Tablet Frame", "Reading Tablet", "Tablet Heading", "Tablet Detail",
        "Upper Scroll Roller", "Upper Left Scroll Cap", "Upper Right Scroll Cap",
        "Lower Scroll Roller", "Lower Left Scroll Cap", "Lower Right Scroll Cap"
    };

    [SerializeField] private Renderer[] stateSurfaces = new Renderer[0];
    [SerializeField] private TMP_Text heading;
    [SerializeField] private TMP_Text detail;
    [SerializeField] private Material normalMaterial;
    [SerializeField] private Material selectedMaterial;
    [SerializeField] private Material correctMaterial;
    [SerializeField] private Material incorrectMaterial;

    public string Heading => heading != null ? heading.text : name;
    public bool TabletVisible
    {
        get
        {
            Transform tablet = transform.Find("Reading Tablet");
            return tablet == null || tablet.gameObject.activeSelf;
        }
    }

    public void SetContent(string title, string description)
    {
        if (heading != null) heading.text = title;
        if (detail != null) detail.text = description;
    }

    public void SetState(VisualState state)
    {
        Material material = state == VisualState.Selected ? selectedMaterial :
            state == VisualState.Correct ? correctMaterial :
            state == VisualState.Incorrect ? incorrectMaterial : normalMaterial;
        if (material == null) return;
        foreach (Renderer surface in stateSurfaces)
            if (surface != null) surface.sharedMaterial = material;
    }

    public void SetTabletVisible(bool visible)
    {
        foreach (string partName in TabletVisualParts)
        {
            Transform part = transform.Find(partName);
            if (part != null) part.gameObject.SetActive(visible);
        }
    }
}
