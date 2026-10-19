using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Restyles existing top-up controls without replacing purchase bindings or prices.</summary>
public static class MainMenuTopUpPresentationSetup
{
    private static readonly Color Ink = new Color32(55, 32, 15, 255);
    private static readonly Color Teal = new Color32(28, 81, 84, 255);

    private static Sprite SpriteAt(string path, string name)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First(s => s.name == name);
    }

    private static void Place(RectTransform r, Vector2 position, Vector2 size)
    {
        r.localScale = Vector3.one;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
        r.anchoredPosition = position; r.sizeDelta = size;
    }

    private static TMP_Text Label(Transform parent, string name, TMP_FontAsset font, string value, float size)
    {
        var child = parent.Find(name);
        if (!child)
        {
            child = new GameObject(name, typeof(RectTransform)).transform;
            child.gameObject.layer = parent.gameObject.layer; child.SetParent(parent, false);
        }
        var label = child.GetComponent<TextMeshProUGUI>();
        if (!label) label = child.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font; label.text = value; label.fontSize = size;
        label.enableAutoSizing = false;
        label.color = Ink; label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }

    [MenuItem("Tools/Logic Legends/Style Main Menu Gem Top-Up")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Scenes/Main Menu.unity")
            throw new InvalidOperationException("Open Main Menu in Edit Mode first.");
        var topUp = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PayMongoTestTopUp>(true)).Single();
        var scroll = topUp.GetComponentInChildren<ScrollRect>(true);
        var content = scroll.content;
        var clothesGrid = scene.GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<GridLayoutGroup>(true))
            .Single(g => g.transform.parent.parent.name == "ClothesScroll");
        var clothesItem = clothesGrid.transform.GetChild(0);
        var clothesFrame = clothesItem.Find("container").GetComponent<RectTransform>();
        var clothesButton = clothesItem.GetComponentInChildren<Button>(true).GetComponent<RectTransform>();
        Undo.RegisterFullObjectHierarchyUndo(topUp.gameObject, "Match top-up card to clothes item size");
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/IMPACT SDF.asset");
        var cardSprite = SpriteAt("Assets/Art/GraphHUD/GraphMapFrame.png", "GraphMapFrame");
        var priceSprite = SpriteAt("Assets/Art/GraphHUD/GraphStatusFrame.png", "GraphStatusFrame");
        var buySprite = SpriteAt("Assets/Resources/StageUI/WoodButtonGreen.png", "WoodButtonGreen");
        var grid = content.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(clothesFrame.rect.width, clothesGrid.cellSize.y);
        grid.spacing = new Vector2(0, 28);
        grid.padding = new RectOffset(24, 24, 48, 48);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 1;
        grid.childAlignment = TextAnchor.UpperCenter;
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        var fitter = content.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
        foreach (Transform item in content)
        {
            var button = item.GetComponentInChildren<Button>(true);
            if (!button) continue;
            // Persist the compact size even while the top-up tab is inactive.
            ((RectTransform)item).sizeDelta = grid.cellSize;
            var background = item.GetComponent<Image>();
            background.enabled = false;
            var cardChild = item.Find("PackFrame");
            if (!cardChild)
            {
                cardChild = new GameObject("PackFrame", typeof(RectTransform)).transform;
                cardChild.gameObject.layer = item.gameObject.layer; cardChild.SetParent(item, false);
            }
            Place((RectTransform)cardChild, clothesFrame.anchoredPosition, clothesFrame.rect.size);
            cardChild.SetAsFirstSibling();
            var card = cardChild.GetComponent<Image>();
            if (!card) card = cardChild.gameObject.AddComponent<Image>();
            card.sprite = cardSprite; card.type = Image.Type.Sliced;
            card.pixelsPerUnitMultiplier = 4f; card.color = Color.white; card.raycastTarget = false;
            var heading = Label(item, "PackHeading", font, "GEM PACK", 21);
            heading.characterSpacing = 2;
            Place(heading.rectTransform, new Vector2(0, 101), new Vector2(220, 26));
            var icon = item.Find("container").GetComponent<Image>();
            Place(icon.rectTransform, new Vector2(0, 48), new Vector2(64, 64));
            icon.preserveAspect = true; icon.raycastTarget = false;
            var title = item.Find("ItemName").GetComponent<TMP_Text>();
            title.font = font; title.fontSize = 26; title.color = Teal;
            title.enableAutoSizing = false;
            title.alignment = TextAlignmentOptions.Center; title.raycastTarget = false;
            Place(title.rectTransform, new Vector2(0, -2), new Vector2(220, 28));
            var price = item.Find("ItemPrice").GetComponent<TMP_Text>();
            price.font = font; price.fontSize = 21; price.color = Ink;
            price.enableAutoSizing = false;
            price.alignment = TextAlignmentOptions.Center; price.raycastTarget = false;
            Place(price.rectTransform, new Vector2(0, -39), new Vector2(200, 40));
            var frameChild = item.Find("PriceFrame");
            if (!frameChild)
            {
                frameChild = new GameObject("PriceFrame", typeof(RectTransform)).transform;
                frameChild.gameObject.layer = item.gameObject.layer; frameChild.SetParent(item, false);
            }
            Place((RectTransform)frameChild, new Vector2(0, -39), new Vector2(200, 40));
            frameChild.SetSiblingIndex(1);
            var priceFrame = frameChild.GetComponent<Image>();
            if (!priceFrame) priceFrame = frameChild.gameObject.AddComponent<Image>();
            priceFrame.sprite = priceSprite; priceFrame.type = Image.Type.Sliced;
            priceFrame.pixelsPerUnitMultiplier = 4.5f; priceFrame.color = Color.white; priceFrame.raycastTarget = false;
            Place((RectTransform)button.transform, clothesButton.anchoredPosition, clothesButton.rect.size);
            var face = button.GetComponent<Image>(); face.sprite = buySprite;
            face.type = Image.Type.Sliced; face.pixelsPerUnitMultiplier = 4.5f;
            face.color = Color.white; face.raycastTarget = true;
            button.targetGraphic = face;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors; colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1, .96f, .86f);
            colors.pressedColor = new Color(.84f, .80f, .72f);
            colors.disabledColor = new Color(.65f, .65f, .65f, .75f); button.colors = colors;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            label.font = font; label.fontSize = 28; label.color = new Color32(255, 242, 212, 255);
            label.enableAutoSizing = false; label.text = label.text.ToUpperInvariant();
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(24, 10); label.rectTransform.offsetMax = new Vector2(-24, -10);
        }
        var data = new SerializedObject(topUp);
        data.FindProperty("hideWalletBalanceStatus").boolValue = true;
        data.ApplyModifiedPropertiesWithoutUndo();
        scroll.verticalNormalizedPosition = 1;
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
