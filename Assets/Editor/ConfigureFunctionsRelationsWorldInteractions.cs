using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ConfigureFunctionsRelationsWorldInteractions
{
    private const string MaterialFolder = "Assets/Materials/FunctionsRelations";

    [MenuItem("Logic Legends/Configure Functions and Relations World Interactions")]
    public static void Configure()
    {
        if (SceneManager.GetActiveScene().name != "Functions and Relations")
            throw new InvalidOperationException("Open the Functions and Relations scene first.");
        var manager = GameObject.Find("FunctionsRelationsStageManager").GetComponent<FunctionsRelationsStageManager>();
        var controller = GameObject.Find("FR_GameplayCanvas/FR_MappingBoard").GetComponent<LaserMappingController>();
        var book = GameObject.Find("Book_Statue");
        var canvas = GameObject.Find("FR_GameplayCanvas");
        if (manager == null || controller == null || book == null || canvas == null)
            throw new InvalidOperationException("The challenge UI and Book Statue must exist before world interactions are configured.");

        Material blueLaser = EnsureLaserMaterial("LaserUnlit", new Color(.08f, .42f, 2.3f, 1f));
        Material greenLaser = EnsureLaserMaterial("LaserGreen", new Color(.08f, 2.0f, .28f, 1f));
        Material redLaser = EnsureLaserMaterial("LaserRed", new Color(2.2f, .07f, .1f, 1f));
        Material orangeIcon = EnsureLaserMaterial("PedestalOrange", new Color(2.1f, .77f, .08f, 1f));
        var domainRoot = FindSceneRoot("Domain");
        var rangeRoot = FindSceneRoot("Range");
        if (domainRoot == null || rangeRoot == null) throw new InvalidOperationException("Domain and Range assets are missing.");
        var domainDiamonds = NumberedDiamonds(domainRoot.transform);
        var rangeDiamonds = NumberedDiamonds(rangeRoot.transform);
        if (domainDiamonds.Length < 3 || rangeDiamonds.Length < 3)
            throw new InvalidOperationException("At least three numbered diamonds are needed on each asset.");
        var crystal = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/CrystalDiamond.mat");
        if (crystal == null) throw new InvalidOperationException("CrystalDiamond material is missing.");
        foreach (var diamond in domainDiamonds)
        {
            AssignCrystal(diamond, crystal);
            var emitter = diamond.GetComponent<LaserEmitter>();
            if (emitter == null) emitter = diamond.gameObject.AddComponent<LaserEmitter>();
            emitter.SetAnchor(EnsurePortAnchor(diamond, "FR_BeamEmitter", true));
            EnsureInteractionCollider(diamond);
        }
        foreach (var diamond in rangeDiamonds)
        {
            AssignCrystal(diamond, crystal);
            var receiver = diamond.GetComponent<LaserReceiver>();
            if (receiver == null) receiver = diamond.gameObject.AddComponent<LaserReceiver>();
            receiver.SetAnchor(EnsurePortAnchor(diamond, "FR_BeamReceiver", false));
            EnsureInteractionCollider(diamond);
        }
        Transform root = FindOrCreate("FR_WorldMapping", null, Vector3.zero);
        var oldDomains = root.Find("Domain Nodes");
        if (oldDomains != null) UnityEngine.Object.DestroyImmediate(oldDomains.gameObject);
        var oldCodomains = root.Find("Codomain Nodes");
        if (oldCodomains != null) UnityEngine.Object.DestroyImmediate(oldCodomains.gameObject);
        Transform beams = FindOrCreate("Laser Connections", root, Vector3.zero);
        controller.ConfigureDiamonds(domainDiamonds, rangeDiamonds, beams, blueLaser, greenLaser, redLaser);

        EnsureDiamondLabel(domainDiamonds[0], "DOMAIN\nCHOOSE AN INPUT", new Color(.52f, .90f, 1f));
        EnsureDiamondLabel(rangeDiamonds[0], "RANGE / CODOMAIN\nCHOOSE AN OUTPUT • UNDO", new Color(.66f, 1f, .82f));

        Transform zone = book.transform.Find("FR_BookInteractionZone");
        if (zone == null)
        {
            zone = new GameObject("FR_BookInteractionZone").transform;
            zone.SetParent(book.transform, false);
        }
        zone.localPosition = new Vector3(0f, .01f, 0f);
        zone.localRotation = Quaternion.identity;
        Vector3 scale = book.transform.lossyScale;
        zone.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
        zone.position = book.transform.position + Vector3.up * 2f;
        var collider = zone.GetComponent<SphereCollider>();
        if (collider == null) collider = zone.gameObject.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 4f;
        Set(manager, "bookInteractionCollider", collider);
        Transform solid = book.transform.Find("FR_BookSolidCollider");
        if (solid != null) UnityEngine.Object.DestroyImmediate(solid.gameObject);
        var bookBlocker = book.GetComponent<BoxCollider>();
        if (bookBlocker == null) bookBlocker = book.AddComponent<BoxCollider>();
        bookBlocker.isTrigger = false;
        bookBlocker.center = book.transform.InverseTransformPoint(book.transform.position + Vector3.up * 2f);
        Vector3 localSize = book.transform.InverseTransformVector(new Vector3(2.9f, 4.2f, 2.6f));
        bookBlocker.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));

        Transform hud = canvas.transform.Find("FR_HUD") ?? canvas.transform.Find("SafeArea/FR_HUD");
        Transform prompt = hud.Find("Interaction Prompt");
        if (prompt == null)
        {
            var go = new GameObject("Interaction Prompt", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            prompt = go.transform;
            prompt.SetParent(hud, false);
            var rect = (RectTransform)prompt;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(900, 76);
            rect.anchoredPosition = new Vector2(0, -340);
            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.color = new Color(.025f, .075f, .14f, .88f);
            image.raycastTarget = false;
            var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(prompt, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12, 4);
            textRect.offsetMax = new Vector2(-12, -4);
            var label = textGo.GetComponent<TextMeshProUGUI>();
            label.fontSize = 27;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(.88f, .96f, 1f);
            label.raycastTarget = false;
        }
        prompt.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        Set(manager, "interactionPromptText", prompt.GetComponentInChildren<TMP_Text>());
        prompt.gameObject.SetActive(false);

        var hudTimer = hud.Find("Challenge Timer") ?? hud.Find("CurrentStageHud/StageCountdown");
        if (hudTimer == null)
        {
            hudTimer = new GameObject("Challenge Timer", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).transform;
            hudTimer.SetParent(hud, false);
        }
        var timerRect = (RectTransform)hudTimer;
        timerRect.anchorMin = timerRect.anchorMax = new Vector2(.5f, 1f);
        timerRect.sizeDelta = new Vector2(260, 76);
        timerRect.anchoredPosition = new Vector2(0, -105);
        var timerLabel = hudTimer.GetComponent<TextMeshProUGUI>();
        timerLabel.text = "10:00";
        timerLabel.fontSize = 42;
        timerLabel.alignment = TextAlignmentOptions.Center;
        timerLabel.color = new Color(.42f, .93f, 1f);
        timerLabel.raycastTarget = false;
        Set(manager, "hudTimerText", timerLabel);
        var managerProperties = new SerializedObject(manager);
        managerProperties.FindProperty("startingSeconds").floatValue = 600f;
        managerProperties.ApplyModifiedPropertiesWithoutUndo();

        var domainPanel = EnsureChoicePanel(hud, "Domain Diamond Controls", "DOMAIN  •  INPUTS", new Color(.08f, .19f, .30f, .94f));
        var rangePanel = EnsureChoicePanel(hud, "Range Diamond Controls", "RANGE  •  OUTPUTS", new Color(.07f, .25f, .22f, .94f));
        Button[] domainButtons = new Button[4];
        Button[] rangeButtons = new Button[4];
        for (int i = 0; i < 4; i++)
        {
            domainButtons[i] = EnsureChoiceButton(domainPanel, "Domain Option " + (i + 1), "" + (i + 1),
                new Vector2(-165 + i * 110, -18), new Vector2(100, 76));
            rangeButtons[i] = EnsureChoiceButton(rangePanel, "Range Option " + (i + 1), ((char)('A' + i)).ToString(),
                new Vector2(-165 + i * 110, -18), new Vector2(100, 76));
        }
        var undo = EnsureChoiceButton(rangePanel, "Undo Connection", "NO CONNECTION TO UNDO",
            new Vector2(0, -105), new Vector2(402, 66));
        Set(manager, "domainChoicesPanel", domainPanel);
        Set(manager, "rangeChoicesPanel", rangePanel);
        Set(manager, "undoConnectionButton", undo);
        SetArray(manager, "domainChoiceButtons", domainButtons);
        SetArray(manager, "rangeChoiceButtons", rangeButtons);
        domainPanel.gameObject.SetActive(false);
        rangePanel.gameObject.SetActive(false);

        string[] pedestalNames = { "Pedestals", "Pedestals (1)", "Pedestals (2)" };
        string[] answerNames = { "FUNCTION\nONLY", "NOT A\nFUNCTION", "RELATION\nONLY" };
        string[] emblems = { "", "", "?" };
        Color[] iconColors = { new Color(.16f, 1f, .48f), new Color(1f, .21f, .28f),
            new Color(1f, .62f, .12f) };
        Material[] iconMaterials = { greenLaser, redLaser, orangeIcon };
        var neutralOrb = EnsurePedestalOrbMaterial(3, crystal, new Color(.16f, .7f, 1f));
        var pedestalTransforms = new Transform[pedestalNames.Length];
        for (int i = 0; i < pedestalNames.Length; i++)
        {
            var pedestal = FindSceneRoot(pedestalNames[i]);
            if (pedestal == null) throw new InvalidOperationException("Missing answer pedestal: " + pedestalNames[i]);
            pedestal.transform.position = new Vector3(-187.9f + (i - 1) * 4.5f,
                pedestal.transform.position.y, 888.5f);
            pedestal.SetActive(true);
            pedestalTransforms[i] = pedestal.transform;
            var orbMaterial = EnsurePedestalOrbMaterial(i, crystal, iconColors[i]);
            EnsurePedestalUI(pedestal.transform, i, answerNames[i], emblems[i],
                iconColors[i], iconMaterials[i], orbMaterial);
            pedestal.SetActive(false);
        }
        SetTransforms(manager, "classificationPedestals", pedestalTransforms);
        SetMaterials(manager, "pedestalOrbMaterials", new[] {
            EnsurePedestalOrbMaterial(0, crystal, iconColors[0]),
            EnsurePedestalOrbMaterial(1, crystal, iconColors[1]),
            EnsurePedestalOrbMaterial(2, crystal, iconColors[2]) });
        SetMaterials(manager, "pedestalIconMaterials", iconMaterials);
        Set(manager, "neutralPedestalOrbMaterial", neutralOrb);
        Set(manager, "neutralPedestalIconMaterial", blueLaser);

        // Reading uses the InformationBoard camera. Submit is restored on the HUD below.
        var submit = canvas.transform.Find("FR_MappingBoard/Submit Mapping");
        if (submit != null) submit.gameObject.SetActive(false);
        var legacyLayer = canvas.transform.Find("FR_MappingBoard/Laser Layer");
        if (legacyLayer != null) legacyLayer.gameObject.SetActive(false);
        var legacyDomains = canvas.transform.Find("FR_MappingBoard/Domain Nodes");
        if (legacyDomains != null) legacyDomains.gameObject.SetActive(false);
        var legacyCodomains = canvas.transform.Find("FR_MappingBoard/Codomain Nodes");
        if (legacyCodomains != null) legacyCodomains.gameObject.SetActive(false);
        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(controller);
        ConfigureFunctionsRelationsPresentation.Configure();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("Functions and Relations diamond controls, solid Book collider, and URP laser configured.");
    }

    private static GameObject FindSceneRoot(string name)
    {
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == name) return root;
        return null;
    }

    private static Transform[] NumberedDiamonds(Transform root)
    {
        var diamonds = new System.Collections.Generic.List<Transform>();
        for (int i = 1; i <= 4; i++)
        {
            var diamond = root.Find(i.ToString());
            if (diamond != null && diamond.GetComponent<Renderer>() != null) diamonds.Add(diamond);
        }
        return diamonds.ToArray();
    }

    private static void EnsureInteractionCollider(Transform diamond)
    {
        var collider = diamond.GetComponent<BoxCollider>();
        if (collider == null) collider = diamond.gameObject.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        var mesh = diamond.GetComponent<MeshFilter>();
        if (mesh != null && mesh.sharedMesh != null)
        {
            collider.center = mesh.sharedMesh.bounds.center;
            collider.size = mesh.sharedMesh.bounds.size;
        }
    }

    private static void AssignCrystal(Transform diamond, Material crystal)
    {
        var renderer = diamond.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = crystal;
        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        EditorUtility.SetDirty(renderer);
    }

    private static Transform EnsurePortAnchor(Transform diamond, string name, bool outgoing)
    {
        var anchor = diamond.Find(name);
        if (anchor == null)
        {
            anchor = new GameObject(name).transform;
            anchor.SetParent(diamond, false);
        }
        var bounds = diamond.GetComponent<Renderer>().bounds;
        anchor.position = new Vector3(outgoing ? bounds.max.x - .1f : bounds.min.x + .1f,
            bounds.center.y, bounds.center.z);
        return anchor;
    }

    private static void EnsurePedestalUI(Transform pedestal, int option, string answer, string emblem,
        Color iconColor, Material iconMaterial, Material orbMaterial)
    {
        var renderer = pedestal.GetComponent<Renderer>();
        if (renderer == null) throw new InvalidOperationException("Answer pedestal has no renderer: " + pedestal.name);
        var bounds = renderer.bounds;
        var ui = pedestal.Find("FR_AnswerUI");
        if (ui == null)
        {
            ui = new GameObject("FR_AnswerUI").transform;
            ui.SetParent(pedestal, false);
        }
        var orb = ui.Find("EmblemOrb");
        if (orb == null)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "EmblemOrb";
            orb = sphere.transform;
            orb.SetParent(ui, false);
            var collider = sphere.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
        }
        orb.position = new Vector3(bounds.center.x, bounds.max.y + 1.2f, bounds.min.z - .22f);
        orb.localScale = Vector3.one * .85f;
        var orbRenderer = orb.GetComponent<Renderer>();
        orbRenderer.sharedMaterial = orbMaterial;
        orbRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        orbRenderer.receiveShadows = false;

        EnsurePedestalText(ui, "Emblem", emblem,
            new Vector3(bounds.center.x, bounds.max.y + 1.2f, bounds.min.z - .72f),
            new Vector2(2.5f, 2f), 5f, iconColor);
        EnsurePedestalText(ui, "Label", answer,
            new Vector3(bounds.center.x, bounds.max.y - .12f, bounds.min.z - .3f),
            new Vector2(2.5f, 1.3f), 2.4f, new Color(1f, .96f, .84f));
        var answerLabel = ui.Find("Label").GetComponent<TextMeshPro>();
        answerLabel.enableAutoSizing = true;
        answerLabel.fontSizeMin = 1.3f;
        answerLabel.fontSizeMax = 2.4f;
        var center = new Vector3(bounds.center.x, bounds.max.y + 1.2f, bounds.min.z - .8f);
        var ring = EnsureIconLine(ui, "EmblemRing", iconMaterial, .075f, 32, true);
        for (int i = 0; i < 32; i++)
        {
            float angle = 2f * Mathf.PI * i / 32f;
            ring.SetPosition(i, center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * .65f);
        }
        if (option == 0)
        {
            var check = EnsureIconLine(ui, "CheckMark", iconMaterial, .13f, 3, false);
            check.SetPosition(0, center + new Vector3(-.38f, -.02f, -.02f));
            check.SetPosition(1, center + new Vector3(-.08f, -.30f, -.02f));
            check.SetPosition(2, center + new Vector3(.43f, .31f, -.02f));
        }
        else if (option == 1)
        {
            var first = EnsureIconLine(ui, "CrossOne", iconMaterial, .13f, 2, false);
            first.SetPosition(0, center + new Vector3(-.35f, -.35f, -.02f));
            first.SetPosition(1, center + new Vector3(.35f, .35f, -.02f));
            var second = EnsureIconLine(ui, "CrossTwo", iconMaterial, .13f, 2, false);
            second.SetPosition(0, center + new Vector3(-.35f, .35f, -.02f));
            second.SetPosition(1, center + new Vector3(.35f, -.35f, -.02f));
        }
    }

    private static LineRenderer EnsureIconLine(Transform parent, string name, Material material,
        float width, int points, bool loop)
    {
        var child = parent.Find(name);
        if (child == null)
        {
            child = new GameObject(name).transform;
            child.SetParent(parent, false);
        }
        var line = child.GetComponent<LineRenderer>();
        if (line == null) line = child.gameObject.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = true;
        line.positionCount = points;
        line.loop = loop;
        line.widthMultiplier = width;
        line.numCapVertices = 6;
        line.numCornerVertices = 4;
        line.startColor = line.endColor = Color.white;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    private static Material EnsurePedestalOrbMaterial(int index, Material crystal, Color color)
    {
        string path = MaterialFolder + "/PedestalOrb" + index + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(crystal) { name = "PedestalOrb" + index };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = crystal.shader;
        material.SetTexture("_BaseMap", crystal.GetTexture("_BaseMap"));
        material.SetColor("_BaseColor", new Color(color.r, color.g, color.b, .65f));
        material.SetColor("_EmissionColor", color * 1.25f);
        material.EnableKeyword("_EMISSION");
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsurePedestalText(Transform parent, string name, string value,
        Vector3 position, Vector2 size, float fontSize, Color color)
    {
        var child = parent.Find(name);
        if (child == null)
        {
            child = new GameObject(name, typeof(TextMeshPro)).transform;
            child.SetParent(parent, false);
        }
        child.position = position;
        child.rotation = Quaternion.identity;
        child.localScale = Vector3.one;
        var label = child.GetComponent<TextMeshPro>();
        label.text = value;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta = size;
        label.color = color;
    }

    private static void SetTransforms(UnityEngine.Object target, string property, Transform[] transforms)
    {
        var so = new SerializedObject(target);
        var field = so.FindProperty(property);
        field.arraySize = transforms.Length;
        for (int i = 0; i < transforms.Length; i++)
            field.GetArrayElementAtIndex(i).objectReferenceValue = transforms[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetMaterials(UnityEngine.Object target, string property, Material[] materials)
    {
        var so = new SerializedObject(target);
        var field = so.FindProperty(property);
        field.arraySize = materials.Length;
        for (int i = 0; i < materials.Length; i++)
            field.GetArrayElementAtIndex(i).objectReferenceValue = materials[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureDiamondLabel(Transform diamond, string text, Color color)
    {
        Transform child = diamond.Find("FR_PurposeLabel");
        if (child == null)
        {
            child = new GameObject("FR_PurposeLabel", typeof(TextMeshPro)).transform;
            child.SetParent(diamond, true);
        }
        var bounds = diamond.GetComponent<Renderer>().bounds;
        // Put the label in front of the decorative pillar so it remains legible.
        child.position = new Vector3(bounds.center.x, bounds.max.y + 1.9f, bounds.center.z - 3f);
        child.rotation = Quaternion.identity;
        child.localScale = Vector3.one;
        var label = child.GetComponent<TextMeshPro>();
        label.text = text;
        label.fontSize = 4.4f;
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta = new Vector2(13f, 3f);
        label.color = color;
    }

    private static RectTransform EnsureChoicePanel(Transform parent, string name, string heading, Color color)
    {
        Transform found = parent.Find(name);
        if (found != null) return found.GetComponent<RectTransform>();
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(460, 265);
        var image = go.GetComponent<UnityEngine.UI.Image>();
        image.color = color;
        image.raycastTarget = false;
        var title = new GameObject("Heading", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var titleRect = title.GetComponent<RectTransform>();
        titleRect.SetParent(rect, false);
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(.5f, .5f);
        titleRect.sizeDelta = new Vector2(430, 55);
        titleRect.anchoredPosition = new Vector2(0, 91);
        var label = title.GetComponent<TextMeshProUGUI>();
        label.text = heading;
        label.fontSize = 29;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        return rect;
    }

    private static Button EnsureChoiceButton(RectTransform panel, string name, string label, Vector2 position, Vector2 size)
    {
        Transform existing = panel.Find(name);
        if (existing != null)
        {
            var existingRect = existing.GetComponent<RectTransform>();
            existingRect.anchoredPosition = position;
            existingRect.sizeDelta = size;
            var existingLabel = existing.GetComponentInChildren<TMP_Text>();
            if (existingLabel != null) existingLabel.text = label;
            return existing.GetComponent<Button>();
        }
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image), typeof(Button));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(panel, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        go.GetComponent<UnityEngine.UI.Image>().color = new Color(.16f, .46f, .59f, 1f);
        var button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.SetParent(rect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(6, 3);
        textRect.offsetMax = new Vector2(-6, -3);
        var text = textGo.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = name == "Undo Connection" ? 23 : 38;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return button;
    }

    private static void SetArray(UnityEngine.Object target, string property, Button[] buttons)
    {
        var so = new SerializedObject(target);
        var field = so.FindProperty(property);
        field.arraySize = buttons.Length;
        for (int i = 0; i < buttons.Length; i++)
            field.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform FindOrCreate(string name, Transform parent, Vector3 position)
    {
        Transform result = parent == null ? (GameObject.Find(name) != null ? GameObject.Find(name).transform : null) : parent.Find(name);
        if (result == null)
        {
            result = new GameObject(name).transform;
            if (parent != null) result.SetParent(parent, false);
        }
        result.position = position;
        return result;
    }

    private static Material EnsureLaserMaterial(string name, Color color)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) throw new InvalidOperationException("URP Unlit shader is unavailable.");
        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetColor("_BaseColor", color);
        material.SetColor("_Color", color);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void Set(UnityEngine.Object target, string property, UnityEngine.Object value)
    {
        var so = new SerializedObject(target);
        var field = so.FindProperty(property);
        if (field == null) throw new InvalidOperationException("Missing serialized field: " + property);
        field.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
