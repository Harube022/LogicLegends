#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PropositionalLogicRegressionChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

    [MenuItem("Logic Legends/Verify Book Activation and Hint Assets")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run these checks outside Play Mode.");
        int savedTopic = LevelTimerManager.savedTopicIndex;
        float savedTime = LevelTimerManager.savedRemainingTime;
        bool savedRetry = LevelTimerManager.isRespawningFromFail;
        int savedTryAgainCount = (int)typeof(LevelTimerManager)
            .GetField("tryAgainCount", StaticPrivate).GetValue(null);
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject root = new GameObject("BookActivationRegression");
            SceneManager.MoveGameObjectToScene(root, scene);
            var timer = root.AddComponent<LevelTimerManager>();
            timer.ResetForFreshRun();
            var quiz = root.AddComponent<QuizManager>();
            var panel = Child(root, "QuizPanel");
            panel.SetActive(false);
            var premiseObject = new GameObject("QuestionText", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
            premiseObject.transform.SetParent(panel.transform, false);
            var premiseText = premiseObject.GetComponent<TMPro.TextMeshProUGUI>();
            Set(quiz, "timerManager", timer);
            Set(quiz, "quizPanel", panel);
            Set(quiz, "questionTextUI", premiseText);
            var challenges = new List<TopicChallenge>();
            for (int i = 0; i < 5; i++)
            {
                var roomCanvas = Child(root, "RoomCanvas" + i).AddComponent<Canvas>();
                for (int doorIndex = 0; doorIndex < 4; doorIndex++)
                {
                    var choiceObject = new GameObject("DoorChoice" + doorIndex, typeof(RectTransform),
                        typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
                    choiceObject.transform.SetParent(roomCanvas.transform, false);
                }

                var questionPool = new List<LogicQuestion>();
                for (int questionIndex = 0; questionIndex < 4; questionIndex++)
                {
                    questionPool.Add(new LogicQuestion {
                        questionText = $"Regression premise {i}-{questionIndex}",
                        options = new[] {
                            $"A{i}-{questionIndex}", $"B{i}-{questionIndex}",
                            $"C{i}-{questionIndex}", $"D{i}-{questionIndex}" },
                        correctOptionIndex = questionIndex });
                }

                challenges.Add(new TopicChallenge { topicName = "Regression " + i,
                    roomChoiceCanvas = roomCanvas,
                    questionsPool = questionPool });
            }
            Set(quiz, "challenges", challenges);
            var order = (List<int>)typeof(QuizManager).GetField("challengeOrder", Private).GetValue(quiz);
            for (int i = 0; i < 5; i++) order.Add(i);
            var player = Child(root, "Player");
            player.tag = "Player";
            var collider = player.AddComponent<BoxCollider>();

            for (int i = 0; i < 5; i++)
            {
                var book = Child(root, "Book" + i).AddComponent<BookInteract>();
                var button = Child(root, "Read" + i).AddComponent<Button>();
                Set(book, "quizManager", quiz);
                Set(book, "timerManager", timer);
                Set(book, "interactButton", button.gameObject);
                Call(book, "Start");
                float carried = timer.RemainingTime;
                Require(!timer.IsTimerRunning, "Entering challenge " + i + " must remain paused.");
                Call(book, "OnTriggerEnter", collider);
                Require(button.gameObject.activeSelf, "Paused challenge must offer Book activation.");
                button.onClick.Invoke();
                Require(quiz.IsQuizActive && timer.IsTimerRunning, "Book click must reveal quiz and resume timer.");
                LogicQuestion currentQuestion = ReadCurrentQuestion(quiz);
                Require(premiseText.enabled && currentQuestion != null &&
                    premiseText.text == currentQuestion.questionText &&
                    challenges[i].questionsPool.Contains(currentQuestion),
                    "Book activation must display a premise from the current challenge pool.");
                Require(Mathf.Approximately(timer.RemainingTime, carried), "Book must preserve carried time.");
                int[] previousDoorOrder = ReadDoorOrder(quiz);
                string previousPremise = premiseText.text;
                var seenPremises = new HashSet<string> { previousPremise };
                Require(IsPermutation(previousDoorOrder), "Every option must be assigned to exactly one door.");
                Require(CountCorrectDoors(quiz) == 1, "Exactly one physical door must hold the correct answer.");

                // Four wrong-answer cycles exercise every premise in the pool and
                // then the first selection after the pool is reshuffled.
                for (int repeatAttempt = 0; repeatAttempt < 4; repeatAttempt++)
                {
                    quiz.ClearQuizUI();
                    timer.DeductTime(10f);
                    book.ResetInteraction();
                    carried = timer.RemainingTime;
                    // Exercise recovery while still inside the trigger, with no enter callback.
                    button.onClick.RemoveAllListeners();
                    Call(book, "OnTriggerStay", collider);
                    button.onClick.Invoke();
                    Require(quiz.IsQuizActive && timer.IsTimerRunning, "Wrong-answer Book reactivation must work.");
                    Require(Mathf.Approximately(timer.RemainingTime, carried), "Repeated activation must not reset time.");
                    Require((int)Get(quiz, "currentTopicIndex") == i,
                        "Wrong answers must not advance or change the active challenge.");

                    currentQuestion = ReadCurrentQuestion(quiz);
                    Require(currentQuestion != null && challenges[i].questionsPool.Contains(currentQuestion) &&
                        premiseText.text == currentQuestion.questionText,
                        "Reactivation must select and display a premise from the same challenge pool.");
                    Require(premiseText.text != previousPremise,
                        "Consecutive Book activations must not repeat the same premise.");
                    if (repeatAttempt < 3)
                    {
                        Require(seenPremises.Add(premiseText.text),
                            "Every premise must be used once before the pool repeats.");
                    }
                    else
                    {
                        Require(seenPremises.Count == 4,
                            "The full premise pool must be exhausted before a new cycle begins.");
                    }

                    int[] currentDoorOrder = ReadDoorOrder(quiz);
                    Require(IsPermutation(currentDoorOrder), "Reshuffled choices must remain a complete permutation.");
                    Require(!OrdersMatch(previousDoorOrder, currentDoorOrder),
                        "Wrong-answer reactivation must change the physical door order.");
                    Require(CountCorrectDoors(quiz) == 1,
                        "The new premise must have exactly one correctly mapped physical door.");
                    var doorTexts = challenges[i].roomChoiceCanvas.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
                    for (int doorIndex = 0; doorIndex < doorTexts.Length; doorIndex++)
                    {
                        Require(doorTexts[doorIndex].text == currentQuestion.options[currentDoorOrder[doorIndex]],
                            "Each door label must come from the newly selected premise's answer set.");
                    }

                    previousPremise = premiseText.text;
                    previousDoorOrder = currentDoorOrder;
                }
                quiz.FinalizeChallengeCompletion();
                // Prevent the final transition from touching real scene singletons.
                if (i < 4) quiz.AdvanceToNextChallenge();
                else timer.StopTimer();
                Require(!timer.IsTimerRunning, "Correct answer must pause countdown.");
            }

            VerifyGameOverChoices(root, timer);

            ConfigurePropositionalLogicStage.ValidateTruthTables();
            foreach (PropositionalOperator op in Enum.GetValues(typeof(PropositionalOperator)))
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FINAL_ASSETS/MAPS/REMADE/S1L1/UPDATED_BOARD.fbx");
                var boardObject = UnityEngine.Object.Instantiate(model, root.transform);
                int predefinedChildren = boardObject.transform.childCount;
                var board = boardObject.AddComponent<TruthTableHintBoard>();
                Set(board, "trueVisualPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FINAL_ASSETS/MAPS/REMADE/S1L1/TRUE.fbx"));
                Set(board, "falseVisualPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FINAL_ASSETS/MAPS/REMADE/S1L1/FALSE.fbx"));
                board.BeginChallenge(op);
                Require(boardObject.transform.childCount == predefinedChildren + 4, "Only four result-column outputs may be generated.");
                Require(boardObject.GetComponentsInChildren<TMPro.TMP_Text>(true).Length == 0,
                    "The baked board headings must not receive duplicate runtime TMP labels.");
                var outputs = (GameObject[])typeof(TruthTableHintBoard).GetField("outputVisuals", Private).GetValue(board);
                foreach (var output in outputs)
                {
                    Require(output != null && !output.activeSelf && output.name.StartsWith("Hint_2_"), "Results must begin hidden in column 3.");
                    Require(Mathf.Abs(Mathf.DeltaAngle(output.transform.localEulerAngles.x, 90f)) < 0.01f, "Answer X rotation must be 90 degrees.");
                }
                board.AdvanceActiveTime(19f);
                Require(board.RevealedRows == 0, "Hints must wait for 20 active seconds.");
                for (int row = 0; row < 4; row++)
                {
                    board.AdvanceActiveTime(row == 0 ? 1f : 20f);
                    Require(board.RevealedRows == row + 1 && outputs[row].activeSelf, "Progressive hint row must appear.");
                }
            }
            Debug.Log("[Propositional Regression] PASS: five challenges with four wrong-answer cycles each, nonrepeating same-topic premise pools, matched answer sets, changed door permutations, carried time, four transitions, Start Over/Try Again/Study states, all truth tables, output-only assets, X=90 and progressive hints.");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            LevelTimerManager.savedTopicIndex = savedTopic;
            LevelTimerManager.savedRemainingTime = savedTime;
            LevelTimerManager.isRespawningFromFail = savedRetry;
            typeof(LevelTimerManager).GetField("tryAgainCount", StaticPrivate).SetValue(null, savedTryAgainCount);
        }
    }

    private static void VerifyGameOverChoices(GameObject root, LevelTimerManager timer)
    {
        LevelTimerManager.ResetSession();
        var startOver = UiButton(root, "StartOver");
        var middle = UiButton(root, "MiddleAction");
        var mainMenu = UiButton(root, "MainMenu");
        var labelObject = new GameObject("MiddleLabel", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Text));
        labelObject.transform.SetParent(middle.transform, false);
        var middleLabel = labelObject.GetComponent<Text>();

        Set(timer, "startOverButton", startOver);
        Set(timer, "middleActionButton", middle);
        Set(timer, "mainMenuButton", mainMenu);
        Set(timer, "middleActionLabel", middleLabel);
        Call(timer, "UpdateGameOverOptions");
        Require(middleLabel.text == "TRY AGAIN", "The initial middle Game Over action must be Try Again.");
        Require(startOver.interactable && middle.interactable && mainMenu.interactable,
            "All three Game Over actions must be available.");

        Require(timer.TryPrepareTryAgainState(), "The first Try Again must be accepted.");
        Require(LevelTimerManager.TryAgainCount == 1 && Mathf.Approximately(timer.RemainingTime, 270f) &&
            !timer.IsTimerRunning && LevelTimerManager.isRespawningFromFail,
            "The first Try Again must prepare a paused 04:30 run.");
        Require(timer.TryPrepareTryAgainState(), "The second Try Again must be accepted.");
        Require(LevelTimerManager.TryAgainCount == 2 && Mathf.Approximately(timer.RemainingTime, 270f) &&
            !timer.IsTimerRunning,
            "The second Try Again must prepare a paused 04:30 run.");
        Call(timer, "UpdateGameOverOptions");
        Require(middleLabel.text == "STUDY IN LOGIC GARDEN LIBRARY" && timer.IsStudyOptionAvailable,
            "After two retries, the middle action must change to the Logic Garden study option.");
        Require(!timer.TryPrepareTryAgainState() && LevelTimerManager.TryAgainCount == 2,
            "A third Try Again must be unavailable.");

        timer.PrepareStartOverState();
        Call(timer, "UpdateGameOverOptions");
        Require(LevelTimerManager.TryAgainCount == 0 && Mathf.Approximately(timer.RemainingTime, 540f) &&
            Mathf.Approximately(LevelTimerManager.savedRemainingTime, 540f) && !timer.IsTimerRunning &&
            !LevelTimerManager.isRespawningFromFail && middleLabel.text == "TRY AGAIN",
            "Start Over must reset the session to a paused 09:00 run and restore Try Again.");
    }

    private static Button UiButton(GameObject parent, string name)
    {
        var buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent.transform, false);
        return buttonObject.GetComponent<Button>();
    }

    private static GameObject Child(GameObject parent, string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent.transform, false);
        return child;
    }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static object Get(object target, string field) => target.GetType().GetField(field, Private).GetValue(target);
    private static void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
    private static LogicQuestion ReadCurrentQuestion(QuizManager quiz) => (LogicQuestion)Get(quiz, "currentQuestion");
    private static int[] ReadDoorOrder(QuizManager quiz)
    {
        var order = new int[4];
        for (int i = 0; i < order.Length; i++) order[i] = quiz.GetOptionIndexForDoor(i);
        return order;
    }
    private static int CountCorrectDoors(QuizManager quiz)
    {
        int count = 0;
        for (int i = 0; i < 4; i++) if (quiz.IsChoiceCorrect(i)) count++;
        return count;
    }
    private static bool IsPermutation(int[] order)
    {
        var seen = new bool[order.Length];
        foreach (int value in order)
        {
            if (value < 0 || value >= order.Length || seen[value]) return false;
            seen[value] = true;
        }
        return true;
    }
    private static bool OrdersMatch(int[] left, int[] right)
    {
        if (left.Length != right.Length) return false;
        for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
        return true;
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
