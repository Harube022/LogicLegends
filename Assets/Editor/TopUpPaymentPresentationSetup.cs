using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Builds the Main Menu pack grid from the backend catalog, with fixed payment-control space.</summary>
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
        if (EditorApplication.isPlayingOrWillChangePlaymode || topUp.gameObject.scene.path != "Assets/Scenes/Main Menu.unity")
            throw new InvalidOperationException("Only Main Menu may be configured, outside Play Mode.");
        Undo.RegisterFullObjectHierarchyUndo(topUp.gameObject, "Configure gem-pack payment controls");
        var data = new SerializedObject(topUp);
        var buy = (UnityEngine.UI.Button)data.FindProperty("buyButton").objectReferenceValue;
        var item = (RectTransform)buy.transform.parent;
        var grid = item.parent.GetComponent<UnityEngine.UI.GridLayoutGroup>();
        grid.cellSize = new Vector2(260, 350); grid.padding = new RectOffset(20, 20, 12, 12);
        grid.spacing = new Vector2(20, 16);
        grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3;
        grid.childAlignment = TextAnchor.UpperCenter;
        var content = (RectTransform)item.parent;
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
        var fitter = content.GetComponent<UnityEngine.UI.ContentSizeFitter>();
        fitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        item.sizeDelta = grid.cellSize;
        Place(item.Find("PackFrame") as RectTransform, Vector2.zero, new Vector2(260, 340));
        Place(item.Find("PackHeading") as RectTransform, new Vector2(0, 142), new Vector2(220, 28));
        Place(item.Find("container") as RectTransform, new Vector2(0, 95), new Vector2(48, 48));
        Place(item.Find("ItemName") as RectTransform, new Vector2(0, 55), new Vector2(220, 32));
        Place(item.Find("ItemPrice") as RectTransform, new Vector2(0, 20), new Vector2(210, 30));
        Place(item.Find("PriceFrame") as RectTransform, new Vector2(0, 20), new Vector2(210, 30));
        Place((RectTransform)buy.transform, new Vector2(0, -38), new Vector2(224, 60));
        buy.image.pixelsPerUnitMultiplier = 4.5f;
        var buyText = buy.GetComponentInChildren<TMP_Text>(true);
        buyText.text = "BUY GEMS"; buyText.fontSize = 20; buyText.enableAutoSizing = false;
        buyText.textWrappingMode = TextWrappingModes.NoWrap;
        buyText.rectTransform.offsetMin = new Vector2(14, 8); buyText.rectTransform.offsetMax = new Vector2(-14, -8);
        var pending = Label("PendingPayment", item, "PAYMENT PENDING", 16);
        pending.color = new Color32(112, 63, 20, 255);
        pending.textWrappingMode = TextWrappingModes.Normal;
        Place(pending.rectTransform, new Vector2(0, -139), new Vector2(224, 36));
        pending.gameObject.SetActive(false);
        var cancelPayment = Action("CancelPayment", item, "CANCEL PAYMENT", new Vector2(0, -94), false);
        Place((RectTransform)cancelPayment.transform, new Vector2(0, -94), new Vector2(224, 48));
        var cancelText = cancelPayment.GetComponentInChildren<TMP_Text>(true);
        cancelText.fontSize = 18; cancelText.textWrappingMode = TextWrappingModes.NoWrap;
        cancelText.rectTransform.offsetMin = new Vector2(14, 6); cancelText.rectTransform.offsetMax = new Vector2(-14, -6);
        cancelPayment.gameObject.SetActive(false);

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
        data.FindProperty("pendingCardHeight").floatValue = 350;
        Bind(data, "packName", item.Find("ItemName").GetComponent<TMP_Text>());
        Bind(data, "packPrice", item.Find("ItemPrice").GetComponent<TMP_Text>());
        Bind(data, "shop", topUp.GetComponentInParent<ShopManager>(true));
        Bind(data, "confirmation", overlay.gameObject); Bind(data, "confirmationMessage", message);
        Bind(data, "confirmationHeading", title);
        Bind(data, "confirmButton", confirm); Bind(data, "cancelButton", cancel);
        data.ApplyModifiedPropertiesWithoutUndo();
        overlay.gameObject.SetActive(false);
        // Each scene card is a reusable fixed cell. Runtime values come from
        // /wallet.packs, so editing prices later does not require another APK.
        var packs = JsonUtility.FromJson<PackCatalog>(System.IO.File.ReadAllText("Backend/commerce/catalog.json")).packs;
        foreach (Transform old in item.parent.Cast<Transform>().Where(t => t != item).ToArray()) Undo.DestroyObjectImmediate(old.gameObject);
        var views = new GemPackCard[packs.Length];
        for (int i = 0; i < packs.Length; i++)
        {
            var cell = i == 0 ? item.gameObject : UnityEngine.Object.Instantiate(item.gameObject, item.parent);
            if (i != 0) Undo.RegisterCreatedObjectUndo(cell, "Add gem pack");
            cell.name = "GemPack_" + packs[i].id;
            var view = cell.GetComponent<GemPackCard>();
            if (view == null) view = Undo.AddComponent<GemPackCard>(cell);
            var viewData = new SerializedObject(view);
            Bind(viewData, "packName", cell.transform.Find("ItemName").GetComponent<TMP_Text>());
            Bind(viewData, "packPrice", cell.transform.Find("ItemPrice").GetComponent<TMP_Text>());
            var button = cell.transform.Find(buy.name).GetComponent<UnityEngine.UI.Button>();
            button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            Bind(viewData, "buyButton", button);
            Bind(viewData, "actionLabel", button.GetComponentInChildren<TMP_Text>(true));
            Bind(viewData, "cancelPaymentButton", cell.transform.Find("CancelPayment").GetComponent<UnityEngine.UI.Button>());
            Bind(viewData, "paymentStatus", cell.transform.Find("PendingPayment").GetComponent<TMP_Text>());
            Bind(viewData, "heading", cell.transform.Find("PackHeading").GetComponent<RectTransform>());
            Bind(viewData, "gemIcon", cell.transform.Find("container").GetComponent<RectTransform>());
            Bind(viewData, "priceFrame", cell.transform.Find("PriceFrame").GetComponent<RectTransform>());
            viewData.ApplyModifiedPropertiesWithoutUndo();
            view.Bind(packs[i], _ => { }, () => { });
            view.Present(false, false, false, false, false); views[i] = view;
        }
        data.Update();
        var cards = data.FindProperty("cards"); cards.arraySize = views.Length;
        for (int i = 0; i < views.Length; i++) cards.GetArrayElementAtIndex(i).objectReferenceValue = views[i];
        data.ApplyModifiedPropertiesWithoutUndo();
        var scroll = topUp.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);
        scroll.horizontal = false; scroll.vertical = true; scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
        scroll.verticalNormalizedPosition = 1;
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)item.parent);
    }
    [Serializable] private sealed class PackCatalog { public CommerceClient.Pack[] packs; }
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
