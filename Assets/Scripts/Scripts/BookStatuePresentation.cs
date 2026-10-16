using TMPro;
using UnityEngine;

/// <summary>Shared book visuals. The stage owns interaction eligibility; this never starts or grades gameplay.</summary>
[DisallowMultipleComponent]
public sealed class BookStatuePresentation : MonoBehaviour
{
    [SerializeField, Min(.1f)] float labelClearance = .65f;
    [SerializeField, Min(0f)] float bobHeight = .16f;
    TMP_Text[] labels;
    TextMeshPro indicator;
    Transform indicatorAnchor;
    Vector3 anchorLocalPosition;
    TMP_Text interactionLabel;
    bool required;

    public bool InteractionRequired => required;
    public TextMeshPro Indicator => indicator;
    public Transform IndicatorAnchor => indicatorAnchor;

    public static BookStatuePresentation For(Transform book)
    {
        if (book == null) return null;
        var visual = book.GetComponent<BookStatuePresentation>() ??
            book.GetComponentInChildren<BookStatuePresentation>(true);
        return visual != null ? visual : book.gameObject.AddComponent<BookStatuePresentation>();
    }

    public static void SetRequired(Transform book, bool value, TMP_Text label = null)
    {
        if (book == null) return;
        var visual = book.GetComponent<BookStatuePresentation>() ?? book.GetComponentInChildren<BookStatuePresentation>(true);
        if (visual == null && value) visual = For(book);
        if (visual != null) { visual.interactionLabel = label; visual.SetRequired(value); }
    }

    public void SetRequired(bool value)
    {
        required = value;
        if (value && indicator == null) CreateIndicator();
        if (indicator != null) indicator.gameObject.SetActive(value && isActiveAndEnabled);
    }

    void Awake()
    {
        // Only static book geometry defines the anchor. Skinned page bounds change
        // dramatically while the turning page is folded away and must never position UI.
        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        bool found = false;
        foreach (var mesh in GetComponentsInChildren<MeshFilter>(true))
        {
            if (mesh.sharedMesh == null || mesh.GetComponent<MeshRenderer>() == null ||
                mesh.GetComponent<TMP_Text>() != null || mesh.GetComponent<TMP_SubMesh>() != null) continue;
            Bounds local = mesh.sharedMesh.bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = mesh.transform.TransformPoint(local.center + Vector3.Scale(local.extents,
                    new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                else bounds.Encapsulate(point);
            }
        }
        // Reserve space above the full page-turn arc, even when the page is upright.
        Vector3 anchor = new Vector3(bounds.center.x, bounds.max.y + labelClearance + bounds.size.y * .15f, bounds.center.z);
        anchorLocalPosition = transform.InverseTransformPoint(anchor);
        labels = GetComponentsInChildren<TMP_Text>(true);
    }

    void CreateIndicator()
    {
        indicatorAnchor = new GameObject("Book Interaction Anchor").transform;
        indicatorAnchor.SetParent(transform, false);
        indicatorAnchor.localPosition = anchorLocalPosition;
        var go = new GameObject("Book Interaction Indicator", typeof(TextMeshPro));
        go.transform.SetParent(indicatorAnchor, false);
        indicator = go.GetComponent<TextMeshPro>();
        indicator.text = "!";
        indicator.font = StageUITheme.HeadingFont;
        indicator.fontSize = 10;
        indicator.fontStyle = FontStyles.Bold;
        indicator.alignment = TextAlignmentOptions.Bottom;
        indicator.textWrappingMode = TextWrappingModes.NoWrap;
        indicator.color = StageUITheme.Gold;
        indicator.outlineColor = Color.black;
        indicator.outlineWidth = .25f;
        indicator.rectTransform.sizeDelta = new Vector2(3, 3);
        indicator.rectTransform.pivot = new Vector2(.5f, 0);
        indicator.raycastTarget = false;
        // The anchor belongs to the nonanimated statue root, never the page rig.
        // Cancel the imported FBX scale and use twice the previous .55 world scale.
        Vector3 parentScale = indicatorAnchor.lossyScale;
        go.transform.localScale = new Vector3(1.1f / Mathf.Abs(parentScale.x),
            1.1f / Mathf.Abs(parentScale.y), 1.1f / Mathf.Abs(parentScale.z));
        indicator.ForceMeshUpdate();
        LateUpdate();
    }

    void LateUpdate()
    {
        if (!required || indicator == null || !isActiveAndEnabled) return;
        Vector3 position = indicatorAnchor.position;
        float top = position.y;
        foreach (var label in labels)
            if (label != null && label.gameObject.activeInHierarchy && label.GetComponent<Renderer>() is Renderer renderer)
                top = Mathf.Max(top, renderer.bounds.max.y + labelClearance);
        if (interactionLabel != null && interactionLabel.gameObject.activeInHierarchy &&
            interactionLabel.GetComponent<Renderer>() is Renderer promptRenderer)
            top = Mathf.Max(top, promptRenderer.bounds.max.y + labelClearance);
        indicator.transform.position = new Vector3(position.x,
            top + bobHeight * (1f + Mathf.Sin(Time.unscaledTime * 2f)), position.z);
        if (Camera.main != null) indicator.transform.rotation = Camera.main.transform.rotation;
    }

    void OnEnable() { if (indicator != null) indicator.gameObject.SetActive(required); }
    void OnDisable() { if (indicator != null) indicator.gameObject.SetActive(false); }
    void OnDestroy() { if (indicatorAnchor != null) Destroy(indicatorAnchor.gameObject); }
}
