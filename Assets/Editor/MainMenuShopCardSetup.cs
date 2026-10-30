using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Bakes the actual outfits into lightweight portraits. No runtime cameras or downloads.</summary>
public static class MainMenuShopCardSetup
{
    private const string PortraitFolder = "Assets/Art/ShopPortraits";

    [MenuItem("Logic Legends/Main Menu/Build Shop Outfit Cards")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/Scenes/Main Menu.unity")
            throw new InvalidOperationException("Open Main Menu outside Play Mode first.");
        var shop = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ShopManager>(true)).Single();
        Undo.RegisterFullObjectHierarchyUndo(shop.gameObject, "Update shop outfit cards");
        var data = new SerializedObject(shop);
        var preview = new SerializedObject(shop.GetComponent<ShopPreviewUI>());
        var models = preview.FindProperty("models");
        var items = data.FindProperty("allShopItems");
        Directory.CreateDirectory(PortraitFolder);
        for (int i = 0; i < items.arraySize; i++)
        {
            var item = (ShopItem)items.GetArrayElementAtIndex(i).objectReferenceValue;
            if (item == null) continue;
            GameObject model = null;
            for (int j = 0; j < models.arraySize; j++)
            {
                var entry = models.GetArrayElementAtIndex(j);
                if (entry.FindPropertyRelative("itemID").stringValue == item.ItemID)
                    model = (GameObject)entry.FindPropertyRelative("model").objectReferenceValue;
            }
            if (model == null) throw new InvalidOperationException("Missing outfit model: " + item.ItemID);
            var path = PortraitFolder + "/" + item.ItemID + ".png";
            BakePortrait(model, path);
            StyleCard(item, AssetDatabase.LoadAssetAtPath<Sprite>(path));
        }
        var firstItem = (ShopItem)items.GetArrayElementAtIndex(0).objectReferenceValue;
        var grid = firstItem.GetComponentInParent<GridLayoutGroup>(true);
        grid.cellSize = new Vector2(260, 350);
        grid.spacing = new Vector2(20, 16);
        grid.padding = new RectOffset(20, 20, 10, 10);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        StyleBalance((TMP_Text)data.FindProperty("coinsText").objectReferenceValue, "COINS", -134, -227, "Coin");
        StyleBalance((TMP_Text)data.FindProperty("gemsText").objectReferenceValue, "GEMS", 134, 41, "Gems");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Shop updated: ten matching half-body outfit portraits, preserved names/prices, readable coin/gem balances. Main Menu only.");
    }

    private static void BakePortrait(GameObject source, string path)
    {
        var utility = new PreviewRenderUtility();
        Texture2D texture = null;
        try
        {
            var clone = UnityEngine.Object.Instantiate(source);
            clone.SetActive(false);
            clone.transform.SetParent(null, false);
            clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            clone.transform.localScale = source.transform.lossyScale;
            foreach (var behaviour in clone.GetComponentsInChildren<Behaviour>(true)) behaviour.enabled = false;
            foreach (var t in clone.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
            utility.AddSingleGO(clone);
            clone.SetActive(true);
            // Skinned renderer bounds include animation padding. Bake vertices to frame the real head/waist.
            var bounds = new Bounds();
            bool found = false;
            foreach (var renderer in clone.GetComponentsInChildren<Renderer>())
            {
                Mesh mesh = null;
                bool temporary = renderer is SkinnedMeshRenderer;
                if (renderer is SkinnedMeshRenderer skin) { mesh = new Mesh(); skin.BakeMesh(mesh); }
                else mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) continue;
                foreach (var vertex in mesh.vertices)
                {
                    var point = renderer.transform.TransformPoint(vertex);
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                    else bounds.Encapsulate(point);
                }
                if (temporary) UnityEngine.Object.DestroyImmediate(mesh);
            }
            if (!found) throw new InvalidOperationException("No outfit geometry: " + source.name);
            float bottom = bounds.min.y + bounds.size.y * .49f;
            float top = bounds.max.y + bounds.size.y * .04f;
            var target = new Vector3(bounds.center.x, (bottom + top) * .5f, bounds.center.z);
            utility.BeginPreview(new Rect(0, 0, 448, 320), GUIStyle.none);
            var camera = utility.camera;
            camera.orthographic = true;
            camera.orthographicSize = (top - bottom) * .5f;
            camera.transform.SetPositionAndRotation(target + Vector3.forward * 15, Quaternion.Euler(0, 180, 0));
            camera.nearClipPlane = .01f; camera.farClipPlane = 40;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            camera.allowHDR = false;
            utility.ambientColor = new Color(.65f, .65f, .65f);
            utility.lights[0].intensity = 1.1f;
            utility.lights[0].transform.rotation = Quaternion.Euler(30, 210, 0);
            utility.lights[1].intensity = .65f;
            utility.lights[1].transform.rotation = Quaternion.Euler(340, 150, 0);
            utility.Render(true);
            // EndStaticPreview returns RGB24 and discards transparency. Preserve the render target alpha.
            var rendered = (RenderTexture)utility.EndPreview();
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = rendered;
                texture = new Texture2D(rendered.width, rendered.height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, rendered.width, rendered.height), 0, 0);
                texture.Apply();
            }
            finally { RenderTexture.active = previous; }
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            utility.Cleanup();
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 512;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static void StyleCard(ShopItem item, Sprite portrait)
    {
        var root = item.transform;
        var data = new SerializedObject(item);
        var button = (Button)data.FindProperty("buyButton").objectReferenceValue;
        var name = item.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name != "ItemPrice" && !t.transform.IsChildOf(button.transform));
        var price = root.Find("ItemPrice").GetComponent<TMP_Text>();
        var frame = root.Find("container").GetComponent<Image>();
        // The original card sprite includes transparent padding above and below its visible edge.
        Place(frame.rectTransform, Vector2.zero, new Vector2(260, 390));
        frame.preserveAspect = false;
        var image = Child("OutfitPortrait", root).GetComponent<Image>();
        if (image == null) image = root.Find("OutfitPortrait").gameObject.AddComponent<Image>();
        image.sprite = portrait; image.preserveAspect = true; image.raycastTarget = false;
        Place(image.rectTransform, new Vector2(0, 78), new Vector2(228, 148));
        image.transform.SetSiblingIndex(1);
        StyleText(name, 28); name.text = item.DisplayName;
        Place(name.rectTransform, new Vector2(0, -16), new Vector2(236, 38));
        StyleText(price, 24); price.text = item.Price.ToString("N0"); price.alignment = TextAlignmentOptions.MidlineLeft;
        Place(price.rectTransform, new Vector2(30, -54), new Vector2(100, 34));
        var icon = root.Find("Coin_Img").GetComponent<Image>();
        Place(icon.rectTransform, new Vector2(-38, -54), new Vector2(32, 32));
        Place((RectTransform)button.transform, new Vector2(0, -119), new Vector2(220, 88));
        button.GetComponent<Image>().pixelsPerUnitMultiplier = 4.5f;
        button.transform.SetAsLastSibling();
        var label = button.GetComponentInChildren<TMP_Text>(true);
        StyleText(label, 28); label.color = new Color32(255, 242, 212, 255);
    }

    private static void StyleBalance(TMP_Text value, string title, float x, float iconX, string iconName)
    {
        var background = (RectTransform)value.transform.parent;
        var banner = background.parent;
        Place(background, new Vector2(x, 0), new Vector2(236, 100));
        StyleText(value, 34); value.color = new Color32(255, 242, 212, 255);
        value.enableAutoSizing = true; value.fontSizeMin = 22; value.fontSizeMax = 34;
        value.text = "0";
        Place(value.rectTransform, new Vector2(20, -14), new Vector2(180, 44));
        var heading = Child("CurrencyLabel", background).GetComponent<TMP_Text>();
        if (heading == null) heading = background.Find("CurrencyLabel").gameObject.AddComponent<TextMeshProUGUI>();
        StyleText(heading, 22); heading.text = title; heading.color = new Color32(255, 226, 172, 255);
        Place(heading.rectTransform, new Vector2(20, 25), new Vector2(180, 28));
        Place((RectTransform)banner.Find(iconName), new Vector2(iconX, 0), new Vector2(80, 80));
        banner.Find(iconName).SetAsLastSibling();
    }

    private static void StyleText(TMP_Text text, float size)
    {
        text.font = StageUITheme.HeadingFont; text.fontSize = size; text.enableAutoSizing = false;
        text.isOrthographic = true; text.richText = false; text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis;
        text.characterWidthAdjustment = 0; text.alignment = TextAlignmentOptions.Center;
        text.color = new Color32(55, 32, 15, 255);
    }
    private static RectTransform Child(string name, Transform parent)
    {
        var existing = parent.Find(name) as RectTransform;
        if (existing != null) return existing;
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
        Undo.RegisterCreatedObjectUndo(go, "Create shop portrait UI");
        go.transform.SetParent(parent, false); return (RectTransform)go.transform;
    }
    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.localScale = Vector3.one; rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
        rect.anchoredPosition = position; rect.sizeDelta = size;
    }
}
