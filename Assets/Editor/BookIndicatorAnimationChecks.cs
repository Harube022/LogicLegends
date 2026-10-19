#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Offline regression fixture: actual saved book instances, with gameplay scripts excluded.</summary>
public static class BookIndicatorAnimationChecks
{
    const string Flag = "Books.IndicatorQA", Report = "Library/BookIndicatorAnimationChecks.txt";

    [MenuItem("Logic Legends/Book Statues/Check Indicators Through Repeated Page Flips")]
    public static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save scene changes first.");
        var paths = new List<string>();
        for (int i = 0; i < SceneManager.sceneCount; i++) paths.Add(SceneManager.GetSceneAt(i).path);
        SessionState.SetString("Books.IndicatorQA.Paths", string.Join("|", paths));
        SessionState.SetString("Books.IndicatorQA.Active", SceneManager.GetActiveScene().path);
        var fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(fixture);
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string yaml = File.ReadAllText(path);
            if (!yaml.Contains("c967a97cb27c10044a985fd2447a67b0") && !yaml.Contains("5c1325e26a4e7a04594f32edff7a66c9")) continue;
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                foreach (var original in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BookStatuePresentation>(true)))
                {
                    var copy = UnityEngine.Object.Instantiate(original.gameObject);
                    SceneManager.MoveGameObjectToScene(copy, fixture);
                    copy.name = scene.name + " / " + Hierarchy(original.transform);
                    copy.transform.SetPositionAndRotation(new Vector3(count % 5 * 12, 0, count / 5 * 12), original.transform.rotation);
                    copy.transform.localScale = original.transform.lossyScale;
                    // Keep model, animation, labels, and presentation. Never run challenge/account logic in this fixture.
                    foreach (var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true))
                        if (behaviour != null && !(behaviour is BookStatuePresentation) && !(behaviour is TMP_Text) && !(behaviour is TMP_SubMesh))
                            UnityEngine.Object.DestroyImmediate(behaviour);
                    copy.SetActive(true); count++;
                }
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
        if (count == 0) throw new InvalidOperationException("No saved books found.");
        foreach (var path in paths) EditorSceneManager.CloseScene(SceneManager.GetSceneByPath(path), true);
        var camera = new GameObject("Fixture Camera", typeof(Camera)).GetComponent<Camera>();
        camera.tag = "MainCamera"; camera.transform.position = new Vector3(24, 22, -40);
        camera.transform.LookAt(new Vector3(24, 3, 12)); camera.fieldOfView = 65;
        File.WriteAllText(Report, "Testing " + count + " saved instances across three complete page-flip loops.\n");
        SessionState.SetBool(Flag, true);
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void Register()
    {
        EditorApplication.playModeStateChanged -= Restore; EditorApplication.playModeStateChanged += Restore;
        if (!EditorApplication.isPlayingOrWillChangePlaymode && SessionState.GetBool(Flag, false))
            EditorApplication.delayCall += RestoreScenes;
    }
    static void Restore(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Flag, false)) return;
        EditorApplication.delayCall += RestoreScenes;
    }
    static void RestoreScenes()
    {
        string[] paths = SessionState.GetString("Books.IndicatorQA.Paths", "Assets/Scenes/PRELIM.unity").Split('|');
        for (int i = 0; i < paths.Length; i++)
            if (!string.IsNullOrEmpty(paths[i])) EditorSceneManager.OpenScene(paths[i], i == 0 ? OpenSceneMode.Single : OpenSceneMode.Additive);
        var active = SceneManager.GetSceneByPath(SessionState.GetString("Books.IndicatorQA.Active", paths[0]));
        if (active.IsValid()) SceneManager.SetActiveScene(active);
        SessionState.SetBool(Flag, false);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void RunIfRequested()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        new GameObject("Repeated Page Flip Check").AddComponent<BookIndicatorCheckRunner>().StartCoroutine(Guard(Run()));
    }

    static IEnumerator Guard(IEnumerator check)
    {
        while (true)
        {
            bool more; object next;
            try { more = check.MoveNext(); next = more ? check.Current : null; }
            catch (Exception error)
            {
                File.AppendAllText(Report, "FAIL " + error + "\n"); Debug.LogException(error);
                EditorApplication.isPlaying = false; yield break;
            }
            if (!more) break;
            yield return next;
        }
        EditorApplication.isPlaying = false;
    }

    static IEnumerator Run()
    {
        Application.runInBackground = true;
        Time.timeScale = 1;
        var books = UnityEngine.Object.FindObjectsByType<BookStatuePresentation>(FindObjectsSortMode.None);
        foreach (var book in books)
        {
            foreach (var label in book.GetComponentsInChildren<TMP_Text>(true)) label.ForceMeshUpdate();
            book.SetRequired(true);
            var animator = book.GetComponent<Animator>(); animator.speed = 1; animator.Rebind(); animator.Update(0);
        }
        yield return null; yield return null;
        var anchors = books.Select(b => b.IndicatorAnchor.localPosition).ToArray();
        var firstBone = books.Select(b => b.transform.Find("Page rig/Page_00").localRotation).ToArray();
        var moved = new bool[books.Length]; var low = Enumerable.Repeat(float.PositiveInfinity, books.Length).ToArray();
        var high = Enumerable.Repeat(float.NegativeInfinity, books.Length).ToArray();
        var unstablePageBounds = new bool[books.Length];
        float until = Time.realtimeSinceStartup + 45f;
        while (Time.realtimeSinceStartup < until && books.Any(b => b.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).normalizedTime < 3.1f))
        {
            yield return null;
            for (int i = 0; i < books.Length; i++)
            {
                var book = books[i]; var glyph = book.Indicator; var anchor = book.IndicatorAnchor;
                string label = book.name;
                Check(glyph.gameObject.activeInHierarchy && glyph.enabled && book.Bubble.gameObject.activeInHierarchy && glyph.text == "!", label + ": marker stays visible");
                Check(anchor.parent == book.transform && !anchor.IsChildOf(book.transform.Find("Page rig")), label + ": stable root parent");
                Check(Vector3.Distance(anchor.localPosition, anchors[i]) < .00001f, label + ": anchor never follows page bounds");
                Check(Mathf.Abs(book.Bubble.rect.width - 105f) < .01f && Mathf.Abs(book.Bubble.rect.height - 110f) < .01f, label + ": compact circular thought bubble");
                Vector3 difference = book.Bubble.position - anchor.position;
                Check(Mathf.Abs(difference.x) < .001f && Mathf.Abs(difference.z) < .001f && difference.y >= -.001f,
                    label + ": only the intended vertical bob moves the marker");
                low[i] = Mathf.Min(low[i], difference.y); high[i] = Mathf.Max(high[i], difference.y);
                moved[i] |= Quaternion.Angle(firstBone[i], book.transform.Find("Page rig/Page_00").localRotation) > 1;
                foreach (var page in book.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    // Imported skinned bounds can extend far beyond the actual page.
                    // The regression is that even these bounds must never reposition or cull the glyph.
                    unstablePageBounds[i] |= page.bounds.max.y > anchor.position.y + 10;
                }
            }
        }
        for (int i = 0; i < books.Length; i++)
        {
            var book = books[i];
            float phase = book.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).normalizedTime;
            Check(moved[i] && phase >= 3, book.name + ": three page flips ran (phase=" + phase + ", page moved=" + moved[i] + ")");
            Check(high[i] - low[i] > .28f, book.name + ": gentle bob preserved");
            Check(high[i] - low[i] <= .321f, book.name + ": no page-driven vertical jumps above the label-safe height (span=" + (high[i] - low[i]) + ")");
            book.SetRequired(false); Check(!book.Indicator.gameObject.activeSelf, book.name + ": hide after activation");
            book.SetRequired(true); Check(book.Indicator.gameObject.activeSelf, book.name + ": show when needed again");
            Check(book.GetComponentsInChildren<TMP_Text>(true).Count(t => t.name == "Book Interaction Indicator") == 1, book.name + ": one marker");
            File.AppendAllText(Report, "PASS " + book.name + ": three flips; stable anchor; white thought bubble; bob; visibility toggles; inflated page bounds=" + unstablePageBounds[i] + "\n");
        }
        File.AppendAllText(Report, "ALL REPEATED PAGE FLIP CHECKS PASSED\n");
    }

    static string Hierarchy(Transform t) => t.parent == null ? t.name : Hierarchy(t.parent) + "/" + t.name;
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
public sealed class BookIndicatorCheckRunner : MonoBehaviour { }
#endif
