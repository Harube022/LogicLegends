#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Updates only card art and price decoration in the current Main Menu.</summary>
public static class ShopCardPresentationSetup
{
    [MenuItem("Logic Legends/Main Menu/Refresh Neutral Shop And Customization Cards")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/Scenes/Main Menu.unity" || scene.isDirty)
            throw new InvalidOperationException("Open the saved Main Menu outside Play Mode first.");
        Directory.CreateDirectory("Temp/ShopCardRefresh");
        File.Copy(scene.path, "Temp/ShopCardRefresh/Main Menu.before.unity", true);
        LatestSkinPortraitSetup.UpdateAll();
        var shop = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ShopManager>(true)).Single();
        var custom = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CustomizationManager>(true)).Single();
        Undo.RegisterFullObjectHierarchyUndo(shop.gameObject, "Decorate skin prices");
        Undo.RegisterFullObjectHierarchyUndo(custom.gameObject, "Add neutral customization portraits");
        var shopData = new SerializedObject(shop);
        var shopItems = shopData.FindProperty("allShopItems");
        var coin = SpriteAt("Assets/Art/ProfileCurrency/Coin.png");
        var gem = SpriteAt("Assets/Art/ProfileCurrency/Gem.png");
        for (int i = 0; i < shopItems.arraySize; i++)
        {
            var item = (ShopItem)shopItems.GetArrayElementAtIndex(i).objectReferenceValue;
            var label = item.transform.Find("ItemPrice").GetComponent<TMP_Text>();
            var data = new SerializedObject(item);
            data.FindProperty("coinPriceIcon").objectReferenceValue = PriceIcon(label.transform, "CoinPriceIcon", coin);
            data.FindProperty("gemPriceIcon").objectReferenceValue = PriceIcon(label.transform, "GemPriceIcon", gem);
            data.ApplyModifiedPropertiesWithoutUndo();
            item.UpdateUI(false);
            var portrait = item.transform.Find("OutfitPortrait").GetComponent<Image>();
            portrait.sprite = Portrait(item.ItemID);
            portrait.color = Color.white;
        }
        var customItems = new SerializedObject(custom).FindProperty("allUIItems");
        for (int i = 0; i < customItems.arraySize; i++)
        {
            var item = (CustomizationItem)customItems.GetArrayElementAtIndex(i).objectReferenceValue;
            if (item.Type != CustomizationItem.ItemType.Clothes) continue;
            var portrait = PriceIcon(item.transform, "OutfitPortrait", Portrait(item.ItemID));
            Place(portrait.rectTransform, new Vector2(0, 66), new Vector2(260, 170));
            portrait.transform.SetSiblingIndex(1);
            var label = item.transform.Find("Text (TMP)").GetComponent<TMP_Text>();
            Place(label.rectTransform, new Vector2(0, -63), new Vector2(280, 62));
            label.enableAutoSizing = true;
            label.fontSizeMin = 22;
            label.fontSizeMax = 28;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            var name = label.text.Replace('\n', ' ');
            int firstSpace = name.IndexOf(' ');
            label.text = firstSpace < 0 ? name : name.Substring(0, firstSpace) + "\n" + name.Substring(firstSpace + 1);
            label.raycastTarget = false;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Updated ten skin cards with Coin/Gem price icons and twelve neutral portraits shared with customization. No wallet or equipment writes.");
    }

    static Sprite Portrait(string id)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LatestSkinPortraitSetup.PortraitFolder + "/" + id + ".png");
        if (sprite == null) throw new InvalidOperationException("Missing card portrait: " + id);
        return sprite;
    }
    static Sprite SpriteAt(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderByDescending(s => s.rect.width * s.rect.height).First();
    static Image PriceIcon(Transform parent, string name, Sprite sprite)
    {
        var child = parent.Find(name);
        if (child == null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "Add card artwork");
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            child = go.transform;
        }
        var image = child.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }
    static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.localScale = Vector3.one;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
#endif
