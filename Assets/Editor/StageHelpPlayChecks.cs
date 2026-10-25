#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Opt-in offline checks of the saved-scene Help screens using real Input System touches.</summary>
public static class StageHelpPlayChecks
{
    const string Flag = "StageHelp.PlayQA", Report = "Library/StageHelpPlayChecks.txt";
    const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly string[] Scenes = { "PRELIM", "TruthTable", "RulesOfInference", "Formal vs. Informal Proofs", "Direct&Indirect", "Induction", "Setssss", "Functions and Relations", "Graph", "Trees" };
    static readonly string[] Histories = { "FunctionsRelations.LastVariant.", "LogicLegends.Induction.LastVariant.", "LogicLegends.ProofsLibrary.LastVariant.", "LogicLegends.Graph.LastVariant.", "LogicLegends.Trees.LastVariant." };
    static Touchscreen screen;
    static int finger;

    [MenuItem("Logic Legends/Mobile/Check Shared Help and Gameplay Videos")]
    public static void Begin() => Begin("PRELIM");
    public static void Begin(string fromScene)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Exit Play Mode and save the current scene before running Help checks.");
        SessionState.SetString(Flag + ".Scene", SceneManager.GetActiveScene().path);
        SessionState.SetInt(Flag + ".Size", BookMapMobileChecks.GetSize());
        SessionState.SetInt(Flag + ".SafeArea", (int)Crystal.SafeArea.Sim);
        foreach (string prefix in Histories) for (int i = 0; i < 5; i++)
        {
            string key = prefix + i;
            SessionState.SetBool(Flag + key, PlayerPrefs.HasKey(key));
            SessionState.SetInt(Flag + key + ".Value", PlayerPrefs.GetInt(key));
        }
        SessionState.SetBool(Flag, true);
        if (fromScene == "PRELIM" || !File.Exists(Report)) File.WriteAllText(Report, "Shared Help saved-scene touch and gameplay-video checks\n");
        else File.WriteAllLines(Report, File.ReadAllLines(Report).TakeWhile(line => !line.StartsWith("FAIL")));
        SessionState.SetString(Flag + ".From", fromScene);
        EditorSceneManager.OpenScene("Assets/Scenes/" + fromScene + ".unity");
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void Register()
    {
        EditorApplication.playModeStateChanged -= Restore;
        EditorApplication.playModeStateChanged += Restore;
    }

