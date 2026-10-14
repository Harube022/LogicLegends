using UnityEngine;
using UnityEngine.UI;

/// <summary>Keeps the achievement list inside its panel when the canvas or safe area changes size.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class MainMenuAchievementsLayout : MonoBehaviour
{
    [SerializeField] private RectTransform viewport;
    [SerializeField] private ScrollRect scroll;
    [Header("Card Spacing")]
    [SerializeField, Min(1)] private int visibleCards = 4;
    [SerializeField, Min(0)] private float cardSpacing = 12;
    [SerializeField, Min(0)] private int verticalPadding = 10;
    [Header("Claim Buttons")]
    [SerializeField] private Vector2 maximumButtonSize = new Vector2(220,64);

    private Vector2 lastSize = new Vector2(-1,-1);
    private bool needsLayout = true;
    private bool applying;

    private void OnEnable() => needsLayout = true;
    private void OnValidate() => needsLayout = true;
    private void OnRectTransformDimensionsChange() => needsLayout = true;

    private void LateUpdate()
    {
        if (viewport != null && (needsLayout || lastSize != viewport.rect.size)) RefreshLayout();
    }

    public void RefreshLayout()
    {
        if (applying || viewport == null || scroll == null || scroll.content == null || viewport.rect.height <= 0) return;
        applying = true;
        try
        {
            float position = scroll.verticalNormalizedPosition;
            if (float.IsNaN(position) || float.IsInfinity(position)) position = 1;
            var layout = scroll.content.GetComponent<VerticalLayoutGroup>();
            if (layout == null) return;
            layout.spacing = cardSpacing;
            layout.padding = new RectOffset(4,4,verticalPadding,verticalPadding);
            int count = Mathf.Max(1,visibleCards);
            float rowHeight = Mathf.Max(1,(viewport.rect.height - verticalPadding * 2 - cardSpacing * (count-1)) / count);
            foreach (Transform row in scroll.content)
            {
                var element = row.GetComponent<LayoutElement>();
                if (element != null) element.minHeight = element.preferredHeight = rowHeight;
                var states = row.Find("ClaimStates");
                if (states == null) continue;
                // Child rect widths may still be stale during the first layout after a view change.
                // Derive the column width from the viewport so buttons cannot collapse to one pixel.
                var statesRect = (RectTransform)states;
                float columnWidth = Mathf.Max(1,viewport.rect.width-layout.padding.horizontal) *
                    (statesRect.anchorMax.x-statesRect.anchorMin.x);
                foreach (Transform state in states)
                {
                    var button = state.Find("Button") as RectTransform;
                    if (button == null) continue;
                    button.sizeDelta = new Vector2(Mathf.Max(1,Mathf.Min(maximumButtonSize.x,columnWidth-32)),
                        Mathf.Max(1,Mathf.Min(maximumButtonSize.y,rowHeight*.48f)));
                }
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = Mathf.Clamp01(position);
            lastSize = viewport.rect.size;
            needsLayout = false;
        }
        finally { applying = false; }
    }
}
