using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LogicLegends.Tests.Khezu.Editor
{
    public static class KhezuAwakeningTestSetup
    {
        public const string Folder = "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Testing/KhezuAwakening";
        public const string ScenePath = Folder + "/OpenArchive_KhezuAwakening_Test.unity";
        public const string SourceScene = "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Scenes/MultiplayerGameplay.unity";
        public const string MonsterFolder = "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/MONSTERS/ProofDevourer";
        public const string PrefabPath = MonsterFolder + "/Prefabs/ProofDevourer.prefab";
        public const string ControllerPath = MonsterFolder + "/Animations/ProofDevourer.controller";

        [MenuItem("Tools/Logic Legends/Khezu Phase 1/Open Saved Test Scene")]
        public static void Open()
        {
            RequireSavedEditMode();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Selection.activeGameObject = Object.FindFirstObjectByType<KhezuAwakeningTestController>().gameObject;
        }

        [MenuItem("Tools/Logic Legends/Khezu Phase 1/Create Test Scene (Once)")]
        public static void Create()
        {
            RequireSavedEditMode();
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Test scene already exists. Open the saved scene instead.");
            if (!AssetDatabase.CopyAsset(SourceScene, ScenePath)) throw new IOException("Could not copy the saved environment scene.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var environment = scene.GetRootGameObjects().Single(g => g.name == "OpenArchive");
            var spawn = environment.transform.Find("UniversalMonsterNest/MonsterSpawnPoint");
            if (spawn == null || environment.transform.lossyScale != Vector3.one) throw new InvalidOperationException("Expected the canonical unit-scale environment and existing MonsterSpawnPoint.");
            int originalObjects = environment.GetComponentsInChildren<Transform>(true).Length;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var monster = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            monster.name = "Khezu_Awakening_Test";
            monster.transform.SetParent(spawn, false);
            monster.transform.localPosition = Vector3.zero;
            monster.transform.localRotation = Quaternion.identity;
            monster.transform.localScale = Vector3.one;
            var animator = monster.GetComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var test = monster.AddComponent<KhezuAwakeningTestController>();
            var serialized = new SerializedObject(test);
            serialized.FindProperty("animator").objectReferenceValue = animator;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // Only the copied scene's presentation camera is disabled. Source scenes stay untouched.
            foreach (var camera in environment.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            var cameraObject = new GameObject("Khezu_Test_Review_Camera", typeof(Camera), typeof(AudioListener));
            var review = cameraObject.GetComponent<Camera>();
            review.tag = "MainCamera";
            review.nearClipPlane = .1f;
            review.farClipPlane = 500;
            review.fieldOfView = 52;
            review.transform.position = spawn.position + new Vector3(7, 4.8f, 10);
            review.transform.LookAt(spawn.position + new Vector3(0, 1.35f, 0));
            AddExplorationControlsInternal();
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = monster;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.LookAt(spawn.position + Vector3.up, review.transform.rotation, 11);
            Directory.CreateDirectory(Folder + "/Reports");
            File.WriteAllText(Folder + "/Reports/Setup.json", JsonUtility.ToJson(new SetupReport {
                sourceScene = SourceScene, testScene = ScenePath, prefab = PrefabPath,
                originalEnvironmentObjects = originalObjects,
                position = monster.transform.position, scale = monster.transform.lossyScale,
                anchor = "OpenArchive/UniversalMonsterNest/MonsterSpawnPoint",
                usesExistingController = animator.runtimeAnimatorController == AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath)
            }, true));
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Logic Legends/Khezu Phase 1/Add Existing Exploration Controls")]
        public static void AddExplorationControls()
        {
            RequireSavedEditMode();
            AddExplorationControlsInternal();
        }

        static void AddExplorationControlsInternal()
        {
            var targetScene = SceneManager.GetActiveScene();
            if (targetScene.path != ScenePath) throw new InvalidOperationException("Open the saved Khezu test scene first.");
            if (targetScene.GetRootGameObjects().Any(g => g.name == "ExplorationTestRig")) return;
            var sourcePath = OpenArchiveAuthoring.OpenArchiveExplorationBuilder.TestScenePath;
            var sourceScene = SceneManager.GetSceneByPath(sourcePath);
            bool openedHere = !sourceScene.IsValid() || !sourceScene.isLoaded;
            if (openedHere) sourceScene = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(targetScene);
                // Clone the already configured local harness, including UI event wiring and
                // internal camera/player references. Never rebuild shared player assets.
                var sourceRoots = sourceScene.GetRootGameObjects();
                var input = Object.Instantiate(sourceRoots.Single(g => g.name == "Exploration_GameInput"));
                input.name = "Exploration_GameInput";
                var rig = Object.Instantiate(sourceRoots.Single(g => g.name == "ExplorationTestRig"));
                rig.name = "ExplorationTestRig";
                var session = rig.GetComponent<LogicLegends.OpenArchiveTesting.OpenArchiveExplorationSession>();
                var spawn = targetScene.GetRootGameObjects().Single(g => g.name == "OpenArchive")
                    .transform.Find("PlayerSpawnArea/" + session.SpawnPoint.name);
                var player = session.Explorer;
                var follow = rig.GetComponentInChildren<Unity.Cinemachine.CinemachineCamera>(true);
                session.Configure(player, spawn, follow);
                player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
                var data = new SerializedObject(player);
                data.FindProperty("gameInput").objectReferenceValue = input.GetComponent<GameInput>();
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(player);
                PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
                foreach (var component in rig.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) continue;
                    var serialized = new SerializedObject(component);
                    var property = serialized.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        var go = property.objectReferenceValue as GameObject;
                        var reference = property.objectReferenceValue as Component;
                        if (reference != null) go = reference.gameObject;
                        if (go != null && go.scene == sourceScene)
                            throw new InvalidOperationException("Unresolved source-scene reference: " + component.name + "/" + property.propertyPath);
                    }
                }
                var review = targetScene.GetRootGameObjects().Single(g => g.name == "Khezu_Test_Review_Camera");
                review.GetComponent<Camera>().enabled = false;
                review.GetComponent<AudioListener>().enabled = false;
                review.tag = "Untagged";
                EditorSceneManager.MarkSceneDirty(targetScene);
                EditorSceneManager.SaveScene(targetScene);
                Selection.activeGameObject = player.gameObject;
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(sourceScene, true);
                SceneManager.SetActiveScene(targetScene);
            }
        }

        static void RequireSavedEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save current scene edits first.");
        }

        [Serializable] class SetupReport
        {
            public string sourceScene, testScene, prefab, anchor;
            public int originalEnvironmentObjects;
            public Vector3 position, scale;
            public bool usesExistingController;
        }

        public static void Capture(string name)
        {
            var camera = GameObject.Find("Khezu_Test_Review_Camera").GetComponent<Camera>();
            Directory.CreateDirectory(Folder + "/Reports");
            var rt = new RenderTexture(1440, 1000, 24);
            var previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                pixels = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Folder + "/Reports/" + name + ".png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                if (pixels != null) Object.DestroyImmediate(pixels);
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }
    }
}
