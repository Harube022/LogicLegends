using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveLearningChecks
    {
        [Serializable] public sealed class Report
        {
            public bool passed; public int sourceAnswers, barrierChoices, feedbackCases;
            public List<string> errors = new List<string>();
        }
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Validate Learning Content And Barrier Math")]
        public static Report Validate()
        {
            var c = AssetDatabase.LoadAssetAtPath<ArchiveTopicCatalog>(ArchiveTopicSetup.Folder + "/Archive_Topic_Catalog.asset");
            var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(ArchiveTopicSetup.Folder + "/Art/Archive_T7_Reading.asset");
            var report = new Report();
            for (int seed = 1; seed <= 16; seed++)
            {
                for (int topic = 0; topic < 10; topic++) for (int round = 0; round < ArchiveTopicCatalog.RoundCount(topic); round++)
                    for (int sub = 0; sub < (topic == 2 ? 2 : 1); sub++)
                    {
                        string key = "T" + (topic + 1) + "/" + round + "/" + sub + "/seed" + seed;
                        try
                        {
                            var q = ArchiveTopicQuestions.Build(c, topic, round, seed * 7919, sub);
                            var s = ArchiveTopicChecks.Solution(q); s.topic = topic;
                            if (!ArchiveTopicQuestions.Validate(q, s, out _)) throw new InvalidOperationException("Existing source answer regressed.");
                            report.sourceAnswers++;
                            if (!ArchiveLearningFeedback.Guide(topic, q).Contains(ArchiveLearningFeedback.Reference(topic))) throw new InvalidOperationException("Course reference absent.");
                            CheckGlyphs(font, ArchiveLearningFeedback.Guide(topic, q));
                            var bad = JsonUtility.FromJson<ArchiveStationState>(JsonUtility.ToJson(s));
                            typeof(ArchiveTopicChecks).GetMethod("MakeIncorrect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, new object[] { q, bad });
                            ArchiveTopicQuestions.Validate(q, bad, out string old);
                            string before = JsonUtility.ToJson(bad), diagnostic = ArchiveLearningFeedback.Diagnose(q, bad, old);
                            if (string.IsNullOrWhiteSpace(diagnostic) || diagnostic == "Check the entire arrangement. Your team's work is kept.") throw new InvalidOperationException("Generic diagnostic retained.");
                            for (int level = 0; level <= 3; level++)
                            {
                                string hint = ArchiveLearningFeedback.Hint(topic, q, bad, level);
                                if (string.IsNullOrWhiteSpace(hint)) throw new InvalidOperationException("Hint is missing.");
                                CheckGlyphs(font, hint);
                            }
                            if (before != JsonUtility.ToJson(bad)) throw new InvalidOperationException("Explanations mutated puzzle work.");
                            report.feedbackCases++;
                        }
                        catch (Exception e) { report.errors.Add(key + ": " + e.GetBaseException().Message); }
                    }
                for (int kind = 0; kind <= 1; kind++) for (int round = 0; round < (kind == 0 ? 9 : 5); round++)
                {
                    try
                    {
                        var p = new ArchiveBarrierProblem(c, new ArchiveBarrierChallengeState { actor = -1, kind = kind, sourceRound = round, seed = seed * 7919 });
                        CheckGlyphs(font, p.prompt + string.Join(" ", p.options));
                        int accepted = 0;
                        for (int choice = 0; choice < p.options.Length; choice++)
                        {
                            bool valid = p.Validate(choice, out string explanation);
                            bool expected;
                            if (kind == 0) expected = choice == 4 ? p.source.expected.All(v => v == 0) : p.source.expected[choice] == 1;
                            else
                            {
                                var answer = new ArchiveStationState(); ArchiveTopicQuestions.Prepare(p.source, answer);
                                answer.placements[0] = Array.IndexOf(p.source.items, p.options[choice]);
                                expected = ArchiveTopicQuestions.Validate(p.source, answer, out _);
                                if (!p.prompt.Contains(p.source.successText)) throw new InvalidOperationException("Inference conclusion missing; premises alone are ambiguous.");
                            }
                            if (valid != expected || string.IsNullOrWhiteSpace(explanation)) throw new InvalidOperationException("Answer differs from source bank/validator.");
                            if (valid) accepted++; report.barrierChoices++;
                        }
                        if (accepted < 1 || kind == 1 && accepted != 1 || p.Validate(-1, out _) || p.Validate(p.options.Length, out _))
                            throw new InvalidOperationException("Missing valid answer or unsafe option bounds.");
                    }
                    catch (Exception e) { report.errors.Add("Barrier " + kind + "/" + round + "/seed" + seed + ": " + e.Message); }
                }
            }
            report.passed = report.errors.Count == 0;
            Directory.CreateDirectory(ArchiveTopicSetup.Folder + "/Testing/Reports");
            File.WriteAllText(ArchiveTopicSetup.Folder + "/Testing/Reports/LearningContent.json", JsonUtility.ToJson(report, true));
            Debug.Log("Learning content: " + (report.passed ? "PASS" : "FAIL") + ", " + report.barrierChoices + " barrier choices, " + report.feedbackCases + " feedback cases.");
            return report;
        }
        static void CheckGlyphs(TMPro.TMP_FontAsset font, string text)
        {
            foreach (char character in text)
                if (!char.IsControl(character) && !font.HasCharacter(character, true))
                    throw new InvalidOperationException("Missing mathematical reading glyph U+" + ((int)character).ToString("X4"));
        }
    }
}
