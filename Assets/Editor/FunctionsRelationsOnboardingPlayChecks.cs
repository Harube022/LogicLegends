using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Real UI/input onboarding checks, followed by the existing gameplay regression.</summary>
public static class FunctionsRelationsOnboardingPlayChecks
{
    public static IEnumerator Run(FunctionsRelationsStageManager stage)
    {
        var help = stage.Help; var ui = UnityEngine.Object.FindFirstObjectByType<FunctionsRelationsPresentation>();
        float readyBy = Time.unscaledTime + 5f;
        while (!help.IsOpen && Time.unscaledTime < readyBy) yield return null;
        float time = stage.TimeRemaining; int variant = stage.CurrentVariantIndex;
        Check(help.IsOpen && Time.timeScale == 0f && GameInput.Instance.GameplayInputBlocked, "automatic Help pauses input/time");
        stage.ActivateChallenge(); stage.Submit(); GameInput.Instance.MobileInteract();
        Check(stage.State == FunctionsRelationsState.WAITING_FOR_ACTIVATION && stage.TimeRemaining == time, "Help cannot start or grade a challenge");
        var page = UnityEngine.Object.FindFirstObjectByType<StageHelpPage>();
        Check(page != null && page.CloseButton == null, "one scrollable Help with sole Begin exit");
        Click(page.BeginButton); yield return null;
        var tutorial = FunctionsRelationsControlsOnboarding.Active;
        Check(tutorial != null && tutorial.CurrentStep == FunctionsRelationsTutorialStep.Movement && Time.timeScale == 1f, "Let's Begin starts movement only");
        CheckFrozen(stage, time, variant);
        var mobile = MobileInputUI.FindJoystick(); var bg = Field<RectTransform>(mobile, "joystickBackground");
        var data = new PointerEventData(EventSystem.current);
        data.position = RectTransformUtility.WorldToScreenPoint(null, bg.TransformPoint(bg.rect.center + Vector2.right * bg.rect.width * .06f));
        mobile.OnPointerDown(data); yield return new WaitForSecondsRealtime(.2f); mobile.OnPointerUp(data);
        Check(tutorial.CurrentStep == FunctionsRelationsTutorialStep.Movement, "small joystick jitter does not advance");
        yield return Capture("TutorialMovement");
        data.position = RectTransformUtility.WorldToScreenPoint(null, bg.TransformPoint(bg.rect.center + Vector2.left * bg.rect.width * .4f));
        mobile.OnPointerDown(data);
        float end = Time.unscaledTime + 4f;
        while (tutorial.CurrentStep == FunctionsRelationsTutorialStep.Movement && Time.unscaledTime < end) yield return null;
        mobile.OnPointerUp(data);
        Check(tutorial.CurrentStep == FunctionsRelationsTutorialStep.Jump, "intentional real movement advances only to jump");
        yield return Layout(tutorial, "Jump");
        var jump = GameObject.Find("Canvas 1").GetComponentsInChildren<Button>(true).Single(b => b.name == "JumpButton");
        Click(jump); end = Time.unscaledTime + 3f;
        while (tutorial.CurrentStep == FunctionsRelationsTutorialStep.Jump && Time.unscaledTime < end) yield return null;
        Check(tutorial.CurrentStep == FunctionsRelationsTutorialStep.Interaction && tutorial.PracticeObject == null, "successful physical jump starts hand-only practice");
        yield return Layout(tutorial, "Interaction");
        var player = Player.LocalInstance;
        var hand = GameObject.Find("Canvas 1").GetComponentsInChildren<Button>(true).Single(b => b.name == "InteractButton");
        Click(hand); yield return null;
        Check(tutorial.CurrentStep == FunctionsRelationsTutorialStep.Book && tutorial.PracticeObject == null, "hand interaction starts book practice");
        yield return new WaitForSecondsRealtime(.9f); yield return Layout(tutorial, "Book");
        Click(hand); yield return null;
        Check(tutorial.CurrentStep == FunctionsRelationsTutorialStep.Timer, "book practice does not activate real challenge");
        yield return Layout(tutorial, "Timer"); Next(tutorial); yield return null;
        Click(stage.ViewBoardButton); yield return new WaitForSecondsRealtime(.95f);
        Check(stage.BoardView.Viewing && tutorial.CurrentStep == FunctionsRelationsTutorialStep.Board, "View Board opens existing camera flow");
        Check(!stage.BoardView.backButton.interactable, "explanation requires Next before Back");
        yield return Layout(tutorial, "Board"); Next(tutorial); yield return null;
        Click(stage.BoardView.backButton); yield return new WaitForSecondsRealtime(.95f);
        Check(tutorial.CurrentStep == FunctionsRelationsTutorialStep.Domain && !stage.BoardView.Viewing, "Back restores guide and moves to Domain");
        yield return new WaitForSecondsRealtime(.9f); yield return Layout(tutorial, "Domain");
        Click(hand); yield return null;
        Check(tutorial.CurrentStep == FunctionsRelationsTutorialStep.Input && ui.domainPanel.gameObject.activeSelf, "real hand opens practice input UI");
        yield return Layout(tutorial, "Input"); Click(stage.DomainButtons[0]); yield return new WaitForSecondsRealtime(.9f);
        Check(tutorial.PracticeInput == "6" && stage.SelectedDomain == null, "practice selection leaves real selected input untouched");
        yield return Layout(tutorial, "Output"); Click(hand); yield return null;
        yield return Layout(tutorial, "OutputChoices"); Click(stage.RangeButtons[1]); yield return null;
        Check(tutorial.PracticeOutput == "Y" && stage.Mapping.ConnectionCount == 0, "practice laser is separate from real mapping");
        yield return Layout(tutorial, "Connection"); Next(tutorial); yield return null;
        Click(stage.UndoButton); yield return null;
        Check(tutorial.CurrentStep == FunctionsRelationsTutorialStep.Submit && tutorial.PracticeOutput == null, "Undo removes practice connection");
        yield return Layout(tutorial, "Submit"); Click(stage.SubmitButton); CheckFrozen(stage, time, variant);
        Next(tutorial); yield return null;
        Check(tutorial.CurrentStep == FunctionsRelationsTutorialStep.Confirm && !stage.BoardView.Viewing, "completion restores camera before confirmation");
        Click(tutorial.NoButton); yield return null;
        Check(tutorial.CurrentStep == FunctionsRelationsTutorialStep.Movement, "No repeats from movement");
        var skip = tutorial.GetComponentInParent<Canvas>().GetComponentsInChildren<Button>().Single(b => b.name == "PRELIM Shared Skip Tutorial");
        Click(skip); yield return null; Click(tutorial.NoButton); yield return null;
        // Interrupt a guided camera/board transition with the same shared Skip.
        typeof(FunctionsRelationsControlsOnboarding).GetMethod("StartCameraGuide", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(tutorial, null);
        typeof(FunctionsRelationsControlsOnboarding).GetMethod("ShowStep", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(tutorial,
            new object[] { FunctionsRelationsTutorialStep.ViewBoard, "Practice View Board", ui.viewBoard, false, null });
        yield return null; yield return new WaitForEndOfFrame();
        Click(stage.ViewBoardButton); Click(skip); yield return null;
        yield return new WaitForEndOfFrame();
        Check(tutorial.CurrentStep == FunctionsRelationsTutorialStep.Confirm && !stage.BoardView.Viewing, "Skip cancels board camera transition");
        Click(tutorial.YesButton); yield return null; yield return null;
        Check(FunctionsRelationsControlsOnboarding.Active == null && !stage.OnboardingPending && Player.LocalInstance.enabled && !GameInput.Instance.GameplayInputBlocked, "Yes restores controls and prepares book gate");
        CheckFrozen(stage, time, variant);
        Check(UnityEngine.Object.FindObjectsByType<FunctionsRelationsControlsOnboarding>(FindObjectsSortMode.None).Length == 1 &&
            GameObject.Find("Functions Relations Practice Connections") == null && GameObject.Find("PRELIM Shared Skip Tutorial") == null, "no duplicate tutorial or practice content");
        Click(help.HelpButton); yield return new WaitForSecondsRealtime(.2f); Check(help.IsOpen, "Help reopens after onboarding");
        Click(UnityEngine.Object.FindFirstObjectByType<StageHelpPage>().BeginButton); yield return null;
        Check(FunctionsRelationsControlsOnboarding.Active == null && stage.TimeRemaining == time, "reopening Help does not restart onboarding or clock");
        Debug.Log("FUNCTIONS_RELATIONS_ONBOARDING_PASS " + Screen.width + "x" + Screen.height + " real movement/jump/hand, guided stations, board Back, choices/individual Remove, Skip/Yes/No, isolated time/progress, and Help pages");
    }
    public static IEnumerator CheckActiveHelp(FunctionsRelationsStageManager stage)
    {
        var pairs = stage.Mapping.Snapshot(); float clock = stage.TimeRemaining, active = stage.ActivePlaySeconds;
        bool controls = Player.LocalInstance.enabled;
        Click(stage.Help.HelpButton); yield return new WaitForSecondsRealtime(.4f);
        stage.Submit(); stage.ChooseDomain(1); stage.ChooseRange(1); stage.UndoRangeConnection();
        Check(stage.Help.IsOpen && Time.timeScale == 0f && stage.TimeRemaining == clock && stage.ActivePlaySeconds == active, "Help pauses an active countdown");
        Check(stage.Mapping.Snapshot().SequenceEqual(pairs) && stage.MistakeCount == 0 && stage.State == FunctionsRelationsState.ACTIVE, "Help preserves real connections and evaluation state");
        Click(Field<Button>(stage.Help, "beginButton")); yield return null;
        Check(!stage.Help.IsOpen && Time.timeScale == 1f && Player.LocalInstance.enabled == controls && !GameInput.Instance.GameplayInputBlocked && stage.Tutorial == null, "Help restores active controls without replaying tutorial");
    }
    static void CheckFrozen(FunctionsRelationsStageManager s, float time, int variant)
    { Check(s.TimeRemaining == time && s.ActivePlaySeconds == 0f && s.CorrectCount == 0 && s.MistakeCount == 0 && s.ChallengeIndex == 0 && s.CurrentVariantIndex == variant && s.Mapping.ConnectionCount == 0 && s.State == FunctionsRelationsState.WAITING_FOR_ACTIVATION, "onboarding never changes real game/progression"); }
    static void Next(FunctionsRelationsControlsOnboarding t) => Click(Field<Button>(t, "next"));
    static IEnumerator Layout(FunctionsRelationsControlsOnboarding t, string label)
    {
        yield return null; yield return new WaitForEndOfFrame();
        var panel = Field<RectTransform>(t, "instructions"); var text = Field<TMP_Text>(t, "instruction");
        text.ForceMeshUpdate(); Check(!text.isTextOverflowing, label + " explanation fits");
        var safe = Field<RectTransform>(t, "safe"); Rect p = RectOf(panel), a = RectOf(safe);
        Check(a.Contains(p.min) && a.Contains(p.max - Vector2.one), label + " instructions within safe area");
        var ui = UnityEngine.Object.FindFirstObjectByType<FunctionsRelationsPresentation>();
        foreach (var rect in ui.mobileControls.Concat(new[] { ui.hud, ui.interaction, ui.viewBoard, ui.submit, ui.boardBack, ui.domainPanel, ui.rangePanel }))
            if (rect != null && rect.gameObject.activeInHierarchy) Check(!p.Overlaps(RectOf(rect)), label + " explanation avoids " + rect.name);
        yield return Capture("Tutorial" + label);
    }
    static void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();
        Check(button.gameObject.activeInHierarchy && button.interactable, button.name + " tappable");
        var pos = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)button.transform).TransformPoint(((RectTransform)button.transform).rect.center));
        var data = new PointerEventData(EventSystem.current) { position = pos, button = PointerEventData.InputButton.Left };
        var hits = new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button, button.name + " unobstructed mobile tap (" + string.Join(", ", hits.Take(3).Select(h => h.gameObject.name)) + ")");
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
    }
    static Rect RectOf(RectTransform r) { var c = new Vector3[4]; r.GetWorldCorners(c); return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y); }
    static T Field<T>(object o, string n) => (T)o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
    static IEnumerator Capture(string label)
    { System.IO.Directory.CreateDirectory("Temp/FunctionsQA"); yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot("Temp/FunctionsQA/" + label + "-" + Screen.width + "x" + Screen.height + ".png"); yield return null; }
    static void Check(bool condition, string message) { if (!condition) throw new Exception("FUNCTIONS_ONBOARDING_QA: " + message); }
}
