#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Explicit, repeatable setup and inspection of the shared book statue.</summary>
[InitializeOnLoad]
public static class BookStatueProjectSetup
{
    const string Request = "Library/BookStatueSetup.request";
    const string Report = "Library/BookStatueSetup.report.txt";
    const string PrefabPath = "Assets/Prefabs/Book_Statue.prefab";
    const string ModelPath = "Assets/FINAL_ASSETS/MAPS/REMADE/Book_Statue.fbx";
    const string ControllerPath = "Assets/FINAL_ASSETS/MAPS/REMADE/BookFlip.controller";

    static BookStatueProjectSetup() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        string command = File.ReadAllText(Request).Trim();
        File.Delete(Request);
        try
        {
            if (command == "inventory") Inventory();
            else if (command == "configure") Configure();
            else if (command == "verify") Verify();
            else throw new ArgumentException("Unknown book setup command: " + command);
        }
        catch (Exception error) { File.WriteAllText(Report, error.ToString()); Debug.LogException(error); }
    }

    [MenuItem("Logic Legends/Book Statues/Inspect Project Books")]
    public static void Inventory()
    {
        var report = new StringBuilder();
        report.AppendLine("Active scene: " + SceneManager.GetActiveScene().path);
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
        {
            report.AppendLine("MODEL " + asset.GetType().Name + " " + asset.name);
            if (asset is AnimationClip clip)
                report.AppendLine("CLIP " + clip.length + "s loop=" + clip.isLooping + " paths=" +
                    string.Join(",", AnimationUtility.GetCurveBindings(clip).Select(b => b.path).Distinct()));
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
                var books = all.Where(t => t.Find("Page rig") != null || t.name.Replace("_", "").Replace(" ", "").IndexOf("BookStatue", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.GetComponent<BookInteract>() != null).ToArray();
                if (books.Length == 0) continue;
                report.AppendLine("\nSCENE " + path);
                foreach (Transform book in books)
                {
                    report.AppendLine("BOOK " + Hierarchy(book) + " prefab=" + PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(book) +
                        " pos=" + book.position + " rot=" + book.eulerAngles + " localScale=" + book.localScale);
                    foreach (Transform child in book.GetComponentsInChildren<Transform>(true))
                        report.AppendLine("  " + Hierarchy(child) + " components=" + string.Join(",", child.GetComponents<Component>()
                            .Where(c => c != null).Select(c => c.GetType().Name)));
                }
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
        File.WriteAllText(Report, report.ToString());
        Debug.Log("Book statue inventory saved to " + Report);
    }

    static string Hierarchy(Transform target) => target.parent == null ? target.name : Hierarchy(target.parent) + "/" + target.name;

    static IEnumerable<Transform> ModelRoots(Scene scene) => scene.GetRootGameObjects()
        .SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Where(t => t.Find("Page rig") != null);

    [MenuItem("Logic Legends/Book Statues/Apply Shared Presentation")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        // Save the user's open scene first; additive inspection never discards unsaved edits.
        EditorSceneManager.SaveOpenScenes();
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        ValidateClip(controller);
        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            ConfigureModel(contents.transform, controller);
            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var report = new StringBuilder("BOOK STATUE SETUP\n");
        VisitScenes(scene =>
        {
            int replacements = 0;
            if (scene.name == "PRELIM")
            {
                var interactions = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BookInteract>(true)).ToArray();
                foreach (var interaction in interactions)
                    if (ReplacePropositionalVisual(interaction, prefab)) replacements++;
            }
            var roots = ModelRoots(scene).ToArray();
            foreach (var root in roots) ConfigureModel(root, controller);
            if (roots.Length > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                report.AppendLine(scene.path + ": " + roots.Length + " animated books; " + replacements + " visual replacements.");
            }
        });
        AssetDatabase.SaveAssets();
        File.WriteAllText(Report, report.ToString());
        Debug.Log(report.ToString());
    }

    static void ConfigureModel(Transform model, RuntimeAnimatorController controller)
    {
        if (model.Find("Page rig") == null) throw new InvalidOperationException("Missing page rig: " + Hierarchy(model));
        var animators = model.GetComponents<Animator>();
        if (animators.Length > 1) throw new InvalidOperationException("Duplicate animators on " + Hierarchy(model));
        var animator = animators.FirstOrDefault() ?? model.gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.speed = 1;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.enabled = true;
        if (model.GetComponent<BookStatuePresentation>() == null) model.gameObject.AddComponent<BookStatuePresentation>();
        PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        EditorUtility.SetDirty(animator);
    }

    static bool ReplacePropositionalVisual(BookInteract interaction, GameObject prefab)
    {
        if (interaction.GetComponentInChildren<BookStatuePresentation>(true) != null) return false;
        var old = interaction.GetComponent<MeshRenderer>();
        if (old == null) throw new InvalidOperationException("Expected the original Propositional book renderer.");
        Bounds originalBounds = old.bounds;
        Vector3 position = interaction.transform.position, scale = interaction.transform.localScale;
        Quaternion rotation = interaction.transform.rotation;
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, interaction.transform);
        visual.name = "Book_Statue";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        visual.transform.localScale = Vector3.one * 200f;
        Bounds replacementBounds = GeometryBounds(visual.transform);
        float fit = originalBounds.size.y / replacementBounds.size.y;
        visual.transform.localScale *= fit;
        replacementBounds = GeometryBounds(visual.transform);
        visual.transform.position += new Vector3(originalBounds.center.x - replacementBounds.center.x,
            originalBounds.min.y - replacementBounds.min.y, originalBounds.center.z - replacementBounds.center.z);
        // Keep the original interaction object, both colliders and all serialized tutorial/quiz links.
        UnityEngine.Object.DestroyImmediate(old);
        var filter = interaction.GetComponent<MeshFilter>();
        if (filter != null) UnityEngine.Object.DestroyImmediate(filter);
        if (interaction.transform.position != position || interaction.transform.rotation != rotation ||
            interaction.transform.localScale != scale) throw new InvalidOperationException("Interaction anchor moved.");
        if (!PrefabUtility.IsPartOfPrefabInstance(visual)) throw new InvalidOperationException("Book must stay a prefab instance.");
        return true;
    }

    public static Bounds GeometryBounds(Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true).Where(r => r is SkinnedMeshRenderer ||
            r is MeshRenderer && r.GetComponent<TMPro.TMP_Text>() == null && r.GetComponent<TMPro.TMP_SubMesh>() == null).ToArray();
        if (renderers.Length == 0) throw new InvalidOperationException("Book has no geometry.");
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    static void ValidateClip(RuntimeAnimatorController controller)
    {
        if (controller == null || controller.animationClips.Length != 1) throw new InvalidOperationException("Expected one existing BookFlip clip.");
        var clip = controller.animationClips[0];
        if (Mathf.Abs(clip.length - 3f) > .01f || !clip.isLooping) throw new InvalidOperationException("Book clip must loop every three seconds.");
        var bindings = AnimationUtility.GetCurveBindings(clip);
        if (bindings.Length == 0 || bindings.Any(b => !b.path.StartsWith("Page rig/Page_", StringComparison.Ordinal)))
            throw new InvalidOperationException("Only page bones may be animated.");
        var asset = controller as AnimatorController;
        if (asset == null || asset.layers.Length != 1 || asset.layers[0].stateMachine.states.Length != 1 ||
            asset.layers[0].stateMachine.defaultState.speed != 1f) throw new InvalidOperationException("Unexpected BookFlip controller state.");
    }

    [MenuItem("Logic Legends/Book Statues/Verify Saved Books")]
    public static void Verify()
    {
        ValidateClip(AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath));
        var report = new StringBuilder("SAVED BOOK VERIFICATION\n");
        VisitScenes(scene =>
        {
            var roots = ModelRoots(scene).ToArray();
            foreach (var root in roots)
            {
                if (root.GetComponentsInChildren<Animator>(true).Length != 1 || root.GetComponentsInChildren<BookStatuePresentation>(true).Length != 1)
                    throw new InvalidOperationException("Duplicate or missing book presentation: " + Hierarchy(root));
                var animator = root.GetComponent<Animator>();
                if (animator.runtimeAnimatorController == null || animator.applyRootMotion) throw new InvalidOperationException("Missing controller or root motion enabled.");
                foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                    if (collider.transform.IsChildOf(root.Find("Page rig")) || collider.name == "Idle turning page")
                        throw new InvalidOperationException("Animated pages must not have colliders.");
            }
            foreach (var book in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BookInteract>(true)))
            {
                var child = book.transform.Find("Book_Statue");
                if (child == null || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child) != PrefabPath || book.GetComponent<MeshRenderer>() != null)
                    throw new InvalidOperationException("Propositional replacement is missing or unpacked.");
                var colliders = book.GetComponents<Collider>();
                if (colliders.Length != 2 || colliders.Count(c => c.isTrigger) != 1) throw new InvalidOperationException("Original book trigger/solid collision changed.");
                var serialized = new SerializedObject(book);
                foreach (string field in new[] { "interactButton", "quizManager", "timerManager" })
                    if (serialized.FindProperty(field).objectReferenceValue == null) throw new InvalidOperationException("Lost " + field + " reference.");
            }
            if (roots.Length > 0) report.AppendLine(scene.path + ": PASS (" + roots.Length + " linked animated books)");
        });
        File.WriteAllText(Report, report.ToString());
        Debug.Log(report.ToString());
    }

    static void VisitScenes(Action<Scene> visit)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string yaml = File.ReadAllText(path);
            if (!yaml.Contains("c967a97cb27c10044a985fd2447a67b0") && !yaml.Contains("5c1325e26a4e7a04594f32edff7a66c9")) continue;
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try { visit(scene); }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
#endif
