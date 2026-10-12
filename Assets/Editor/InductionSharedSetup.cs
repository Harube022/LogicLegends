using System;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Copies only PRELIM's shared scene scaffold, preserving connected prefab instances.</summary>
public static class InductionSharedSetup
{
    private const string TargetPath = "Assets/Scenes/Induction.unity";
    private const string SnapshotPath = "Assets/Scenes/__Codex_Induction_Prelim_Source.unity";
    private const string CollisionPath = "Assets/Scenes/InductionStageCollision.asset";
    private static readonly string[] SharedRoots = { "CameraPivot", "GameInput", "EventSystem", "LevelSpawner",
        "LevelMenuManager", "MobileInputUI", "Canvas 1", "CM_ThirdPersonCam", "TeleportManager" };

    [MenuItem("Tools/Logic Legends/Setup Induction Shared Controls")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        Scene target = SceneManager.GetActiveScene();
        if (target.path != TargetPath) throw new InvalidOperationException("Open Induction first.");
        if (target.GetRootGameObjects().Any(g => SharedRoots.Contains(g.name)))
        {
            Validate(target);
            Configure(target, target.GetRootGameObjects().Single(g => g.name == "Stage"),
                target.GetRootGameObjects().Single(g => g.name == "Main Camera"));
            EditorSceneManager.MarkSceneDirty(target);
            EditorSceneManager.SaveScene(target);
            Debug.Log("Induction shared setup already exists; no duplicate objects were created.");
            return;
        }
        var stage = target.GetRootGameObjects().First(g => g.name == "Stage");
        var mainCamera = target.GetRootGameObjects().First(g => g.name == "Main Camera");
        // A disposable serialized scene retains prefab links and button references.
        // Object.Instantiate on scene roots can lose those connections.
        Scene source = SceneManager.GetSceneByPath(SnapshotPath);
        if (!source.isLoaded)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SnapshotPath) != null)
                throw new InvalidOperationException("Temporary source path is occupied.");
            if (!AssetDatabase.CopyAsset("Assets/Scenes/PRELIM.unity", SnapshotPath))
                throw new InvalidOperationException("Could not copy PRELIM for read-only setup.");
            source = EditorSceneManager.OpenScene(SnapshotPath, OpenSceneMode.Additive);
        }
        try
        {
            var sourceCamera = source.GetRootGameObjects().First(g => g.name == "Main Camera");
            foreach (var component in sourceCamera.GetComponents<Component>())
            {
                if (component is Transform) continue;
                var destination = mainCamera.GetComponent(component.GetType());
                if (destination == null) destination = Undo.AddComponent(mainCamera, component.GetType());
                Undo.RecordObject(destination, "Copy PRELIM camera settings");
                EditorUtility.CopySerialized(component, destination);
            }
            foreach (string name in SharedRoots)
            {
                var root = source.GetRootGameObjects().Single(g => g.name == name);
                SceneManager.MoveGameObjectToScene(root, target);
            }
            SceneManager.SetActiveScene(target);
            ClearOmittedReferences(target, source);
            Configure(target, stage, mainCamera);
            Validate(target);
            EditorSceneManager.MarkSceneDirty(target);
        }
        finally
        {
            EditorSceneManager.CloseScene(source, true);
            AssetDatabase.DeleteAsset(SnapshotPath); // Only the disposable copy created for this setup.
            SceneManager.SetActiveScene(target);
        }
        EditorSceneManager.SaveScene(target);
        Debug.Log("Induction shared controls configured; PRELIM and prefab assets were not modified or unpacked.");
    }

    private static void Configure(Scene target, GameObject stage, GameObject camera)
    {
        Func<string, GameObject> root = name => target.GetRootGameObjects().Single(g => g.name == name);
        var canvas = root("Canvas 1");
        var safe = Find(canvas, "SafeArea"); var controls = Find(canvas, "Gameplay_Interface");
        var pause = Find(canvas, "PausePanel");
        foreach (Transform child in canvas.transform) if (child != safe && child != pause) Active(child.gameObject, false);
        foreach (Transform child in safe) if (child != controls) Active(child.gameObject, false);
        foreach (Transform child in controls)
            Active(child.gameObject, new[] { "LookArea", "Joystick", "JumpButton", "InteractButton", "SettingsButton" }.Contains(child.name));
        Active(controls.gameObject, true); Active(pause.gameObject, false);
        var backing = controls.GetComponent<Image>();
        Undo.RecordObject(backing, "Keep mobile gameplay view clear");
        backing.color = Color.clear; backing.raycastTarget = false; Override(backing);
        var menu = root("LevelMenuManager").GetComponent<LevelMenu>();
        Set(menu, "pausePanel", pause.gameObject);
        Click(Find(canvas, "SettingsButton").GetComponent<Button>(), menu.OpenPause);
        Click(Find(canvas, "ResumeButton").GetComponent<Button>(), menu.ClosePause);
        Click(Find(canvas, "ReturnButton").GetComponent<Button>(), menu.ReturnToMainMenu);
        Click(Find(canvas, "QuitButton").GetComponent<Button>(), menu.QuitGame);

        var floor = stage.GetComponent<MeshCollider>();
        if (floor == null) floor = Undo.AddComponent<MeshCollider>(stage);
        Undo.RecordObject(floor, "Configure Induction walk surface");
        floor.sharedMesh = WalkCollision(stage); floor.convex = false; floor.isTrigger = false; Override(floor);
        Physics.SyncTransforms();
        Bounds bounds = stage.GetComponent<Renderer>().bounds;
        var ray = new Ray(new Vector3(bounds.center.x, bounds.max.y + 5, stage.transform.position.z - 18), Vector3.down);
        if (!floor.Raycast(ray, out RaycastHit hit, bounds.size.y + 10) || hit.normal.y < .7f)
            throw new InvalidOperationException("Induction needs a clear floor at its entry point.");
        var spawner = root("LevelSpawner").GetComponent<LevelSpawner>();
        var player = new SerializedObject(spawner).FindProperty("malePrefab").objectReferenceValue as GameObject;
        var capsule = player.GetComponent<CharacterController>();
        float bottom = player.transform.TransformVector(capsule.center - Vector3.up * capsule.height * .5f).y;
        var existingSpawn = stage.transform.Find("Induction Spawn Point");
        var spawn = existingSpawn != null ? existingSpawn.gameObject : new GameObject("Induction Spawn Point");
        if (existingSpawn == null)
        {
            Undo.RegisterCreatedObjectUndo(spawn, "Create Induction entry spawn");
            spawn.transform.SetParent(stage.transform, false);
        }
        Undo.RecordObject(spawn.transform, "Position Induction entry spawn");
        spawn.transform.position = hit.point + Vector3.up * (-bottom + capsule.skinWidth * Mathf.Abs(player.transform.lossyScale.y) + .05f);
        spawn.transform.rotation = Quaternion.identity;
        var cm = root("CM_ThirdPersonCam").GetComponent<CinemachineCamera>();
        Set(spawner, "spawnPoint", spawn.transform); Set(spawner, "cmCamera", cm);
        var pivot = root("CameraPivot").GetComponent<ThirdPersonCameraController>();
        Undo.RecordObject(pivot, "Use the PRELIM Cinemachine follow rig"); pivot.enabled = false; Override(pivot);
        var teleport = root("TeleportManager").GetComponent<TeleportManager>();
        Set(teleport, "fadeCanvasGroup", Find(teleport.gameObject, "FadeScreen").GetComponent<CanvasGroup>());
        Set(teleport, "cameraScript", null);
        camera.transform.SetPositionAndRotation(spawn.transform.position + new Vector3(0,3,-6), Quaternion.Euler(20,0,0));
    }

    private static Mesh WalkCollision(GameObject stage)
    {
        var collision = AssetDatabase.LoadAssetAtPath<Mesh>(CollisionPath);
        if (collision != null) return collision;
        // The imported floor slab's upper/lower faces point inward. Keep the
        // rendering mesh untouched and correct those faces only for physics.
        collision = UnityEngine.Object.Instantiate(stage.GetComponent<MeshFilter>().sharedMesh);
        collision.name = "Induction Stage Collision";
        var vertices = collision.vertices;
        var triangles = collision.triangles;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            var a = vertices[triangles[i]];
            var b = vertices[triangles[i + 1]];
            var c = vertices[triangles[i + 2]];
            var normal = Vector3.Cross(b - a, c - a);
            if (normal.magnitude * .5f <= 100 || Mathf.Abs(normal.normalized.y) < .99f || (a.y + b.y + c.y) / 3 >= 3)
                continue;
            int previous = triangles[i + 1];
            triangles[i + 1] = triangles[i + 2];
            triangles[i + 2] = previous;
        }
        collision.triangles = triangles;
        collision.RecalculateNormals();
        AssetDatabase.CreateAsset(collision, CollisionPath);
        AssetDatabase.SaveAssets();
        return collision;
    }

    private static void ClearOmittedReferences(Scene target, Scene source)
    {
        foreach (var component in target.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Component>(true)))
        {
            if (component == null) continue;
            var serialized = new SerializedObject(component); var property = serialized.GetIterator(); bool changed = false;
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                var referenced = property.objectReferenceValue;
                var owner = referenced as GameObject ?? (referenced as Component)?.gameObject;
                if (owner == null || EditorUtility.IsPersistent(owner) || owner.scene != source) continue;
                property.objectReferenceValue = null; changed = true;
            }
            if (changed) { serialized.ApplyModifiedProperties(); Override(component); }
        }
    }

    public static void Validate(Scene target)
    {
        foreach (var name in SharedRoots)
            if (target.GetRootGameObjects().Count(g => g.name == name) != 1)
                throw new InvalidOperationException("Induction needs exactly one " + name);
        foreach (var name in new[] { "CameraPivot", "LevelSpawner", "LevelMenuManager", "Canvas 1", "CM_ThirdPersonCam" })
            if (!PrefabUtility.IsPartOfPrefabInstance(target.GetRootGameObjects().Single(g => g.name == name)))
                throw new InvalidOperationException(name + " must remain a connected prefab instance.");
        foreach (var component in target.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Component>(true)))
        {
            if (component == null) throw new InvalidOperationException("Missing script in Induction.");
            var serialized = new SerializedObject(component); var property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                var referenced = property.objectReferenceValue;
                var owner = referenced as GameObject ?? (referenced as Component)?.gameObject;
                if (owner != null && !EditorUtility.IsPersistent(owner) && owner.scene != target)
                    throw new InvalidOperationException(component.name + " has an external scene reference: " + property.propertyPath);
            }
        }
    }
    private static Transform Find(GameObject root, string name) => root.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
    private static void Override(UnityEngine.Object obj) { EditorUtility.SetDirty(obj); PrefabUtility.RecordPrefabInstancePropertyModifications(obj); }
    private static void Active(GameObject obj, bool active) { Undo.RecordObject(obj, "Configure shared UI visibility"); obj.SetActive(active); Override(obj); }
    private static void Set(UnityEngine.Object obj, string property, UnityEngine.Object value)
    { var data = new SerializedObject(obj); data.FindProperty(property).objectReferenceValue = value; data.ApplyModifiedProperties(); Override(obj); }
    private static void Click(Button button, UnityAction action)
    {
        Undo.RecordObject(button, "Bind Induction shared button");
        for (int i = button.onClick.GetPersistentEventCount()-1; i>=0; i--) UnityEventTools.RemovePersistentListener(button.onClick, i);
        UnityEventTools.AddPersistentListener(button.onClick, action); Override(button);
    }
}
