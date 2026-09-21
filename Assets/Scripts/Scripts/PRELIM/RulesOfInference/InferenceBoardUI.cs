using TMPro;
using UnityEngine;

namespace LogicLegends.Inference
{
    // Shared by the editor scene setup and the data-driven runtime premise rows.
    public static class InferenceBoardUI
    {
        public static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); return rect;
        }

        public static TextMeshProUGUI Text(Transform parent, string value, TMP_FontAsset font, float size, float width = 0)
        {
            var rect = Rect("Label", parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.text = value; text.fontSize = size;
            text.color = new Color(0.91f, 0.95f, 0.93f); text.raycastTarget = false;
            text.richText = false; text.alignment = TextAlignmentOptions.MidlineLeft;
            if (width > 0)
            {
                var layout = rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                layout.preferredWidth = width; layout.minWidth = width;
            }
            return text;
        }

        public static TMP_InputField Input(Transform parent, string placeholder, TMP_FontAsset font, float width)
        {
            var rect = Rect("Answer_" + placeholder, parent);
            var background = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); background.color = new Color(0.12f, 0.2f, 0.23f);
            var layout = rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.preferredWidth = width; layout.minWidth = width; layout.preferredHeight = 70;
            var input = rect.gameObject.AddComponent<TMP_InputField>();
            var viewport = Rect("Text Area", rect); Stretch(viewport, 12);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var content = Text(viewport, "", font, 30); Stretch(content.rectTransform, 0);
            var hint = Text(viewport, placeholder, font, 28); Stretch(hint.rectTransform, 0); hint.color = new Color(0.6f, 0.72f, 0.74f);
            input.textViewport = viewport; input.textComponent = content; input.placeholder = hint;
            input.targetGraphic = background; input.fontAsset = font; input.pointSize = 30;
            input.lineType = TMP_InputField.LineType.SingleLine; input.characterLimit = 100;
            input.richText = false; input.customCaretColor = true; input.caretColor = Color.white;
            return input;
        }

        public static void Stretch(RectTransform rect, float padding)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * padding; rect.offsetMax = Vector2.one * -padding;
        }
    }
}
