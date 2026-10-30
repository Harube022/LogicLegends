using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LogicLegends.OpenArchive.Hunting;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Development-only content, deterministic deck and confirmed-capture fixture checks.</summary>
    public static class ArchiveMonsterBankChecks
    {
        [Serializable] public sealed class Report
        {
            public bool passed, contentPassed, playPassed;
            public int questions, displayedChoices, deckDraws, captureDraws;
            public string scope = "Native Unity content/validator checks and offline confirmed-capture snapshot fixtures. Snapshot fixtures verify the existing barrier question transaction, not physical chase or capture collision.";
            public List<string> checks = new List<string>(), errors = new List<string>();
        }
        const string Output = "Temp/ArchiveMonsterBank/Native.json";
        static Report report;
        static IEnumerator flow;
        static double next;
        static ArchiveTopicSession session;
        static bool originalInference;
        public static string Status { get; private set; } = "Idle";

        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Validate 50 Monster Challenges")]
        public static Report Validate()
        {
            report = new Report();
            try
            {
                var catalog = AssetDatabase.LoadAssetAtPath<ArchiveTopicCatalog>(ArchiveTopicSetup.Folder + "/Archive_Topic_Catalog.asset");
                var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(ArchiveTopicSetup.Folder + "/Art/Archive_T7_Reading.asset");
                Check(catalog != null && font != null, "Saved catalog and actual reading font are available");
                Check(ArchiveMonsterChallengeBank.Count == 50, "The build-included Resources bank contains exactly 50 questions");
                var ids = new HashSet<string>(); var prompts = new HashSet<string>(); var topics = new int[10];
                int[] optionSeeds = { 0, 1, -1, 7919, int.MinValue, int.MaxValue };
                for (int index = 0; index < 50; index++)
                {
                    var entry = ArchiveMonsterChallengeBank.Get(index);
                    Check(ids.Add(entry.id) && prompts.Add(entry.prompt), entry.id + ": unique ID and question");
                    topics[entry.topic]++; report.questions++;
                    Check(entry.slides.Length > 0 && entry.slides.All(s => s > 0) && !string.IsNullOrWhiteSpace(entry.module), entry.id + ": module and slide provenance retained");
                    var seenOrders = new HashSet<string>();
                    foreach (int kind in new[] { 0, 1 }) foreach (int seed in optionSeeds)
                    {
                        var state = new ArchiveBarrierChallengeState { actor = -1, kind = kind, bankVersion = ArchiveMonsterChallengeBank.CurrentVersion,
                            questionIndex = index, sourceTopic = entry.topic, sourceRound = index, seed = seed, encounterId = kind == 0 ? "" : "secondary" };
                        var problem = new ArchiveBarrierProblem(catalog, state);
                        var checkpoint = JsonUtility.FromJson<ArchiveBarrierChallengeState>(JsonUtility.ToJson(state));
                        var clone = new ArchiveBarrierProblem(catalog, checkpoint);
                        if (problem.options.Length != 4 || problem.options.Distinct().Count() != 4 || !problem.options.SequenceEqual(clone.options) || problem.prompt != clone.prompt)
                            throw new InvalidOperationException(entry.id + ": option count or checkpoint determinism failed");
                        int accepted = 0;
                        for (int choice = 0; choice < 4; choice++)
                        {
                            bool valid = problem.Validate(choice, out string explanation);
                            if (valid) accepted++;
                            if (valid != (problem.options[choice] == entry.options[entry.correctIndex]) || string.IsNullOrWhiteSpace(explanation))
                                throw new InvalidOperationException(entry.id + ": displayed option grading differs from the authored answer");
                            report.displayedChoices++;
                        }
                        if (accepted != 1 || problem.Validate(-1, out _) || problem.Validate(4, out _) || problem.QuestionId != entry.id || problem.Topic != entry.topic)
                            throw new InvalidOperationException(entry.id + ": invalid answer bounds, identity or unique correct choice");
                        seenOrders.Add(string.Join("|", problem.options));
                        CheckGlyphs(font, problem.title + problem.prompt + string.Join(" ", problem.options));
                        for (int hint = 0; hint <= 3; hint++)
                        {
                            if (string.IsNullOrWhiteSpace(problem.Hint(hint))) throw new InvalidOperationException(entry.id + ": missing hint " + hint);
                            CheckGlyphs(font, problem.Hint(hint));
                        }
                        CheckGlyphs(font, entry.explanation);
                    }
                    Check(seenOrders.Count > 1, entry.id + ": both monster variants grade the shuffled answer positions consistently");
                }
                Check(topics.All(n => n == 5), "All ten actual scene topics contribute five questions each");
                foreach (int seed in new[] { 0, 1, -1, 7919, -104729, int.MinValue, int.MaxValue })
                {
                    int previous = -1;
                    for (int cycle = 0; cycle < 100; cycle++)
                    {
                        var unique = new HashSet<int>();
                        for (int slot = 0; slot < 50; slot++)
                        {
                            int draw = cycle * 50 + slot;
                            int index = ArchiveTopicSession.MonsterChallengeIndex(seed, draw);
                            if (index < 0 || index >= 50 || !unique.Add(index) || index != ArchiveTopicSession.MonsterChallengeIndex(seed, draw) || slot == 0 && index == previous)
                                throw new InvalidOperationException("Deck repeats or changes at seed " + seed + ", draw " + draw);
                            previous = index; report.deckDraws++;
                        }
                    }
                }
                bool rejectsNegative = false;
                try { ArchiveTopicSession.MonsterChallengeIndex(0, -1); } catch (ArgumentOutOfRangeException) { rejectsNegative = true; }
                Check(rejectsNegative, "Negative draws are rejected; 35,000 native draws have no within-cycle or boundary repeat");
                report.contentPassed = true; report.passed = true; Status = "Content passed";
            }
            catch (Exception e) { report.errors.Add(e.ToString()); Status = "Content failed"; }
            Save(); return report;
        }

        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test All 50 Monster Draws (Offline Play Mode)")]
        public static void Begin()
        {
            if (!Application.isPlaying || Photon.Pun.PhotonNetwork.InRoom || flow != null)
                throw new InvalidOperationException("Use fresh offline Play Mode for the confirmed-capture fixture.");
            session = ArchiveTopicSession.Current;
            if (session == null || !session.Ready || !session.enableBarrierMath || session.learningEncounter == null)
                throw new InvalidOperationException("Load the saved MultiplayerGameplay scene with barrier math enabled.");
            if (!Validate().contentPassed) throw new InvalidOperationException("Content validation failed; inspect " + Output);
            originalInference = session.learningEncounter.inferenceBarrier;
            next = 0; flow = Flow(); Status = "Starting capture deck fixtures";
            EditorApplication.update += Tick; Application.logMessageReceived += Log;
        }
        static IEnumerator Flow()
        {
            const int seed = 194729;
            session.Initialize(seed);
            var hunt = session.hunting;
            if (session.worldProgression != null && session.worldProgression.secondary != null)
                session.worldProgression.secondary.Request(KhezuCommand.Sleep);
            hunt.Request(KhezuCommand.Sleep);
            ArchiveTopicPlayChecks.MoveTo(session.stations[0], ArchiveTopicStation.BookPosition + Vector3.back * 1.7f);
            yield return .2f;
            Check(session.State.monsterChallengeDrawIndex == 0, "Fresh match initializes the shared monster draw cursor at zero");
            var unique = new HashSet<int>(); int previous = -1;
            for (int draw = 0; draw <= 50; draw++)
            {
                Status = "Confirmed capture draw " + (draw + 1) + "/51";
                hunt.Request(KhezuCommand.Sleep);
                session.learningEncounter.inferenceBarrier = draw % 2 == 1;
                var capture = hunt.GetSnapshot();
                capture.state = (int)KhezuHuntState.Caught; capture.target = capture.caught = -1;
                capture.captureSerial++; capture.huntSerial++;
                capture.capturePosition = Player.LocalInstance.transform.position; capture.captureAnchorValid = true;
                capture.captureStartedAt = KhezuHuntingController.EncounterClock - 3;
                capture.encounterPhase = 4; capture.awake = true; capture.pending = false;
                capture.animation = "Idle"; capture.animationSpeed = 1;
                capture.resolvedOutcome = 0; capture.resolvedSerial = 0;
                capture.barrierDeadline = 0; capture.barrierTimerSerial = 0;
                hunt.ApplySnapshot(capture, true);
                double timeout = EditorApplication.timeSinceStartup + 5;
                while (session.State.monsterChallengeDrawIndex <= draw && EditorApplication.timeSinceStartup < timeout) yield return .05f;
                var barrier = session.State.barrier;
                int expected = ArchiveTopicSession.MonsterChallengeIndex(seed, draw);
                Check(barrier.bankVersion == ArchiveMonsterChallengeBank.CurrentVersion && barrier.questionIndex == expected && session.State.monsterChallengeDrawIndex == draw + 1,
                    "Capture " + (draw + 1) + " commits exactly its next match-seeded question");
                Check(barrier.sourceTopic == ArchiveMonsterChallengeBank.Get(expected).topic && session.BarrierProblem().QuestionId == ArchiveMonsterChallengeBank.Get(expected).id,
                    "Capture " + (draw + 1) + " resolves the authored question and actual topic");
                if (draw < 50) Check(unique.Add(expected), "Capture " + (draw + 1) + " has no repeated question in the first deck");
                else Check(previous != expected && unique.Count == 50, "The next shuffled cycle starts after all fifty, without an immediate boundary repeat");
                previous = expected; report.captureDraws++;
                int revision = barrier.revision; string identity = JsonUtility.ToJson(barrier);
                yield return .22f;
                Check(session.State.monsterChallengeDrawIndex == draw + 1 && JsonUtility.ToJson(session.State.barrier) == identity,
                    "The active capture keeps its question across subsequent authority ticks");
                if (draw == 0)
                {
                    double deadline = hunt.BarrierDeadline;
                    double hintTimeout = EditorApplication.timeSinceStartup + 5;
                    while ((!session.IsLocalBarrierQuestion || !session.learningEncounter.LocalChallengeActive) && EditorApplication.timeSinceStartup < hintTimeout) yield return .05f;
                    Check(session.RequestBarrierHint(), "Native captured-player hint command is accepted");
                    yield return .1f;
                    Check(session.State.barrier.questionIndex == expected && session.State.monsterChallengeDrawIndex == 1 && session.State.barrier.revision > revision && hunt.BarrierDeadline == deadline,
                        "A hint advances only its revision, preserving question, deck cursor and original deadline");
                }
            }
            hunt.Request(KhezuCommand.Sleep);
            session.learningEncounter.inferenceBarrier = originalInference;
            session.Initialize(seed + 1);
            Check(session.State.monsterChallengeDrawIndex == 0, "A new match resets the deck cursor without carrying a previous match draw");
            report.playPassed = true;
        }
        static void CheckGlyphs(TMPro.TMP_FontAsset font, string value)
        {
            foreach (char c in value) if (!char.IsControl(c) && !font.HasCharacter(c, true))
                throw new InvalidOperationException("Actual reading font lacks glyph U+" + ((int)c).ToString("X4"));
        }
        static void Check(bool value, string name)
        {
            report.checks.Add((value ? "PASS " : "FAIL ") + name);
            if (!value) throw new InvalidOperationException(name);
        }
        static void Tick()
        {
            if (flow == null) return;
            try
            {
                if (!Application.isPlaying) throw new InvalidOperationException("Play Mode ended during capture fixtures.");
                EditorApplication.QueuePlayerLoopUpdate();
                if (EditorApplication.timeSinceStartup < next) return;
                if (!flow.MoveNext()) { Finish(true); return; }
                next = EditorApplication.timeSinceStartup + (flow.Current is float seconds ? seconds : 0);
            }
            catch (Exception e) { report.errors.Add(Status + ": " + e); Finish(false); }
        }
        static void Log(string message, string stack, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && report.errors.Count < 10)
                report.errors.Add(message + "\n" + stack);
        }
        static void Finish(bool passed)
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log; flow = null;
            if (session != null)
            {
                if (session.hunting != null) session.hunting.Request(KhezuCommand.Sleep);
                if (session.learningEncounter != null) session.learningEncounter.inferenceBarrier = originalInference;
            }
            report.playPassed = passed && report.errors.Count == 0;
            report.passed = report.contentPassed && report.playPassed;
            Status = report.passed ? "Passed" : "Failed"; Save();
        }
        static void Save() { Directory.CreateDirectory("Temp/ArchiveMonsterBank"); File.WriteAllText(Output, JsonUtility.ToJson(report, true)); }
    }
}
