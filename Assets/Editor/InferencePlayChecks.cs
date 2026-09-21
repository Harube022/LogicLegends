using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LogicLegends.Inference;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>Opt-in integration checks against the actual player/scene in Play mode.</summary>
public static class InferencePlayChecks
{
    [MenuItem("Tools/Logic Legends/Validate Inference Content")]
    static void ContentMenu() => Debug.Log(InferenceSetup.ValidateContent());

    [MenuItem("Tools/Logic Legends/Test Inference Flow (Play Mode)")]
    static void PlayMenu() => Debug.Log(Run());

    public static string Run()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Run only in PRELIM Play mode after the player has spawned. This test advances the local challenge.");
        var c = UnityEngine.Object.FindFirstObjectByType<InferenceChallenge>(FindObjectsInactive.Include);
        var p = Player.LocalInstance;
        if (c == null || p == null) throw new InvalidOperationException("Inference area and local player are required.");
        c.transform.parent.gameObject.SetActive(true);
        c.CloseBoard(); c.OpenBoard(p);
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var fields = (List<TMP_InputField>)typeof(InferenceChallenge).GetField("fields", flags).GetValue(c);
        var tokens = (List<string>)typeof(InferenceChallenge).GetField("tokens", flags).GetValue(c);
        var newRound = typeof(InferenceChallenge).GetMethod("NewRound", flags);
        var conclusions = new HashSet<string>();
        int completions = 0;
        UnityEngine.Events.UnityAction onComplete = () => completions++;
        c.onChallengeCompleted.AddListener(onComplete);
        bool alreadyComplete = c.PuzzleCompleted;
        try
        {
            newRound.Invoke(c, null);
            int initialRounds = c.SolvedRounds;
            c.ValidateBoard();
            Require(c.SolvedRounds == initialRounds, "Empty answers were accepted.");
            Fill(c, fields, tokens);
            string id = c.Question.Id, answer = fields[0].text;
            c.CloseBoard(); Require(p.enabled && !c.focusCamera.activeSelf, "Controls were not restored.");
            c.OpenBoard(p);
            Require(c.Question.Id == id && fields[0].text == answer, "Exploration lost progress.");
            var current = c.GetComponentsInChildren<InferenceCrystal>().Where(x => x.QuestionId == id).ToArray();
            var wrong = current.First(x => x.Answer != c.Question.Conclusion);
            Hold(p, wrong); c.PlaceHeldCrystal(); c.ValidateBoard();
            Require(c.SolvedRounds == initialRounds && !wrong.IsPlaced, "Wrong crystal was accepted.");
            var correct = current.First(x => x.Answer == c.Question.Conclusion);
            correct.Configure("stale-question", correct.Answer, c.transform);
            Hold(p, correct); c.PlaceHeldCrystal();
            Require(!correct.IsPlaced && p.GetHeldObject() != null, "Stale crystal was accepted.");
            correct.GetComponent<GrabbableObject>().Drop(); p.SetHeldObjectSilently(null);
            correct.Configure(id, c.Question.Conclusion, c.transform);

            // Every rule is drawn once before repetition, exercising all layouts and validation paths.
            for (int i = 0; i < 20 && conclusions.Count < 10; i++)
            {
                Fill(c, fields, tokens);
                var crystal = c.GetComponentsInChildren<InferenceCrystal>().First(x => x.QuestionId == c.Question.Id && x.Answer == c.Question.Conclusion);
                Hold(p, crystal); c.PlaceHeldCrystal();
                int before = c.SolvedRounds;
                c.ValidateBoard(); c.ValidateBoard();
                Require(c.SolvedRounds == before + 1 && crystal.IsPlaced && p.GetHeldObject() == null, "Completion did not occur exactly once.");
                conclusions.Add(c.Question.Rule.abbreviation);
                c.NextChallenge();
            }
            Require(conclusions.Count == 10, "Did not exercise every rule.");
            Require(alreadyComplete ? completions == 0 : completions == 1, "Completion event fired incorrectly.");
            c.transform.parent.gameObject.SetActive(false);
            Require(p.enabled && !c.IsBoardOpen && !c.focusCamera.activeSelf, "Area disable did not restore control.");
            c.transform.parent.gameObject.SetActive(true);
            c.OpenBoard(p); Require(c.Question.Id != id, "Re-entry reused an old question.");
            return "PASS: 10 rules, empty/incorrect/stale answers, exploration persistence, completion exactly once, replay, disable and re-entry.";
        }
        finally
        {
            c.onChallengeCompleted.RemoveListener(onComplete);
            c.CloseBoard();
        }
    }

    static void Fill(InferenceChallenge c, List<TMP_InputField> fields, List<string> tokens)
    {
        for (int i = 0; i < fields.Count; i++) fields[i].text = tokens[i].StartsWith("[") ? tokens[i].Trim('[', ']') : c.Question.Resolve(tokens[i]);
    }
    static void Hold(Player player, InferenceCrystal crystal)
    {
        var grab = crystal.GetComponent<GrabbableObject>();
        grab.Grab(player.HoldPoint); player.SetHeldObjectSilently(grab);
    }
    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
