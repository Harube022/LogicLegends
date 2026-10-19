using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LogicLegends.OpenArchiveTesting;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OpenArchiveAuthoring
{
    public static class OpenArchiveExplorationBuilder
    {
        public const string TestScenePath = OpenArchiveBuilder.Folder + "/Scenes/OpenArchive_Explore.unity";
        // Resolve by GUID so the user's rename to MultiplayerGameplay remains intact.
        const string EnvironmentGuid = "6a705d4447127c547bf25f4a7c90bf23";
        const string PlayerPrefab = "Assets/Prefabs/PREFABS/CHARACTERS/Player.prefab";
        const string DefaultUIActions = "Packages/com.unity.inputsystem/InputSystem/Plugins/PlayerInput/DefaultInputActions.inputactions";
        static Font font;

        [MenuItem("Tools/Logic Legends/Open Archive/Create Exploration Test Scene")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene edits before creating the exploration copy.");
            string sourcePath = AssetDatabase.GUIDToAssetPath(EnvironmentGuid);
            if (string.IsNullOrEmpty(sourcePath)) sourcePath = OpenArchiveBuilder.ScenePath;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(sourcePath) == null) throw new InvalidOperationException("The Open Archive source scene was not found.");
            byte[] sourceBefore = File.ReadAllBytes(sourcePath);
            var source = SceneManager.GetSceneByPath(sourcePath);
            if (!source.IsValid() || !source.isLoaded) source = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(source);
            var previousTest = SceneManager.GetSceneByPath(TestScenePath);
            if (previousTest.IsValid() && previousTest.isLoaded && !EditorSceneManager.CloseScene(previousTest, true))
                throw new InvalidOperationException("Could not unload the old exploration scene.");
            if (!EditorSceneManager.SaveScene(source, TestScenePath, true)) throw new InvalidOperationException("Could not save an exploration copy.");
            var scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            var archive = scene.GetRootGameObjects().Single(g => g.name == "OpenArchive");
            var spawn = archive.transform.Find("PlayerSpawnArea/PlayerSpawn_03");
            if (spawn == null) throw new InvalidOperationException("The player spawn anchor is missing.");
            foreach (var camera in archive.GetComponentsInChildren<Camera>(true)) camera.gameObject.SetActive(false);

            Directory.CreateDirectory(OpenArchiveBuilder.Folder + "/Exploration");
            Directory.CreateDirectory(OpenArchiveBuilder.Folder + "/Prefabs/Exploration");
            AssetDatabase.Refresh();
            var inputAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PREFABS/WORLD_UI/GameInput.prefab");
            var inputObject = (GameObject)PrefabUtility.InstantiatePrefab(inputAsset);
            inputObject.name = "Exploration_GameInput"; // Must stay a root: GameInput uses DontDestroyOnLoad.
            var input = inputObject.GetComponent<GameInput>();
            var rig = new GameObject("ExplorationTestRig");
            var session = rig.AddComponent<OpenArchiveExplorationSession>();
            var player = CreateExplorer(rig.transform, spawn);
            var inputReference = new SerializedObject(player);
            inputReference.FindProperty("gameInput").objectReferenceValue = input;
            inputReference.ApplyModifiedPropertiesWithoutUndo();

            var target = player.transform.Find("CameraTarget");
            target.localRotation = Quaternion.Euler(12, 0, 0);
            var cameraObject = new GameObject("Exploration_Camera", typeof(Camera), typeof(AudioListener), typeof(CinemachineBrain));
            cameraObject.transform.SetParent(rig.transform, false); cameraObject.tag = "MainCamera";
            var output = cameraObject.GetComponent<Camera>(); output.fieldOfView = 60; output.farClipPlane = 650; output.nearClipPlane = .2f;
            output.allowHDR = false;
            cameraObject.transform.position = spawn.position + new Vector3(0, 7, -13);
            cameraObject.transform.LookAt(target.position);
            var virtualObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PREFABS/WORLD_UI/CM_ThirdPersonCam.prefab"));
            virtualObject.transform.SetParent(rig.transform, false);
            var virtualCamera = virtualObject.GetComponent<CinemachineCamera>();
            virtualCamera.Target.TrackingTarget = target;
            virtualCamera.Lens.FieldOfView = 60;
            virtualCamera.Lens.NearClipPlane = .2f; virtualCamera.Lens.FarClipPlane = 650;
            virtualCamera.Lens.ModeOverride = LensSettings.OverrideModes.Perspective;
            PrefabUtility.RecordPrefabInstancePropertyModifications(virtualCamera);
            CreateControls(rig.transform, session);
            session.Configure(player, spawn, virtualCamera);
            if (!EditorSceneManager.SaveScene(scene, TestScenePath)) throw new InvalidOperationException("Could not save the exploration controls.");
            AssetDatabase.SaveAssets();
            if (!sourceBefore.SequenceEqual(File.ReadAllBytes(sourcePath))) throw new InvalidOperationException("The source environment changed unexpectedly.");
            Selection.activeGameObject = player.gameObject;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
            Debug.Log("Exploration ready. Play OpenArchive_Explore: WASD, right-side drag to look, Space to jump, R to return. Original environment preserved.");
        }

        static Player CreateExplorer(Transform parent, Transform spawn)
        {
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(original);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = "Archive_Explorer";
            // Keep one local outfit. No equipment service or multiplayer spawner is needed to explore.
            Object.DestroyImmediate(instance.GetComponent<PlayerEquipmentLoader>());
            foreach (Transform child in instance.transform.Cast<Transform>().ToArray())
                if (child.name != "UNIFORM_MALE" && child.name != "CameraTarget" && child.name != "HoldPoint") Object.DestroyImmediate(child.gameObject);
            instance.transform.Find("UNIFORM_MALE").gameObject.SetActive(true);
            foreach (var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)) renderer.quality = SkinQuality.Bone4;
            instance.transform.position = Vector3.zero;
            var player = instance.GetComponent<Player>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, OpenArchiveBuilder.Folder + "/Prefabs/Exploration/Archive_Explorer.prefab");
            Object.DestroyImmediate(instance);
            var saved = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            saved.transform.SetParent(parent, false); saved.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            PrefabUtility.RecordPrefabInstancePropertyModifications(saved.transform);
            return saved.GetComponent<Player>();
        }

        static void CreateControls(Transform parent, OpenArchiveExplorationSession session)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            // Copy the actual shared controls and artwork used by MultiplayerStage and the lesson scenes.
            var canvasObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PREFABS/WORLD_UI/Canvas 1.prefab"));
            PrefabUtility.UnpackPrefabInstance(canvasObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            canvasObject.transform.SetParent(parent, false);
            var safe = Find(canvasObject, "SafeArea"); var gameplay = Find(canvasObject, "Gameplay_Interface"); var pause = Find(canvasObject, "PausePanel");
            foreach (Transform child in canvasObject.transform.Cast<Transform>().ToArray())
                if (child != safe && child != pause) Object.DestroyImmediate(child.gameObject);
            foreach (Transform child in safe.Cast<Transform>().ToArray())
                if (child != gameplay) Object.DestroyImmediate(child.gameObject);
            foreach (Transform child in gameplay.Cast<Transform>().ToArray())
                if (!new[] { "LookArea", "Joystick", "JumpButton", "InteractButton", "SettingsButton" }.Contains(child.name)) Object.DestroyImmediate(child.gameObject);
            safe.gameObject.SetActive(true); gameplay.gameObject.SetActive(true); pause.gameObject.SetActive(false);
            foreach (Transform child in gameplay) child.gameObject.SetActive(true);
            var backing = gameplay.GetComponent<Image>(); backing.color = Color.clear; backing.raycastTarget = false;
            var canvas = canvasObject.GetComponent<RectTransform>();
            var mobile = canvasObject.GetComponentInChildren<MobileInputUI>(true);
            Click(Find(canvasObject, "JumpButton").GetComponent<UnityEngine.UI.Button>(), mobile.Jump);
            Click(Find(canvasObject, "InteractButton").GetComponent<UnityEngine.UI.Button>(), mobile.Interact);
            var menuObject = new GameObject("Exploration_LevelMenu", typeof(LevelMenu)); menuObject.transform.SetParent(parent, false);
            var menu = menuObject.GetComponent<LevelMenu>(); var menuData = new SerializedObject(menu);
            menuData.FindProperty("pausePanel").objectReferenceValue = pause.gameObject; menuData.ApplyModifiedPropertiesWithoutUndo();
            Click(Find(canvasObject, "SettingsButton").GetComponent<UnityEngine.UI.Button>(), menu.OpenPause);
            Click(Find(canvasObject, "ResumeButton").GetComponent<UnityEngine.UI.Button>(), menu.ClosePause);
            Click(Find(canvasObject, "ReturnButton").GetComponent<UnityEngine.UI.Button>(), menu.ReturnToMainMenu);
            Click(Find(canvasObject, "QuitButton").GetComponent<UnityEngine.UI.Button>(), menu.QuitGame);
            var title = Rect("Controls_Help", canvas, new Vector2(0, -52), new Vector2(1100, 70));
            Anchor(title, new Vector2(.5f, 1));
            title.gameObject.AddComponent<Image>().color = new Color(.025f, .07f, .10f, .83f);
            Label(title, "Explore the Open Archive\nWASD / joystick: move   |   Right-side drag: look   |   Space / jump button: jump", 24);
            Button(canvas, "Return_To_Spawn", new Vector2(-155, -132), new Vector2(240, 48), session.ReturnToSpawn, "Return to spawn (R)", true);
            var eventObject = new GameObject("Exploration_EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventObject.transform.SetParent(parent, false);
            var module = eventObject.GetComponent<InputSystemUIInputModule>();
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(DefaultUIActions);
            var references = AssetDatabase.LoadAllAssetsAtPath(DefaultUIActions).OfType<InputActionReference>().ToArray();
            Func<string, InputActionReference> reference = name => references.First(r => r.action != null && r.action.id == actions.FindAction("UI/" + name, true).id);
            module.actionsAsset = actions; module.point = reference("Point"); module.leftClick = reference("Click");
            module.rightClick = reference("RightClick"); module.middleClick = reference("MiddleClick");
            module.scrollWheel = reference("ScrollWheel"); module.move = reference("Navigate");
            module.submit = reference("Submit"); module.cancel = reference("Cancel");
        }

        static Transform Find(GameObject root, string name) { return root.GetComponentsInChildren<Transform>(true).First(t => t.name == name); }
        static void Click(UnityEngine.UI.Button button, UnityAction action)
        { button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent(); UnityEventTools.AddPersistentListener(button.onClick, action); }

        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5; go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        static void Anchor(RectTransform rect, Vector2 anchor) { rect.anchorMin = rect.anchorMax = anchor; }
        static Text Label(RectTransform parent, string value, int size)
        {
            var rect = Rect("Label", parent, Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(6, 2); rect.offsetMax = new Vector2(-6, -2);
            var text = rect.gameObject.AddComponent<Text>(); text.font = font; text.text = value; text.fontSize = size;
            text.color = new Color(.94f, .97f, .95f); text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; return text;
        }
        static GameObject Button(RectTransform canvas, string name, Vector2 position, Vector2 size, UnityAction action, string label, bool top = false)
        {
            var rect = Rect(name, canvas, position, size); Anchor(rect, new Vector2(1, top ? 1 : 0));
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.035f, .23f, .30f, .94f);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            UnityEventTools.AddPersistentListener(button.onClick, action); Label(rect, label, 21); return rect.gameObject;
        }

        [Serializable] public class PlayReport { public bool passed; public string generatedUtc; public List<PlayCheck> checks = new List<PlayCheck>(); public string limits; }
        [Serializable] public class PlayCheck { public string name; public bool passed; public string detail; }
        public static void RunPlayChecks()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Run these checks in the exploration scene's Play mode.");
            var session = Object.FindFirstObjectByType<OpenArchiveExplorationSession>();
            if (session == null) throw new InvalidOperationException("The exploration session is not loaded.");
            session.StartCoroutine(CheckControls(session));
        }
        static IEnumerator CheckControls(OpenArchiveExplorationSession session)
        {
            var report = new PlayReport { generatedUtc = DateTime.UtcNow.ToString("o"), limits = "Editor Play-mode checks use UI pointer events and existing player physics. Physical Android multitouch and multiplayer were not tested." };
            Action<string, bool, string> check = (name, passed, detail) => report.checks.Add(new PlayCheck { name = name, passed = passed, detail = detail });
            var player = session.Explorer; var controller = player.GetComponent<CharacterController>();
            session.ReturnToSpawn(); yield return new WaitForSecondsRealtime(.25f);
            check("Visible local player at spawn", Player.LocalInstance == player && controller.isGrounded && player.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r => r.enabled), player.transform.position.ToString("F3"));
            var joystick = MobileInputUI.FindJoystick(); var joystickRect = (RectTransform)joystick.transform;
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, joystickRect.TransformPoint(Vector3.up * joystickRect.rect.height * .4f)), button = PointerEventData.InputButton.Left };
            Vector3 start = player.transform.position;
            ExecuteEvents.Execute(joystick.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            yield return new WaitForSecondsRealtime(.45f);
            ExecuteEvents.Execute(joystick.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            check("Shared joystick moves actual CharacterController", Vector3.Distance(start, player.transform.position) > 1, "Travelled " + Vector3.Distance(start, player.transform.position).ToString("F2") + "m.");
            check("Releasing joystick stops input", GameInput.Instance.GetMovementVectorNormalized() == Vector2.zero, "Existing MobileInputUI pointer-up callback.");
            yield return new WaitForSecondsRealtime(.2f);
            float initialY = player.transform.position.y, peak = initialY; bool jumpEvent = false;
            Action jumped = () => jumpEvent = true; player.OnJumpStarted += jumped;
            FindButton("JumpButton").onClick.Invoke();
            float deadline = Time.realtimeSinceStartup + 1.2f;
            while (Time.realtimeSinceStartup < deadline) { peak = Mathf.Max(peak, player.transform.position.y); yield return null; }
            player.OnJumpStarted -= jumped;
            check("Shared jump button triggers jump physics", jumpEvent && peak - initialY > 1, "Peak rise " + (peak - initialY).ToString("F2") + "m.");
            var target = player.transform.Find("CameraTarget"); float yaw = target.eulerAngles.y;
            var look = Object.FindFirstObjectByType<MobileLookInput>();
            pointer.position = new Vector2(Screen.width * .7f, Screen.height * .5f);
            ExecuteEvents.Execute(look.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            pointer.position += new Vector2(160, 0);
            ExecuteEvents.Execute(look.gameObject, pointer, ExecuteEvents.dragHandler);
            yield return new WaitForSecondsRealtime(.15f);
            ExecuteEvents.Execute(look.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            check("Shared look drag rotates camera target", Mathf.Abs(Mathf.DeltaAngle(yaw, target.eulerAngles.y)) > 5, "Yaw changed " + Mathf.DeltaAngle(yaw, target.eulerAngles.y).ToString("F1") + " degrees.");
            FindButton("SettingsButton").onClick.Invoke(); yield return new WaitForSecondsRealtime(.1f);
            check("Shared settings opens pause", Time.timeScale == 0 && GameInput.Instance.GameplayInputBlocked, "Existing LevelMenu pause handling.");
            FindButton("ResumeButton").onClick.Invoke(); yield return new WaitForSecondsRealtime(.1f);
            check("Shared resume restores input", Time.timeScale > 0 && !GameInput.Instance.GameplayInputBlocked, "Existing LevelMenu resume handling.");
            FindButton("Return_To_Spawn").onClick.Invoke(); yield return new WaitForSecondsRealtime(.15f);
            check("Return-to-spawn test utility", AtSpawnOnPlaza(session), player.transform.position.ToString("F3"));
            controller.enabled = false; player.transform.position = new Vector3(session.SpawnPoint.position.x, -9, session.SpawnPoint.position.z); controller.enabled = true;
            yield return new WaitForSecondsRealtime(.2f);
            check("Falling off island recovers to spawn", AtSpawnOnPlaza(session), player.transform.position.ToString("F3"));
            check("Shared camera pinch-zoom script present", Object.FindFirstObjectByType<CinemachinePinchZoom>() != null, "Original CM_ThirdPersonCam prefab, original sensitivity and pinch limits.");
            check("One output camera and listener", Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c => c.enabled) == 1 && Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(c => c.enabled) == 1, "Environment preview camera disabled in test copy.");
            report.passed = report.checks.All(c => c.passed);
            File.WriteAllText(OpenArchiveBuilder.Folder + "/Previews/ExplorationValidation.json", JsonUtility.ToJson(report, true));
            ScreenCapture.CaptureScreenshot(OpenArchiveBuilder.Folder + "/Previews/OpenArchive_Explore_Play.png");
            Debug.Log("Open Archive exploration controls checks: " + report.passed);
        }
        static UnityEngine.UI.Button FindButton(string name) { return Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(b => b.name == name); }
        static bool AtSpawnOnPlaza(OpenArchiveExplorationSession session)
        {
            var player = session.Explorer; var controller = player.GetComponent<CharacterController>();
            Vector3 position = player.transform.position, anchor = session.SpawnPoint.position;
            var plaza = session.SpawnPoint.parent.Find("Spawn_StonePlaza_18m").GetComponent<Collider>();
            float scale = Mathf.Abs(player.transform.lossyScale.y);
            float bottom = position.y + (controller.center.y - controller.height * .5f) * scale;
            float clearance = bottom - plaza.bounds.max.y;
            return Vector2.Distance(new Vector2(position.x, position.z), new Vector2(anchor.x, anchor.z)) < .2f
                && controller.isGrounded && clearance >= -.02f && clearance < .25f;
        }
    }
}
