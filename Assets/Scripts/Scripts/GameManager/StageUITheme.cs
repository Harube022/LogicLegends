using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared fantasy puzzle chrome; leaves labels and gameplay callbacks editable.</summary>
public static class StageUITheme
{
    public static readonly Color Gold = new Color(0.89f, 0.76f, 0.46f);
    public static readonly Color Ink = new Color(0.91f, 0.95f, 0.93f);
    public static readonly Color ReadingInk = new Color(0.16f, 0.10f, 0.06f);
    private static Sprite panel, buttonArt, continueArt, returnArt, paper;
    public static Sprite Panel => panel != null ? panel : panel = LoadSprite("StageUI/WoodPanel");
    public static Sprite ButtonArt => buttonArt != null ? buttonArt : buttonArt = LoadSprite("StageUI/WoodButtonTeal");
    private static Sprite ContinueArt => continueArt != null ? continueArt : continueArt = LoadSprite("StageUI/WoodButtonGreen");
    private static Sprite ReturnArt => returnArt != null ? returnArt : returnArt = LoadSprite("StageUI/WoodButtonRed");
    private static Sprite Paper => paper != null ? paper : paper = LoadSprite("StageUI/ReadingPaper");
    private static Sprite LoadSprite(string path) { var sprites = Resources.LoadAll<Sprite>(path); return sprites.Length > 0 ? sprites[0] : null; }
    public static TMP_FontAsset HeadingFont => Resources.Load<TMP_FontAsset>("Fonts & Materials/IMPACT SDF");
    public static TMP_FontAsset ReadingFont => Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

    public static void Surface(Image image, bool button = false)
    {
        image.sprite = button ? ButtonArt : Panel;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        image.pixelsPerUnitMultiplier = button ? 4f : 1.7f;
        if (!button)
        {
            FitContentInset(image);
        }
    }

    public static void CompactSurface(Image image)
    {
        Surface(image);
        image.pixelsPerUnitMultiplier = 5;
        FitContentInset(image);
    }

    private static void FitContentInset(Image image)
    {
        var inset = Decorate(image.transform, "CalmContentInset", Vector2.zero, Vector2.one);
        inset.rectTransform.offsetMin = new Vector2(image.sprite.border.x, image.sprite.border.y) / image.pixelsPerUnitMultiplier + Vector2.one * 4;
        inset.rectTransform.offsetMax = -new Vector2(image.sprite.border.z, image.sprite.border.w) / image.pixelsPerUnitMultiplier - Vector2.one * 4;
        inset.color = new Color(0.035f, 0.085f, 0.11f);
        inset.transform.SetAsFirstSibling();
    }

    public static void SkinButton(Button button)
    {
        var image = button.GetComponent<Image>();
        if (image != null)
        {
            Surface(image, true);
            if (button.name == "MainMenuButton") image.sprite = ReturnArt;
            else if (button.name == "NextOrRetryButton" || button.name == "CheckpointRetryButton" ||
                button.name == "BeginButton" || button.name == "GotItButton" || button.name == "BeginStage") image.sprite = ContinueArt;
            button.targetGraphic = image;
        }
        var outline = button.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
        // Keep the original painted rim untinted in every interaction state.
        button.transition = Selectable.Transition.None;
        if (button.GetComponent<StageButtonFeedback>() == null) button.gameObject.AddComponent<StageButtonFeedback>();
        foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
        {
            label.font = HeadingFont;
            label.color = Ink;
            label.raycastTarget = false;
            label.enableAutoSizing = true;
            label.fontSizeMin = 22; label.fontSizeMax = 30;
            label.outlineColor = new Color(0.035f, 0.06f, 0.055f, 0.9f);
            label.outlineWidth = 0.12f;
        }
        foreach (var label in button.GetComponentsInChildren<UnityEngine.UI.Text>(true))
        {
            label.color = Ink; label.raycastTarget = false;
        }
    }

