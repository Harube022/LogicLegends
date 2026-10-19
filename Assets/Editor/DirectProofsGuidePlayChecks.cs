using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Opt-in offline checks: no authentication, account rewards or saved progression writes.</summary>
public static class DirectProofsGuidePlayChecks
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    const string Report = "Library/DirectProofsGuideChecks.txt";
    public static void Begin(int size = 6)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name != "Direct&Indirect") throw new InvalidOperationException("Open Direct&Indirect outside Play Mode.");
        var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        SessionState.SetInt("DirectQA.PreviousSize", (int)type.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(EditorWindow.GetWindow(type)));
        SessionState.SetInt("DirectQA.Size", size); SessionState.SetBool("DirectQA.Run", true);
        System.IO.File.WriteAllText(Report, "Direct & Indirect Help, practice, introductions and ten-question Play Mode checks\n");
        EditorApplication.isPlaying = true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap() { if (SessionState.GetBool("DirectQA.Run", false)) { SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded; } }
    [InitializeOnLoadMethod]
    static void Register() { EditorApplication.playModeStateChanged -= Restore; EditorApplication.playModeStateChanged += Restore; }
    static void Restore(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool("DirectQA.Run", false)) return;
        SessionState.SetBool("DirectQA.Run", false); Crystal.SafeArea.Sim = Crystal.SafeArea.SimDevice.None;
        var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        type.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(EditorWindow.GetWindow(type), SessionState.GetInt("DirectQA.PreviousSize", 6));
    }
    static void Loaded(Scene scene, LoadSceneMode load)
    {
        if (scene.name != "Direct&Indirect") return;
        SceneManager.sceneLoaded -= Loaded;
        var player = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Multiplayer_Male"));
        player.GetComponent<PlayerEquipmentLoader>().enabled = false;
        var sync = player.GetComponent<MultiplayerAvatarSync>(); if (sync != null) sync.enabled = false;
        foreach (var skin in player.GetComponentsInChildren<PlayerAnimator>(true)) skin.gameObject.SetActive(skin.name == "UNIFORM_MALE");
        var spawn = GameObject.Find("Trial Player Spawn").transform;
        player.transform.SetPositionAndRotation(spawn.position, spawn.rotation); player.GetComponent<CharacterController>().enabled = true;
        DirectIndirectProofStageController.SuppressProgressionForValidation = true;
        foreach (bool visible in new[] { true, false })
        {
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube); roof.name = visible ? "QA Ceiling Visible" : "QA Ceiling Hidden";
            roof.transform.position = new Vector3(56, 23, 50); roof.transform.localScale = new Vector3(30, .1f, 30);
            roof.GetComponent<Collider>().enabled = false; roof.GetComponent<Renderer>().enabled = visible;
        }
        new GameObject("Direct QA Runner").AddComponent<DirectGuideQaRunner>().StartCoroutine(Guard(Run()));
    }
    static IEnumerator Guard(IEnumerator run)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(run);
        while (stack.Count > 0)
        {
            object next;
            try { var current = stack.Peek(); if (!current.MoveNext()) { stack.Pop(); continue; } next = current.Current; }
            catch (Exception e) { System.IO.File.AppendAllText(Report, "FAIL " + e + "\n"); Debug.LogException(e); if (SessionState.GetBool("DirectQA.Diagnose", false)) EditorApplication.isPaused = true; else EditorApplication.isPlaying = false; yield break; }
            if (next is IEnumerator child) stack.Push(child); else yield return next;
        }
        Pass("ALL DIRECT PROOF GUIDE CHECKS PASSED"); EditorApplication.isPlaying = false;
    }
    static IEnumerator Run()
    {
        var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"); int size = SessionState.GetInt("DirectQA.Size", 6);
        type.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(EditorWindow.GetWindow(type), size);
        Crystal.SafeArea.Sim = size == 5 ? Crystal.SafeArea.SimDevice.iPhoneX : Crystal.SafeArea.SimDevice.None;
        DirectIndirectProofStageController stage = null; float until = Time.unscaledTime + 30;
        while (Time.unscaledTime < until)
        { stage = UnityEngine.Object.FindFirstObjectByType<DirectIndirectProofStageController>(); if (stage != null && stage.Guide != null && stage.Guide.IsBusy) break; yield return null; }
        Check(stage != null && stage.Guide != null && stage.Guide.IsBusy && Time.timeScale == 0, "first entry automatically opens paused Help");
        var guide = stage.Guide; var player = Player.LocalInstance; var input = GameInput.Instance; float time = stage.SecondsLeft;
        CheckSchedule(stage); Frozen(stage, time);
        CheckPresentation(stage);
        yield return Shot(guide, "Help1");
        for (int i = 0; i < 7; i++) Click(guide.NextButton);
        yield return Shot(guide, "Help8"); Click(guide.PreviousButton); Check(Text(guide).Contains("shared"), "Previous displays timing explanation"); Click(guide.NextButton);
        Click(guide.BeginButton); yield return null;
        Check(guide.IsPractice && guide.Step == DirectProofsPracticeStep.Movement && Time.timeScale > 0, "Let's Begin starts isolated control practice");
        var mobile = MobileInputUI.FindJoystick(); yield return Shot(guide, "Movement");
        SetMobile(mobile, new Vector2(.1f, 0)); yield return new WaitForSecondsRealtime(.9f);
        Check(guide.Step == DirectProofsPracticeStep.Movement, "small joystick jitter cannot complete movement");
        SetMobile(mobile, Vector2.right); until = Time.unscaledTime + 6;
        while (guide.Step == DirectProofsPracticeStep.Movement && Time.unscaledTime < until) yield return null;
        SetMobile(mobile, Vector2.zero); Check(guide.Step == DirectProofsPracticeStep.Jump, "intentional joystick movement advances only to jumping");
        yield return Shot(guide, "Jump");
        var controls = GameObject.Find("Canvas 1").transform.Find("SafeArea/Gameplay_Interface");
        var jump = controls.Find("JumpButton").GetComponent<Button>(); var hand = controls.Find("InteractButton").GetComponent<Button>();
        until = Time.unscaledTime + 3; while (!player.GetComponent<CharacterController>().isGrounded && Time.unscaledTime < until) yield return null;
        Click(jump); until = Time.unscaledTime + 3;
        while (guide.Step == DirectProofsPracticeStep.Jump && Time.unscaledTime < until) yield return null;
        Check(guide.Step == DirectProofsPracticeStep.Interaction && guide.PracticeObject != null, "successful real jump advances to practice interaction");
        yield return Shot(guide, "Interaction");
        var delta = guide.PracticeObject.transform.position - player.transform.position; delta.y = 0; player.transform.rotation = Quaternion.LookRotation(delta);
        Click(hand); yield return null; Check(guide.Step == DirectProofsPracticeStep.Timer && guide.PracticeObject == null, "hand interaction removes practice stand and advances");
        yield return Shot(guide, "Timer"); Click(guide.NextButton); yield return Shot(guide, "Progress"); Click(guide.NextButton);
        yield return Shot(guide, "Book"); Click(hand); yield return null;
        Check(guide.Step == DirectProofsPracticeStep.Instructions, "book practice never starts a real question"); Frozen(stage, time);
        yield return Shot(guide, "Instructions"); Click(guide.NextButton); yield return Shot(guide, "Altars"); Click(guide.NextButton);
        yield return Shot(guide, "Choose"); Click(guide.AlternateButton); Check(stage.MistakeCount == 0, "wrong practice method has no penalty"); Click(guide.ActionButton);
        yield return Shot(guide, "OpenBoard"); Click(guide.ActionButton); yield return Shot(guide, "PracticeOrdering");
        CheckReceivesPointer(guide.SafeArea.GetComponentsInChildren<Button>().Single(b => b.name == "PRELIM Shared Skip Tutorial"));
        Check(stage.OrderingBoard.IsPractice && !Enumerable.Range(0, 5).Any(i => stage.PlacedTabletId(i) >= 0), "practice uses actual board with a separate order");
        Drag(stage.OrderingBoard, 1, 2); Check(stage.OrderingBoard.CurrentOrder.SequenceEqual(new[] { 0, 1, 2, 3, 4 }), "actual touch/mouse drag swaps practice rows");
        Click(stage.OrderingBoard.Panel.GetComponentsInChildren<Button>().Single(b => b.name == "Close Altar Board")); yield return null;
        Check(guide.Step == DirectProofsPracticeStep.Correction, "Back keeps practice order and explains correction");
        yield return Shot(guide, "Correction"); Click(guide.NextButton); yield return Shot(guide, "ConfirmBook"); Click(hand); yield return null;
        Check(guide.Step == DirectProofsPracticeStep.Confirmation && !stage.OrderingBoard.IsOpen, "practice confirmation restores camera and opens Let's start"); Frozen(stage, time);
        yield return Shot(guide, "Confirmation"); Click(guide.AlternateButton); yield return null;
        Check(guide.Step == DirectProofsPracticeStep.Movement, "No repeats movement");
        var skip = guide.SafeArea.GetComponentsInChildren<Button>(true).Single(b => b.name == "PRELIM Shared Skip Tutorial"); Click(skip); yield return null;
        Check(guide.Step == DirectProofsPracticeStep.Confirmation, "one shared Skip cleans practice and opens same confirmation");
        yield return new WaitForSecondsRealtime(1.1f); Click(guide.ActionButton); yield return null; yield return null;
        Check(!stage.OnboardingPending && guide.IsIntroduction, "Yes prepares Challenge 1 introduction without starting timer"); Frozen(stage, time);
        yield return Introduction(stage, false);
        Check(player.enabled && !input.GameplayInputBlocked && !stage.GuidanceBlocksGameplay, "introduction restores gameplay and book gate");
        Pass("Help, intentional movement, successful jump, hand, book, separate classification/drag/correction practice, No/Skip/Yes, camera and roof cleanup");
        for (int c = 1; c <= 5; c++)
        {
            if (c > 1) yield return Introduction(stage, c % 2 == 0);
            int previous = -1;
            for (int q = 1; q <= 2; q++)
            {
                Check(stage.ChallengeNumber == c && stage.QuestionNumber == q && stage.State == DirectIndirectProofStageController.TrialState.WaitingForActivation, "next question waits for book");
                Check(!guide.IsBusy, "challenge introduction does not repeat before second question");
                Move(player, stage.Book.position + Vector3.forward * 2); input.MobileInteract(); yield return null;
                Check(stage.State == DirectIndirectProofStageController.TrialState.Active && stage.ActiveVariantIndex != previous, "hand starts a distinct selected question");
                previous = stage.ActiveVariantIndex;
                var challenge = Field<TrialProofChallenge>(stage, "activeChallenge");
                Check(stage.Progress.text.Contains("Question " + q + " of 2"), "HUD reports question count");
                if (c == 1 && q == 1)
                {
                    stage.OpenProblem(); yield return null; yield return null;
                    var reader = Field<GameObject>(stage, "readerPanel"); var corners = new Vector3[4]; reader.GetComponent<RectTransform>().GetWorldCorners(corners);
                    Check(corners.All(p => p.x >= 0 && p.x <= Screen.width && p.y >= 0 && p.y <= Screen.height), "full-problem reader fits the mobile screen");
                    Check(Field<TMP_Text>(stage, "readerBody").text == challenge.problem && reader.GetComponent<ScrollRect>().content != null, "full-problem reader uses actual data with scrolling");
                    CheckReceivesPointer(Field<Button>(stage, "closeReaderButton"));
                    ScreenCapture.CaptureScreenshot("Temp/DirectProofsQA/" + Screen.width + "-FullProblem.png");
                    stage.CloseReader(); yield return null;
                    var beforeOrder = Enumerable.Range(0, 5).Select(stage.PlacedTabletId).ToArray(); float before = stage.SecondsLeft;
                    guide.OpenHelp(); yield return new WaitForSecondsRealtime(.5f);
                    Check(stage.SecondsLeft == before && stage.State == DirectIndirectProofStageController.TrialState.Active, "reopened Help pauses an active timer");
                    Click(guide.BeginButton); yield return null;
                    Check(!guide.IsBusy && beforeOrder.SequenceEqual(Enumerable.Range(0, 5).Select(stage.PlacedTabletId)) && input.GameplayInputBlocked == false, "Help resumes exact active progress without replay");
                }
                if (c == 1)
                {
                    bool correctDirect = challenge.correctMethod == TrialProofMethod.Direct;
                    stage.InteractAltar(!correctDirect); yield return Wrong(stage, input);
                    stage.InteractAltar(correctDirect);
                }
                else
                {
                    if (c <= 3) stage.InteractAltar(c == 2); else stage.InteractNeutralAltar();
                    yield return new WaitForSecondsRealtime(.5f);
                    Check(stage.OrderingBoard.IsOpen && !stage.OrderingBoard.IsPractice, "actual active altar opens ordering UI");
                    // Every question uses real drag handlers, never an injected answer array.
                    var order = stage.OrderingBoard.CurrentOrder;
                    for (int row = 0; row < order.Length; row++)
                    {
                        int from = Array.IndexOf(order, row); if (from != row) Drag(stage.OrderingBoard, from, row);
                        order = stage.OrderingBoard.CurrentOrder;
                    }
                    if (!Field<bool>(stage, "stepsReordered")) { Drag(stage.OrderingBoard, 0, 1); Drag(stage.OrderingBoard, 0, 1); }
                    if (c == 5) Click(stage.OrderingBoard.Panel.GetComponentsInChildren<Button>().Single(b => b.name == "Strategy " + (challenge.correctMethod == TrialProofMethod.Direct ? "DIRECT" : challenge.correctMethod == TrialProofMethod.Contrapositive ? "CONTRAPOSITIVE" : "CONTRADICTION")));
                    if (c == 3 && q == 1)
                    {
                        Drag(stage.OrderingBoard, 0, 1); stage.OrderingBoard.Close(); yield return Wrong(stage, input);
                        stage.InteractAltar(false); yield return new WaitForSecondsRealtime(.4f); Drag(stage.OrderingBoard, 0, 1);
                    }
                    yield return GameplayShot(stage, c, q);
                    stage.OrderingBoard.Close();
                    Check(challenge.IsCorrect(stage.SelectedMethod, Enumerable.Range(0, 5).Select(stage.PlacedTabletId).ToArray()), "unchanged validator accepts arranged actual data");
                }
                Move(player, stage.Book.position + Vector3.forward * 2); input.MobileInteract(); yield return null;
                Check(stage.State == DirectIndirectProofStageController.TrialState.ChallengeComplete, "real book validates complete answer");
                float paused = stage.SecondsLeft; yield return new WaitForSecondsRealtime(1.1f);
                Check(stage.SecondsLeft == paused && stage.CorrectCount == (c - 1) * 2 + q, "correct answer pauses shared time and counts once");
                Pass("challenge " + c + " question " + q + ": selected variant " + (previous + 1) + ", real method/order, book submission, retained timer and question progression");
            }
        }
        Check(stage.State == DirectIndirectProofStageController.TrialState.StageComplete && stage.CorrectCount == 10 && stage.MistakeCount == 3, "ten real questions complete five challenges with expected penalties");
        Check(UnityEngine.Object.FindObjectsByType<DirectProofsGuide>(FindObjectsSortMode.None).Length == 1 && !guide.IsBusy && GameObject.Find("Direct Proofs PRACTICE — Interact with me") == null, "no duplicate guide/practice content remains");
    }
    static IEnumerator Wrong(DirectIndirectProofStageController stage, GameInput input)
    {
        var kept = Enumerable.Range(0, 5).Select(stage.PlacedTabletId).ToArray(); int method = stage.SelectedMethod; int mistakes = stage.MistakeCount;
        float time = stage.SecondsLeft, active = stage.ActiveSeconds;
        Move(Player.LocalInstance, stage.Book.position + Vector3.forward * 2); input.MobileInteract(); yield return new WaitForSecondsRealtime(.9f);
        Check(stage.State == DirectIndirectProofStageController.TrialState.Active && stage.MistakeCount == mistakes + 1, "wrong confirmation penalizes once then permits correction");
        Check(Mathf.Abs(time - stage.SecondsLeft - (stage.ActiveSeconds - active) - stage.WrongPenalty) < .02f, "time penalty remains exactly ten seconds apart from elapsed gameplay");
        Check(kept.SequenceEqual(Enumerable.Range(0, 5).Select(stage.PlacedTabletId)) && method == stage.SelectedMethod, "wrong proof keeps method and placements");
    }
    static IEnumerator Introduction(DirectIndirectProofStageController stage, bool skip)
    {
        var guide = stage.Guide; yield return null; yield return null; float time = stage.SecondsLeft;
        Check(guide.IsIntroduction && Time.timeScale == 0 && !Player.LocalInstance.enabled && GameInput.Instance.GameplayInputBlocked, "introduction pauses timer and all gameplay");
        Check(!GameObject.Find("QA Ceiling Visible").GetComponent<Renderer>().enabled && !GameObject.Find("QA Ceiling Hidden").GetComponent<Renderer>().enabled, "overhead camera hides ceilings without changing originally hidden state");
        var room = GameObject.Find("Stage").GetComponent<Renderer>(); var originals = (Material[])Field<Material[]>(guide, "roomMaterials").Clone();
        var collision = GameObject.Find("Stage").GetComponent<Collider>();
        Check(room.sharedMaterials.All(m => m.shader.name.Contains("Tutorial Cutaway")) && collision.enabled, "combined ceiling uses a visual-only cutaway while room collision stays enabled");
        yield return Shot(guide, "Challenge" + stage.ChallengeNumber + "Overview");
        Check(stage.State == DirectIndirectProofStageController.TrialState.WaitingForActivation && stage.SecondsLeft == time, "introduction consumes no time or activation");
        Check(guide.SafeArea.GetComponentsInChildren<Button>(true).Count(b => b.name == "PRELIM Shared Skip Tutorial" && b.gameObject.activeInHierarchy) == 1, "one shared Skip throughout intro");
        if (skip) Click(guide.SafeArea.GetComponentsInChildren<Button>().Single(b => b.name == "PRELIM Shared Skip Tutorial"));
        else for (int i = 0; i < 3; i++) { yield return Shot(guide, "Challenge" + stage.ChallengeNumber + "Page" + (i + 1)); Click(guide.NextButton); }
        yield return new WaitForSecondsRealtime(PrelimUIPresentation.CameraTransitionSeconds + .25f);
        Check(UnityEngine.Object.FindObjectsByType<PrelimTutorialCameraRig>(FindObjectsSortMode.None).Length==0 && Camera.main.GetComponent<Unity.Cinemachine.CinemachineBrain>().enabled, "camera blend completes with no leftover tutorial camera or disabled Brain");
        Check(!guide.IsBusy && guide.IntroductionSeen(stage.ChallengeNumber) && Time.timeScale > 0, "Next/Skip marks intro seen and restores controls");
        Check(GameObject.Find("QA Ceiling Visible").GetComponent<Renderer>().enabled && !GameObject.Find("QA Ceiling Hidden").GetComponent<Renderer>().enabled, "all previous ceiling renderer states restored");
        Check(room.sharedMaterials.SequenceEqual(originals) && collision.enabled, "original chamber material references and collision restored exactly");
        Check(stage.State == DirectIndirectProofStageController.TrialState.WaitingForActivation && stage.SecondsLeft == time, "intro leaves real book activation required");
    }
    static void CheckPresentation(DirectIndirectProofStageController stage)
    {
        var guide = stage.Guide;
        var help = Field<RectTransform>(guide,"helpCard");
        var source = Resources.Load<GameObject>("StageUI/PrelimHelpBook");
        Check(help.GetComponent<Image>().sprite == source.GetComponent<Image>().sprite, "Help uses PRELIM book artwork");
        Check(guide.HelpButton.image.sprite == Resources.Load<GameObject>("StageUI/PrelimHelpButton").GetComponent<Image>().sprite, "Help button uses PRELIM artwork");
        Check(guide.NextButton.onClick.GetPersistentEventCount()==0, "cloned Help has no stale scene callbacks");
        Check(Field<RectTransform>(guide,"instructionCard").GetComponent<Image>().sprite == null, "tutorial uses PRELIM instruction panel without custom wood frame");
        var hud = Field<RectTransform>(guide,"hud");
        Check(hud.sizeDelta == new Vector2(500,136) && hud.anchoredPosition == new Vector2(-24,-24), "HUD shares Propositional size and safe-area dock");
        Check(stage.Clock.font == StageUITheme.HeadingFont && stage.Clock.color == StageUITheme.Gold && stage.Progress.color == StageUITheme.Ink, "HUD fonts and colors match PRELIM");
        Check(hud.GetComponentInChildren<TMP_Text>().text == TrialCatalog.ForScene("Direct&Indirect").Title, "HUD retains Direct trial number and title");
        Pass("PRELIM book/button artwork, tutorial panel, shared HUD and Direct title verified");
    }
    static void CheckSchedule(DirectIndirectProofStageController stage)
    {
        var schedule = Field<int[,]>(stage, "selectedVariants"); var original = (int[,])schedule.Clone(); var state = UnityEngine.Random.state;
        var seen = Enumerable.Range(0, 5).Select(c => new HashSet<int>()).ToArray();
        for (int sample = 0; sample < 256; sample++)
        {
            stage.GetType().GetMethod("BuildQuestionSchedule", Hidden).Invoke(stage, null);
            for (int c = 0; c < 5; c++) { Check(schedule[c, 0] != schedule[c, 1], "distinct schedule pair"); seen[c].Add(schedule[c, 0]); seen[c].Add(schedule[c, 1]); }
        }
        Array.Copy(original, schedule, original.Length); UnityEngine.Random.state = state;
        Check(seen.All(s => s.Count == 10), "all bank entries reachable in randomized pairs");
        Pass("256 randomized schedules: two distinct questions in each of five banks; all 50 entries reachable; original schedule restored");
    }
    static void Drag(ProofAltarOrderingBoard board, int from, int to)
    {
        var card = board.Panel.GetComponentsInChildren<ProofStepDragCard>().Single(c => c.name == "Draggable Step " + (from + 1));
        var point = RectTransformUtility.WorldToScreenPoint(null, board.Panel.TransformPoint(new Vector3(0, 200 - to * 105, 0)));
        var data = new PointerEventData(EventSystem.current) { position = point };
        ExecuteEvents.Execute(card.gameObject, data, ExecuteEvents.beginDragHandler);
        ExecuteEvents.Execute(card.gameObject, data, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(card.gameObject, data, ExecuteEvents.endDragHandler);
    }
    static void SetMobile(MobileInputUI mobile, Vector2 axis)
    {
        var rect = Field<RectTransform>(mobile, "joystickBackground");
        var point = rect.TransformPoint(new Vector3(axis.x * rect.rect.width * .5f, axis.y * rect.rect.height * .5f));
        mobile.OnDrag(new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, point) });
        if (axis == Vector2.zero) mobile.OnPointerUp(new PointerEventData(EventSystem.current));
    }
    static IEnumerator Shot(DirectProofsGuide guide, string name)
    {
        yield return new WaitForSecondsRealtime(1.15f); Canvas.ForceUpdateCanvases();
        // GameView rotation applies Canvas/SafeArea geometry at frame end. Allow
        // two complete layout updates even when shader loading makes a frame slow.
        yield return null; yield return null; yield return null; yield return null;
        var card = Field<RectTransform>(guide, "card"); var corners = new Vector3[4]; card.GetWorldCorners(corners);
        System.IO.Directory.CreateDirectory("Temp/DirectProofsQA"); Capture("Temp/DirectProofsQA/" + Screen.width + "-" + name + ".png");
        if (!corners.All(c => c.x >= -1 && c.x <= Screen.width + 1 && c.y >= -1 && c.y <= Screen.height + 1))
            System.IO.File.AppendAllText(Report, "LAYOUT " + name + " screen=" + Screen.width + "x" + Screen.height + " safe=" + guide.SafeArea.rect + " card=" + card.rect + " corners=" + string.Join(";", corners.Select(c => c.ToString())) + "\n");
        Check(corners.All(c => c.x >= -1 && c.x <= Screen.width + 1 && c.y >= -1 && c.y <= Screen.height + 1), "guide stays inside mobile screen: " + name);
        Check(Field<ScrollRect>(guide, "scroll").content != null, "long explanations remain scrollable");
        if(name!="Confirmation")
        {
            var reading=Field<TMP_Text>(guide,"body");
            Check(reading.rectTransform.rect.height >= reading.preferredHeight-2, "text content has its complete preferred height: "+name);
            var view=Field<ScrollRect>(guide,"scroll").viewport;
            if(reading.preferredHeight<=view.rect.height-2)
            {
                var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(view,reading.rectTransform);
                Check(bounds.min.y>=view.rect.yMin-1 && bounds.max.y<=view.rect.yMax+1,"short instruction fits completely without clipping: "+name);
            }
            if(name=="Help8" || name.EndsWith("Page1"))
            {
                var scrolling=Field<ScrollRect>(guide,"scroll");
                if(scrolling.content.rect.height>scrolling.viewport.rect.height+4)
                {
                    scrolling.verticalNormalizedPosition=0;yield return null;yield return null;
                    Check(scrolling.verticalNormalizedPosition<.05f,"long content can scroll to its end without layout resetting it");
                    scrolling.verticalNormalizedPosition=1;yield return null;
                }
            }
        }
        if (name == "Interaction")
        {
            var label = guide.PracticeObject.GetComponentsInChildren<TMP_Text>().Single(t => t.text == "Interact with me");
            var point = Camera.main.WorldToViewportPoint(label.GetComponent<Renderer>().bounds.center);
            Check(point.z > 0 && point.x > 0 && point.x < 1 && point.y > 0 && point.y < 1, "practice interaction label is inside the gameplay view");
            Check(Vector3.Dot(-guide.PracticeObject.transform.forward, (Camera.main.transform.position - label.transform.position).normalized) > .8f, "practice stand faces the guided camera");
        }
        var target = Field<RectTransform>(guide, "controlTarget");
        if (target != null && target.gameObject.activeInHierarchy)
        {
            var points = new Vector3[4]; target.GetWorldCorners(points);
            Check(points.All(p => p.x >= -1 && p.x <= Screen.width + 1 && p.y >= -1 && p.y <= Screen.height + 1), "actual highlighted control stays on mobile screen: " + name);
            var controlBounds = Rect.MinMaxRect(points[0].x, points[0].y, points[2].x, points[2].y);
            var cardBounds = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
            Check(!cardBounds.Overlaps(controlBounds), "instructions do not cover the highlighted control: " + name);
        }
        yield return null;
    }
    static IEnumerator GameplayShot(DirectIndirectProofStageController stage, int c, int q)
    {
        if (q != 1) yield break;
        yield return null; Capture("Temp/DirectProofsQA/" + Screen.width + "-GameplayAltar" + c + ".png"); yield return null;
        var corners = new Vector3[4]; stage.OrderingBoard.Panel.GetWorldCorners(corners);
        Check(corners.All(p => p.x >= -1 && p.x <= Screen.width + 1 && p.y >= -1 && p.y <= Screen.height + 1), "real ordering board fits mobile screen");
    }
    static void Capture(string path)
    {
        var type=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        var window=EditorWindow.GetWindow(type);
        var target=(RenderTexture)type.GetMethod("RenderView",flags).Invoke(window,new object[]{new Vector2(Screen.width,Screen.height),false});
        var previous=RenderTexture.active;RenderTexture.active=target;
        var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
        // GameView's render target is vertically inverted on this graphics backend.
        if(SystemInfo.graphicsUVStartsAtTop)
        {
            var pixels=image.GetPixels32();var flipped=new Color32[pixels.Length];
            for(int y=0;y<image.height;y++) Array.Copy(pixels,y*image.width,flipped,(image.height-1-y)*image.width,image.width);
            image.SetPixels32(flipped);image.Apply();
        }
        System.IO.File.WriteAllBytes(path,image.EncodeToPNG());UnityEngine.Object.Destroy(image);RenderTexture.active=previous;
    }
    static string Text(DirectProofsGuide guide) => Field<TMP_Text>(guide, "body").text;
    static void CheckReceivesPointer(Button button)
    {
        var rect = (RectTransform)button.transform;
        var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button, "button receives real pointer input above modal panels: " + button.name + "; hit=" + (hits.Count > 0 ? hits[0].gameObject.name : "none"));
    }
    static void Move(Player player, Vector3 point) { PrelimTeleportTransition.MovePlayerImmediately(player.gameObject, point, Quaternion.LookRotation(Vector3.back)); }
    static void Frozen(DirectIndirectProofStageController stage, float time) { Check(stage.State == DirectIndirectProofStageController.TrialState.WaitingForActivation && stage.SecondsLeft == time && stage.CorrectCount == 0 && stage.MistakeCount == 0, "tutorial never starts, penalizes or advances real gameplay"); }
    static void Click(Button button) { Check(button != null && button.gameObject.activeInHierarchy && button.interactable, "button available: " + button?.name); button.onClick.Invoke(); }
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Hidden).GetValue(target);
    static void Check(bool test, string message) { if (!test) throw new InvalidOperationException(message); }
    static void Pass(string text) { System.IO.File.AppendAllText(Report, "PASS " + text + "\n"); }
}
public sealed class DirectGuideQaRunner : MonoBehaviour { }
