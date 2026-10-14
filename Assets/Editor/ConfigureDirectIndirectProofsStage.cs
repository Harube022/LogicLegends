using System;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>One-time, non-destructive setup for the existing Direct&Indirect scene.</summary>
public static class ConfigureDirectIndirectProofsStage
{
    private const string CatalogPath = "Assets/Resources/DirectIndirectProofChallenges.asset";
    private const string TabletPrefabPath = "Assets/Prefabs/PREFABS/DirectIndirectProofs/ProofTablet.prefab";
    private const string BasePedestalPath = "Assets/Prefabs/PREFABS/FormalInformalProofs/ProofWorldPedestal.prefab";
    private const string MaterialFolder = "Assets/Materials/DirectIndirectProofs/";

    [MenuItem("Logic Legends/Configure Direct and Indirect Proofs Stage")]
    public static void Configure()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.name != "Direct&Indirect")
            throw new InvalidOperationException("Open Direct&Indirect in Edit mode before configuring it.");
        if (GameObject.Find("TrialOfProofs") != null)
            throw new InvalidOperationException("TrialOfProofs already exists. Existing scene work was preserved.");

        DirectIndirectProofCatalog catalog = EnsureCatalog();
        GameObject tabletPrefab = EnsureTabletPrefab();
        GameObject pedestalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePedestalPath);
        if (pedestalPrefab == null) throw new InvalidOperationException("The shared ProofWorldPedestal prefab is missing.");
        Material blue = Unlit("DirectSeal_Blue", new Color(.11f, .58f, 1.0f));
        Material purple = Unlit("IndirectSeal_Purple", new Color(.62f, .30f, 1.0f));
        Material red = Unlit("ContradictionSeal_Red", new Color(1.0f, .18f, .14f));

        AddChamberCollision();
        Transform book = AddBookCollider();
        ConfigurePlayerSpawn();
        ConfigureMobileControls();

        Transform root = new GameObject("TrialOfProofs").transform;
        TMP_Text boardText = BuildBoardText(root);
        Transform boardPoint = new GameObject("Board Interaction Point").transform;
        boardPoint.SetParent(root, false);
        boardPoint.position = new Vector3(68.8f, 1.55f, 44.7f);

        ProofWorldPedestal[] methods = new ProofWorldPedestal[3];
        for (int i = 0; i < methods.Length; i++)
        {
            GameObject station = Spawn(pedestalPrefab, root, "Strategy " + (i + 1),
                new Vector3(43f + i * 15f, 1.42f, 73f), 0f, 1.3f);
            methods[i] = station.GetComponent<ProofWorldPedestal>();
            methods[i].SetContent(i == 0 ? "DIRECT PROOF" : i == 1 ? "CONTRAPOSITIVE" : "CONTRADICTION",
                i == 0 ? "Assume and derive." : i == 1 ? "Prove not Q → not P." : "Assume the opposite.");
            station.SetActive(false);
        }

        TrialProofTablet[] sources = new TrialProofTablet[5];
        ProofWorldPedestal[] slots = new ProofWorldPedestal[5];
        for (int i = 0; i < 5; i++)
        {
            float z = 47.5f + i * 4.5f;
            GameObject tablet = Spawn(tabletPrefab, root, "Proof Tablet " + (char)('A' + i),
                new Vector3(38.5f, 1.42f, z), -90f, 1.2f);
            sources[i] = tablet.GetComponent<TrialProofTablet>();
            if (sources[i] == null) throw new InvalidOperationException("ProofTablet prefab lacks TrialProofTablet.");
            tablet.SetActive(false);

            GameObject slot = Spawn(pedestalPrefab, root, "Proof Step Slot " + (i + 1),
                new Vector3(76.5f, 1.42f, z), 90f, 1.2f);
            slots[i] = slot.GetComponent<ProofWorldPedestal>();
            slots[i].SetContent("STEP " + (i + 1), "EMPTY");
            slot.SetActive(false);
        }

        Renderer indirectRenderer;
        GameObject directMarker = BuildMarker(root, "Direct Station Active", "DIRECT", blue,
            new Vector3(58.7f, 6.3f, 42.1f), out _);
        GameObject indirectMarker = BuildMarker(root, "Indirect Station Active", "INDIRECT", purple,
            new Vector3(53.0f, 6.3f, 48.1f), out indirectRenderer);
        directMarker.SetActive(false);
        indirectMarker.SetActive(false);

        HudRefs hud = BuildHud();
        GameObject managerObject = new GameObject("DirectIndirectProofStageManager");
        DirectIndirectProofStageController manager = managerObject.AddComponent<DirectIndirectProofStageController>();
        AudioSource audio = managerObject.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;
        Set(manager, "catalog", catalog);
        Set(manager, "bookStatue", book);
        Set(manager, "boardInteractionPoint", boardPoint);
        Set(manager, "boardText", boardText);
        SetArray(manager, "methodPedestals", methods);
        SetArray(manager, "sourceTablets", sources);
        SetArray(manager, "stepSlots", slots);
        Set(manager, "directStationMarker", directMarker);
        Set(manager, "indirectStationMarker", indirectMarker);
        Set(manager, "indirectMarkerColor", indirectRenderer);
        Set(manager, "contrapositiveMaterial", purple);
        Set(manager, "contradictionMaterial", red);
        Set(manager, "timerText", hud.timer);
        Set(manager, "progressText", hud.progress);
        Set(manager, "promptText", hud.prompt);
        Set(manager, "selectionText", hud.selection);
        Set(manager, "readerPanel", hud.readerPanel);
        Set(manager, "readerTitle", hud.readerTitle);
        Set(manager, "readerBody", hud.readerBody);
        Set(manager, "selectTabletButton", hud.selectButton);
        Set(manager, "closeReaderButton", hud.closeButton);
        Set(manager, "resultsPanel", hud.resultsPanel);
        Set(manager, "resultsText", hud.resultsText);
        Set(manager, "retryButton", hud.retryButton);
        Set(manager, "menuButton", hud.menuButton);
        Set(manager, "audioSource", audio);
        Set(manager, "correctSound", AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Assets/Sounds Effects/WORLDBGM/SOFTWIN.mp3"));
        Set(manager, "incorrectSound", AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Assets/Sounds Effects/WORLDBGM/DAMAGETAKEN1.mp3"));

        OrganizeDirectIndirectProofsRoom.Apply();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Direct and Indirect Proofs stage configured: five challenges, physical tablets, Book, timer, controls, and collision.");
    }

    private static void AddChamberCollision()
    {
        GameObject stage = GameObject.Find("Stage");
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/ProofChamber/DirectIndirectStageCollision.asset");
        if (stage == null || mesh == null) throw new InvalidOperationException("The chamber Stage or its lightweight collision mesh is missing.");
        MeshCollider collider = stage.GetComponent<MeshCollider>();
        if (collider == null) collider = stage.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
        collider.convex = false;
        collider.enabled = true;
    }

    private static Transform AddBookCollider()
    {
        GameObject book = GameObject.Find("Book_Statue");
        if (book == null) throw new InvalidOperationException("Book_Statue is missing.");
        BoxCollider collider = book.GetComponent<BoxCollider>();
        if (collider == null) collider = book.AddComponent<BoxCollider>();
        Vector3 scale = book.transform.lossyScale;
        collider.center = new Vector3(0f, 2.1f / scale.y, 0f);
        collider.size = new Vector3(3.5f / scale.x, 4.2f / scale.y, 3.1f / scale.z);
        return book.transform;
    }

    private static void ConfigurePlayerSpawn()
    {
        GameObject spawnerObject = GameObject.Find("LevelSpawner");
        if (spawnerObject == null) throw new InvalidOperationException("LevelSpawner is missing.");
        Transform spawn = new GameObject("Trial Player Spawn").transform;
        spawn.position = new Vector3(56.7f, 1.60f, 64.0f);
        spawn.rotation = Quaternion.Euler(0f, 180f, 0f);
        Set(spawnerObject.GetComponent<LevelSpawner>(), "spawnPoint", spawn);
        CinemachineCamera cm = GameObject.Find("CM_ThirdPersonCam").GetComponent<CinemachineCamera>();
        Set(spawnerObject.GetComponent<LevelSpawner>(), "cmCamera", cm);
        Camera camera = Camera.main;
        if (camera != null)
        {
            camera.transform.position = new Vector3(56.7f, 4.8f, 70.5f);
            camera.transform.LookAt(new Vector3(56.7f, 2.9f, 55.5f));
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, 100f);
        }
        cm.transform.position = new Vector3(56.7f, 4.8f, 70.5f);
        cm.transform.rotation = camera != null ? camera.transform.rotation : Quaternion.Euler(0f, 180f, 0f);
    }

    private static void ConfigureMobileControls()
    {
        Transform safe = GameObject.Find("Canvas 1").transform.Find("SafeArea");
        MobileInputUI mobile = GameObject.Find("MobileInputUI").GetComponent<MobileInputUI>();
        if (safe == null || mobile == null) throw new InvalidOperationException("Shared mobile controls are missing.");
        Transform background = safe.Find("Gameplay_Interface/Joystick/Background");
        Transform handle = safe.Find("Gameplay_Interface/Joystick/Background/Handle");
        if (background == null || handle == null) throw new InvalidOperationException("Shared joystick RectTransforms are missing.");
        Set(mobile, "joystickBackground", background.GetComponent<RectTransform>());
        Set(mobile, "joystickHandle", handle.GetComponent<RectTransform>());
        foreach (string path in new[] { "InventoryBar", "Gameplay_Interface/HealthBar",
            "Gameplay_Interface/ParchmentPaper", "Gameplay_Interface/ObjectivesToggleButton",
            "Gameplay_Interface/SettingsButton", "Gameplay_Interface/TimerText" })
        {
            Transform item = safe.Find(path);
            if (item != null) item.gameObject.SetActive(false);
        }
        Transform controls = safe.Find("Gameplay_Interface");
        foreach (string name in new[] { "Joystick", "JumpButton", "InteractButton" })
        {
            Transform item = controls.Find(name);
            if (item != null) item.gameObject.SetActive(true);
        }
    }

    private static TMP_Text BuildBoardText(Transform parent)
    {
        WorldText("Trial Board Header", parent, "TRIAL OF PROOFS",
            new Vector3(68.8f, 15.2f, 42.05f), new Vector2(7.2f, .70f), 3.2f,
            new Color(.19f, .11f, .07f));
        return WorldText("Current Proof Problem", parent, "Activate the Book Statue to begin.",
            new Vector3(68.8f, 11.30f, 42.07f), new Vector2(7.4f, 4.25f), 3.4f,
            new Color(.18f, .10f, .06f));
    }

    private static GameObject BuildMarker(Transform parent, string name, string label,
        Material material, Vector3 position, out Renderer renderer)
    {
        GameObject marker = new GameObject(name);
        marker.transform.SetParent(parent, false);
        marker.transform.position = position;
        GameObject seal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        seal.name = "Glowing Seal";
        seal.transform.SetParent(marker.transform, false);
        seal.transform.localScale = new Vector3(2.4f, .30f, .25f);
        UnityEngine.Object.DestroyImmediate(seal.GetComponent<Collider>());
        renderer = seal.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        WorldText("Station Label", marker.transform, label, position + new Vector3(0f, -.55f, .21f),
            new Vector2(3.8f, .7f), 2.3f, Color.white);
        return marker;
    }

    private static GameObject Spawn(GameObject prefab, Transform parent, string name,
        Vector3 position, float yaw, float scale)
    {
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = Vector3.one * scale;
        return go;
    }

    private sealed class HudRefs
    {
        public TMP_Text timer, progress, prompt, selection, readerTitle, readerBody, resultsText;
        public GameObject readerPanel, resultsPanel;
        public Button selectButton, closeButton, retryButton, menuButton;
    }

    private static HudRefs BuildHud()
    {
        HudRefs hud = new HudRefs();
        GameObject go = new GameObject("Trial of Proofs HUD", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 12;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;

        hud.timer = HudText("Shared Timer", go.transform, "10:00", new Vector2(-155f, -78f),
            new Vector2(260f, 75f), new Vector2(1f, 1f), 58f, new Color(1f, .83f, .52f));
        hud.progress = HudText("Challenge Progress", go.transform, "CHALLENGE 1 / 5",
            new Vector2(195f, -82f), new Vector2(340f, 62f), new Vector2(0f, 1f), 37f,
            new Color(.90f, .87f, .75f));
        hud.prompt = HudText("Interaction Prompt", go.transform, "Approach the Book Statue to begin.",
            new Vector2(0f, -82f), new Vector2(1040f, 64f), new Vector2(.5f, 1f), 33f,
            new Color(1f, .91f, .71f));
        hud.selection = HudText("Proof Selection", go.transform, "Timer paused • Activate the Book Statue",
            new Vector2(0f, -143f), new Vector2(1100f, 58f), new Vector2(.5f, 1f), 29f,
            new Color(.91f, .86f, .73f));

        hud.readerPanel = Panel("Proof Reader", go.transform, new Vector2(1140f, 640f));
        hud.readerTitle = HudText("Reader Title", hud.readerPanel.transform, "PROOF TABLET",
            new Vector2(0f, 248f), new Vector2(1010f, 72f), new Vector2(.5f, .5f), 47f,
            new Color(1f, .84f, .53f));
        hud.readerBody = HudText("Reader Body", hud.readerPanel.transform, "",
            new Vector2(0f, 10f), new Vector2(1010f, 385f), new Vector2(.5f, .5f), 38f,
            new Color(.97f, .92f, .80f));
        hud.selectButton = HudButton("Select Tablet", hud.readerPanel.transform, "SELECT TABLET",
            new Vector2(-155f, -255f));
        hud.closeButton = HudButton("Close Reader", hud.readerPanel.transform, "CLOSE",
            new Vector2(155f, -255f));
        hud.readerPanel.SetActive(false);

        hud.resultsPanel = Panel("Trial Results", go.transform, new Vector2(1000f, 475f));
        hud.resultsText = HudText("Result Message", hud.resultsPanel.transform, "TRIAL COMPLETE",
            new Vector2(0f, 75f), new Vector2(880f, 245f), new Vector2(.5f, .5f), 45f,
            new Color(1f, .85f, .57f));
        hud.retryButton = HudButton("Retry Trial", hud.resultsPanel.transform, "RETRY",
            new Vector2(-160f, -160f));
        hud.menuButton = HudButton("Return to Menu", hud.resultsPanel.transform, "MAIN MENU",
            new Vector2(160f, -160f));
        hud.resultsPanel.SetActive(false);
        return hud;
    }

    private static GameObject Panel(string name, Transform parent, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        go.GetComponent<Image>().color = new Color(.045f, .075f, .14f, .97f);
        return go;
    }

    private static Button HudButton(string name, Transform parent, string label, Vector2 position)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(285f, 80f);
        go.GetComponent<Image>().color = new Color(.12f, .35f, .53f);
        HudText("Label", go.transform, label, Vector2.zero, new Vector2(270f, 72f),
            new Vector2(.5f, .5f), 33f, Color.white);
        return go.GetComponent<Button>();
    }

    private static TMP_Text HudText(string name, Transform parent, string content,
        Vector2 position, Vector2 size, Vector2 anchor, float fontSize, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TMP_Text text = go.GetComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = fontSize * .65f;
        text.fontSizeMax = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static TMP_Text WorldText(string name, Transform parent, string content,
        Vector3 position, Vector2 size, float fontSize, Color color)
    {
        GameObject go = new GameObject(name, typeof(TextMeshPro));
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        TextMeshPro text = go.GetComponent<TextMeshPro>();
        text.text = content;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = fontSize * .52f;
        text.fontSizeMax = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.rectTransform.sizeDelta = size;
        text.color = color;
        return text;
    }

    private static Material Unlit(string name, Color color)
    {
        string path = MaterialFolder + name + ".mat";
        Material result = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (result != null) return result;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) throw new InvalidOperationException("URP Unlit shader is missing.");
        result = new Material(shader);
        result.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(result, path);
        return result;
    }

    private static GameObject EnsureTabletPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(TabletPrefabPath);
        if (existing != null) return existing;
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(BasePedestalPath);
        if (source == null) throw new InvalidOperationException("Shared ProofWorldPedestal prefab is missing.");
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        instance.name = "ProofTablet";
        instance.AddComponent<TrialProofTablet>();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, TabletPrefabPath);
        UnityEngine.Object.DestroyImmediate(instance);
        return prefab;
    }

    private static DirectIndirectProofCatalog EnsureCatalog()
    {
        DirectIndirectProofCatalog catalog = AssetDatabase.LoadAssetAtPath<DirectIndirectProofCatalog>(CatalogPath);
        if (catalog != null) return catalog;
        catalog = ScriptableObject.CreateInstance<DirectIndirectProofCatalog>();
        catalog.challenges = new[]
        {
            new TrialProofChallenge
            {
                kind = TrialProofKind.IdentifyMethod,
                title = "Identify the proof method",
                problem = "If n² is even, then n is even. An argument assumes n is odd and derives that n² is odd. Is it a direct or an indirect proof? Activate the matching altar, then confirm at the Book Statue.",
                explanation = "It proves the contrapositive: if n is odd, then n² is odd. This is logically equivalent to the original statement.",
                station = TrialProofMethod.Contrapositive,
                correctMethod = TrialProofMethod.Contrapositive
            },
            new TrialProofChallenge
            {
                kind = TrialProofKind.ArrangeSteps,
                title = "Construct a direct proof",
                problem = "Prove directly that the sum of two even integers is even. Interact with the Direct Proof altar, then drag the five steps into logical order on its board. Confirm at the Book Statue.",
                explanation = "A direct proof starts with even integers, rewrites their sum as twice an integer, and reaches the conclusion.",
                station = TrialProofMethod.Direct,
                tablets = new[]
                {
                    Tablet("even-a", "Let a and b be even integers.", "Begin with the given assumption."),
                    Tablet("even-b", "a = 2m and b = 2n for integers m and n.", "Use the definition of an even integer."),
                    Tablet("even-c", "a + b = 2m + 2n.", "Substitute the representations of a and b."),
                    Tablet("even-d", "a + b = 2(m + n).", "Factor out 2."),
                    Tablet("even-e", "Since m + n is an integer, a + b is even.", "Apply the definition of even again.")
                },
                displayOrder = new[] { 2, 4, 0, 3, 1 },
                acceptedOrders = new[] { Order(0, 1, 2, 3, 4) }
            },
            new TrialProofChallenge
            {
                kind = TrialProofKind.ArrangeSteps,
                title = "Prove the contrapositive",
                problem = "Prove: if n² is even, then n is even. Use the equivalent contrapositive: if n is odd, then n² is odd. Interact with the Indirect Proof altar and drag all five steps into order.",
                explanation = "For odd n = 2k + 1, its square has the form 2r + 1. Therefore n² is odd, establishing the contrapositive.",
                station = TrialProofMethod.Contrapositive,
                tablets = new[]
                {
                    Tablet("odd-a", "Assume n is odd.", "Negate the original conclusion."),
                    Tablet("odd-b", "n = 2k + 1 for some integer k.", "Use the definition of odd."),
                    Tablet("odd-c", "n² = (2k + 1)² = 4k² + 4k + 1.", "Expand the square."),
                    Tablet("odd-d", "n² = 2(2k² + 2k) + 1.", "Factor out 2 from the even part."),
                    Tablet("odd-e", "Thus n² is odd, proving the contrapositive.", "The term 2k² + 2k is an integer.")
                },
                displayOrder = new[] { 3, 0, 4, 1, 2 },
                acceptedOrders = new[] { Order(0, 1, 2, 3, 4) }
            },
            new TrialProofChallenge
            {
                kind = TrialProofKind.ArrangeSteps,
                title = "Proof by contradiction",
                problem = "Prove that there is no greatest integer. Assume the opposite, derive an integer larger than the supposed greatest, and arrange the five tablets.",
                explanation = "Assuming a greatest integer N gives N + 1, an even larger integer. That contradiction rules out a greatest integer.",
                station = TrialProofMethod.Contradiction,
                tablets = new[]
                {
                    Tablet("greatest-a", "Assume a greatest integer N exists.", "Assume the negation of the conclusion."),
                    Tablet("greatest-b", "N + 1 is also an integer.", "Integers are closed under addition."),
                    Tablet("greatest-c", "N + 1 > N.", "Adding 1 makes a larger integer."),
                    Tablet("greatest-d", "This contradicts N being the greatest integer.", "The assumption and result cannot both hold."),
                    Tablet("greatest-e", "Therefore, no greatest integer exists.", "Reject the contradictory assumption.")
                },
                displayOrder = new[] { 4, 2, 0, 3, 1 },
                acceptedOrders = new[] { Order(0, 1, 2, 3, 4) }
            },
            new TrialProofChallenge
            {
                kind = TrialProofKind.ChooseAndArrange,
                title = "Choose and complete a proof",
                problem = "Prove that the sum of two odd integers is even. Choose a proof strategy, then arrange the five tablets in a valid argument.",
                explanation = "This trial uses a direct derivation. An indirect proof is possible mathematically, but the authored tablet sequence establishes the claim directly.",
                station = TrialProofMethod.Direct,
                correctMethod = TrialProofMethod.Direct,
                tablets = new[]
                {
                    Tablet("sum-a", "Let a and b be odd integers.", "Begin with the given assumption."),
                    Tablet("sum-b", "a = 2m + 1 and b = 2n + 1 for integers m and n.", "Use the definition of odd integers."),
                    Tablet("sum-c", "a + b = (2m + 1) + (2n + 1) = 2m + 2n + 2.", "Substitute and simplify."),
                    Tablet("sum-d", "a + b = 2(m + n + 1).", "Factor out 2."),
                    Tablet("sum-e", "Since m + n + 1 is an integer, a + b is even.", "Apply the definition of even.")
                },
                displayOrder = new[] { 4, 1, 3, 0, 2 },
                acceptedOrders = new[] { Order(0, 1, 2, 3, 4) }
            }
        };
        AssetDatabase.CreateAsset(catalog, CatalogPath);
        return catalog;
    }

    private static TrialProofTabletData Tablet(string id, string statement, string why)
    {
        return new TrialProofTabletData { id = id, statement = statement, justification = why };
    }

    private static TrialProofOrder Order(params int[] ids)
    {
        return new TrialProofOrder { tabletIds = ids };
    }

    private static void Set(UnityEngine.Object owner, string field, UnityEngine.Object value)
    {
        SerializedObject serialized = new SerializedObject(owner);
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null) throw new InvalidOperationException(owner.name + " lacks field " + field);
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetArray<T>(UnityEngine.Object owner, string field, T[] values) where T : UnityEngine.Object
    {
        SerializedObject serialized = new SerializedObject(owner);
        SerializedProperty array = serialized.FindProperty(field);
        if (array == null) throw new InvalidOperationException(owner.name + " lacks array " + field);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
