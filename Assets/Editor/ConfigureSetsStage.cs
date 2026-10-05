using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class ConfigureSetsStage
{
    private const string ScenePath = "Assets/Scenes/Sets.unity";
    private const string CatalogPath = "Assets/Resources/SetsChallengeCatalog.asset";
    private const string ElementPrefabPath = "Assets/Prefabs/PREFABS/Sets/SetElement.prefab";
    private const string ElementMaterialPath = "Assets/Materials/Sets/SetElement.mat";

    static ConfigureSetsStage()
    {
        EditorApplication.delayCall += AutoConfigureOpenSetsScene;
    }

    [MenuItem("Logic Legends/Setup Sets Stage")]
    public static void SetupSetsStage()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("Stop Play Mode before setting up the Sets stage.");
            return;
        }

        if (!System.IO.File.Exists(ScenePath))
        {
            Debug.LogError($"Could not find the Sets scene at {ScenePath}.");
            return;
        }

        Scene previousActive = EditorSceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedForSetup = !scene.IsValid() || !scene.isLoaded;
        if (openedForSetup) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            ApplySetup(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            if (openedForSetup) EditorSceneManager.CloseScene(scene, true);
            if (previousActive.IsValid() && previousActive.isLoaded)
                EditorSceneManager.SetActiveScene(previousActive);
        }
        Debug.Log("Sets stage setup is complete. Open Assets/Scenes/Sets.unity and press Play to begin.");
    }

    private static void AutoConfigureOpenSetsScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedForSetup = !scene.IsValid() || !scene.isLoaded;
        if (openedForSetup) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        try
        {
            if (FindInScene<SetsStageManager>(scene) == null)
            {
                ApplySetup(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
            }
        }
        finally
        {
            if (openedForSetup) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void ApplySetup(Scene scene)
    {
        if (!scene.IsValid() || scene.path != ScenePath)
            throw new InvalidOperationException("Sets setup must run in Assets/Scenes/Sets.unity.");

        EnsureBuildSettingsEntry();
        SetsChallengeCatalog catalog = EnsureCatalog();
        SetElement elementPrefab = EnsureElementPrefab();

        GameObject gameInput = EnsurePrefabInstance("Assets/Prefabs/PREFABS/WORLD_UI/GameInput.prefab", "GameInput", scene);
        GameObject mobileCanvas = EnsurePrefabInstance("Assets/Prefabs/PREFABS/WORLD_UI/Canvas.prefab", "Canvas", scene);
        GameObject dialogueObject = EnsurePrefabInstance("Assets/Prefabs/PREFABS/WORLD_UI/DialogueManager.prefab", "DialogueManager", scene);
        GameObject levelObject = EnsurePrefabInstance("Assets/Prefabs/PREFABS/WORLD_UI/LevelManager.prefab", "LevelManager", scene);
        GameObject gameOverObject = EnsurePrefabInstance("Assets/Prefabs/PREFABS/WORLD_UI/GameOverManager.prefab", "GameOverManager", scene);
        GameObject stageManagerObject = EnsurePrefabInstance("Assets/Prefabs/PREFABS/WORLD_UI/StageManager.prefab", "Sets Stage Progression", scene);

        GameObject playerObject = EnsurePrefabInstance("Assets/Prefabs/PREFABS/CHARACTERS/Player.prefab", "Player", scene);
        GameObject cameraObject = EnsurePrefabInstance("Assets/Prefabs/PREFABS/WORLD_UI/CM_ThirdPersonCam.prefab", "CM_ThirdPersonCam", scene);

        if (mobileCanvas == null || dialogueObject == null || levelObject == null || gameOverObject == null || stageManagerObject == null || playerObject == null)
            throw new InvalidOperationException("One or more existing Logic Legends prefabs could not be loaded. Check the Console for the missing prefab path.");

        // The Canvas prefab's root is serialized at a zero scale; normalize only this scene instance.
        mobileCanvas.transform.localScale = Vector3.one;

        EnsureEventSystem(scene);
        GameInput input = gameInput != null ? gameInput.GetComponent<GameInput>() : null;
        if (input == null) Debug.LogWarning("GameInput prefab is missing GameInput; the Sets scene will not receive the existing Interact input.");

        GameObject spawnerObject = FindGameObjectInScene(scene, "Spawner");
        Transform spawner = spawnerObject != null ? spawnerObject.transform : null;
        Vector3 spawnPosition = spawner != null ? spawner.position : new Vector3(-250.67f, 0.5f, 943.85f);
        if (playerObject != null)
        {
            playerObject.transform.SetPositionAndRotation(spawnPosition + Vector3.back * 2.25f,
                Quaternion.LookRotation(Vector3.forward, Vector3.up));
        }

        Player player = playerObject.GetComponent<Player>();
        GameObject diagramObject = FindGameObjectInScene(scene, "AB");
        Transform diagram = diagramObject != null ? diagramObject.transform : null;
        Vector3 diagramCenter = diagram != null ? diagram.position : new Vector3(-230f, 0f, 917.7f);
        SetPlacementZone[] zones = EnsureZones(diagramCenter, scene);

        GameObject runtimeRoot = FindGameObjectInScene(scene, "Sets Stage Runtime");
        if (runtimeRoot == null) runtimeRoot = new GameObject("Sets Stage Runtime");
        SceneManager.MoveGameObjectToScene(runtimeRoot, scene);

        SetsUIController ui = runtimeRoot.GetComponent<SetsUIController>();
        if (ui == null) ui = Undo.AddComponent<SetsUIController>(runtimeRoot);

        LevelManager levelManager = levelObject.GetComponent<LevelManager>();
        GameOverManager gameOverManager = gameOverObject.GetComponent<GameOverManager>();
        StageCompleteManager stageCompleteManager = stageManagerObject.GetComponent<StageCompleteManager>();
        if (levelManager == null || gameOverManager == null || stageCompleteManager == null)
            throw new InvalidOperationException("A reused stage prefab is missing its manager component.");

        levelManager.ConfigureSetsStage(gameOverManager);
        SerializedObject gameOverSerialized = new SerializedObject(gameOverManager);
        gameOverSerialized.FindProperty("gameplayInterfacePanel").objectReferenceValue = mobileCanvas;
        gameOverSerialized.ApplyModifiedPropertiesWithoutUndo();
        ConfigureProgression(stageCompleteManager, mobileCanvas);
        ConfigureMobileInteraction(dialogueObject.GetComponent<DialogueManager>(), mobileCanvas);

        SetsStageManager manager = runtimeRoot.GetComponent<SetsStageManager>();
        if (manager == null) manager = Undo.AddComponent<SetsStageManager>(runtimeRoot);
        manager.ConfigureForScene(catalog, elementPrefab, spawner, zones, levelManager, gameOverManager, stageCompleteManager, ui);

        GameObject statue = EnsureBookStatue(spawnPosition, scene);
        WizardInteraction wizard = statue != null ? statue.GetComponent<WizardInteraction>() : null;
        if (wizard != null)
        {
            if (wizard.OnWizardInteract == null) wizard.OnWizardInteract = new UnityEngine.Events.UnityEvent();
            bool alreadyWired = Enumerable.Range(0, wizard.OnWizardInteract.GetPersistentEventCount())
                .Any(i => wizard.OnWizardInteract.GetPersistentTarget(i) == manager &&
                          wizard.OnWizardInteract.GetPersistentMethodName(i) == nameof(SetsStageManager.ActivateCurrentChallenge));
            if (!alreadyWired)
                UnityEventTools.AddPersistentListener(wizard.OnWizardInteract, manager.ActivateCurrentChallenge);
        }

        SetupCamera(player, cameraObject, scene);
        EditorUtility.SetDirty(runtimeRoot);
        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static SetsChallengeCatalog EnsureCatalog()
    {
        EnsureFolder("Assets/Resources");
        SetsChallengeCatalog catalog = AssetDatabase.LoadAssetAtPath<SetsChallengeCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<SetsChallengeCatalog>();
            catalog.SetChallenges(SetsChallengeCatalog.CreateDefaultChallenges());
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        else if (catalog.Challenges == null || catalog.Challenges.Count == 0)
        {
            catalog.SetChallenges(SetsChallengeCatalog.CreateDefaultChallenges());
            EditorUtility.SetDirty(catalog);
        }

        return catalog;
    }

    private static SetElement EnsureElementPrefab()
    {
        EnsureFolder("Assets/Materials/Sets");
        EnsureFolder("Assets/Prefabs/PREFABS/Sets");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(ElementMaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) shader = Shader.Find("Standard");
            material = new Material(shader) { name = "Set Element Teal" };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(0.13f, 0.62f, 0.7f, 1f));
            else if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(0.13f, 0.62f, 0.7f, 1f));
            AssetDatabase.CreateAsset(material, ElementMaterialPath);
        }

        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ElementPrefabPath);
        if (existingPrefab == null)
        {
            GameObject element = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            element.name = "SetElement";
            element.transform.localScale = Vector3.one * 0.72f;
            element.GetComponent<Renderer>().sharedMaterial = material;
            Rigidbody body = element.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            GameObject labelObject = new GameObject("Value Label", typeof(TextMeshPro));
            labelObject.transform.SetParent(element.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0f, 0.48f);
            labelObject.transform.localRotation = Quaternion.identity;
            labelObject.transform.localScale = Vector3.one * 0.18f;
            TextMeshPro label = labelObject.GetComponent<TextMeshPro>();
            if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = 4f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.text = "1";

            SetElement setElement = element.AddComponent<SetElement>();
            SerializedObject serializedElement = new SerializedObject(setElement);
            serializedElement.FindProperty("valueLabel").objectReferenceValue = label;
            serializedElement.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(element, ElementPrefabPath);
            UnityEngine.Object.DestroyImmediate(element);
            AssetDatabase.ImportAsset(ElementPrefabPath);
            existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ElementPrefabPath);
        }

        return existingPrefab != null ? existingPrefab.GetComponent<SetElement>() : null;
    }

    private static SetPlacementZone[] EnsureZones(Vector3 center, Scene scene)
    {
        GameObject root = FindGameObjectInScene(scene, "Sets Placement Zones");
        if (root == null) root = new GameObject("Sets Placement Zones");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.position = Vector3.zero;

        var definitions = new[]
        {
            new ZoneSetup("A ONLY", SetZone.A_ONLY, center + Vector3.left * 2.6f, 0),
            new ZoneSetup("A ∩ B", SetZone.INTERSECTION, center, int.MaxValue),
            new ZoneSetup("B ONLY", SetZone.B_ONLY, center + Vector3.right * 2.6f, 0),
            new ZoneSetup("OUTSIDE A (inside U)", SetZone.OUTSIDE, center + Vector3.forward * 4.6f, 0)
        };

        var zones = new List<SetPlacementZone>(definitions.Length);
        foreach (ZoneSetup definition in definitions)
        {
            Transform existing = root.transform.Find(definition.name);
            GameObject zoneObject;
            if (existing == null)
            {
                zoneObject = new GameObject(definition.name);
                zoneObject.transform.SetParent(root.transform, true);
                zoneObject.AddComponent<BoxCollider>();
                zoneObject.AddComponent<SetPlacementZone>();
            }
            else
            {
                zoneObject = existing.gameObject;
                if (zoneObject.GetComponent<BoxCollider>() == null) zoneObject.AddComponent<BoxCollider>();
                if (zoneObject.GetComponent<SetPlacementZone>() == null) zoneObject.AddComponent<SetPlacementZone>();
            }

            zoneObject.transform.SetPositionAndRotation(definition.position, Quaternion.identity);
            BoxCollider trigger = zoneObject.GetComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1.05f, 0f);
            trigger.size = new Vector3(2.1f, 2.35f, 2.1f);

            Transform point = zoneObject.transform.Find("Placement Point");
            if (point == null)
            {
                GameObject pointObject = new GameObject("Placement Point");
                pointObject.transform.SetParent(zoneObject.transform, false);
                point = pointObject.transform;
            }
            point.localPosition = new Vector3(0f, 0.55f, 0f);
            point.localRotation = Quaternion.identity;

            SetPlacementZone placementZone = zoneObject.GetComponent<SetPlacementZone>();
            placementZone.Configure(definition.zone, point, definition.priority);
            zones.Add(placementZone);
            EnsureZoneLabel(root.transform, definition);
        }

        return zones.ToArray();
    }

    private static void EnsureZoneLabel(Transform root, ZoneSetup definition)
    {
        string labelName = definition.name + " Label";
        Transform existing = root.Find(labelName);
        if (existing == null)
        {
            GameObject labelObject = new GameObject(labelName, typeof(TextMeshPro));
            labelObject.transform.SetParent(root, true);
            existing = labelObject.transform;
        }

        existing.position = definition.position + Vector3.up * 1.2f;
        existing.rotation = Quaternion.Euler(90f, 0f, 0f);
        TextMeshPro label = existing.GetComponent<TextMeshPro>();
        if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
        label.text = definition.name;
        label.fontSize = 3f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
    }

    private static GameObject EnsureBookStatue(Vector3 spawnerPosition, Scene scene)
    {
        GameObject statue = FindGameObjectInScene(scene, "Sets Book Statue");
        if (statue == null)
        {
            GameObject statueModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FINAL_ASSETS/BOOKSTATUE.fbx");
            if (statueModel == null)
            {
                Debug.LogWarning("Assets/FINAL_ASSETS/BOOKSTATUE.fbx is missing. A trigger-only Book Statue activator was created; add the model when it is available.");
                statue = new GameObject("Sets Book Statue");
            }
            else
            {
                statue = (GameObject)PrefabUtility.InstantiatePrefab(statueModel, scene);
                statue.name = "Sets Book Statue";
            }
        }

        SceneManager.MoveGameObjectToScene(statue, scene);
        statue.transform.position = spawnerPosition + Vector3.right * 2.5f;
        statue.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);

        Renderer[] renderers = statue.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            if (bounds.size.y > 0.01f)
                statue.transform.localScale *= Mathf.Clamp(1.8f / bounds.size.y, 0.05f, 20f);
        }

        SphereCollider trigger = statue.GetComponent<SphereCollider>();
        if (trigger == null) trigger = statue.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 0.9f, 0f);
        trigger.radius = 2.8f;

        WizardInteraction wizard = statue.GetComponent<WizardInteraction>();
        if (wizard == null) wizard = statue.AddComponent<WizardInteraction>();
        if (wizard.OnWizardInteract == null) wizard.OnWizardInteract = new UnityEngine.Events.UnityEvent();
        return statue;
    }

    private static void ConfigureMobileInteraction(DialogueManager dialogueManager, GameObject canvas)
    {
        if (dialogueManager == null || canvas == null) return;
        Transform interactButton = canvas.transform.Find("InteractButton");
        if (interactButton != null) dialogueManager.ConfigureInteractButton(interactButton.gameObject);
    }

    private static void ConfigureProgression(StageCompleteManager stageCompleteManager, GameObject mobileCanvas)
    {
        if (stageCompleteManager == null) return;
        SerializedObject serializedManager = new SerializedObject(stageCompleteManager);
        serializedManager.FindProperty("gameplayInterfacePanel").objectReferenceValue = mobileCanvas;
        serializedManager.FindProperty("thisStageNumber").intValue = StageSelectionState.LastStage;
        serializedManager.FindProperty("nextSceneName").stringValue = "Main Menu";
        serializedManager.FindProperty("mainMenuSceneName").stringValue = "Main Menu";
        serializedManager.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(stageCompleteManager);
    }

    private static void SetupCamera(Player player, GameObject virtualCamera, Scene scene)
    {
        if (player == null) return;
        GameObject mainCameraObject = FindGameObjectInScene(scene, "Main Camera");
        Camera mainCamera = mainCameraObject != null ? mainCameraObject.GetComponent<Camera>() : null;
        if (mainCamera != null)
        {
            mainCamera.transform.position = player.transform.position + Vector3.up * 3.2f + Vector3.back * 6f;
            mainCamera.transform.rotation = Quaternion.LookRotation(player.transform.position + Vector3.up * 1.2f - mainCamera.transform.position, Vector3.up);
            if (mainCamera.GetComponent<CinemachineBrain>() == null)
                Undo.AddComponent<CinemachineBrain>(mainCamera.gameObject);
        }

        if (virtualCamera != null)
            virtualCamera.transform.position = player.transform.position + Vector3.up * 2f;
    }

    private static void EnsureEventSystem(Scene scene)
    {
        EventSystem eventSystem = FindInScene<EventSystem>(scene);
        if (eventSystem != null) return;

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        SceneManager.MoveGameObjectToScene(eventSystemObject, scene);
        eventSystemObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    private static GameObject EnsurePrefabInstance(string assetPath, string sceneObjectName, Scene scene)
    {
        GameObject existing = FindGameObjectInScene(scene, sceneObjectName);
        if (existing != null) return existing;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
        {
            Debug.LogError($"Missing required Logic Legends prefab: {assetPath}");
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = sceneObjectName;
        return instance;
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }
        return null;
    }

    private static GameObject FindGameObjectInScene(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == objectName) return root;
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (child.name == objectName) return child.gameObject;
            }
        }
        return null;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folder = System.IO.Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folder);
    }

    private static void EnsureBuildSettingsEntry()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        if (scenes.Any(scene => scene.path == ScenePath)) return;
        List<EditorBuildSettingsScene> updated = scenes.ToList();
        updated.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = updated.ToArray();
    }

    private struct ZoneSetup
    {
        public readonly string name;
        public readonly SetZone zone;
        public readonly Vector3 position;
        public readonly int priority;

        public ZoneSetup(string name, SetZone zone, Vector3 position, int priority)
        {
            this.name = name;
            this.zone = zone;
            this.position = position;
            this.priority = priority;
        }
    }
}
