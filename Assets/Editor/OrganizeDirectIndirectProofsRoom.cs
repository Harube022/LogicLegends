using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Positions the existing chamber assets without replacing their prefab instances.</summary>
public static class OrganizeDirectIndirectProofsRoom
{
    [MenuItem("Logic Legends/Organize Direct and Indirect Proofs Room")]
    public static void OrganizeAndSave()
    {
        Apply();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Logic Legends/Remove Floating Proof Altar Signs")]
    public static void RemoveFloatingAltarSignsAndSave()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.name != "Direct&Indirect")
            throw new InvalidOperationException("Open Direct&Indirect in Edit mode to fix the altar labels.");
        Transform trialRoot = RequiredRoot(scene, "TrialOfProofs").transform;
        RemoveAltarSigns(trialRoot,
            RequiredRoot(scene, "DirectAltar"), RequiredRoot(scene, "IndirectAltar"));
        PositionBoardText(RequiredRoot(scene, "Board"), trialRoot);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    public static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.name != "Direct&Indirect")
            throw new InvalidOperationException("Open Direct&Indirect in Edit mode before organizing the room.");

        GameObject stage = RequiredRoot(scene, "Stage");
        GameObject board = RequiredRoot(scene, "Board");
        GameObject direct = RequiredRoot(scene, "DirectAltar");
        GameObject indirect = RequiredRoot(scene, "IndirectAltar");
        GameObject spareBlue = FindRoot(scene, "ProofTrialAltar") ?? RequiredRoot(scene, "BlueAltar");
        GameObject book = RequiredRoot(scene, "Book_Statue");
        GameObject rack = FindRoot(scene, "Direct Statement Rack") ?? RequiredRoot(scene, "StatementTable");
        GameObject trial = RequiredRoot(scene, "TrialOfProofs");
        if (stage.GetComponent<MeshCollider>() == null)
            throw new InvalidOperationException("The Stage floor collision mesh is missing.");

        // The stage floor is y=1.42. Keep the approach to the Book open.
        SetRenderedPosition(board, 56.7f, 31.15f, 4.4f);
        SetRenderedPosition(direct, 67.7f, 45.5f, 1.42f);
        SetRenderedPosition(indirect, 45.7f, 45.5f, 1.42f);
        SetRenderedPosition(spareBlue, 56.7f, 45.5f, 1.42f);
        SetRenderedPosition(book, 56.7f, 56.2f, 1.42f);
        SetRenderedPosition(rack, 79.9f, 44f, 1.42f);
        rack.name = "Direct Statement Rack";

        GameObject otherRack = FindRoot(scene, "Indirect Statement Rack");
        if (otherRack == null)
        {
            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(rack);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) throw new InvalidOperationException("The StatementTable model is missing.");
            otherRack = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (otherRack == null) throw new InvalidOperationException("The indirect tablet rack could not be created.");
            otherRack.name = "Indirect Statement Rack";
        }
        SetRenderedPosition(otherRack, 33.5f, 44f, 1.42f);
        // Statement tablets are no longer part of the altar-led proof interaction.
        rack.SetActive(false);
        otherRack.SetActive(false);

        // The unnumbered stone altar serves the two general proof trials.
        EnsureMeshColliders(spareBlue);
        spareBlue.name = "ProofTrialAltar";
        EnsureNeutralAltarMaterials(spareBlue);
        spareBlue.SetActive(false);
        foreach (GameObject asset in new[] { direct, indirect, board, rack, otherRack })
            EnsureMeshColliders(asset);
        FitBoxCollider(book, RenderBounds(book), .12f);

        Transform proofRoot = trial.transform;
        PositionBoardText(board, proofRoot);
        Transform boardPoint = RequiredChild(proofRoot, "Board Interaction Point");
        boardPoint.position = new Vector3(56.7f, 1.55f, 45f);

        for (int i = 0; i < 3; i++)
        {
            Transform station = RequiredChild(proofRoot, "Strategy " + (i + 1));
            station.SetPositionAndRotation(new Vector3(46.5f + i * 10.2f, 1.42f, 73.5f), Quaternion.identity);
            FitBoxCollider(station.gameObject, RenderBounds(station.gameObject), .06f);
        }
        for (int i = 0; i < 5; i++)
        {
            float z = 50.2f + i * 4f;
            Transform tablet = RequiredChild(proofRoot, "Proof Tablet " + (char)('A' + i));
            Transform slot = RequiredChild(proofRoot, "Proof Step Slot " + (i + 1));
            tablet.SetPositionAndRotation(new Vector3(66.6f, 1.42f, z), Quaternion.Euler(0f, 90f, 0f));
            slot.SetPositionAndRotation(new Vector3(62.1f, 1.42f, z), Quaternion.Euler(0f, 90f, 0f));
            FitBoxCollider(tablet.gameObject, RenderBounds(tablet.gameObject), .06f);
            FitBoxCollider(slot.gameObject, RenderBounds(slot.gameObject), .06f);
        }

        foreach (string name in new[] { "Direct Station Active", "Indirect Station Active" })
        {
            Transform marker = proofRoot.Find(name);
            if (marker != null) UnityEngine.Object.DestroyImmediate(marker.gameObject);
        }
        RemoveAltarSigns(proofRoot, direct, indirect);
        RestoreDirectIndirectAltarLabels.EnsureSceneObjects();
        TMP_Text neutralText = EnsureNeutralTablet(proofRoot);
        EnsureBoardMount(proofRoot);
        EnsureAltarGameplay(scene, proofRoot, direct, indirect, spareBlue, neutralText);
        EnsureAdminDebug(scene);
        DressChamber(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Direct and Indirect Proofs room organized: accessible proof rows, central Book aisle, Admin button, and lightweight chamber dressing.");
    }

    private static void EnsureAltarGameplay(Scene scene, Transform proofRoot,
        GameObject direct, GameObject indirect, GameObject neutral, TMP_Text neutralText)
    {
        Material yellow = EnsureSignMaterial("AltarChoice_Yellow", new Color(2.8f, 2.0f, .1f));
        Material green = EnsureSignMaterial("AltarChoice_Green", new Color(.2f, 2.5f, .55f));
        Material red = EnsureSignMaterial("BookFeedback_Red", new Color(2.5f, .12f, .1f));
        GameObject directGlow = EnsureAltarRing(proofRoot, "Direct Altar Selection Ring",
            "Direct Altar Choice Glow", new Vector3(67.7f, 2.40f, 45.5f), yellow);
        GameObject indirectGlow = EnsureAltarRing(proofRoot, "Indirect Altar Selection Ring",
            "Indirect Altar Choice Glow", new Vector3(45.7f, 2.40f, 45.5f), yellow);
        GameObject bookGlow = EnsureGlow(proofRoot, "Book Wrong Answer Glow",
            new Vector3(56.7f, 5.85f, 56.2f), red);
        EnsureBookFlash(bookGlow.transform, "Left Book Flash", new Vector3(55.30f, 4.0f, 57.52f), red);
        EnsureBookFlash(bookGlow.transform, "Right Book Flash", new Vector3(58.10f, 4.0f, 57.52f), red);
        DirectIndirectProofStageController manager = RequiredRoot(scene,
            "DirectIndirectProofStageManager").GetComponent<DirectIndirectProofStageController>();
        if (manager == null) throw new InvalidOperationException("Direct and Indirect stage manager is missing.");
        SerializedObject serialized = new SerializedObject(manager);
        serialized.FindProperty("directChoiceGlow").objectReferenceValue = directGlow;
        serialized.FindProperty("indirectChoiceGlow").objectReferenceValue = indirectGlow;
        serialized.FindProperty("bookErrorGlow").objectReferenceValue = bookGlow;
        serialized.FindProperty("choiceYellow").objectReferenceValue = yellow;
        serialized.FindProperty("choiceGreen").objectReferenceValue = green;
        serialized.FindProperty("directAltar").objectReferenceValue = direct.transform;
        serialized.FindProperty("indirectAltar").objectReferenceValue = indirect.transform;
        serialized.FindProperty("neutralAltar").objectReferenceValue = neutral.transform;
        serialized.FindProperty("neutralTabletText").objectReferenceValue = neutralText;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        GameObject hud = RequiredRoot(scene, "Trial of Proofs HUD");
        if (hud.GetComponent<ProofAltarOrderingBoard>() == null)
            hud.AddComponent<ProofAltarOrderingBoard>();
    }

    private static GameObject EnsureAltarRing(Transform parent, string name, string oldName,
        Vector3 center, Material material)
    {
        Transform existing = parent.Find(name) ?? parent.Find(oldName);
        GameObject ring = existing != null ? existing.gameObject : new GameObject(name);
        if (existing == null) ring.transform.SetParent(parent, false);
        ring.name = name;
        LineRenderer oldLine = ring.GetComponent<LineRenderer>();
        if (oldLine != null) UnityEngine.Object.DestroyImmediate(oldLine);
        Collider oldCollider = ring.GetComponent<Collider>();
        if (oldCollider != null) UnityEngine.Object.DestroyImmediate(oldCollider);
        ring.transform.position = center;
        ring.transform.rotation = Quaternion.identity;
        ring.transform.localScale = Vector3.one;
        MeshFilter filter = ring.GetComponent<MeshFilter>();
        if (filter == null) filter = ring.AddComponent<MeshFilter>();
        filter.sharedMesh = EnsureRingMesh();
        MeshRenderer renderer = ring.GetComponent<MeshRenderer>();
        if (renderer == null) renderer = ring.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        ring.SetActive(false);
        return ring;
    }

    private static Mesh EnsureRingMesh()
    {
        const string path = "Assets/Art/ProofChamber/AltarSelectionRing.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool created = mesh == null;
        const int count = 64;
        const float inner = 4.72f;
        const float outer = 5.22f;
        Vector3[] vertices = new Vector3[count * 2];
        int[] triangles = new int[count * 6];
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            vertices[2 * i] = direction * inner;
            vertices[2 * i + 1] = direction * outer;
            int next = (i + 1) % count;
            int offset = i * 6;
            triangles[offset] = 2 * i;
            triangles[offset + 1] = 2 * next + 1;
            triangles[offset + 2] = 2 * i + 1;
            triangles[offset + 3] = 2 * i;
            triangles[offset + 4] = 2 * next;
            triangles[offset + 5] = 2 * next + 1;
        }
        if (created) mesh = new Mesh { name = "Altar Selection Ring" };
        else mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        if (created) AssetDatabase.CreateAsset(mesh, path);
        else EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static GameObject EnsureGlow(Transform parent, string name, Vector3 position, Material material)
    {
        Transform existing = parent.Find(name);
        GameObject glow = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Sphere);
        if (existing == null)
        {
            glow.name = name;
            glow.transform.SetParent(parent, false);
        }
        glow.transform.position = position;
        glow.transform.rotation = Quaternion.identity;
        glow.transform.localScale = new Vector3(1.4f, .32f, 1.4f);
        glow.GetComponent<Renderer>().sharedMaterial = material;
        Collider collider = glow.GetComponent<Collider>();
        if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
        glow.SetActive(false);
        return glow;
    }

    private static void EnsureBookFlash(Transform parent, string name, Vector3 position, Material material)
    {
        Transform existing = parent.Find(name);
        GameObject flash = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
        if (existing == null)
        {
            flash.name = name;
            flash.transform.SetParent(parent, false);
        }
        flash.transform.position = position;
        flash.transform.rotation = Quaternion.identity;
        flash.transform.localScale = new Vector3(.22f, 1.25f, .24f);
        flash.GetComponent<Renderer>().sharedMaterial = material;
        Collider collider = flash.GetComponent<Collider>();
        if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
    }

    private static void EnsureAdminDebug(Scene scene)
    {
        GameObject hud = RequiredRoot(scene, "Trial of Proofs HUD");
        Canvas canvas = hud.GetComponent<Canvas>();
        if (canvas == null) throw new InvalidOperationException("Trial of Proofs HUD needs a Canvas.");
        GameObject adminObject = FindRoot(scene, "Admin Debug");
        bool created = adminObject == null;
        if (created) adminObject = new GameObject("Admin Debug");
        AdminDebugController admin = adminObject.GetComponent<AdminDebugController>();
        if (admin == null) admin = adminObject.AddComponent<AdminDebugController>();
        SerializedObject serialized = new SerializedObject(admin);
        serialized.FindProperty("uiCanvas").objectReferenceValue = canvas;
        if (created)
        {
            serialized.FindProperty("featureEnabled").boolValue = true;
            serialized.FindProperty("allowInPlayerBuilds").boolValue = true;
            serialized.FindProperty("adminButtonEnabled").boolValue = true;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void DressChamber(Scene scene)
    {
        GameObject root = FindRoot(scene, "Proof Chamber Dressing");
        if (root == null) root = new GameObject("Proof Chamber Dressing");
        foreach (string name in new[] { "Left Wall Banner", "Right Wall Banner" })
        {
            Transform oldBanner = root.transform.Find(name);
            if (oldBanner != null) UnityEngine.Object.DestroyImmediate(oldBanner.gameObject);
        }
        EnsureBrandBanner(root.transform);
        string torch = "Assets/CHEATCODE/DUNGEON/LowPolyDungeon/Prefabs/Lamps/Torch_Wall.prefab";
        PlaceDecoration(root.transform, torch, "Left Wall Torch", 49.8f, 31.3f, 5.1f, 180f, 2.2f);
        PlaceDecoration(root.transform, torch, "Right Wall Torch", 63.6f, 31.3f, 5.1f, 180f, 2.2f);
    }

    private static void EnsureBrandBanner(Transform parent)
    {
        const string texturePath = "Assets/Assets/Interface_Sprites/background/banner_v2.png";
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture == null) throw new InvalidOperationException("Logic Legends banner texture is missing: " + texturePath);
        const string materialPath = "Assets/Materials/DirectIndirectProofs/LogicLegendsWallBanner.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new InvalidOperationException("URP Unlit shader is missing.");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        Transform existing = parent.Find("Logic Legends Wall Banner");
        GameObject banner = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Quad);
        if (existing == null) { banner.name = "Logic Legends Wall Banner"; banner.transform.SetParent(parent, false); }
        banner.transform.SetPositionAndRotation(new Vector3(56.7f, 13.7f, 33f),
            Quaternion.Euler(0f, 180f, 0f));
        banner.transform.localScale = new Vector3(9f, 5.06f, 1f);
        banner.GetComponent<Renderer>().sharedMaterial = material;
        Collider collider = banner.GetComponent<Collider>();
        if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
    }

    private static void PlaceDecoration(Transform root, string prefabPath, string name,
        float x, float z, float bottom, float yaw, float scale)
    {
        Transform existing = root.Find(name);
        GameObject decoration = existing != null ? existing.gameObject : null;
        if (decoration == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) throw new InvalidOperationException("Missing chamber decoration: " + prefabPath);
            decoration = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (decoration == null) throw new InvalidOperationException("Could not place " + name);
            decoration.name = name;
            decoration.transform.SetParent(root, true);
        }
        decoration.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        decoration.transform.localScale = Vector3.one * scale;
        SetRenderedPosition(decoration, x, z, bottom);
        EnsureMeshColliders(decoration);
        // Existing chamber light and emissive materials provide the atmosphere;
        // extra real-time point lights would increase mobile rendering cost.
        foreach (Light light in decoration.GetComponentsInChildren<Light>(true)) light.enabled = false;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == name) return root;
        return null;
    }

    private static GameObject RequiredRoot(Scene scene, string name)
    {
        return FindRoot(scene, name) ?? throw new InvalidOperationException("Missing scene asset: " + name);
    }

    private static Transform RequiredChild(Transform parent, string name)
    {
        return parent.Find(name) ?? throw new InvalidOperationException("Missing trial asset: " + name);
    }

    private static void PositionBoardText(GameObject board, Transform trialRoot)
    {
        Renderer header = null, page = null;
        foreach (Renderer renderer in board.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.name == "InformationBoard") header = renderer;
            else if (renderer.name == "Cube.224") page = renderer;
        }
        if (header == null || page == null)
            throw new InvalidOperationException("The Board model is missing one of its display meshes.");
        RectTransform heading = RequiredChild(trialRoot, "Trial Board Header").GetComponent<RectTransform>();
        RectTransform problem = RequiredChild(trialRoot, "Current Proof Problem").GetComponent<RectTransform>();
        heading.anchoredPosition3D = new Vector3(
            header.bounds.center.x, header.bounds.center.y, header.bounds.max.z + .14f);
        problem.anchoredPosition3D = new Vector3(
            page.bounds.center.x, page.bounds.center.y, page.bounds.max.z + .14f);
    }

    private static void EnsureBoardMount(Transform parent)
    {
        Material stone = EnsureSignMaterial("WallMount_Stone", new Color(.13f, .16f, .20f));
        Material bronze = EnsureSignMaterial("WallMount_Bronze", new Color(.44f, .30f, .14f));
        EnsureMountCube(parent, "Board Wall Backer", new Vector3(56.7f, 8.62f, 29.9f),
            new Vector3(10.3f, 9.1f, 1.4f), stone);
        EnsureMountCube(parent, "Board Lower Bracket", new Vector3(56.7f, 3.95f, 30.25f),
            new Vector3(10.6f, .42f, 1.55f), bronze);
        EnsureMountCube(parent, "Board Left Support", new Vector3(52.35f, 2.65f, 30.25f),
            new Vector3(.52f, 2.5f, 1.0f), stone);
        EnsureMountCube(parent, "Board Right Support", new Vector3(61.05f, 2.65f, 30.25f),
            new Vector3(.52f, 2.5f, 1.0f), stone);
    }

    private static TMP_Text EnsureNeutralTablet(Transform parent)
    {
        Transform existing = parent.Find("Neutral Proof Tablet");
        GameObject tablet = existing != null ? existing.gameObject : new GameObject("Neutral Proof Tablet");
        if (existing == null) tablet.transform.SetParent(parent, false);
        Material parchment = EnsureSignMaterial("TrialTablet_Parchment", new Color(.80f, .69f, .48f));
        Material bronze = EnsureSignMaterial("WallMount_Bronze", new Color(.44f, .30f, .14f));
        // Center the floating goal above the neutral altar and leave its top visible.
        EnsureMountCube(tablet.transform, "Tablet Rim", new Vector3(56.7f, 6.1f, 45.5f),
            new Vector3(5.2f, 2.3f, .35f), bronze);
        EnsureMountCube(tablet.transform, "Tablet Page", new Vector3(56.7f, 6.1f, 45.72f),
            new Vector3(4.9f, 2.05f, .16f), parchment);
        Transform textObject = tablet.transform.Find("Problem Text");
        if (textObject == null)
        {
            GameObject created = new GameObject("Problem Text", typeof(TextMeshPro));
            created.transform.SetParent(tablet.transform, false);
            textObject = created.transform;
        }
        // TMP's visible face points toward local -Z. Face the player approach (+Z),
        // otherwise the problem reads backward through the back of the mesh.
        textObject.SetPositionAndRotation(new Vector3(56.7f, 6.1f, 45.85f), Quaternion.Euler(0f, 180f, 0f));
        TextMeshPro text = textObject.GetComponent<TextMeshPro>();
        text.color = new Color(.15f, .10f, .06f);
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = .75f;
        text.fontSizeMax = 1.8f;
        text.fontSize = 1.8f;
        text.rectTransform.sizeDelta = new Vector2(4.55f, 1.82f);
        text.text = "";
        tablet.SetActive(false);
        return text;
    }

    private static void EnsureMountCube(Transform parent, string name, Vector3 center,
        Vector3 size, Material material)
    {
        Transform existing = parent.Find(name);
        GameObject cube = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
        if (existing == null) { cube.name = name; cube.transform.SetParent(parent, false); }
        cube.transform.SetPositionAndRotation(center, Quaternion.identity);
        cube.transform.localScale = size;
        cube.GetComponent<Renderer>().sharedMaterial = material;
        if (cube.GetComponent<Collider>() == null) cube.AddComponent<BoxCollider>();
    }

    private static void EnsureNeutralAltarMaterials(GameObject altar)
    {
        const string path = "Assets/Materials/DirectIndirectProofs/TrialAltar_GoldGlow.mat";
        Material gold = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (gold == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is missing.");
            gold = new Material(shader);
            AssetDatabase.CreateAsset(gold, path);
        }
        gold.SetColor("_BaseColor", new Color(.82f, .61f, .21f));
        gold.SetFloat("_Metallic", .55f);
        gold.SetFloat("_Smoothness", .48f);
        gold.SetColor("_EmissionColor", new Color(.42f, .22f, .04f));
        gold.EnableKeyword("_EMISSION");
        EditorUtility.SetDirty(gold);
        foreach (Renderer renderer in altar.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
                if (materials[i] != null && materials[i].name.Contains("BlueGlow"))
                { materials[i] = gold; changed = true; }
            if (changed) renderer.sharedMaterials = materials;
        }
    }

    private static Vector3 SetRenderedPosition(GameObject asset, float x, float z, float bottom)
    {
        Bounds bounds = RenderBounds(asset);
        Vector3 delta = new Vector3(x - bounds.center.x, bottom - bounds.min.y, z - bounds.center.z);
        asset.transform.position += delta;
        return delta;
    }

    private static Bounds RenderBounds(GameObject asset)
    {
        Renderer[] renderers = asset.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException(asset.name + " has no visible mesh.");
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static void EnsureMeshColliders(GameObject asset)
    {
        foreach (MeshFilter mesh in asset.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mesh.sharedMesh == null || mesh.GetComponent<Collider>() != null) continue;
            MeshCollider collider = mesh.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh.sharedMesh;
        }
    }

    private static void FitBoxCollider(GameObject asset, Bounds visible, float margin)
    {
        visible.Expand(2f * margin);
        Transform root = asset.transform;
        Vector3 min = visible.min, max = visible.max;
        Bounds local = new Bounds(root.InverseTransformPoint(min), Vector3.zero);
        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
                for (int z = 0; z < 2; z++)
                    local.Encapsulate(root.InverseTransformPoint(new Vector3(
                        x == 0 ? min.x : max.x,
                        y == 0 ? min.y : max.y,
                        z == 0 ? min.z : max.z)));
        BoxCollider collider = asset.GetComponent<BoxCollider>();
        if (collider == null) collider = asset.AddComponent<BoxCollider>();
        collider.center = local.center;
        collider.size = local.size;
        collider.isTrigger = false;
    }

    private static void RemoveAltarSigns(Transform trialRoot, GameObject direct, GameObject indirect)
    {
        foreach (string name in new[] { "Direct Proof Chamber Sign", "Indirect Proof Chamber Sign" })
        {
            Transform sign = trialRoot.Find(name);
            if (sign != null) UnityEngine.Object.DestroyImmediate(sign.gameObject);
        }
        foreach (GameObject altar in new[] { direct, indirect })
        {
            Transform plaque = altar.transform.Find("Proof Nameplate");
            if (plaque != null) UnityEngine.Object.DestroyImmediate(plaque.gameObject);
        }
    }

    private static Material EnsureSignMaterial(string name, Color color)
    {
        string path = "Assets/Materials/DirectIndirectProofs/" + name + ".mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) throw new InvalidOperationException("The URP Unlit shader is missing.");
        Material created = new Material(shader);
        created.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(created, path);
        return created;
    }

}
