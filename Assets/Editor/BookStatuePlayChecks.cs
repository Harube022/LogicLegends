#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LogicLegends.Graph;
using LogicLegends.Induction;
using LogicLegends.Inference;
using LogicLegends.Trees;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Opt-in offline checks of saved book models, real activation, and terminal-state indicators.</summary>
public static class BookStatuePlayChecks
{
    const string Flag = "Books.RunQA", Report = "Library/BookStatuePlayChecks.txt";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly string[] Scenes = { "PRELIM", "Truth_Table", "RulesOfInference", "Setssss", "Functions and Relations",
        "Formal vs. Informal Proofs", "Direct&Indirect", "Induction", "Graph", "Trees" };
    static readonly string[] History = { "FunctionsRelations.LastVariant.", "LogicLegends.Induction.LastVariant." };

    [MenuItem("Logic Legends/Book Statues/Run Offline Play Checks")]
    public static void Begin()
    {
        SessionState.SetBool("Books.TruthOnly", false);
        SessionState.SetBool("Books.PropositionalOnly", false);
        BeginRun();
    }

    [MenuItem("Logic Legends/Book Statues/Run Truth Table Play Checks")]
    public static void BeginTruthTable()
    {
        SessionState.SetBool("Books.TruthOnly", true);
        SessionState.SetBool("Books.PropositionalOnly", false);
        BeginRun();
    }

    [MenuItem("Logic Legends/Book Statues/Run Propositional Replacement Checks")]
    public static void BeginPropositional()
    {
        SessionState.SetBool("Books.TruthOnly", false);
        SessionState.SetBool("Books.PropositionalOnly", true);
        BeginRun();
    }

