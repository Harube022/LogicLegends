using System;
using System.Linq;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ConfigureFormalInformalProofsScene
{
    private const string Dungeon = "Assets/CHEATCODE/DUNGEON/LowPolyDungeon/Prefabs/";
    private const string MaterialFolder = "Assets/Materials/FormalInformalProofs";
    private const string PrefabFolder = "Assets/Prefabs/PREFABS/FormalInformalProofs";

    [MenuItem("Logic Legends/Configure Formal vs. Informal Proofs Scene")]
    public static void Configure()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "Formal vs. Informal Proofs")
            throw new InvalidOperationException("Open the Formal vs. Informal Proofs scene first.");
        if (GameObject.Find("ProofsLibrary") != null)
            throw new InvalidOperationException("ProofsLibrary already exists. Preserve scene edits before rebuilding it.");
        GameObject originalStage = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Stage");
        if (originalStage != null) originalStage.SetActive(false);

        EnsureFolder("Assets/Materials", "FormalInformalProofs");
        EnsureFolder("Assets/Prefabs/PREFABS", "FormalInformalProofs");
        Texture stoneTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/FINAL_ASSETS/MAPS/REMADE/S2L4/841864612_3063346467206057_2078166620475297814_n.png");
        if (stoneTexture == null) throw new InvalidOperationException("S2L4 stone texture is missing.");

        Material stone = Lit("Library_Stone", new Color(.85f, .9f, 1f), stoneTexture);
        Material darkStone = Lit("Library_DarkStone", new Color(.25f, .31f, .47f), stoneTexture);
        Material wood = Lit("Library_Wood", new Color(.35f, .19f, .10f), null);
        Material parchment = Lit("Library_Parchment", new Color(.94f, .78f, .52f), null);
        Material gold = Lit("Library_Gold", new Color(.74f, .43f, .13f), null);
        Material blue = Lit("Formal_Blue", new Color(.08f, .34f, .80f), null);
        Material red = Lit("Informal_Red", new Color(.72f, .11f, .09f), null);
        Material blueGlow = Unlit("Proof_Emblem", new Color(.14f, .50f, .90f));
        Material redGlow = Unlit("Informal_Emblem", new Color(.90f, .20f, .12f));
        Material flame = Unlit("Torch_Flame", new Color(1.35f, .57f, .08f));

        GameObject scrollPrefab = BuildScrollPrefab(wood, parchment, gold);
        GameObject pedestalPrefab = BuildPedestalPrefab(darkStone, blue, blueGlow, gold);
        GameObject root = new GameObject("ProofsLibrary");
        BuildRoom(root.transform, stone, darkStone, wood, gold, flame);

        GameObject book = NormalizedPrefab(root.transform, "Book Statue",
            "Assets/Prefabs/Book_Statue.prefab", new Vector3(0, 0, 3.2f), 0f, .92f);
        BoxCollider bookCollider = book.AddComponent<BoxCollider>();
        bookCollider.center = new Vector3(0, 2.3f, 0);
        bookCollider.size = new Vector3(3f, 4.6f, 2.2f);

        GameObject board = NormalizedPrefab(root.transform, "Proof Information Board",
            "Assets/FINAL_ASSETS/MAPS/REMADE/S2L4/Board.fbx", new Vector3(10.1f, 0, 3.2f), 180f, .57f);
        BoxCollider boardCollider = board.AddComponent<BoxCollider>();
        boardCollider.center = new Vector3(0, 2.3f, 0);
        boardCollider.size = new Vector3(5.3f, 4.6f, 1.1f);
        TMP_Text boardTitle = WorldText("Board Header", root.transform, "PROOFS LIBRARY",
            new Vector3(10.1f, 4.28f, 2.58f), new Vector2(4.7f, .65f), 4.2f,
            new Color(.26f, .12f, .05f));
        TMP_Text boardText = WorldText("Current Challenge", root.transform,
            "Activate the Book Statue to reveal Challenge 1.",
            new Vector3(10.1f, 2.25f, 2.48f), new Vector2(4.5f, 2.8f), 3.3f,
            new Color(.21f, .10f, .05f));

        GameObject archive = NormalizedPrefab(root.transform, "Proof Archive",
            "Assets/FINAL_ASSETS/MAPS/REMADE/S2L4/StatementTable.fbx",
            new Vector3(-10.1f, 0, 3.4f), 180f, .54f);
        BoxCollider archiveCollider = archive.AddComponent<BoxCollider>();
        archiveCollider.center = new Vector3(0, 2.6f, 0);
        archiveCollider.size = new Vector3(3.3f, 5.2f, .9f);
        WorldText("Archive Heading", root.transform, "PROOF ARCHIVE\nREAD • SORT • CONFIRM",
            new Vector3(-10.1f, 3.85f, 2.82f), new Vector2(2.8f, 1.2f), 2.9f,
            new Color(.23f, .12f, .06f));

        Transform[] pedestals = new Transform[2];
        Renderer[] emblems = new Renderer[2];
        TMP_Text[] counts = new TMP_Text[2];
        for (int i = 0; i < 2; i++)
        {
            GameObject pedestal = (GameObject)PrefabUtility.InstantiatePrefab(pedestalPrefab);
            pedestal.name = i == 0 ? "Formal Proof Pedestal" : "Informal Proof Pedestal";
            pedestal.transform.SetParent(root.transform, false);
            pedestal.transform.localPosition = new Vector3(i == 0 ? -5f : 5f, 0, -3.2f);
            pedestal.transform.Find("Color Band").GetComponent<Renderer>().sharedMaterial = i == 0 ? blue : red;
            pedestal.transform.Find("Emblem Letter").GetComponent<TMP_Text>().text = i == 0 ? "F" : "I";
            pedestal.transform.Find("Pedestal Label").GetComponent<TMP_Text>().text =
                i == 0 ? "FORMAL PROOF" : "INFORMAL PROOF";
            pedestals[i] = pedestal.transform;
            emblems[i] = pedestal.transform.Find("Emblem").GetComponent<Renderer>();
            emblems[i].sharedMaterial = i == 0 ? blueGlow : redGlow;
            counts[i] = pedestal.transform.Find("Scroll Count").GetComponent<TMP_Text>();
        }

        string[] statements =
        {
            "Let n = 2k. Then n² = 4k² = 2(2k²). Hence n² is even.",
            "Two odd numbers leave one extra each. The extras pair, so their sum is even."
        };
        Transform[] scrolls = new Transform[2];
        TMP_Text[] statuses = new TMP_Text[2];
        for (int i = 0; i < 2; i++)
        {
            GameObject scroll = (GameObject)PrefabUtility.InstantiatePrefab(scrollPrefab);
            scroll.name = "Proof Scroll " + (i + 1);
            scroll.transform.SetParent(root.transform, false);
            scroll.transform.localPosition = new Vector3(-8.1f + i * 4.5f, 3.15f, 8.35f);
            scroll.transform.Find("Proof Text").GetComponent<TMP_Text>().text = statements[i];
            scrolls[i] = scroll.transform;
            statuses[i] = scroll.transform.Find("Classification Status").GetComponent<TMP_Text>();
        }

        BuildLibrarySign(root.transform, darkStone, gold, blueGlow);
        TMP_Text timer, prompt, resultsText;
        GameObject resultsPanel;
        Button retryButton, menuButton;
        BuildHud(out timer, out prompt, out resultsPanel, out resultsText,
            out retryButton, out menuButton);
        BuildPlayerControls();
        EnsureAdminDebug();

        GameObject managerObject = new GameObject("ProofsLibraryStageManager");
        var manager = managerObject.AddComponent<ProofsLibraryStageController>();
        var audio = managerObject.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;
        Set(manager, "bookStatue", book.transform);
        SetArray(manager, "scrolls", scrolls);
        SetArray(manager, "scrollStatus", statuses);
        SetArray(manager, "pedestals", pedestals);
        SetArray(manager, "pedestalEmblems", emblems);
        SetArray(manager, "pedestalCounts", counts);
        Set(manager, "boardText", boardText);
        Set(manager, "timerText", timer);
        Set(manager, "promptText", prompt);
        Set(manager, "resultsPanel", resultsPanel);
        Set(manager, "resultsText", resultsText);
        Set(manager, "retryButton", retryButton);
        Set(manager, "menuButton", menuButton);
        Set(manager, "audioSource", audio);
        Set(manager, "correctSound", AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Assets/Sounds Effects/WORLDBGM/SOFTWIN.mp3"));
        Set(manager, "incorrectSound", AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Assets/Sounds Effects/WORLDBGM/DAMAGETAKEN1.mp3"));
        ApplyFiveChallengeScenario(root, manager);
        ArrangeLayout(root, manager);
        ConfigurePhysicalProofStations.EnsureSceneObjects();

        Camera camera = Camera.main;
        camera.transform.position = new Vector3(0, 5.9f, -17.1f);
        camera.transform.LookAt(new Vector3(0, 3.15f, 1.0f));
        Transform virtualCamera = GameObject.Find("CM_ThirdPersonCam").transform;
        virtualCamera.position = camera.transform.position;
        virtualCamera.rotation = camera.transform.rotation;
        camera.fieldOfView = 64f;
        camera.farClipPlane = 85f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.06f, .09f, .17f);
        if (camera.GetComponent<CinemachineBrain>() == null) camera.gameObject.AddComponent<CinemachineBrain>();
        Light light = UnityEngine.Object.FindFirstObjectByType<Light>();
        if (light != null)
        {
            light.transform.rotation = Quaternion.Euler(45f, -25f, 0);
            light.color = new Color(1f, .83f, .69f);
            light.intensity = 1.55f;
        }
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.70f, .69f, .75f);
        RenderSettings.fog = false;

        string path = "Assets/Scenes/Formal vs. Informal Proofs.unity";
        if (!EditorBuildSettings.scenes.Any(s => s.path == path))
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Proofs Library scene built with five proof challenges, scrolls, step board, Book, timer, and mobile controls.");
    }

    [MenuItem("Logic Legends/Update Proofs Library Five-Challenge Scenario")]
    public static void UpgradeExistingScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "Formal vs. Informal Proofs")
            throw new InvalidOperationException("Open the Formal vs. Informal Proofs scene first.");
        GameObject root = GameObject.Find("ProofsLibrary");
        var manager = UnityEngine.Object.FindFirstObjectByType<ProofsLibraryStageController>();
        if (root == null || manager == null) throw new InvalidOperationException("Build the Proofs Library scene first.");
        ApplyFiveChallengeScenario(root, manager);
        ConfigurePhysicalProofStations.EnsureSceneObjects();
        EnsureAdminDebug();
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Proofs Library upgraded to five sequential challenges.");
    }

    [MenuItem("Logic Legends/Add Proofs Library Admin Button")]
    public static void AddAdminButtonToExistingScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "Formal vs. Informal Proofs" || EditorApplication.isPlaying)
            throw new InvalidOperationException("Open the Formal vs. Informal Proofs scene in Edit mode first.");
        EnsureAdminDebug();
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Proofs Library Admin button is ready; use Admin Button Enabled to show or hide it.");
    }

    private static void EnsureAdminDebug()
    {
        GameObject hud = GameObject.Find("Proofs HUD");
        if (hud == null || hud.GetComponent<Canvas>() == null)
            throw new InvalidOperationException("Proofs HUD Canvas is required for the Admin button.");
        GameObject adminObject = SceneManager.GetActiveScene().GetRootGameObjects()
            .FirstOrDefault(go => go.name == "Admin Debug");
        bool created = adminObject == null;
        if (created) adminObject = new GameObject("Admin Debug");
        AdminDebugController admin = adminObject.GetComponent<AdminDebugController>();
        if (admin == null) admin = adminObject.AddComponent<AdminDebugController>();
        var serialized = new SerializedObject(admin);
        serialized.FindProperty("uiCanvas").objectReferenceValue = hud.GetComponent<Canvas>();
        if (created)
        {
            serialized.FindProperty("featureEnabled").boolValue = true;
            serialized.FindProperty("allowInPlayerBuilds").boolValue = true;
            serialized.FindProperty("adminButtonEnabled").boolValue = true;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private static void ApplyFiveChallengeScenario(GameObject root, ProofsLibraryStageController manager)
    {
        Material wood = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/Library_Wood.mat");
        Material gold = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/Library_Gold.mat");
        if (root.transform.Find("Central Proof Table") == null)
        {
            Transform table = new GameObject("Central Proof Table").transform;
            table.SetParent(root.transform, false);
            table.localPosition = new Vector3(0, 0, -1f);
            Box("Tabletop", table, new Vector3(0, 1.2f, 0), new Vector3(8.2f, .20f, 2.4f), wood, true);
            Box("Gold Table Edge", table, new Vector3(0, 1.12f, -1.17f), new Vector3(8.25f, .10f, .10f), gold, false);
            foreach (float x in new[] { -3.65f, 3.65f })
                foreach (float z in new[] { -.85f, .85f })
                    Box("Table Leg", table, new Vector3(x, .58f, z), new Vector3(.28f, 1.16f, .28f), wood, true);
        }

        Transform existingTable = root.transform.Find("Central Proof Table");
        existingTable.Find("Tabletop").localScale = new Vector3(8.2f, .20f, 2.4f);
        existingTable.Find("Gold Table Edge").localScale = new Vector3(8.25f, .10f, .10f);
        existingTable.Find("Gold Table Edge").localPosition = new Vector3(0, 1.12f, -1.17f);
        int legIndex = 0;
        foreach (Transform child in existingTable)
            if (child.name == "Table Leg")
            {
                child.localPosition = new Vector3(legIndex < 2 ? -3.65f : 3.65f, .58f,
                    legIndex % 2 == 0 ? -.85f : .85f);
                legIndex++;
            }

        Transform[] scrolls = new Transform[4];
        TMP_Text[] statuses = new TMP_Text[4];
        string[] previews =
        {
            "SCROLL A\nP, P → Q\nTherefore Q",
            "SCROLL B\nP implies Q. P holds.\nTherefore Q.",
            "SCROLL C\nFormal proof",
            "SCROLL D\nInformal proof"
        };
        GameObject scrollPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/ProofScrollDisplay.prefab");
        for (int i = 0; i < 4; i++)
        {
            Transform scroll = root.transform.Find("Proof Scroll " + (i + 1));
            if (scroll == null) scroll = root.transform.Find("Scroll " + (char)('A' + i));
            if (scroll == null && i >= 2)
            {
                GameObject made = (GameObject)PrefabUtility.InstantiatePrefab(scrollPrefab);
                made.transform.SetParent(root.transform, false);
                scroll = made.transform;
            }
            if (scroll == null) throw new InvalidOperationException("Missing source proof scroll " + (i + 1));
            scroll.name = "Scroll " + (char)('A' + i);
            scroll.localPosition = new Vector3(-2.85f + i * 1.9f, 1.33f, -1.03f);
            scroll.localRotation = Quaternion.identity;
            scroll.localScale = Vector3.one * .42f;
            scroll.gameObject.SetActive(true);
            scroll.Find("Proof Text").GetComponent<TMP_Text>().text = previews[i];
            scrolls[i] = scroll;
            statuses[i] = scroll.Find("Classification Status").GetComponent<TMP_Text>();
        }
        foreach (string oldName in new[] { "Proof Scroll 3", "Proof Scroll 4" })
        {
            Transform extra = root.transform.Find(oldName);
            if (extra != null) UnityEngine.Object.DestroyImmediate(extra.gameObject);
        }

        GameObject smallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Dungeon + "Clutter/Scroll_Small.prefab");
        if (smallPrefab == null) throw new InvalidOperationException("Missing small carried scroll prefab.");
        GameObject[] visuals = new GameObject[4];
        for (int i = 0; i < 4; i++)
        {
            string name = "Carried Scroll " + (char)('A' + i);
            Transform existing = root.transform.Find(name);
            GameObject visual = existing != null ? existing.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(smallPrefab);
            visual.name = name;
            visual.transform.SetParent(root.transform, false);
            visual.SetActive(false);
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            visuals[i] = visual;
        }

        GameObject hud = GameObject.Find("Proofs HUD");
        if (hud == null) throw new InvalidOperationException("Missing Proofs HUD.");
        if (hud.GetComponent<GraphicRaycaster>() == null) hud.AddComponent<GraphicRaycaster>();
        Transform reader = hud.transform.Find("Scroll Reader");
        if (reader == null)
        {
            GameObject panel = new GameObject("Scroll Reader", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(hud.transform, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(1020f, 660f);
            panel.GetComponent<Image>().color = new Color(.07f, .10f, .16f, .98f);
            HudText("Scroll Title", panel.transform, "SCROLL A", new Vector2(0, 255f),
                new Vector2(900f, 72f), new Vector2(.5f, .5f), 52f, new Color(1f, .80f, .45f));
            TMP_Text body = HudText("Scroll Body", panel.transform, "", new Vector2(0, 5f),
                new Vector2(880f, 410f), new Vector2(.5f, .5f), 35f, new Color(.98f, .89f, .74f));
            body.alignment = TextAlignmentOptions.TopLeft;
            HudButton("Carry Scroll", panel.transform, "CARRY SCROLL", new Vector2(-170f, -265f));
            HudButton("Close", panel.transform, "CLOSE", new Vector2(170f, -265f));
            reader = panel.transform;
        }
        reader.gameObject.SetActive(false);
        SetArray(manager, "scrolls", scrolls);
        SetArray(manager, "scrollStatus", statuses);
        Set(manager, "catalog", BuildProofCatalog());
        Set(manager, "stepBoardWorld", root.transform.Find("Proof Information Board"));
        Transform archiveHeading = root.transform.Find("Archive Heading");
        if (archiveHeading != null) archiveHeading.GetComponent<TMP_Text>().text =
            "PROOF STEP BOARD\nEDIT • REORDER • CONFIRM";
        SetArray(manager, "carriedVisuals", visuals);
        Set(manager, "readerPanel", reader.gameObject);
        Set(manager, "readerTitle", reader.Find("Scroll Title").GetComponent<TMP_Text>());
        Set(manager, "readerBody", reader.Find("Scroll Body").GetComponent<TMP_Text>());
        Set(manager, "carryButton", reader.Find("Carry Scroll").GetComponent<Button>());
        Set(manager, "closeButton", reader.Find("Close").GetComponent<Button>());
        Transform edit = hud.transform.Find("Edit Proof Board");
        if (edit == null) edit = HudButton("Edit Proof Board", hud.transform, "EDIT PROOF",
            new Vector2(0, -145f)).transform;
        edit.gameObject.SetActive(false);
        Set(manager, "editBoardButton", edit.GetComponent<Button>());
        Transform resultPanel = hud.transform.Find("Proof Results");
        if (resultPanel != null)
        {
            resultPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(1120, 560);
            resultPanel.Find("Result Message").GetComponent<RectTransform>().sizeDelta = new Vector2(1040, 360);
            resultPanel.Find("Result Message").GetComponent<TMP_Text>().fontSize = 37;
            resultPanel.Find("Retry").GetComponent<RectTransform>().anchoredPosition = new Vector2(-150, -215);
            resultPanel.Find("Main Menu").GetComponent<RectTransform>().anchoredPosition = new Vector2(150, -215);
        }
        var serialized = new SerializedObject(manager);
        serialized.FindProperty("interactionRange").floatValue = 3f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        ArrangeLayout(root, manager);
    }

    [MenuItem("Logic Legends/Arrange Proofs Library Layout")]
    public static void ArrangeExistingLayout()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "Formal vs. Informal Proofs")
            throw new InvalidOperationException("Open the Formal vs. Informal Proofs scene first.");
        GameObject root = GameObject.Find("ProofsLibrary");
        var manager = UnityEngine.Object.FindFirstObjectByType<ProofsLibraryStageController>();
        if (root == null || manager == null) throw new InvalidOperationException("The Proofs Library scene is not configured.");
        ArrangeLayout(root, manager);
        ConfigurePhysicalProofStations.EnsureSceneObjects();
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Proofs Library layout arranged with Book, scroll pedestals, side altars, and enlarged board.");
    }

    private static void ArrangeLayout(GameObject root, ProofsLibraryStageController manager)
    {
        Transform sceneRoot = root.transform;
        Transform book = sceneRoot.Find("Book Statue");
        Transform board = sceneRoot.Find("Proof Information Board");
        Transform table = sceneRoot.Find("Central Proof Table");
        Transform formal = sceneRoot.Find("Formal Proof Pedestal");
        Transform informal = sceneRoot.Find("Informal Proof Pedestal");
        if (book == null || board == null || formal == null || informal == null)
            throw new InvalidOperationException("The Proofs Library layout is missing its Book, board, or altar.");

        RecenterVisualAndCollider(book, new Vector3(0, 0, -8.3f),
            Vector3.one * .55f, new Vector3(0, 1.14f, -8.3f), .12f);
        RecenterVisualAndCollider(board, new Vector3(0, 0, 12.55f),
            new Vector3(.88f, .64f, .57f), new Vector3(0, 3.7f, 12.55f), .08f);
        formal.localPosition = new Vector3(-6.3f, 0, 3.5f);
        informal.localPosition = new Vector3(6.3f, 0, 3.5f);
        if (table != null) UnityEngine.Object.DestroyImmediate(table.gameObject);

        Bounds boardBounds = RendererBounds(board);
        Transform header = sceneRoot.Find("Board Header");
        Transform body = sceneRoot.Find("Current Challenge");
        if (header != null)
        {
            header.position = new Vector3(boardBounds.center.x, boardBounds.max.y - .74f, boardBounds.min.z - .08f);
            header.GetComponent<RectTransform>().sizeDelta = new Vector2(7.4f, .65f);
        }
        if (body != null)
        {
            body.position = new Vector3(boardBounds.center.x, boardBounds.center.y - .48f, boardBounds.min.z - .09f);
            body.GetComponent<RectTransform>().sizeDelta = new Vector2(7.2f, 3.55f);
        }

        foreach (string name in new[] { "Proof Archive", "Archive Heading", "Library Sign", "Sign Gold Rail", "Archive Crystal" })
        {
            Transform decoration = sceneRoot.Find(name);
            if (decoration != null) decoration.gameObject.SetActive(false);
        }
        Transform oldSignText = sceneRoot.Find("Library Sign Text");
        if (oldSignText != null) oldSignText.gameObject.SetActive(false);

        Set(manager, "bookStatue", book);
        Set(manager, "stepBoardWorld", board);
        Camera camera = Camera.main;
        if (camera != null)
        {
            camera.transform.position = new Vector3(0, 5.9f, -17.1f);
            camera.transform.LookAt(new Vector3(0, 3.15f, 1.0f));
            camera.fieldOfView = 64f;
            Transform virtualCamera = GameObject.Find("CM_ThirdPersonCam")?.transform;
            if (virtualCamera != null)
            {
                virtualCamera.position = camera.transform.position;
                virtualCamera.rotation = camera.transform.rotation;
            }
        }
        Physics.SyncTransforms();
    }

    private static Bounds RendererBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException("No visible model under " + root.name);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static void RecenterVisualAndCollider(Transform holder, Vector3 rootPosition,
        Vector3 rootScale, Vector3 visualCenter, float padding)
    {
        if (holder.childCount == 0) throw new InvalidOperationException("Missing model under " + holder.name);
        Transform model = holder.GetChild(0);
        holder.localPosition = rootPosition;
        holder.localScale = rootScale;
        Bounds bounds = RendererBounds(model);
        model.position += visualCenter - bounds.center;
        bounds = RendererBounds(model);
        BoxCollider collider = holder.GetComponent<BoxCollider>();
        if (collider == null) collider = holder.gameObject.AddComponent<BoxCollider>();
        collider.center = holder.InverseTransformPoint(bounds.center);
        Vector3 size = bounds.size + Vector3.one * padding;
        Vector3 scale = holder.lossyScale;
        collider.size = new Vector3(size.x / Mathf.Abs(scale.x), size.y / Mathf.Abs(scale.y), size.z / Mathf.Abs(scale.z));
        collider.isTrigger = false;
    }

    private static void BuildRoom(Transform parent, Material stone, Material darkStone,
        Material wood, Material gold, Material flame)
    {
        Transform room = new GameObject("Library Architecture").transform;
        room.SetParent(parent, false);
        Box("Foundation", room, new Vector3(0, -.31f, -.5f), new Vector3(28f, .6f, 30f), darkStone, true);
        GameObject tilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Dungeon + "Tiles/FloorTIle.prefab");
        for (int x = -2; x <= 2; x++)
            for (int z = -2; z <= 2; z++)
            {
                GameObject tile = (GameObject)PrefabUtility.InstantiatePrefab(tilePrefab);
                tile.name = "Stone Tile " + (x + 2) + "-" + (z + 2);
                tile.transform.SetParent(room, false);
                tile.transform.localPosition = new Vector3(x * 4f, .04f, z * 4f - .5f);
            }
        Box("Back Stone Wall", room, new Vector3(0, 4.45f, 14.2f), new Vector3(28f, 8.9f, .7f), stone, true);
        Box("Left Stone Wall", room, new Vector3(-14.1f, 4.45f, -.5f), new Vector3(.7f, 8.9f, 30f), stone, true);
        Box("Right Stone Wall", room, new Vector3(14.1f, 4.45f, -.5f), new Vector3(.7f, 8.9f, 30f), stone, true);
        Box("Front Left Wall", room, new Vector3(-10.5f, 4.45f, -15.2f), new Vector3(7f, 8.9f, .7f), stone, true);
        Box("Front Right Wall", room, new Vector3(10.5f, 4.45f, -15.2f), new Vector3(7f, 8.9f, .7f), stone, true);
        Box("Ceiling", room, new Vector3(0, 9.0f, -.5f), new Vector3(28f, .55f, 30f), darkStone, true);
        for (int i = -1; i <= 1; i++)
        {
            float x = i * 9.1f;
            Column(room, new Vector3(x, 0, 13.45f), darkStone, gold);
        }
        for (int side = -1; side <= 1; side += 2)
        {
            Column(room, new Vector3(side * 13.2f, 0, -9.5f), darkStone, gold);
            Column(room, new Vector3(side * 13.2f, 0, 7.7f), darkStone, gold);
            GameObject frontShelf = NormalizedPrefab(room, side < 0 ? "West Front Bookshelf" : "East Front Bookshelf",
                "Assets/FINAL_ASSETS/BOOKSHELF.fbx", new Vector3(side * 12.3f, 0, -4.8f),
                side < 0 ? 90f : -90f, .65f);
            GameObject rearShelf = NormalizedPrefab(room, side < 0 ? "West Rear Bookshelf" : "East Rear Bookshelf",
                "Assets/FINAL_ASSETS/BOOKSHELF.fbx", new Vector3(side * 12.3f, 0, 4.3f),
                side < 0 ? 90f : -90f, .65f);
            foreach (GameObject shelf in new[] { frontShelf, rearShelf })
            {
                BoxCollider collider = shelf.AddComponent<BoxCollider>();
                collider.center = new Vector3(0, 5.05f, 0);
                collider.size = new Vector3(7f, 10.5f, 1.9f);
            }
            GameObject torchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Dungeon + "Lamps/Torch_Wall.prefab");
            GameObject torch = (GameObject)PrefabUtility.InstantiatePrefab(torchPrefab);
            torch.name = side < 0 ? "West Wall Torch" : "East Wall Torch";
            torch.transform.SetParent(room, false);
            torch.transform.localPosition = new Vector3(side * 8.8f, 4.1f, 13.67f);
            torch.transform.localRotation = Quaternion.Euler(0, 180, 0);
            Sphere("Torch Glow", room, new Vector3(side * 8.8f, 4.6f, 13.25f),
                new Vector3(.28f, .52f, .28f), flame);
            Box("Hanging Banner", room, new Vector3(side * 11.3f, 5.8f, 13.69f),
                new Vector3(1.2f, 3.2f, .07f), side < 0 ? wood : darkStone, false);
            Box("Banner Trim", room, new Vector3(side * 11.3f, 7.35f, 13.58f),
                new Vector3(1.35f, .13f, .14f), gold, false);
        }
    }

    private static GameObject BuildScrollPrefab(Material wood, Material parchment, Material gold)
    {
        const string path = PrefabFolder + "/ProofScrollDisplay.prefab";
        GameObject root = new GameObject("ProofScrollDisplay");
        Box("Parchment", root.transform, new Vector3(0, 1.2f, 0), new Vector3(3.65f, 2.27f, .10f), parchment, false);
        for (int i = 0; i < 2; i++)
        {
            GameObject roller = Cylinder(i == 0 ? "Top Roller" : "Bottom Roller", root.transform,
                new Vector3(0, i == 0 ? 2.42f : -.02f, -.025f), new Vector3(.14f, 1.98f, .14f), wood);
            roller.transform.localRotation = Quaternion.Euler(0, 0, 90);
            Sphere("Left Cap", root.transform, new Vector3(-2f, i == 0 ? 2.42f : -.02f, -.025f),
                new Vector3(.20f, .20f, .20f), gold);
            Sphere("Right Cap", root.transform, new Vector3(2f, i == 0 ? 2.42f : -.02f, -.025f),
                new Vector3(.20f, .20f, .20f), gold);
        }
        WorldText("Proof Text", root.transform, "Proof statement", new Vector3(0, 1.17f, -.073f),
            new Vector2(3.2f, 1.95f), 2.85f, new Color(.16f, .085f, .045f));
        WorldText("Classification Status", root.transform, "UNCLASSIFIED", new Vector3(0, -.48f, -.08f),
            new Vector2(3.5f, .44f), 2.8f, new Color(.48f, .83f, 1f));
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.center = new Vector3(0, 1.2f, 0);
        collider.size = new Vector3(3.8f, 2.6f, .25f);
        collider.isTrigger = true;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject BuildPedestalPrefab(Material stone, Material colorBand, Material emblem, Material gold)
    {
        const string path = PrefabFolder + "/ProofClassificationPedestal.prefab";
        GameObject root = new GameObject("ProofClassificationPedestal");
        Cylinder("Foundation", root.transform, new Vector3(0, .23f, 0), new Vector3(1.50f, .23f, 1.25f), stone);
        Box("Plinth", root.transform, new Vector3(0, .92f, 0), new Vector3(2.45f, 1.20f, 1.85f), stone, false);
        Box("Color Band", root.transform, new Vector3(0, 1.53f, -.03f), new Vector3(2.55f, .20f, 2.0f), colorBand, false);
        Box("Gold Edge", root.transform, new Vector3(0, 1.67f, -.08f), new Vector3(2.67f, .08f, 2.10f), gold, false);
        GameObject disc = Cylinder("Emblem", root.transform, new Vector3(0, 1.99f, -.88f),
            new Vector3(.60f, .075f, .60f), emblem);
        disc.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        WorldText("Emblem Letter", root.transform, "F", new Vector3(0, 1.97f, -1.05f),
            new Vector2(1.1f, 1.0f), 5.2f, Color.white);
        WorldText("Pedestal Label", root.transform, "FORMAL PROOF", new Vector3(0, .94f, -.944f),
            new Vector2(2.3f, .65f), 3.25f, Color.white);
        WorldText("Scroll Count", root.transform, "0 SCROLLS", new Vector3(0, 2.78f, -.19f),
            new Vector2(2.8f, .53f), 2.8f, new Color(.9f, .84f, .60f));
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.center = new Vector3(0, .85f, 0);
        collider.size = new Vector3(2.55f, 1.7f, 1.95f);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static void BuildLibrarySign(Transform parent, Material backing, Material trim, Material glow)
    {
        Box("Library Sign", parent, new Vector3(0, 6.55f, 13.32f),
            new Vector3(8.5f, 1.3f, .28f), backing, false);
        Box("Sign Gold Rail", parent, new Vector3(0, 7.17f, 13.16f),
            new Vector3(8.65f, .12f, .34f), trim, false);
        WorldText("Library Sign Text", parent, "PROOFS LIBRARY", new Vector3(0, 6.53f, 13.10f),
            new Vector2(8f, .96f), 5.3f, new Color(.94f, .70f, .36f));
        GameObject crystal = Sphere("Archive Crystal", parent, new Vector3(0, 7.72f, 13.1f),
            new Vector3(.55f, .84f, .40f), glow);
        crystal.transform.localRotation = Quaternion.Euler(0, 0, 45f);
    }

    private static ProofChallengeCatalog BuildProofCatalog()
    {
        const string path = "Assets/Resources/ProofChallenges.asset";
        ProofChallengeCatalog catalog = AssetDatabase.LoadAssetAtPath<ProofChallengeCatalog>(path);
        if (catalog != null) return catalog;
        catalog = ScriptableObject.CreateInstance<ProofChallengeCatalog>();
        ProofStepData[] derivation =
        {
            Step("premise-p", "P", "Premise"),
            Step("premise-pq", "P → Q", "Premise"),
            Step("premise-qr", "Q → R", "Premise"),
            Step("derive-q", "Q", "Modus Ponens from P and P → Q", 0, 1),
            Step("derive-r", "R", "Modus Ponens from Q and Q → R", 2, 3)
        };
        ProofOrderData[] orders =
        {
            Order(0, 1, 2, 3, 4), Order(0, 2, 1, 3, 4), Order(1, 0, 2, 3, 4),
            Order(1, 2, 0, 3, 4), Order(2, 0, 1, 3, 4), Order(2, 1, 0, 3, 4)
        };
        catalog.challenges = new[]
        {
            new ProofChallengeData
            {
                kind = ProofChallengeKind.SortScrolls, title = "Identify Proof Styles",
                context = "Premises: P; P → Q. Conclusion: Q.",
                instructions = "Place each proof on the correct altar.",
                scrolls = new[]
                {
                    Scroll("A", "Formal Scroll", "Premises: P and P → Q. Conclusion: Q.\n\n1. P — Premise\n2. P → Q — Premise\n3. Q — Modus Ponens, steps 1 and 2", true),
                    Scroll("B", "Informal Scroll", "Premises: P and P → Q. Conclusion: Q.\n\nWe know P is true. We are also given that P implies Q. Therefore, Q must be true.", false)
                }
            },
            new ProofChallengeData
            {
                kind = ProofChallengeKind.SortScrolls, title = "Classify Multiple Proofs",
                instructions = "Classify all four valid proofs by how their reasoning is presented.",
                scrolls = new[]
                {
                    Scroll("A", "Even Square", "Claim: The square of an even integer is even.\n1. n = 2k for some integer k. (Definition of even.)\n2. n² = 4k² = 2(2k²). (Algebra.)\n3. 2k² is an integer, so n² is even. (Definition of even.)", true),
                    Scroll("B", "Odd Sum", "The sum of two odd integers is even. Each odd group has one item left over after pairing. The two leftovers make a pair, so their combined group has no unpaired item.", false),
                    Scroll("C", "Divisibility", "Claim: If a divides b and b divides c, then a divides c.\n1. b = ak and c = bm for integers k,m. (Definition of divides.)\n2. c = (ak)m = a(km). (Substitution.)\n3. km is an integer, so a divides c. (Definition of divides.)", true),
                    Scroll("D", "Multiple of Four", "Every multiple of four is even. Four is two pairs; any whole number of groups of four can be split into pairs without leaving anything over.", false)
                }
            },
            new ProofChallengeData
            {
                kind = ProofChallengeKind.OrderSteps, title = "Arrange Formal Proof Steps",
                context = "Premises: P, P → Q, Q → R. Conclusion: R.",
                instructions = "Select each shuffled step tablet, then tap its numbered slot. You may replace or clear a slot before Book confirmation.",
                steps = derivation, acceptableOrders = orders
            },
            new ProofChallengeData
            {
                kind = ProofChallengeKind.MissingRule, title = "Missing Justification",
                context = "1. P — Premise    2. P → Q — Premise    3. Q — [MISSING JUSTIFICATION]",
                instructions = "Select the correct justification for step 3 and place it in the slot.",
                ruleOptions = new[] { "Modus Ponens", "Modus Tollens", "Simplification", "Addition" },
                expectedRule = "Modus Ponens"
            },
            new ProofChallengeData
            {
                kind = ProofChallengeKind.Formalize, title = "Formalize an Informal Explanation",
                context = "Suppose P is true. Since P implies Q, Q follows. Since Q implies R, R follows.\nPremises: P, P → Q, Q → R. Conclusion: R.",
                instructions = "Select statement and justification tablets for all five numbered slots.",
                steps = derivation, acceptableOrders = orders
            }
        };
        AssetDatabase.CreateAsset(catalog, path);
        EditorUtility.SetDirty(catalog);
        return catalog;
    }

    private static ProofScrollData Scroll(string id, string title, string content, bool formal)
    {
        return new ProofScrollData { id = id, title = title, content = content, isFormal = formal };
    }

    private static ProofStepData Step(string id, string statement, string justification, params int[] prerequisites)
    {
        return new ProofStepData { id = id, statement = statement, justification = justification, prerequisiteIds = prerequisites };
    }

    private static ProofOrderData Order(params int[] ids) { return new ProofOrderData { stepIds = ids }; }

    private static void BuildHud(out TMP_Text timer, out TMP_Text prompt,
        out GameObject resultsPanel, out TMP_Text resultsText,
        out Button retryButton, out Button menuButton)
    {
        var go = new GameObject("Proofs HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 12;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;
        timer = HudText("Proof Timer", go.transform, "10:00", new Vector2(-145f, -92f),
            new Vector2(230f, 75f), new Vector2(1, 1), 56f, new Color(1f, .80f, .50f));
        prompt = HudText("Interaction Prompt", go.transform, "Approach the Book Statue to begin.",
            new Vector2(0, -72f), new Vector2(1200f, 62f), new Vector2(.5f, 1), 31f,
            new Color(.95f, .88f, .69f));
        resultsPanel = new GameObject("Proof Results", typeof(RectTransform), typeof(Image));
        resultsPanel.transform.SetParent(go.transform, false);
        RectTransform panelRect = resultsPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(.5f, .5f);
        panelRect.sizeDelta = new Vector2(850f, 390f);
        resultsPanel.GetComponent<Image>().color = new Color(.045f, .075f, .14f, .96f);
        resultsText = HudText("Result Message", resultsPanel.transform, "CHALLENGE COMPLETE",
            new Vector2(0, 68f), new Vector2(760f, 170f), new Vector2(.5f, .5f), 44f,
            new Color(.98f, .85f, .57f));
        retryButton = HudButton("Retry", resultsPanel.transform, "RETRY", new Vector2(-150f, -117f));
        menuButton = HudButton("Main Menu", resultsPanel.transform, "MAIN MENU", new Vector2(150f, -117f));
        resultsPanel.SetActive(false);
    }

    private static Button HudButton(string name, Transform parent, string label, Vector2 position)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(250f, 70f);
        go.GetComponent<Image>().color = new Color(.13f, .29f, .49f, 1f);
        HudText("Label", go.transform, label, Vector2.zero, new Vector2(240f, 65f),
            new Vector2(.5f, .5f), 30f, Color.white);
        return go.GetComponent<Button>();
    }

    private static void BuildPlayerControls()
    {
        const string ui = "Assets/Prefabs/PREFABS/WORLD_UI/";
        GameObject gameInput = Spawn(ui + "GameInput.prefab", "GameInput");
        GameObject mobile = Spawn(ui + "MobileInputUI.prefab", "MobileInputUI");
        GameObject controls = Spawn(ui + "Canvas 1.prefab", "Canvas 1");
        Transform safe = controls.transform.Find("SafeArea");
        foreach (string path in new[] { "QuizPanel", "InventoryBar", "Gameplay_Interface/HealthBar",
            "Gameplay_Interface/ParchmentPaper", "Gameplay_Interface/ObjectivesToggleButton",
            "Gameplay_Interface/SettingsButton", "Gameplay_Interface/TimerText" })
        {
            Transform child = safe.Find(path);
            if (child != null) child.gameObject.SetActive(false);
        }
        foreach (string name in new[] { "PausePanel", "Scroll_Objectives", "GameOver",
            "GoTo_Challenge 2", "DialoguePanel", "TorchUIPanel", "StageCompletePanel", "ReadBook_BTN" })
        {
            Transform child = controls.transform.Find(name);
            if (child != null) child.gameObject.SetActive(false);
        }
        var mobileScript = mobile.GetComponent<MobileInputUI>();
        Set(mobileScript, "joystickBackground", safe.Find("Gameplay_Interface/Joystick/Background").GetComponent<RectTransform>());
        Set(mobileScript, "joystickHandle", safe.Find("Gameplay_Interface/Joystick/Background/Handle").GetComponent<RectTransform>());
        GameObject pivot = Spawn(ui + "CameraPivot.prefab", "CameraPivot");
        Transform duplicateCamera = pivot.transform.Find("3rdPersonCam");
        if (duplicateCamera != null) duplicateCamera.gameObject.SetActive(false);
        GameObject cm = Spawn(ui + "CM_ThirdPersonCam.prefab", "CM_ThirdPersonCam");
        GameObject spawner = Spawn(ui + "LevelSpawner.prefab", "LevelSpawner");
        Transform spawn = new GameObject("Proofs Player Spawn").transform;
        spawn.position = new Vector3(0, .18f, -11.6f);
        spawn.rotation = Quaternion.identity;
        Set(spawner.GetComponent<LevelSpawner>(), "spawnPoint", spawn);
        Set(spawner.GetComponent<LevelSpawner>(), "cmCamera", cm.GetComponent<CinemachineCamera>());
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    private static GameObject Spawn(string path, string name)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new InvalidOperationException("Missing prefab: " + path);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = name;
        instance.transform.position = Vector3.zero;
        return instance;
    }

    private static GameObject NormalizedPrefab(Transform parent, string name, string path,
        Vector3 position, float yaw, float scale)
    {
        GameObject holder = new GameObject(name);
        holder.transform.SetParent(parent, false);
        GameObject model = Spawn(path, name + " Model");
        model.transform.SetParent(holder.transform, false);
        model.transform.localPosition = Vector3.zero;
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException("No renderers in " + path);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        model.transform.position += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
        holder.transform.localPosition = position;
        holder.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        holder.transform.localScale = Vector3.one * scale;
        return holder;
    }

    private static void Column(Transform parent, Vector3 position, Material stone, Material gold)
    {
        Cylinder("Stone Column", parent, position + new Vector3(0, 4.2f, 0),
            new Vector3(.55f, 4.2f, .55f), stone);
        Cylinder("Column Foot", parent, position + new Vector3(0, .20f, 0),
            new Vector3(.78f, .20f, .78f), stone);
        Cylinder("Gold Capital", parent, position + new Vector3(0, 8.38f, 0),
            new Vector3(.78f, .13f, .78f), gold);
    }

    private static GameObject Box(string name, Transform parent, Vector3 position,
        Vector3 size, Material material, bool collider)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private static GameObject Cylinder(string name, Transform parent, Vector3 position,
        Vector3 size, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private static GameObject Sphere(string name, Transform parent, Vector3 position,
        Vector3 size, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private static TMP_Text WorldText(string name, Transform parent, string content, Vector3 position,
        Vector2 size, float fontSize, Color color)
    {
        GameObject go = new GameObject(name, typeof(TextMeshPro));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        TMP_Text text = go.GetComponent<TextMeshPro>();
        text.text = content;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = fontSize * .7f;
        text.fontSizeMax = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.rectTransform.sizeDelta = size;
        text.color = color;
        return text;
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
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        return text;
    }

    private static Material Lit(string name, Color color, Texture texture)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = Shader.Find("Universal Render Pipeline/Lit");
        material.SetColor("_BaseColor", color);
        material.SetTexture("_BaseMap", texture);
        material.SetFloat("_Smoothness", .08f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material Unlit(string name, Color color)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = Shader.Find("Universal Render Pipeline/Unlit");
        material.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
    }

    private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetArray<T>(UnityEngine.Object target, string field, T[] values) where T : UnityEngine.Object
    {
        var serialized = new SerializedObject(target);
        var array = serialized.FindProperty(field);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

}
