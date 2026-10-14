using TMPro;
using UnityEngine;

// The physical model is reusable; this value is the element represented by it.
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public sealed class SetssssNumberedBlock : MonoBehaviour
{
    [SerializeField, Range(1, 99)] private int value = 1;
    [SerializeField] private TMP_Text numberLabel;
    [SerializeField, Min(0f)] private float labelClearance = .3f;
    [SerializeField, Min(10f)] private float minimumLabelPixels = 18f;
    [SerializeField, Min(18f)] private float maximumLabelPixels = 36f;
    // Serialized caches are copied with spawner replacements. A clone must not
    // treat a red/green tint or a distance-expanded scale as its default style.
    [SerializeField, HideInInspector] private bool appearanceCaptured;
    [SerializeField, HideInInspector] private Color defaultLabelColor;
    [SerializeField, HideInInspector] private Vector3 baseLabelScale;
    private Renderer blockRenderer;
    private Collider blockCollider;
    private Rigidbody body;
    private Vector3 gradedPosition;
    private Quaternion gradedRotation;
    private bool hasFeedback, correctPlacement;
    private Bounds textBounds;

    public int Value => value;
    public TMP_Text NumberLabel => numberLabel;
    public Color DefaultLabelColor => defaultLabelColor;
    public bool? PlacementCorrectness => hasFeedback ? correctPlacement : (bool?)null;
    public static readonly Color CorrectColor = new Color(.3f, 1f, .45f);
    public static readonly Color IncorrectColor = new Color(1f, .24f, .24f);

    public Bounds BlockBounds => blockRenderer != null ? blockRenderer.bounds :
        blockCollider != null && blockCollider.enabled ? blockCollider.bounds : new Bounds(transform.position, Vector3.one);

    public void SetValue(int newValue)
    {
        value = Mathf.Clamp(newValue, 1, 99);
        RefreshLabel();
        ResetPlacementFeedback();
    }

    private void Awake()
    {
        blockRenderer = GetComponent<Renderer>();
        blockCollider = GetComponent<Collider>();
        body = GetComponent<Rigidbody>();
        if (numberLabel == null) numberLabel = GetComponentInChildren<TMP_Text>(true);
        if (numberLabel != null && !appearanceCaptured)
        {
            defaultLabelColor = numberLabel.color;
            baseLabelScale = numberLabel.transform.localScale;
            appearanceCaptured = true;
        }
        RefreshLabel();
        ResetPlacementFeedback();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RefreshLabel();
    }
#endif

    private void RefreshLabel()
    {
        if (numberLabel == null) numberLabel = GetComponentInChildren<TMP_Text>(true);
        if (numberLabel == null) return;
        numberLabel.text = value.ToString();
        numberLabel.enableAutoSizing = true;
        numberLabel.fontSizeMin = 40f;
        numberLabel.fontSizeMax = 54f;
        if (Application.isPlaying)
        {
            // Enough room for a two-digit number at a readable size.
            numberLabel.rectTransform.sizeDelta = new Vector2(6f, 5f);
            numberLabel.alignment = TextAlignmentOptions.Center;
            numberLabel.ForceMeshUpdate(true);
            textBounds = numberLabel.mesh != null ? numberLabel.mesh.bounds : numberLabel.textBounds;
        }
    }

    public void SetPlacementFeedback(bool correct)
    {
        hasFeedback = true;
        correctPlacement = correct;
        gradedPosition = body != null ? body.position : transform.position;
        gradedRotation = body != null ? body.rotation : transform.rotation;
        if (numberLabel != null) numberLabel.color = correct ? CorrectColor : IncorrectColor;
    }

    public void ResetPlacementFeedback()
    {
        hasFeedback = false;
        if (numberLabel != null && appearanceCaptured) numberLabel.color = defaultLabelColor;
    }

    private void LateUpdate()
    {
        if (numberLabel == null) return;
        Vector3 physicalPosition = body != null ? body.position : transform.position;
        Quaternion physicalRotation = body != null ? body.rotation : transform.rotation;
        if (hasFeedback && ((physicalPosition - gradedPosition).sqrMagnitude > .0016f ||
            Quaternion.Angle(physicalRotation, gradedRotation) > 1f)) ResetPlacementFeedback();
        Camera camera = Camera.main;
        if (camera == null) return;
        Bounds bounds = BlockBounds;
        Vector3 parentScale = numberLabel.transform.parent != null ? numberLabel.transform.parent.lossyScale : Vector3.one;
        Vector3 worldScale = Vector3.Scale(baseLabelScale, Abs(parentScale));
        float baseHeight = Mathf.Max(.01f, textBounds.size.y * worldScale.y);
        float depth = Vector3.Dot(bounds.center - camera.transform.position, camera.transform.forward);
        float worldPerPixel = camera.orthographic ? 2f * camera.orthographicSize / camera.pixelHeight :
            2f * Mathf.Max(.1f, depth) * Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad) / Mathf.Max(1, camera.pixelHeight);
        float pixelScale = Mathf.Max(1f, camera.pixelHeight / 480f);
        float wantedPixels = Mathf.Clamp(baseHeight / worldPerPixel, minimumLabelPixels * pixelScale,
            Mathf.Max(minimumLabelPixels, maximumLabelPixels) * pixelScale);
        float multiplier = Mathf.Clamp(wantedPixels * worldPerPixel / baseHeight, .1f, 3f);
        worldScale *= multiplier;
        numberLabel.transform.localScale = baseLabelScale * multiplier;
        Quaternion rotation = camera.transform.rotation;
        // The text remains a child so spawner cloning/lifetime works normally,
        // but its rendered world pose is independent of the block's rotation.
        Vector3 extents = Vector3.Scale(textBounds.extents, worldScale);
        float verticalExtent = Mathf.Abs(camera.transform.right.y) * extents.x +
            Mathf.Abs(camera.transform.up.y) * extents.y;
        Vector3 center = new Vector3(bounds.center.x, bounds.max.y + labelClearance + verticalExtent, bounds.center.z);
        if (depth > camera.nearClipPlane)
        {
            // Keep numbers clear of the projected block in steep camera views.
            float top = float.NegativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                top = Mathf.Max(top, camera.WorldToScreenPoint(corner).y);
            }
            Vector3 projected = camera.WorldToScreenPoint(center);
            float labelDepth = Vector3.Dot(center - camera.transform.position, camera.transform.forward);
            float labelWorldPerPixel = camera.orthographic ? worldPerPixel :
                worldPerPixel * Mathf.Max(.1f, labelDepth) / Mathf.Max(.1f, depth);
            float bottom = projected.y - extents.y / labelWorldPerPixel;
            float liftPixels = Mathf.Max(0f, top + 4f * pixelScale - bottom);
            center += camera.transform.up * (liftPixels * labelWorldPerPixel);
        }
        Vector3 origin = center - rotation * Vector3.Scale(textBounds.center, worldScale);
        numberLabel.transform.SetPositionAndRotation(origin, rotation);
    }

    private static Vector3 Abs(Vector3 scale) => new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));

    private void OnDisable()
    {
        ResetPlacementFeedback();
    }
}
