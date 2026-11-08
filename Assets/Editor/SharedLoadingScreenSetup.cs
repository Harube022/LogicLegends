using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Builds a standalone prefab and opts in Main Menu only.</summary>
public static class SharedLoadingScreenSetup
{
    public const string PrefabPath = "Assets/Prefabs/UI/SharedLoadingScreen.prefab";
    const string Art = "Assets/Art/LoadingScreen/";

    [MenuItem("Logic Legends/Main Menu/Configure Shared Loading Screen")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/Scenes/Main Menu.unity")
            throw new InvalidOperationException("Open Main Menu in Edit Mode first.");
        if (UnityEngine.Object.FindFirstObjectByType<MainMenuLoadingScreen>(FindObjectsInactive.Include) != null)
            throw new InvalidOperationException("Main Menu loading screen is already configured.");
        Directory.CreateDirectory("Assets/Prefabs/UI");
        AssetDatabase.Refresh();
        var root = new GameObject("SharedLoadingScreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32761;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        var group = root.GetComponent<CanvasGroup>(); group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false;
        var screen = root.AddComponent<SharedLoadingScreen>();
        var background = Rect("Forest", root.transform, Vector2.zero, Vector2.zero);
        Stretch(background);
        var raw = background.gameObject.AddComponent<RawImage>();
        background.gameObject.AddComponent<CanvasBackgroundCover>();
        raw.texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Assets/Interface_Sprites/background/background(2).png");
        raw.raycastTarget = true;
        var shade = Rect("BackdropShade", root.transform, Vector2.zero, Vector2.zero); Stretch(shade);
        var shadeImage = shade.gameObject.AddComponent<Image>(); shadeImage.sprite = Gradient(); shadeImage.raycastTarget = false;
        shade.gameObject.SetActive(false); // Match the un-tinted Main Menu forest.
        var safe = Rect("SafeArea", root.transform, Vector2.zero, Vector2.zero); Stretch(safe);
        var content = Rect("Composition", safe, new Vector2(1920, 1080), Vector2.zero);
        var brand = Picture("Brand", content, new Vector2(780, 211), new Vector2(0, 347.5f), Import("LoadingBanner", Vector4.zero));
        brand.preserveAspect = true;
        Text("Heading", content, "PREPARING YOUR ADVENTURE", new Vector2(1100, 90), new Vector2(0, 25), 56, true, Cream);
        var status = Text("Status", content, "Loading...", new Vector2(1000, 50), new Vector2(0, -42), 26, false, Cream);
        var track = Picture("ProgressTrack", content, new Vector2(716, 43), new Vector2(-42, -106.5f), Import("LoadingProgressTrack", new Vector4(75, 28, 75, 28)));
        track.type = Image.Type.Sliced; track.pixelsPerUnitMultiplier = 4.5f;
        var fillRect = Rect("ProgressFill", track.transform, Vector2.zero, Vector2.zero);
        Stretch(fillRect); fillRect.offsetMin = new Vector2(7, 7); fillRect.offsetMax = new Vector2(-7, -7);
        var fill = fillRect.gameObject.AddComponent<RoundedLoadingFill>();
        fill.sprite = Import("LoadingProgressFill", Vector4.zero); fill.raycastTarget = false;
        fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0;
        var percent = Text("Percentage", content, "0%", new Vector2(80, 50), new Vector2(370, -106.5f), 28, true, Cream);
        var tip = Picture("TipContainer", content, new Vector2(860, 202), new Vector2(0, -339), Import("LoadingTipFrame", Vector4.one * 136.56f));
        tip.type = Image.Type.Sliced; tip.pixelsPerUnitMultiplier = 3.9f;
        // One continuous sliced Image, with no extra Outline, border or debug/slice guides.
        Text("TipHeading", tip.transform, "A LITTLE WISDOM", new Vector2(720, 42), new Vector2(0, 41), 28, true, Ink);
        var tipText = Text("Tip", tip.transform, "Read your objective before you begin.\nSmall clues can lead to big discoveries.", new Vector2(730, 90), new Vector2(0, -21), 28, false, Ink);
        Text("Footer", content, "Explore. Think. Become a legend.", new Vector2(1000, 40), new Vector2(0, -486), 20, false, Cream);
        var serialized = new SerializedObject(screen);
        Set(serialized, "canvas", canvas); Set(serialized, "visibility", group); Set(serialized, "composition", content); Set(serialized, "safeArea", safe);
        Set(serialized, "background", raw); Set(serialized, "progressFill", fill); Set(serialized, "statusLabel", status); Set(serialized, "progressLabel", percent); Set(serialized, "tipLabel", tipText);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        ConfigureConnectionError(root);
        canvas.enabled = false;
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        var binding = new GameObject("MainMenuLoadingScreenBinding").AddComponent<MainMenuLoadingScreen>();
        var bindingData = new SerializedObject(binding); Set(bindingData, "screen", instance.GetComponent<SharedLoadingScreen>()); bindingData.ApplyModifiedPropertiesWithoutUndo();
        Undo.RegisterCreatedObjectUndo(instance, "Add Main Menu loading screen");
        Undo.RegisterCreatedObjectUndo(binding.gameObject, "Bind Main Menu loading screen");
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("Saved reusable loading prefab and Main Menu-only binding. Existing teleport/fade objects are preserved.");
    }

    public static void UpgradeConnectionError()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try { ConfigureConnectionError(root); PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void ConfigureConnectionError(GameObject root)
    {
        var screen = root.GetComponent<SharedLoadingScreen>();
        var content = root.transform.Find("SafeArea/Composition");
        var data = new SerializedObject(screen);
        Set(data, "headingLabel", content.Find("Heading").GetComponent<TMP_Text>());
        content.Find("Status").GetComponent<TMP_Text>().text = "Loading...";
        var existing = content.Find("ConnectionError");
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
        Sprite frame = null;
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath("Assets/Art/GraphHUD/GraphMapFrame.png"))
            if (asset is Sprite sprite && sprite.name == "GraphMapFrame") frame = sprite;
        if (frame == null) throw new InvalidOperationException("The shared GraphMapFrame sprite is missing.");
        var panel = Picture("ConnectionError", content, new Vector2(960, 480), new Vector2(0, -150), frame);
        panel.type = Image.Type.Sliced; panel.pixelsPerUnitMultiplier = 4.5f;
        Text("ErrorHeading", panel.transform, "CONNECTION PROBLEM", new Vector2(800, 60), new Vector2(0, 135), 40, true, Ink);
        Text("ErrorMessage", panel.transform, "Check your internet connection, then try again.\nCancelling will close the app.",
            new Vector2(760, 110), new Vector2(0, 25), 29, false, Ink);
        var retry = ErrorButton("Retry", panel.transform, new Vector2(-160, -120), "WoodButtonGreen");
        var cancel = ErrorButton("Cancel", panel.transform, new Vector2(160, -120), "WoodButtonRed");
        Set(data, "errorPanel", panel.gameObject); Set(data, "retryButton", retry); Set(data, "cancelButton", cancel);
        data.ApplyModifiedPropertiesWithoutUndo();
        panel.gameObject.SetActive(false);
    }

    static Button ErrorButton(string label, Transform parent, Vector2 position, string sprite)
    {
        var image = Picture(label, parent, new Vector2(280, 88), position, Resources.Load<Sprite>("StageUI/" + sprite));
        image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 4.5f; image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        Text("Label", image.transform, label.ToUpperInvariant(), new Vector2(232, 68), Vector2.zero, 28, true, Cream);
        return button;
    }

    static readonly Color Cream = new Color32(255, 242, 212, 255);
    static readonly Color Ink = new Color32(41, 26, 15, 255);
    static void Set(SerializedObject data, string name, UnityEngine.Object value) { data.FindProperty(name).objectReferenceValue = value; }
    static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.sizeDelta = size; r.anchoredPosition = position; return r;
    }
    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    static Image Picture(string name, Transform parent, Vector2 size, Vector2 position, Sprite sprite)
    {
        var i = Rect(name, parent, size, position).gameObject.AddComponent<Image>(); i.sprite = sprite; i.raycastTarget = false; return i;
    }
    static TMP_Text Text(string name, Transform parent, string text, Vector2 size, Vector2 position, float fontSize, bool heading, Color color)
    {
        var label = Rect(name, parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/" + (heading ? "IMPACT SDF.asset" : "LiberationSans SDF.asset"));
        label.text = text; label.fontSize = fontSize; label.color = color; label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.Normal; label.overflowMode = TextOverflowModes.Overflow;
        return label;
    }
    static Sprite Import(string name, Vector4 border)
    {
        string path = Art + name + ".png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100; importer.spriteBorder = border; importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048; importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static Sprite Gradient()
    {
        string path = Art + "LoadingBackdropShade.png";
        var texture = new Texture2D(2, 256, TextureFormat.RGBA32, false);
        for (int y = 0; y < 256; y++)
        {
            float t = y / 255f;
            float alpha = t > .75f ? .14f : Mathf.Lerp(.78f, .06f, t / .75f);
            for (int x = 0; x < 2; x++) texture.SetPixel(x, y, new Color(.015f, .035f, .04f, alpha));
        }
        texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path); return Import("LoadingBackdropShade", Vector4.zero);
    }
}
