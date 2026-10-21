using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Visible native scroll controls for every Open Archive reading pane and item list.</summary>
    public static class ArchiveReadingScrollbars
    {
        const float Gutter = 30;

        public static void Install(Transform root)
        {
            foreach (var scroll in root.GetComponentsInChildren<ScrollRect>(true))
            {
                if (!scroll.vertical || scroll.content == null || scroll.verticalScrollbar != null) continue;
                var host = (RectTransform)scroll.transform;
                var viewport = scroll.viewport == null ? host : scroll.viewport;
                if (viewport == host)
                {
                    // Keep the existing ScrollRect, hit surface, content, and callbacks.
                    // Only the clipping area becomes narrower, leaving a dedicated rail.
                    viewport = Rect("Reading_Viewport", host);
                    viewport.offsetMax = new Vector2(-Gutter, 0);
                    viewport.gameObject.AddComponent<RectMask2D>();
                    scroll.content.SetParent(viewport, false);
                    scroll.viewport = viewport;
                }
                else viewport.offsetMax -= new Vector2(Gutter, 0);

                var rail = Rect("Vertical_Reading_Scrollbar", host);
                rail.anchorMin = new Vector2(1, 0); rail.anchorMax = Vector2.one;
                rail.offsetMin = new Vector2(-24, 6); rail.offsetMax = new Vector2(-2, -6);
                var track = rail.gameObject.AddComponent<Image>();
                track.color = new Color(.30f, .19f, .09f, .35f);
                var gold = Rect("Gold_Track", rail);
                gold.offsetMin = new Vector2(8, 3); gold.offsetMax = new Vector2(-8, -3);
                var line = gold.gameObject.AddComponent<Image>();
                line.color = new Color(.76f, .52f, .22f, .55f); line.raycastTarget = false;
                var sliding = Rect("Sliding_Area", rail);
                sliding.offsetMin = new Vector2(3, 3); sliding.offsetMax = new Vector2(-3, -3);
                var handle = Rect("Drag_To_Scroll", sliding);
                var thumb = handle.gameObject.AddComponent<Image>(); thumb.color = new Color(.08f, .36f, .35f);
                for (int i = 0; i < 3; i++)
                {
                    var grip = Rect("Grip_" + i, handle);
                    grip.anchorMin = grip.anchorMax = new Vector2(.5f, .5f);
                    grip.sizeDelta = new Vector2(8, 2); grip.anchoredPosition = new Vector2(0, (i - 1) * 5);
                    var mark = grip.gameObject.AddComponent<Image>(); mark.color = new Color(1, .90f, .64f); mark.raycastTarget = false;
                }
                var bar = rail.gameObject.AddComponent<Scrollbar>();
                bar.direction = Scrollbar.Direction.BottomToTop; bar.handleRect = handle; bar.targetGraphic = thumb;
                bar.navigation = new Navigation { mode = Navigation.Mode.None };
                var colors = bar.colors;
                colors.highlightedColor = new Color(.80f, 1, .95f);
                colors.pressedColor = new Color(1, .88f, .55f); bar.colors = colors;
                scroll.verticalScrollbar = bar;
                scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
                // A permanent rail shows that this is a reading area; the native
                // thumb fills the rail when all content fits and shrinks as it grows.
                bar.value = scroll.verticalNormalizedPosition;
            }
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
