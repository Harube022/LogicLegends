using TMPro;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared book visuals. The stage owns interaction eligibility; this never starts or grades gameplay.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(1400)]
public sealed class BookStatuePresentation : MonoBehaviour
{
    public const string HandPromptText = "Use the hand button to interact.";
    [SerializeField, Min(.1f)] float labelClearance = .65f;
    [SerializeField, Min(0f)] float bobHeight = .16f;
    TMP_Text[] labels;
    TMP_Text indicator;
    Transform indicatorAnchor;
    Vector3 anchorLocalPosition;
    TMP_Text interactionLabel;
    bool required;
    bool initialized;
    Func<bool> canUseHand;
    Action useHand;
    RectTransform bubble;
    CanvasGroup explanation;
    Image handIcon;
    float expansion;
    RectTransform[] hudObstacles;
    readonly Vector3[] corners = new Vector3[4];
    float nextHudScan, hudOffset;
    static readonly List<BookStatuePresentation> handBooks = new List<BookStatuePresentation>();

    public bool InteractionRequired => required;
    public TMP_Text Indicator => indicator;
    public Transform IndicatorAnchor => indicatorAnchor;
    public bool PlayerInRange => isActiveAndEnabled && canUseHand != null && canUseHand();
    public float Expansion => expansion;
    public RectTransform Bubble => bubble;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetHandBooks() { handBooks.Clear(); }

    public static void BindHand(Transform book, Func<bool> available, Action interact)
    {
        var visual = For(book);
        if (visual == null) return;
        visual.canUseHand = available; visual.useHand = interact;
        if (!handBooks.Contains(visual)) handBooks.Add(visual);
    }

    // Consume a book action before generic inventory pickup/drop can also handle it.
    public static bool TryHandInteraction()
    {
        if (GameInput.Instance != null && GameInput.Instance.GameplayInputBlocked) return false;
        BookStatuePresentation nearest = null; float distance = float.MaxValue;
        var player = Player.LocalInstance;
        if (player == null || !player.enabled || Time.timeScale <= 0) return false;
        foreach (var book in handBooks)
        {
            if (book == null || !book.PlayerInRange || book.useHand == null) continue;
            float candidate = (player.transform.position - book.transform.position).sqrMagnitude;
            if (candidate < distance) { nearest = book; distance = candidate; }
        }
        if (nearest == null) return false;
        nearest.useHand(); return true;
    }

    public static BookStatuePresentation For(Transform book)
    {
        if (book == null) return null;
        var visual = book.GetComponent<BookStatuePresentation>();
        if (visual == null) visual = book.GetComponentInChildren<BookStatuePresentation>(true);
        return visual != null ? visual : book.gameObject.AddComponent<BookStatuePresentation>();
    }

    public static void SetRequired(Transform book, bool value, TMP_Text label = null)
    {
        if (book == null) return;
        var visual = book.GetComponent<BookStatuePresentation>();
        if (visual == null) visual = book.GetComponentInChildren<BookStatuePresentation>(true);
        if (visual == null && value) visual = For(book);
        if (visual != null) { visual.interactionLabel = label; visual.SetRequired(value); }
    }

    public void SetRequired(bool value)
    {
        if (value && !required) expansion = 0;
        required = value;
        if (value && indicator == null && initialized) CreateIndicator();
        if (bubble != null) bubble.gameObject.SetActive(value && isActiveAndEnabled);
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
        labels = GetComponentsInChildren<TextMeshPro>(true);
        initialized = true;
        if (indicatorAnchor != null) indicatorAnchor.localPosition = anchorLocalPosition;
        if (required && indicator == null) CreateIndicator();
    }

    void CreateIndicator()
    {
        indicatorAnchor = new GameObject("Book Interaction Anchor").transform;
        indicatorAnchor.SetParent(transform, false);
        indicatorAnchor.localPosition = anchorLocalPosition;
        bool prelimMarker=false;
        for (var owner = transform; owner != null; owner = owner.parent)
            if (owner.name == "Propositional Logic" || owner.name == "Truth_Table")
            {
                prelimMarker=true;
                break;
            }
        var view = HandThoughtBubble.Create(indicatorAnchor, "Book Thought Bubble", "Book Interaction Indicator", prelimMarker);
        bubble = view.Rect; indicator = view.Marker; explanation = view.Instruction; handIcon = view.HandIcon;
        LateUpdate();
    }

