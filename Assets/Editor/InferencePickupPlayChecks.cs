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
        GameInput.Instance.MobileInteract(); yield return null;
        Check(quiz.IsOpen && quiz.IsPractice && tutorial.CurrentStep == "CrystalQuiz", "hand pickup opens practice quiz");
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
            if(challenge==0)yield return CheckHud(journey);
            stage.OpenBoard(player); yield return BoardReady(stage);
            FillPremises(stage);
            yield return CheckPartialFeedback(stage);
            Click(stage.validateButton); yield return null;
            Check(stage.Crystals.Count == 9 && stage.Crystals.Select(c => c.GetComponent<RuleDiamond>().Abbreviation).SequenceEqual(codes), "every tower receives its intended crystal");
            Check(stage.Crystals.All(c=>!c.PickupQuizCompleted),"new challenge resets every crystal's pickup quiz requirement");
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
            if(challenge==0)yield return CheckCrystalCycles(stage);
            if(challenge==0)yield return CheckTowerDetection(stage);
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
                typeof(InferenceCrystal).GetField("nameCycleElapsed",Private).SetValue(crystal,45f);
                typeof(InferenceCrystal).GetField("nameVisibility",Private).SetValue(crystal,0f);crystal.label.alpha=0;
                typeof(InferenceCrystal).GetField("bubbleCycleElapsed",Private).SetValue(crystal,45f);
                typeof(InferenceCrystal).GetField("bubbleVisibility",Private).SetValue(crystal,0f);
                typeof(InferenceCrystal).GetMethod("UpdateInteractionPresentation",Private).Invoke(crystal,new object[]{1f});
                Check(crystal.label.alpha==1&&(float)typeof(InferenceCrystal).GetField("nameCycleElapsed",Private).GetValue(crystal)==45,"nearby name stays visible and pauses cycle");
                Check(crystal.InteractionBubble.GetComponent<CanvasGroup>().alpha==1&&(float)typeof(InferenceCrystal).GetField("bubbleCycleElapsed",Private).GetValue(crystal)==45,"nearby bubble stays visible and pauses its own cycle");
                yield return new WaitForEndOfFrame();
                var tower = stage.crystalSpawns[stage.Crystals.ToList().IndexOf(crystal)].parent;
                var towerRenderers = tower.GetComponentsInChildren<MeshRenderer>(true);
                var towerBounds = towerRenderers[0].bounds;
                foreach (var renderer in towerRenderers) towerBounds.Encapsulate(renderer.bounds);
                crystal.label.ForceMeshUpdate();
                Check(!towerBounds.Contains(crystal.label.transform.position) && !crystal.label.isTextOverflowing, "rule name outside complete tower geometry: " + crystal.name);
                var crystalBounds=crystal.transform.Find("CrystalMesh").GetComponent<Renderer>().bounds;
                Check(crystal.label.transform.position.y>crystalBounds.max.y&&crystal.label.transform.position.y-crystalBounds.max.y<2f,"name sits just above the collectible, below tower roof");
                Check(crystal.label.font==StageUITheme.HeadingFont&&crystal.label.color.r>.95f&&crystal.label.fontSharedMaterial.GetFloat("_OutlineWidth")>=.12f,"heavy high-contrast outlined rule name");
                var outward = Camera.main.transform.position - towerBounds.center; outward.y = 0; outward.Normalize();
                float edge = Mathf.Abs(outward.x) * towerBounds.extents.x + Mathf.Abs(outward.z) * towerBounds.extents.z;
                var corners = new Vector3[4]; crystal.label.rectTransform.GetWorldCorners(corners);
                Check(corners.All(p => Vector3.Dot(p - towerBounds.center, outward) > edge), "entire rule label clears the tower face: "+crystal.name+" minimum="+corners.Min(p=>Vector3.Dot(p-towerBounds.center,outward))+" face="+edge);
                var toLabel = (crystal.label.transform.position - Camera.main.transform.position).normalized;
                Check(Vector3.Dot(crystal.label.transform.forward, toLabel) > .999f, "rule label faces gameplay camera");
                if (challenge == 0) yield return CheckTowerFraming(crystal, towerBounds);
                float expansionDeadline=Time.unscaledTime+1f;
                while(crystal.InteractionBubble.Expansion<1&&Time.unscaledTime<expansionDeadline)yield return null;
                Check(interaction.AvailableCrystal == crystal && crystal.InteractionBubble!=null && crystal.InteractionBubble.gameObject.activeInHierarchy && crystal.InteractionBubble.Expansion>.5f, "reachable pickup prompt at " + crystal.name);
                Check(crystal.InteractionBubble.Instruction.alpha>.99f&&crystal.InteractionBubble.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="Use the")&&crystal.InteractionBubble.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="button to interact."),"expanded bubble has fully visible hand instruction");
                Check(crystal.InteractionBubble.HandIcon.sprite==UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name=="InteractButton").image.sprite,"bubble uses actual hand-button icon");
                bool wrong = challenge == 0 && Array.IndexOf(toVisit, crystal) % 2 == 1;
                GameInput.Instance.MobileInteract();
                yield return null;
                Check(quiz.IsOpen && player.GetHeldObject() == null && !player.enabled && GameInput.Instance.GameplayInputBlocked, "hand pickup requires quiz before inventory");
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
                if(challenge==0&&Array.IndexOf(toVisit,crystal)==0)
                    foreach(int size in new[]{6,12,3})
                    {SetSize(size);yield return new WaitForSecondsRealtime(.3f);yield return CheckLayout(quiz);yield return Capture("QuizLayout");}
                Click(quiz.CollectButton); yield return null;
                Check(player.GetHeldObject() == crystal.GetComponent<GrabbableObject>() && player.enabled && !journey.InferenceQuizOpen && !GameInput.Instance.GameplayInputBlocked,
                    "answered quiz gives exact crystal and restores gameplay");
                Check(crystal.PickupQuizCompleted,"successful first collection remembers quiz completion for either answer");
                Check(!crystal.InteractionBubble.gameObject.activeInHierarchy,"pickup bubble is hidden while carried");
                typeof(InferenceCrystal).GetField("nameVisibility",Private).SetValue(crystal,0f);
                typeof(InferenceCrystal).GetMethod("UpdateInteractionPresentation",Private).Invoke(crystal,new object[]{1f});
                Check(crystal.label.alpha==1,"carried crystal name stays visible");
                if (challenge == 0)
                {
                    GameInput.Instance.MobileInteract(); yield return null;
                    Check(player.GetHeldObject() == null && !quiz.IsOpen && !crystal.GetComponent<SphereCollider>().isTrigger, "dropping uses existing accessible collision, not another pickup quiz");
                    for(int attempt=0;attempt<2;attempt++)
                    {
                        // Approach the actual dropped collider, including gems settling on raised rocks.
                        // The pillar-base helper can otherwise put the avatar below its pickup range.
                        var dropped=crystal.GetComponent<SphereCollider>();
                        var bodyOffset=player.GetComponent<CharacterController>().bounds.center-player.transform.position;
                        PrelimTeleportTransition.MovePlayerImmediately(player.gameObject,dropped.bounds.center+Vector3.back*(dropped.bounds.extents.z+1f)-bodyOffset,Quaternion.identity);
                        yield return new WaitForSecondsRealtime(.1f);
                        Check(interaction.IsWithinRange(crystal),"recollection fixture is within the actual dropped-collider range");
                        float timeBefore=journey.InferenceRemainingSeconds;string drawBefore=quiz.Current.Question.Id;
                        GameInput.Instance.MobileInteract();
                        Check(!quiz.IsOpen&&player.GetHeldObject()==crystal.GetComponent<GrabbableObject>(),"dropped crystal recollects without another quiz: "+crystal.name+" attempt="+attempt+" completed="+crystal.PickupQuizCompleted+" quiz="+quiz.IsOpen+" held="+(player.GetHeldObject()!=null?player.GetHeldObject().name:"none")+" available="+(interaction.AvailableCrystal!=null?interaction.AvailableCrystal.name:"none")+" inRange="+interaction.IsWithinRange(crystal)+" player="+player.transform.position+" gem="+crystal.transform.position+" bounds="+crystal.GetComponent<SphereCollider>().bounds);
                        Check(journey.InferenceRemainingSeconds==timeBefore&&quiz.Current.Question.Id==drawBefore,"recollection does not consume a question or charge another penalty");
                        yield return null;
                        Check(!crystal.InteractionBubble.gameObject.activeInHierarchy&&crystal.label.gameObject.activeInHierarchy,"held crystal keeps its independent rule label, without pickup prompt");
                        GameInput.Instance.MobileInteract();yield return null;
                    }
                    crystal.ReturnToOrigin();
                    Check(crystal.PickupQuizCompleted,"returning to the tower preserves answered status");
                }
            }
            if (challenge == 0)
            {
                File.AppendAllText(Report, "PASS all nine towers: first-pickup quizzes, correct/incorrect answers, two quiz-free drop/recollect attempts per crystal, no repeated questions or penalties, hidden carried prompts and visible carried names.\n");
                yield return CheckWaterRecovery(stage,toVisit[0]);
                var wrongCrystal=stage.Crystals.First(c=>c.GetComponent<RuleDiamond>().Abbreviation!=stage.Question.Rule.abbreviation);
                MoveToCrystal(player,wrongCrystal);yield return new WaitForSecondsRealtime(.35f);
                GameInput.Instance.MobileInteract();yield return null;
                Check(!quiz.IsOpen&&player.GetHeldObject()==wrongCrystal.GetComponent<GrabbableObject>(),"previously collected wrong crystal requires no repeat quiz");
                stage.OpenBoard(player);yield return BoardReady(stage);
                float wrongTime=journey.InferenceRemainingSeconds;int rounds=stage.SolvedRounds;
                stage.PlaceHeldCrystal();stage.PlaceHeldCrystal();
                Check(journey.InferenceRemainingSeconds==wrongTime-10&&stage.SolvedRounds==rounds&&stage.IsBoardOpen,"incorrect altar submission deducts exactly once and keeps board open");
                var penalty=Field<TMP_Text>(journey,"inferenceDeduction");
                Check(penalty.text=="−10s"&&penalty.gameObject.activeInHierarchy&&penalty.color.r>penalty.color.g*2,"altar −10s visible while HUD is hidden");
                yield return Capture("WrongAltarPenalty");
                stage.CloseBoard();yield return null;
                File.AppendAllText(Report,"PASS wrong altar crystal: separate single ten-second penalty, red visible deduction, unchanged progress and open board.\n");
                var correct = stage.Crystals.First(c => c.GetComponent<RuleDiamond>().Abbreviation == stage.Question.Rule.abbreviation);
                MoveToCrystal(player, correct); yield return new WaitForSecondsRealtime(.2f);
                GameInput.Instance.MobileInteract(); yield return null;
                Check(!quiz.IsOpen&&player.GetHeldObject()==correct.GetComponent<GrabbableObject>(),"previously collected correct crystal requires no repeat quiz");
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
            Check(stage.dragPuzzle.conclusion.label.text.Contains(conclusion) && (stage.feedback.text.Contains("Correct")||stage.feedback.text.Contains("completed!")), "revealed conclusion and completion prompt remain readable");
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
            if(challenge==0)yield return CheckNotice(journey);
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
        int index=Enumerable.Range(0,stage.crystalSpawns.Length).OrderBy(i=>(stage.crystalSpawns[i].position-crystal.PickupOrigin).sqrMagnitude).First();
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
            yield return new WaitForSecondsRealtime(.3f); yield return new WaitForEndOfFrame();
            var corners = new Vector3[4]; crystal.label.rectTransform.GetWorldCorners(corners);
            Check(corners.Select(p => camera.WorldToViewportPoint(p)).All(p => p.z > 0 && p.x > .02f && p.x < .98f && p.y > .02f && p.y < .98f), "full rule label fits gameplay camera frame");
            yield return Capture("TowerLabel_" + crystal.GetComponent<RuleDiamond>().Abbreviation);
            if(crystal==UnityEngine.Object.FindFirstObjectByType<InferenceChallenge>().Crystals[0])
                foreach(int mobileSize in new[]{12,3})
                {
                    SetSize(mobileSize);yield return new WaitForSecondsRealtime(.35f);yield return new WaitForEndOfFrame();
                    crystal.InteractionBubble.Rect.GetWorldCorners(corners);
                    Check(corners.Select(p=>camera.WorldToViewportPoint(p)).All(p=>p.z>0&&p.x>=0&&p.x<=1&&p.y>=0&&p.y<=1),"expanded mobile bubble remains on screen");
                    Check(crystal.InteractionBubble.GetComponentsInChildren<TMP_Text>().All(t=>!t.isTextOverflowing),"mobile hand instruction is readable without text overflow");
                    var first=camera.WorldToScreenPoint(corners[0]);var last=camera.WorldToScreenPoint(corners[2]);
                    var prompt=Rect.MinMaxRect(first.x,first.y,last.x,last.y);
                    yield return Capture("ExpandedCrystalPrompt");
                    foreach(var button in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
                    {
                        var canvas=button.GetComponentInParent<Canvas>();
                        if(canvas!=null&&canvas.rootCanvas.renderMode==RenderMode.ScreenSpaceOverlay)
                            Check(!prompt.Overlaps(ScreenRect((RectTransform)button.transform)),"mobile prompt avoids "+button.name+" prompt="+prompt+" control="+ScreenRect((RectTransform)button.transform));
                    }
                }
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
    static IEnumerator CheckHud(StageJourneyUI journey)
    {
        var map=UnityEngine.Object.FindFirstObjectByType<TruthTableMinimap>().Panel;
        var hud=journey.HudPanel;
        var jump=(RectTransform)UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name=="JumpButton").transform;
        foreach(int size in new[]{6,12,3})
        {
            SetSize(size);yield return new WaitForSecondsRealtime(.3f);yield return new WaitForEndOfFrame();
            var m=ScreenRect(map);var h=ScreenRect(hud);
            Check(Mathf.Abs(m.width-h.width)<2&&h.yMax<m.yMin&&Mathf.Abs(m.xMax-h.xMax)<2,"matching minimap and HUD width/alignment");
            Check(!h.Overlaps(ScreenRect(jump)),"compact HUD clears jump");
            var admin=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name=="AdminButton");
            Check(!h.Overlaps(ScreenRect((RectTransform)admin.transform)),"HUD clears Admin button");
            foreach(var text in hud.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();Check(!text.isTextOverflowing,"compact HUD text fits: "+text.name);}
            yield return Capture("CompactHud");
        }
        File.AppendAllText(Report,"PASS map/HUD matching width, vertical safe-area dock, readable title/timer/progress, clear jump at three resolutions.\n");
    }
    static IEnumerator CheckCrystalCycles(InferenceChallenge stage)
    {
        var method=typeof(InferenceCrystal).GetMethod("UpdateInteractionPresentation",Private);
        foreach(var crystal in stage.Crystals)
        {
            typeof(InferenceCrystal).GetField("nameCycleElapsed",Private).SetValue(crystal,0f);
            typeof(InferenceCrystal).GetField("nameVisibility",Private).SetValue(crystal,1f);
            typeof(InferenceCrystal).GetField("bubbleCycleElapsed",Private).SetValue(crystal,0f);
            typeof(InferenceCrystal).GetField("bubbleVisibility",Private).SetValue(crystal,1f);
            for(int frame=1;frame<=288;frame++)
            {
                method.Invoke(crystal,new object[]{.5f});float phase=frame*.5f%72;
                Check(phase<=10?crystal.label.alpha==1:phase>=11&&phase<=71?crystal.label.alpha==0:crystal.label.alpha>0&&crystal.label.alpha<1,"ten visible/sixty hidden name cycle: "+crystal.name);
                float bubble=crystal.InteractionBubble.GetComponent<CanvasGroup>().alpha;
                Check(phase<=10?bubble==1:phase>=11&&phase<=71?bubble==0:bubble>0&&bubble<1,"ten visible/sixty hidden bubble cycle: "+crystal.name);
            }
            Check(crystal.InteractionBubble!=null&&crystal.InteractionBubble.Marker.font==StageUITheme.ReadingFont,"readable yellow crystal bubble exists");
            Check(crystal.label.text.Contains(crystal.GetComponent<RuleDiamond>().DisplayName)&&!crystal.label.transform.IsChildOf(crystal.InteractionBubble.transform),"correct rule name is separate from its bubble");
            typeof(InferenceCrystal).GetField("bubbleCycleElapsed",Private).SetValue(crystal,45f);
            typeof(InferenceCrystal).GetField("bubbleVisibility",Private).SetValue(crystal,0f);
            method.Invoke(crystal,new object[]{0f});
            Check(crystal.label.gameObject.activeInHierarchy&&crystal.label.alpha==1&&crystal.InteractionBubble.GetComponent<CanvasGroup>().alpha==0,"hiding bubble leaves the name label visible");
            typeof(InferenceCrystal).GetField("bubbleCycleElapsed",Private).SetValue(crystal,0f);
            typeof(InferenceCrystal).GetField("bubbleVisibility",Private).SetValue(crystal,1f);
            typeof(InferenceCrystal).GetField("nameCycleElapsed",Private).SetValue(crystal,45f);
            typeof(InferenceCrystal).GetField("nameVisibility",Private).SetValue(crystal,0f);
            method.Invoke(crystal,new object[]{0f});
            Check(crystal.label.alpha==0&&crystal.InteractionBubble.GetComponent<CanvasGroup>().alpha==1,"name cycle does not hide the bubble");
            typeof(InferenceCrystal).GetField("nameCycleElapsed",Private).SetValue(crystal,0f);
            typeof(InferenceCrystal).GetField("nameVisibility",Private).SetValue(crystal,1f);
            method.Invoke(crystal,new object[]{0f});
        }
        Check(UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None).All(b=>b.name!="CollectRuleCrystalButton"),"no dedicated world pickup button");
        yield return null;
        File.AppendAllText(Report,"PASS all nine independent name/bubble cycles: ten seconds visible, smooth fades, sixty seconds hidden; actual rule labels restored.\n");
    }
    static IEnumerator CheckNotice(StageJourneyUI journey)
    {
        var notice=Field<PrelimChallengeCompletionNotice>(journey,"inferenceNotice");
        var panel=typeof(PrelimChallengeCompletionNotice).GetField("panel",Private).GetValue(notice) as RectTransform;
        var stage=UnityEngine.Object.FindFirstObjectByType<InferenceChallenge>();
        int solved=stage.SolvedRounds;float seconds=journey.InferenceRemainingSeconds;
        Check(!panel.gameObject.activeInHierarchy,"waiting prompt is on the board, not below the HUD");
        stage.OpenBoard(Player.LocalInstance);yield return BoardReady(stage);
        foreach(int size in new[]{6,12,3})
        {
            SetSize(size);yield return new WaitForSecondsRealtime(.3f);yield return new WaitForEndOfFrame();
            Check(stage.feedback.text=="Challenge "+solved+" completed! Interact with the book statue to start Challenge "+(solved+1)+".","board uses exact completed/next numbers");
            Check(stage.feedback.font==stage.completionPromptFont&&stage.completionPromptFont==StageUITheme.HeadingFont,"completion uses the inspected Setssss board font");
            stage.feedback.ForceMeshUpdate();Check(!stage.feedback.isTextOverflowing,"completion board text fits on mobile");
            Check(!stage.HasStarted&&stage.SolvedRounds==solved&&journey.InferenceRemainingSeconds==seconds,"reading completion prompt cannot resume time or change progression");
            yield return Capture("CompletionNotice");
        }
        stage.CloseBoard();yield return null;
        File.AppendAllText(Report,"PASS exact board-only completion prompt with Setssss's IMPACT font at three sizes, paused timer, unchanged progression.\n");
    }
    static IEnumerator CheckTowerDetection(InferenceChallenge stage)
    {
        var player=Player.LocalInstance;var interaction=stage.GetComponent<InferenceCrystalInteraction>();
        var method=typeof(InferenceCrystal).GetMethod("UpdateInteractionPresentation",Private);
        foreach(var crystal in stage.Crystals)
        {
            var collider=crystal.GetComponent<SphereCollider>();
            var offset=player.GetComponent<CharacterController>().bounds.center-player.transform.position;
            var point=collider.bounds.center+Vector3.right*(collider.bounds.extents.x+interaction.interactionRange+2f)-offset;
            PrelimTeleportTransition.MovePlayerImmediately(player.gameObject,point,Quaternion.identity);
            typeof(InferenceCrystal).GetField("bubbleCycleElapsed",Private).SetValue(crystal,45f);
            typeof(InferenceCrystal).GetField("bubbleVisibility",Private).SetValue(crystal,0f);
            typeof(InferenceCrystal).GetField("nameCycleElapsed",Private).SetValue(crystal,45f);
            typeof(InferenceCrystal).GetField("nameVisibility",Private).SetValue(crystal,0f);
            crystal.InteractionBubble.SetExpansion(0);
            method.Invoke(crystal,new object[]{0f});
            Check(interaction.IsWithinDetectionRange(crystal)&&!interaction.IsWithinRange(crystal),"detection area is wider than pickup range");
            Check(crystal.InteractionBubble.GetComponent<CanvasGroup>().alpha==1&&crystal.InteractionBubble.Marker.alpha==1,"detection immediately restores the yellow !, even during hidden phase");
            Check(crystal.label.alpha==1&&Field<float>(crystal,"nameCycleElapsed")==45f,"detection immediately reveals the rule name and holds its cycle");
            PrelimTeleportTransition.MovePlayerImmediately(player.gameObject,point+Vector3.right*20,Quaternion.identity);
            method.Invoke(crystal,new object[]{1f});
            Check(crystal.InteractionBubble.GetComponent<CanvasGroup>().alpha==0,"departing detection resumes the paused visibility cycle");
            Check(crystal.label.alpha==0,"departing detection resumes the independent name cycle");
        }
        Move(player,UnityEngine.Object.FindFirstObjectByType<InferenceBookStart>().transform.position+Vector3.forward*3);
        yield return null;
        File.AppendAllText(Report,"PASS all nine tower detection overrides: immediate ! outside pickup range, resumed cycle on exit, full hand text/icon in pickup range.\n");
    }
    static IEnumerator CheckWaterRecovery(InferenceChallenge stage,InferenceCrystal crystal)
    {
        var water=GameObject.Find("Ocean50x50W750H750").GetComponent<Renderer>();
        var player=Player.LocalInstance;var quiz=stage.GetComponent<InferenceCrystalQuiz>();
        var body=crystal.GetComponent<Rigidbody>();var collider=crystal.GetComponent<SphereCollider>();
        int id=crystal.GetInstanceID(),count=stage.Crystals.Count;
        string question=crystal.QuestionId,answer=crystal.Answer;
        bool completed=crystal.PickupQuizCompleted;
        var solid=typeof(InferenceCrystal).GetMethod("RestoreSolidCollider",Private);
        var tick=typeof(InferenceCrystal).GetMethod("UpdateWaterRecovery",Private);
        var waterPoint=water.bounds.center+Vector3.right*40;
        solid.Invoke(crystal,null);collider.enabled=true;
        crystal.transform.rotation=Quaternion.identity;
        crystal.transform.position=new Vector3(waterPoint.x,water.bounds.max.y+collider.radius-.15f,waterPoint.z);
        body.isKinematic=false;body.useGravity=true;body.linearVelocity=Vector3.down;
        Physics.SyncTransforms();
        yield return null;yield return null;
        Check(crystal.IsRecovering,"submerged crystal starts an upward/outward recovery hop");
        Check(!collider.enabled&&!stage.GetComponent<InferenceCrystalInteraction>().CanShowBubble(crystal),"recovery hides pickup prompt and prevents mid-flight grabs");
        var start=crystal.transform.position;
        yield return new WaitForSecondsRealtime(.3f);
        Check(crystal.transform.position.y>start.y,"water ejection rises above the water");
        yield return new WaitForSecondsRealtime(.8f);
        Check(!crystal.IsRecovering&&collider.enabled&&!collider.isTrigger&&collider.bounds.min.y>water.bounds.max.y,"recovered crystal rests above dry ground");
        Check((bool)typeof(InferenceCrystal).GetMethod("SupportedLanding",Private).Invoke(crystal,new object[]{crystal.transform.position}),"landing has full ground support and clear scenery");
        int recoveries=Field<int>(crystal,"waterRecoveryCount");var landed=crystal.transform.position;
        yield return new WaitForSecondsRealtime(2.2f);
        Check(Field<int>(crystal,"waterRecoveryCount")==recoveries&&Vector3.Distance(landed,crystal.transform.position)<.01f,"landed crystal does not drift back or repeatedly recover");
        Check(crystal.GetInstanceID()==id&&stage.Crystals.Count==count&&crystal.QuestionId==question&&crystal.Answer==answer&&crystal.PickupQuizCompleted==completed,"water recovery preserves instance, identity and completed quiz");
        var offset=player.GetComponent<CharacterController>().bounds.center-player.transform.position;
        PrelimTeleportTransition.MovePlayerImmediately(player.gameObject,collider.bounds.center+Vector3.back*(collider.bounds.extents.z+1)-offset,Quaternion.identity);
        yield return new WaitForSecondsRealtime(.1f);
        float seconds=StageJourneyUI.Instance.InferenceRemainingSeconds;
        GameInput.Instance.MobileInteract();
        Check(!quiz.IsOpen&&player.GetHeldObject()==crystal.GetComponent<GrabbableObject>()&&StageJourneyUI.Instance.InferenceRemainingSeconds==seconds,"recovered answered crystal recollects without another quiz or penalty");
        // Exercise an intentional hand drop while standing in the pond as well.
        Move(player,waterPoint);yield return null;
        GameInput.Instance.MobileInteract();
        var dropPosition=crystal.transform.position;
        float dropDeadline=Time.unscaledTime+1f;
        while(Field<int>(crystal,"waterRecoveryCount")==recoveries&&Time.unscaledTime<dropDeadline)yield return null;
        Check(player.GetHeldObject()==null&&Field<int>(crystal,"waterRecoveryCount")==recoveries+1,"intentional water drop also triggers recovery once: held="+(player.GetHeldObject()!=null)+" count="+Field<int>(crystal,"waterRecoveryCount")+" expected="+(recoveries+1)+" drop="+dropPosition+" now="+crystal.transform.position+" player="+player.transform.position+" kinematic="+body.isKinematic+" enabled="+collider.enabled);
        yield return new WaitForSecondsRealtime(1f);
        Check(!crystal.IsRecovering&&collider.enabled&&crystal.PickupQuizCompleted==completed,"intentional recovery remains collectible and remembers its quiz");
        // Block every nearby landing in the fixture to check the tower fallback.
        var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.name="QA Shore Blocker";
        blocker.transform.position=waterPoint;blocker.transform.localScale=new Vector3(70,100,70);
        try
        {
            typeof(InferenceCrystal).GetField("recoveryCooldown",Private).SetValue(crystal,0f);
            solid.Invoke(crystal,null);collider.enabled=true;
            crystal.transform.position=new Vector3(waterPoint.x,water.bounds.max.y-1,waterPoint.z);
            body.isKinematic=false;Physics.SyncTransforms();tick.Invoke(crystal,new object[]{0f});
            Check(!crystal.IsRecovering&&Vector3.Distance(crystal.transform.position,crystal.PickupOrigin)<.01f&&collider.enabled&&collider.isTrigger,"no clear shore returns the same crystal to its original tower pickup");
            Check(crystal.PickupQuizCompleted==completed&&crystal.GetInstanceID()==id,"tower fallback preserves quiz completion and identity");
        }
        finally{UnityEngine.Object.Destroy(blocker);}
        yield return null;
        File.AppendAllText(Report,"PASS accidental and intentional water drops: upward/outward hop, supported dry landing, no repeated recovery, same identity/quiz state, quiz-free recollection, and blocked-shore tower fallback.\n");
    }
    static Rect ScreenRect(RectTransform rect){var corners=new Vector3[4];rect.GetWorldCorners(corners);return Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);}
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
