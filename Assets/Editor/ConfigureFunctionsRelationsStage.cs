using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ConfigureFunctionsRelationsStage
{
    private const string PrefabFolder = "Assets/Prefabs/PREFABS/FunctionsRelations";
    private const string MaterialFolder = "Assets/Materials/FunctionsRelations";

    [MenuItem("Logic Legends/Configure Functions and Relations Stage")]
    public static void Configure()
    {
        if (SceneManager.GetActiveScene().name != "Functions and Relations")
            throw new InvalidOperationException("Open the Functions and Relations scene before configuring its board.");
        EnsureBuildScene();
        EnsureFolder("Assets/Prefabs/PREFABS", "FunctionsRelations");
        EnsureFolder("Assets/Materials", "FunctionsRelations");
        CreateCrystalMaterial();
        DomainNode domainPrefab = CreateDomainPrefab();
        CodomainNode codomainPrefab = CreateCodomainPrefab();

        if (GameObject.Find("FR_GameplayCanvas") != null)
        {
            var existingBoard = GameObject.Find("FR_GameplayCanvas/FR_MappingBoard").GetComponent<RectTransform>();
            existingBoard.anchorMin = Vector2.zero;
            existingBoard.anchorMax = Vector2.one;
            existingBoard.offsetMin = Vector2.zero;
            existingBoard.offsetMax = Vector2.zero;
            existingBoard.GetComponent<Image>().color = new Color(.055f, .09f, .15f, 1f);
            var existingManager = GameObject.Find("FunctionsRelationsStageManager").GetComponent<FunctionsRelationsStageManager>();
            SetInt(existingManager, "stageNumber", 8);
            var existingResults = existingBoard.Find("Results Panel").GetComponent<RectTransform>();
            Stretch(existingResults);
            existingResults.GetComponent<Image>().color = new Color(.04f, .08f, .13f, 1f);
            var existingRetry = existingResults.Find("Retry Stage") != null ? existingResults.Find("Retry Stage").GetComponent<Button>() : Button("Retry Stage", existingResults, "RETRY", new Vector2(-165, -345), new Vector2(270, 68));
            var existingMenu = existingResults.Find("Return To Menu") != null ? existingResults.Find("Return To Menu").GetComponent<Button>() : Button("Return To Menu", existingResults, "MAIN MENU", new Vector2(165, -345), new Vector2(270, 68));
            Set(existingManager, "retryButton", existingRetry);
            Set(existingManager, "menuButton", existingMenu);
            var controls = GameObject.Find("Canvas 1");
            if (controls != null)
            {
                var controlsGroup = controls.GetComponent<CanvasGroup>();
                if (controlsGroup == null) controlsGroup = controls.AddComponent<CanvasGroup>();
                Set(existingManager, "gameplayControls", controlsGroup);
            }
            ConfigureFunctionsRelationsWorldInteractions.Configure();
            Debug.Log("Functions and Relations UI and crystal material refreshed.");
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            return;
        }

        var canvasGo = new GameObject("FR_GameplayCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 20;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;

        var hud = Rect("FR_HUD", canvasGo.transform, Vector2.zero, new Vector2(1920, 1080));
        TMP_Text status = Text("Status", hud, "Approach the Book Statue and press Interact", new Vector2(0, 480), new Vector2(1450, 58), 28, TextAlignmentOptions.Center);
        status.color = new Color(.9f, .95f, 1f);
        Button open = Button("Open Board", hud, "BOARD", new Vector2(790, 450), new Vector2(170, 66));
        open.gameObject.SetActive(false);

        var boardRect = Rect("FR_MappingBoard", canvasGo.transform, Vector2.zero, new Vector2(1600, 880));
        Image background = boardRect.gameObject.AddComponent<Image>();
        background.color = new Color(.055f, .09f, .15f, 1f);
        Stretch(boardRect);
        var group = boardRect.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        TMP_Text title = Text("Challenge Title", boardRect, "FUNCTIONS AND RELATIONS", new Vector2(-90, 375), new Vector2(1200, 65), 42, TextAlignmentOptions.Center);
        TMP_Text timer = Text("Timer", boardRect, "10:00", new Vector2(640, 378), new Vector2(240, 68), 44, TextAlignmentOptions.Center);
        timer.color = new Color(.4f, .91f, 1f);
        TMP_Text instruction = Text("Instructions", boardRect, "", new Vector2(0, 300), new Vector2(1420, 92), 28, TextAlignmentOptions.Center);
        TMP_Text leftHeader = Text("Domain Header", boardRect, "DOMAIN", new Vector2(-560, 205), new Vector2(300, 55), 30, TextAlignmentOptions.Center);
        TMP_Text rightHeader = Text("Codomain Header", boardRect, "CODOMAIN", new Vector2(560, 205), new Vector2(300, 55), 30, TextAlignmentOptions.Center);
        leftHeader.color = rightHeader.color = new Color(.6f, .85f, 1f);

        var beams = Rect("Laser Layer", boardRect, Vector2.zero, new Vector2(1540, 520));
        var domains = Rect("Domain Nodes", boardRect, new Vector2(-560, 0), new Vector2(140, 430));
        var codomains = Rect("Codomain Nodes", boardRect, new Vector2(560, 0), new Vector2(140, 430));

        var classification = Rect("Function Choices", boardRect, new Vector2(0, -225), new Vector2(750, 68));
        Button yes = Button("Function Answer", classification, "FUNCTION", new Vector2(-165, 0), new Vector2(300, 64));
        Button no = Button("Not Function Answer", classification, "NOT A FUNCTION", new Vector2(165, 0), new Vector2(300, 64));
        var properties = Rect("Property Choices", boardRect, new Vector2(0, -225), new Vector2(1150, 68));
        string[] options = { "INJECTIVE", "SURJECTIVE", "BIJECTIVE", "MANY-TO-ONE" };
        Button[] optionButtons = new Button[4];
        for (int i = 0; i < 4; i++)
            optionButtons[i] = Button(options[i] + " Option", properties, options[i], new Vector2(-420 + i * 280, 0), new Vector2(260, 62));
        classification.gameObject.SetActive(false);
        properties.gameObject.SetActive(false);

        TMP_Text feedback = Text("Feedback", boardRect, "", new Vector2(0, -285), new Vector2(1250, 55), 26, TextAlignmentOptions.Center);
        TMP_Text range = Text("Range", boardRect, "Range = { }", new Vector2(0, -333), new Vector2(900, 52), 24, TextAlignmentOptions.Center);
        range.color = new Color(.63f, .84f, .93f);
        Button submit = Button("Submit Mapping", boardRect, "SUBMIT", new Vector2(0, -386), new Vector2(265, 66));
        Button close = Button("Close Mapping", boardRect, "×", new Vector2(735, 382), new Vector2(64, 64));

        var results = Rect("Results Panel", boardRect, Vector2.zero, new Vector2(1480, 750));
        Stretch(results);
        var resultsBackground = results.gameObject.AddComponent<Image>();
        resultsBackground.color = new Color(.04f, .08f, .13f, 1f);
        TMP_Text resultText = Text("Results", results, "", Vector2.zero, new Vector2(1250, 500), 48, TextAlignmentOptions.Center);
        Button retry = Button("Retry Stage", results, "RETRY", new Vector2(-165, -345), new Vector2(270, 68));
        Button menu = Button("Return To Menu", results, "MAIN MENU", new Vector2(165, -345), new Vector2(270, 68));
        results.gameObject.SetActive(false);

        var controller = boardRect.gameObject.AddComponent<LaserMappingController>();
        controller.Configure(domainPrefab, codomainPrefab, domains, codomains, beams);
        var managerGo = new GameObject("FunctionsRelationsStageManager");
        var manager = managerGo.AddComponent<FunctionsRelationsStageManager>();
        var source = managerGo.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;

        Set(manager, "catalog", AssetDatabase.LoadAssetAtPath<MappingChallengeCatalog>("Assets/Resources/FunctionsRelationsChallenges.asset"));
        Set(manager, "mapping", controller);
        Set(manager, "bookStatue", GameObject.Find("Book_Statue").transform);
        SetInt(manager, "stageNumber", 8);
        Set(manager, "board", group);
        var existingControls = GameObject.Find("Canvas 1");
        if (existingControls != null)
        {
            var controlsGroup = existingControls.GetComponent<CanvasGroup>();
            if (controlsGroup == null) controlsGroup = existingControls.AddComponent<CanvasGroup>();
            Set(manager, "gameplayControls", controlsGroup);
        }
        Set(manager, "boardBackground", background);
        Set(manager, "titleText", title);
        Set(manager, "instructionText", instruction);
        Set(manager, "feedbackText", feedback);
        Set(manager, "timerText", timer);
        Set(manager, "statusText", status);
        Set(manager, "rangeText", range);
        Set(manager, "resultsText", resultText);
        Set(manager, "resultsPanel", results.gameObject);
        Set(manager, "classificationChoices", classification.gameObject);
        Set(manager, "propertyChoices", properties.gameObject);
        Set(manager, "submitButton", submit);
        Set(manager, "closeButton", close);
        Set(manager, "openButton", open);
        Set(manager, "functionButton", yes);
        Set(manager, "notFunctionButton", no);
        Set(manager, "audioSource", source);
        Set(manager, "correctSound", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets/Sounds Effects/WORLDBGM/SOFTWIN.mp3"));
        Set(manager, "incorrectSound", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets/Sounds Effects/WORLDBGM/DAMAGETAKEN1.mp3"));
        Set(manager, "retryButton", retry);
        Set(manager, "menuButton", menu);
        var so = new SerializedObject(manager);
        var array = so.FindProperty("propertyButtons");
        array.arraySize = 4;
        for (int i = 0; i < 4; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = optionButtons[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        ConfigureFunctionsRelationsWorldInteractions.Configure();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("Configured Functions and Relations challenge board, node prefabs, and crystal diamonds.");
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }

    private static void EnsureBuildScene()
    {
        const string path = "Assets/Scenes/Functions and Relations.unity";
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var scene in scenes)
        {
            if (scene.path != path) continue;
            if (!scene.enabled)
            {
                scene.enabled = true;
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            return;
        }
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static Material CreateCrystalMaterial()
    {
        const string path = MaterialFolder + "/CrystalDiamond.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("The project URP Lit shader is missing.");
        if (material == null)
        {
            material = new Material(shader) { name = "CrystalDiamond" };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Hovl Studio/Magic effects pack/Textures/Crystal1_Base.png"));
        material.SetColor("_BaseColor", new Color(.55f, .92f, 1f, .48f));
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_Smoothness", .82f);
        material.SetFloat("_Metallic", .12f);
        material.SetColor("_EmissionColor", new Color(.04f, .19f, .29f));
        material.EnableKeyword("_EMISSION");
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void AssignCrystal(string path, Material material)
    {
        var go = GameObject.Find(path);
        if (go == null) throw new InvalidOperationException("Missing model: " + path);
        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer == null) throw new InvalidOperationException("Missing diamond renderer: " + path);
        renderer.sharedMaterial = material;
        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        EditorUtility.SetDirty(renderer);
    }

    private static DomainNode CreateDomainPrefab()
    {
        const string path = PrefabFolder + "/DomainNode.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<DomainNode>(path);
        if (existing != null)
        {
            DecorateNodePrefab(path, new Color(.16f, .43f, .66f, 1f));
            return AssetDatabase.LoadAssetAtPath<DomainNode>(path);
        }
        var go = new GameObject("DomainNode", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LaserEmitter), typeof(DomainNode));
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(105, 84);
        go.GetComponent<Image>().color = new Color(.16f, .43f, .66f);
        go.GetComponent<Button>().transition = Selectable.Transition.None;
        Text("Label", rect, "1", Vector2.zero, new Vector2(95, 72), 40, TextAlignmentOptions.Center);
        var anchor = Rect("Emitter", rect, new Vector2(54, 0), new Vector2(1, 1));
        go.GetComponent<LaserEmitter>().SetAnchor(anchor);
        PrefabUtility.SaveAsPrefabAsset(go, path);
        UnityEngine.Object.DestroyImmediate(go);
        DecorateNodePrefab(path, new Color(.16f, .43f, .66f, 1f));
        return AssetDatabase.LoadAssetAtPath<DomainNode>(path);
    }

    private static CodomainNode CreateCodomainPrefab()
    {
        const string path = PrefabFolder + "/CodomainNode.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<CodomainNode>(path);
        if (existing != null)
        {
            DecorateNodePrefab(path, new Color(.18f, .52f, .65f, 1f));
            return AssetDatabase.LoadAssetAtPath<CodomainNode>(path);
        }
        var go = new GameObject("CodomainNode", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LaserReceiver), typeof(CodomainNode));
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(105, 84);
        go.GetComponent<Image>().color = new Color(.17f, .35f, .48f);
        go.GetComponent<Button>().transition = Selectable.Transition.None;
        Text("Label", rect, "A", Vector2.zero, new Vector2(95, 72), 40, TextAlignmentOptions.Center);
        var anchor = Rect("Receiver", rect, new Vector2(-54, 0), new Vector2(1, 1));
        go.GetComponent<LaserReceiver>().SetAnchor(anchor);
        PrefabUtility.SaveAsPrefabAsset(go, path);
        UnityEngine.Object.DestroyImmediate(go);
        DecorateNodePrefab(path, new Color(.18f, .52f, .65f, 1f));
        return AssetDatabase.LoadAssetAtPath<CodomainNode>(path);
    }

    private static void DecorateNodePrefab(string path, Color color)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
            if (path.EndsWith("/DomainNode.prefab", StringComparison.Ordinal))
            {
                var emitter = root.GetComponent<LaserEmitter>();
                if (emitter == null) emitter = root.AddComponent<LaserEmitter>();
                emitter.SetAnchor(root.transform.Find("Emitter") as RectTransform);
            }
            else
            {
                var receiver = root.GetComponent<LaserReceiver>();
                if (receiver == null) receiver = root.AddComponent<LaserReceiver>();
                receiver.SetAnchor(root.transform.Find("Receiver") as RectTransform);
            }
            var hitArea = root.GetComponent<Image>();
            hitArea.color = new Color(0f, 0f, 0f, 0f);
            hitArea.raycastTarget = true;
            var visual = root.transform.Find("Crystal Visual") as RectTransform;
            if (visual == null)
            {
                visual = Rect("Crystal Visual", root.transform, Vector2.zero, new Vector2(64, 64));
                visual.SetSiblingIndex(0);
                visual.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var image = visual.gameObject.AddComponent<Image>();
                image.raycastTarget = false;
                var facet = Rect("Inner Facet", visual, new Vector2(-9, 9), new Vector2(32, 32));
                var facetImage = facet.gameObject.AddComponent<Image>();
                facetImage.color = new Color(.65f, .95f, 1f, .25f);
                facetImage.raycastTarget = false;
            }
            visual.GetComponent<Image>().color = color;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static TMP_Text Text(string name, Transform parent, string value, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        var rect = Rect(name, parent, position, size);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static Button Button(string name, Transform parent, string label, Vector2 position, Vector2 size)
    {
        var rect = Rect(name, parent, position, size);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(.15f, .28f, .40f, 1f);
        var button = rect.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        Text("Text", rect, label, Vector2.zero, size - new Vector2(12, 6), 27, TextAlignmentOptions.Center);
        return button;
    }

    private static void Set(UnityEngine.Object target, string field, UnityEngine.Object reference)
    {
        var so = new SerializedObject(target);
        var property = so.FindProperty(field);
        if (property == null) throw new InvalidOperationException("Missing serialized field: " + field);
        property.objectReferenceValue = reference;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetInt(UnityEngine.Object target, string field, int value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