    void LateUpdate()
    {
        if (interactionLabel != null) interactionLabel.gameObject.SetActive(false);
        if (!required || bubble == null || !isActiveAndEnabled) return;
        Vector3 position = indicatorAnchor.position;
        float top = position.y;
        if (labels != null) foreach (var label in labels)
        {
            if (label == null || !label.gameObject.activeInHierarchy) continue;
            var renderer = label.GetComponent<Renderer>();
            if (renderer != null) top = Mathf.Max(top, renderer.bounds.max.y + labelClearance);
        }
        bubble.position = new Vector3(position.x,
            top + bobHeight * (1f + Mathf.Sin(Time.unscaledTime * 2f)), position.z);
        var camera = Camera.main;
        if (camera != null)
        {
            bubble.rotation = camera.transform.rotation;
            // Keep the instruction readable without inheriting an FBX's import scale.
            float scale = Mathf.Clamp(Vector3.Distance(camera.transform.position,bubble.position)*.0009f,.006f,.06f);
            var parentScale = indicatorAnchor.lossyScale;
            bubble.localScale = new Vector3(scale/Mathf.Abs(parentScale.x),scale/Mathf.Abs(parentScale.y),scale/Mathf.Abs(parentScale.z));
        }
        // Opening a map does not move the player; preserve the bubble's state under it.
        if (ExpandableMinimap.OpenMap == null)
        {
            bool near = PlayerInRange && (GameInput.Instance == null || !GameInput.Instance.GameplayInputBlocked);
            expansion = Mathf.MoveTowards(expansion,near?1:0,Time.unscaledDeltaTime*3.5f);
        }
        float smooth = Mathf.SmoothStep(0,1,expansion);
        bubble.sizeDelta = new Vector2(Mathf.Lerp(105,470,smooth),110);
        indicator.alpha = 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.7f,.9f,expansion));
        explanation.alpha = Mathf.SmoothStep(0,1,Mathf.InverseLerp(.8f,1,expansion));
        if (handIcon.sprite == null)
            foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if (button.name == "InteractButton" && button.image != null) { handIcon.sprite = button.image.sprite; break; }
        if (camera != null) AvoidHud(camera);
    }

    void AvoidHud(Camera camera)
    {
        if (hudObstacles == null || Time.unscaledTime >= nextHudScan)
        {
            var list = new List<RectTransform>();
            foreach(var rect in FindObjectsByType<RectTransform>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(rect.name == "CurrentStageHud" || rect.name == "PrelimChallengeCompletionNotice" ||
                    rect.name == "TruthTableMinimapPanel" || IsMobileControl(rect)) list.Add(rect);
            hudObstacles = list.ToArray(); nextHudScan = Time.unscaledTime + 1;
        }
        var point = camera.WorldToScreenPoint(bubble.position);
        if(point.z <= 0) return;
        bubble.GetWorldCorners(corners);
        var low = camera.WorldToScreenPoint(corners[0]);
        var high = camera.WorldToScreenPoint(corners[2]);
        var bounds = Rect.MinMaxRect(low.x,low.y,high.x,high.y);
        float down = 0;
        foreach(var rect in hudObstacles)
        {
            if(rect == null || !rect.gameObject.activeInHierarchy || IsMobileControl(rect)) continue;
            rect.GetWorldCorners(corners);
            var obstacle = Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);
            if(bounds.Overlaps(obstacle)) down = Mathf.Max(down,bounds.yMax-obstacle.yMin+12);
        }
        float up = 0;
        var lowered = bounds; lowered.y -= down;
        foreach(var rect in hudObstacles)
        {
            if(rect == null || !rect.gameObject.activeInHierarchy || !IsMobileControl(rect)) continue;
            rect.GetWorldCorners(corners);
            var obstacle = Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);
            if(lowered.Overlaps(obstacle)) up = Mathf.Max(up,obstacle.yMax-lowered.yMin+12);
        }
        hudOffset = Mathf.MoveTowards(hudOffset,down-up,Time.unscaledDeltaTime*Screen.height*2);
        if(Mathf.Abs(hudOffset) > .01f) bubble.position = camera.ScreenToWorldPoint(point-Vector3.up*hudOffset);
    }
    static bool IsMobileControl(RectTransform rect) => rect.name == "JumpButton" || rect.name == "InteractButton" || rect.name == "Joystick";

    void OnEnable() { if (required && indicator == null && initialized) CreateIndicator(); if (bubble != null) bubble.gameObject.SetActive(required); }
    void OnDisable() { if (bubble != null) bubble.gameObject.SetActive(false); }
    void OnDestroy() { handBooks.Remove(this); if (indicatorAnchor != null) Destroy(indicatorAnchor.gameObject); }
}
