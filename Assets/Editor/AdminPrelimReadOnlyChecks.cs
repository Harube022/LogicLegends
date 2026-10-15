using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Opt-in checks for live PRELIM questions and nested pop-quiz/Admin pause restoration.</summary>
public static class AdminPrelimReadOnlyChecks
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        if (!SessionState.GetBool("Admin.PrelimQA", false)) return;
        SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded;
    }
    static void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "RulesOfInference" && scene.name != "PRELIM") return;
        // The imported Photon demo UI has no session/Text in this offline harness.
        foreach (var demo in UnityEngine.Object.FindObjectsByType<Photon.Pun.Demo.Cockpit.CountOfPlayersProperty>(FindObjectsInactive.Include, FindObjectsSortMode.None)) demo.enabled = false;
        var avatar = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Multiplayer_Male"));
        avatar.GetComponent<MultiplayerAvatarSync>().enabled = false;
        avatar.GetComponent<CharacterController>().enabled = true;
        if (scene.name != "RulesOfInference") return;
        var runner = new GameObject("PRELIM Admin QA").AddComponent<AdminAnswerCheckRunner>();
        UnityEngine.Object.DontDestroyOnLoad(runner.gameObject);
        runner.StartCoroutine(Run());
    }
    static IEnumerator Run()
    {
        yield return null;
        StageSelectionState.Select(2); SceneManager.LoadScene("PRELIM");
        yield return null;
        float until = Time.unscaledTime + 50;
        TruthTableTutorial tutorial = null;
        while (Time.unscaledTime < until)
        {
            tutorial = UnityEngine.Object.FindFirstObjectByType<TruthTableTutorial>();
            if (tutorial != null && tutorial.IsOpen) break;
            yield return null;
        }
        Check(tutorial != null && tutorial.IsOpen, "Truth Table Help opens");
        tutorial.CloseTutorial(); yield return null;
        var controls = TruthTableControlsOnboarding.Active;
        controls.SkipTutorial(); Invoke(controls, "ConfirmYes"); yield return null;
        var doors = UnityEngine.Object.FindFirstObjectByType<TruthTableDoorInteraction>();
        var clock = doors.GetComponent<TruthTableStageClock>();
        Move(Player.LocalInstance, doors.TutorialBook.position + Vector3.forward * 2);
        doors.ActivateFocusedInteraction(); yield return null;
        Check(clock.IsRunning, "Truth Table book starts actual stage");
        var door = doors.GetTutorialDoor(0);
        Move(Player.LocalInstance, new Vector3(door.bounds.max.x + 1, door.bounds.min.y + 1, door.bounds.center.z));
        Set(doors, "focusedDoor", door); doors.ActivateFocusedInteraction(); yield return null;
        Check(doors.AdminQuestionOpen && !Player.LocalInstance.enabled && GameInput.Instance.GameplayInputBlocked, "real unanswered pop quiz is open");
        var assigned = Field<TruthTableDoorQuestionBank.Question[]>(doors, "assignedQuestions")[0];
        var initialAnswers = string.Join(",", Field<bool[]>(doors, "answered"));
        var random = UnityEngine.Random.state;
        float remaining = clock.RemainingSeconds;
        var admin = UnityEngine.Object.FindFirstObjectByType<AdminDebugController>();
        Field<Button>(admin, "adminButton").onClick.Invoke(); yield return null;
        Check(Field<bool>(admin, "panelOpen"), "Admin can inspect current pop quiz");
        OpenViewer(admin); yield return null; yield return new WaitForSecondsRealtime(.3f);
        var viewer = Field<RectTransform>(admin, "panelRoot").GetComponent<AdminAnswerViewer>();
        var body = Field<TextMeshProUGUI>(viewer, "body");
        Check(body.text.Contains(assigned.prompt) && body.text.Contains(assigned.choices[assigned.correctChoice]), "viewer shows actual current pop quiz and answer");
        Check(remaining == clock.RemainingSeconds && initialAnswers == string.Join(",", Field<bool[]>(doors, "answered")) && random.Equals(UnityEngine.Random.state), "pop quiz viewing does not answer, reroll or consume time");
        admin.ClosePanel(); yield return null;
        Check(doors.AdminQuestionOpen && !Player.LocalInstance.enabled && GameInput.Instance.GameplayInputBlocked && !clock.IsTutorialOpen && clock.IsCountdownPaused, "Admin close restores pop quiz input and timer state");
        doors.ClosePanel();
        Check(Player.LocalInstance.enabled && !GameInput.Instance.GameplayInputBlocked, "pop quiz still restores gameplay normally");
        Debug.Log("ADMIN_PRELIM_TRUTH_PASS current column bank, pop quiz answer, nested input/timer restoration");

        StageSelectionState.Select(1); SceneManager.LoadScene("PRELIM"); yield return null;
        until = Time.unscaledTime + 50;
        PropositionalLogicTutorial guide = null;
        while (Time.unscaledTime < until)
        {
            guide = UnityEngine.Object.FindFirstObjectByType<PropositionalLogicTutorial>();
            if (guide != null && guide.IsOpen) break;
            yield return null;
        }
        Check(guide != null && guide.IsOpen, "Propositional Help opens");
        guide.CloseTutorial(); yield return null;
        var propControls = PropositionalControlsOnboarding.Active;
        propControls.SkipTutorial(); Invoke(propControls, "ConfirmYes"); yield return null;
        var quiz = UnityEngine.Object.FindFirstObjectByType<QuizManager>();
        quiz.OpenQuiz(UnityEngine.Object.FindObjectsByType<BookInteract>(FindObjectsSortMode.None).First()); yield return null;
        Check(quiz.IsQuizActive, "real Propositional question open");
        var question = Field<LogicQuestion>(quiz, "currentQuestion");
        var timer = UnityEngine.Object.FindFirstObjectByType<LevelTimerManager>();
        admin = UnityEngine.Object.FindFirstObjectByType<AdminDebugController>();
        float before = timer.RemainingTime;
        var order = string.Join(",", Field<int[]>(quiz, "currentDoorOptionOrder")); random = UnityEngine.Random.state;
        Field<Button>(admin, "adminButton").onClick.Invoke(); yield return null;
        Check(Field<bool>(admin, "panelOpen"), "Admin can inspect live Propositional question");
        OpenViewer(admin); yield return null; yield return new WaitForSecondsRealtime(.3f);
        viewer = Field<RectTransform>(admin, "panelRoot").GetComponent<AdminAnswerViewer>();
        body = Field<TextMeshProUGUI>(viewer, "body");
        Check(body.text.Contains(question.questionText) && body.text.Contains(question.options[question.correctOptionIndex]) && body.text.Contains("physical door"), "viewer shows live question and shuffled correct door");
        Check(before == timer.RemainingTime && question == Field<LogicQuestion>(quiz, "currentQuestion") && order == string.Join(",", Field<int[]>(quiz, "currentDoorOptionOrder")) && random.Equals(UnityEngine.Random.state), "Propositional viewing preserves timer, question, choice order and progression");
        admin.ClosePanel(); yield return null;
        Check(quiz.IsQuizActive && Player.LocalInstance.enabled && !GameInput.Instance.GameplayInputBlocked, "Close returns to the same live question");
        SceneManager.sceneLoaded -= Loaded;
        Debug.Log("ADMIN_PRELIM_PROPOSITIONAL_PASS live current question / answer / shuffled door / read-only timer and selection");
    }
    static void OpenViewer(AdminDebugController admin) => Field<RectTransform>(admin, "panelRoot").GetComponentsInChildren<Button>(true).Single(b => b.name == "ViewQuestionsAndAnswers").onClick.Invoke();
    static T Field<T>(object obj, string field) => (T)obj.GetType().GetField(field, Hidden).GetValue(obj);
    static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Hidden).SetValue(obj, value);
    static void Invoke(object obj, string method) => obj.GetType().GetMethod(method, Hidden).Invoke(obj, null);
    static void Check(bool okay, string message) { if (!okay) throw new InvalidOperationException("ADMIN_PRELIM_QA: " + message); }
    static void Move(Player p, Vector3 point)
    {
        var cc = p.GetComponent<CharacterController>(); cc.enabled = false;
        var hits = Physics.RaycastAll(point + Vector3.up * 5, Vector3.down, 60, ~0, QueryTriggerInteraction.Ignore).Where(h => h.collider.GetComponentInParent<Player>() == null && h.normal.y > .7f).OrderBy(h => h.distance).ToArray();
        if (hits.Length > 0) point.y = hits[0].point.y + cc.height * .5f - cc.center.y + .12f;
        p.transform.position = point; cc.enabled = true; Physics.SyncTransforms();
    }
}
