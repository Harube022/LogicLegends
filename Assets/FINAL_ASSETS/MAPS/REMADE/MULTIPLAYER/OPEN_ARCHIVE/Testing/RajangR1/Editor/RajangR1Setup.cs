using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LogicLegends.Tests.Khezu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LogicLegends.Tests.Monsters.Editor
{
    public static class RajangR1Setup
    {
        public const string Folder = "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Testing/RajangR1";
        public const string ScenePath = Folder + "/OpenArchive_RajangR1_Test.unity";
        public const string SourceScene = "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Testing/KhezuAwakening/OpenArchive_KhezuAwakening_Test.unity";
        public const string AssetFolder = "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/MONSTERS/PrimalAxiom";
        public const string PrefabPath = AssetFolder + "/Prefabs/PrimalAxiom.prefab";

        [MenuItem("Tools/Logic Legends/Rajang R1/Open Saved Test Scene")]
        public static void Open()
        {
            RequireSaved();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Selection.activeGameObject = Object.FindFirstObjectByType<DevelopmentMonsterSelection>().gameObject;
        }

        [MenuItem("Tools/Logic Legends/Rajang R1/Create Test Scene (Once)")]
        public static void Create()
        {
            RequireSaved();
            // Validate the actual prefab before creating or modifying a scene.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new FileNotFoundException("Rajang / Primal Axiom is missing. No scene was modified.", PrefabPath);
            var assetAnimator = prefab.GetComponent<Animator>();
            var assetController = assetAnimator == null ? null : assetAnimator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
            string[] required = { "Sleep", "Wake_01", "Wake_02", "Wake_03", "Full_Awaken", "Idle" };
            if (assetController == null || required.Any(n => !assetController.layers[0].stateMachine.states.Any(s => s.state.name == n && s.state.motion is AnimationClip)))
                throw new InvalidOperationException("The existing Rajang Animator is missing an awakening state. No scene was modified.");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("R1 already exists. Open the saved test scene.");
            var sourceBytes = File.ReadAllBytes(SourceScene);
            if (!AssetDatabase.CopyAsset(SourceScene, ScenePath)) throw new IOException("Could not copy the existing animation-test scene.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var environment = scene.GetRootGameObjects().Single(g => g.name == "OpenArchive");
            var spawn = environment.transform.Find("UniversalMonsterNest/MonsterSpawnPoint");
            if (spawn == null) throw new InvalidOperationException("The existing MonsterSpawnPoint is missing.");
            var before = EnvironmentSignature(environment, spawn);
            var khezu = spawn.GetComponentsInChildren<KhezuAwakeningTestController>(true).Single();
            khezu.gameObject.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(khezu.gameObject);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "Rajang_Test";
            instance.transform.SetParent(spawn, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(0, 180, 0); // South entrance / player arrival.
            instance.transform.localScale = Vector3.one; // Inherit the same 2.5x arena unit scale.
            var bed = spawn.parent.Find("Den_Compacted_Sleeping_Bed").GetComponent<Renderer>();
            var position = spawn.position;
            position.y = Mathf.Max(position.y, bed.bounds.max.y) + .005f;
            instance.transform.position = position;
            var animator = instance.GetComponent<Animator>();
            animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var driver = instance.AddComponent<KhezuAwakeningTestController>();
            var data = new SerializedObject(driver);
            data.FindProperty("animator").objectReferenceValue = animator;
            data.ApplyModifiedPropertiesWithoutUndo();
            var settings = new GameObject("R1_Monster_Selection").AddComponent<DevelopmentMonsterSelection>();
            settings.Configure(khezu, driver);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);

            var review = scene.GetRootGameObjects().Single(g => g.name == "Khezu_Test_Review_Camera");
            review.name = "Rajang_Test_Review_Camera";
            review.transform.position = position + new Vector3(18, 13, -24);
            review.transform.LookAt(position + Vector3.up * 6);
            review.GetComponent<Camera>().enabled = false;
            review.GetComponent<AudioListener>().enabled = false;
            review.tag = "Untagged";
            if (before != EnvironmentSignature(environment, spawn)) throw new InvalidOperationException("Environment geometry changed unexpectedly.");
            if (!sourceBytes.SequenceEqual(File.ReadAllBytes(SourceScene))) throw new InvalidOperationException("The original Khezu scene changed.");
            if (scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).Any(c => c != null &&
                (c.GetType().Name == "KhezuHuntingController" || c.GetType().Name == "KhezuCaptureEncounterController" || c.GetType().Name == "KhezuPhotonSync")))
                throw new InvalidOperationException("R1 must not contain later-phase controllers.");
            EditorSceneManager.SaveScene(scene);
            Directory.CreateDirectory(Folder + "/Reports");
            File.WriteAllText(Folder + "/Reports/Setup.json", JsonUtility.ToJson(new SetupReport {
                sourceScene = SourceScene, scene = ScenePath, prefab = PrefabPath,
                environmentSignature = before, environmentPreserved = true, sourceScenePreserved = true,
                selected = settings.Selected.ToString(), position = instance.transform.position,
                worldScale = instance.transform.lossyScale, forward = instance.transform.forward,
                onlyOneActiveMonster = instance.activeInHierarchy && !khezu.gameObject.activeInHierarchy,
                usesExistingAnimator = animator.runtimeAnimatorController == assetAnimator.runtimeAnimatorController,
                entrances = spawn.parent.Cast<Transform>().Count(t => t.name.StartsWith("Entrance_"))
            }, true));
            Selection.activeGameObject = settings.gameObject;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(position + Vector3.up * 5, review.transform.rotation, 27);
            Debug.Log("Rajang R1 is ready. Select R1_Monster_Selection, enter Play Mode and use its Inspector controls.");
        }

        public static string EnvironmentSignature(GameObject environment, Transform spawn)
        {
            var text = new StringBuilder();
            foreach (var t in environment.GetComponentsInChildren<Transform>(true))
            {
                if (t != spawn && t.IsChildOf(spawn)) continue;
                text.Append(t.name).Append('|').Append(t.localPosition.ToString("R")).Append(t.localRotation.ToString("R")).Append(t.localScale.ToString("R"));
                text.Append('|').Append(t.gameObject.layer).Append('|').Append(t.gameObject.activeSelf);
                var filter = t.GetComponent<MeshFilter>();
                if (filter != null) text.Append('|').Append(Reference(filter.sharedMesh));
                foreach (var r in t.GetComponents<Renderer>())
                    text.Append('|').Append(r.enabled).Append('|').Append(string.Join(",", r.sharedMaterials.Select(Reference)));
                foreach (var collider in t.GetComponents<Collider>())
                {
                    text.Append('|').Append(collider.GetType().Name).Append(collider.enabled).Append(collider.isTrigger);
                    var mesh = collider as MeshCollider;
                    if (mesh != null) text.Append(Reference(mesh.sharedMesh));
                }
                text.AppendLine();
            }
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "");
        }
        static string Reference(Object asset)
        {
            if (asset == null) return "null";
            string guid; long id;
            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out guid, out id) ? guid + ":" + id : asset.name;
        }
        static void RequireSaved()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save current scene edits first.");
        }
        [Serializable] sealed class SetupReport
        {
            public string sourceScene, scene, prefab, environmentSignature, selected;
            public bool environmentPreserved, sourceScenePreserved, onlyOneActiveMonster, usesExistingAnimator;
            public int entrances;
            public Vector3 position, worldScale, forward;
        }
    }
}