    public static void SkinGameplay(Canvas canvas)
    {
        foreach (var image in canvas.GetComponentsInChildren<Image>(true))
        {
            if (image.name == "QuizPanel")
            {
                // This panel holds the question labels over the world, not a framed card.
                image.sprite = null;
                image.type = Image.Type.Simple;
                image.color = Color.clear;
                image.raycastTarget = false;
                image.enabled = false;
            }
            if (image.name == "BlankPanel" && HasAncestor(image.transform, "TruthTableDoorPanel"))
                Surface(image);
            if (image.name == "TruthTableColumnFeedbackPanel")
            {
                CompactSurface(image);
            }
        }
        foreach (var button in canvas.GetComponentsInChildren<Button>(true))
            if (HasAncestor(button.transform, "QuizPanel") || HasAncestor(button.transform, "TruthTableDoorPanel") ||
                button.name == "TruthTableSubmitColumnButton") SkinButton(button);
    }

    private static bool HasAncestor(Transform transform, string name)
    {
        for (var parent = transform.parent; parent != null; parent = parent.parent)
            if (parent.name == name) return true;
        return false;
    }

    public static void SkinTutorial(GameObject overlay)
    {
        foreach (var image in overlay.GetComponentsInChildren<Image>(true))
        {
            if (image.name == "TutorialCard" || image.name == "FramedCard")
            {
                BookSurface(image, overlay.name.Contains("Propositional") ? 1 : overlay.name.Contains("Inference") ? 3 : 2);
                var outline = image.GetComponent<Outline>();
                if (outline != null) outline.enabled = false;
            }
            else if (image.name == "FooterDivider") { image.color = Gold; image.raycastTarget = false; }
        }
        foreach (var button in overlay.GetComponentsInChildren<Button>(true)) SkinButton(button);
        foreach (var text in overlay.GetComponentsInChildren<TMP_Text>(true))
        {
            bool body = text.name == "PageBody" || text.name == "FieldNotesBody";
            text.font = body ? ReadingFont : HeadingFont;
            bool action = text.GetComponentInParent<Button>() != null;
            text.color = action ? Ink : text.name == "SectionLabel" || text.name == "PageIndicator" ? Gold : ReadingInk;
            if (!action) text.outlineWidth = 0;
            text.raycastTarget = false;
        }
        LayoutTutorial(overlay);
    }

    private static void LayoutTutorial(GameObject overlay)
    {
        foreach (var rect in overlay.GetComponentsInChildren<RectTransform>(true))
        {
            if (rect.parent == null || (rect.parent.name != "TutorialCard" && rect.parent.name != "FramedCard")) continue;
            switch (rect.name)
            {
                case "SectionLabel": Stretch(rect, new Vector2(0.09f, 0.87f), new Vector2(0.91f, 0.93f)); break;
                case "PageTitle": Stretch(rect, new Vector2(0.08f, 0.67f), new Vector2(0.46f, 0.78f)); break;
                case "PageBody": Stretch(rect, new Vector2(0.08f, 0.30f), new Vector2(0.46f, 0.65f)); break;
                case "FooterDivider": rect.gameObject.SetActive(false); break;
                case "BackButton":
                case "PreviousPage": Stretch(rect, new Vector2(0.09f, 0.10f), new Vector2(0.28f, 0.21f)); break;
                case "NextButton":
                case "NextPage": Stretch(rect, new Vector2(0.30f, 0.10f), new Vector2(0.49f, 0.21f)); break;
                case "PageIndicator": Stretch(rect, new Vector2(0.50f, 0.10f), new Vector2(0.62f, 0.21f)); break;
                case "BeginButton":
                case "BeginStage":
                case "GotItButton": Stretch(rect, new Vector2(0.64f, 0.10f), new Vector2(0.91f, 0.21f)); break;
            }
            var label = rect.GetComponent<TMP_Text>();
            if (label != null)
            {
                label.textWrappingMode = TextWrappingModes.Normal;
                label.enableAutoSizing = true;
                label.fontSizeMin = rect.name == "PageTitle" ? 32 : 24;
                label.fontSizeMax = rect.name == "PageTitle" ? 44 : rect.name == "PageBody" || rect.name == "FieldNotesBody" ? 31 : 28;
                label.margin = Vector4.zero;
            }
        }
    }

