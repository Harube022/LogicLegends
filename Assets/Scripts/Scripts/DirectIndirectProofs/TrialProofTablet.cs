using UnityEngine;

/// <summary>Runtime state for one reusable physical proof tablet.</summary>
[DisallowMultipleComponent]
public sealed class TrialProofTablet : MonoBehaviour
{
    [SerializeField] private int tabletId = -1;
    [SerializeField] private string statement;
    [SerializeField] private string justification;
    [SerializeField] private Vector3 originalPosition;
    [SerializeField] private int assignedSlot = -1;
    [SerializeField] private bool selected;
    [SerializeField] private bool interactionEnabled;

    public int TabletId => tabletId;
    public string Statement => statement;
    public string Justification => justification;
    public Vector3 OriginalPosition => originalPosition;
    public int AssignedSlot => assignedSlot;
    public bool Selected => selected;
    public bool InteractionEnabled => interactionEnabled;

    public void Configure(int id, TrialProofTabletData data)
    {
        tabletId = id;
        statement = data.statement;
        justification = data.justification;
        originalPosition = transform.position;
        assignedSlot = -1;
        selected = false;
        interactionEnabled = true;
    }

    public void SetPlacement(int slot) { assignedSlot = slot; selected = false; }
    public void SetSelected(bool value) { selected = value; }
    public void SetInteractionEnabled(bool value) { interactionEnabled = value; }

    public void MoveRack(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
        originalPosition = position;
    }
}
