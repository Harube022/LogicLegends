using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SetElement : MonoBehaviour
{
    [SerializeField] private string elementValue;
    [SerializeField] private SetZone correctZone;
    [SerializeField] private TMP_Text valueLabel;

    private Rigidbody body;
    private Collider[] elementColliders;
    private Renderer[] renderers;
    private MaterialPropertyBlock propertyBlock;
    private Transform originalParent;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private SetPlacementZone reservedZone;

    public string ElementValue => elementValue;
    public SetZone CorrectZone => correctZone;
    public bool IsCarried { get; private set; }
    public bool IsPlaced { get; private set; }
    public SetPlacementZone PlacedZone => reservedZone;

    private void Awake()
    {
        CacheComponents();
        UpdateValueLabel();
    }

    private void LateUpdate()
    {
        if (valueLabel == null || Camera.main == null) return;
        Vector3 toCamera = valueLabel.transform.position - Camera.main.transform.position;
        if (toCamera.sqrMagnitude > 0.0001f)
            valueLabel.transform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
    }

    private void CacheComponents()
    {
        if (body == null) body = GetComponent<Rigidbody>();
        if (elementColliders == null || elementColliders.Length == 0)
            elementColliders = GetComponentsInChildren<Collider>(true);
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>(true);
        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
        if (valueLabel == null) valueLabel = GetComponentInChildren<TMP_Text>(true);
    }

    public void Initialize(string value, SetZone zone, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        CacheComponents();
        elementValue = value;
        correctZone = zone;
        originalParent = transform.parent;
        originalPosition = spawnPosition;
        originalRotation = spawnRotation;
        IsCarried = false;
        IsPlaced = false;
        reservedZone = null;
        if (body != null)
        {
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = true;
        }
        SetCollidersEnabled(true);
        ResetFeedbackColor();
        UpdateValueLabel();
        transform.SetPositionAndRotation(originalPosition, originalRotation);
    }

    public bool BeginCarry(Transform carryPoint)
    {
        if (IsCarried || carryPoint == null) return false;

        if (reservedZone != null)
        {
            reservedZone.ReleasePlacementPoint(this);
            reservedZone = null;
        }
        IsPlaced = false;
        ResetFeedbackColor();

        if (body != null)
        {
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = true;
        }

        IsCarried = true;
        transform.SetParent(carryPoint, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        SetCollidersEnabled(false);
        return true;
    }

    public void PlaceInZone(SetPlacementZone zone, bool tintCorrect = true)
    {
        if (zone == null || !IsCarried || IsPlaced) return;

        IsCarried = false;
        IsPlaced = true;
        reservedZone = zone;
        transform.SetParent(null, true);
        Vector3 placementPosition = zone.ReservePlacementPoint(this);
        transform.SetPositionAndRotation(placementPosition, Quaternion.identity);
        if (body != null)
        {
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = true;
        }
        SetCollidersEnabled(true);
        if (tintCorrect) SetFeedbackColor(new Color(0.25f, 0.85f, 0.35f, 1f));
        else ResetFeedbackColor();
    }

    public void ReturnToSpawn()
    {
        if (reservedZone != null)
        {
            reservedZone.ReleasePlacementPoint(this);
            reservedZone = null;
        }

        IsCarried = false;
        IsPlaced = false;
        transform.SetParent(originalParent, true);
        transform.SetPositionAndRotation(originalPosition, originalRotation);
        if (body != null)
        {
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = true;
        }
        SetCollidersEnabled(true);
        ResetFeedbackColor();
    }

    public void SetFeedbackColor(Color color)
    {
        CacheComponents();
        propertyBlock.Clear();
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    public void ResetFeedbackColor()
    {
        CacheComponents();
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null) renderer.SetPropertyBlock(null);
        }
    }

    private void SetCollidersEnabled(bool value)
    {
        CacheComponents();
        foreach (Collider elementCollider in elementColliders)
        {
            if (elementCollider != null) elementCollider.enabled = value;
        }
    }

    private void UpdateValueLabel()
    {
        if (valueLabel != null) valueLabel.text = elementValue ?? string.Empty;
    }
}