    static void Restore(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Flag, false)) return;
        SessionState.SetBool(Flag, false); SceneManager.sceneLoaded -= Loaded;
        foreach (string field in new[] { "ValidationTransport", "ValidationUid", "ValidationEmail" })
            typeof(TrialBackendClient).GetField(field, BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
        BookMapMobileChecks.SetSize(SessionState.GetInt(Flag + ".Size", 6));
        Crystal.SafeArea.Sim = (Crystal.SafeArea.SimDevice)SessionState.GetInt(Flag + ".SafeArea", 0);
        foreach (string prefix in Histories) for (int i = 0; i < 5; i++)
        {
            string key = prefix + i;
            if (SessionState.GetBool(Flag + key, false)) PlayerPrefs.SetInt(key, SessionState.GetInt(Flag + key + ".Value", 0));
            else PlayerPrefs.DeleteKey(key);
        }
        PlayerPrefs.Save();
        string path = SessionState.GetString(Flag + ".Scene", "Assets/Scenes/PRELIM.unity");
        EditorApplication.delayCall += () => EditorSceneManager.OpenScene(path);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        // No authenticated UID and an inert transport prevent trial-wallet writes or Admin bypasses.
        typeof(TrialBackendClient).GetField("ValidationTransport", flags)?.SetValue(null,
            (Func<string, string, System.Threading.Tasks.Task<string>>)((route, body) => System.Threading.Tasks.Task.FromResult("{}")));
        typeof(TrialBackendClient).GetField("ValidationUid", flags)?.SetValue(null, null);
        typeof(TrialBackendClient).GetField("ValidationEmail", flags)?.SetValue(null, null);
        StageSelectionState.SelectForEditorSpawn(1);
        SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded;
    }

    static void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (!SessionState.GetBool(Flag, false)) return;
        foreach (var component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if ((component.GetType().Namespace ?? "").StartsWith("Photon.Pun.Demo.Cockpit")) component.enabled = false;
        if (Player.LocalInstance == null)
        {
            var avatar = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Multiplayer_Male"));
            avatar.GetComponent<MultiplayerAvatarSync>().enabled = false;
            avatar.GetComponent<PlayerEquipmentLoader>().enabled = false;
            foreach (var skin in avatar.GetComponentsInChildren<PlayerAnimator>(true)) skin.gameObject.SetActive(skin.name == "UNIFORM_MALE");
            avatar.GetComponent<CharacterController>().enabled = true;
            var spawner = Find<LevelSpawner>();
            Transform spawn = spawner != null ? Member<Transform>(spawner, "spawnPoint") : null;
            if (spawn == null) spawn = GameObject.Find("Proofs Player Spawn")?.transform ?? GameObject.Find("Trial Player Spawn")?.transform;
            if (spawn != null) avatar.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
        }
        if (Find<StageHelpQaRunner>() == null)
        {
            var runner = new GameObject("Stage Help Offline QA").AddComponent<StageHelpQaRunner>();
            UnityEngine.Object.DontDestroyOnLoad(runner); runner.StartCoroutine(Guard(Run()));
        }
    }

    static IEnumerator Guard(IEnumerator work)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(work);
        while (stack.Count > 0)
        {
            object next;
            try
            {
                var current = stack.Peek();
                if (!current.MoveNext()) { stack.Pop(); continue; }
                next = current.Current;
            }
            catch (Exception error)
            {
                File.AppendAllText(Report, "FAIL " + error + "\n"); Debug.LogException(error);
                if (screen != null && screen.added) InputSystem.RemoveDevice(screen);
                EditorApplication.isPlaying = false; yield break;
            }
            if (next is IEnumerator child) stack.Push(child); else yield return next;
        }
        Log("ALL SHARED HELP CHECKS PASSED"); SceneManager.sceneLoaded -= Loaded;
        if (screen != null && screen.added) InputSystem.RemoveDevice(screen);
        EditorApplication.isPlaying = false;
    }

    static IEnumerator Run()
    {
        Application.runInBackground = true; screen = InputSystem.AddDevice<Touchscreen>(); finger = 100;
        foreach (string scene in Scenes.SkipWhile(scene => scene != SessionState.GetString(Flag + ".From", "PRELIM")))
        {
            Time.timeScale = 1; GameInput.Instance?.SetGameplayInputBlocked(false);
            StageSelectionState.SelectForEditorSpawn(TrialCatalog.ForScene(scene).Number);
            BookMapMobileChecks.SetSize(6);
            if (SceneManager.GetActiveScene().name != scene) yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/" + scene + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Until(() => Player.LocalInstance != null && GameInput.Instance != null && StageReady(scene) &&
                (TeleportManager.Instance == null || !TeleportManager.Instance.IsTransitioning), 30, "stage spawn and fade: " + scene);
            // Most trials open Help at spawn. Trees exposes its field guide on demand.
            if (scene == "Trees") Find<LogicLegends.Trees.TreeStageManager>().SetLesson(true);
            yield return Until(() => ActivePage() != null, 18, "first Help opens: " + scene);
            yield return CompleteInitialGuide(scene);
            Check(Time.timeScale > 0 && Player.LocalInstance.enabled && !GameInput.Instance.GameplayInputBlocked,
                "first tutorial exit restores controls and book eligibility: " + scene);

            foreach (int size in new[] { 6, 3 })
            {
                BookMapMobileChecks.SetSize(size); yield return new WaitForSecondsRealtime(.35f);
                var before = Snapshot.Capture(scene);
                yield return Tap(HelpButton(scene));
                yield return Until(() => ActivePage() != null, 4, "Help reopens: " + scene);
                var page = ActivePage(); yield return null; yield return null;
                Check(UnityEngine.Object.FindObjectsByType<StageHelpPage>(FindObjectsSortMode.None).Count(p => p.isActiveAndEnabled) == 1,
                    "one shared Help page: " + scene);
                Check(UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None).Count(r => r.name == "Shared Scrollable Help") == 1,
                    "one scrollable presentation: " + scene);
                Check(!page.transform.GetComponentsInChildren<Button>().Any(b =>
                    b != page.BeginButton && b.GetComponentInChildren<TMP_Text>() != null &&
                    new[] { "PREVIOUS", "NEXT" }.Contains(b.GetComponentInChildren<TMP_Text>().text.Trim().ToUpperInvariant())),
                    "no unnecessary Previous/Next navigation: " + scene);
                Check(GameInput.Instance.GameplayInputBlocked && !Player.LocalInstance.enabled, "Help blocks gameplay: " + scene);
                Check(page.CloseButton == null && !page.GetComponentsInChildren<Button>().Any(b => b.name == "Close Help"), "no Close/X exit: " + scene);
                Check(page.BeginButton.gameObject.activeInHierarchy && page.BeginButton.IsInteractable(), "Let’s Begin is the sole exit: " + scene);
                MobileInstructionAssetChecks.VerifyVisibleCopy(scene);
                yield return CheckLayout(page, scene);
                yield return DragReading(page.ReadingScroll);
                yield return CheckVideo(page, scene);
                float paused = Clock(scene); yield return new WaitForSecondsRealtime(.35f);
                Check(Mathf.Abs(Clock(scene) - paused) < .02f, "Help does not consume countdown time: " + scene);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot("Library/SharedHelp_" + TrialCatalog.ForScene(scene).Number + "_" + Screen.width + "x" + Screen.height + ".png");
                yield return Tap(page.BeginButton); yield return null;
                Check(!page.gameObject.activeInHierarchy, "Close hides the shared page: " + scene);
                Check(page.GameplayVideo.targetTexture == null, "Close releases video render target: " + scene);
                before.CheckRestored(scene);
                Log(scene + " " + Screen.width + "x" + Screen.height + ": touch scroll, layout, video, Pause/Replay, Let’s Begin, timer/input restore");
            }

            Check(Coach(scene) == null || !CoachActive(Coach(scene)), "reopening Help never restarts practice: " + scene);
            // Trees enters its existing landscape board interface on activation.
            // The guide itself was checked at both sizes above.
            if (scene == "Trees") { BookMapMobileChecks.SetSize(6); yield return new WaitForSecondsRealtime(.35f); }
            yield return VerifyBookHand(scene);
            yield return Until(()=>HelpButton(scene)!=null&&HelpButton(scene).gameObject.activeInHierarchy&&HelpButton(scene).IsInteractable()&&Time.timeScale>0,12,"active gameplay Help is available: "+scene);
            var activeSnapshot=Snapshot.Capture(scene);
            yield return Tap(HelpButton(scene));yield return Until(()=>ActivePage()!=null,3,"active Help opens: "+scene);
            float heldTime=Clock(scene);yield return new WaitForSecondsRealtime(.4f);
            Check(Math.Abs(Clock(scene)-heldTime)<.02f,"active countdown pauses inside Help: "+scene);
            yield return Tap(ActivePage().BeginButton);yield return null;
            activeSnapshot.CheckRestored(scene);
            Check(Coach(scene)==null||!CoachActive(Coach(scene)),"active Help never restarts onboarding: "+scene);
            Log(scene+": reopened active Help preserves proof/progress and restores countdown/input");

        }
    }

    // Shared by playlist checks: follow the same first-time exit instead of bypassing onboarding.
    public static IEnumerator CompleteInitialGuide(string scene)
    {
        bool ownTouch = screen == null || !screen.added;
        if (ownTouch) { screen = InputSystem.AddDevice<Touchscreen>(); finger = 700; }
        var page = ActivePage(); Check(page != null && page.CloseButton == null, "guide exposes only Let's Begin");
        float remaining = Clock(scene);
        yield return Tap(page.BeginButton); yield return null; yield return null;
        Check(ActivePage() == null, "Let's Begin releases Help");
        if (scene != "Trees")
        {
            var coach = Coach(scene);
            Check(coach != null && CoachActive(coach) && Step(coach) == "Movement", "first exit begins movement practice: " + scene);
            var skip = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.gameObject.activeInHierarchy &&
                b.GetComponentInChildren<TMP_Text>() != null && b.GetComponentInChildren<TMP_Text>().text.IndexOf("Skip Tutorial", StringComparison.OrdinalIgnoreCase) >= 0);
            yield return Tap(skip); yield return new WaitForSecondsRealtime(1.1f);
            var yes = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b => b.gameObject.activeInHierarchy &&
                b.GetComponentInChildren<TMP_Text>() != null && b.GetComponentInChildren<TMP_Text>().text.Trim().Equals("Yes", StringComparison.OrdinalIgnoreCase));
            yield return Tap(yes); yield return null; yield return null;
            // The next challenge's independent introduction must also finish through its real Skip action.
            var formal = Find<ProofsChallengeIntroductions>();
            var sets = Find<SetssssChallengeIntroductions>();
            var direct = Find<DirectProofsGuide>();
            if (formal != null && formal.IsOpen || sets != null && sets.IsOpen || direct != null && direct.IsIntroduction)
            {
                var introSkip = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.gameObject.activeInHierarchy &&
                    b.GetComponentInChildren<TMP_Text>() != null && b.GetComponentInChildren<TMP_Text>().text.IndexOf("Skip Tutorial", StringComparison.OrdinalIgnoreCase) >= 0);
                yield return Tap(introSkip); yield return null;
            }
            yield return Until(() => !CoachActive(coach) && !GameInput.Instance.GameplayInputBlocked && Player.LocalInstance.enabled, 5, "tutorial and camera cleanup: " + scene);
        }
        Check(Math.Abs(Clock(scene)-remaining)<.1f, "Help/practice never starts real countdown: " + scene);
        Log(scene + ": sole Begin exit, first movement tutorial, real Skip/Yes, input and countdown cleanup");
        if (ownTouch) { InputSystem.RemoveDevice(screen); screen = null; }
    }
    static IEnumerator VerifyBookHand(string scene)
    {
        var book = UnityEngine.Object.FindObjectsByType<BookStatuePresentation>(FindObjectsSortMode.None).Where(b=>b.InteractionRequired).OrderBy(b=>
            Vector3.Distance(b.transform.position,Player.LocalInstance.transform.position)).FirstOrDefault();
        Check(book != null, "waiting book remains eligible after Help: " + scene);
        Vector3 at=book.transform.position;
        for(int direction=0; direction<8 && !book.PlayerInRange; direction++)
        {
            float angle=direction*Mathf.PI/4; Vector3 point=at+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*2;
            var hits=Physics.RaycastAll(point+Vector3.up*8,Vector3.down,25,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance);
            foreach(var hit in hits)
                if(hit.normal.y>.6f && hit.collider.GetComponentInParent<Player>()==null && !hit.collider.transform.IsChildOf(book.transform))
                {point.y=hit.point.y+.1f;break;}
            PrelimTeleportTransition.MovePlayerImmediately(Player.LocalInstance.gameObject,point,Quaternion.LookRotation(new Vector3(at.x-point.x,0,at.z-point.z)));
            yield return new WaitForSecondsRealtime(.1f);
        }
        Check(book.PlayerInRange,"book proximity is restored after Help: "+scene);
        var hand=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name=="InteractButton");
        yield return Tap(hand); yield return new WaitForSecondsRealtime(.3f);
        Check(!book.InteractionRequired,"actual hand activates book after Help: "+scene);
        Log(scene+": Book Statue remains accessible through actual hand touch after reopened Help");
    }

    static IEnumerator CheckLayout(StageHelpPage page, string scene)
    {
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        var bounds = new Rect(0, 0, Screen.width, Screen.height);
        foreach (var rect in new[] { (RectTransform)page.BeginButton.transform, page.ReadingScroll.viewport, page.transform.Find("Shared Scrollable Help/Gameplay Demonstration") as RectTransform })
            Check(rect != null && Contains(bounds, ScreenRect(rect)), "Help element fits screen: " + scene + " " + rect?.name);
        var reading = ScreenRect(page.ReadingScroll.viewport);
        var media = ScreenRect(page.transform.Find("Shared Scrollable Help/Gameplay Demonstration") as RectTransform);
        Check(!reading.Overlaps(media), "reading and gameplay video do not overlap: " + scene);
        if (Screen.width < Screen.height) Check(reading.yMin >= media.yMax - 1, "narrow Help stacks text above video: " + scene);
        else Check(reading.xMax <= media.xMin + 1, "wide Help keeps reading/video side by side: " + scene);
        var body = page.ReadingScroll.content.GetComponentInChildren<TMP_Text>();
        Check(body != null && body.fontSize >= 30 && body.richText && !body.enableAutoSizing, "mobile reading text stays large: " + scene);
        Check(body.text.Contains("<b>") && body.text.Contains("<u>"), "selective emphasis remains available: " + scene);
        Check(page.ReadingScroll.content.rect.height > page.ReadingScroll.viewport.rect.height + 10, "guide has scrollable content: " + scene);
        foreach (var text in page.GetComponentsInChildren<TMP_Text>())
        {
            if (text == body || string.IsNullOrWhiteSpace(text.text)) continue;
            text.ForceMeshUpdate();
            Check(!text.isTextOverflowing, "Help title/caption/control text does not clip: " + scene + " " + text.name);
        }
        yield return null;
    }

    static IEnumerator DragReading(ScrollRect scroll)
    {
        scroll.StopMovement(); scroll.verticalNormalizedPosition = 1; Canvas.ForceUpdateCanvases(); yield return null;
        var rect = ScreenRect(scroll.viewport); Vector2 from = new Vector2(rect.center.x, rect.yMin + rect.height * .25f);
        Vector2 to = new Vector2(from.x, rect.yMin + rect.height * .85f);
        var hits = Raycast(from); Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<ScrollRect>() == scroll, "touch reaches reading ScrollRect");
        int id = ++finger;
        Touch(id, UnityEngine.InputSystem.TouchPhase.Began, from); yield return null; yield return null;
        for (int i = 1; i <= 12; i++) { Touch(id, UnityEngine.InputSystem.TouchPhase.Moved, Vector2.Lerp(from, to, i / 12f)); yield return new WaitForSecondsRealtime(.025f); }
        Touch(id, UnityEngine.InputSystem.TouchPhase.Ended, to); yield return null; yield return null;
        Check(scroll.verticalNormalizedPosition < .985f, "actual touch drag scrolls the reading page");
    }

    static IEnumerator CheckVideo(StageHelpPage page, string scene)
    {
        var video = page.GameplayVideo;
        Check(video != null && video.clip != null, "actual stage gameplay recording is assigned: " + scene);
        yield return Until(() => video.isPrepared && video.frame > 0, 25, "stage video prepares and displays frames: " + scene);
        Check(video.timeUpdateMode == UnityEngine.Video.VideoTimeUpdateMode.UnscaledGameTime && video.targetTexture != null && video.targetTexture.IsCreated(), "video uses unscaled playback and valid texture: " + scene);
        long frame = video.frame; yield return Until(() => video.frame != frame, 4, "video advances while gameplay is paused: " + scene);
        var pause = page.GetComponentsInChildren<Button>().First(b => b.name == "Pause Gameplay Video");
        yield return Tap(pause); Check(!video.isPlaying, "touch pauses gameplay video: " + scene);
        double time = video.time; yield return new WaitForSecondsRealtime(.25f);
        Check(Math.Abs(video.time - time) < .15, "paused video stays still: " + scene);
        var replay = page.GetComponentsInChildren<Button>().First(b => b.name == "Replay Gameplay Video");
        yield return Tap(replay); yield return new WaitForSecondsRealtime(.2f);
        Check(video.isPlaying && video.time < 1.5, "touch replays stage video: " + scene);
        var caption = page.GetComponentsInChildren<TMP_Text>().First(t => t.name == "Action Caption");
        Check(!string.IsNullOrWhiteSpace(caption.text) && caption.text != "Gameplay demonstration", "recorded action caption is available: " + scene);
    }

    struct Snapshot
    {
        bool blocked, enabled; float scale, remaining, scaledAt; string progress;
        public static Snapshot Capture(string scene) => new Snapshot { blocked = GameInput.Instance.GameplayInputBlocked, enabled = Player.LocalInstance.enabled, scale = Time.timeScale, remaining = Clock(scene), scaledAt = Time.time, progress = Progress(scene) };
        public void CheckRestored(string scene)
        {
            Check(GameInput.Instance.GameplayInputBlocked == blocked && Player.LocalInstance.enabled == enabled && Mathf.Approximately(Time.timeScale, scale), "Help restores prior input/player/time state: " + scene);
            // Real touch injection takes rendered frames before opening and after closing.
            // Charge only that resumed gameplay time, never the paused Help duration.
            float used=remaining-Clock(scene), resumed=Time.time-scaledAt;
            Check(used>=-.02f&&used<=Mathf.Max(.2f,resumed+.05f), "Help preserves remaining timer: " + scene+" used="+used+" resumed="+resumed);
            Check(Progress(scene) == progress, "Help does not change question or challenge progress: " + scene);
        }
    }
    static object Stage(string scene)
    {
        switch (scene)
        {
            case "PRELIM": return Find<LevelTimerManager>();
            case "TruthTable": return Find<TruthTableStageClock>();
            case "RulesOfInference": return StageJourneyUI.Instance;
            case "Formal vs. Informal Proofs": return Find<ProofsLibraryStageController>();
            case "Direct&Indirect": return Find<DirectIndirectProofStageController>();
            case "Induction": return Find<LogicLegends.Induction.InductionStageManager>();
            case "Setssss": return Find<SetssssChallengeOne>();
            case "Functions and Relations": return Find<FunctionsRelationsStageManager>();
            case "Graph": return Find<LogicLegends.Graph.GraphStageManager>().timer;
            case "Trees": return Find<LogicLegends.Trees.TreeStageManager>().timer;
            default: return null;
        }
    }
    static float Clock(string scene)
    {
        object stage = Stage(scene);
        foreach (string key in new[] { "RemainingTime", "RemainingSeconds", "TimeRemaining", "SecondsLeft", "Remaining", "inferenceRemaining", "remaining" })
        {
            object value = Read(stage, key); if (value is float) return (float)value;
        }
        var nested = Read(stage, "timer");
        if (Read(nested, "Remaining") is float remaining) return remaining;
        throw new InvalidOperationException("Cannot read actual countdown for " + scene);
    }
    static string Progress(string scene)
    {
        object stage = scene == "Graph" ? (object)Find<LogicLegends.Graph.GraphStageManager>() : scene == "Trees" ? Find<LogicLegends.Trees.TreeStageManager>() : Stage(scene);
        return string.Join("/", new[] { "ChallengeIndex", "ChallengeNumber", "CurrentChallengeNumber", "QuestionIndex", "QuestionNumber", "CorrectCount", "Correct" }.Select(key => key + "=" + Read(stage, key)));
    }
    static Button HelpButton(string scene)
    {
        switch (scene)
        {
            case "PRELIM": return Member<Button>(Find<PropositionalLogicTutorial>(), "helpButton");
            case "TruthTable": return Member<Button>(Find<TruthTableTutorial>(), "helpButton");
            case "RulesOfInference": return Member<Button>(StageJourneyUI.Instance, "inferenceHelp");
            case "Formal vs. Informal Proofs": return Find<ProofsHelpUI>().HelpButton;
            case "Direct&Indirect": return Find<DirectProofsGuide>().HelpButton;
            case "Induction": return Find<LogicLegends.Induction.InductionStageManager>().help.HelpButton;
            case "Setssss": return Member<Button>(Find<SetssssHelpUI>(), "helpButton");
            case "Functions and Relations": return Find<FunctionsRelationsHelpUI>().HelpButton;
            case "Graph": return Member<Button>(Find<LogicLegends.Graph.GraphUIController>(), "lessonButton");
            case "Trees": return Member<Button>(Find<LogicLegends.Trees.TreeUIController>(),
                Find<LogicLegends.Trees.TreeStageManager>().BoardOpen ? "focusGuide" : "guide");
            default: throw new InvalidOperationException(scene);
        }
    }
    static MonoBehaviour Coach(string scene)
    {
        switch (scene)
        {
            case "PRELIM": return Find<PropositionalControlsOnboarding>();
            case "TruthTable": return Find<TruthTableControlsOnboarding>();
            case "RulesOfInference": return Find<InferenceControlsOnboarding>();
            case "Formal vs. Informal Proofs": return Find<ProofsControlsOnboarding>();
            case "Direct&Indirect": return Find<DirectProofsGuide>();
            case "Induction": return Find<LogicLegends.Induction.InductionControlsOnboarding>();
            case "Setssss": return Find<SetssssControlsOnboarding>();
            case "Functions and Relations": return Find<FunctionsRelationsControlsOnboarding>();
            case "Graph": return Find<LogicLegends.Graph.GraphControlsOnboarding>();
            default: return null;
        }
    }
    static bool StageReady(string scene)
    {
        if (scene == "Graph") return Find<LogicLegends.Graph.GraphStageManager>()?.Ready == true;
        if (scene == "Trees") return Find<LogicLegends.Trees.TreeStageManager>()?.Ready == true;
        if (scene == "Induction") return Find<LogicLegends.Induction.InductionStageManager>()?.Ready == true;
        return true;
    }
    static string Step(object coach) => (Read(coach, "CurrentStep") ?? Read(coach, "Step") ?? Read(coach, "step"))?.ToString() ?? "";
    static bool CoachActive(object coach) => (Read(coach, "IsActive") ?? Read(coach, "IsPractice")) is bool active && active;
    static StageHelpPage ActivePage() => UnityEngine.Object.FindObjectsByType<StageHelpPage>(FindObjectsSortMode.None).FirstOrDefault(p => p.isActiveAndEnabled);
    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    static object Read(object target, string key) => target?.GetType().GetProperty(key, Members)?.GetValue(target) ?? target?.GetType().GetField(key, Members)?.GetValue(target);
    static T Member<T>(object target, string key) => (T)Read(target, key);
    static void Invoke(object target, string method)
    {
        var call = target?.GetType().GetMethod(method, Members);
        if (call == null) throw new InvalidOperationException("Missing cleanup hook " + target?.GetType().Name + "." + method);
        call.Invoke(target, null);
    }
    static IEnumerator Until(Func<bool> condition, float seconds, string description)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        Check(condition(), description);
    }
    static IEnumerator Tap(Button button)
    {
        Check(button != null && button.gameObject.activeInHierarchy && button.IsInteractable(), "touch button is available: " + button?.name);
        yield return null; Canvas.ForceUpdateCanvases(); yield return new WaitForEndOfFrame(); Vector2 point = ScreenRect((RectTransform)button.transform).center;
        var hits = Raycast(point);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button,
            "touch reaches " + button.name + "; hit " + string.Join(",", hits.Take(4).Select(h => h.gameObject.name)));
        int id = ++finger; Touch(id, UnityEngine.InputSystem.TouchPhase.Began, point); yield return null; yield return null;
        Touch(id, UnityEngine.InputSystem.TouchPhase.Ended, point); yield return null; yield return null;
    }
    static void Touch(int id, UnityEngine.InputSystem.TouchPhase phase, Vector2 position) => InputSystem.QueueStateEvent(screen, new TouchState { touchId = id, phase = phase, position = position });
    static List<RaycastResult> Raycast(Vector2 position)
    {
        Check(EventSystem.current != null, "EventSystem is present");
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits); return hits;
    }
    static Rect ScreenRect(RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        var points = corners.Select(c => RectTransformUtility.WorldToScreenPoint(null, c)).ToArray();
        return Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
    }
    static bool Contains(Rect outer, Rect inner) => inner.xMin >= outer.xMin - 1 && inner.yMin >= outer.yMin - 1 && inner.xMax <= outer.xMax + 1 && inner.yMax <= outer.yMax + 1;
    static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
    static void Log(string message) => File.AppendAllText(Report, "PASS " + message + "\n");
}
public sealed class StageHelpQaRunner : MonoBehaviour { }
#endif
