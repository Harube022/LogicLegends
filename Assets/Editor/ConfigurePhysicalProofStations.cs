using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Builds reusable, mobile-friendly scroll stations for all five proof challenges.</summary>
public static class ConfigurePhysicalProofStations
{
    private const string MaterialFolder = "Assets/Materials/FormalInformalProofs";
    private const string PrefabPath = "Assets/Prefabs/PREFABS/FormalInformalProofs/ProofWorldPedestal.prefab";

    [MenuItem("Logic Legends/Build Physical Proof Stations")]
    public static void BuildInCurrentScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "Formal vs. Informal Proofs" || EditorApplication.isPlaying)
            throw new InvalidOperationException("Open the Formal vs. Informal Proofs scene in Edit mode first.");
        EnsureSceneObjects();
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Physical proof scroll stations and readable board built for all five challenges.");
    }

    public static void EnsureSceneObjects()
    {
        GameObject library = GameObject.Find("ProofsLibrary");
        ProofsLibraryStageController stage = UnityEngine.Object.FindFirstObjectByType<ProofsLibraryStageController>();
        if (library == null || stage == null)
            throw new InvalidOperationException("The Proofs Library scene and stage manager must exist first.");
        GameObject prefab = EnsurePrefab();
        EnsureScrollAppearance();
        Transform holder = library.transform.Find("Physical Proof Stations");
        if (holder == null)
        {
            holder = new GameObject("Physical Proof Stations").transform;
            holder.SetParent(library.transform, false);
        }
        ProofWorldBoardController board = holder.GetComponent<ProofWorldBoardController>();
        if (board == null) board = holder.gameObject.AddComponent<ProofWorldBoardController>();

        ProofWorldPedestal[] statements = EnsureColumn(holder, prefab, "Statement Tablet", -10.4f, -90f);
        ProofWorldPedestal[] rules = EnsureColumn(holder, prefab, "Rule Tablet", -6.5f, -90f);
        ProofWorldPedestal[] stepSlots = EnsureColumn(holder, prefab, "Numbered Step Slot", 6.5f, 90f);
        ProofWorldPedestal[] ruleSlots = EnsureColumn(holder, prefab, "Numbered Rule Slot", 10.4f, 90f);
        SetArray(board, "statementTablets", statements);
        SetArray(board, "ruleTablets", rules);
        SetArray(board, "statementSlots", stepSlots);
        SetArray(board, "ruleSlots", ruleSlots);
        ProofWorldPedestal[] scrollSources = EnsureScrollSources(holder, prefab, library.transform);
        var stageSerialized = new SerializedObject(stage);
        stageSerialized.FindProperty("worldBoard").objectReferenceValue = board;
        var sourceArray = stageSerialized.FindProperty("scrollSources");
        sourceArray.arraySize = scrollSources.Length;
        for (int i = 0; i < scrollSources.Length; i++)
            sourceArray.GetArrayElementAtIndex(i).objectReferenceValue = scrollSources[i];
        stageSerialized.ApplyModifiedPropertiesWithoutUndo();
        EnsureBoardLayout(library.transform);
        Transform table = library.transform.Find("Central Proof Table");
        if (table != null) UnityEngine.Object.DestroyImmediate(table.gameObject);

        ProofChallengeCatalog catalog = AssetDatabase.LoadAssetAtPath<ProofChallengeCatalog>(
            "Assets/Resources/ProofChallenges.asset");
        if (catalog == null || catalog.challenges.Length != 5)
            throw new InvalidOperationException("Five-challenge Proof catalog is missing.");
        ReplaceInstruction(catalog.challenges[2],
            "Select each shuffled step tablet, then tap its numbered slot. You may replace or clear a slot before Book confirmation.",
            "Select a statement tablet, then place it on a numbered STEP pedestal. Interact with a filled slot to remove it.");
        ReplaceInstruction(catalog.challenges[2],
            "Interact with a shuffled statement tablet, then walk to a numbered STEP pedestal and place it. Interact with a filled slot to remove it. Confirm at the Book Statue.",
            "Select a statement tablet, then place it on a numbered STEP pedestal. Interact with a filled slot to remove it.");
        ReplaceInstruction(catalog.challenges[3],
            "Select the correct justification for step 3 and place it in the slot.",
            "Select a RULE tablet and place it on RULE SLOT 1.");
        ReplaceInstruction(catalog.challenges[3],
            "Read the missing justification on the board. Select a RULE tablet, carry its selection to RULE SLOT 1, then confirm at the Book Statue.",
            "Select a RULE tablet and place it on RULE SLOT 1.");
        ReplaceInstruction(catalog.challenges[4],
            "Select statement and justification tablets for all five numbered slots.",
            "Place statements and matching rules on the numbered STEP and RULE pedestals.");
        ReplaceInstruction(catalog.challenges[4],
            "Select statement and rule tablets around the room. Place each on the matching numbered STEP or RULE pedestal, then confirm at the Book Statue.",
            "Place statements and matching rules on the numbered STEP and RULE pedestals.");
        EditorUtility.SetDirty(catalog);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private static void ReplaceInstruction(ProofChallengeData challenge, string oldText, string newText)
    {
        if (challenge.instructions == oldText) challenge.instructions = newText;
    }

    private static ProofWorldPedestal[] EnsureScrollSources(Transform holder, GameObject prefab, Transform library)
    {
        var sources = new ProofWorldPedestal[4];
        for (int i = 0; i < sources.Length; i++)
        {
            string name = "Scroll Source " + (char)('A' + i);
            Transform existing = holder.Find(name);
            GameObject station = existing != null ? existing.gameObject :
                (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (existing == null)
            {
                station.name = name;
                station.transform.SetParent(holder, false);
            }
            bool left = i < 2;
            station.transform.localPosition = new Vector3(left ? -9f : 9f,
                0f, i % 2 == 0 ? -3.3f : .2f);
            station.transform.localRotation = Quaternion.Euler(0f, left ? -90f : 90f, 0f);
            // Use the same scroll face and rollers as Challenges 3–5.
            foreach (string child in new[] { "Reading Tablet", "Tablet Frame", "Tablet Heading",
                         "Tablet Detail", "Book Emblem", "Book Left Page", "Book Right Page" })
            {
                Transform part = station.transform.Find(child);
                if (part != null) part.gameObject.SetActive(true);
            }
            station.SetActive(false);
            sources[i] = station.GetComponent<ProofWorldPedestal>();
            if (sources[i] == null) throw new InvalidOperationException(name + " lacks ProofWorldPedestal.");
            Transform scroll = library.Find("Scroll " + (char)('A' + i));
            if (scroll == null) throw new InvalidOperationException("Missing proof scroll " + (char)('A' + i));
            // Retain the original scene reference for the read/carry logic, but do
            // not render a second scroll over the reusable pedestal's parchment.
            foreach (Renderer renderer in scroll.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            foreach (Collider collider in scroll.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            scroll.localPosition = station.transform.localPosition;
            scroll.localScale = Vector3.one * .42f;
            scroll.gameObject.SetActive(false);
        }
        return sources;
    }

    private static void EnsureScrollAppearance()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            if (root.transform.Find("Upper Scroll Roller") != null) return;
            Material wood = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/Library_Wood.mat");
            Material gold = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/Library_Gold.mat");
            if (wood == null || gold == null) throw new InvalidOperationException("Scroll trim materials are missing.");
            Renderer frame = root.transform.Find("Tablet Frame")?.GetComponent<Renderer>();
            if (frame != null) frame.sharedMaterial = wood;
            foreach (bool upper in new[] { true, false })
            {
                float y = upper ? 2.67f : 1.49f;
                Renderer roller = Primitive(upper ? "Upper Scroll Roller" : "Lower Scroll Roller",
                    PrimitiveType.Cylinder, root.transform,
                    new Vector3(0f, y, -.34f), new Vector3(.16f, 1.40f, .16f), wood);
                roller.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                for (int side = -1; side <= 1; side += 2)
                    Primitive((upper ? "Upper" : "Lower") + (side < 0 ? " Left" : " Right") + " Scroll Cap",
                        PrimitiveType.Sphere, root.transform,
                        new Vector3(side * 1.43f, y, -.34f), new Vector3(.23f, .23f, .23f), gold);
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void EnsureBoardLayout(Transform library)
    {
        Transform board = library.Find("Proof Information Board");
        Transform header = library.Find("Board Header");
        Transform body = library.Find("Current Challenge");
        if (board == null || header == null || body == null)
            throw new InvalidOperationException("The Proof Information Board or its text is missing.");
        Transform model = board.GetChild(0);
        board.localScale = new Vector3(1.21f, .77f, .57f);
        Bounds bounds = RendererBounds(model);
        model.position += new Vector3(0f, 4.05f, 12.55f) - bounds.center;
        bounds = RendererBounds(model);
        BoxCollider collider = board.GetComponent<BoxCollider>();
        if (collider == null) collider = board.gameObject.AddComponent<BoxCollider>();
        collider.center = board.InverseTransformPoint(bounds.center);
        Vector3 scale = board.lossyScale;
        collider.size = new Vector3((bounds.size.x + .08f) / Mathf.Abs(scale.x),
            (bounds.size.y + .08f) / Mathf.Abs(scale.y),
            (bounds.size.z + .08f) / Mathf.Abs(scale.z));
        header.position = new Vector3(bounds.center.x, bounds.max.y - .70f, bounds.min.z - .09f);
        header.GetComponent<RectTransform>().sizeDelta = new Vector2(10.2f, .68f);
        TMP_Text title = header.GetComponent<TMP_Text>();
        title.fontSize = 4.0f;
        // TMP's top-left mesh grows upward from this world-space baseline.
        // Inset it from the visible parchment frame on all four sides.
        body.position = new Vector3(bounds.center.x + .25f, bounds.min.y + 1.55f, bounds.min.z - .10f);
        body.GetComponent<RectTransform>().sizeDelta = new Vector2(9.0f, 4.65f);
        TMP_Text text = body.GetComponent<TMP_Text>();
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableAutoSizing = true;
        text.fontSize = 3.0f;
        text.fontSizeMin = 1.55f;
        text.fontSizeMax = 3.0f;
        text.margin = new Vector4(.15f, .12f, .15f, .10f);
    }

    private static Bounds RendererBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException("No board renderer found.");
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static ProofWorldPedestal[] EnsureColumn(Transform holder, GameObject prefab,
        string prefix, float x, float yaw)
    {
        var stations = new ProofWorldPedestal[5];
        for (int i = 0; i < stations.Length; i++)
        {
            string name = prefix + " " + (i + 1);
            Transform existing = holder.Find(name);
            GameObject station = existing != null ? existing.gameObject :
                (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (existing == null)
            {
                station.name = name;
                station.transform.SetParent(holder, false);
                station.transform.localPosition = new Vector3(x, 0f, -6.2f + i * 3.25f);
                station.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                station.SetActive(false);
            }
            stations[i] = station.GetComponent<ProofWorldPedestal>();
            if (stations[i] == null) throw new InvalidOperationException(name + " lacks ProofWorldPedestal.");
        }
        return stations;
    }

    private static GameObject EnsurePrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab != null) return prefab;
        Material stone = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/Library_DarkStone.mat");
        Material gold = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/Library_Gold.mat");
        Material parchment = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/Library_Parchment.mat");
        if (stone == null || gold == null || parchment == null)
            throw new InvalidOperationException("Proofs Library base materials are missing.");
        Material normal = StateMaterial("ProofStation_Normal", new Color(.32f, .39f, .53f));
        Material selected = StateMaterial("ProofStation_Selected", new Color(.08f, .52f, 1f));
        Material correct = StateMaterial("ProofStation_Correct", new Color(.06f, .91f, .35f));
        Material incorrect = StateMaterial("ProofStation_Incorrect", new Color(1f, .16f, .11f));

        GameObject root = new GameObject("ProofWorldPedestal");
        Primitive("Foundation", PrimitiveType.Cylinder, root.transform,
            new Vector3(0f, .13f, 0f), new Vector3(1.30f, .13f, 1.05f), stone);
        Primitive("Stone Plinth", PrimitiveType.Cube, root.transform,
            new Vector3(0f, .53f, 0f), new Vector3(1.55f, .73f, 1.17f), stone);
        Renderer band = Primitive("State Band", PrimitiveType.Cube, root.transform,
            new Vector3(0f, .98f, 0f), new Vector3(1.68f, .17f, 1.30f), normal);
        Primitive("Gold Rim", PrimitiveType.Cube, root.transform,
            new Vector3(0f, 1.10f, 0f), new Vector3(1.76f, .08f, 1.37f), gold);
        Renderer emblem = Primitive("Book Emblem", PrimitiveType.Cylinder, root.transform,
            new Vector3(0f, 1.34f, -.68f), new Vector3(.53f, .055f, .53f), normal);
        emblem.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Primitive("Book Left Page", PrimitiveType.Cube, root.transform,
            new Vector3(-.145f, 1.34f, -.755f), new Vector3(.24f, .31f, .04f), parchment)
            .transform.localRotation = Quaternion.Euler(0f, 0f, -10f);
        Primitive("Book Right Page", PrimitiveType.Cube, root.transform,
            new Vector3(.145f, 1.34f, -.755f), new Vector3(.24f, .31f, .04f), parchment)
            .transform.localRotation = Quaternion.Euler(0f, 0f, 10f);
        Renderer gem = Primitive("State Gem", PrimitiveType.Cube, root.transform,
            new Vector3(0f, .56f, -.67f), new Vector3(.17f, .17f, .10f), normal);
        gem.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Primitive("Reading Tablet", PrimitiveType.Cube, root.transform,
            new Vector3(0f, 2.08f, -.26f), new Vector3(2.32f, 1.08f, .12f), parchment);
        Primitive("Tablet Frame", PrimitiveType.Cube, root.transform,
            new Vector3(0f, 2.08f, -.18f), new Vector3(2.42f, 1.16f, .08f), gold)
            .transform.SetAsFirstSibling();
        TMP_Text heading = WorldText("Tablet Heading", root.transform, "STATEMENT A",
            new Vector3(0f, 2.38f, -.34f), new Vector2(2.12f, .30f), 2.5f);
        TMP_Text detail = WorldText("Tablet Detail", root.transform, "Proof statement",
            new Vector3(0f, 1.99f, -.34f), new Vector2(2.15f, .68f), 2.1f);
        var collider = root.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 1.22f, 0f);
        collider.size = new Vector3(2.35f, 2.48f, 1.35f);
        ProofWorldPedestal component = root.AddComponent<ProofWorldPedestal>();
        var serialized = new SerializedObject(component);
        var surfaces = serialized.FindProperty("stateSurfaces");
        surfaces.arraySize = 3;
        surfaces.GetArrayElementAtIndex(0).objectReferenceValue = band;
        surfaces.GetArrayElementAtIndex(1).objectReferenceValue = emblem;
        surfaces.GetArrayElementAtIndex(2).objectReferenceValue = gem;
        serialized.FindProperty("heading").objectReferenceValue = heading;
        serialized.FindProperty("detail").objectReferenceValue = detail;
        serialized.FindProperty("normalMaterial").objectReferenceValue = normal;
        serialized.FindProperty("selectedMaterial").objectReferenceValue = selected;
        serialized.FindProperty("correctMaterial").objectReferenceValue = correct;
        serialized.FindProperty("incorrectMaterial").objectReferenceValue = incorrect;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static Renderer Primitive(string name, PrimitiveType type, Transform parent,
        Vector3 position, Vector3 scale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
        Renderer renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        return renderer;
    }

    private static TMP_Text WorldText(string name, Transform parent, string value,
        Vector3 position, Vector2 size, float fontSize)
    {
        GameObject go = new GameObject(name, typeof(TextMeshPro));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        TextMeshPro label = go.GetComponent<TextMeshPro>();
        label.text = value;
        label.fontSize = fontSize;
        label.enableAutoSizing = true;
        label.fontSizeMin = fontSize * .52f;
        label.fontSizeMax = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta = size;
        label.color = new Color(.17f, .10f, .05f);
        return label;
    }

    private static Material StateMaterial(string name, Color color)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void SetArray<T>(UnityEngine.Object owner, string name, T[] values)
        where T : UnityEngine.Object
    {
        var serialized = new SerializedObject(owner);
        var array = serialized.FindProperty(name);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
