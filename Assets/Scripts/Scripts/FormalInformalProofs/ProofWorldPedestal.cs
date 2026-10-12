using TMPro;
using UnityEngine;

/// <summary>A lightweight, reusable proof tablet/slot with the four visual states.</summary>
[DisallowMultipleComponent]
public sealed class ProofWorldPedestal : MonoBehaviour
{
    public enum VisualState { Normal, Selected, Correct, Incorrect }

    [SerializeField] private Renderer[] stateSurfaces = new Renderer[0];
    [SerializeField] private TMP_Text heading;
    [SerializeField] private TMP_Text detail;
    [SerializeField] private Material normalMaterial;
    [SerializeField] private Material selectedMaterial;
    [SerializeField] private Material correctMaterial;
    [SerializeField] private Material incorrectMaterial;

    public string Heading => heading != null ? heading.text : name;

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
}
