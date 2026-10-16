#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Runs against the saved Sets scene, without accounts or completion rewards.</summary>
public static class SetssssIntroductionPlayChecks
{
    const string Flag = "Sets.IntroductionQA";
    const string Report = "Library/SetssssIntroductionChecks.txt";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void BeginFocusedFour() { SessionState.SetBool(Flag + ".Focus4", true); Begin(); }
    [MenuItem("Logic Legends/Sets/Check Challenge Introductions")]
    public static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name != "Setssss") throw new InvalidOperationException("Open Setssss outside Play Mode.");
        if (!UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes()) return;
        SessionState.SetInt(Flag + ".Size", GetSize()); SessionState.SetBool(Flag, true);
        File.WriteAllText(Report, "Sets: per-challenge introductions and world region labels\n");
        EditorApplication.isPlaying = true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded;
    }
    static void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Setssss") return;
        SceneManager.sceneLoaded -= Loaded;
        var avatar = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Multiplayer_Male"));
        avatar.GetComponent<MultiplayerAvatarSync>().enabled = false;
        avatar.GetComponent<PlayerEquipmentLoader>().enabled = false;
        foreach (var skin in avatar.GetComponentsInChildren<PlayerAnimator>(true)) skin.gameObject.SetActive(skin.name == "UNIFORM_MALE");
        avatar.GetComponent<CharacterController>().enabled = true;
        var stage = UnityEngine.Object.FindFirstObjectByType<SetssssChallengeOne>();
        Set(stage, "onChallengeCompleted", new UnityEngine.Events.UnityEvent());
        MoveToBook(stage, avatar.GetComponent<Player>());
        new GameObject("Sets Introduction QA Runner").AddComponent<SetsIntroductionCheckRunner>().StartCoroutine(Guard(Run(stage)));
    }
    static IEnumerator Guard(IEnumerator run)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(run);
        while (stack.Count > 0)
        {
            object next = null; bool more;
            try { more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current; }
            catch (Exception error) { File.AppendAllText(Report, "FAIL " + error + "\n"); Debug.LogException(error); Finish(); yield break; }
            if (!more) { stack.Pop(); continue; }
            if (next is IEnumerator child) stack.Push(child); else yield return next;
        }
        File.AppendAllText(Report, "ALL INTRODUCTION CHECKS PASSED\n"); Finish();
    }
    static void Finish()
    {
        SetSize(SessionState.GetInt(Flag + ".Size", 6));
        Crystal.SafeArea.Sim = Crystal.SafeArea.SimDevice.None;
        SessionState.SetBool(Flag, false); EditorApplication.isPlaying = false;
        SessionState.SetBool(Flag + ".Focus4", false);
    }
    static IEnumerator Run(SetssssChallengeOne stage)
    {
        Application.runInBackground = true; SetSize(6);
        var help = UnityEngine.Object.FindFirstObjectByType<SetssssHelpUI>();
        float deadline = Time.unscaledTime + 40;
        while ((help == null || !Field<bool>(help, "open")) && Time.unscaledTime < deadline)
        { help = UnityEngine.Object.FindFirstObjectByType<SetssssHelpUI>(); yield return null; }
        Check(help != null && Field<bool>(help, "open"), "first Help appears before challenge introduction");
        Check(!stage.Introductions.IsOpen && !stage.Started, "introduction does not conflict with initial Help");
        Invoke(help, "BeginFromHelp"); yield return null;
        var controls = SetssssControlsOnboarding.Active;
        Check(controls != null && controls.IsActive && !stage.Introductions.IsOpen, "control practice retains priority");
        controls.SkipTutorial(); Set(help, "shownOnEntry", false); controls.ConfirmYes();
        yield return new WaitForSecondsRealtime(.15f);
        Check(!stage.Introductions.IsOpen, "initial Help retains priority when player loading is delayed");
        Set(help, "shownOnEntry", true); yield return null;
        var intro = stage.Introductions;
        var player = Player.LocalInstance;
        for (int challenge = 1; challenge <= 5; challenge++)
        {
            deadline = Time.unscaledTime + 8;
            while (!intro.IsOpen && Time.unscaledTime < deadline) yield return null;
            Check(intro.IsOpen && stage.ChallengeNumber == challenge && !intro.HasSeen(challenge), "automatic introduction for challenge " + challenge);
            if (SessionState.GetBool(Flag + ".Focus4", false) && challenge < 4)
            {
                intro.Skip(); yield return null;
                MoveToBook(stage, player); yield return new WaitForSecondsRealtime(.25f);
                GameInput.Instance.MobileInteract(); yield return null;
                Check(stage.Started && stage.DebugClearCurrentChallenge(), "advance to focused check");
                yield return null; continue;
            }
            float remaining = stage.TimeRemaining;
            Vector3 position = player.transform.position;
            int question = stage.QuestionIndex;
            int[] selection = stage.SelectedEntryIndices.ToArray();
            Check(Time.timeScale == 0 && GameInput.Instance.GameplayInputBlocked && !player.enabled, "intro pauses timer and gameplay input");
            Check(!stage.Started && remaining == 1080 && stage.ActiveCondition == null, "no real question or timer started by introduction");
            Check(UnityEngine.Object.FindObjectsByType<PrelimTutorialSkipControl>(FindObjectsSortMode.None).Length == 1, "one shared Skip Tutorial");
            Check(!stage.GetComponent<SetssssChallengePresentation>().IsIntroductionOpen, "no old Challenge 4 popup");
            for (int page = 0; page < intro.PageCount; page++)
            {
                foreach (int size in challenge == 1 || challenge == 4 ? new[] { 6, 12, 3 } : new[] { 12 })
                {
                    SetSize(size); yield return new WaitForSecondsRealtime(1.35f);
                    float framedBy = Time.unscaledTime + 2;
                    while (!intro.NextButton.interactable && Time.unscaledTime < framedBy) yield return null;
                    yield return new WaitForEndOfFrame();
                    yield return CheckLayout(intro, challenge);
                    yield return Capture("Challenge" + challenge + "_Page" + (page + 1));
                }
                stage.Submit(); stage.DeductTime(10); GameInput.Instance.MobileInteract();
                Check(stage.TimeRemaining == remaining && stage.QuestionIndex == question && !stage.Started && stage.SelectedEntryIndices.SequenceEqual(selection), "intro inputs cannot consume time or change questions");
                Check(player.GetHeldObject() == null && Vector3.Distance(position, player.transform.position) < .001f, "practice has no carry or movement effects");
                if (intro.CurrentFocus == SetssssChallengeIntroductions.Focus.Spawner)
                {
                    var examples = Field<GameObject>(intro, "examples");
                    Check(examples != null && examples.GetComponentsInChildren<GrabbableObject>().Length == 0 && examples.GetComponentsInChildren<Collider>().Length == 0, "practice samples are visual only");
                }
                Click(intro.NextButton);
                if (page == intro.PageCount - 1) break;
                yield return null;
            }
            yield return null; yield return new WaitForEndOfFrame();
            Check(!intro.IsOpen && intro.HasSeen(challenge) && !stage.Started && stage.TimeRemaining == remaining, "completion waits for the real book interaction");
            Check(Time.timeScale == 1 && player.enabled && !GameInput.Instance.GameplayInputBlocked, "completion restores prior controls");
            Check(GameObject.Find("Sets Challenge Introduction Camera") == null && GameObject.Find("Sets PRACTICE EXAMPLES — not gameplay elements") == null, "no camera or sample content remains");
            Check(UnityEngine.Object.FindObjectsByType<PrelimTutorialSkipControl>(FindObjectsSortMode.None).Length == 0, "shared Skip is cleaned up");
            CheckLabels(stage);
            MoveToBook(stage, player); yield return new WaitForSecondsRealtime(.25f);
            GameInput.Instance.MobileInteract(); yield return null;
            Check(stage.Started, "real book starts challenge " + challenge);
            Check(stage.DebugClearCurrentCondition(), "normal question advancement available");
            yield return new WaitForSecondsRealtime(1.4f);
            Check(stage.ChallengeNumber == challenge && stage.QuestionIndex == 1 && !intro.IsOpen, "introduction does not repeat before another question");
            Check(stage.DebugClearCurrentChallenge(), "normal challenge advancement available");
            yield return null;
            File.AppendAllText(Report, "PASS Challenge " + challenge + ": paused lesson pages, examples, mobile layouts, cleanup, real book start, no repeat per question.\n");
        }
        Check(stage.Complete && !intro.IsOpen, "final completion has no extra introduction");
        // Restart a run, then skip a moving camera transition.
        var results = UnityEngine.Object.FindFirstObjectByType<SetssssStageResultUI>();
        if (results != null) UnityEngine.Object.Destroy(results.gameObject);
        stage.RestartChallenge(); yield return null;
        yield return new WaitForSecondsRealtime(.15f); yield return new WaitForEndOfFrame();
        Check(intro.IsOpen && !intro.HasSeen(1), "new playthrough resets introduction history");
        float time = stage.TimeRemaining;
        var skip = UnityEngine.Object.FindFirstObjectByType<PrelimTutorialSkipControl>();
        Click(skip.GetComponent<Button>()); yield return null; yield return new WaitForEndOfFrame();
        Check(intro.HasSeen(1) && !intro.IsOpen && !stage.Started && stage.TimeRemaining == time && player.enabled && Time.timeScale == 1, "skip cancels transition and waits for book");
        Check(GameObject.Find("Sets Challenge Introduction Camera") == null, "skip removes active tutorial camera");
        MoveToBook(stage, player); yield return new WaitForSecondsRealtime(.25f); GameInput.Instance.MobileInteract(); yield return null;
        stage.DeductTime(2000); yield return null;
        Check(stage.IsGameOver, "Game Over reached for retry check");
        stage.RestartChallenge(); yield return null;
        Check(!intro.IsOpen && intro.HasSeen(1) && !stage.Started, "retry preserves once-per-playthrough history");
        File.AppendAllText(Report, "PASS new-run reset, skip during camera motion, Game Over retry, correct A ∩ B / U labels; no progression rewards written.\n");
    }
    static IEnumerator CheckLayout(SetssssChallengeIntroductions intro, int challenge)
    {
        Canvas.ForceUpdateCanvases();
        var card = Field<RectTransform>(intro, "card"); var safe = Field<RectTransform>(intro, "safe");
        var viewport = Field<RectTransform>(intro, "bodyViewport");
        Check(Contains(ScreenRect(safe), ScreenRect(card)), "lesson stays in safe area");
        Check(!ScreenRect(viewport).Overlaps(ScreenRect((RectTransform)intro.NextButton.transform)), "reading area and Next do not overlap");
        var text = Field<TMP_Text>(intro, "body"); text.ForceMeshUpdate();
        var heading = Field<TMP_Text>(intro, "heading"); heading.ForceMeshUpdate();
        Check(!text.isTextOverflowing && !heading.isTextOverflowing, "lesson text fits or scrolls without clipping");
        CheckRaycast(intro.NextButton);
        var skip = UnityEngine.Object.FindFirstObjectByType<PrelimTutorialSkipControl>(); CheckRaycast(skip.GetComponent<Button>());
        Check(!ScreenRect(card).Overlaps(ScreenRect(skip.GetComponent<RectTransform>())), "Skip clear of instruction panel");
        var highlight = Field<RectTransform>(intro, "highlight");
        Check(highlight.gameObject.activeSelf && !ScreenRect(highlight).Overlaps(ScreenRect(card)), "highlight stays clear of explanation: C" + challenge + " page " + intro.PageIndex + " " + Screen.width + "x" + Screen.height + " highlight=" + ScreenRect(highlight) + " card=" + ScreenRect(card) + " camera=" + Camera.main.transform.position + " tour=" + Field<Unity.Cinemachine.CinemachineCamera>(intro, "tourCamera").transform.position);
        Check(!ScreenRect(highlight).Overlaps(ScreenRect(skip.GetComponent<RectTransform>())), "Skip stays clear of highlighted region");
        foreach (var label in GameObject.Find("Sets Diagram Region Labels").GetComponentsInChildren<TextMeshPro>())
        {
            var point = Camera.main.WorldToScreenPoint(label.transform.position);
            Check(point.z > 0 && point.y < Screen.height - Screen.safeArea.yMin && point.y > 0, "world label remains in tutorial framing: " + label.text);
            Check(!ScreenRect(card).Contains(point) && !ScreenRect(skip.GetComponent<RectTransform>()).Contains(point), "world label stays clear of tutorial controls");
        }
        Check(Field<GameObject>(SetssssChallengeOne.ActiveInstance, "aMinusBRegion").activeSelf == (challenge == 4), "difference regions preview only Challenge 4");
        yield return null;
    }
    static void CheckLabels(SetssssChallengeOne stage)
    {
        var root = GameObject.Find("Sets Diagram Region Labels");
        var labels = root.GetComponentsInChildren<TextMeshPro>();
        Check(labels.Select(l => l.text).OrderBy(s => s).SequenceEqual(new[] { "A", "A ∩ B", "B", "U — Universal Set" }.OrderBy(s => s)), "four unique world-space region names");
        foreach (var label in labels)
        {
            label.ForceMeshUpdate(); Check(!label.isTextOverflowing, "full world label fits: " + label.text);
            Check(Vector3.Dot(label.transform.forward, (label.transform.position - Camera.main.transform.position).normalized) > .999f, "label faces gameplay camera");
        }
        Check(labels.First(l => l.text == "A").transform.position.x < labels.First(l => l.text == "A ∩ B").transform.position.x && labels.First(l => l.text == "B").transform.position.x > labels.First(l => l.text == "A ∩ B").transform.position.x, "A, intersection, B ordered over actual meshes");
    }
    static bool Contains(Rect outside, Rect inside) => outside.Contains(inside.min + Vector2.one * .5f) && outside.Contains(inside.max - Vector2.one * .5f);
    static Rect ScreenRect(RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        var min = (Vector2)corners[0]; var max = (Vector2)corners[2];
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
    static void MoveToBook(SetssssChallengeOne stage, Player player)
    {
        var book = Field<Transform>(stage, "bookStatue");
        PrelimTeleportTransition.MovePlayerImmediately(player.gameObject, book.position + Vector3.back * 2f, Quaternion.identity);
    }
    static void Click(Button button) { Canvas.ForceUpdateCanvases(); CheckRaycast(button); ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler); }
    static void CheckRaycast(Button button)
    {
        Check(button != null && button.gameObject.activeInHierarchy && button.interactable, "button available");
        var point = ScreenRect(button.GetComponent<RectTransform>()).center;
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
        Check(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)), "button unobscured: " + button.name + "; hit " + (hits.Count == 0 ? "none" : hits[0].gameObject.name));
    }
    static IEnumerator Capture(string name)
    {
        Directory.CreateDirectory("Temp/SetsIntroductionsQA"); yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot("Temp/SetsIntroductionsQA/" + Screen.width + "x" + Screen.height + "_" + name + ".png"); yield return null;
    }
    static readonly Type ViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
    static int GetSize() => (int)ViewType.GetProperty("selectedSizeIndex", Private | BindingFlags.Public).GetValue(EditorWindow.GetWindow(ViewType));
    static void SetSize(int index)
    {
        ViewType.GetProperty("selectedSizeIndex", Private | BindingFlags.Public).SetValue(EditorWindow.GetWindow(ViewType), index);
        Crystal.SafeArea.Sim = index == 12 ? Crystal.SafeArea.SimDevice.Pixel3XL_LSR : index == 3 ? Crystal.SafeArea.SimDevice.iPhoneX : Crystal.SafeArea.SimDevice.None;
    }
    static T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name, Private).GetValue(obj);
    static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Private).SetValue(obj, value);
    static object Invoke(object obj, string name) => obj.GetType().GetMethod(name, Private).Invoke(obj, null);
    static void Check(bool okay, string message) { if (!okay) throw new InvalidOperationException(message); }
}
public sealed class SetsIntroductionCheckRunner : MonoBehaviour { }
#endif
