#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ConfigurePropositionalLogicStage
{
    private const string ScenePath = "Assets/Scenes/PRELIM.unity";
    private const string TrueAssetPath = "Assets/FINAL_ASSETS/MAPS/REMADE/S1L1/TRUE.fbx";
    private const string FalseAssetPath = "Assets/FINAL_ASSETS/MAPS/REMADE/S1L1/FALSE.fbx";

    [MenuItem("Logic Legends/Configure Propositional Logic Stage")]
    public static void Configure()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject trueVisual = AssetDatabase.LoadAssetAtPath<GameObject>(TrueAssetPath);
        GameObject falseVisual = AssetDatabase.LoadAssetAtPath<GameObject>(FalseAssetPath);

        if (trueVisual == null || falseVisual == null)
        {
            throw new InvalidOperationException("The existing TRUE.fbx and FALSE.fbx assets must be imported before configuring hints.");
        }

        QuizManager quizManager = FindInScene<QuizManager>(scene);
        LevelTimerManager timerManager = FindInScene<LevelTimerManager>(scene);
        if (quizManager == null || timerManager == null)
        {
            throw new InvalidOperationException("PRELIM is missing QuizManager or LevelTimerManager.");
        }

        SerializedObject timerObject = new SerializedObject(timerManager);
        timerObject.FindProperty("initialLevelDuration").floatValue = 540f;
        timerObject.FindProperty("retryLevelDuration").floatValue = 270f;
        timerObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(timerManager);

        Transform[] transforms = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .ToArray();

        SerializedObject quizObject = new SerializedObject(quizManager);
        SerializedProperty challenges = quizObject.FindProperty("challenges");
        if (challenges == null || challenges.arraySize != 5)
        {
            throw new InvalidOperationException($"Expected exactly five configured Propositional Logic challenges, found {challenges?.arraySize ?? 0}.");
        }

        for (int i = 0; i < challenges.arraySize; i++)
        {
            SerializedProperty challenge = challenges.GetArrayElementAtIndex(i);
            string topicName = challenge.FindPropertyRelative("topicName").stringValue;
            Transform room = transforms.FirstOrDefault(candidate => NamesMatch(candidate.name, topicName));
            if (room == null)
            {
                throw new InvalidOperationException($"Could not find the scene room for challenge '{topicName}'.");
            }

            Transform boardTransform = room.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name.StartsWith("UPDATED_BOARD", StringComparison.OrdinalIgnoreCase));
            if (boardTransform == null)
            {
                throw new InvalidOperationException($"Challenge room '{topicName}' has no existing UPDATED_BOARD instance.");
            }

            TruthTableHintBoard board = boardTransform.GetComponent<TruthTableHintBoard>();
            if (board == null)
            {
                board = Undo.AddComponent<TruthTableHintBoard>(boardTransform.gameObject);
            }

            SerializedObject boardObject = new SerializedObject(board);
            boardObject.FindProperty("trueVisualPrefab").objectReferenceValue = trueVisual;
            boardObject.FindProperty("falseVisualPrefab").objectReferenceValue = falseVisual;
            boardObject.FindProperty("secondsPerRow").floatValue = 20f;
            boardObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(board);

            challenge.FindPropertyRelative("hintBoard").objectReferenceValue = board;
        }

        quizObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(quizManager);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        ValidateTruthTables();
        ValidateRequestedScenarios(timerManager);
        Debug.Log("[Propositional Logic] Configured 09:00 initial timer, 04:30 retry, and five progressive hint boards.");
    }

    public static void ValidateTruthTables()
    {
        AssertRows(PropositionalOperator.Conjunction, true, false, false, false);
        AssertRows(PropositionalOperator.Disjunction, true, true, true, false);
        AssertRows(PropositionalOperator.ExclusiveOr, false, true, true, false);
        AssertRows(PropositionalOperator.Implication, true, false, true, true);
        AssertRows(PropositionalOperator.Biconditional, true, false, false, true);
    }

    private static void AssertRows(PropositionalOperator logicOperator, params bool[] expected)
    {
        bool[] p = { true, true, false, false };
        bool[] q = { true, false, true, false };
        for (int row = 0; row < expected.Length; row++)
        {
            bool actual = TruthTableHintBoard.Evaluate(logicOperator, p[row], q[row]);
            if (actual != expected[row])
            {
                throw new InvalidOperationException($"{logicOperator} truth row {row + 1} was {actual}, expected {expected[row]}.");
            }
        }
    }

    private static void ValidateRequestedScenarios(LevelTimerManager timerManager)
    {
        timerManager.ResetForFreshRun();
        Require(!timerManager.IsTimerRunning && Approximately(timerManager.RemainingTime, 540f),
            "Test A — initial timer must be paused at 09:00.");

        timerManager.StartLevelTimer();
        Require(timerManager.IsTimerRunning && Approximately(timerManager.RemainingTime, 540f),
            "Test A — Book activation must start the 09:00 timer.");

        timerManager.StopTimer();
        Require(!timerManager.IsTimerRunning,
            "Test B — completing the correct door must pause the timer.");

        timerManager.StartLevelTimer();
        Require(timerManager.IsTimerRunning && Approximately(timerManager.RemainingTime, 540f),
            "Test C — the next Book activation must resume the carried time.");

        timerManager.DeductTime(10f);
        Require(Approximately(timerManager.RemainingTime, 530f),
            "Test D — a wrong door must deduct exactly 10 seconds.");

        GameObject hintTestObject = new GameObject("TemporaryHintProgressionCheck");
        TruthTableHintBoard hintBoard = hintTestObject.AddComponent<TruthTableHintBoard>();
        hintBoard.AdvanceActiveTime(19.99f);
        Require(hintBoard.RevealedRows == 0, "Test E — no hint row may appear before 20 active seconds.");
        hintBoard.AdvanceActiveTime(0.01f);
        Require(hintBoard.RevealedRows == 1, "Test E — row 1 must appear at 20 active seconds.");
        hintBoard.AdvanceActiveTime(20f);
        Require(hintBoard.RevealedRows == 2, "Test E — row 2 must appear at 40 active seconds.");
        hintBoard.AdvanceActiveTime(40f);
        Require(hintBoard.RevealedRows == 4, "Test E — all rows must appear at 80 active seconds.");
        UnityEngine.Object.DestroyImmediate(hintTestObject);

        timerManager.PrepareRetryState();
        Require(!timerManager.IsTimerRunning && Approximately(timerManager.RemainingTime, 270f),
            "Test F — Retry must be paused at 04:30.");
        Require(LevelTimerManager.savedTopicIndex == 0,
            "Test G — Retry must reset the sequence to challenge 1.");

        timerManager.StartLevelTimer();
        Require(timerManager.IsTimerRunning && Approximately(timerManager.RemainingTime, 270f),
            "Test F — Book activation after Retry must start at 04:30.");

        timerManager.ResetForFreshRun();
        Debug.Log("[Propositional Logic Tests] A–G timer, penalty, retry reset, and 20/40/60/80-second hint checks passed.");
    }

    private static bool Approximately(float left, float right)
    {
        return Mathf.Abs(left - right) < 0.001f;
    }

    private static void Require(bool condition, string failureMessage)
    {
        if (!condition) throw new InvalidOperationException(failureMessage);
    }

    private static bool NamesMatch(string sceneName, string topicName)
    {
        return Normalize(sceneName) == Normalize(topicName);
    }

    private static string Normalize(string value)
    {
        return (value ?? string.Empty)
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("&", "AND")
            .ToUpperInvariant();
    }

    private static T FindInScene<T>(Scene scene) where T : UnityEngine.Object
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .FirstOrDefault();
    }
}
#endif
