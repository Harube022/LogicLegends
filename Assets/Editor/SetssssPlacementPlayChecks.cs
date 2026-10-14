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

/// <summary>Opt-in Play Mode checks using the scene's hand, blocks, floor, and Submit.</summary>
public static class SetssssPlacementPlayChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (!SessionState.GetBool("Sets.RunChecks", false)) return;
        SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded;
    }

    [InitializeOnLoadMethod]
    static void Hook()
    {
        EditorApplication.playModeStateChanged -= Restore; EditorApplication.playModeStateChanged += Restore;
    }

    static void Restore(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool("Sets.RunChecks", false)) return;
        SessionState.SetBool("Sets.RunChecks", false);
        Crystal.SafeArea.Sim = Crystal.SafeArea.SimDevice.None;
    }

    static void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Setssss") return;
        SceneManager.sceneLoaded -= Loaded;
        // Use the existing avatar before LevelSpawner.Start; do not spawn via an account or award progress.
        var avatar = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Multiplayer_Male"));
        var sync = avatar.GetComponent<MultiplayerAvatarSync>();
        if (sync != null) sync.enabled = false;
        avatar.GetComponent<CharacterController>().enabled = true;
        avatar.name = "Sets QA Local Avatar";
        var stage = UnityEngine.Object.FindFirstObjectByType<SetssssChallengeOne>();
        // Completion layout is tested without invoking account/reward callbacks.
        Set(stage, "onChallengeCompleted", new UnityEngine.Events.UnityEvent());
        var book = Field<Transform>(stage, "bookStatue");
        PrelimTeleportTransition.MovePlayerImmediately(avatar, book.position + Vector3.back * 2f, Quaternion.identity);
        avatar.GetComponent<Player>().enabled = true;
        new GameObject("Sets QA Runner").AddComponent<SetssssPlayCheckRunner>().StartCoroutine(Run(stage));
    }

    static IEnumerator Run(SetssssChallengeOne stage)
    {
        var view = Type.GetType("UnityEditor.GameView,UnityEditor");
        var window = EditorWindow.GetWindow(view);
        int size = SessionState.GetInt("Sets.CheckSize", 2);
        view.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(window, size, null);
        window.Focus();
        Crystal.SafeArea.Sim = size == 12 ? Crystal.SafeArea.SimDevice.Pixel3XL_LSR :
            size == 4 ? Crystal.SafeArea.SimDevice.iPhoneX : Crystal.SafeArea.SimDevice.None;
        yield return new WaitForSecondsRealtime(.6f);
        var help = UnityEngine.Object.FindFirstObjectByType<SetssssHelpUI>();
        yield return CheckHelpPages(help);
        help.CloseHelp();
        var player = Player.LocalInstance;
        player.enabled = true;
        yield return null;
        Check(stage.TimeRemaining == 1080f && !stage.Started, "help close does not start challenge");
        CheckQuestionSampling(stage);
        var canvas = GameObject.Find("Canvas 1").GetComponent<Canvas>();
        var hand = canvas.GetComponentsInChildren<Button>(true).Single(b => b.name == "InteractButton");
        var pickup = Field<Button>(stage, "pickUpBlockButton");
        var submit = Field<Button>(stage, "submitButton");
        var book = Field<Transform>(stage, "bookStatue");
        var notice = stage.GetComponent<SetssssChallengePresentation>();
        var a = Field<Bounds>(stage, "aCircleBounds");
        var b = Field<Bounds>(stage, "bCircleBounds");
        var differenceA = Field<Bounds>(stage, "aMinusBBounds");
        var differenceB = Field<Bounds>(stage, "bMinusABounds");
        Vector3 intersection = (a.center + b.center) * .5f;
        yield return CheckHudTutorial(stage, canvas);
        CheckHud(stage, canvas);
        yield return Capture("SpawnHud");
        if (SessionState.GetBool("Sets.HudOnly", false))
        {
            yield return CheckCompactHud(stage, canvas, player, book, hand);
            yield break;
        }

        // Normal book activation + a real natural drop in Challenge 1.
        yield return StartBook(stage, player, book, hand);
        Check(stage.Started && stage.ChallengeNumber == 1, "hand starts Challenge 1");
        Check(NoticeText(stage) == "Start!" && NoticeVisible(stage), "shared PRELIM Start notification");
        CheckHud(stage, canvas);
        yield return Capture("StartNotification");
        yield return new WaitForSecondsRealtime(.7f);
        Move(player, book.position + Vector3.back * 2f, Quaternion.identity);
        GameInput.Instance.MobileInteract();
        yield return new WaitForSecondsRealtime(.9f);
        Check(!NoticeVisible(stage), "Start hides after PRELIM's 1.5 seconds and active book does not replay it");
        var blocks = Field<GrabbableObject[]>(stage, "numberedBlocks");
        int requiredValue = int.Parse(stage.ActiveCondition.setA.Concat(stage.ActiveCondition.setB).First());
        var required = blocks.Single(x => Value(x) == requiredValue);
        yield return Pickup(stage, player, required, pickup);
        Vector3 firstPoint = stage.ActiveCondition.setA.Length > 0 ? a.center : b.center;
        yield return Drop(stage, player, required, firstPoint, hand);
        Check(!required.GetComponent<Rigidbody>().isKinematic && required.GetComponent<Rigidbody>().useGravity, "drop uses physics");
        yield return Settle(stage);
        // Natural tumbling moves the bounds center, especially on these
        // imported blocks with offset pivots. Verify the released block stays
        // in its intended area; Drop already checks that it never teleports.
        Check(Vector3.Distance(XZ(required.GetComponent<Collider>().bounds.center), XZ(firstPoint)) < 1.5f, "natural drop settles near the requested position");
        yield return TumbleLabel(required);
        yield return Settle(stage);
        yield return Submit(stage, player, submit, a.center);
        Check(Field<bool>(stage, "advancing"), "Challenge 1 actual-position submission accepted");
        Check(required.GetComponent<SetssssNumberedBlock>().PlacementCorrectness == true, "successful submission colors placed number green");
        yield return new WaitForSecondsRealtime(1.4f);
        Check(required.GetComponent<SetssssNumberedBlock>().PlacementCorrectness == null, "next condition resets number feedback");
        CheckHud(stage, canvas);
        Check(Field<TextMeshPro>(stage, "boardText").text.Contains("Question 2 of 5"), "question counter remains on board and follows correct answer");

        // Preserve all existing challenge gates, without writing final-stage rewards.
        yield return CompleteQuestions(stage);
        Check(stage.ChallengeNumber == 2 && !stage.Started, "Challenge 2 waits for book");
        Check(NoticeText(stage) == "Challenge 1 completed! Interact with the book statue to start Challenge 2.", "continue notification uses exact completed/next numbers");
        yield return Capture("ContinueNotification");
        yield return new WaitForSecondsRealtime(1.8f);
        Check(NoticeVisible(stage) && stage.TimeRemaining == 1080f, "continue notice persists with paused waiting timer");
        yield return StartBook(stage, player, book, hand);
        yield return CompleteQuestions(stage);
        yield return StartBook(stage, player, book, hand);
        yield return CompleteQuestions(stage);
        Check(stage.ChallengeNumber == 4 && !stage.Started, "Challenge 4 waits before explanation");

        bool playerWasEnabled = player.enabled;
        yield return StartBook(stage, player, book, hand);
        float pausedTime = stage.TimeRemaining;
        Check(notice.IsIntroductionOpen && !stage.Started && stage.ActiveCondition == null, "explanation precedes Challenge 4 draw and countdown");
        Check(Time.timeScale == 0f && GameInput.Instance.GameplayInputBlocked && !player.enabled, "modal pauses timer and gameplay input");
        GameInput.Instance.MobileInteract(); stage.Submit(); stage.DeductTime(10f);
        yield return new WaitForSecondsRealtime(.4f);
        Check(stage.TimeRemaining == pausedTime && !stage.Started, "modal rejects gameplay, deduction, and repeated activation");
        Check(GameObject.Find("A-B") != null && GameObject.Find("B-A") != null, "new difference regions visible for explanation");
        Check(canvas.GetComponentsInChildren<Button>(true).Count(x => x.name == "Continue Challenge 4") == 1, "single Continue button");
        yield return null; yield return new WaitForEndOfFrame();
        CheckMobileUI(notice, canvas);
        yield return Capture("Challenge4Introduction");
        var scroll = Field<ScrollRect>(notice, "scroll"); scroll.verticalNormalizedPosition = 0f;
        yield return null; yield return Capture("Challenge4Example");
        Click(notice.ContinueButton); yield return null;
        Check(stage.Started && Time.timeScale == 1f && !GameInput.Instance.GameplayInputBlocked && player.enabled == playerWasEnabled, "Continue restores previous controls and starts once");
        float began = stage.TimeRemaining;
        notice.Continue(); Check(stage.TimeRemaining == began, "duplicate Continue does not reset timer");
        yield return new WaitForSecondsRealtime(1.5f);

        // Three generations from the same spawner slot, using real hand/context input.
        blocks = Field<GrabbableObject[]>(stage, "numberedBlocks");
        requiredValue = int.Parse(stage.ActiveCondition.setA.Intersect(stage.ActiveCondition.setB).First());
        int slot = Array.FindIndex(blocks, x => Value(x) == requiredValue);
        var copies = new List<GrabbableObject>();
        var targets = new[] { a.center, b.center, intersection };
        for (int i = 0; i < 3; i++)
        {
            var available = Field<GrabbableObject[]>(stage, "challengeFourAvailableAtSlot");
            var source = available[slot]; Vector3 spawn = source.transform.position;
            yield return Pickup(stage, player, source, i % 2 == 0 ? hand : pickup);
            var successor = available[slot];
            Check(successor != source && Value(successor) == requiredValue, "each replacement pickup creates same-number successor");
            Check(Vector3.Distance(successor.transform.position, spawn) < .01f && successor.GetComponent<Collider>().enabled, "replacement immediately returns to exact spawn point");
            Check(successor.GetComponent<SetssssNumberedBlock>().NumberLabel != source.GetComponent<SetssssNumberedBlock>().NumberLabel, "replacement has independent number label");
            yield return Drop(stage, player, source, targets[i], hand);
            copies.Add(source);
            yield return new WaitForSecondsRealtime(.3f);
            Check(Vector3.Distance(successor.transform.position, spawn) < .01f, "replacement never follows inherited hold point");
        }
        yield return Settle(stage);
        int copiesBefore = Field<HashSet<GrabbableObject>>(stage, "challengeFourCopies").Count;
        yield return Pickup(stage, player, copies[0], hand);
        Check(Field<HashSet<GrabbableObject>>(stage, "challengeFourCopies").Count == copiesBefore, "placed block pickup creates no duplicate");
        yield return Drop(stage, player, copies[0], a.center + Vector3.back * 4f, hand);
        yield return Settle(stage);

        // Fill the remaining requirements using separate physical copies. This
        // exercises other existing grabs through the stage's Update observation.
        yield return FillArrangement(stage, player, a, b, differenceA, differenceB, intersection);
        yield return Settle(stage);
        var arranged = Field<HashSet<GrabbableObject>>(stage, "releasedBlocks").ToArray();
        Check(ReviewCorrect(stage), "five regions are fully satisfiable with spawner copies");
        copies[2].gameObject.SetActive(false);
        Check(!ReviewCorrect(stage), "A/B copies cannot also count as the missing intersection copy");
        copies[2].gameObject.SetActive(true);
        var victim = copies[0];
        Vector3 correctPoint = victim.transform.position;
        var body = victim.GetComponent<Rigidbody>();
        Vector3 outside = a.center + Vector3.left * (a.extents.x + 2f); outside.y = correctPoint.y;
        body.position = outside; body.Sleep(); Physics.SyncTransforms();
        Check(!ReviewCorrect(stage), "moving a recorded block outside changes validation");
        var retained = arranged.ToDictionary(x => x, x => x.transform.position);
        float beforeWrong = stage.TimeRemaining;
        float beforeWrongAt = Time.time;
        yield return Submit(stage, player, submit, a.center);
        float penalty = beforeWrong - stage.TimeRemaining - (Time.time - beforeWrongAt);
        Check(Mathf.Abs(penalty - 10f) < .15f, "incorrect answer applies exactly existing 10-second penalty");
        foreach (var pair in retained) Check(Vector3.Distance(pair.Value, pair.Key.transform.position) < .06f, "wrong submission retains every physical placement");
        var marks = Field<Dictionary<GrabbableObject, LineRenderer>>(stage, "errorIndicators");
        Check(marks.Count(x => x.Value.gameObject.activeSelf) == 1 && marks.ContainsKey(victim), "only incorrect block is marked");
        foreach (var block in arranged)
        {
            var number = block.GetComponent<SetssssNumberedBlock>();
            Check(number.PlacementCorrectness == (block != victim), "mixed submission grades each placed number independently");
            Check(number.NumberLabel.color == (block == victim ? SetssssNumberedBlock.IncorrectColor : SetssssNumberedBlock.CorrectColor), "mixed submission uses red/green number colors");
            CheckLabel(block);
        }
        yield return Capture("MixedLabelFeedback");
        var amounts = canvas.GetComponentsInChildren<TMP_Text>().Where(x => x.name == "Amount").ToArray();
        Check(amounts.Any(x => x.text == "−10s"), "actual deduction appears beside timer");
        stage.DeductTime(3f); stage.DeductTime(2.5f); yield return null;
        Check(amounts.Length <= 3 && canvas.GetComponentsInChildren<TMP_Text>().Count(x => x.name == "Amount") == 3, "repeated penalties use separate readable rows");
        yield return Capture("Deductions");
        var timer = Field<TMP_Text>(stage, "timerText").rectTransform;
        var rows = Field<CanvasGroup[]>(notice, "deductionGroups");
        foreach (var row in rows)
        {
            Check(!ScreenRect((RectTransform)row.transform).Overlaps(ScreenRect(timer)), "deduction row avoids timer");
            foreach (var other in rows)
                if (other != row) Check(!ScreenRect((RectTransform)row.transform).Overlaps(ScreenRect((RectTransform)other.transform)), "repeated deductions do not overlap");
        }
        yield return new WaitForSecondsRealtime(1.4f);
        Check(Field<CanvasGroup[]>(notice, "deductionGroups").Where(x => x.gameObject.activeSelf).All(x => x.alpha < 1f), "deductions fade on unscaled time");
        yield return new WaitForSecondsRealtime(.5f);
        Check(!canvas.GetComponentsInChildren<TMP_Text>().Any(x => x.name == "Amount"), "deductions automatically disappear");

        // Correct just that block. The untouched placements still satisfy their regions.
        body.position = correctPoint; body.Sleep(); Physics.SyncTransforms();
        yield return null; yield return new WaitForEndOfFrame();
        var victimNumber = victim.GetComponent<SetssssNumberedBlock>();
        Check(victimNumber.PlacementCorrectness == null && victimNumber.NumberLabel.color == victimNumber.DefaultLabelColor, "moving a graded block resets only its number");
        Check(arranged.Where(x => x != victim).All(x => x.GetComponent<SetssssNumberedBlock>().PlacementCorrectness == true), "untouched correct numbers retain green feedback");
        yield return Pickup(stage, player, victim, hand);
        Check(victimNumber.PlacementCorrectness == null && victimNumber.NumberLabel.color == victimNumber.DefaultLabelColor, "hand pickup resets number to authored default");
        yield return Drop(stage, player, victim, a.center + Vector3.back * 4f, hand);
        yield return Settle(stage);
        Check(ReviewCorrect(stage), "correcting one wrong block restores complete answer");
        yield return Submit(stage, player, submit, a.center);
        Check(Field<bool>(stage, "advancing"), "Submit advances corrected full arrangement");
        Check(arranged.All(x => x.GetComponent<SetssssNumberedBlock>().PlacementCorrectness == true), "resubmission reevaluates every label as green");
        yield return new WaitForSecondsRealtime(1.4f);
        Check(stage.ConditionIndex == 1 && Field<HashSet<GrabbableObject>>(stage, "challengeFourCopies").Count == 0, "next condition clears copies without stacked spawner blocks");

        // Every remaining randomized Challenge 4 condition can be filled. None
        // is satisfied by using one block in multiple regions.
        for (int condition = 1; condition < stage.QuestionCount; condition++)
        {
            yield return FillArrangement(stage, player, a, b, differenceA, differenceB, intersection);
            yield return Settle(stage);
            Check(ReviewCorrect(stage), "all Challenge 4 conditions accept complete physical arrangements");
            yield return Submit(stage, player, submit, a.center);
            if (condition < stage.QuestionCount - 1) yield return new WaitForSecondsRealtime(1.35f); else yield return null;
        }
        Check(stage.ChallengeNumber == 5 && !stage.Started && stage.TimeRemaining == 1080f, "Challenge 5 remains gated by book after Challenge 4");
        Check(GameObject.Find("A-B") == null && GameObject.Find("B-A") == null, "separate regions hidden after Challenge 4");
        yield return StartBook(stage, player, book, hand);
        Check(stage.Started && stage.ChallengeNumber == 5, "complement still uses existing start flow");
        yield return null;
        CheckHud(stage, canvas);
        Check(Field<TextMeshPro>(stage, "boardText").text.Contains("Question 1 of 5") && stage.CurrentProblem.StartsWith("U = {"), "complement board preserves actual universe and five-question total");
        yield return CompleteQuestions(stage);
        Check(stage.Complete && GameObject.Find("Setssss Stage Complete") != null, "five questions complete the final stage with existing results");
        UnityEngine.Object.Destroy(GameObject.Find("Setssss Stage Complete"));
        stage.RestartChallenge(); yield return null;
        Check(!stage.Started && stage.ChallengeNumber == 1 && Field<HashSet<GrabbableObject>>(stage, "releasedBlocks").Count == 0, "restart clears physical answers and timer notifications");
        // Test interrupted explanation cleanup without starting a challenge.
        Set(stage, "challengeNumber", 4);
        yield return StartBook(stage, player, book, hand);
        Check(notice.IsIntroductionOpen, "new attempt gets explanation");
        stage.enabled = false; yield return null;
        Check(!notice.IsIntroductionOpen && Time.timeScale == 1f && !GameInput.Instance.GameplayInputBlocked && !stage.Started, "interruption restores state without starting challenge");
        stage.enabled = true; stage.RestartChallenge();
        Debug.Log("SETSSSS_PLACEMENT_PLAY_CHECKS_PASS " + Screen.width + "x" + Screen.height + " timer/challenge-only HUD, two tutorial targets, five distinct questions for all challenges, final results, timing, tumbling labels, replenishment, all five difference questions, and mobile UI");
    }

    static IEnumerator CheckHelpPages(SetssssHelpUI help)
    {
        Check(Field<bool>(help, "open") && Time.timeScale == 0f, "auto Help pauses initial gameplay");
        var body = Field<TextMeshProUGUI>(help, "bodyText");
        var next = Field<Button>(help, "nextButton");
        var previous = Field<Button>(help, "previousButton");
        var begin = Field<Button>(help, "beginButton");
        var seen = new System.Text.StringBuilder();
        int count = ((Array)help.GetType().GetField("Pages", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null)).Length;
        for (int page = 0; page < count; page++)
        {
            yield return null; yield return new WaitForEndOfFrame();
            seen.Append(body.text);
            Check(!System.Text.RegularExpressions.Regex.IsMatch(body.text, "\\bconditions?\\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase), "Help consistently says question");
            Check(!ScreenRect((RectTransform)next.transform).Overlaps(ScreenRect((RectTransform)begin.transform)), "Help buttons do not overlap");
            if (page == 0) yield return Capture("HelpElements");
            if (page < count - 1) Click(next);
        }
        Check(seen.ToString().Contains("five distinct questions") && seen.ToString().Contains("elements of a set") && seen.ToString().Contains("complement"), "Help teaches question count and correct set terminology");
        Click(previous); yield return null; Click(next); yield return null;
    }

    static void CheckQuestionSampling(SetssssChallengeOne stage)
    {
        var savedRandom = UnityEngine.Random.state;
        int savedChallenge = stage.ChallengeNumber;
        var pool = Field<List<int>>(stage, "unansweredEntries");
        int[] savedPool = pool.ToArray();
        string[] banks = { "challengeOneBank", "challengeTwoBank", "challengeThreeBank", "challengeFourBank", "challengeFiveBank" };
        try
        {
            for (int challenge = 1; challenge <= 5; challenge++)
            {
                Set(stage, "challengeNumber", challenge);
                int bankSize = ((Array)typeof(SetssssChallengeOne).GetField(banks[challenge - 1], BindingFlags.NonPublic | BindingFlags.Static).GetValue(null)).Length;
                var covered = new HashSet<int>(); var orders = new HashSet<string>();
                for (int attempt = 0; attempt < 100; attempt++)
                {
                    Invoke(stage, "ShuffleUnansweredEntries");
                    Check(stage.QuestionCount == 5 && pool.Count == 5 && pool.Distinct().Count() == 5 && pool.All(x => x >= 0 && x < bankSize), "five distinct questions sampled within existing bank");
                    covered.UnionWith(pool); orders.Add(string.Join(",", pool));
                }
                Check(covered.Count == bankSize && orders.Count > 1, "whole bank remains eligible with randomized selections/order");
            }
        }
        finally
        {
            Set(stage, "challengeNumber", savedChallenge); pool.Clear(); pool.AddRange(savedPool);
            UnityEngine.Random.state = savedRandom;
        }
    }

    static IEnumerator CompleteQuestions(SetssssChallengeOne stage)
    {
        int challenge = stage.ChallengeNumber;
        var seen = new HashSet<int>(stage.SelectedEntryIndices.Take(stage.QuestionIndex));
        for (int question = stage.QuestionIndex; question < stage.QuestionCount; question++)
        {
            Check(stage.Started && stage.ChallengeNumber == challenge && stage.QuestionIndex == question, "challenge stays active through question " + (question + 1));
            Check(seen.Add(stage.SelectedEntryIndices[question]), "no repeated question in one challenge");
            Check(Field<TextMeshPro>(stage, "boardText").text.Contains("Question " + (question + 1) + " of 5"), "Information Board tracks all five questions");
            Check(stage.DebugClearCurrentCondition(), "existing correct-answer progression accepts question");
            float until = Time.unscaledTime + 10f;
            while (stage.ChallengeNumber == challenge && stage.QuestionIndex == question && !stage.Complete && Time.unscaledTime < until)
                yield return null;
            yield return null;
        }
        Check(seen.Count == 5, "challenge completes after exactly five distinct questions");
        Check(challenge == 5 ? stage.Complete : stage.ChallengeNumber == challenge + 1 && !stage.Started && stage.TimeRemaining == 1080f,
            "fifth question reaches final results or waits at next book with reset countdown");
    }

    static IEnumerator CheckCompactHud(SetssssChallengeOne stage, Canvas canvas, Player player, Transform book, Button hand)
    {
        var hud = stage.Hud;
        float normalHeight = hud.Hud.rect.height;
        Check(hud.Hud.rect.width <= 560f && hud.Hud.rect.width * normalHeight < 740f * 320f * .6f, "HUD occupies substantially less screen space");
        Check(hud.Hud.GetComponentsInChildren<TMP_Text>().Length == 2 && hud.Hud.rect.width <= 440f && normalHeight <= 76f, "compact HUD contains only time and challenge progress");

        var safe = Field<RectTransform>(hud, "safeArea");
        var maps = Field<RectTransform[]>(hud, "minimaps");
        var map = StageUITheme.Rect("Compact HUD QA Minimap", safe);
        map.anchorMin = map.anchorMax = map.pivot = Vector2.one;
        map.anchoredPosition = new Vector2(-16f, -16f); map.sizeDelta = new Vector2(280f, 220f);
        Set(hud, "minimaps", new[] { map });
        yield return null; yield return new WaitForEndOfFrame();
        CheckHud(stage, canvas);
        Check(!ScreenRect(hud.Hud).Overlaps(ScreenRect(map)), "compact HUD avoids a top-right minimap");
        Set(hud, "minimaps", maps); UnityEngine.Object.Destroy(map.gameObject);
        yield return null;

        yield return StartBook(stage, player, book, hand);
        Check(stage.Started && NoticeText(stage) == "Start!", "compact HUD preserves book start and notification");
        CheckHud(stage, canvas); yield return Capture("CompactStart");
        float before = stage.TimeRemaining;
        stage.DeductTime(10f); stage.DeductTime(3f); stage.DeductTime(2.5f);
        yield return null; yield return new WaitForEndOfFrame();
        Check(before - stage.TimeRemaining >= 15.5f && before - stage.TimeRemaining < 16f, "HUD reflow preserves actual deductions");
        var rows = Field<CanvasGroup[]>(stage.GetComponent<SetssssChallengePresentation>(), "deductionGroups");
        Check(hud.Hud.rect.height > normalHeight, "deduction row gets space only while visible");
        foreach (var row in rows)
        {
            Rect rect = ScreenRect((RectTransform)row.transform);
            Check(!rect.Overlaps(ScreenRect(hud.Timer.rectTransform)) && !rect.Overlaps(ScreenRect(hud.Challenge.rectTransform)), "deductions avoid timer and challenge text");
            foreach (var other in rows) if (other != row) Check(!rect.Overlaps(ScreenRect((RectTransform)other.transform)), "deductions do not stack");
        }
        CheckHud(stage, canvas); yield return Capture("CompactDeductions");
        yield return new WaitForSecondsRealtime(1.9f);
        Check(!NoticeVisible(stage) && !stage.GetComponent<SetssssChallengePresentation>().HasVisibleDeductions, "original notification/fade durations preserved");
        stage.DebugClearCurrentChallenge(); yield return null; yield return new WaitForEndOfFrame();
        CheckHud(stage, canvas);
        Check(stage.ChallengeNumber == 2 && !stage.Started && stage.TimeRemaining == 1080f, "next challenge waits for book with paused timer");
        Check(NoticeText(stage) == "Challenge 1 completed! Interact with the book statue to start Challenge 2.", "completion notification preserved");
        yield return Capture("CompactContinue");
        yield return new WaitForSecondsRealtime(1.6f);
        Check(NoticeVisible(stage) && stage.TimeRemaining == 1080f, "completion notification remains while waiting");
        stage.RestartChallenge();
        Debug.Log("SETSSSS_COMPACT_HUD_PASS " + Screen.width + "x" + Screen.height + " safe-area layout, simplified HUD, two tutorial targets, minimap avoidance, deductions, and book progression");
    }

    static IEnumerator FillArrangement(SetssssChallengeOne stage, Player player, Bounds a, Bounds b, Bounds da, Bounds db, Vector3 intersection)
    {
        var expected = (IDictionary)Invoke(stage, "ExpectedFiveRegionValues");
        int regionIndex = 0;
        foreach (DictionaryEntry pair in expected)
        {
            var values = (HashSet<int>)pair.Value;
            Vector3 center = regionIndex == 0 ? a.center : regionIndex == 1 ? b.center : regionIndex == 2 ? intersection : regionIndex == 3 ? da.center : db.center;
            int index = 0;
            foreach (int value in values)
            {
                // Existing copies from the real hand practice remain in the first condition.
                bool exists = Field<HashSet<GrabbableObject>>(stage, "releasedBlocks").Any(x => Value(x) == value &&
                    Vector3.Distance(XZ(x.GetComponent<Collider>().bounds.center), XZ(center)) < 5f);
                if (exists) { index++; continue; }
                var available = Field<GrabbableObject[]>(stage, "challengeFourAvailableAtSlot");
                var source = available.First(x => Value(x) == value);
                source.Grab(player.GetHoldPoint()); player.SetHeldObjectSilently(source);
                yield return null; // Existing stage observes grab and replenishes exactly once.
                Vector3 point = center + new Vector3((index % 3 - 1) * 3.5f, 0f, (index / 3 - 1) * 3.5f);
                PositionCarry(player, source, point);
                // Teleporting a test avatar is instantaneous. Give the existing
                // held Rigidbody a physics tick before releasing it naturally.
                yield return new WaitForFixedUpdate();
                yield return new WaitForEndOfFrame();
                source.Drop(); player.SetHeldObjectSilently(null);
                yield return null;
                index++;
            }
            regionIndex++;
        }
    }

    static IEnumerator StartBook(SetssssChallengeOne stage, Player player, Transform book, Button hand)
    {
        Move(player, book.position + Vector3.back * 2f, Quaternion.identity);
        yield return new WaitForSecondsRealtime(.23f);
        Click(hand); yield return null;
    }

    static IEnumerator Pickup(SetssssChallengeOne stage, Player player, GrabbableObject block, Button button)
    {
        Vector3 pos = block.transform.position;
        Move(player, new Vector3(pos.x, 2.55f, pos.z - 1.5f), Quaternion.identity);
        yield return new WaitForSecondsRealtime(.23f); yield return new WaitForEndOfFrame();
        Click(button); yield return null;
        Check(player.GetHeldObject() == block, "existing hand/context grabs targeted block");
    }

    static IEnumerator Drop(SetssssChallengeOne stage, Player player, GrabbableObject block, Vector3 point, Button hand)
    {
        PositionCarry(player, block, point);
        yield return new WaitForSecondsRealtime(.23f); yield return new WaitForEndOfFrame();
        Vector3 before = block.transform.position;
        Click(hand);
        Check(player.GetHeldObject() == null && Vector3.Distance(before, block.transform.position) < .01f, "hand drop never snaps or teleports");
        yield return null;
    }

    static void PositionCarry(Player player, GrabbableObject block, Vector3 center)
    {
        var collider = (BoxCollider)block.GetComponent<Collider>();
        player.transform.rotation = Quaternion.identity;
        Vector3 holdOffset = player.GetHoldPoint().position - player.transform.position;
        Vector3 meshOffset = player.GetHoldPoint().rotation * Vector3.Scale(collider.center, block.transform.lossyScale);
        Vector3 target = center - meshOffset - holdOffset;
        target.y = 2.55f;
        Move(player, target, Quaternion.identity);
    }

    static IEnumerator Submit(SetssssChallengeOne stage, Player player, Button submit, Vector3 point)
    {
        // Stand beside the test arrangement, clear of its physical blocks.
        Move(player, new Vector3(point.x, 2.55f, point.z - 7f), Quaternion.identity);
        yield return new WaitForSecondsRealtime(.27f); yield return new WaitForEndOfFrame();
        // Moving the test avatar beside an arrangement can nudge nearby
        // physical blocks. Respect the same settling guard as the player.
        yield return Settle(stage);
        Click(submit); yield return null;
    }

    static IEnumerator Settle(SetssssChallengeOne stage)
    {
        float until = Time.unscaledTime + 6f;
        yield return new WaitForSecondsRealtime(.8f);
        while (!(bool)Invoke(stage, "BlocksAreSettled") && Time.unscaledTime < until) yield return null;
        Check((bool)Invoke(stage, "BlocksAreSettled"), "physical drops settle on existing floor");
    }

    static IEnumerator CheckHudTutorial(SetssssChallengeOne stage, Canvas canvas)
    {
        var tutorial = canvas.GetComponent<SetssssControlsOnboarding>();
        if (tutorial == null) tutorial = canvas.gameObject.AddComponent<SetssssControlsOnboarding>();
        var help = canvas.GetComponentsInChildren<Button>(true).Single(x => x.name == "SetssssHelpButton");
        tutorial.Begin(help);
        Check(tutorial.IsActive, "tutorial can resolve relocated HUD fields");
        Invoke(tutorial, "BeginTimer");
        var targets = new[] { stage.Hud.Timer.rectTransform, stage.Hud.Challenge.rectTransform };
        var names = new[] { "Timer", "Challenge" };
        for (int i = 0; i < targets.Length; i++)
        {
            yield return null; yield return new WaitForEndOfFrame();
            var highlight = Field<RectTransform>(tutorial, "highlight");
            var arrow = Field<RectTransform>(tutorial, "arrow");
            var instructions = Field<RectTransform>(tutorial, "instructions");
            var text = Field<TextMeshProUGUI>(tutorial, "instructionText");
            Check(highlight.parent == targets[i] && arrow.gameObject.activeInHierarchy, "highlight/arrow follow actual " + names[i] + " target");
            Check(Vector2.Distance(ScreenRect(highlight).center, ScreenRect(targets[i]).center) < 1f, "highlight remains aligned across sizes");
            Check(!ScreenRect(arrow).Overlaps(ScreenRect(stage.Hud.Hud)), "HUD arrow remains outside compact panel");
            foreach (var other in targets) if (other != targets[i]) Check(!ScreenRect(highlight).Overlaps(ScreenRect(other)), "compact highlight avoids neighboring labels");
            Check(!ScreenRect(instructions).Overlaps(ScreenRect(stage.Hud.Hud)), "explanations avoid shared HUD");
            Check(!ScreenRect(instructions).Overlaps(ScreenRect(arrow)), "explanations leave HUD arrow visible");
            text.ForceMeshUpdate(); Check(!text.isTextOverflowing, "tutorial explanation readable");
            Check(!stage.Started && stage.TimeRemaining == 1080f && !NoticeVisible(stage), "practice HUD does not start challenge/timer/notification");
            Check(canvas.GetComponentsInChildren<Button>().Count(x => x.name == "PRELIM Shared Skip Tutorial") == 1, "single shared Skip");
            yield return Capture("Tutorial" + names[i]);
            Click(Field<Button>(tutorial, "nextButton"));
        }
        Check(Field<object>(tutorial, "step").ToString() == "Spawner", "challenge highlight advances directly to spawner, without deleted HUD lessons");
        Click(Field<Button>(tutorial, "nextButton")); yield return null; yield return new WaitForEndOfFrame();
        Check(Field<object>(tutorial, "step").ToString() == "ViewBoard", "question explanation targets View Board");
        Click(Field<Button>(Field<SetssssBoardView>(stage, "boardView"), "viewBoardButton")); yield return new WaitForSecondsRealtime(.8f);
        Check(Field<object>(tutorial, "step").ToString() == "Board" && Field<TextMeshPro>(stage, "boardText").text.Contains("PRACTICE QUESTION"), "practice question is visible on the Information Board");
        foreach (var name in new[] { "Board", "Placement", "PlacementSubmit", "Differences", "Complement" })
        {
            Check(Field<object>(tutorial, "step").ToString() == name, "educational walkthrough follows " + name);
            yield return null; yield return new WaitForEndOfFrame();
            var text = Field<TextMeshProUGUI>(tutorial, "instructionText"); text.ForceMeshUpdate();
            Check(!text.isTextOverflowing && !text.text.ToLowerInvariant().Contains("condition"), "readable question/element terminology in " + name);
            if (name == "Board") yield return Capture("TutorialQuestionBoard");
            Click(Field<Button>(tutorial, "nextButton")); yield return null;
        }
        Check(Field<object>(tutorial, "step").ToString() == "Confirm", "updated tutorial finishes at existing confirmation");
        tutorial.ConfirmNo(); yield return null;
        Check(Field<object>(tutorial, "step").ToString() == "Movement", "No restarts from movement");
        tutorial.SkipTutorial(); yield return null;
        tutorial.ConfirmYes(); yield return null;
        Check(!tutorial.IsActive && !stage.Started && stage.TimeRemaining == 1080f && !Field<TextMeshPro>(stage, "boardText").text.Contains("PRACTICE"), "Yes clears preview and preserves book gate");
    }

    static void CheckHud(SetssssChallengeOne stage, Canvas canvas)
    {
        var hud = stage.Hud;
        Check(hud.Timer.transform.parent == hud.Hud && hud.Hud.Find("ConditionProgress") == null && hud.Hud.Find("ChallengeReminder") == null, "HUD contains timer and challenge without removed fields");
        Check(canvas.GetComponentsInChildren<TMP_Text>(true).Count(x => x.name == "TimerText") == 1 &&
            canvas.GetComponentsInChildren<TMP_Text>(true).Count(x => x.name == "ChallengeReminder") == 1 &&
            canvas.GetComponentsInChildren<TMP_Text>(true).Count(x => x.name == "ChallengeFeedback") == 1, "no duplicated legacy labels");
        Check(hud.Challenge.text == "Challenge " + stage.ChallengeNumber + " of 5" && !Field<TMP_Text>(stage, "reminderText").gameObject.activeInHierarchy, "HUD shows progress and legacy problem duplicate is hidden");
        var safe = ScreenRect(Field<RectTransform>(hud, "safeArea"));
        var panel = ScreenRect(hud.Hud); var dock = ScreenRect(hud.NoticeDock);
        Check(safe.Contains(panel.min) && safe.Contains(panel.max) && panel.center.x > safe.center.x, "HUD stays top-right inside safe area");
        Check(!panel.Overlaps(dock) && safe.Contains(dock.min) && safe.Contains(dock.max), "notification dock clear of HUD and safe area");
        foreach (var button in canvas.GetComponentsInChildren<Button>())
            if (button.name == "JumpButton" || button.name == "InteractButton" || button.name == "SettingsButton" || button.name == "SetssssHelpButton")
                Check(!panel.Overlaps(ScreenRect((RectTransform)button.transform)) && !dock.Overlaps(ScreenRect((RectTransform)button.transform)), "HUD/notifications avoid " + button.name);
        foreach (var text in hud.Hud.GetComponentsInChildren<TMP_Text>()) { text.ForceMeshUpdate(); Check(!text.isTextOverflowing, "HUD label fits: " + text.name); }
        var message = Field<TextMeshProUGUI>(hud.Notice, "message");
        message.ForceMeshUpdate(true);
        if (NoticeVisible(stage)) Check(!message.isTextOverflowing, "notification readable without clipping");
    }

    static string NoticeText(SetssssChallengeOne stage) => Field<TextMeshProUGUI>(stage.Hud.Notice, "message").text;
    static bool NoticeVisible(SetssssChallengeOne stage) => Field<RectTransform>(stage.Hud.Notice, "panel").gameObject.activeInHierarchy;

    static IEnumerator TumbleLabel(GrabbableObject block)
    {
        var body = block.GetComponent<Rigidbody>();
        foreach (Vector3 rotation in new[] { new Vector3(90f, 25f, 10f), new Vector3(155f, 70f, 135f), new Vector3(0f, 180f, 90f) })
        {
            body.rotation = Quaternion.Euler(rotation);
            body.angularVelocity = new Vector3(.7f, .4f, .5f);
            Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(.08f); yield return new WaitForEndOfFrame();
            CheckLabel(block);
        }
        yield return Capture("TumblingNumberLabel");
    }

    static void CheckLabel(GrabbableObject block)
    {
        var number = block.GetComponent<SetssssNumberedBlock>();
        var label = number.NumberLabel;
        var renderer = label.GetComponent<Renderer>();
        Check(renderer.bounds.min.y >= number.BlockBounds.max.y + .25f, "number stays above current rotated block bounds");
        Check(Vector3.Dot(label.transform.up, Camera.main.transform.up) > .999f &&
            Vector3.Dot(label.transform.forward, Camera.main.transform.forward) > .999f, "number faces camera without block roll");
        label.ForceMeshUpdate();
        Check(!label.isTextOverflowing, "two-digit number fits label");
        var labelBounds = label.mesh.bounds;
        Vector3 bottom = label.transform.TransformPoint(labelBounds.center - Vector3.up * labelBounds.extents.y);
        Vector3 top = label.transform.TransformPoint(labelBounds.center + Vector3.up * labelBounds.extents.y);
        float pixels = Camera.main.WorldToScreenPoint(top).y - Camera.main.WorldToScreenPoint(bottom).y;
        if (Camera.main.WorldToScreenPoint(number.BlockBounds.center).z > Camera.main.nearClipPlane)
            Check(pixels > 14f * Mathf.Max(1f, Camera.main.pixelHeight / 480f), "number remains readable at mobile resolution");
    }

    static bool ReviewCorrect(SetssssChallengeOne stage)
    {
        var review = Invoke(stage, "ReviewIncorrectPlacement");
        return !Field<bool>(review, "missingRequired", BindingFlags.Instance | BindingFlags.Public) &&
            Field<HashSet<GrabbableObject>>(review, "incorrectBlocks", BindingFlags.Instance | BindingFlags.Public).Count == 0;
    }

    static void CheckMobileUI(SetssssChallengePresentation ui, Canvas canvas)
    {
        var card = Field<RectTransform>(ui, "card");
        var viewport = Field<ScrollRect>(ui, "scroll").viewport;
        var button = (RectTransform)ui.ContinueButton.transform;
        Check(!ScreenRect(viewport).Overlaps(ScreenRect(button)), "Continue has separate footer area");
        Check(ScreenRect(button).height >= 30f, "Continue easy to tap at mobile resolution");
        var safe = ScreenRect(Field<RectTransform>(ui, "safeArea"));
        Check(safe.Contains(ScreenRect(card).min) && safe.Contains(ScreenRect(card).max), "explanation stays inside mobile safe area");
        foreach (var text in card.GetComponentsInChildren<TMP_Text>())
        {
            text.ForceMeshUpdate();
            Check(!text.isTextOverflowing, "modal text does not clip: " + text.name);
        }
    }

    static Rect ScreenRect(RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        var canvas = rect.GetComponentInParent<Canvas>();
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    static IEnumerator Capture(string label)
    {
        System.IO.Directory.CreateDirectory("Temp/SetsQA");
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot("Temp/SetsQA/" + Screen.width + "x" + Screen.height + "_" + label + ".png");
        yield return null;
    }

    static void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();
        Check(button != null && button.gameObject.activeInHierarchy && button.interactable, "button available: " + (button != null ? button.name : "null"));
        var data = new PointerEventData(EventSystem.current) { position = ScreenRect((RectTransform)button.transform).center };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        Check(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)), "button not obscured: " + button.name);
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
    }

    static void Move(Player player, Vector3 position, Quaternion rotation) => PrelimTeleportTransition.MovePlayerImmediately(player.gameObject, position, rotation);
    static Vector3 XZ(Vector3 p) => new Vector3(p.x, 0f, p.z);
    static int Value(GrabbableObject b) => b.GetComponent<SetssssNumberedBlock>().Value;
    static T Field<T>(object obj, string name, BindingFlags flags = Private) => (T)obj.GetType().GetField(name, flags).GetValue(obj);
    static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Private).SetValue(obj, value);
    static object Invoke(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Private).Invoke(obj, args);
    static void Check(bool passed, string message) { if (!passed) throw new Exception("SETSSSS_QA: " + message); }
}

public sealed class SetssssPlayCheckRunner : MonoBehaviour { }