    private static Image Decorate(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var child = parent.Find(name) as RectTransform;
        if (child == null) child = Rect(name, parent);
        Stretch(child, min, max);
        var image = child.GetComponent<Image>();
        if (image == null) image = child.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static void BookSurface(Image image, int stage)
    {
        image.sprite = Panel; image.type = Image.Type.Sliced; image.color = Color.white;
        image.pixelsPerUnitMultiplier = 1.7f;
        var inset = image.transform.Find("CalmContentInset");
        if (inset != null) inset.gameObject.SetActive(false);
        foreach (var name in new[] { "LeftReadingPage", "RightReadingPage" })
        {
            bool left = name == "LeftReadingPage";
            var page = Decorate(image.transform, name, new Vector2(left ? 0.045f : 0.51f, 0.25f), new Vector2(left ? 0.49f : 0.955f, 0.85f));
            page.sprite = Paper; page.type = Image.Type.Sliced; page.color = Color.white; page.pixelsPerUnitMultiplier = 1.5f;
            page.transform.SetAsFirstSibling();
        }
        var spine = Decorate(image.transform, "BookSpine", new Vector2(0.493f, 0.25f), new Vector2(0.507f, 0.85f));
        spine.color = new Color(0.17f, 0.075f, 0.032f, 0.95f); spine.transform.SetAsFirstSibling();
        var heading = image.transform.Find("FieldNotesHeading");
        var notes = image.transform.Find("FieldNotesBody");
        if (heading == null) heading = Text("FieldNotesHeading", image.transform, "QUICK REFERENCE", 28).transform;
        if (notes == null) notes = Text("FieldNotesBody", image.transform, "", 31, true).transform;
        Stretch((RectTransform)heading, new Vector2(0.54f, 0.68f), new Vector2(0.92f, 0.78f));
        Stretch((RectTransform)notes, new Vector2(0.54f, 0.30f), new Vector2(0.92f, 0.65f));
        var body = notes.GetComponent<TMP_Text>(); body.alignment = TextAlignmentOptions.TopLeft;
        body.text = stage == 1 ? "MOVE\nJoystick or WASD\n\nREAD\nUse the book statue to start.\n\nCHOOSE\nStand on the pad at your door.\n\nFinish five challenges using one shared countdown."
            : stage == 2 ? "YOUR ROUTE\nBook / Houses / Truth table\n\nCollect True and False blocks. Fill four rows, then submit the column.\n\nComplete three columns at Easy, Medium and Hard.\n\nThe clock pauses at the table."
            : "ROUND CHECKLIST\n1. Complete the premises.\n2. Find a rule crystal.\n3. Place and validate it.\n\nThe conclusion appears after correct validation.\n\nThe exploration timer pauses at the board.";
    }

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static void Stretch(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min; rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    public static TMP_Text Text(string name, Transform parent, string value, float size, bool reading = false)
    {
        var rect = Rect(name, parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = reading ? ReadingFont : HeadingFont;
        text.text = value; text.fontSize = size;
        text.color = reading ? Ink : Gold;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    public static Button Button(string name, Transform parent, string title, UnityEngine.Events.UnityAction action)
    {
        var rect = Rect(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        var button = rect.gameObject.AddComponent<Button>();
        var text = Text("Label", rect, title, 30);
        Stretch(text.rectTransform, Vector2.zero, Vector2.one);
        text.rectTransform.offsetMin = new Vector2(28, 10);
        text.rectTransform.offsetMax = new Vector2(-28, -10);
        text.enableAutoSizing = true;
        text.fontSizeMin = 22; text.fontSizeMax = 30;
        SkinButton(button);
        if (action != null) button.onClick.AddListener(action);
        return button;
    }
}
