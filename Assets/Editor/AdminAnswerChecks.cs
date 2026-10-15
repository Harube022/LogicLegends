using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LogicLegends.Inference;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Opt-in QA. Never grades a player answer or writes account progress.</summary>
public static class AdminAnswerChecks
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    public static string CheckCatalogs()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run catalog inspection in Edit Mode.");
        var original = SceneManager.GetActiveScene();
        var reports = new List<string>();
        foreach (string name in new[] { "PRELIM", "Setssss", "Formal vs. Informal Proofs", "Direct&Indirect", "RulesOfInference" })
        {
            var scene = SceneManager.GetSceneByName(name); bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity", OpenSceneMode.Additive);
            try
            {
                var components = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                var pages = new List<AdminAnswerPage>();
                var before = UnityEngine.Random.state;
                if (name == "PRELIM")
                {
                    components.OfType<QuizManager>().Single().AppendAdminAnswers(pages);
                    Check(pages.Count > 1 && pages.Skip(1).All(p => p.Body.Contains("CORRECT ANSWER")), "Propositional bank answers");
                    reports.Add("Propositional: " + pages.Count + " pages"); pages.Clear();
                    components.OfType<DynamicLogicPuzzle>().First().AppendAdminAnswers(pages);
                    components.OfType<TruthTableDoorInteraction>().Single().AppendAdminAnswers(pages);
                    Check(pages.Count == 5 && pages.Last().Body.Contains("CORRECT ANSWER"), "Truth Table columns and pop quizzes");
                }
                else if (name == "Setssss") components.OfType<SetssssChallengeOne>().Single().AppendAdminAnswers(pages);
                else if (name == "Formal vs. Informal Proofs") components.OfType<ProofsLibraryStageController>().Single().AppendAdminAnswers(pages);
                else if (name == "Direct&Indirect") components.OfType<DirectIndirectProofStageController>().Single().AppendAdminAnswers(pages);
                else components.OfType<InferenceChallenge>().Single().AppendAdminAnswers(pages);
                Check(before.Equals(UnityEngine.Random.state), "viewer does not consume random state: " + name);
                Check(pages.Count >= 5 && pages.All(p => !string.IsNullOrEmpty(p.Body)), "complete grouped bank: " + name);
                int count = pages.Sum(p => p.Body.Split(new[] { "QUESTION " }, StringSplitOptions.None).Length - 1);
                if (name == "Setssss") Check(count == 45 && pages.Last().Body.Contains("complement"), "all 45 set questions and exact regions");
                if (name == "RulesOfInference") Check(count == 50 && pages.Skip(1).All(p => p.Body.Contains("Fill the blanks") && p.Body.Contains("CONCLUSION")), "all 50 inference solutions");
                if (name.Contains("Proofs") || name == "Direct&Indirect") Check(count == 50 && pages.Skip(1).All(p => p.Body.Contains("EXPECTED SOLUTION")), "all 50 proof solutions");
                reports.Add(name + ": " + pages.Count + " pages / " + count + " bank entries");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); SceneManager.SetActiveScene(original); }
        }
        return string.Join("; ", reports);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        if (!SessionState.GetBool("Admin.AnswersQA", false)) return;
        SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded;
    }
    static void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "RulesOfInference") return;
        SceneManager.sceneLoaded -= Loaded;
        var avatar = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Multiplayer_Male"));
        var sync = avatar.GetComponent<MultiplayerAvatarSync>(); if (sync != null) sync.enabled = false;
        avatar.GetComponent<CharacterController>().enabled = true;
        var book = UnityEngine.Object.FindFirstObjectByType<InferenceBookStart>();
        Move(avatar.GetComponent<Player>(), book.transform.position + Vector3.forward * 3, Quaternion.Euler(0, 180, 0));
        new GameObject("Admin QA Runner").AddComponent<AdminAnswerCheckRunner>().StartCoroutine(Run());
    }
    static IEnumerator Run()
    {
        float until = Time.unscaledTime + 45;
        while ((StageJourneyUI.Instance == null || !Field<bool>(StageJourneyUI.Instance, "tutorialOpen")) && Time.unscaledTime < until) yield return null;
        var journey = StageJourneyUI.Instance;
        journey.CloseInferenceTutorial(); yield return null;
        InferenceControlsOnboarding.Active.SkipTutorial(); InferenceControlsOnboarding.Active.ConfirmYes(); yield return null;
        var stage = UnityEngine.Object.FindFirstObjectByType<InferenceChallenge>();
        var book = UnityEngine.Object.FindFirstObjectByType<InferenceBookStart>();
        var indicator = UnityEngine.Object.FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(t => t.name == "Inference Book Waiting Indicator");
        Check(indicator.gameObject.activeSelf, "waiting book indicator is shown");
        float y = indicator.transform.position.y; yield return new WaitForSecondsRealtime(.3f);
        Check(Mathf.Abs(y - indicator.transform.position.y) > .01f, "indicator bobs");
        var access = stage.GetComponent<InferenceAltarAccess>();
        var originalAltar = GameObject.Find("ALTAR").transform.Find("Cylinder.077").GetComponent<Collider>();
        var ramp = GameObject.Find("Altar Invisible Stair Ramp").GetComponent<Collider>();
        var floor = GameObject.Find("FLOOR").GetComponent<Collider>();
        var water = GameObject.Find("Ocean50x50W750H750").GetComponent<Renderer>();
        int waterSamples = 0;
        for (int x = -40; x <= 40; x += 2) for (int z = -40; z <= 40; z += 2)
        {
            var ray = new Ray(new Vector3(access.Center.x + x, access.TopHeight + .5f, access.Center.z + z), Vector3.down);
            RaycastHit a, b, c;
            if (!originalAltar.Raycast(ray, out a, 20) && floor.Raycast(ray, out b, 20) && b.point.y < water.bounds.max.y)
            { waterSamples++; Check(!ramp.Raycast(ray, out c, 20), "no invisible support over exposed water: " + x + "," + z); }
        }
        Check(waterSamples > 10, "sampled exposed pond");
        var admin = UnityEngine.Object.FindFirstObjectByType<AdminDebugController>();
        Check(admin != null && Field<Button>(admin, "adminButton").gameObject.activeInHierarchy, "AdminButtonOn enabled in RulesOfInference");
        Move(Player.LocalInstance, book.transform.position + Vector3.forward * 3, Quaternion.Euler(0, 180, 0));
        Check(book.TryStartFromInteraction(), "actual book activation"); yield return null;
        Check(!indicator.gameObject.activeSelf, "active challenge hides indicator");
        stage.OpenBoard(Player.LocalInstance); yield return new WaitForSecondsRealtime(1); stage.CloseBoard(); yield return null;
        foreach (int size in new[] { 2, 4, 12 }) yield return CheckViewer(admin, stage, size);
        // Exercise the waiting / terminal predicates without invoking completion rewards.
        SetAuto(stage, "HasStarted", false); yield return null;
        Check(indicator.gameObject.activeSelf, "next waiting state re-shows indicator");
        SetAuto(stage, "PuzzleCompleted", true); yield return null;
        Check(!indicator.gameObject.activeSelf, "completion hides book indicator"); SetAuto(stage, "PuzzleCompleted", false);
        journey.ShowFailure(3, "QA only", () => { }); yield return null;
        Check(!indicator.gameObject.activeSelf, "Game Over hides book indicator");
        Debug.Log("ADMIN_ANSWERS_PLAY_PASS pond support / book indicator states / mobile scrolling and read-only state at 800x480, 1280x720, 2960x1440");
    }
    static IEnumerator CheckViewer(AdminDebugController admin, InferenceChallenge stage, int size)
    {
        var type = Type.GetType("UnityEditor.GameView,UnityEditor"); var view = EditorWindow.GetWindow(type);
        type.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(view, size, null); view.Focus();
        Crystal.SafeArea.Sim = size == 12 ? Crystal.SafeArea.SimDevice.Pixel3XL_LSR : size == 4 ? Crystal.SafeArea.SimDevice.iPhoneX : Crystal.SafeArea.SimDevice.None;
        yield return null; yield return new WaitForEndOfFrame(); yield return null;
        var before = Fingerprint(stage); var random = UnityEngine.Random.state;
        float time = Field<float>(StageJourneyUI.Instance, "inferenceRemaining");
        bool input = GameInput.Instance.GameplayInputBlocked; bool controls = Player.LocalInstance.enabled;
        Field<Button>(admin, "adminButton").onClick.Invoke(); yield return null;
        Check(Field<bool>(admin, "panelOpen") && Time.timeScale == 0, "Admin modal pauses");
        var root = Field<RectTransform>(admin, "panelRoot");
        root.GetComponentsInChildren<Button>(true).Single(b => b.name == "ViewQuestionsAndAnswers").onClick.Invoke();
        yield return null; yield return new WaitForEndOfFrame();
        var viewer = root.GetComponent<AdminAnswerViewer>();
        var pages = Field<IReadOnlyList<AdminAnswerPage>>(viewer, "pages");
        Check(pages.Count == 6 && pages[0].Body.Contains(stage.Question.Conclusion), "current solution from active validator");
        Check(pages.Skip(1).Sum(p => p.Body.Split(new[] { "[SELECTED" }, StringSplitOptions.None).Length - 1) >= 4, "selected randomized questions are identified");
        for (int page = 0; page < pages.Count; page++)
        {
            Invoke(viewer, "ShowPage", page); yield return null; yield return new WaitForEndOfFrame();
            var body = Field<TextMeshProUGUI>(viewer, "body"); var title = Field<TextMeshProUGUI>(viewer, "title");
            var content = Field<RectTransform>(viewer, "content"); var viewport = Field<RectTransform>(viewer, "viewport");
            body.ForceMeshUpdate(); title.ForceMeshUpdate();
            var corners = new Vector3[4]; Field<RectTransform>(viewer, "root").GetWorldCorners(corners);
            Check(corners.All(c => c.x >= -1 && c.y >= -1 && c.x <= Screen.width + 1 && c.y <= Screen.height + 1), "visible viewer stays inside the mobile screen");
            Check(!title.isTextOverflowing, "mobile viewer title fits");
            Check(content.rect.height >= body.preferredHeight, "entire answer text remains scrollable");
            var scroll = Field<ScrollRect>(viewer, "scroll");
            scroll.verticalNormalizedPosition = 0; yield return new WaitForEndOfFrame();
            Check(viewport.rect.width > 300 && body.fontSize * body.canvas.scaleFactor >= 12, "mobile readable text size");
            if (page == 4)
            {
                System.IO.Directory.CreateDirectory("Temp/AdminAnswerQA");
                ScreenCapture.CaptureScreenshot("Temp/AdminAnswerQA/Viewer-" + size + ".png");
                yield return null; yield return new WaitForEndOfFrame();
            }
        }
        root.GetComponentsInChildren<Button>(true).Single(b => b.name == "BackToAdmin").onClick.Invoke(); yield return null;
        Check(Time.timeScale == 0 && Field<RectTransform>(admin, "card").gameObject.activeSelf, "Back returns to Admin and stays paused");
        Check(time == Field<float>(StageJourneyUI.Instance, "inferenceRemaining"), "viewer consumes no time");
        Check(before == Fingerprint(stage) && random.Equals(UnityEngine.Random.state), "viewer leaves inventory, placements, question and progression untouched");
        admin.ClosePanel(); yield return null;
        Check(Time.timeScale == 1 && GameInput.Instance.GameplayInputBlocked == input && Player.LocalInstance.enabled == controls, "Close restores prior gameplay controls");
        Debug.Log("ADMIN_VIEWER_SIZE_PASS " + Screen.width + "x" + Screen.height);
    }
    static string Fingerprint(InferenceChallenge stage) => stage.SolvedRounds + ":" + stage.HasStarted + ":" + stage.Stage + ":" + stage.Question.Id + ":" + stage.Crystals.Count + ":" + stage.PlacedCrystal + ":" + Player.LocalInstance.GetHeldObject();
    static T Field<T>(object obj, string field) => (T)obj.GetType().GetField(field, Hidden).GetValue(obj);
    static void Invoke(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Hidden).Invoke(obj, args);
    static void SetAuto(object obj, string property, object value) => obj.GetType().GetField("<" + property + ">k__BackingField", Hidden).SetValue(obj, value);
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("ADMIN_ANSWERS_QA: " + message); }
    static void Move(Player p, Vector3 point, Quaternion rotation)
    {
        var cc = p.GetComponent<CharacterController>(); bool was = cc.enabled; cc.enabled = false;
        var hits = Physics.RaycastAll(point + Vector3.up * 20, Vector3.down, 60, ~0, QueryTriggerInteraction.Ignore).Where(h => h.collider.GetComponentInParent<Player>() == null && h.normal.y > .7f).OrderBy(h => h.distance).ToArray();
        if (hits.Length > 0) point.y = hits[0].point.y + cc.height * .5f - cc.center.y + .12f;
        p.transform.SetPositionAndRotation(point, rotation); cc.enabled = was; Physics.SyncTransforms();
    }
}
public sealed class AdminAnswerCheckRunner : MonoBehaviour { }
