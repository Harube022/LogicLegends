#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Opt-in offline Play Mode checks. Uses the existing no-account QA session.</summary>
public static class TrialContextHintChecks
{
    const string Report = "Library/TrialContextHintChecks.txt";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Flags).GetValue(owner);
    static void Call(object owner, string name) => owner.GetType().GetMethod(name, Flags).Invoke(owner, null);
    static void Check(bool pass, string message)
    { if (!pass) throw new InvalidOperationException(message); File.AppendAllText(Report, "PASS " + message + "\n"); }
    static void Near(Transform target)
    {
        var player = Player.LocalInstance; var capsule = player.GetComponent<CharacterController>();
        capsule.enabled = false; player.transform.SetPositionAndRotation(target.position + Vector3.back * 2, Quaternion.identity); capsule.enabled = true;
        Physics.SyncTransforms();
    }
    static void Model()
    {
        var state = new TrialHintState(); var action = new TrialHintAction("book", "Book");
        state.Advance(action, 0); state.Advance(action, 29.9f); Check(!state.Visible, "no hint before 30 seconds");
        state.Advance(action, 90, true); Check(!state.Visible && state.WaitingSeconds == 29.9f, "modal time excluded from delay");
        state.Advance(action, .11f); Check(state.Visible, "hint appears after 30 active seconds");
        state.Advance(action, 120); Check(state.Visible && state.Key == "book", "unfinished hint remains visible; no periodic reminder reset");
        state.Advance(action.Suspend(true), 90); Check(!state.Visible, "visible hint hides during a modal");
        state.Advance(action, 0); Check(state.Visible, "same action resumes without another 30-second delay");
        state.Advance(new TrialHintAction("collect", "Collect"), 8); Check(!state.Visible && state.WaitingSeconds == 0, "new action clears old hint and starts a fresh delay");
        state.Advance(default, 90); Check(!state.Visible && state.Key == null, "terminal/absent action clears hint");
    }
    public static IEnumerator Run(string scene)
    {
        if (scene == "PRELIM") { File.WriteAllText(Report, "Offline saved-scene Play Mode checks. Delays accelerated only through an Editor-only clock input. No account writes or completion rewards.\n"); TrialHintPresentationChecks.Begin(); Model(); }
        var owner = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(component => component is ITrialHintSource &&
            (!(component is DynamicLogicPuzzle puzzle) || puzzle.IsTruthTableHintSource) && (!(component is QuizManager) || scene == "PRELIM"));
        var source = (ITrialHintSource)owner;
        var hints = TrialContextHints.Instance;
        Check(hints != null && UnityEngine.Object.FindObjectsByType<TrialContextHints>(FindObjectsSortMode.None).Length == 1, scene + " has one shared hint display");
        var initial = source.RequiredHintAction();
        Check(initial.Available && initial.Message.Contains("Book Statue") && !initial.Paused, scene + " begins with an unfinished book action, not an answer reminder: " + initial.Key);
        float by = Time.realtimeSinceStartup + 12;
        do { hints.AdvanceForValidation(0); yield return null; } while (hints.Suspended && Time.realtimeSinceStartup < by);
        Check(!hints.Suspended, scene + " Help/tutorial camera restoration finishes before active waiting is measured");
        hints.State.Clear(); hints.AdvanceForValidation(0); hints.AdvanceForValidation(29);
        Check(!hints.State.Visible, scene + " waits before showing the book hint");
        hints.AdvanceForValidation(1.1f); yield return new WaitForSecondsRealtime(.4f);
        Check(hints.State.Visible && hints.Opacity > .9f, scene + " fades in after the delay; " + Diagnostics(hints, source));
        int firstSound = hints.NotificationPlayCount;
        hints.AdvanceForValidation(0); hints.AdvanceForValidation(0);
        Check(hints.NotificationPlayCount == firstSound, scene + " text refresh does not replay hint audio");
        string key = hints.State.Key;
        Player.LocalInstance.GetComponent<CharacterController>().Move(Vector3.right * .1f); yield return null;
        Check(hints.State.Key == key && hints.State.Visible, scene + " unrelated movement does not complete or reset the action");
        var help = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(button => button.interactable &&
            button.GetComponentsInChildren<TMPro.TMP_Text>().Any(text => text.text.Trim().Equals("Help", StringComparison.OrdinalIgnoreCase)));
        help.onClick.Invoke(); yield return new WaitForSecondsRealtime(.2f);
        Check(Find<StageHelpPage>() != null, scene + " actual Help opens");
        float waiting = hints.State.WaitingSeconds;
        hints.AdvanceForValidation(45); yield return null;
        Check(!hints.State.Visible && hints.Opacity == 0 && Mathf.Abs(waiting - hints.State.WaitingSeconds) < .05f, scene + " Help hides and suspends the hint clock");
        Find<StageHelpPage>().BeginButton.onClick.Invoke(); yield return new WaitForSecondsRealtime(.3f);
        hints.AdvanceForValidation(0); yield return new WaitForSecondsRealtime(.4f);
        Check(hints.State.Visible && hints.State.Key == key && hints.NotificationPlayCount == firstSound, scene + " reopening Help returns to the same unfinished action without another sound");
        if (scene == "PRELIM")
        {
            hints.State.Clear(); yield return new WaitForSecondsRealtime(29);
            Check(!hints.State.Visible, "real-time Play Mode check: no hint at 29 seconds");
            yield return new WaitForSecondsRealtime(1.5f);
            Check(hints.State.Visible && hints.Opacity > .9f, "real-time Play Mode check: hint appears and fades in after 30 seconds");
        }

        var rig = PrelimTutorialCameraRig.Create("Hint QA transition");
        rig.MoveTowards(rig.transform.position + Vector3.up * 10, rig.transform.rotation, 40);
        waiting = hints.State.WaitingSeconds; hints.AdvanceForValidation(40);
        Check(!hints.State.Visible && Mathf.Abs(waiting - hints.State.WaitingSeconds) < .05f, scene + " moving camera suspends delay");
        rig.FinishReturn(); yield return new WaitForSecondsRealtime(2.5f);
        by = Time.realtimeSinceStartup + 12;
        do { hints.AdvanceForValidation(0); yield return null; } while (hints.Suspended && Time.realtimeSinceStartup < by);
        hints.AdvanceForValidation(0); yield return new WaitForSecondsRealtime(.4f);

        foreach (int size in new[] { 6, 12, 3 })
        {
            BookMapMobileChecks.SetSize(size); yield return new WaitForSecondsRealtime(.65f);
            hints.AdvanceForValidation(0); yield return new WaitForSecondsRealtime(.4f);
            yield return new WaitForEndOfFrame();
            Check(hints.State.Visible && hints.Opacity > .9f, scene + " rendered hint is visible; " + Diagnostics(hints, source));
            var corners = new Vector3[4]; hints.HintPanel.GetWorldCorners(corners);
            var safe = Screen.safeArea;
            if (safe.xMin < 0 || safe.yMin < 0 || safe.xMax > Screen.width || safe.yMax > Screen.height)
                safe = new Rect(0, 0, Screen.width, Screen.height);
            Check(corners.All(point => point.x >= safe.xMin - 1 && point.x <= safe.xMax + 1 && point.y >= safe.yMin - 1 && point.y <= safe.yMax + 1), scene + " hint fits safe area at " + Screen.width + "x" + Screen.height);
            Check(hints.GetComponent<GraphicRaycaster>() == null && hints.GetComponentsInChildren<Graphic>().All(graphic => !graphic.raycastTarget), scene + " hint cannot block mobile taps at " + Screen.width + "x" + Screen.height);
            TrialHintPresentationChecks.Layout(scene, hints);
            if (scene == "PRELIM" || scene == "Graph" || scene == "Trees")
            { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot("Library/Hint_" + scene + "_" + Screen.width + "x" + Screen.height + ".png"); }
        }
        BookMapMobileChecks.SetSize(6); yield return new WaitForSecondsRealtime(.3f);
        yield return TrialHintPresentationChecks.Animations(scene, hints);
        yield return Actions(scene, owner);
        var current = source.RequiredHintAction(); hints.AdvanceForValidation(0);
        Check(current.Available && current.Key != key && !hints.State.Visible, scene + " required gameplay action replaces the book hint and starts a new delay");
        File.AppendAllText(Report, "VERIFIED " + scene + " next action: " + current.Message + "\n");
        yield return AdditionalModes(scene, owner);
        TerminalStates(scene, owner);
        if (scene == "Trees") File.AppendAllText(Report, "ALL TEN TRIALS PASSED. Editor mobile resolutions; physical-device profiling not performed.\n");
    }
    static string Diagnostics(TrialContextHints hints, ITrialHintSource source)
    {
        return "key=" + hints.State.Key + ",elapsed=" + hints.State.WaitingSeconds + ",alpha=" + hints.Opacity +
            ",input=" + GameInput.Instance.GameplayInputBlocked + ",time=" + Time.timeScale + ",sourcePaused=" + source.RequiredHintAction().Paused +
            ",blend=" + string.Join(",", UnityEngine.Object.FindObjectsByType<Unity.Cinemachine.CinemachineBrain>(FindObjectsSortMode.None).Select(brain => brain.IsBlending)) +
            ",rig=" + string.Join(",", UnityEngine.Object.FindObjectsByType<PrelimTutorialCameraRig>(FindObjectsSortMode.None).Select(rig => rig.IsMoving));
    }
    static void SetTerminal(object target, string fieldName, object value, ITrialHintSource source, string description)
    {
        var field = target.GetType().GetField(fieldName, Flags) ?? target.GetType().GetField("<" + fieldName + ">k__BackingField", Flags);
        object before = field.GetValue(target);
        try
        {
            field.SetValue(target, field.FieldType.IsEnum ? Enum.Parse(field.FieldType, (string)value) : value);
            Check(!source.RequiredHintAction().Available, description + " supplies no unfinished-action reminder");
            TrialContextHints.Instance.AdvanceForValidation(45);
            Check(!TrialContextHints.Instance.State.Visible && TrialContextHints.Instance.Opacity == 0, description + " immediately hides and clears the hint");
        }
        finally { field.SetValue(target, before); }
    }
    static void TerminalStates(string scene, MonoBehaviour owner)
    {
        var source = (ITrialHintSource)owner;
        switch (scene)
        {
            case "PRELIM": SetTerminal(Find<LevelTimerManager>(), "currentTimer", 0f, source, "Trial 1 Game Over"); break;
            case "TruthTable":
                SetTerminal(Find<TruthTableStageClock>(), "state", "GameOver", source, "Trial 2 Game Over");
                SetTerminal(Find<TruthTableStageClock>(), "state", "Completed", source, "Trial 2 completion"); break;
            case "RulesOfInference":
                SetTerminal(StageJourneyUI.Instance, "inferenceFinished", true, source, "Trial 3 Game Over");
                SetTerminal(owner, "PuzzleCompleted", true, source, "Trial 3 completion"); break;
            case "Formal vs. Informal Proofs": SetTerminal(owner, "finished", true, source, "Trial 4 result"); break;
            case "Direct&Indirect":
                SetTerminal(owner, "state", "GameOver", source, "Trial 5 Game Over"); SetTerminal(owner, "state", "StageComplete", source, "Trial 5 completion"); break;
            case "Induction":
                SetTerminal(owner, "State", "GameOver", source, "Trial 6 Game Over"); SetTerminal(owner, "State", "StageComplete", source, "Trial 6 completion"); break;
            case "Setssss":
                SetTerminal(owner, "gameOverTriggered", true, source, "Trial 7 Game Over"); SetTerminal(owner, "complete", true, source, "Trial 7 completion"); break;
            default:
                string field = scene == "Functions and Relations" ? "state" : "State";
                SetTerminal(owner, field, "GAME_OVER", source, scene + " Game Over"); SetTerminal(owner, field, "STAGE_COMPLETE", source, scene + " completion"); break;
        }
    }
    static IEnumerator AdditionalModes(string scene, MonoBehaviour owner)
    {
        var hints = TrialContextHints.Instance;
        if (scene == "Graph")
        {
            var stage = (LogicLegends.Graph.GraphStageManager)owner;
            owner.GetType().GetField("<ChallengeIndex>k__BackingField", Flags).SetValue(owner, 1);
            Field<bool[]>(stage.Introductions, "seen")[1] = true;
            Call(stage, "BuildChallenge");
            stage.GetType().GetMethod("SetGraphRevealed", Flags).Invoke(stage, new object[] { true });
            float by = Time.realtimeSinceStartup + 12;
            do { yield return null; hints.AdvanceForValidation(0); } while ((stage.QuestionView == null || !stage.QuestionView.IsOpen || hints.Suspended) && Time.realtimeSinceStartup < by);
            Check(stage.QuestionView.IsOpen && !hints.Suspended, "Graph degree camera settles and answer mode permits hints despite blocked movement");
            Check(stage.RequiredHintAction().Message.Contains("select a number"), "Graph degree mode asks for a number without giving it");
            stage.SelectAnswer(0); Check(stage.RequiredHintAction().Message.Contains("Confirm"), "Graph number selection changes the required action to Confirm");
            hints.AdvanceForValidation(0); hints.AdvanceForValidation(31); yield return new WaitForSecondsRealtime(.7f);
            var subject = stage.ui.TutorialTarget("Objective");
            Check(!ScreenRect(subject).Overlaps(ScreenRect(hints.HintPanel)), "hint stays clear of Graph's top-center degree question");
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot("Library/Hint_Graph_Degree.png"); yield return null;
        }
        if (scene == "Trees")
        {
            var stage = (LogicLegends.Trees.TreeStageManager)owner;
            owner.GetType().GetField("<ChallengeIndex>k__BackingField", Flags).SetValue(owner, 2);
            Call(stage, "ChooseVariant"); stage.board.Build(stage.Challenge); stage.ui.RebuildDiagram();
            var edge = stage.board.Edges.First(e => e.removable);
            stage.SelectNode(edge.source); Check(stage.RequiredHintAction().Message.Contains("second endpoint"), "Tree repair: first node requires a second endpoint");
            stage.SelectNode(edge.destination); Check(stage.RequiredHintAction().Message.Contains("Connect or Remove"), "Tree repair: two endpoints require an edit action");
            string before = stage.RequiredHintAction().Key; stage.Edit(true);
            Check(stage.RequiredHintAction().Key != before, "successful tree edit advances the action clock; selecting unrelated nodes does not finish the edit");
            hints.AdvanceForValidation(0); hints.AdvanceForValidation(31); yield return new WaitForSecondsRealtime(.7f);
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot("Library/Hint_Trees_Repair.png"); yield return null;
        }
    }
    static Rect ScreenRect(RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners.Min(p => p.x), corners.Min(p => p.y), corners.Max(p => p.x), corners.Max(p => p.y));
    }
    static IEnumerator Actions(string scene, MonoBehaviour owner)
    {
        switch (scene)
        {
            case "PRELIM":
                var quiz = (QuizManager)owner; quiz.OpenQuiz(Find<BookInteract>());
                Check(quiz.RequiredHintAction().Message.Contains("pad"), "Trial 1 uses the actual door-pad selection mechanic"); break;
            case "TruthTable":
                var puzzle = (DynamicLogicPuzzle)owner; var clock = Find<TruthTableStageClock>(); clock.StartFromBook();
                var action = puzzle.RequiredHintAction(); Check(action.Available && !action.Message.Contains("Book Statue"), "Trial 2 advances to house/block collection");
                clock.SetQuizOpen(true); Check(puzzle.RequiredHintAction().Paused, "Trial 2 quiz pauses hint timing"); clock.SetQuizOpen(false);
                var blocks = TruthBlock.ActiveBlocks.Where(block => block != null && !block.IsPlacedInColumn).Take(4).ToArray();
                Check(blocks.Length == 4, "Truth Table test has four physical blocks");
                for (int row = 0; row < 4; row++)
                { InventoryManager.Instance.TryPickupBlock(blocks[row].value, blocks[row]); Check(puzzle.RequiredHintAction().Message.Contains("arrow"), "Trial 2 selected inventory block needs placement"); puzzle.TryPlace(blocks[row], 0); }
                Check(puzzle.RequiredHintAction().Message.Contains("Submit Column"), "Trial 2 all four rows need Submit Column, not another block"); break;
            case "RulesOfInference":
                var inference = (LogicLegends.Inference.InferenceChallenge)owner; Check(inference.StartFromBook(), "Inference book activates");
                Check(inference.RequiredHintAction().Message.Contains("View Board"), "Trial 3 directs unopened premises to the altar");
                inference.OpenBoard(Player.LocalInstance); Check(inference.RequiredHintAction().Message.Contains("blanks"), "Trial 3 incomplete premises need word-bank entries");
                inference.CloseBoard(); break;
            case "Formal vs. Informal Proofs":
                var proofs = (ProofsLibraryStageController)owner; proofs.Activate();
                Check(proofs.RequiredHintAction().Message.Contains("Read a scroll"), "Trial 4 begins with scroll reading");
                proofs.ReadScroll(0); Check(proofs.RequiredHintAction().Paused, "Trial 4 scroll reader suspends hints"); proofs.CarryReadScroll();
                Check(proofs.RequiredHintAction().Message.Contains("Place your scroll"), "Trial 4 carrying needs placement");
                proofs.PlaceOnPedestal(0); break;
            case "Direct&Indirect":
                var direct = (DirectIndirectProofStageController)owner; direct.ActivateChallenge();
                Check(direct.RequiredHintAction().Message.Contains("chosen proof altar"), "Trial 5 starts with altar selection"); direct.InteractAltar(true);
                Check(direct.RequiredHintAction().Message.Contains("submit"), "Trial 5 selected altar needs book submission"); break;
            case "Induction":
                var induction = (LogicLegends.Induction.InductionStageManager)owner; Near(induction.book); induction.InteractBook();
                Check(induction.State == LogicLegends.Induction.InductionState.Active, "Induction book activates");
                Check(induction.RequiredHintAction().Message.Contains("scroll stand"), "Trial 6 needs a formula scroll"); break;
            case "Setssss":
                var sets = (SetssssChallengeOne)owner; Near(Field<Transform>(sets, "bookStatue"));
                sets.TryHandleInteraction(); Check(sets.Started, "Sets book activates");
                Check(sets.RequiredHintAction().Message.Contains("Collect an element"), "Trial 7 needs a requested element, without naming its answer"); break;
            case "Functions and Relations":
                var relations = (FunctionsRelationsStageManager)owner; relations.ActivateChallenge();
                Check(relations.RequiredHintAction().Message.Contains("Domain"), "Trial 8 starts at Domain");
                relations.Mapping.SelectDomain(relations.CurrentChallenge.domain[0]);
                Check(relations.RequiredHintAction().Message.Contains("Range"), "Trial 8 selected input needs an output");
                relations.Mapping.SelectCodomain(relations.CurrentChallenge.codomain[0]); relations.Mapping.ClearSelection();
                Check(relations.RequiredHintAction().Message.Contains("Domain") || relations.RequiredHintAction().Message.Contains("Submit"), "Trial 8 completed connection needs another input or submission"); break;
            case "Graph":
                var graph = (LogicLegends.Graph.GraphStageManager)owner; Near(graph.bookStatue); graph.InteractBook();
                float by = Time.realtimeSinceStartup + 12;
                while (graph.State == LogicLegends.Graph.GraphStageState.WAITING_FOR_ACTIVATION && Time.realtimeSinceStartup < by) yield return null;
                Check(graph.State == LogicLegends.Graph.GraphStageState.ACTIVE, "Graph book activates");
                Check(graph.RequiredHintAction().Message.Contains("bridges"), "Trial 9 requires a traversed edge, not unrelated walking"); break;
            case "Trees":
                var tree = (LogicLegends.Trees.TreeStageManager)owner; Near(tree.bookStatue); tree.InteractBook();
                float readyBy = Time.realtimeSinceStartup + 12;
                while (!tree.CanSolve && Time.realtimeSinceStartup < readyBy) yield return null;
                Check(tree.BoardOpen && tree.State == LogicLegends.Trees.TreeStageState.ACTIVE, "Trees book opens the interactive board");
                Check(tree.RequiredHintAction().Message.Contains("Tap the node"), "Trial 10 first needs a node selection");
                tree.SelectNode(tree.Challenge.nodes[0].id);
                Check(tree.RequiredHintAction().Message.Contains("Submit"), "Trial 10 selected node needs submission"); break;
        }
        yield return null;
    }
}
#endif
