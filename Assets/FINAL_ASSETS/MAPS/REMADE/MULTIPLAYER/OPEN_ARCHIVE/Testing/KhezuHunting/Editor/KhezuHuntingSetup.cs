using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.OpenArchiveTesting;
using Photon.Pun;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LogicLegends.Tests.Khezu.Editor
{
    public static class KhezuHuntingSetup
    {
        public const string Folder = "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Testing/KhezuHunting";
        public const string ScenePath = Folder + "/OpenArchive_KhezuHunting_Test.unity";
        public const string NavigationPath = Folder + "/Khezu_WorldNavigation.asset";
        [MenuItem("Tools/Logic Legends/Khezu Phase 2/Create Hunting Test Scene")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene edits first.");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Phase 2 already exists; open its saved scene.");
            byte[] original = File.ReadAllBytes(KhezuAwakeningTestSetup.ScenePath);
            if (!AssetDatabase.CopyAsset(KhezuAwakeningTestSetup.ScenePath, ScenePath)) throw new IOException("Could not copy the Phase 1 scene.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var environment = scene.GetRootGameObjects().Single(g => g.name == "OpenArchive");
            var driver = Object.FindFirstObjectByType<KhezuAwakeningTestController>();
            var go = driver.gameObject;
            var agent = go.AddComponent<NavMeshAgent>(); agent.enabled = false;
            agent.radius = 9.7f; agent.height = 10.2f; agent.baseOffset = 0;
            agent.stoppingDistance = .1f; agent.autoTraverseOffMeshLink = false; agent.autoBraking = false;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            var body = go.AddComponent<BoxCollider>(); body.isTrigger = true;
            // Actual sampled Idle/Chase mesh bounds at authored unit scale, padded slightly.
            body.center = new Vector3(-.02f, 1.70f, .44f); body.size = new Vector3(4.25f, 3.43f, 4.62f);
            var hunting = go.AddComponent<KhezuHuntingController>(); hunting.Configure(driver);
            go.AddComponent<KhezuPhotonSync>();
            var session = Object.FindFirstObjectByType<OpenArchiveExplorationSession>();
            var run = new SerializedObject(session.Explorer).FindProperty("runSpeedMultiplier").floatValue;
            hunting.normalPlayerRunSpeed = session.Explorer.moveSpeed * run;
            var bootstrap = new GameObject("Khezu_Phase2_Session");
            var loader = bootstrap.AddComponent<KhezuNavigationData>();
            loader.data = Bake(environment, agent);
            var spawner = bootstrap.AddComponent<PlayerSpawner>(); spawner.enabled = false;
            var data = new SerializedObject(spawner);
            data.FindProperty("spawnPoint").objectReferenceValue = session.SpawnPoint;
            data.ApplyModifiedPropertiesWithoutUndo();
            var multiplayer = bootstrap.AddComponent<KhezuMultiplayerTestSession>();
            multiplayer.exploration = session; multiplayer.offlineExplorer = session.Explorer.gameObject;
            multiplayer.playerSpawner = spawner; multiplayer.spawn = session.SpawnPoint;
            multiplayer.followCamera = session.GetComponentInChildren<CinemachineCamera>(true);
            var ui = AddControls(session, hunting);
            EditorSceneManager.SaveScene(scene);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath)) { scenes.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Folder + "/Reports");
            File.WriteAllText(Folder + "/Reports/Setup.json", JsonUtility.ToJson(new SetupReport {
                sourceScenePreserved = original.SequenceEqual(File.ReadAllBytes(KhezuAwakeningTestSetup.ScenePath)),
                monsterWorldScale = go.transform.lossyScale, environmentScale = environment.transform.lossyScale,
                radius = agent.radius, height = agent.height, bodySize = Vector3.Scale(body.size, go.transform.lossyScale),
                runningPlayerSpeed = hunting.normalPlayerRunSpeed, chaseSpeed = hunting.normalPlayerRunSpeed * hunting.chaseSpeedMultiplier,
                agentType = agent.agentTypeID, scene = ScenePath
            }, true));
            Selection.activeGameObject = ui.gameObject;
            AssetDatabase.Refresh();
        }
        [Serializable] class SetupReport
        {
            public bool sourceScenePreserved;
            public string scene;
            public Vector3 monsterWorldScale, environmentScale, bodySize;
            public float radius, height, runningPlayerSpeed, chaseSpeed;
            public int agentType;
        }
        public static NavMeshData Bake(GameObject environment, NavMeshAgent agent)
        {
            Physics.SyncTransforms();
            var collected = new List<NavMeshBuildSource>();
            var valid = new HashSet<int>(environment.GetComponentsInChildren<Collider>(true)
                .Where(c => c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy && !(c is CharacterController)).Select(c => c.GetInstanceID()));
            UnityEngine.AI.NavMeshBuilder.CollectSources(environment.transform, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), collected);
            var sources = new List<NavMeshBuildSource>();
            foreach (var item in collected)
            {
                if (item.component == null || !valid.Contains(item.component.GetInstanceID())) continue;
                var source = item; bool excluded = false;
                for (var t = item.component.transform; t != null && t != environment.transform; t = t.parent)
                    if (new[] { "Cliffs", "Rivers", "Ponds", "Waterfalls" }.Contains(t.name)) excluded = true;
                source.area = excluded ? 1 : 0; sources.Add(source);
            }
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavigationPath);
            var settings = existing == null ? NavMesh.CreateSettings() : NavMesh.GetSettingsByID(new SerializedObject(existing).FindProperty("m_AgentTypeID").intValue);
            settings.agentRadius = agent.radius; settings.agentHeight = agent.height;
            settings.agentClimb = 1.6f; settings.agentSlope = 40;
            settings.minRegionArea = 2; settings.overrideVoxelSize = true; settings.voxelSize = .3f;
            settings.overrideTileSize = true; settings.tileSize = 128;
            SaveAgentSettings(settings);
            var bounds = new Bounds(environment.transform.position + Vector3.up * 10, new Vector3(560, 180, 560));
            NavMeshData result = existing;
            if (result == null)
            {
                result = UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
                if (result == null) throw new InvalidOperationException("Khezu navigation bake failed.");
                result.name = "Khezu_CurrentScale_NavMesh"; AssetDatabase.CreateAsset(result, NavigationPath);
            }
            else if (!UnityEngine.AI.NavMeshBuilder.UpdateNavMeshData(result, settings, sources, bounds)) throw new InvalidOperationException("Khezu NavMesh update failed.");
            agent.agentTypeID = settings.agentTypeID;
            EditorUtility.SetDirty(result);
            return result;
        }
        public static void SaveAgentSettings(NavMeshBuildSettings settings)
        {
            // Same serialized settings mechanism used by the installed Navigation editor.
            var project = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]);
            var agents = project.FindProperty("m_Settings");
            for (int i = 0; i < agents.arraySize; i++)
            {
                var value = agents.GetArrayElementAtIndex(i);
                if (value.FindPropertyRelative("agentTypeID").intValue != settings.agentTypeID) continue;
                value.FindPropertyRelative("agentRadius").floatValue = settings.agentRadius;
                value.FindPropertyRelative("agentHeight").floatValue = settings.agentHeight;
                value.FindPropertyRelative("agentClimb").floatValue = settings.agentClimb;
                value.FindPropertyRelative("agentSlope").floatValue = settings.agentSlope;
                value.FindPropertyRelative("minRegionArea").floatValue = settings.minRegionArea;
                value.FindPropertyRelative("manualCellSize").boolValue = true;
                value.FindPropertyRelative("cellSize").floatValue = settings.voxelSize;
                value.FindPropertyRelative("manualTileSize").boolValue = true;
                value.FindPropertyRelative("tileSize").intValue = settings.tileSize;
                project.FindProperty("m_SettingNames").GetArrayElementAtIndex(i).stringValue = "Khezu Proof Devourer";
                project.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                return;
            }
            throw new InvalidOperationException("Khezu agent type is missing from the project navigation settings.");
        }
        [MenuItem("Tools/Logic Legends/Khezu Phase 2/Rebake Current Hunting Navigation")]
        public static void Rebake()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || scene.isDirty) throw new InvalidOperationException("Open and save the hunting scene first.");
            var root = scene.GetRootGameObjects().Single(g => g.name == "OpenArchive");
            var agent = Object.FindFirstObjectByType<KhezuHuntingController>().GetComponent<NavMeshAgent>();
            Object.FindFirstObjectByType<KhezuNavigationData>().data = Bake(root, agent);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Tools/Logic Legends/Khezu Phase 2/Use Existing Lobby For Next Hosted Room")]
        public static void ChooseLobbyTarget()
        {
            if (!Application.isPlaying || PhotonNetwork.InRoom) throw new InvalidOperationException("In Main Menu Play Mode, open Multiplayer first, then choose this before hosting.");
            MultiplayerLobbyService.EnsureExists().TargetScene = "OpenArchive_KhezuHunting_Test";
            Debug.Log("Next hosted lobby uses the Khezu hunting test. Host, invite 2–4 players, ready up, then Start using the existing lobby.");
        }
        static KhezuHuntTestControls AddControls(OpenArchiveExplorationSession session, KhezuHuntingController monster)
        {
            var canvas = session.GetComponentsInChildren<Canvas>(true).First(c => c.isRootCanvas);
            var safe = canvas.GetComponentsInChildren<RectTransform>(true).First(t => t.name == "SafeArea");
            var root = Rect("Khezu_Hunting_Test_Panel", safe, new Vector2(0, -300), new Vector2(940, 150));
            root.anchorMin = root.anchorMax = new Vector2(.5f, 1);
            root.gameObject.AddComponent<Image>().color = new Color(.025f, .07f, .10f, .94f);
            var ui = root.gameObject.AddComponent<KhezuHuntTestControls>(); ui.monster = monster;
            ui.status = Label(root, "HUNT: Sleeping | Target: None\nHunted IDs: none", new Vector2(0, 40), new Vector2(920, 66));
            Button(root, "Hunt_ForceFour", "Force 4 Mistakes", -364, ui.ForceFourMistakes);
            Button(root, "Hunt_Start", "Start / Resume", -182, ui.StartHunting);
            Button(root, "Hunt_Reset", "Reset Hunt", 0, ui.ResetCurrentHunt);
            Button(root, "Hunt_ClearRotation", "Clear Rotation", 182, ui.ResetFullRotation);
            Button(root, "Hunt_Sleep", "Return to Sleep", 364, ui.ReturnToSleep);
            return ui;
        }
        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5; var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        static Text Label(Transform parent, string value, Vector2 position, Vector2 size)
        {
            var text = Rect("Label", parent, position, size).gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 20; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.raycastTarget = false; text.text = value; return text;
        }
        static void Button(Transform parent, string name, string title, float x, UnityAction callback)
        {
            var rect = Rect(name, parent, new Vector2(x, -36), new Vector2(174, 52));
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.07f, .29f, .37f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            UnityEventTools.AddPersistentListener(button.onClick, callback);
            Label(rect, title, Vector2.zero, new Vector2(170, 48));
        }
    }
}
