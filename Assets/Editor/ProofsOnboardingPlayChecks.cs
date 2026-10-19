using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Opt-in, offline onboarding and all-five-challenge regression checks; no account writes.</summary>
public static class ProofsOnboardingPlayChecks
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void Begin(int size = 6, bool scrollsOnly = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name != "Formal vs. Informal Proofs")
            throw new InvalidOperationException("Open the proof scene outside Play Mode.");
        var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        SessionState.SetInt("Proofs.PreviousSize", (int)type.GetProperty("selectedSizeIndex", BindingFlags.Public | Hidden).GetValue(EditorWindow.GetWindow(type)));
        SessionState.SetInt("Proofs.CheckSize", size);
        SessionState.SetBool("Proofs.OnboardingQA", true);
        SessionState.SetBool("Proofs.ScrollsOnly", scrollsOnly);
        System.IO.File.WriteAllText("Library/ProofsOnboardingChecks.txt", "Proof library artwork, onboarding and five-challenge regression checks\n");
        EditorApplication.isPlaying = true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (!SessionState.GetBool("Proofs.OnboardingQA", false)) return;
        SessionState.SetBool("Proofs.PrefsCaptured", true);
        for (int c = 0; c < 5; c++)
        {
            string key = "LogicLegends.ProofsLibrary.LastVariant." + c;
            SessionState.SetBool("Proofs.PrefExists." + c, PlayerPrefs.HasKey(key));
            SessionState.SetInt("Proofs.PrefValue." + c, PlayerPrefs.GetInt(key, -1));
        }
        SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded;
    }
    [InitializeOnLoadMethod]
    static void RegisterRestore()
    {
        EditorApplication.playModeStateChanged -= RestorePrefs; EditorApplication.playModeStateChanged += RestorePrefs;
    }
    static void RestorePrefs(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        if (!SessionState.GetBool("Proofs.PrefsCaptured", false)) return;
        for (int c = 0; c < 5; c++)
        {
            string key = "LogicLegends.ProofsLibrary.LastVariant." + c;
            if (SessionState.GetBool("Proofs.PrefExists." + c, false)) PlayerPrefs.SetInt(key, SessionState.GetInt("Proofs.PrefValue." + c, -1));
            else PlayerPrefs.DeleteKey(key);
        }
        PlayerPrefs.Save(); SessionState.SetBool("Proofs.PrefsCaptured", false);
        SessionState.SetBool("Proofs.OnboardingQA", false);
        var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        type.GetProperty("selectedSizeIndex", BindingFlags.Public | Hidden).SetValue(EditorWindow.GetWindow(type), SessionState.GetInt("Proofs.PreviousSize", 6));
        Crystal.SafeArea.Sim = Crystal.SafeArea.SimDevice.None;
    }
    static void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Formal vs. Informal Proofs") return;
        SceneManager.sceneLoaded -= Loaded;
        var player = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Multiplayer_Male"));
        var sync = player.GetComponent<MultiplayerAvatarSync>(); if (sync != null) sync.enabled = false;
        player.GetComponent<PlayerEquipmentLoader>().enabled = false;
        foreach (var skin in player.GetComponentsInChildren<PlayerAnimator>(true)) skin.gameObject.SetActive(skin.name == "UNIFORM_MALE");
        player.GetComponent<CharacterController>().enabled = true;
        var spawn = GameObject.Find("Proofs Player Spawn"); if (spawn != null) player.transform.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);
        ProofsLibraryStageController.SuppressProgressionForValidation = true;
        // Verify roof cleanup even when a chamber uses separate ceiling renderers.
        var stage = UnityEngine.Object.FindFirstObjectByType<ProofsLibraryStageController>();
        foreach (bool visible in new[] { true, false })
        {
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = visible ? "QA Ceiling Visible" : "QA Ceiling Hidden";
            roof.transform.SetParent(stage.BookTarget.root, false);
            roof.transform.position = new Vector3(0, 20, 0); roof.transform.localScale = new Vector3(25, .1f, 25);
            roof.GetComponent<Collider>().enabled = false; roof.GetComponent<Renderer>().enabled = visible;
        }
        new GameObject("Proofs QA Runner").AddComponent<ProofsQaRunner>().StartCoroutine(Guard(Run()));
    }
    static IEnumerator Guard(IEnumerator run)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(run);
        while (stack.Count > 0)
        {
            object next;
            try { var step = stack.Peek(); if (!step.MoveNext()) { stack.Pop(); continue; } next = step.Current; }
            catch (Exception e)
            { System.IO.File.AppendAllText("Library/ProofsOnboardingChecks.txt", "FAIL " + e + "\n"); Debug.LogException(e); EditorApplication.isPlaying = false; yield break; }
            if (next is IEnumerator child) stack.Push(child); else yield return next;
        }
        System.IO.File.AppendAllText("Library/ProofsOnboardingChecks.txt", "ALL PROOF ARTWORK AND GAMEPLAY CHECKS PASSED\n");
        EditorApplication.isPlaying = false;
    }
    static IEnumerator Run()
    {
        int size = SessionState.GetInt("Proofs.CheckSize", 2);
        var type = Type.GetType("UnityEditor.GameView,UnityEditor"); type.GetProperty("selectedSizeIndex", BindingFlags.Public | Hidden).SetValue(EditorWindow.GetWindow(type), size, null);
        Crystal.SafeArea.Sim = size == 4 || size == 5 ? Crystal.SafeArea.SimDevice.iPhoneX : size == 12 ? Crystal.SafeArea.SimDevice.Pixel3XL_LSR : Crystal.SafeArea.SimDevice.None;
        float until = Time.unscaledTime + 45;
        ProofsStageUI ui = null;
        while (Time.unscaledTime < until)
        {
            ui = UnityEngine.Object.FindFirstObjectByType<ProofsStageUI>();
            if (ui != null && ui.Help != null && ui.Help.IsOpen) break;
            yield return null;
        }
        Check(ui != null && ui.Help.IsOpen && Time.timeScale == 0, "first-entry Help pauses input and time");
        var stage = ui.Stage; var help = ui.Help; float time = stage.SecondsLeft; string originalBoard = stage.BoardInstructions.text;
        CheckRandomization(stage);
        CheckArtwork(stage);
        Check(time == stage.StartingSeconds && !stage.Started && Field<ProofChallengeData>(stage, "activeChallenge") == null, "challenge waits before tutorial");
        var p = Player.LocalInstance;
        if (SessionState.GetBool("Proofs.ScrollsOnly", false))
        {
            help.CloseHelp(); stage.FinishOnboarding(); yield return Introduction(stage, false);
            goto RealGameplay;
        }
        string taught = "";
        for (int page = 0; page < help.PageCount; page++)
        {
            yield return null; yield return new WaitForEndOfFrame();
            var title = Field<TextMeshProUGUI>(help, "titleText"); var body = Field<TextMeshProUGUI>(help, "bodyText");
            title.ForceMeshUpdate(); body.ForceMeshUpdate(); taught += title.text + body.text;
            Check(!title.isTextOverflowing, "Help title fits mobile");
            if (page == 0) yield return Capture("Help");
            if (page < help.PageCount - 1) Click(Field<Button>(help, "nextButton"));
        }
        Check(taught.Contains("Informal does not mean incorrect") && taught.Contains("confirm") && taught.Contains("10:00") && taught.Contains("10 seconds") && taught.Contains("Challenge 5"), "Help describes actual proof kinds, timer and book confirmation");
        Click(Field<Button>(help, "beginButton")); yield return null;
        var tutorial = ProofsControlsOnboarding.Active; Check(tutorial != null && tutorial.CurrentStep == ProofsTutorialStep.Movement, "Let's Begin starts movement");
        var mobile = MobileInputUI.FindJoystick();
        SetMobile(mobile, new Vector2(.1f, 0)); yield return new WaitForSecondsRealtime(.9f);
        Check(tutorial.CurrentStep == ProofsTutorialStep.Movement, "joystick jitter does not advance");
        SetMobile(mobile, Vector2.right);
        until = Time.unscaledTime + 5;
        while (tutorial.CurrentStep == ProofsTutorialStep.Movement && Time.unscaledTime < until) yield return null;
        SetMobile(mobile, Vector2.zero);
        Check(tutorial.CurrentStep == ProofsTutorialStep.Interaction, "intentional movement advances only to hand interaction");
        yield return Layout(tutorial, "Hand");
        Check(tutorial.PracticeObject != null, "visible labeled practice object exists");
        var delta = tutorial.PracticeObject.transform.position - p.transform.position; delta.y = 0; p.transform.rotation = Quaternion.LookRotation(delta);
        var hand = GameObject.Find("Canvas 1").GetComponentsInChildren<Button>(true).Single(b => b.name == "InteractButton");
        Click(hand); yield return null;
        Check(tutorial.CurrentStep == ProofsTutorialStep.Jump && tutorial.PracticeObject == null, "hand practice advances only to jump");
        yield return Layout(tutorial, "Jump");
        until = Time.unscaledTime + 2;
        while (!p.GetComponent<CharacterController>().isGrounded && Time.unscaledTime < until) yield return null;
        var jump = GameObject.Find("Canvas 1").GetComponentsInChildren<Button>(true).Single(b => b.name == "JumpButton"); Click(jump);
        until = Time.unscaledTime + 3; while (tutorial.CurrentStep == ProofsTutorialStep.Jump && Time.unscaledTime < until) yield return null;
        Check(tutorial.CurrentStep == ProofsTutorialStep.Timer, "successful jump advances to timer");
        yield return Layout(tutorial, "Timer"); Next(tutorial); yield return null;
        Check(tutorial.CurrentStep == ProofsTutorialStep.Progress, "progress explained separately");
        yield return Layout(tutorial, "Progress"); Next(tutorial); yield return null;
        yield return Layout(tutorial, "Book"); Click(hand); yield return null;
        Check(tutorial.CurrentStep == ProofsTutorialStep.FormalAltar, "practice book interaction leaves real challenge waiting");
        yield return Layout(tutorial, "FormalAltar"); Next(tutorial); yield return null;
        yield return Layout(tutorial, "InformalAltar"); Next(tutorial); yield return null;
        yield return Layout(tutorial, "Scroll"); Next(tutorial); yield return null;
        yield return Layout(tutorial, "ReadScroll"); Click(hand); yield return null;
        Check(tutorial.CurrentStep == ProofsTutorialStep.Reader && stage.ReaderPanel.activeSelf, "hand opens actual reader with practice text");
        yield return Layout(tutorial, "Reader"); Click(stage.ReaderCloseButton); yield return null;
        Check(tutorial.CurrentStep == ProofsTutorialStep.ReadScroll, "Close does not collect practice scroll");
        yield return new WaitForSecondsRealtime(.75f); Click(ui.InteractionButton); yield return null;
        yield return Layout(tutorial, "ReaderCollect"); Click(stage.CollectButton); yield return null;
        Check(tutorial.PracticeCarried && stage.CarriedScroll == -1, "Carry Scroll affects only tutorial inventory");
        yield return Layout(tutorial, "Place"); Click(hand); yield return null;
        Check(tutorial.PracticePlacement == 1 && Enumerable.Range(0,4).All(i => stage.Placement(i) == -1), "practice placement leaves real altars empty");
        yield return Layout(tutorial, "TakeBack"); Click(ui.InteractionButton); yield return null;
        yield return Layout(tutorial, "TakeBackPicker"); Click(stage.TakeBackButtons[0]); yield return null;
        Check(tutorial.PracticeCarried && tutorial.PracticePlacement == -1, "existing take-back UI retrieves only practice scroll");
        yield return Layout(tutorial, "Replace"); Click(hand); yield return null;
        yield return Layout(tutorial, "ConfirmBook"); Click(hand); yield return null;
        Check(tutorial.CurrentStep == ProofsTutorialStep.ViewBoard, "practice confirmation never evaluates real arrangement");
        yield return Layout(tutorial, "ViewBoard"); Click(ui.ViewButton); yield return new WaitForSecondsRealtime(.95f);
        Check(ui.BoardView.Viewing && tutorial.CurrentStep == ProofsTutorialStep.Board && !ui.BackButton.interactable, "View Board blends and requires explanation before Back");
        yield return Layout(tutorial, "Board"); CheckBoardFrame(ui); Next(tutorial); yield return null;
        yield return Layout(tutorial, "BoardBack"); Click(ui.BackButton); yield return new WaitForSecondsRealtime(.95f);
        Check(tutorial.CurrentStep == ProofsTutorialStep.Confirm && !ui.BoardView.Viewing, "Back restores camera and reaches confirmation");
        CheckFrozen(stage, time); Click(tutorial.NoButton); yield return null;
        Check(tutorial.CurrentStep == ProofsTutorialStep.Movement, "No repeats tutorial from movement");
        var skip = ui.SafeArea.GetComponentsInChildren<Button>(true).Single(b => b.name == "PRELIM Shared Skip Tutorial");
        Click(skip); yield return null; Click(tutorial.NoButton); yield return null;
        // Interrupt both a guided camera and the board's own coroutine.
        Invoke(tutorial, "StartCamera");
        Invoke(tutorial, "ShowStep", ProofsTutorialStep.ViewBoard, "Practice View Board", (RectTransform)ui.ViewButton.transform, false, null);
        yield return new WaitForSecondsRealtime(.75f); Click(ui.ViewButton); Click(skip); yield return null;
        Check(!ui.BoardView.Viewing && !ui.BoardView.Transitioning && tutorial.CurrentStep == ProofsTutorialStep.Confirm, "Skip cancels camera transition");
        Click(tutorial.YesButton); yield return null; yield return null;
        Check(ProofsControlsOnboarding.Active == null && !stage.OnboardingPending, "Yes prepares challenge introduction and book gate");
        CheckFrozen(stage, time); Check(stage.BoardInstructions.text == originalBoard && GameObject.Find("Proofs Tutorial Practice Content") == null && GameObject.Find("Proofs Practice Object") == null, "all practice content removed");
        yield return Introduction(stage, false);
        Check(p.enabled && !GameInput.Instance.GameplayInputBlocked, "introduction restores controls and prepares book gate");
        RealGameplay:
        Click(help.HelpButton); yield return null; Check(help.IsOpen, "Help reopens"); Click(Field<Button>(help,"beginButton")); yield return null;
        Check(!help.IsOpen && ProofsControlsOnboarding.Active == null && !stage.Started, "reopened guide resumes without repeating or activating");
        yield return WalkChamber(stage, p);
        yield return null; yield return new WaitForEndOfFrame();
        Click(ui.ViewButton); yield return new WaitForSecondsRealtime(.95f); CheckBoardFrame(ui);
        Check(!stage.Started && stage.SecondsLeft == time, "ordinary board view does not start or submit"); Click(ui.BackButton); yield return new WaitForSecondsRealtime(.95f);
        for (int challenge = 1; challenge <= 5; challenge++)
        {
          if (challenge > 1) yield return Introduction(stage, challenge == 2 && size != 2);
          ProofChallengeData previousQuestion = null;
          for (int question = 1; question <= 2; question++)
          {
            Move(p, stage.BookTarget.position + Vector3.back * 2); yield return null;
            stage.InteractNearby(); yield return null;
            Check(stage.Started && stage.ChallengeNumber == challenge && stage.QuestionNumber == question, "book starts real challenge/question " + challenge + "/" + question);
            var q = Field<ProofChallengeData>(stage, "activeChallenge");
            Check(q != previousQuestion, "two distinct questions in challenge " + challenge); previousQuestion = q;
            if (q.kind == ProofChallengeKind.SortScrolls)
            {
                CheckArrows(stage);
                float beforeReading = stage.SecondsLeft;
                stage.ReadScroll(0); yield return new WaitForSecondsRealtime(.15f);
                Check(stage.ReaderPanel.activeSelf && stage.ReaderBody.text == q.scrolls[0].content, "real reader restored after tutorial");
                yield return ReaderLayout(stage, "RealReader" + challenge + "Q" + question);
                Check(stage.SecondsLeft < beforeReading, "scroll reading preserves the active countdown");
                if (challenge == 1 && question == 1)
                {
                    var original = stage.ReaderBody.text;
                    stage.ReaderBody.text = string.Join("\n\n", Enumerable.Repeat(original, 12)); ui.RefreshReader(true); yield return null;
                    var reader = stage.ReaderPanel.GetComponentInChildren<ScrollRect>(true);
                    Check(reader.content.rect.height > reader.viewport.rect.height, "long proof uses scrolling rather than shrinking or truncating");
                    reader.verticalNormalizedPosition = 0; yield return null; yield return Capture("LongProofBottom");
                    Click(stage.ReaderCloseButton); yield return null;
                    Check(stage.CarriedScroll == -1 && stage.ReadingScroll == -1 && !stage.ReaderPanel.activeSelf && p.enabled && !GameInput.Instance.GameplayInputBlocked, "Back closes without collecting and restores gameplay");
                    // Snapshot restoration also respects controls that were already suspended by a caller.
                    GameInput.Instance.SetGameplayInputBlocked(true); p.ToggleControl(false);
                    stage.ReadScroll(0); stage.CloseReader();
                    Check(GameInput.Instance.GameplayInputBlocked && !p.enabled, "reader restores previous blocked/control state");
                    GameInput.Instance.SetGameplayInputBlocked(false); p.ToggleControl(true);
                    stage.ReadScroll(0); yield return null;
                    Check(stage.ReaderBody.text == original && Mathf.Abs(reader.content.anchoredPosition.y) < .1f, "reopening restores exact data and scrolls to top");
                }
                Click(stage.CollectButton); yield return null; Check(stage.CarriedScroll == 0, "real collect still works");
                stage.CarryReadScroll(); stage.ReadScroll(0);
                Check(stage.CarriedScroll == 0 && !stage.ReaderPanel.activeSelf, "duplicate collect/read input cannot collect the scroll twice");
                CheckArrows(stage); yield return Capture("CarryArrows" + challenge + "Q" + question);
                if (challenge == 1 && question == 1)
                {
                    var guide = stage.GetComponent<ProofsPedestalIndicators>();
                    float low = guide.Arrows[0].position.y, high = low;
                    for (int sample = 0; sample < 4; sample++)
                    {
                        yield return new WaitForSecondsRealtime(.13f);
                        low = Mathf.Min(low, guide.Arrows[0].position.y); high = Mathf.Max(high, guide.Arrows[0].position.y);
                    }
                    Check(high - low > .005f && high - low <= .241f, "yellow placement arrows bob subtly");
                    help.OpenHelp(); yield return null; CheckArrows(stage);
                    Check(stage.CarriedScroll == 0 && guide.Arrows.All(a => !a.gameObject.activeSelf), "Help hides placement arrows without changing inventory");
                    help.CloseHelp(); yield return null; CheckArrows(stage);
                    Check(guide.Arrows.All(a => a.gameObject.activeSelf), "closing Help restores both neutral empty-altar arrows");
                }
                Vector3 sourcePosition = stage.ScrollTargets[0].transform.position;
                Click(ui.ReturnScrollButton); yield return null;
                Check(!stage.HasCarriedScroll && stage.ScrollTargets[0].TabletVisible && stage.ScrollTargets[0].transform.position == sourcePosition && !ui.ReturnScrollButton.gameObject.activeSelf, "Return restores classification scroll to its exact source");
                stage.ReturnScroll(); CheckArrows(stage);
                stage.ReadScroll(0); Click(stage.CollectButton); yield return null; Check(stage.CarriedScroll == 0, "returned classification scroll remains readable and collectible");
                stage.PlaceOnPedestal(q.scrolls[0].isFormal ? 0 : 1);
                CheckArrows(stage);
                for (int i = 1; i < q.scrolls.Length; i++)
                {
                    stage.ReadScroll(i); Click(stage.CollectButton); CheckArrows(stage);
                    stage.PlaceOnPedestal(q.scrolls[i].isFormal ? 0 : 1); CheckArrows(stage);
                }
                if (challenge == 1 && question == 1)
                {
                    int correct = q.scrolls[0].isFormal ? 0 : 1; stage.RetrieveFromPedestal(correct); stage.TakeBackScroll(0); stage.PlaceOnPedestal(1-correct);
                    int[] before = Enumerable.Range(0,4).Select(stage.Placement).ToArray(); float beforeTime = stage.SecondsLeft;
                    stage.Submit(); Check(Mathf.Abs(beforeTime - stage.SecondsLeft - stage.WrongPenalty) < .001f && before.SequenceEqual(Enumerable.Range(0,4).Select(stage.Placement)), "incorrect submission preserves placements and penalty");
                    yield return new WaitForSecondsRealtime(.85f); stage.RetrieveFromPedestal(1-correct); stage.TakeBackScroll(0); CheckArrows(stage); stage.PlaceOnPedestal(correct); CheckArrows(stage);
                    Click(help.HelpButton); yield return null; float paused = stage.SecondsLeft; yield return new WaitForSecondsRealtime(.25f);
                    Check(help.IsOpen && stage.Started && stage.SecondsLeft == paused, "Help pauses active challenge without clearing it"); Click(Field<Button>(help,"beginButton")); yield return null;
                    float readingStart = stage.SecondsLeft; Click(ui.ViewButton); yield return new WaitForSecondsRealtime(.95f);
                    Check(stage.Started && stage.SecondsLeft < readingStart, "board viewing preserves active countdown rules"); Click(ui.BackButton); yield return new WaitForSecondsRealtime(.95f);
                }
            }
            else yield return Arrange(stage, q);
            stage.Submit(); yield return new WaitForSecondsRealtime(.95f);
            Check(!stage.Started && stage.CorrectCount == (challenge - 1) * 2 + question, "correct answer pauses and progresses " + challenge + "/" + question);
            Check(Enumerable.Range(0, 4).All(i => stage.Placement(i) == -1) && stage.CarriedScroll == -1 && stage.ReadingScroll == -1 && !stage.WorldBoard.IsConfigured, "question reset removes previous answers");
            if (question == 1)
            {
                Check(stage.ChallengeNumber == challenge && stage.QuestionNumber == 2 && !stage.Finished && !stage.Introductions.IsOpen, "first answer stays in challenge without repeating its introduction");
                float waitingTime = stage.SecondsLeft; yield return new WaitForSecondsRealtime(.2f);
                Check(stage.SecondsLeft == waitingTime && !stage.Started, "question 2 waits for book with paused timer");
            }
          }
        }
        Check(stage.CorrectCount == 10 && stage.CompletedChallenges == 5, "all ten questions required for five-challenge completion");
        Check(stage.Finished && stage.Presentation.Progress.text.Contains("5 of 5"), "existing stage results remain connected");
        CheckArrows(stage);
        System.IO.File.AppendAllText("Library/ProofsOnboardingChecks.txt", "PASS centered parchment reader, full data, long scrolling, Back, exact control restoration, duplicate guards, neutral empty-altar arrows, placements/retrieval and all ten validations\n");
        Debug.Log("PROOFS_ONBOARDING_PLAY_PASS " + Screen.width + "x" + Screen.height + " Help / movement-hand-jump / isolated reader and take-back / skip-restart / camera-Back / shared timer / all five validations");
    }
    static void CheckArrows(ProofsLibraryStageController stage)
    {
        var guide = stage.GetComponent<ProofsPedestalIndicators>(); Check(guide != null && guide.Arrows.Length == 2, "one placement arrow per proof altar");
        guide.Refresh();
        for (int i = 0; i < 2; i++)
        {
            bool expected = stage.Started && !stage.Finished && stage.CarriedScroll >= 0 && !stage.InputSuppressed && !stage.HasReaderOpen && stage.IsPedestalEmpty(i);
            Check(guide.Arrows[i].gameObject.activeSelf == expected, "arrow follows carrying/occupancy, independent of answer classification");
            if (expected)
            {
                var yellow = guide.Arrows[i].Find("Yellow Arrow").GetComponent<Renderer>();
                Color c = yellow.sharedMaterial.GetColor("_BaseColor"); Check(c.r > .9f && c.g > .7f && c.b < .2f, "placement arrow is yellow");
                var corners = new Vector3[4]; float labelTop = 0;
                foreach (var label in stage.AltarTargets[i].GetComponentsInChildren<TMP_Text>())
                { label.rectTransform.GetWorldCorners(corners); foreach (var v in corners) labelTop = Mathf.Max(labelTop, v.y); }
                Check(yellow.bounds.min.y > labelTop, "arrow clears altar labels");
            }
        }
        var board = stage.WorldBoard;
        foreach (var group in new[] { "statementSlots", "ruleSlots" })
        {
            var slots = Field<ProofWorldPedestal[]>(board, group);
            var assignments = Field<int[]>(board, group == "ruleSlots" ? "assignedRules" : "assignedSteps");
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                bool expected = stage.Started && !stage.Finished && board.HasCarriedScroll && !stage.InputSuppressed && !stage.HasReaderOpen &&
                    slot.gameObject.activeInHierarchy && board.CarryingRule == (group == "ruleSlots") && i < assignments.Length && assignments[i] < 0;
                Check(guide.SlotArrows.ContainsKey(slot) && guide.SlotArrows[slot].gameObject.activeSelf == expected, "all compatible empty proof slots have neutral arrows");
                if (expected) Check(Vector3.Dot(guide.SlotArrows[slot].position - slot.transform.position, slot.transform.right) > 0, "slot arrow stays on its right side");
            }
        }
    }
    static IEnumerator ReaderLayout(ProofsLibraryStageController stage, string name)
    {
        var panel = (RectTransform)stage.ReaderPanel.transform; var safe = stage.Presentation.SafeArea;
        Vector2 center = safe.InverseTransformPoint(panel.TransformPoint(panel.rect.center));
        Check(Vector2.Distance(center, safe.rect.center) < 1, "scroll panel centered in safe area");
        Check(GameInput.Instance.GameplayInputBlocked && !Player.LocalInstance.enabled && Cursor.visible, "reader blocks gameplay and unlocks cursor");
        Check(stage.CollectButton.GetComponentInChildren<TMP_Text>().text == "Get This Scroll" && stage.ReaderCloseButton.GetComponentInChildren<TMP_Text>().text == "Back", "reader uses requested action labels");
        foreach (var button in new[] { stage.CollectButton, stage.ReaderCloseButton })
        {
            Check(Mathf.Abs(button.image.pixelsPerUnitMultiplier - 4.5f) < .01f, "reader buttons follow artwork multiplier");
            var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
            var rect = (RectTransform)button.transform;
            pointer.position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var hits = new List<UnityEngine.EventSystems.RaycastResult>(); UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer, hits);
            Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button, "reader button receives UI raycasts: " + button.name);
        }
        var corners = new Vector3[4]; panel.GetWorldCorners(corners);
        Check(corners.All(c => c.x >= 0 && c.y >= 0 && c.x <= Screen.width && c.y <= Screen.height), "reader inside mobile screen");
        stage.ReaderTitle.ForceMeshUpdate(); Check(!stage.ReaderTitle.isTextOverflowing, "full scroll title wraps inside panel");
        yield return Capture(name);
    }
    static IEnumerator Introduction(ProofsLibraryStageController stage, bool skip)
    {
        var intro = stage.Introductions;
        float until = Time.unscaledTime + 4;
        while (!intro.IsOpen && Time.unscaledTime < until) yield return null;
        Check(intro.IsOpen && Time.timeScale == 0 && !Player.LocalInstance.enabled && GameInput.Instance.GameplayInputBlocked, "challenge introduction pauses gameplay and input");
        if (stage.ChallengeNumber == 3)
        {
            // Interrupt before the first blend finishes, then ensure a fresh introduction can safely resume.
            intro.enabled = false;
            Check(!intro.IsOpen && !intro.HasSeen(3) && Time.timeScale == 1 && Player.LocalInstance.enabled && !GameInput.Instance.GameplayInputBlocked,
                "interrupted introduction restores state without marking it complete");
            Check(GameObject.Find("QA Ceiling Visible").GetComponent<Renderer>().enabled, "interruption restores ceiling");
            yield return null; intro.enabled = true; yield return null;
            until = Time.unscaledTime + 4; while (!intro.IsOpen && Time.unscaledTime < until) yield return null;
            Check(intro.IsOpen, "interrupted introduction safely resumes before book activation");
        }
        float seconds = stage.SecondsLeft; int count = stage.CorrectCount;
        string schedule = string.Join(";", Field<int[][]>(stage, "selectedVariants").Select(a => string.Join(",", a)));
        Check(Field<int[][]>(stage, "selectedVariants").All(a => a.Length == 2 && a.Distinct().Count() == 2), "all banks have exactly two distinct selected questions");
        Check(!GameObject.Find("QA Ceiling Visible").GetComponent<Renderer>().enabled && !GameObject.Find("QA Ceiling Hidden").GetComponent<Renderer>().enabled, "overhead view hides ceiling without enabling hidden parts");
        Check(stage.BookTarget.root.Find("Induction Chamber").GetComponent<Renderer>().enabled, "ceiling filter preserves chamber floor and walls");
        stage.Activate(); stage.Submit(); stage.InteractNearby();
        Check(!stage.Started && stage.CorrectCount == count, "introduction blocks real book submission and progress");
        for (int page = 0; page < intro.PageCount; page++)
        {
            until = Time.unscaledTime + 6;
            while (!intro.NextButton.interactable && Time.unscaledTime < until) yield return null;
            Check(intro.NextButton.interactable, "camera frames introduction target " + stage.ChallengeNumber + "/" + page);
            var header = Field<TMP_Text>(intro, "heading"); header.ForceMeshUpdate(); Check(!header.isTextOverflowing, "challenge title fits mobile");
            var card = Field<RectTransform>(intro, "card"); var corners = new Vector3[4]; card.GetWorldCorners(corners);
            Check(corners.All(c => c.x >= -1 && c.y >= -1 && c.x <= Screen.width + 1 && c.y <= Screen.height + 1), "challenge card inside screen");
            Check(UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Count(b => b.name == "PRELIM Shared Skip Tutorial") == 1, "one shared introduction Skip");
            Check(Vector3.Dot(Camera.main.transform.forward, Vector3.down) > .98f, "bird's-eye view faces the chamber");
            foreach (var label in UnityEngine.Object.FindObjectsByType<TextMeshPro>(FindObjectsSortMode.None).Where(t => t.name.StartsWith("Practice location ")))
            {
                var labelCorners = new Vector3[4]; label.rectTransform.GetWorldCorners(labelCorners);
                Check(labelCorners.Select(Camera.main.WorldToViewportPoint).All(v => v.z > 0 && v.x >= 0 && v.x <= 1 && v.y >= 0 && v.y <= 1), "active location label fits camera: " + label.text);
            }
            yield return Capture("Challenge" + stage.ChallengeNumber + "Intro" + page);
            if (skip)
            {
                // Skip while a fresh page starts moving the camera, rather than waiting for it to settle.
                intro.Next(); Click(uiSkip(stage)); yield return null; break;
            }
            intro.Next(); yield return null;
        }
        Check(!intro.IsOpen && intro.HasSeen(stage.ChallengeNumber), "introduction shown once and cleaned up");
        Check(GameObject.Find("QA Ceiling Visible").GetComponent<Renderer>().enabled && !GameObject.Find("QA Ceiling Hidden").GetComponent<Renderer>().enabled, "ceiling renderer states restored exactly");
        Check(stage.SecondsLeft == seconds && stage.CorrectCount == count && !stage.Started && stage.WaitingForBook, "introduction leaves timer and challenge waiting for book");
        Check(string.Join(";", Field<int[][]>(stage, "selectedVariants").Select(a => string.Join(",", a))) == schedule, "tutorial does not redraw selected questions");
        Check(GameObject.Find("Proofs Challenge PRACTICE Examples") == null && GameObject.Find("Proofs Challenge Introduction Camera") == null, "practice and transition removed");
        Check(Player.LocalInstance.enabled && !GameInput.Instance.GameplayInputBlocked && Time.timeScale == 1, "controls and timer scale restored");
        System.IO.File.AppendAllText("Library/ProofsOnboardingChecks.txt", "PASS challenge " + stage.ChallengeNumber + " introduction: overhead targets, one Skip, pause/isolation, ceiling states, camera/input cleanup" + (skip ? " (skipped during transition)" : " (all Next pages)") + "\n");
        // Keep the synthetic test roof from hiding gameplay in subsequent checks.
        GameObject.Find("QA Ceiling Visible").transform.position = new Vector3(0, 200, 0);
    }
    static Button uiSkip(ProofsLibraryStageController stage) => stage.Presentation.SafeArea.GetComponentsInChildren<Button>().Single(b => b.name == "PRELIM Shared Skip Tutorial");
    static void CheckRandomization(ProofsLibraryStageController stage)
    {
        var state = UnityEngine.Random.state;
        var schedule = Field<int[][]>(stage, "selectedVariants"); var original = schedule.Select(a => a.ToArray()).ToArray();
        var seen = Enumerable.Range(0, 5).Select(c => new HashSet<int>()).ToArray();
        try
        {
            for (int run = 0; run < 256; run++)
            {
                Invoke(stage, "SelectPlaythroughQuestions");
                for (int c = 0; c < 5; c++)
                {
                    Check(schedule[c].Length == 2 && schedule[c][0] != schedule[c][1] && schedule[c].All(i => i >= 0 && i < 10), "distinct valid questions in randomized run");
                    foreach (int id in schedule[c]) seen[c].Add(id);
                }
            }
            Check(seen.All(bank => bank.Count == 10), "all ten existing bank entries remain available across new runs");
        }
        finally { for (int c = 0; c < 5; c++) schedule[c] = original[c]; UnityEngine.Random.state = state; }
        System.IO.File.AppendAllText("Library/ProofsOnboardingChecks.txt", "PASS 256 randomized schedules: two distinct questions in all five banks; every bank entry reachable; original live schedule restored\n");
    }
    static void CheckArtwork(ProofsLibraryStageController stage)
    {
        var root = GameObject.Find("ProofsLibrary").transform;
        Check(root.Find("Library Architecture") == null && root.Find("Induction Chamber") != null, "old architecture replaced by Induction chamber");
        var floor = root.Find("Induction Chamber").GetComponent<MeshCollider>();
        Check(floor != null && floor.enabled && floor.sharedMesh != null, "visible chamber has solid mesh collision");
        foreach (var point in new[] { new Vector3(0, .4f, -11.6f), new Vector3(0, .4f, -4), new Vector3(-3.5f, .4f, 3.5f), new Vector3(3.5f, .4f, 3.5f), new Vector3(0, .4f, 10) })
        {
            RaycastHit hit;
            Check(Physics.Raycast(point, Vector3.down, out hit, 1, ~0, QueryTriggerInteraction.Ignore) && hit.collider == floor && Mathf.Abs(hit.point.y) < .025f, "ground matches visible floor at " + point);
        }
        Check(root.Find("Garden Bookshelves").GetComponentsInChildren<MeshRenderer>().Length >= 4, "existing decorative bookshelves retained");
        Check(Mathf.Abs(stage.BookTarget.localScale.x - .825f) < .001f && stage.BoardTarget.localScale == Vector3.one * 1.5f && stage.AltarTargets.All(t => t.localScale == Vector3.one * 1.5f), "main assets enlarged exactly 1.5 times");
        var decorations = root.Find("ChamberDecorations");
        Check(decorations != null && decorations.GetComponentsInChildren<Light>().Count(l => l.enabled && l.intensity > 0 && l.range > 0) == 8, "organized decorations have eight working practical lights");
        Check(decorations.GetComponentsInChildren<Collider>().All(c => !c.enabled), "overhead and wall decorations do not obstruct navigation");
        Check(stage.BoardTarget.Find("Induction Information Board Model/Info_GoldFrame") != null && stage.BoardInstructions.transform.IsChildOf(stage.BoardTarget), "original board text connected to replacement frame");
        foreach (var renderer in root.Find("Induction Chamber").GetComponentsInChildren<Renderer>().Concat(root.Find("Garden Bookshelves").GetComponentsInChildren<Renderer>()).Concat(stage.BoardTarget.GetComponentsInChildren<Renderer>()))
            foreach (var material in renderer.sharedMaterials) Check(material != null && material.shader != null && !material.shader.name.Contains("InternalError"), "replacement materials resolve");
        System.IO.File.AppendAllText("Library/ProofsOnboardingChecks.txt", "PASS enlarged assets, materials, preserved shelves, original board text/reference, eight decorative lights, and visible ground at spawn/altar/board approaches\n");
    }
    static IEnumerator WalkChamber(ProofsLibraryStageController stage, Player player)
    {
        var original = player.transform.position; var rotation = player.transform.rotation;
        var input = GameInput.Instance; bool blocked = input.GameplayInputBlocked, enabled = player.enabled;
        float time = stage.SecondsLeft;
        player.ToggleControl(false); input.SetGameplayInputBlocked(true);
        Move(player, new Vector3(0, 0, -11.6f)); var controller = player.GetComponent<CharacterController>();
        try
        {
            foreach (var point in new[] { new Vector2(0,-11.1f), new Vector2(3,-11.1f), new Vector2(3,-6), new Vector2(0,-4), new Vector2(-3,0),
                new Vector2(-5.8f,1.9f), new Vector2(-3,0), new Vector2(-7.2f,-3.3f), new Vector2(-7.2f,.2f), new Vector2(-3,0),
                new Vector2(0,-4), new Vector2(7.2f,-3.3f), new Vector2(7.2f,.2f), new Vector2(5.8f,1.9f), new Vector2(3,1.5f),
                new Vector2(0,7), new Vector2(0,10), new Vector2(0,7), new Vector2(3,-6), new Vector2(3,-11.1f), new Vector2(0,-11.6f) })
            {
                float deadline = Time.unscaledTime + 6;
                while (Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z), point) > .14f && Time.unscaledTime < deadline)
                {
                    var delta = new Vector3(point.x-player.transform.position.x, 0, point.y-player.transform.position.z);
                    var step = Vector3.ClampMagnitude(delta, player.moveSpeed * Time.unscaledDeltaTime);
                    controller.Move(step + Vector3.down * 12 * Time.unscaledDeltaTime); yield return null;
                }
                Check(Vector2.Distance(new Vector2(player.transform.position.x,player.transform.position.z), point) <= .14f, "character can walk to " + point);
                Check(player.transform.position.y > -.3f && player.transform.position.y < 1.2f, "route stays grounded at " + point);
            }
            Check(stage.SecondsLeft == time && !stage.Started && stage.CorrectCount == 0, "navigation checks preserve book gate, timer and progress");
            System.IO.File.AppendAllText("Library/ProofsOnboardingChecks.txt", "PASS actual CharacterController walk: spawn, around book, both altars, all four scroll approaches, board, and return; no blocked paths or progress changes\n");
        }
        finally
        {
            PrelimTeleportTransition.MovePlayerImmediately(player.gameObject, original, rotation);
            player.ToggleControl(enabled); input.SetGameplayInputBlocked(blocked);
        }
    }
    static IEnumerator Arrange(ProofsLibraryStageController stage, ProofChallengeData q)
    {
        var board = stage.WorldBoard;
        var options = Field<ProofWorldPedestal[]>(board, "statementTablets"); var rules = Field<ProofWorldPedestal[]>(board, "ruleTablets");
        var slots = Field<ProofWorldPedestal[]>(board, "statementSlots"); var ruleSlots = Field<ProofWorldPedestal[]>(board, "ruleSlots");
        var stepIds = Field<int[]>(board, "optionToStep"); var ruleIds = Field<int[]>(board, "optionToRule");
        // Runtime replacements must use the same routing as the original configured instances.
        if (stage.QuestionNumber == 1)
        {
            var sources = q.kind == ProofChallengeKind.MissingRule ? rules : options;
            var old = sources[0]; var spawned = UnityEngine.Object.Instantiate(old, old.transform.parent);
            spawned.name = old.name + " QA Runtime Spawn"; spawned.transform.SetPositionAndRotation(old.transform.position, old.transform.rotation);
            sources[0] = spawned; old.gameObject.SetActive(false); spawned.gameObject.SetActive(true);
            var targets = q.kind == ProofChallengeKind.MissingRule ? ruleSlots : slots;
            var oldSlot = targets[0]; var spawnedSlot = UnityEngine.Object.Instantiate(oldSlot, oldSlot.transform.parent);
            spawnedSlot.name = oldSlot.name + " QA Runtime Spawn"; spawnedSlot.transform.SetPositionAndRotation(oldSlot.transform.position, oldSlot.transform.rotation);
            targets[0] = spawnedSlot; oldSlot.gameObject.SetActive(false); spawnedSlot.gameObject.SetActive(true);
            stage.GetComponent<ProofsPedestalIndicators>().Refresh();
            Check(!stage.GetComponent<ProofsPedestalIndicators>().SlotArrows.ContainsKey(oldSlot), "runtime slot replaces old indicator without duplication");
            yield return null;
        }
        var sourcesToRead = options.Concat(rules).Where(s => s.gameObject.activeInHierarchy).ToArray();
        int mistakes = stage.MistakeCount;
        foreach (var source in sourcesToRead)
        {
            string content = Field<TMP_Text>(source, "detail").text;
            Vector3 origin = source.transform.position;
            Approach(source.transform); yield return null;
            Click(stage.Presentation.InteractionButton); yield return null;
            Check(stage.ReaderPanel.activeSelf && stage.ReaderBody.text == content && stage.ReaderTitle.text == source.Heading && !stage.HasCarriedScroll, "every selectable source opens its exact full reader before collecting: " + source.name + " expected=" + source.Heading + " actual=" + stage.ReaderTitle.text);
            if (source == sourcesToRead[0]) yield return ReaderLayout(stage, "WorldReader" + stage.ChallengeNumber + "Q" + stage.QuestionNumber);
            Click(stage.ReaderCloseButton); yield return null;
            Check(!stage.HasCarriedScroll && source.TabletVisible, "Back leaves world source available and unselected");
            yield return GetWorldScroll(stage, source);
            Check(!source.TabletVisible && stage.PracticeScrollModel.activeSelf, "collected world scroll has exactly one carried visual");
            Click(stage.Presentation.ReturnScrollButton); yield return null; stage.ReturnScroll();
            Check(!stage.HasCarriedScroll && source.TabletVisible && source.transform.position == origin && !stage.PracticeScrollModel.activeSelf, "Return restores the same world source and clears the proxy");
            Check(!stage.Presentation.ReturnScrollButton.gameObject.activeSelf, "Return hides when empty-handed"); CheckArrows(stage);
        }
        Check(stage.MistakeCount == mistakes && Field<int[]>(board, "assignedSteps").All(i => i == -1) && Field<int[]>(board, "assignedRules").All(i => i == -1), "read Back Get Return never grades or changes proof placements");
        if (q.kind == ProofChallengeKind.MissingRule)
        {
            int id = Array.IndexOf(q.ruleOptions, q.expectedRule); var source = rules[Array.IndexOf(ruleIds, id)];
            yield return GetWorldScroll(stage, source); yield return PlaceWorldScroll(stage, ruleSlots[0]);
            yield return RemoveAndReplace(stage, source, ruleSlots[0]);
        }
        else for (int i = 0; i < q.steps.Length; i++)
        {
            int id = q.acceptableOrders[0].stepIds[i]; var source = options[Array.IndexOf(stepIds, id)];
            yield return GetWorldScroll(stage, source);
            if (i == 1)
            {
                int[] before = Field<int[]>(board, "assignedSteps").ToArray();
                Click(stage.Presentation.ReturnScrollButton); yield return null;
                Check(before.SequenceEqual(Field<int[]>(board, "assignedSteps")), "returning a world scroll preserves earlier proof steps");
                yield return GetWorldScroll(stage, source);
            }
            if (i == 0) yield return Capture("SlotArrows" + stage.ChallengeNumber + "Q" + stage.QuestionNumber);
            yield return PlaceWorldScroll(stage, slots[i]);
            if (i == 0) yield return RemoveAndReplace(stage, source, slots[i]);
            if (q.kind == ProofChallengeKind.Formalize)
            {
                yield return GetWorldScroll(stage, rules[Array.IndexOf(ruleIds, id)]);
                yield return PlaceWorldScroll(stage, ruleSlots[i]);
            }
        }
        Check(board.IsComplete() && board.IsCorrect(), "actual world-board validator accepts authored solution");
        System.IO.File.AppendAllText("Library/ProofsOnboardingChecks.txt", "PASS challenge " + stage.ChallengeNumber + " question " + stage.QuestionNumber + ": all statement/rule readers, Back/Get/Return, original source identity, runtime sources/slots, neutral empty-slot arrows, removal/recollection and actual validation\n");
    }
    static IEnumerator GetWorldScroll(ProofsLibraryStageController stage, ProofWorldPedestal source)
    {
        Approach(source.transform); yield return null;
        var hand = GameObject.Find("Canvas 1").GetComponentsInChildren<Button>(true).Single(b => b.name == "InteractButton");
        Click(hand); yield return null;
        Check(stage.ReaderPanel.activeSelf && !stage.HasCarriedScroll, "hand opens full world reader without collecting");
        Click(stage.CollectButton); yield return null;
        Check(stage.WorldBoard.HasCarriedScroll && stage.HasCarriedScroll && !stage.ReaderPanel.activeSelf, "Get This Scroll acquires exactly the chosen world option");
        stage.CarryReadScroll(); Check(stage.WorldBoard.HasCarriedScroll, "duplicate Get cannot acquire an extra world scroll");
        CheckArrows(stage);
        Check(stage.Presentation.ReturnScrollButton.gameObject.activeInHierarchy, "Return is available while carrying each world scroll");
        Canvas.ForceUpdateCanvases(); // Newly re-enabled actions need their first canvas geometry before synthetic hit tests.
        yield return new WaitForEndOfFrame();
        CheckActionSpacing(stage);
        var returnRect = (RectTransform)stage.Presentation.ReturnScrollButton.transform;
        var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, returnRect.TransformPoint(returnRect.rect.center)) };
        var hits = new List<UnityEngine.EventSystems.RaycastResult>(); UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer, hits);
        if (hits.Count == 0 || hits[0].gameObject.GetComponentInParent<Button>() != stage.Presentation.ReturnScrollButton)
        {
            var image = stage.Presentation.ReturnScrollButton.image;
            System.IO.File.AppendAllText("Library/ProofsOnboardingChecks.txt", "RETURN DIAGNOSTIC point=" + pointer.position + " rect=" + returnRect.rect + " pos=" + returnRect.position + " hitInside=" + RectTransformUtility.RectangleContainsScreenPoint(returnRect, pointer.position, null) + " imageEnabled=" + image.enabled + " raycast=" + image.raycastTarget + " depth=" + image.depth + " cull=" + image.canvasRenderer.cull + " canvas=" + image.canvas.name + " order=" + image.canvas.sortingOrder + " scale=" + returnRect.lossyScale + "\n" + string.Join("\n", hits.Select(h => h.gameObject.name + " order=" + h.sortingOrder + " depth=" + h.depth + " layer=" + h.sortingLayer)) + "\n");
        }
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == stage.Presentation.ReturnScrollButton, "Return receives touch input without control overlap; hit=" + (hits.Count > 0 ? hits[0].gameObject.name : "none"));
    }
    static IEnumerator PlaceWorldScroll(ProofsLibraryStageController stage, ProofWorldPedestal slot)
    {
        Approach(slot.transform); yield return null;
        if (stage.QuestionNumber == 1 && slot.Heading.EndsWith("1")) yield return CaptureSlotFace(stage, slot);
        Click(stage.Presentation.InteractionButton); yield return null;
        Check(!stage.HasCarriedScroll, "hand places world scroll using existing slot assignment"); CheckArrows(stage);
    }
    static IEnumerator RemoveAndReplace(ProofsLibraryStageController stage, ProofWorldPedestal source, ProofWorldPedestal slot)
    {
        stage.InteractNearby(); yield return null;
        Check(source.TabletVisible && !stage.HasCarriedScroll, "removing a filled slot restores its original readable source");
        yield return GetWorldScroll(stage, source);
        Check(stage.GetComponent<ProofsPedestalIndicators>().SlotArrows[slot].gameObject.activeSelf, "removed slot becomes arrow-eligible while carrying");
        yield return PlaceWorldScroll(stage, slot);
    }
    static void Approach(Transform station)
    {
        // Approach its reading face, outside the base collider, rather than teleporting into its side.
        Move(Player.LocalInstance, station.position - station.forward * 1.5f);
        Player.LocalInstance.transform.rotation = station.rotation;
    }
    static IEnumerator CaptureSlotFace(ProofsLibraryStageController stage, ProofWorldPedestal slot)
    {
        // Inspect a reading-face view as if looking toward this station, without changing gameplay camera settings.
        var camera = Camera.main; var position = camera.transform.position; var rotation = camera.transform.rotation; float fov = camera.fieldOfView;
        var paused = new List<Behaviour>(); var avatar = new Dictionary<Renderer, bool>();
        foreach (var brain in UnityEngine.Object.FindObjectsByType<Unity.Cinemachine.CinemachineBrain>(FindObjectsSortMode.None))
            if (brain.enabled) { brain.enabled = false; paused.Add(brain); }
        foreach (var controller in UnityEngine.Object.FindObjectsByType<ThirdPersonCameraController>(FindObjectsSortMode.None))
            if (controller.enabled) { controller.enabled = false; paused.Add(controller); }
        foreach (var renderer in Player.LocalInstance.GetComponentsInChildren<Renderer>(true)) { avatar[renderer] = renderer.enabled; renderer.enabled = false; }
        try
        {
            var tablet = slot.transform.Find("Reading Tablet").GetComponent<Renderer>().bounds;
            Vector3 center = tablet.center + slot.transform.right * .35f;
            camera.transform.position = center - slot.transform.forward * 3.4f + Vector3.up * .5f;
            camera.transform.rotation = Quaternion.LookRotation(center - Vector3.up * .7f - camera.transform.position);
            camera.fieldOfView = camera.aspect < 1 ? 90 : 75;
            yield return null; yield return new WaitForEndOfFrame(); CheckArrows(stage);
            CheckActionSpacing(stage);
            var arrow = stage.GetComponent<ProofsPedestalIndicators>().SlotArrows[slot];
            Vector3 projected = camera.WorldToViewportPoint(arrow.position);
            Check(arrow.gameObject.activeSelf && projected.z > 0 && projected.x > 0 && projected.x < 1 && projected.y > 0 && projected.y < 1, "right-side slot arrow visible from its reading face");
            yield return Capture("PlacementView" + stage.ChallengeNumber + (stage.WorldBoard.CarryingRule ? "Rule" : "Step"));
        }
        finally
        {
            camera.transform.SetPositionAndRotation(position, rotation); camera.fieldOfView = fov;
            foreach (var renderer in avatar) if (renderer.Key != null) renderer.Key.enabled = renderer.Value;
            foreach (var behaviour in paused) if (behaviour != null) behaviour.enabled = true;
        }
    }
    static void CheckActionSpacing(ProofsLibraryStageController stage)
    {
        var controls = GameObject.Find("Canvas 1").transform.Find("SafeArea/Gameplay_Interface");
        var buttons = new[] { stage.Presentation.ViewButton, stage.Presentation.InteractionButton, stage.Presentation.ReturnScrollButton }
            .Where(b => b.gameObject.activeInHierarchy).ToArray();
        foreach (var button in buttons)
        {
            var bounds = ScreenRect((RectTransform)button.transform);
            foreach (var name in new[] { "Joystick", "InteractButton", "JumpButton" })
            {
                var control = (RectTransform)controls.Find(name);
                if (control != null && control.gameObject.activeInHierarchy)
                    Check(!bounds.Overlaps(ScreenRect(control)), button.name + " stays clear of " + name);
            }
            foreach (var other in buttons.Where(b => b != button))
                Check(!bounds.Overlaps(ScreenRect((RectTransform)other.transform)), "action buttons do not overlap");
        }
    }
    static Rect ScreenRect(RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        var min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
        var max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
    static IEnumerator Layout(ProofsControlsOnboarding t, string name)
    {
        yield return new WaitForSecondsRealtime(.85f); yield return new WaitForEndOfFrame();
        if (name == "Book" || name == "FormalAltar")
        {
            var target = name == "Book" ? Field<ProofsLibraryStageController>(t, "stage").BookTarget : Field<ProofsLibraryStageController>(t, "stage").AltarTargets[0];
            var details = "FRAME " + name + " camera=" + Camera.main.transform.position + " goal=" + Field<Vector3>(t, "goalPosition") + "\n";
            System.IO.File.AppendAllText("Library/ProofsOnboardingChecks.txt", details);
            Check(Vector3.Distance(Camera.main.transform.position, target.position) < 40, "guided camera stays close to " + name);
            var delta = target.GetComponent<Collider>().bounds.center - Camera.main.transform.position;
            var hits = Physics.RaycastAll(Camera.main.transform.position, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => h.collider.GetComponentInParent<Player>() == null).OrderBy(h => h.distance).ToArray();
            Check(hits.Length > 0 && hits[0].collider.transform.IsChildOf(target), "guided camera has clear view of " + name + (hits.Length > 0 ? " first=" + hits[0].collider.name : " no hit"));
        }
        var text = Field<TMP_Text>(t,"instruction"); text.ForceMeshUpdate();
        Check(!text.isTextOverflowing, "instructions fit: " + name);
        var panel = Field<RectTransform>(t,"instructions"); var corners = new Vector3[4]; panel.GetWorldCorners(corners);
        Check(corners.All(c=>c.x>=-1 && c.y>=-1 && c.x<=Screen.width+1 && c.y<=Screen.height+1), "instructions stay on mobile screen: " + name);
        Check(UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Count(b=>b.name=="PRELIM Shared Skip Tutorial") == 1, "one shared Skip control");
        yield return Capture(name);
    }
    static IEnumerator Capture(string name)
    {
        System.IO.Directory.CreateDirectory("Temp/ProofsOnboardingQA"); ScreenCapture.CaptureScreenshot("Temp/ProofsOnboardingQA/"+Screen.width+"-"+name+".png"); yield return null; yield return new WaitForEndOfFrame();
    }
    static void CheckBoardFrame(ProofsStageUI ui)
    {
        var b=ui.BoardView.BoardBounds; var camera=Camera.main;
        for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)
        { var point=camera.WorldToViewportPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,0))); Check(point.z>0 && point.x>=0 && point.x<=1 && point.y>=0 && point.y<=1,"complete board framed on mobile"); }
        Vector3 direction = b.center - camera.transform.position;
        var hits = Physics.RaycastAll(camera.transform.position, direction.normalized, direction.magnitude, ~0, QueryTriggerInteraction.Ignore)
            .Where(h => h.collider.GetComponentInParent<Player>() == null).OrderBy(h => h.distance).ToArray();
        Check(hits.Length > 0 && hits[0].collider.transform.IsChildOf(ui.Stage.BoardTarget), "board view unobstructed" + (hits.Length == 0 ? " (no board hit)" : " (first hit: " + hits[0].collider.name + ")"));
    }
    static void CheckFrozen(ProofsLibraryStageController stage,float time) => Check(!stage.Started && stage.SecondsLeft==time && stage.ChallengeNumber==1 && stage.CorrectCount==0 && stage.MistakeCount==0 && stage.CarriedScroll==-1 && Field<ProofChallengeData>(stage,"activeChallenge")==null && Enumerable.Range(0,4).All(i=>stage.Placement(i)==-1), "practice never changes real game state");
    static void SetMobile(MobileInputUI ui,Vector2 v){typeof(MobileInputUI).GetField("inputVector",Hidden).SetValue(ui,v); GameInput.Instance.SetMobileMovement(v);}
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Hidden).GetValue(o);
    static void Invoke(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Hidden).Invoke(o,args);
    static void Next(ProofsControlsOnboarding t)=>Click(Field<Button>(t,"next"));
    static void Click(Button button){Check(button!=null && button.gameObject.activeInHierarchy && button.interactable,"button available: "+(button!=null?button.name:"null"));button.onClick.Invoke();}
    static void Check(bool okay,string message){if(!okay)throw new InvalidOperationException("PROOFS_QA: "+message);}
    static void Move(Player p,Vector3 point){var cc=p.GetComponent<CharacterController>();cc.enabled=false;point.y=.9f;p.transform.SetPositionAndRotation(point,Quaternion.LookRotation(Vector3.forward));cc.enabled=true;Physics.SyncTransforms();}
}
public sealed class ProofsQaRunner:MonoBehaviour{}