    static void BeginRun()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        EditorSceneManager.SaveOpenScenes();
        SessionState.SetString("Books.OriginalScene", SceneManager.GetActiveScene().path);
        SessionState.SetBool(Flag, true);
        if (!SessionState.GetBool("Books.TruthOnly", false) && !SessionState.GetBool("Books.PropositionalOnly", false)) File.WriteAllText(Report, "Book statue Play Mode checks started.\n");
        EditorSceneManager.OpenScene("Assets/Scenes/PRELIM.unity");
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void Register() { EditorApplication.playModeStateChanged -= Restore; EditorApplication.playModeStateChanged += Restore; }
    static void Restore(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Flag, false)) return;
        SessionState.SetBool(Flag, false);
        if (SessionState.GetBool("Books.PrefsCaptured", false))
        {
            foreach (var prefix in History) for (int i = 0; i < 5; i++)
            {
                string key = prefix + i;
                if (SessionState.GetBool("Books.Exists." + key, false)) PlayerPrefs.SetInt(key, SessionState.GetInt("Books.Value." + key, -1));
                else PlayerPrefs.DeleteKey(key);
            }
            PlayerPrefs.Save(); SessionState.SetBool("Books.PrefsCaptured", false);
        }
        string path = SessionState.GetString("Books.OriginalScene", "Assets/Scenes/PRELIM.unity");
        EditorApplication.delayCall += () => EditorSceneManager.OpenScene(path);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        StageSelectionState.SelectForEditorSpawn(SessionState.GetBool("Books.TruthOnly", false) ? 2 : 1);
        SessionState.SetBool("Books.PrefsCaptured", true);
        foreach (var prefix in History) for (int i = 0; i < 5; i++)
        {
            string key = prefix + i;
            SessionState.SetBool("Books.Exists." + key, PlayerPrefs.HasKey(key));
            SessionState.SetInt("Books.Value." + key, PlayerPrefs.GetInt(key, -1));
        }
        SceneManager.sceneLoaded += Loaded;
    }

    static void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (!SessionState.GetBool(Flag, false)) return;
        if (Player.LocalInstance == null)
        {
            var avatar = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Multiplayer_Male"));
            avatar.GetComponent<MultiplayerAvatarSync>().enabled = false;
            avatar.GetComponent<CharacterController>().enabled = true;
        }
        if (UnityEngine.Object.FindFirstObjectByType<BookStatueCheckRunner>() == null)
        {
            var runner = new GameObject("Offline Book Checks").AddComponent<BookStatueCheckRunner>();
            UnityEngine.Object.DontDestroyOnLoad(runner.gameObject);
            runner.StartCoroutine(Guard(Run()));
        }
    }

    static IEnumerator Guard(IEnumerator run)
    {
        // Flatten nested enumerators so failures are recorded and Play Mode always exits.
        var stack = new Stack<IEnumerator>(); stack.Push(run);
        while (stack.Count > 0)
        {
            object next = null; bool moved = false;
            try { moved = stack.Peek().MoveNext(); if (moved) next = stack.Peek().Current; }
            catch (Exception error)
            {
                File.AppendAllText(Report, "FAIL " + error + "\n"); Debug.LogException(error);
                EditorApplication.isPlaying = false; yield break;
            }
            if (!moved) { stack.Pop(); continue; }
            if (next is IEnumerator child) stack.Push(child);
            else yield return next;
        }
        File.AppendAllText(Report, "ALL BOOK PLAY CHECKS PASSED\n");
        SceneManager.sceneLoaded -= Loaded;
        EditorApplication.isPlaying = false;
    }

    static IEnumerator Run()
    {
        foreach (string scene in SessionState.GetBool("Books.TruthOnly", false) ? new[] { "Truth_Table" } :
            SessionState.GetBool("Books.PropositionalOnly", false) ? new[] { "PRELIM" } : Scenes)
        {
            string sceneName = scene == "Truth_Table" ? "PRELIM" : scene;
            if (SceneManager.GetActiveScene().name != sceneName || scene == "Truth_Table" && !SessionState.GetBool("Books.TruthOnly", false))
            {
                Time.timeScale = 1;
                if (scene == "Truth_Table") StageSelectionState.Select(2);
                yield return SceneManager.LoadSceneAsync("Assets/Scenes/" + sceneName + ".unity");
            }
            yield return new WaitForSecondsRealtime(2.2f);
            var fr = Find<FunctionsRelationsStageManager>(); var induction = Find<InductionStageManager>();
            var proofs = Find<ProofsLibraryStageController>(); var graph = Find<GraphStageManager>(); var trees = Find<TreeStageManager>();
            float until = Time.realtimeSinceStartup + 30;
            while (Time.realtimeSinceStartup < until && (graph != null && !graph.Ready || induction != null && !induction.Ready || trees != null && !trees.Ready)) yield return null;
            if (fr != null) { fr.Help.CloseHelp(); fr.FinishOnboarding(); }
            if (proofs != null) { proofs.Presentation.Help.CloseHelp(); proofs.FinishOnboarding(); }
            if (induction != null) { induction.help.Close(); Call(induction.help, "CompleteOnboarding"); }
            if (graph != null) graph.SetLesson(false);
            var prelimHelp = Find<PropositionalLogicTutorial>(); if (prelimHelp != null) prelimHelp.CloseTutorial();
            if (PropositionalControlsOnboarding.Active != null)
            {
                var tutorial = PropositionalControlsOnboarding.Active;
                tutorial.SkipTutorial(); Call(tutorial, "ConfirmYes");
            }
            var truthHelp = Find<TruthTableTutorial>(); if (truthHelp != null) truthHelp.CloseTutorial();
            if (TruthTableControlsOnboarding.Active != null)
            {
                var tutorial = TruthTableControlsOnboarding.Active;
                tutorial.SkipTutorial(); Call(tutorial, "ConfirmYes");
            }
            var setsHelp = Find<SetssssHelpUI>(); if (setsHelp != null) setsHelp.CloseHelp();
            if (StageJourneyUI.Instance != null) StageJourneyUI.Instance.CloseInferenceTutorial();
            if (InferenceControlsOnboarding.Active != null)
            {
                var tutorial = InferenceControlsOnboarding.Active; tutorial.SkipTutorial(); tutorial.ConfirmYes();
            }
            Time.timeScale = 1;
            if (GameInput.Instance != null) GameInput.Instance.SetGameplayInputBlocked(false);
            Player.LocalInstance.ToggleControl(true);
            yield return null; yield return null;

            var visuals = UnityEngine.Object.FindObjectsByType<BookStatuePresentation>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(b => b.gameObject.scene.name == sceneName && b.gameObject.activeInHierarchy).ToArray();
            Check(visuals.Length > 0, scene + " has saved book presentation");
            yield return CheckAnimation(visuals, scene);
            if (scene == "PRELIM")
            {
                var coach = Find<PropositionalControlsOnboarding>();
                var quiz = Find<QuizManager>(); var timer = Find<LevelTimerManager>();
                float seconds = timer.RemainingTime;
                bool running = timer.IsTimerRunning;
                coach.Begin(Field<UnityEngine.UI.Button>(Find<PropositionalLogicTutorial>(), "helpButton"));
                Call(coach, "BeginBookDemo");
                Check(coach.IsDemonstratingBook, "replacement supports the existing book demonstration");
                yield return new WaitForSecondsRealtime(.8f);
                var interaction = Field<BookInteract>(coach, "roomBook");
                Call(interaction, "OnInteractClicked");
                Check(!coach.IsDemonstratingBook && coach.IsActive, "practice book interaction advances only the tutorial");
                Check(timer.IsTimerRunning == running && Mathf.Abs(timer.RemainingTime - seconds) < .01f &&
                    !Field<bool>(quiz, "currentChallengeStarted"), "book practice preserves real challenge and timer");
                coach.SkipTutorial(); Call(coach, "ConfirmYes");
                yield return null;
                File.AppendAllText(Report, "Propositional replacement: PASS original tutorial book camera and practice interaction; timer/progress unchanged\n");
            }
            // The Truth Table guide waits for PRELIM's arrival fade and FixedUpdates.
            // It can open during the animation sampling window; finish that real guide before testing interaction.
            if (scene == "Truth_Table")
            {
                Find<TruthTableTutorial>()?.CloseTutorial();
                if (TruthTableControlsOnboarding.Active != null)
                {
                    var tutorial = TruthTableControlsOnboarding.Active; tutorial.SkipTutorial(); Call(tutorial, "ConfirmYes");
                }
                Time.timeScale = 1; GameInput.Instance.SetGameplayInputBlocked(false); Player.LocalInstance.ToggleControl(true);
                yield return null;
            }
            yield return CheckStates(scene, fr, induction, proofs, graph, trees);
            File.AppendAllText(Report, scene + ": PASS animation, fixed statue transform, waiting/activation/next/terminal indicators\n");
        }
    }

    static IEnumerator CheckAnimation(BookStatuePresentation[] books, string scene)
    {
        var transforms = books.Select(b => b.transform).ToArray();
        var positions = transforms.Select(t => t.localPosition).ToArray();
        var rotations = transforms.Select(t => t.localRotation).ToArray();
        var scales = transforms.Select(t => t.localScale).ToArray();
        var bones = transforms.Select(t => t.Find("Page rig/Page_00")).ToArray();
        var first = bones.Select(t => t.localRotation).ToArray();
        var changed = new bool[books.Length];
        float until = Time.realtimeSinceStartup + 3.4f;
        while (Time.realtimeSinceStartup < until)
        {
            Time.timeScale = 1;
            for (int i = 0; i < books.Length; i++)
            {
                Check(Vector3.Distance(positions[i], transforms[i].localPosition) < .0001f &&
                    Quaternion.Angle(rotations[i], transforms[i].localRotation) < .01f &&
                    Vector3.Distance(scales[i], transforms[i].localScale) < .0001f, scene + " statue transform remains fixed");
                if (Quaternion.Angle(first[i], bones[i].localRotation) > 1f) changed[i] = true;
            }
            yield return null;
        }
        for (int i = 0; i < books.Length; i++) if (!changed[i])
        {
            var animator = books[i].GetComponent<Animator>();
            File.AppendAllText(Report, books[i].name + " did not flip; initialized=" + animator.isInitialized +
                " enabled=" + animator.enabled + " speed=" + animator.speed + " state=" +
                animator.GetCurrentAnimatorStateInfo(0).normalizedTime + " timescale=" + Time.timeScale + "\n");
        }
        Check(changed.All(x => x), scene + " every active book page flips");
    }

    static IEnumerator CheckStates(string scene, FunctionsRelationsStageManager fr, InductionStageManager induction,
        ProofsLibraryStageController proofs, GraphStageManager graph, TreeStageManager trees)
    {
        Transform book = null; Action activate = null, waiting = null, terminal = null, completed = null;
        if (fr != null)
        {
            book = fr.BookTarget; activate = fr.ActivateChallenge;
            waiting = () => Set(fr, "state", FunctionsRelationsState.WAITING_FOR_ACTIVATION);
            terminal = () => Set(fr, "state", FunctionsRelationsState.GAME_OVER);
            completed = () => Set(fr, "state", FunctionsRelationsState.STAGE_COMPLETE);
        }
        else if (induction != null)
        {
            book = induction.book; activate = induction.InteractBook;
            waiting = () => Set(induction, "State", InductionState.WaitingForBook);
            terminal = () => Set(induction, "State", InductionState.GameOver);
            completed = () => Set(induction, "State", InductionState.StageComplete);
        }
        else if (proofs != null)
        {
            book = proofs.BookTarget; activate = proofs.Activate;
            waiting = () => Set(proofs, "started", false);
            terminal = () => Set(proofs, "finished", true);
            completed = terminal;
        }
        else if (graph != null)
        {
            book = graph.bookStatue; activate = graph.InteractBook;
            waiting = () => Set(graph, "State", GraphStageState.WAITING_FOR_ACTIVATION);
            terminal = () => Set(graph, "State", GraphStageState.GAME_OVER);
            completed = () => Set(graph, "State", GraphStageState.STAGE_COMPLETE);
        }
        else if (trees != null)
        {
            book = trees.bookStatue; activate = trees.InteractBook;
            waiting = () => { trees.ExitBoard(); Set(trees, "State", TreeStageState.WAITING_FOR_ACTIVATION); };
            terminal = () => Set(trees, "State", TreeStageState.GAME_OVER);
            completed = () => Set(trees, "State", TreeStageState.STAGE_COMPLETE);
        }
        else if (scene == "Direct&Indirect")
        {
            var stage = Find<DirectIndirectProofStageController>(); book = Field<Transform>(stage, "bookStatue"); activate = stage.ActivateChallenge;
            waiting = () => Set(stage, "state", DirectIndirectProofStageController.TrialState.WaitingForActivation);
            terminal = () => Set(stage, "state", DirectIndirectProofStageController.TrialState.GameOver);
            completed = () => Set(stage, "state", DirectIndirectProofStageController.TrialState.StageComplete);
        }
        else if (scene == "Setssss")
        {
            var stage = Find<SetssssChallengeOne>(); book = Field<Transform>(stage, "bookStatue");
            activate = () => Call(stage, "StartGameplayChallenge");
            waiting = () => Set(stage, "started", false);
            terminal = () => Set(stage, "gameOverTriggered", true);
            completed = () => { Set(stage, "gameOverTriggered", false); Set(stage, "complete", true); };
        }
        else if (scene == "RulesOfInference")
        {
            var stage = Find<InferenceChallenge>(); book = Find<InferenceBookStart>().transform;
            activate = () => Check(stage.StartFromBook(), "inference book activation accepted");
            waiting = () => Set(stage, "HasStarted", false);
            terminal = () => Set(stage, "PuzzleCompleted", true);
            completed = terminal;
        }
        else if (scene == "Truth_Table")
        {
            var doors = Find<TruthTableDoorInteraction>(); var clock = Find<TruthTableStageClock>();
            book = doors.TutorialBook; activate = doors.ActivateFocusedInteraction;
            waiting = () => SetEnum(clock, "state", "WaitingForBook");
            terminal = () => SetEnum(clock, "state", "GameOver");
            completed = () => SetEnum(clock, "state", "Completed");
        }
        else
        {
            var quiz = Find<QuizManager>();
            var challenge = (TopicChallenge)Call(quiz, "GetCurrentChallenge");
            var interaction = challenge.roomChoiceCanvas.transform.parent.GetComponentInChildren<BookInteract>(true);
            book = interaction.transform; activate = () => Call(interaction, "OnInteractClicked");
            waiting = () => { Field<GameObject>(quiz, "quizPanel").SetActive(false); interaction.ResetInteraction(); };
            terminal = () => Set(Field<LevelTimerManager>(interaction, "timerManager"), "currentTimer", 0f);
            completed = terminal;
        }
        Check(book != null, scene + " book reference");
        var visual = BookStatuePresentation.For(book);
        yield return null;
        Check(visual.InteractionRequired && visual.Indicator != null && visual.Indicator.gameObject.activeSelf, scene + " waiting indicator");
        float glyphY = visual.Indicator.transform.position.y;
        yield return new WaitForSecondsRealtime(.25f);
        Check(Mathf.Abs(visual.Indicator.transform.position.y - glyphY) > .001f, scene + " indicator bobs");
        Check(visual.Indicator.text == "!", scene + " exclamation text");
        CaptureBook(visual, scene);
        // Move using the existing teleport helper to exercise the real book-start method.
        Vector3 offset = new Vector3(0, 0, -2);
        PrelimTeleportTransition.MovePlayerImmediately(Player.LocalInstance.gameObject, book.position + offset,
            Quaternion.LookRotation(-offset));
        activate();
        yield return new WaitForSecondsRealtime(2f);
        Check(!visual.InteractionRequired, scene + " activation hides indicator");
        waiting(); yield return null; yield return null;
        Check(visual.InteractionRequired, scene + " next interaction shows indicator");
        terminal(); yield return null; yield return null;
        Check(!visual.InteractionRequired && !visual.Indicator.gameObject.activeSelf, scene + " game over hides indicator");
        completed(); yield return null; yield return null;
        Check(!visual.InteractionRequired, scene + " stage completion hides indicator");
    }

    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    static void SetEnum(object target, string name, string value) => Set(target, name, Enum.Parse(target.GetType().GetField(name, Flags).FieldType, value));
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Flags).GetValue(target);
    static void Set(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, Flags) ?? target.GetType().GetField("<" + name + ">k__BackingField", Flags);
        if (field == null) throw new MissingFieldException(target.GetType().Name, name);
        field.SetValue(target, value);
    }
    static object Call(object target, string name) => target.GetType().GetMethod(name, Flags)?.Invoke(target, null) ?? null;
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    static void CaptureBook(BookStatuePresentation book, string scene)
    {
        Directory.CreateDirectory("Temp/BookStatueQA");
        Bounds bounds = BookStatueProjectSetup.GeometryBounds(book.transform);
        bounds.Encapsulate(book.Indicator.GetComponent<Renderer>().bounds);
        var go = new GameObject("Book QA Camera"); var camera = go.AddComponent<Camera>();
        camera.fieldOfView = 38; camera.nearClipPlane = .05f; camera.farClipPlane = 500;
        var direction = new Vector3(0, .16f, -1).normalized;
        camera.transform.position = bounds.center + direction * bounds.size.y * 1.8f;
        camera.transform.LookAt(bounds.center);
        var target = new RenderTexture(1000, 750, 24); camera.targetTexture = target;
        camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = target;
        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
        File.WriteAllBytes("Temp/BookStatueQA/" + scene + ".png", pixels.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; target.Release();
        UnityEngine.Object.Destroy(go); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(pixels);
    }
}
public sealed class BookStatueCheckRunner : MonoBehaviour { }
#endif
