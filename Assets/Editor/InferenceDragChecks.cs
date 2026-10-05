using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using LogicLegends.Inference;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Checks logical entailment, hidden answers, draggable event flow and retry behavior.</summary>
public static class InferenceDragChecks
{
    public static string PlayReport { get; private set; } = "Not started";
    static readonly HashSet<string> checkedRules = new HashSet<string>();
    static double nextCheck;
    static int checkedRounds;

    public static string StartPlayChecks()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Run in Play Mode.");
        var c = UnityEngine.Object.FindFirstObjectByType<InferenceChallenge>();
        Require(c != null && Player.LocalInstance != null, "Player required.");
        checkedRules.Clear(); checkedRounds = 0; PlayReport = "Running";
        c.OpenBoard(Player.LocalInstance); nextCheck = EditorApplication.timeSinceStartup + 1;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        return PlayReport;
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; PlayReport = "Interrupted"; return; }
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        try
        {
            var c = UnityEngine.Object.FindFirstObjectByType<InferenceChallenge>();
            // Gameplay intentionally blocks pointer input until the existing camera blend ends.
            if (!c.boardInput.blocksRaycasts) return;
            checkedRules.Add(c.Question.Rule.abbreviation);
            PlayRound(); checkedRounds++;
            if (checkedRounds == 10)
            {
                Require(checkedRules.Count == 10, "Not all ten rules exercised.");
                PlayReport = "PASS: ten rules; pointer drag/drop, simple words, two-stage checks, physical pickup/carry/place, wrong diamond retry, hidden conclusion, locked success and replay.";
                EditorApplication.update -= Tick; return;
            }
            c.OpenBoard(Player.LocalInstance); nextCheck = EditorApplication.timeSinceStartup + 1;
        }
        catch (Exception e) { PlayReport = "FAIL: " + e.Message; EditorApplication.update -= Tick; }
    }
    public static string Content()
    {
        var rules = AssetDatabase.FindAssets("t:InferenceRule", new[] { "Assets/Gameplay/RulesOfInference" })
            .Select(g => AssetDatabase.LoadAssetAtPath<InferenceRule>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
        Require(rules.Length == 10, "Expected ten rules.");
        var signatures = new Dictionary<string, string>(); int count = 0;
        foreach (var rule in rules)
        {
            Require(rule.examples.Length >= 3, "Too few examples: " + rule.abbreviation);
            Require(rule.examples.Select(e => string.Join("|", e.statements)).Distinct().Count() >= 3, "Repeated examples.");
            for (int f = 0; f < rule.forms.Length; f++)
            {
                if (rule.abbreviation == "DN" && f != 0) continue;
                var source = new InferenceQuestion(rule, f, 0);
                var signature = string.Join(";", source.Form.premises.Select(p => string.Join("", p.tokens.Where(t => t != "\n"))));
                Require(!signatures.ContainsKey(signature) || signatures[signature] == rule.abbreviation, "Ambiguous premise structure.");
                signatures[signature] = rule.abbreviation;
                for (int bits = 0; bits < 16; bits++)
                {
                    bool premises = source.Form.premises.All(p => new LogicExpression(string.Join(" ", p.tokens), bits).Evaluate());
                    if (premises) Require(new LogicExpression(source.Form.conclusion, bits).Evaluate(), "Invalid inference: " + rule.abbreviation);
                }
                for (int e = 0; e < rule.examples.Length; e++)
                {
                    var puzzle = new RuleOfInferencePuzzle(new InferenceQuestion(rule, f, e));
                    Require(puzzle.CorrectPlacements.Count > 0, "Puzzle has no draggable blanks.");
                    Require(!puzzle.CorrectPlacements.Any(w => new[] { "OR", "NOT", "AND", "TRUE", "FALSE", "P", "Q", "R", "S", "THEREFORE" }.Contains(w.ToUpperInvariant())), "Operator word bank remains.");
                    var answers = puzzle.CorrectPlacements.ToArray();
                    Require(ArgumentValidator.Check(puzzle, answers, puzzle.RuleType) == ArgumentCheck.Correct, "Correct answer rejected.");
                    Require(ArgumentValidator.Check(puzzle, answers, null) == ArgumentCheck.NoRuleSelected, "Missing selection accepted.");
                    Require(ArgumentValidator.Check(puzzle, answers, (RuleOfInferenceType)(((int)puzzle.RuleType + 1) % 10)) == ArgumentCheck.WrongRule, "Wrong rule accepted.");
                    answers[0] = "INCORRECT";
                    Require(ArgumentValidator.Check(puzzle, answers, puzzle.RuleType) == ArgumentCheck.IncompleteOrIncorrect, "Wrong placement accepted.");
                    Require(ArgumentValidator.Check(puzzle, new string[0], puzzle.RuleType) == ArgumentCheck.IncompleteOrIncorrect, "Empty argument accepted.");
                    count++;
                }
            }
        }
        var deck = new InferenceDeck(1095);
        var correctPositions = new HashSet<int>();
        var distractorGroups = new HashSet<string>();
        for (int draw = 0; draw < 100; draw++)
        {
            int size = draw % 2 == 0 ? 4 : 5;
            var choices = RuleDiamond.Choose(rules[0], rules, size, new System.Random(draw));
            Require(choices.Length == size && choices.Distinct().Count() == size, "Invalid diamond pool.");
            Require(choices.Count(r => r == rules[0]) == 1, "Correct diamond not included exactly once.");
            correctPositions.Add(Array.IndexOf(choices, rules[0]));
            distractorGroups.Add(string.Join("|", choices.Where(r => r != rules[0]).Select(r => r.abbreviation).OrderBy(s => s)));
        }
        Require(correctPositions.Count >= 4 && distractorGroups.Count > 10, "Predictable diamond choices/positions.");
        for (int cycle = 0; cycle < 10; cycle++)
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < 10; i++)
            {
                var q = deck.Draw(rules, true);
                Require(seen.Add(q.Rule.abbreviation), "Rule repeated inside shuffled cycle.");
                if (q.Rule.abbreviation == "DN") Require(q.Form.premises[0].tokens.Count(t => t == "[NOT]") == 2, "Ambiguous DN selected.");
            }
        }
        return "PASS: " + count + " scenarios, truth tables, simple word banks, incorrect answer rejection, 100 shuffled questions and 100 randomized 4/5-diamond pools.";
    }

    public static string PlayRound()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Run in Play Mode.");
        var c = UnityEngine.Object.FindFirstObjectByType<InferenceChallenge>();
        var player = Player.LocalInstance;
        Require(c != null && c.dragPuzzle != null && player != null, "Scene and spawned player required.");
        var b = c.dragPuzzle; int required = c.requiredRounds; c.requiredRounds = int.MaxValue;
        try
        {
            if (!c.IsBoardOpen) c.OpenBoard(player);
            Require(!player.enabled, "Player still moves while solving.");
            string id = c.Question.Id;
            Require(c.heading.text == "RULES OF INFERENCE", "Rule leaked in title.");
            Require(c.Crystals.Count == 0 && !b.conclusion.Revealed, "Stage 1 leaks diamonds/conclusion.");
            Require(!b.ruleChoices.gameObject.activeSelf, "Rule button list still visible.");
            Require(b.Words.Count == b.Puzzle.CorrectPlacements.Count, "Irrelevant word bank choices.");
            c.ValidateBoard(); Require(c.Stage == InferencePuzzleStage.CompletingPremises && c.Crystals.Count == 0, "Empty argument accepted.");
            var first = b.Zones[0]; var wrongWord = b.Words.FirstOrDefault(w => w.Word != b.Puzzle.CorrectPlacements[0]);
            if (wrongWord != null)
            {
                Drag(wrongWord, first, c); c.ValidateBoard();
                Require(!b.PremisesCompleted && c.Crystals.Count == 0, "Wrong words accepted.");
                wrongWord.ReturnToBank(); Require(first.Value == null, "Moved word left the original blank occupied.");
                Drag(wrongWord, first, c);
            }
            var used = new HashSet<DraggableWord>();
            for (int i = 0; i < b.Zones.Count; i++)
            {
                var word = b.Words.First(w => !used.Contains(w) && w.Word == b.Puzzle.CorrectPlacements[i]);
                used.Add(word); Drag(word, b.Zones[i], c);
            }
            c.CloseBoard(); Require(player.enabled && !c.focusCamera.activeSelf, "Exit failed to restore controls.");
            c.OpenBoard(player); c.boardInput.interactable = c.boardInput.blocksRaycasts = true;
            Require(c.Question.Id == id && b.CheckPremises(), "Re-entry lost words.");
            c.ValidateBoard();
            Require(c.Stage == InferencePuzzleStage.FindingDiamond && b.PremisesCompleted && !b.Solved, "Premise check completes entire puzzle.");
            Require(!b.wordBank.gameObject.activeSelf && !c.wordBank.gameObject.activeSelf, "Word bank remains after stage 1.");
            Require(b.Zones.All(z => z.Locked) && b.Words.All(w => w.Locked), "Premises not locked.");
            Require(c.Crystals.Count >= 4 && c.Crystals.Count <= 5, "Wrong diamond count.");
            var diamonds = c.Crystals.Select(x => x.GetComponent<RuleDiamond>()).ToArray();
            Require(diamonds.Select(x => x.RuleType).Distinct().Count() == diamonds.Length, "Duplicate rules.");
            Require(diamonds.Count(x => x.RuleType == b.Puzzle.RuleType) == 1, "Missing/duplicate correct diamond.");
            Require(diamonds.All(x => x.Crystal.label.gameObject.activeSelf && x.Crystal.label.text == x.DisplayName + " (" + x.Abbreviation + ")"), "Labels incorrect.");
            Require(diamonds.Select(x => x.transform.localScale).Distinct().Count() == 1, "Correct diamond has a visual hint.");
            Require(!c.validateButton.gameObject.activeSelf && !b.conclusion.Revealed, "Final check/conclusion shown without placement.");
            Require(c.diamondPlacementMarker.activeSelf, "Physical placement marker missing.");
            c.ValidateBoard(); Require(!b.Solved, "Final check bypassed placement.");
            var wrong = diamonds.First(x => x.RuleType != b.Puzzle.RuleType);
            Vector3 origin = wrong.transform.position;
            PickAndCarry(wrong, c, player);
            c.placeButton.onClick.Invoke();
            Require(!b.conclusion.Revealed && !b.Solved && c.Stage == InferencePuzzleStage.FindingDiamond, "Wrong diamond solved puzzle.");
            Require(Vector3.Distance(wrong.transform.position, origin) < .01f && !wrong.Crystal.IsPlaced && wrong.GetComponent<Collider>().enabled, "Wrong diamond not recoverable.");
            Require(c.diamondPlacementMarker.activeSelf, "Retry marker not restored.");
            Require(!c.feedback.text.Contains(b.Puzzle.RuleLabel), "Feedback reveals correct rule.");
            var correct = diamonds.Single(x => x.RuleType == b.Puzzle.RuleType);
            PickAndCarry(correct, c, player);
            int before = c.SolvedRounds;
            c.placeButton.onClick.Invoke();
            Require(c.SolvedRounds == before + 1 && b.Solved && b.conclusion.Revealed && c.Stage == InferencePuzzleStage.Solved, "Correct placement did not instantly reveal conclusion and solve round.");
            Require(c.conclusionLabel.text.EndsWith(b.Puzzle.Conclusion), "Conclusion does not match data.");
            Require(!c.placeButton.gameObject.activeSelf && !c.validateButton.gameObject.activeSelf, "Solved puzzle remains editable.");
            string rule = c.Question.Rule.abbreviation; c.NextChallenge();
            Require(c.Crystals.Count == 0 && c.Stage == InferencePuzzleStage.CompletingPremises && !b.conclusion.Revealed, "Replay did not reset stage.");
            return "PASS: " + rule + " sentence drag/drop, two checks, physical pickup/carry/place, wrong retry, hidden answer, success and replay.";
        }
        finally { c.requiredRounds = required; c.CloseBoard(); }
    }
    static void PickAndCarry(RuleDiamond diamond, InferenceChallenge c, Player player)
    {
        c.CloseBoard();
        Require(player.GetHeldObject() == null, "Hand not cleared before pickup.");
        var controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        player.transform.SetPositionAndRotation(diamond.transform.position + Vector3.back + Vector3.down * .5f, Quaternion.identity);
        if (controller != null) controller.enabled = true;
        Physics.SyncTransforms();
        typeof(Player).GetMethod("GameInput_OnInteractAction", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(player, new object[] { null, EventArgs.Empty });
        var grabbed = diamond.GetComponent<GrabbableObject>();
        Require(player.GetHeldObject() == grabbed && !diamond.GetComponent<Collider>().enabled, "Player interaction failed to pick up diamond.");
        if (controller != null) controller.enabled = false;
        var altar = UnityEngine.Object.FindFirstObjectByType<InferenceAltarTrigger>();
        player.transform.position = altar.transform.position;
        if (controller != null) controller.enabled = true;
        // Exercise the existing carry update at the new player position.
        grabbed.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
        Require(Vector3.Distance(diamond.transform.position, player.HoldPoint.position) < .01f, "Diamond does not follow hold point.");
        Require(diamond.Crystal.label.gameObject.activeSelf, "Label hidden while carried.");
        c.OpenBoard(player); c.boardInput.interactable = c.boardInput.blocksRaycasts = true;
    }
    static void Drag(DraggableWord word, DropZone zone, InferenceChallenge c)
    {
        Canvas.ForceUpdateCanvases();
        var raycaster = c.board.GetComponent<UnityEngine.UI.GraphicRaycaster>();
        var data = new PointerEventData(EventSystem.current);
        data.position = RectTransformUtility.WorldToScreenPoint(Camera.main, word.transform.position);
        var hits = new List<RaycastResult>(); raycaster.Raycast(data, hits);
        Require(hits.Any(h => h.gameObject == word.gameObject), "Word is not reachable by pointer raycast.");
        data.pointerPressRaycast = hits.First(h => h.gameObject == word.gameObject);
        data.pointerDrag = word.gameObject;
        ExecuteEvents.Execute(word.gameObject, data, ExecuteEvents.beginDragHandler);
        data.position = RectTransformUtility.WorldToScreenPoint(Camera.main, zone.transform.position);
        ExecuteEvents.Execute(word.gameObject, data, ExecuteEvents.dragHandler);
        hits.Clear(); raycaster.Raycast(data, hits);
        Require(hits.Any(h => h.gameObject == zone.gameObject), "Blank is not reachable by pointer raycast.");
        ExecuteEvents.Execute(zone.gameObject, data, ExecuteEvents.dropHandler);
        ExecuteEvents.Execute(word.gameObject, data, ExecuteEvents.endDragHandler);
    }

    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

    // Parse the stored symbolic templates, then independently check every truth assignment.
    sealed class LogicExpression
    {
        readonly string[] tokens; readonly int bits; int index;
        public LogicExpression(string text, int assignment)
        {
            tokens = Regex.Matches(text, @"IF|THEN|NOT|AND|OR|[pqrs()]" ).Cast<Match>().Select(m => m.Value).ToArray(); bits = assignment;
        }
        public bool Evaluate() { bool value = Implication(); Require(index == tokens.Length, "Unparsed logical expression."); return value; }
        bool Match(string token) { if (index >= tokens.Length || tokens[index] != token) return false; index++; return true; }
        bool Implication()
        {
            if (!Match("IF")) return Or();
            bool left = Or(); Require(Match("THEN"), "Missing THEN."); bool right = Or(); return !left || right;
        }
        bool Or() { bool value = And(); while (Match("OR")) { bool right = And(); value |= right; } return value; }
        bool And() { bool value = Atom(); while (Match("AND")) { bool right = Atom(); value &= right; } return value; }
        bool Atom()
        {
            if (Match("NOT")) return !Atom();
            if (Match("(")) { bool value = Implication(); Require(Match(")"), "Missing parenthesis."); return value; }
            Require(index < tokens.Length && tokens[index].Length == 1 && tokens[index][0] >= 'p' && tokens[index][0] <= 's', "Missing proposition.");
            return (bits & (1 << (tokens[index++][0] - 'p'))) != 0;
        }
    }
}

