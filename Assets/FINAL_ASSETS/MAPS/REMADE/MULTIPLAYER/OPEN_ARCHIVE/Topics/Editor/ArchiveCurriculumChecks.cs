using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveCurriculumChecks
    {
        [Serializable] public sealed class Report
        {
            public bool passed; public int questionCases, truthRows, inferenceModels, relayCases;
            public string scope = "Module-bounded multiplayer content. Medium/Medium-Hard/Hard are design targets pending student playtesting. Original slide files and Solo catalogs are unchanged.";
            public List<string> errors = new List<string>();
        }
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Validate Module Difficulty And Optional Lessons")]
        public static Report Validate()
        {
            var report = new Report();
            var c = AssetDatabase.LoadAssetAtPath<ArchiveTopicCatalog>(ArchiveTopicSetup.Folder + "/Archive_Topic_Catalog.asset");
            string[] Before() => new[] { JsonUtility.ToJson(c), JsonUtility.ToJson(c.formalProofs), JsonUtility.ToJson(c.proofMethods), JsonUtility.ToJson(c.mappings), JsonUtility.ToJson(c.graphs), JsonUtility.ToJson(c.trees) };
            var originals = Before();
            try
            {
                for (int tier = 0; tier < 3; tier++) foreach (var question in ArchiveCurriculumQuestions.TruthBanks[tier])
                {
                    Require(question.Expression.Count(ch => "¬∧∨⊕→↔".Contains(ch)) >= tier + 2, "A T2 tier contains introductory or insufficiently compound content.");
                    for (int row = 0; row < 4; row++)
                    {
                        bool p = row < 2, q = row % 2 == 0;
                        Require(new Formula(question.Expression, p, q).Evaluate() == question.EvaluateRow(row), "Displayed T2 formula and evaluator disagree: " + question.Expression + "/" + row);
                        report.truthRows++;
                    }
                }
                foreach (var bank in ArchiveCurriculumQuestions.InferenceBanks) foreach (var spec in bank)
                { Require(spec.IsValid(), "An inference is invalid or vacuous: " + spec.Key); report.inferenceModels += 16; }
                for (int seed = 0; seed < 64; seed++)
                {
                    var connectives = new HashSet<string>(); var formulas = new HashSet<string>();
                    for (int topic = 0; topic < 10; topic++) for (int round = 0; round < ArchiveTopicCatalog.RoundCount(topic); round++) for (int sub = 0; sub < (topic == 2 ? 2 : 1); sub++)
                    {
                        var q = ArchiveTopicQuestions.Build(c, topic, round, seed * 7919, sub);
                        Require(q.moduleReference == ArchiveCurriculumQuestions.References[topic] && q.difficulty == ArchiveCurriculumQuestions.TierName(topic, round), "Missing scope/tier for T" + (topic + 1));
                        Require(ArchiveTopicQuestions.Validate(q, ArchiveTopicChecks.Solution(q), out _), "Unsolvable seeded question T" + (topic + 1) + "/" + round + "/" + seed);
                        if (topic == 0)
                        {
                            Require(q.items.Length == 4 && q.items.Distinct().Count() == 4 && connectives.Add(q.items[q.correctChoice]), "T1 loses four choices or five-family coverage.");
                            if (round == 4)
                            {
                                var observations = Regex.Matches(q.prompt, @"P=([TF]), Q=([TF]) gives (TRUE|FALSE)").Cast<Match>().ToArray();
                                Require(observations.Length == 3, "Hard T1 needs three evidence cases.");
                                var matching = ArchiveCurriculumQuestions.ConnectiveMasks.Select((mask, i) => new { mask, i }).Where(v => observations.All(m =>
                                { int row = (m.Groups[1].Value == "T" ? 0 : 2) + (m.Groups[2].Value == "T" ? 0 : 1); return ((v.mask & (1 << row)) != 0) == (m.Groups[3].Value == "TRUE"); })).ToArray();
                                Require(matching.Length == 1 && ArchiveCurriculumQuestions.Connectives[matching[0].i] == q.items[q.correctChoice], "Ambiguous T1 observations.");
                            }
                        }
                        if (topic == 1) Require(formulas.Add(q.prompt), "T2 repeats the exact formula in one match.");
                        if (topic == 6)
                        {
                            Require(ArchiveTopicCatalog.RoundCount(6) == 10 && ArchiveTopicCatalog.SetProblemsPerChallenge == 2, "Sets must retain five groups of two problems.");
                            if (round < 6) Require(q.prompt.Contains("x ∈ U") && q.setSourceValues.All(q.setUniverse.Contains), "Set-builder supply includes a value outside U.");
                        }
                        if (topic == 7 && round == 0) Require(q.mapping.requiredPairs.All(pair => int.Parse(pair.codomain) == 2 * int.Parse(pair.domain) + 1), "T8 does not follow the displayed function rule.");
                        if (topic == 7 && round == 4 && q.mapping.correctProperties == MappingProperty.None)
                        {
                            var untouched = new ArchiveStationState { phase = 1 }; ArchiveTopicQuestions.Prepare(q, untouched);
                            Require(!ArchiveMappingPresentation.ReadyToCheck(q, untouched) && ArchiveMappingPresentation.ReadyToCheck(q, ArchiveTopicChecks.Solution(q)), "T8 must distinguish untouched seals from an explicit None of these answer.");
                        }
                        if (topic == 9 && round == 4) Require(q.tree.nodes.Count == 7 && q.tree.initial.Count > 6, "Hard spanning tree lost its cycle-removal task.");
                        report.questionCases++;
                    }
                    for (int topic = 0; topic < 10; topic++)
                    {
                        var lesson = ArchiveCoopLesson.Build(topic, seed);
                        Require(!string.IsNullOrEmpty(lesson.context) && !string.IsNullOrEmpty(lesson.claimP) && !string.IsNullOrEmpty(lesson.claimQ), "Missing optional topic lesson.");
                        int accepted = 0;
                        foreach (bool p in new[] { false, true }) foreach (bool q in new[] { false, true })
                        { if (lesson.Accepts(p, q)) accepted++; report.relayCases++; }
                        Require(accepted > 0 && (topic == 0 || accepted == 1), "Optional claim exercise lacks an unambiguous assignment.");
                    }
                }
                Require(originals.SequenceEqual(Before()), "A multiplayer builder mutated the shared Solo catalog.");
            }
            catch (Exception e) { report.errors.Add(e.ToString()); }
            report.passed = report.errors.Count == 0;
            Directory.CreateDirectory("Temp/ArchiveCurriculum"); File.WriteAllText("Temp/ArchiveCurriculum/Validation.json", JsonUtility.ToJson(report, true));
            return report;
        }
        // Independent parser for the DISPLAYED notation, rather than the authored evaluator lambdas.
        sealed class Formula
        {
            readonly string text; readonly bool p, q; int at;
            public Formula(string text, bool p, bool q) { this.text = text.Replace(" ", ""); this.p = p; this.q = q; }
            bool Take(char c) { if (at >= text.Length || text[at] != c) return false; at++; return true; }
            public bool Evaluate() { bool v = Equivalent(); Require(at == text.Length, "Unparsed formula: " + text); return v; }
            bool Equivalent() { bool v = Implication(); while (Take('↔')) { bool b = Implication(); v = v == b; } return v; }
            bool Implication() { bool v = Or(); if (Take('→')) { bool b = Implication(); v = !v || b; } return v; }
            bool Or() { bool v = And(); while (at < text.Length && (text[at] == '∨' || text[at] == '⊕')) { char op = text[at++]; bool b = And(); v = op == '∨' ? v || b : v != b; } return v; }
            bool And() { bool v = Unary(); while (Take('∧')) { bool b = Unary(); v = v && b; } return v; }
            bool Unary()
            {
                if (Take('¬')) return !Unary();
                if (Take('(')) { bool v = Equivalent(); Require(Take(')'), "Missing formula parenthesis."); return v; }
                if (Take('P')) return p; if (Take('Q')) return q;
                throw new FormatException(text);
            }
        }
    }
}
