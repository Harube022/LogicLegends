using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Read-only Admin child panel. Back returns to Admin without releasing its gameplay pause.</summary>
public sealed class AdminAnswerViewer : MonoBehaviour
{
    RectTransform root, viewport, content;
    TextMeshProUGUI title, body, pageLabel;
    Button previous, next;
    ScrollRect scroll;
    Canvas host;
    IReadOnlyList<AdminAnswerPage> pages;
    int index, width, height;
    bool visible;

    public void Build(Canvas canvas, RectTransform parent, TMP_FontAsset font, Action back)
    {
        host = canvas;
        root = Rect("QuestionsAndAnswers", parent);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f);
        var background = root.gameObject.AddComponent<Image>();
        background.color = new Color(.06f, .11f, .13f, 1f);
        var outline = root.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(.72f, .58f, .28f); outline.effectDistance = new Vector2(3, -3);
        title = Text("Title", root, font, 36);
        title.alignment = TextAlignmentOptions.Center;
        Set(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -95), new Vector2(-24, -20));
        previous = Button("PreviousGroup", root, font, "Previous", () => ShowPage(index - 1));
        Set((RectTransform)previous.transform, new Vector2(0, 1), new Vector2(.27f, 1), new Vector2(24, -182), new Vector2(-8, -108));
        next = Button("NextGroup", root, font, "Next", () => ShowPage(index + 1));
        Set((RectTransform)next.transform, new Vector2(.73f, 1), new Vector2(1, 1), new Vector2(8, -182), new Vector2(-24, -108));
        pageLabel = Text("Page", root, font, 29);
        pageLabel.alignment = TextAlignmentOptions.Center;
        Set(pageLabel.rectTransform, new Vector2(.27f, 1), new Vector2(.73f, 1), new Vector2(0, -182), new Vector2(0, -108));
        RectTransform scrollRoot = Rect("QuestionScroll", root);
        Set(scrollRoot, Vector2.zero, Vector2.one, new Vector2(24, 118), new Vector2(-24, -202));
        scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 45;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        viewport = Rect("Viewport", scrollRoot);
        Set(viewport, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-30, 0));
        viewport.gameObject.AddComponent<Image>().color = new Color(.035f, .065f, .075f);
        viewport.gameObject.AddComponent<RectMask2D>();
        content = Rect("Content", viewport);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
        body = Text("Questions", content, StageUITheme.ReadingFont != null ? StageUITheme.ReadingFont : font, 32);
        body.richText = false;
        body.fontStyle = FontStyles.Normal;
        body.alignment = TextAlignmentOptions.TopLeft;
        body.textWrappingMode = TextWrappingModes.Normal;
        body.overflowMode = TextOverflowModes.Overflow;
        Set(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(22, 18), new Vector2(-22, -18));
        scroll.viewport = viewport; scroll.content = content;
        RectTransform bar = Rect("Scrollbar", scrollRoot);
        Set(bar, new Vector2(1, 0), Vector2.one, new Vector2(-24, 0), Vector2.zero);
        bar.gameObject.AddComponent<Image>().color = new Color(.16f, .23f, .24f);
        var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
        RectTransform handle = Rect("Handle", bar);
        Set(handle, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = new Color(.75f, .63f, .35f);
        scrollbar.handleRect = handle; scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = scrollbar;
        var backButton = Button("BackToAdmin", root, font, "Back", () => back());
        Set((RectTransform)backButton.transform, new Vector2(.27f, 0), new Vector2(.73f, 0), new Vector2(0, 22), new Vector2(0, 96));
        root.gameObject.SetActive(false);
    }

    public void Show(IReadOnlyList<AdminAnswerPage> snapshot)
    {
        if (!AdminAuthorization.IsAuthorized) return;
        pages = snapshot; visible = true; root.gameObject.SetActive(true); root.SetAsLastSibling();
        Layout(); ShowPage(0);
    }
    public void Hide() { visible = false; if (root != null) root.gameObject.SetActive(false); }
    void LateUpdate()
    {
        if (!AdminAuthorization.IsAuthorized) { Hide(); return; }
        if (!visible) return;
        Vector2 oldSize = root.sizeDelta;
        Layout();
        if (oldSize != root.sizeDelta) ResizeContent();
    }
    void Layout()
    {
        width = Screen.width; height = Screen.height;
        Canvas.ForceUpdateCanvases();
        Rect safe = UnityEngine.Rect.MinMaxRect(Mathf.Clamp(Screen.safeArea.xMin, 0, width), Mathf.Clamp(Screen.safeArea.yMin, 0, height),
            Mathf.Clamp(Screen.safeArea.xMax, 0, width), Mathf.Clamp(Screen.safeArea.yMax, 0, height));
        if (safe.width < 1 || safe.height < 1) safe = new Rect(0, 0, width, height);
        float scale = Mathf.Max(.001f, host.scaleFactor);
        root.sizeDelta = new Vector2(Mathf.Min(1120, safe.width / scale - 48), Mathf.Min(940, safe.height / scale - 40));
        root.anchoredPosition = (safe.center - new Vector2(width, height) * .5f) / scale;
        Canvas.ForceUpdateCanvases();
    }
    void ShowPage(int page)
    {
        if (pages == null || pages.Count == 0) return;
        index = Mathf.Clamp(page, 0, pages.Count - 1);
        title.text = pages[index].Title; body.text = pages[index].Body;
        pageLabel.text = (index + 1) + " / " + pages.Count + "\nScroll to read";
        previous.interactable = index > 0; next.interactable = index < pages.Count - 1;
        ResizeContent(); scroll.StopMovement(); scroll.verticalNormalizedPosition = 1;
    }
    void ResizeContent()
    {
        Canvas.ForceUpdateCanvases();
        float textWidth = Mathf.Max(100, viewport.rect.width - 44);
        float needed = body.GetPreferredValues(body.text, textWidth, float.PositiveInfinity).y + 44;
        content.sizeDelta = new Vector2(0, Mathf.Max(viewport.rect.height, needed));
        content.anchoredPosition = Vector2.zero;
        body.ForceMeshUpdate();
    }
    static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false); return (RectTransform)go.transform;
    }
    static void Set(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = offsetMin; rect.offsetMax = offsetMax; }
    static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, float size)
    {
        RectTransform rect = Rect(name, parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size; text.color = new Color(1f, .95f, .81f); text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal; return text;
    }
    static Button Button(string name, Transform parent, TMP_FontAsset font, string caption, UnityEngine.Events.UnityAction callback)
    {
        RectTransform rect = Rect(name, parent);
        var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.09f, .34f, .29f);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(callback);
        var label = Text("Label", rect, font, 32); label.text = caption; label.alignment = TextAlignmentOptions.Center;
        Set(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(10, 6), new Vector2(-10, -6));
        return button;
    }
}
