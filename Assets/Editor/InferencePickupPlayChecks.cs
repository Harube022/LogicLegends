#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LogicLegends.Inference;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Offline checks of the actual saved scene, UI raycasts, pickup gate and solved board.</summary>
public static class InferencePickupPlayChecks
{
    const string Flag = "Inference.PickupQA";
    const string Report = "Library/InferencePickupPlayChecks.txt";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Logic Legends/Inference/Check Crystal Quizzes and Solved Board")]
    public static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name != "RulesOfInference")
            throw new InvalidOperationException("Open RulesOfInference outside Play Mode.");
        if (!UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes()) return;
        var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        var view = EditorWindow.GetWindow(type);
        SessionState.SetInt(Flag + ".View", (int)type.GetProperty("selectedSizeIndex", Private | BindingFlags.Public).GetValue(view));
        SessionState.SetBool(Flag, true);
        File.WriteAllText(Report, "Actual RulesOfInference scene: crystal quizzes, nine towers, solved-board visibility.\n");
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
        if (scene.name != "RulesOfInference") return;
        SceneManager.sceneLoaded -= Loaded;
        var avatar = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Multiplayer_Male"));
        avatar.GetComponent<MultiplayerAvatarSync>().enabled = false;
        avatar.GetComponent<PlayerEquipmentLoader>().enabled = false;
        foreach (var skin in avatar.GetComponentsInChildren<PlayerAnimator>(true)) skin.gameObject.SetActive(skin.name == "UNIFORM_MALE");
        avatar.GetComponent<CharacterController>().enabled = true;
        Move(avatar.GetComponent<Player>(), UnityEngine.Object.FindFirstObjectByType<InferenceBookStart>().transform.position + Vector3.forward * 3);
        new GameObject("Inference Pickup QA Runner").AddComponent<InferencePickupCheckRunner>().StartCoroutine(Guard(Run()));
    }
    static IEnumerator Guard(IEnumerator run)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(run);
        while (stack.Count > 0)
        {
            object next = null; bool more;
            try { more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current; }
            catch (Exception e)
            {
                File.AppendAllText(Report, "FAIL " + e + "\n"); Debug.LogException(e); Finish(); yield break;
            }
            if (!more) { stack.Pop(); continue; }
            if (next is IEnumerator child) stack.Push(child); else yield return next;
        }
        File.AppendAllText(Report, "ALL CRYSTAL QUIZ AND BOARD CHECKS PASSED\n");
        Finish();
    }
    static void Finish()
    {
        var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        type.GetProperty("selectedSizeIndex", Private | BindingFlags.Public).SetValue(EditorWindow.GetWindow(type), SessionState.GetInt(Flag + ".View", 4));
        Crystal.SafeArea.Sim = Crystal.SafeArea.SimDevice.None;
        SessionState.SetBool(Flag, false); EditorApplication.isPlaying = false;
    }

    static IEnumerator Run()
    {
        Application.runInBackground = true;
        float deadline = Time.unscaledTime + 50;
        while ((StageJourneyUI.Instance == null || !Field<bool>(StageJourneyUI.Instance, "tutorialOpen")) && Time.unscaledTime < deadline) yield return null;
        var stage = UnityEngine.Object.FindFirstObjectByType<InferenceChallenge>();
        var journey = StageJourneyUI.Instance;
        var quiz = stage.GetComponent<InferenceCrystalQuiz>();
        var player = Player.LocalInstance;
        Check(journey != null && quiz != null && player != null, "stage initialized");
        Check(stage.crystalSpawns.Length == 9 && stage.rules.Length == 9, "nine towers and nine gameplay rules saved");
        string[] codes = { "MP", "MT", "HS", "DS", "CD", "SIMP", "CONJ", "ADD", "DN" };
        Check(stage.rules.Select(r => r.abbreviation).SequenceEqual(codes), "unique intended tower rule order");
        for (int i = 0; i < 9; i++) Check(stage.crystalSpawns[i].parent.name == "PILLARS (" + (i + 1) + ")", "tower spawn reference " + (i + 1));

        int pages = Field<string[]>(journey, "tutorialBodies").Length;
        for (int page = 0; page < pages; page++)
        {
            yield return null; yield return new WaitForEndOfFrame();
            var text = Field<TMP_Text>(journey, "tutorialBody"); text.ForceMeshUpdate();
            Check(!text.isTextOverflowing, "Help page fits: " + page);
            if (page == 4) yield return Capture("HelpQuiz");
            if (page < pages - 1) Field<Button>(journey, "tutorialNext").onClick.Invoke();
        }
        journey.CloseInferenceTutorial(); yield return null;
        var tutorial = InferenceControlsOnboarding.Active;
        Invoke(tutorial, "BeginCrystalTower"); yield return new WaitForSecondsRealtime(1.4f);
        var interaction = stage.GetComponent<InferenceCrystalInteraction>();
        Click(interaction.CollectButton); yield return null;
        Check(quiz.IsOpen && quiz.IsPractice && tutorial.CurrentStep == "CrystalQuiz", "dedicated pickup opens practice quiz");
        float before = journey.InferenceRemainingSeconds;
        Click(quiz.AnswerButtons[(quiz.Current.CorrectChoice + 1) % 4]);
        quiz.Answer(quiz.Current.CorrectChoice);
        Check(quiz.Answered && journey.InferenceRemainingSeconds == before && player.GetHeldObject() == null, "practice answer is isolated and cannot penalize twice");
        yield return CheckLayout(quiz);
        yield return Capture("PracticeFeedback");
        Click(quiz.CollectButton); yield return null;
        Check(tutorial.CurrentStep == "Confirm" && !quiz.IsOpen && player.GetHeldObject() == null && !stage.HasStarted, "practice completes without awarding a real crystal");
        tutorial.ConfirmNo(); Invoke(tutorial, "BeginCrystalTower"); yield return null;
        GameInput.Instance.MobileInteract(); yield return null;
        Check(quiz.IsOpen, "hand opens practice quiz");
        Click(quiz.BackButton);
        Check(tutorial.CurrentStep == "CrystalTower" && !quiz.IsOpen, "practice Back returns to collection lesson");
        // Reopen in the same frame to catch deferred-destruction races on shared Skip.
        GameInput.Instance.MobileInteract(); yield return null;
        var skip = Field<Button>(tutorial, "skipButton");
        Check(skip.GetComponents<Canvas>().Length == 1 && skip.GetComponents<GraphicRaycaster>().Length == 1, "same-frame reopening reuses Skip rendering and raycasts");
        Check(UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Count(b => b.name == "Inference Shared Skip") == 1, "one shared Skip Tutorial");
        Click(skip); yield return null;
        Check(!quiz.IsOpen && tutorial.CurrentStep == "Confirm" && GameInput.Instance.GameplayInputBlocked, "skip cancels quiz safely and opens confirmation");
        tutorial.ConfirmYes(); yield return null;
        Check(InferenceControlsOnboarding.Active == null && !journey.InferenceQuizOpen && player.enabled && !GameInput.Instance.GameplayInputBlocked, "tutorial cleanup restores controls and waiting timer");
        File.AppendAllText(Report, "PASS tutorial practice, Back, shared Skip, no inventory/time/progress mutation.\n");

        var questionIds = new HashSet<string>();
        for (int challenge = 0; challenge < 5; challenge++)
        {
            Move(player, UnityEngine.Object.FindFirstObjectByType<InferenceBookStart>().transform.position + Vector3.forward * 2);
            player.transform.forward = Vector3.back;
            yield return new WaitForSecondsRealtime(.2f);
            GameInput.Instance.MobileInteract(); yield return null;
            Check(stage.HasStarted && stage.SolvedRounds == challenge, "real book starts challenge " + (challenge + 1));
            stage.OpenBoard(player); yield return BoardReady(stage);
            FillPremises(stage);
            yield return CheckPartialFeedback(stage);
            Click(stage.validateButton); yield return null;
            Check(stage.Crystals.Count == 9 && stage.Crystals.Select(c => c.GetComponent<RuleDiamond>().Abbreviation).SequenceEqual(codes), "every tower receives its intended crystal");
            foreach (int size in challenge == 0 ? new[] { 6, 12, 3 } : new[] { 12 })
            {
                SetSize(size); yield return null; yield return new WaitForEndOfFrame();
                var status = stage.board.GetComponentsInChildren<TMP_Text>().Single(t => t.name == "Theme_BankStatus");
                status.ForceMeshUpdate();
                Check(!status.isTextOverflowing, "Crystal Search instructions fit " + Screen.width + "x" + Screen.height);
                Check(!stage.wordBank.gameObject.activeSelf && stage.board.GetComponentsInChildren<TMP_Text>().Count(t => t.text.Contains("Bring the crystal to the altar")) == 1, "one Crystal Search instruction, no word-bank heading overlap");
                if (challenge == 0) yield return Capture("CrystalSearch");
            }
            Click(stage.exploreButton); yield return null;
            var altar = UnityEngine.Object.FindFirstObjectByType<InferenceAltarTrigger>();
            Move(player, altar.transform.position); yield return new WaitForSecondsRealtime(.3f);
            var altarButton = Field<Button>(altar, "viewBoardButton");
            Check(altarButton.gameObject.activeInHierarchy && altarButton.GetComponentInChildren<TMP_Text>().text == "View Board", "empty-handed altar label is View Board");

            // On Challenge 1, actually quiz/collect/drop from every existing tower.
            var toVisit = challenge == 0 ? stage.Crystals.ToArray() : new[] { stage.Crystals.First(c => c.GetComponent<RuleDiamond>().Abbreviation == stage.Question.Rule.abbreviation) };
            foreach (var crystal in toVisit)
            {
                MoveToCrystal(player, crystal);
                yield return new WaitForSecondsRealtime(.25f);
                yield return new WaitForEndOfFrame();
                var tower = stage.crystalSpawns[stage.Crystals.ToList().IndexOf(crystal)].parent;
                var towerRenderers = tower.GetComponentsInChildren<MeshRenderer>(true);
                var towerBounds = towerRenderers[0].bounds;
                foreach (var renderer in towerRenderers) towerBounds.Encapsulate(renderer.bounds);
                crystal.label.ForceMeshUpdate();
                Check(!towerBounds.Contains(crystal.label.transform.position) && !crystal.label.isTextOverflowing, "rule name outside complete tower geometry: " + crystal.name);
                var outward = Camera.main.transform.position - towerBounds.center; outward.y = 0; outward.Normalize();
                float edge = Mathf.Abs(outward.x) * towerBounds.extents.x + Mathf.Abs(outward.z) * towerBounds.extents.z;
                var corners = new Vector3[4]; crystal.label.rectTransform.GetWorldCorners(corners);
                Check(corners.All(p => Vector3.Dot(p - towerBounds.center, outward) > edge), "entire rule label clears the tower face");
                var toLabel = (crystal.label.transform.position - Camera.main.transform.position).normalized;
                Check(Vector3.Dot(crystal.label.transform.forward, toLabel) > .999f, "rule label faces gameplay camera");
                if (challenge == 0) yield return CheckTowerFraming(crystal, towerBounds);
                Check(interaction.AvailableCrystal == crystal && interaction.CollectButton.gameObject.activeInHierarchy, "reachable pickup prompt at " + crystal.name);
                bool wrong = challenge == 0 && Array.IndexOf(toVisit, crystal) % 2 == 1;
                if (wrong) GameInput.Instance.MobileInteract(); else Click(interaction.CollectButton);
                yield return null;
                Check(quiz.IsOpen && player.GetHeldObject() == null && !player.enabled && GameInput.Instance.GameplayInputBlocked, "both pickup methods require quiz before inventory");
                Check(quiz.FeedbackLabel.text == "", "removed countdown helper sentence");
                Check(questionIds.Add(quiz.Current.Question.Id), "no question repeats before exhausting bank");
                float time = journey.InferenceRemainingSeconds;
                yield return new WaitForSecondsRealtime(.25f);
                Check(journey.InferenceRemainingSeconds == time, "quiz pauses countdown");
                interaction.TryHandleInteraction();
                Check(quiz.IsOpen && player.GetHeldObject() == null, "duplicate hand input cannot collect or overlap quiz");
                int answer = wrong ? (quiz.Current.CorrectChoice + 1) % 4 : quiz.Current.CorrectChoice;
                Click(quiz.AnswerButtons[answer]); quiz.Answer(answer);
                Check(journey.InferenceRemainingSeconds == time - (wrong ? 10 : 0), "wrong-answer penalty applies exactly once");
                if (wrong && Array.IndexOf(toVisit, crystal) == 1)
                {
                    var minus = Field<TMP_Text>(journey, "inferenceDeduction");
                    Check(minus.text == "−10s" && minus.gameObject.activeInHierarchy && minus.color.a == 1 && minus.color.r > minus.color.g * 2,
                        "red penalty appears immediately, fully visible near timer");
                    Check(minus.GetComponent<Canvas>().sortingOrder > Field<RectTransform>(quiz, "root").GetComponent<Canvas>().sortingOrder, "penalty visible above quiz shade");
                    yield return Capture("IncorrectQuiz");
                    yield return new WaitForSecondsRealtime(2.4f);
                    Check(!minus.gameObject.activeSelf && journey.InferenceRemainingSeconds == time - 10, "penalty fades while quiz remains paused");
                }
                yield return CheckLayout(quiz);
                Click(quiz.CollectButton); yield return null;
                Check(player.GetHeldObject() == crystal.GetComponent<GrabbableObject>() && player.enabled && !journey.InferenceQuizOpen && !GameInput.Instance.GameplayInputBlocked,
                    "answered quiz gives exact crystal and restores gameplay");
                if (challenge == 0)
                {
                    GameInput.Instance.MobileInteract(); yield return null;
                    Check(player.GetHeldObject() == null && !quiz.IsOpen && !crystal.GetComponent<SphereCollider>().isTrigger, "dropping uses existing accessible collision, not another pickup quiz");
                    crystal.ReturnToOrigin();
                }
            }
            if (challenge == 0)
            {
                File.AppendAllText(Report, "PASS all nine towers: correct rule/label/pickup, both inputs, distinct quiz draws, correct/incorrect collection, single penalties and fading −10s.\n");
                var correct = stage.Crystals.First(c => c.GetComponent<RuleDiamond>().Abbreviation == stage.Question.Rule.abbreviation);
                MoveToCrystal(player, correct); yield return new WaitForSecondsRealtime(.2f);
                Click(interaction.CollectButton); yield return null;
                // Check desktop and both mobile orientations using this same live panel.
                foreach (int size in new[] { 6, 12, 3 })
                {
                    SetSize(size); yield return new WaitForSecondsRealtime(.25f);
                    yield return CheckLayout(quiz); yield return Capture("QuizLayout");
                }
                Click(quiz.AnswerButtons[quiz.Current.CorrectChoice]); yield return null;
                Click(quiz.CollectButton); yield return null;
            }
            Move(player, altar.transform.position); yield return new WaitForSecondsRealtime(.3f);
            Check(altarButton.GetComponentInChildren<TMP_Text>().text == "Submit to Altar", "carried Rule Crystal changes altar label");
            int roundsBefore = stage.SolvedRounds;
            var carriedBefore = player.GetHeldObject();
            Click(altarButton); yield return BoardReady(stage);
            Check(stage.SolvedRounds == roundsBefore && player.GetHeldObject() == carriedBefore && !stage.dragPuzzle.conclusion.Revealed,
                "Submit to Altar only opens board; no automatic validation or inventory change");
            string conclusion = stage.dragPuzzle.Puzzle.Conclusion;
            if (challenge == 4) stage.requiredRounds = 6; // Avoid writing to the signed-in account from QA.
            Click(stage.placeButton); yield return null;
            Check(stage.SolvedRounds == challenge + 1 && stage.IsBoardOpen && stage.board.activeSelf && stage.dragPuzzle.conclusion.Revealed,
                "submission keeps solved board open: challenge " + (challenge + 1));
            Check(stage.dragPuzzle.conclusion.label.text.Contains(conclusion) && stage.feedback.text.Contains("Correct"), "revealed conclusion and result remain readable");
            float completedTime = journey.InferenceRemainingSeconds;
            stage.PlaceHeldCrystal(); yield return new WaitForSecondsRealtime(.7f);
            Check(stage.SolvedRounds == challenge + 1 && journey.InferenceRemainingSeconds == completedTime && !Field<bool>(journey, "resultOpen"), "no duplicate progress or automatic board exit");
            yield return Capture("ConclusionChallenge" + (challenge + 1));
            if (challenge == 4)
            {
                stage.requiredRounds = 5;
                typeof(InferenceChallenge).GetField("<PuzzleCompleted>k__BackingField", Private).SetValue(stage, true);
                Invoke(journey, "OnInferenceComplete");
                Check(stage.IsBoardOpen && !Field<bool>(journey, "resultOpen"), "final result waits for acknowledgement of conclusion");
            }
            Click(challenge % 2 == 0 ? stage.nextButton : stage.exploreButton); yield return null;
            Check(!stage.IsBoardOpen, "Back and Continue both leave solved board");
            if (challenge < 4) Check(!stage.HasStarted && stage.Question == null && player.enabled, "next question waits for real book interaction");
        }
        Check(Field<bool>(journey, "resultOpen"), "completion UI appears only after final board acknowledgement");
        yield return Capture("StageComplete");
        File.AppendAllText(Report, "PASS five challenges, persistent conclusions, Back/Continue raycasts, book-gated progression, final completion acknowledgement; desktop and mobile layouts.\n");

        // Check every possible question and feedback paragraph at both mobile aspects.
        foreach (int size in new[] { 3, 12 })
        {
            SetSize(size); yield return null;
            Check(quiz.Open(() => { }, true), "isolated layout practice opens");
            var deck = new InferencePickupQuestions.Deck(917);
            for (int q = 0; q < 20; q++)
            {
                var draw = deck.Next();
                typeof(InferenceCrystalQuiz).GetField("<Current>k__BackingField", Private).SetValue(quiz, draw);
                Invoke(quiz, "PresentQuestion");
                quiz.Answer((draw.CorrectChoice + 1) % 4);
                yield return CheckLayout(quiz);
            }
            quiz.Close();
        }
        File.AppendAllText(Report, "PASS all 20 question/answer/feedback layouts in portrait and wide mobile.\n");

        // Exercise a penalty that exhausts the clock without persisting any account progress.
        typeof(StageJourneyUI).GetField("inferenceFinished", Private).SetValue(journey, false);
        typeof(StageJourneyUI).GetField("resultOpen", Private).SetValue(journey, false);
        typeof(StageJourneyUI).GetField("inferenceRemaining", Private).SetValue(journey, 5f);
        Field<RectTransform>(journey, "modal").gameObject.SetActive(false);
        typeof(InferenceChallenge).GetField("<HasStarted>k__BackingField", Private).SetValue(stage, true);
        typeof(InferenceChallenge).GetField("<PuzzleCompleted>k__BackingField", Private).SetValue(stage, false);
        Time.timeScale = 1; player.ToggleControl(true); GameInput.Instance.SetGameplayInputBlocked(false);
        bool awarded = false;
        Check(quiz.Open(() => awarded = true), "last-seconds quiz opens");
        quiz.Answer((quiz.Current.CorrectChoice + 1) % 4); quiz.Answer(quiz.Current.CorrectChoice);
        Check(quiz.IsOpen && journey.InferenceRemainingSeconds == 0 && !Field<bool>(journey, "resultOpen"), "zero-time penalty still leaves answer feedback readable");
        quiz.CollectAnsweredCrystal(); yield return null;
        Check(!awarded && !quiz.IsOpen && Field<bool>(journey, "resultOpen") && GameInput.Instance.GameplayInputBlocked && !player.enabled,
            "expired timer prevents award and opens existing Game Over on acknowledgement");
        File.AppendAllText(Report, "PASS zero-time penalty and Game Over cleanup, with no duplicate penalties or awards.\n");
    }

    static IEnumerator BoardReady(InferenceChallenge stage)
    {
        float deadline = Time.unscaledTime + 5;
        while (!stage.boardInput.interactable && Time.unscaledTime < deadline) yield return null;
        Check(stage.boardInput.interactable, "board input enabled after camera blend");
        yield return new WaitForSecondsRealtime(.2f);
    }
    static void FillPremises(InferenceChallenge stage)
    {
        var tiles = stage.dragPuzzle.wordBank.GetComponentsInChildren<DraggableWord>().ToList();
        for (int i = 0; i < stage.dragPuzzle.Zones.Count; i++)
        {
            var tile = tiles.First(t => t.Word == stage.dragPuzzle.Puzzle.CorrectPlacements[i]);
            tiles.Remove(tile); stage.dragPuzzle.Zones[i].Place(tile);
        }
    }
    static IEnumerator CheckPartialFeedback(InferenceChallenge stage)
    {
        var zones = stage.dragPuzzle.Zones;
        var first = zones[0]; var original = first.PlacedWord;
        var other = zones.Skip(1).FirstOrDefault(z => z.Value != original.Word);
        var wrong = other != null ? other.PlacedWord : null;
        original.ReturnToBank();
        if (wrong != null) { first.Place(wrong); other.Place(original); }
        var placed = zones.Select(z => z.PlacedWord).ToArray();
        Click(stage.validateButton); yield return null; yield return new WaitForEndOfFrame();
        Check(stage.Stage == InferencePuzzleStage.CompletingPremises && zones.Select(z => z.PlacedWord).SequenceEqual(placed), "incorrect check preserves all premise entries");
        for (int i = 0; i < zones.Count; i++)
        {
            bool correct = string.Equals(zones[i].Value, stage.dragPuzzle.Puzzle.CorrectPlacements[i], StringComparison.Ordinal);
            Check(zones[i].CheckedCorrect == correct, "independent premise verdict " + i);
            var color = (zones[i].PlacedWord != null ? zones[i].PlacedWord.gameObject : zones[i].gameObject).GetComponent<Image>().color;
            Check(!correct ? color.r > color.g * 2 : color.g > color.r * 2, "visible red/green on filled entry " + i);
        }
        if (zones.Count > 1) yield return Capture("PartialPremises");
        first.Place(original); yield return null;
        Check(first.CheckedCorrect == null, "replacing an entry clears its previous verdict");
        Check(zones.Skip(1).Where(z => z != other).All(z => z.CheckedCorrect == true), "editing one entry preserves untouched feedback");
        if (other != null) other.Place(wrong);
        original.ReturnToBank(); first.Place(original); yield return null;
        Check(first.CheckedCorrect == null, "moving and replacing the same value remains unchecked");
    }
    static IEnumerator CheckLayout(InferenceCrystalQuiz quiz)
    {
        yield return null; yield return new WaitForEndOfFrame(); Canvas.ForceUpdateCanvases();
        foreach (var text in quiz.AnswerButtons.SelectMany(b => b.GetComponentsInChildren<TMP_Text>()).Concat(new[] { quiz.FeedbackLabel, Field<TMP_Text>(quiz, "prompt"), Field<TMP_Text>(quiz, "title") }))
        { text.ForceMeshUpdate(); Check(!text.isTextOverflowing, "quiz text fits " + Screen.width + "x" + Screen.height + ": " + text.name); }
        foreach (var button in quiz.AnswerButtons.Concat(new[] { quiz.BackButton }).Concat(quiz.Answered ? new[] { quiz.CollectButton } : Array.Empty<Button>()))
            CheckRaycast(button);
        var card = Field<RectTransform>(quiz, "card");
        foreach (var button in new[] { quiz.BackButton, quiz.CollectButton })
        {
            var rect = (RectTransform)button.transform;
            Check(rect.anchorMin.y >= .075f && rect.anchorMax.y <= .1751f, "quiz footer has inset from panel border");
        }
    }
    static void SetSize(int index)
    {
        var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        var view = EditorWindow.GetWindow(type); type.GetProperty("selectedSizeIndex", Private | BindingFlags.Public).SetValue(view, index); view.Focus();
        Crystal.SafeArea.Sim = index == 12 ? Crystal.SafeArea.SimDevice.Pixel3XL_LSR : index == 3 ? Crystal.SafeArea.SimDevice.iPhoneX : Crystal.SafeArea.SimDevice.None;
    }
    static void MoveToCrystal(Player player, InferenceCrystal crystal)
    {
        var stage = UnityEngine.Object.FindFirstObjectByType<InferenceChallenge>();
        int index = stage.Crystals.ToList().IndexOf(crystal);
        var tower = stage.crystalSpawns[index].parent;
        var direction = crystal.PickupOrigin - tower.position; direction.y = 0; direction.Normalize();
        Move(player, tower.position + direction * 7.3f);
        player.transform.forward = -direction;
    }
    static IEnumerator CheckTowerFraming(InferenceCrystal crystal, Bounds tower)
    {
        // Use the gameplay camera to frame each tower for a readable visual check.
        // The offline avatar fixture has no network equipment/camera binding.
        var camera = Camera.main; var brain = camera.GetComponent<Unity.Cinemachine.CinemachineBrain>();
        bool wasEnabled = brain != null && brain.enabled;
        var position = camera.transform.position; var rotation = camera.transform.rotation;
        var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        var view = EditorWindow.GetWindow(type);
        int size = (int)type.GetProperty("selectedSizeIndex", Private | BindingFlags.Public).GetValue(view);
        if (brain != null) brain.enabled = false;
        try
        {
            SetSize(6);
            var outward = new Vector3(100 - tower.center.x, 0, 768 - tower.center.z).normalized;
            var target = crystal.PickupOrigin + Vector3.up * 2f;
            camera.transform.position = new Vector3(tower.center.x, target.y + 2f, tower.center.z) + outward * 45f;
            camera.transform.LookAt(target);
            yield return null; yield return new WaitForEndOfFrame();
            var corners = new Vector3[4]; crystal.label.rectTransform.GetWorldCorners(corners);
            Check(corners.Select(p => camera.WorldToViewportPoint(p)).All(p => p.z > 0 && p.x > .02f && p.x < .98f && p.y > .02f && p.y < .98f), "full rule label fits gameplay camera frame");
            yield return Capture("TowerLabel_" + crystal.GetComponent<RuleDiamond>().Abbreviation);
        }
        finally
        {
            camera.transform.SetPositionAndRotation(position, rotation);
            if (brain != null) brain.enabled = wasEnabled;
            SetSize(size);
        }
        yield return null;
    }
    static void Move(Player player, Vector3 point)
    {
        var hits = Physics.RaycastAll(new Vector3(point.x, -123, point.z), Vector3.down, 35, ~0, QueryTriggerInteraction.Ignore)
            .Where(h => h.normal.y > .7f && h.collider.GetComponentInParent<Player>() == null && h.collider.GetComponentInParent<InferenceCrystal>() == null).OrderBy(h => h.distance).ToArray();
        if (hits.Length > 0)
        {
            var cc = player.GetComponent<CharacterController>(); point.y = hits[0].point.y + cc.height * .5f - cc.center.y + .12f;
        }
        PrelimTeleportTransition.MovePlayerImmediately(player.gameObject, point, Quaternion.identity);
    }
    static void Click(Button button) { Canvas.ForceUpdateCanvases(); Check(button != null && button.interactable && button.gameObject.activeInHierarchy, "button available " + button?.name); var data = CheckRaycast(button); ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler); }
    static PointerEventData CheckRaycast(Button button)
    {
        var canvas = button.GetComponentInParent<Canvas>();
        var camera = canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        var rect = (RectTransform)button.transform;
        Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
        var data = new PointerEventData(EventSystem.current) { position = point };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        Check(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)), "button unobscured " + button.name + ": " + (hits.Count == 0 ? "no hit" : hits[0].gameObject.name));
        return data;
    }
    static IEnumerator Capture(string name)
    {
        Directory.CreateDirectory("Temp/InferencePickupQA"); yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot("Temp/InferencePickupQA/" + Screen.width + "x" + Screen.height + "_" + name + ".png"); yield return null;
    }
    static T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name, Private).GetValue(obj);
    static object Invoke(object obj, string name) => obj.GetType().GetMethod(name, Private).Invoke(obj, null);
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
public sealed class InferencePickupCheckRunner : MonoBehaviour { }
#endif
