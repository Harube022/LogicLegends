using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Edits only the existing Main Menu gem pack and its checkout confirmation.</summary>
public static class TopUpPaymentPresentationSetup
{
    [MenuItem("Logic Legends/Main Menu/Configure Gem Payment Confirmation")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/Scenes/Main Menu.unity")
            throw new InvalidOperationException("Open Main Menu in Edit Mode first.");
        var topUp = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PayMongoTestTopUp>(true)).Single();
        Configure(topUp);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
    }
    public static void Configure(PayMongoTestTopUp topUp)
    {
        Undo.RegisterFullObjectHierarchyUndo(topUp.gameObject, "Configure gem-pack payment controls");
        var data = new SerializedObject(topUp);
        var buy = (UnityEngine.UI.Button)data.FindProperty("buyButton").objectReferenceValue;
        var item = (RectTransform)buy.transform.parent;
        var grid = item.parent.GetComponent<UnityEngine.UI.GridLayoutGroup>();
        grid.cellSize = new Vector2(320, 430); grid.padding = new RectOffset(24, 24, 32, 32);
        item.sizeDelta = grid.cellSize;
        Place(item.Find("PackFrame") as RectTransform, new Vector2(0, 25), new Vector2(320, 350));
        Place(item.Find("PackHeading") as RectTransform, new Vector2(0, 155), new Vector2(260, 30));
        Place(item.Find("container") as RectTransform, new Vector2(0, 97), new Vector2(64, 64));
        Place(item.Find("ItemName") as RectTransform, new Vector2(0, 48), new Vector2(260, 32));
        Place(item.Find("ItemPrice") as RectTransform, new Vector2(0, 9), new Vector2(230, 38));
        Place(item.Find("PriceFrame") as RectTransform, new Vector2(0, 9), new Vector2(230, 38));
        Place((RectTransform)buy.transform, new Vector2(0, -66), new Vector2(220, 88));
        buy.image.pixelsPerUnitMultiplier = 4.5f;
        var buyText = buy.GetComponentInChildren<TMP_Text>(true);
        buyText.text = "BUY GEMS"; buyText.fontSize = 22; buyText.enableAutoSizing = false;
        buyText.rectTransform.offsetMin = new Vector2(20, 10); buyText.rectTransform.offsetMax = new Vector2(-20, -10);
        var pending = Label("PendingPayment", item, "PAYMENT PENDING\nContinue checkout to pay.", 20);
        pending.color = new Color32(112, 63, 20, 255);
        pending.textWrappingMode = TextWrappingModes.Normal;
        Place(pending.rectTransform, new Vector2(0, -138), new Vector2(270, 72));
        pending.gameObject.SetActive(false);

        // Delete the obsolete standalone feedback, rather than retaining a hidden duplicate.
        foreach (string name in new[] { "TestPaymentStatus", "TextPaymentStatus" })
        {
            var old = topUp.transform.Find(name);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        }
        var canvas = topUp.GetComponentInParent<Canvas>().transform;
        var overlay = Child("GemPurchaseConfirmation", canvas);
        Stretch(overlay);
        var shade = overlay.GetComponent<UnityEngine.UI.Image>();
        if (shade == null) shade = overlay.gameObject.AddComponent<UnityEngine.UI.Image>();
        shade.color = new Color(0, .02f, .03f, .76f); shade.raycastTarget = true;
        var card = Child("Card", overlay); Place(card, Vector2.zero, new Vector2(780, 440));
        var frame = card.GetComponent<UnityEngine.UI.Image>();
        if (frame == null) frame = card.gameObject.AddComponent<UnityEngine.UI.Image>();
        frame.sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/GraphHUD/GraphMapFrame.png").OfType<Sprite>().OrderByDescending(s => s.rect.width * s.rect.height).First();
        frame.type = UnityEngine.UI.Image.Type.Sliced; frame.pixelsPerUnitMultiplier = 5; frame.color = Color.white; frame.raycastTarget = true;
        var title = Label("Heading", card, "CONFIRM PURCHASE", 40);
        Place(title.rectTransform, new Vector2(0, 142), new Vector2(660, 58));
        var message = Label("Message", card, "Buy 5 Gems for PHP 1.00?\nPayment opens in the browser.", 28);
        Place(message.rectTransform, new Vector2(0, 30), new Vector2(650, 116));
        var cancel = Action("Cancel", card, "CANCEL", new Vector2(-158, -135), false);
        var confirm = Action("Confirm", card, "CONFIRM", new Vector2(158, -135), true);
        Bind(data, "buyLabel", buyText); Bind(data, "pendingPayment", pending);
        Bind(data, "packFrame", item.Find("PackFrame").GetComponent<RectTransform>());
        data.FindProperty("normalCardHeight").floatValue = 350;
        data.FindProperty("pendingCardHeight").floatValue = 400;
        Bind(data, "packName", item.Find("ItemName").GetComponent<TMP_Text>());
        Bind(data, "packPrice", item.Find("ItemPrice").GetComponent<TMP_Text>());
        Bind(data, "shop", topUp.GetComponentInParent<ShopManager>(true));
        Bind(data, "confirmation", overlay.gameObject); Bind(data, "confirmationMessage", message);
        Bind(data, "confirmButton", confirm); Bind(data, "cancelButton", cancel);
        data.ApplyModifiedPropertiesWithoutUndo();
        overlay.gameObject.SetActive(false);
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)item.parent);
    }
    private static void Bind(SerializedObject data, string name, UnityEngine.Object value) => data.FindProperty(name).objectReferenceValue = value;
    private static RectTransform Child(string name, Transform parent)
    {
        var existing = parent.Find(name) as RectTransform;
        if (existing != null) return existing;
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
        Undo.RegisterCreatedObjectUndo(go, "Create gem payment UI"); go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
    private static void Stretch(RectTransform r)
    {
        r.localScale = Vector3.one; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }
    private static void Place(RectTransform r, Vector2 position, Vector2 size)
    {
        r.localScale = Vector3.one; r.anchorMin = r.anchorMax = r.pivot = Vector2.one * .5f;
        r.anchoredPosition = position; r.sizeDelta = size;
    }
    private static TMP_Text Label(string name, Transform parent, string text, float size)
    {
        var rect = Child(name, parent);
        var label = rect.GetComponent<TextMeshProUGUI>();
        if (label == null) label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = StageUITheme.HeadingFont; label.text = text; label.fontSize = size;
        label.enableAutoSizing = false; label.isOrthographic = true; label.richText = false; label.raycastTarget = false;
        label.color = new Color32(55, 32, 15, 255); label.alignment = TextAlignmentOptions.Center;
        return label;
    }
    private static UnityEngine.UI.Button Action(string name, Transform parent, string text, Vector2 position, bool green)
    {
        var rect = Child(name, parent); Place(rect, position, new Vector2(280, 88));
        var image = rect.GetComponent<UnityEngine.UI.Image>();
        if (image == null) image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = Resources.LoadAll<Sprite>(green ? "StageUI/WoodButtonGreen" : "StageUI/WoodButtonRed")[0];
        image.type = UnityEngine.UI.Image.Type.Sliced; image.pixelsPerUnitMultiplier = 4.5f; image.color = Color.white;
        var button = rect.GetComponent<UnityEngine.UI.Button>();
        if (button == null) button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image; button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
        var label = Label("Label", rect, text, 28); label.color = new Color32(255, 242, 212, 255);
        Stretch(label.rectTransform); label.rectTransform.offsetMin = new Vector2(24, 10); label.rectTransform.offsetMax = new Vector2(-24, -10);
        return button;
    }
}
