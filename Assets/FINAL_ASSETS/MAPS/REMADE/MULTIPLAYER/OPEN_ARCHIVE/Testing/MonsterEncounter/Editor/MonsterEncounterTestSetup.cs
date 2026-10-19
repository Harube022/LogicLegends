using System;
using System.IO;
using System.Linq;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Cinematics.VFX;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.Tests.Khezu;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace LogicLegends.Tests.Monsters.Editor
{
    public static class MonsterEncounterTestSetup
    {
        public const string Folder = "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Testing/MonsterEncounter";
        public const string ScenePath = Folder + "/OpenArchive_MonsterEncounter_Test.unity";
        const string Source = "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Testing/KhezuCinematic/OpenArchive_KhezuCinematic_Test.unity";
        const string RajangPrefab = "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/MONSTERS/PrimalAxiom/Prefabs/PrimalAxiom.prefab";
        const string OrangeBarrierStyle = "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Cinematics/VFX/CrimsonBarrier/Rajang_Orange_Barrier_Style.asset";

        [MenuItem("Tools/Logic Legends/Monster Encounter Test/Open Saved Test Scene")]
        public static void Open()
        {
            RequireSaved(); EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Selection.activeGameObject = Object.FindFirstObjectByType<DevelopmentMonsterSelection>().gameObject;
        }
        [MenuItem("Tools/Logic Legends/Monster Encounter Test/Create Shared Test Scene (Once)")]
        public static void Create()
        {
            RequireSaved();
            if (File.Exists(ScenePath)) throw new InvalidOperationException("The shared test already exists. Open the saved scene.");
            var rajangAsset = AssetDatabase.LoadAssetAtPath<GameObject>(RajangPrefab);
            if (rajangAsset == null) throw new FileNotFoundException("Existing Primal Axiom prefab is missing.", RajangPrefab);
            if (!AssetDatabase.CopyAsset(Source, ScenePath)) throw new IOException("Could not copy the existing encounter scene.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var driver = Object.FindFirstObjectByType<KhezuAwakeningTestController>();
            var actor = driver.gameObject;
            var spawn = actor.transform.parent;
            var environment = scene.GetRootGameObjects().Single(g => g.name == "OpenArchive");
            var environmentBefore = RajangR1Setup.EnvironmentSignature(environment, spawn);
            if (PrefabUtility.IsPartOfPrefabInstance(actor)) PrefabUtility.UnpackPrefabInstance(actor, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            actor.name = "Active_Test_Monster";
            var animator = actor.GetComponent<Animator>();
            var body = actor.GetComponent<BoxCollider>();
            var cinematic = actor.GetComponent<KhezuCinematicJumpscareController>();
            var barrier = actor.GetComponent<KhezuBarrierVfxPresentation>();
            var agent = actor.GetComponent<NavMeshAgent>();
            var khezu = new DevelopmentMonsterProfile {
                visualRoots = actor.transform.Cast<Transform>().Select(t => t.gameObject).ToArray(),
                avatar = animator.avatar, controller = animator.runtimeAnimatorController,
                presentationPrefab = cinematic.presentationPrefab, capture = cinematic.capture,
                barrierStyle = barrier.barrierPrefab.style,
                jumpscare = cinematic.jumpscare, barrierCast = cinematic.barrierCast,
                localScale = actor.transform.localScale, nestEuler = actor.transform.localEulerAngles,
                nestOffset = actor.transform.position - spawn.position, bodyCenter = body.center, bodySize = body.size,
                mouthUpper = cinematic.mouthUpperBone, mouthLower = cinematic.mouthLowerBone,
                faceOffset = cinematic.faceTargetOffset, framingDistance = cinematic.framingDistance,
                agentTypeId = agent.agentTypeID, agentRadius = agent.radius, agentHeight = agent.height };
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(rajangAsset, scene);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var rajangAnimator = instance.GetComponent<Animator>();
            var clips = rajangAnimator.runtimeAnimatorController.animationClips.ToDictionary(c => c.name);
            clips["Idle"].SampleAnimation(instance, 0);
            var mesh = new Mesh();
            var skin = instance.GetComponentInChildren<SkinnedMeshRenderer>();
            skin.BakeMesh(mesh);
            var bounds = new Bounds(); bool first = true;
            var toRoot = instance.transform.worldToLocalMatrix * skin.transform.localToWorldMatrix;
            foreach (var point in mesh.vertices)
            {
                var vertex = toRoot.MultiplyPoint3x4(point);
                if (first) { bounds = new Bounds(vertex, Vector3.zero); first = false; } else bounds.Encapsulate(vertex);
            }
            Object.DestroyImmediate(mesh);
            var rajang = new DevelopmentMonsterProfile {
                visualRoots = instance.transform.Cast<Transform>().Select(t => t.gameObject).ToArray(),
                avatar = rajangAnimator.avatar, controller = rajangAnimator.runtimeAnimatorController,
                presentationPrefab = rajangAsset, capture = clips["Capture"], jumpscare = clips["Jumpscare"], barrierCast = clips["Barrier_Cast"],
                barrierStyle = AssetDatabase.LoadAssetAtPath<CrimsonMathBarrierStyle>(OrangeBarrierStyle),
                localScale = Vector3.one, nestEuler = new Vector3(0, 180, 0), nestOffset = Vector3.up * .065f,
                bodyCenter = bounds.center, bodySize = bounds.size,
                mouthUpper = "Head", mouthLower = "Jaw", faceOffset = new Vector3(0, .05f, .4f), framingDistance = 1.15f };
            foreach (var root in rajang.visualRoots) root.transform.SetParent(actor.transform, false);
            Object.DestroyImmediate(instance);
            var selection = new GameObject("Monster_Test_Selection").AddComponent<DevelopmentMonsterSelection>();
            selection.ConfigureShared(driver, spawn, khezu, rajang);
            ConfigureNavigation(selection, environment);
            AddSwapButton(selection);
            // The existing barrier UI disables debug actions until its authoritative outcome completes.
            var barrierUi = Object.FindFirstObjectByType<KhezuBarrierDevelopmentUI>();
            var huntUi = Object.FindFirstObjectByType<KhezuHuntTestControls>();
            var cinematicUi = Object.FindFirstObjectByType<KhezuCinematicTestControls>();
            barrierUi.otherTestControls = new[] { huntUi.gameObject, cinematicUi.gameObject }
                .Select(g => g.GetComponent<CanvasGroup>() ?? g.AddComponent<CanvasGroup>()).ToArray();
            if (environmentBefore != RajangR1Setup.EnvironmentSignature(environment, spawn)) throw new InvalidOperationException("Environment geometry changed.");
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = selection.gameObject;
            Directory.CreateDirectory(Folder + "/Reports");
            File.WriteAllText(Folder + "/Reports/Setup.json", Newtonsoft.Json.JsonConvert.SerializeObject(new {
                scene = ScenePath, source = Source, selected = selection.Selected.ToString(), sharedControllerCount = 1,
                existingKhezuAndRajangAssetsReused = true, environmentPreserved = true, environmentSignature = environmentBefore,
                rajangBodyCenter = rajang.bodyCenter.ToString(), rajangBodySize = rajang.bodySize.ToString(), swapIsOfflineDevelopmentControl = true
            }, Newtonsoft.Json.Formatting.Indented));
        }

        public static void AddRajangNavigation()
        {
            RequireSaved();
            var selection = Object.FindFirstObjectByType<DevelopmentMonsterSelection>();
            var scene = selection.gameObject.scene;
            if (scene.path != ScenePath) throw new InvalidOperationException("Open the shared encounter test first.");
            // Older copies had a single navigation profile. Preserve its original Khezu settings.
            var data = new SerializedObject(selection);
            var khezu = data.FindProperty("khezuProfile");
            var agent = selection.ActiveTest.GetComponent<NavMeshAgent>();
            khezu.FindPropertyRelative("agentTypeId").intValue = agent.agentTypeID;
            khezu.FindPropertyRelative("agentRadius").floatValue = agent.radius;
            khezu.FindPropertyRelative("agentHeight").floatValue = agent.height;
            data.ApplyModifiedPropertiesWithoutUndo();
            ConfigureNavigation(selection, scene.GetRootGameObjects().Single(g => g.name == "OpenArchive"));
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }

        static void ConfigureNavigation(DevelopmentMonsterSelection selection, GameObject environment)
        {
            string path = Folder + "/Rajang_WorldNavigation.asset";
            var nav = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if (nav == null)
            {
                var settings = NavMesh.CreateSettings();
                settings.agentRadius = 7.2f; settings.agentHeight = 12.3f; settings.agentClimb = 1.6f; settings.agentSlope = 40;
                settings.minRegionArea = 2; settings.overrideVoxelSize = true; settings.voxelSize = .3f;
                settings.overrideTileSize = true; settings.tileSize = 128;
                LogicLegends.Tests.Khezu.Editor.KhezuHuntingSetup.SaveAgentSettings(settings);
                var config = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]);
                var agents = config.FindProperty("m_Settings");
                for (int i = 0; i < agents.arraySize; i++)
                    if (agents.GetArrayElementAtIndex(i).FindPropertyRelative("agentTypeID").intValue == settings.agentTypeID)
                        config.FindProperty("m_SettingNames").GetArrayElementAtIndex(i).stringValue = "Rajang Primal Axiom";
                config.ApplyModifiedPropertiesWithoutUndo();
                Physics.SyncTransforms();
                var sources = new System.Collections.Generic.List<NavMeshBuildSource>();
                NavMeshBuilder.CollectSources(environment.transform, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new System.Collections.Generic.List<NavMeshBuildMarkup>(), sources);
                var valid = environment.GetComponentsInChildren<Collider>(true).Where(c => c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy && !(c is CharacterController)).Select(c => c.GetInstanceID()).ToHashSet();
                sources = sources.Where(s => s.component != null && valid.Contains(s.component.GetInstanceID())).Select(s => {
                    for (var t = s.component.transform; t != null && t != environment.transform; t = t.parent)
                        if (new[] { "Cliffs", "Rivers", "Ponds", "Waterfalls" }.Contains(t.name)) { s.area = 1; break; }
                    return s;
                }).ToList();
                nav = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(environment.transform.position + Vector3.up * 10, new Vector3(560, 180, 560)), Vector3.zero, Quaternion.identity);
                if (nav == null) throw new InvalidOperationException("Rajang navigation bake failed.");
                nav.name = "Rajang_WorldNavigation"; AssetDatabase.CreateAsset(nav, path);
            }
            var profile = selection.RajangEncounterProfile;
            profile.agentTypeId = new SerializedObject(nav).FindProperty("m_AgentTypeID").intValue;
            profile.agentRadius = 7.2f; profile.agentHeight = 12.3f;
            if (!selection.gameObject.scene.GetRootGameObjects().Any(g => g.name == "Rajang_Test_Navigation"))
                new GameObject("Rajang_Test_Navigation").AddComponent<KhezuNavigationData>().data = nav;
            selection.SelectRajang(); EditorUtility.SetDirty(selection);
        }

        static void AddSwapButton(DevelopmentMonsterSelection selection)
        {
            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Single(c => c.isRootCanvas);
            var transforms = canvas.GetComponentsInChildren<Transform>(true);
            var row = transforms.Single(t => t.name == "Hunt_Reset_Actions");
            var template = row.GetChild(0).gameObject;
            var buttonObject = Object.Instantiate(template, row); buttonObject.name = "Swap_Active_Monster";
            var button = buttonObject.GetComponent<Button>();
            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(button.onClick, i);
            var ui = buttonObject.AddComponent<DevelopmentMonsterSwapButton>();
            ui.selection = selection; ui.button = button; ui.label = button.GetComponentInChildren<Text>(); ui.label.text = "Swap to Khezu";
            ui.heading = transforms.Single(t => t.name == "Show_Hide_Test_Tools").Find("Title").GetComponent<Text>();
            ui.heading.text = "RAJANG TEST TOOLS";
            ui.awakenLabel = transforms.Single(t => t.name == "Hunt_ForceFour").GetComponentInChildren<Text>();
            ui.awakenLabel.text = "Awaken Rajang";
            var barrier = Object.FindFirstObjectByType<KhezuBarrierDevelopmentUI>();
            ui.barrierDescription = barrier.panel.transform.Find("Description").GetComponent<Text>();
            ui.barrierDescription.text = "Rajang has trapped you inside a mathematical seal!";
            UnityEventTools.AddPersistentListener(button.onClick, ui.Swap);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)row);
        }
        static void RequireSaved()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save current edits first.");
        }
    }
}
