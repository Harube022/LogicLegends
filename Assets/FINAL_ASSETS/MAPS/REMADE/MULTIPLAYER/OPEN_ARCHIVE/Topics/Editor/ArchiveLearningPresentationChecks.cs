using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.Tests.Monsters;
using Photon.Pun;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Opt-in offline UI checks against the saved scene's real rigs, controls and cinematic.</summary>
    public static class ArchiveLearningPresentationChecks
    {
        [Serializable] sealed class Entry { public string name; public bool passed; }
        [Serializable] sealed class Report
        {
            public bool passed;
            public string scope = "Rendered Windows Editor, current Game View resolution, actual saved monster rigs and UI pointer events. Physical Android touch/audio/performance and two independently rendered multiplayer clients are unverified.";
            public List<Entry> checks = new List<Entry>(); public List<string> errors = new List<string>();
        }
        static Report report;
        static ArchiveTopicSession session;
        static ArchiveLearningUI ui;
        static DevelopmentMonsterSelection selector;
        static DevelopmentMonster originalMonster;
        static HealthManager.ActorHealth[] originalHealth;
        static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        static bool active;
        static double next;
        public static string Status { get; private set; } = "Idle";
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Learning Presentation (Offline Play Mode)")]
        public static void Begin()
        {
            if (!Application.isPlaying || PhotonNetwork.InRoom || active) throw new InvalidOperationException("Use offline Play Mode in the saved topic scene.");
            session = ArchiveTopicSession.Current; ui = ArchiveLearningUI.Current;
            selector = UnityEngine.Object.FindFirstObjectByType<DevelopmentMonsterSelection>();
            if (session == null || !session.Ready || ui == null || selector == null) throw new InvalidOperationException("Learning scene not ready.");
            originalMonster = selector.Selected; originalHealth = session.hunting.health.ExportActorHealth();
            report = new Report(); active = true; stack.Clear(); stack.Push(Flow()); next = 0;
            Directory.CreateDirectory("Temp/ArchiveLearningSprint");
            EditorApplication.update += Tick; EditorApplication.playModeStateChanged += PlayState;
        }
        static void Check(bool pass, string name)
        { report.checks.Add(new Entry { name = name, passed = pass }); if (!pass) throw new InvalidOperationException(name); }
        static void Tick()
        {
            if (!active) return;
            try
            {
                EditorApplication.QueuePlayerLoopUpdate(); if (EditorApplication.timeSinceStartup < next) return;
                if (!Application.isPlaying) throw new InvalidOperationException("Play Mode stopped.");
                if (stack.Count == 0) { Finish(true); return; }
                var current = stack.Peek(); if (!current.MoveNext()) { stack.Pop(); return; }
                if (current.Current is IEnumerator nested) stack.Push(nested);
                else next = EditorApplication.timeSinceStartup + (current.Current is float delay ? delay : 0);
            }
            catch (Exception e) { report.errors.Add(Status + ": " + e); Finish(false); }
        }
        static void PlayState(PlayModeStateChange state) { if (active && state == PlayModeStateChange.ExitingPlayMode) Finish(false); }
        static void Finish(bool success)
        {
            active = false; EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= PlayState; stack.Clear();
            if (session != null && Application.isPlaying)
            {
                session.hunting.Request(KhezuCommand.Sleep); session.hunting.health.ApplyActorHealthSnapshot(originalHealth);
                if (selector != null && selector.CanSwap) selector.Select(originalMonster);
                session.Initialize(314159);
            }
            report.passed = success && report.errors.Count == 0 && report.checks.All(c => c.passed);
            Status = report.passed ? "Passed" : "Failed";
            File.WriteAllText(ArchiveTopicSetup.Folder + "/Testing/Reports/LearningPresentation.json", JsonUtility.ToJson(report, true));
            Debug.Log("Learning presentation: " + Status + ", " + report.checks.Count + " checks.");
        }
        static IEnumerator Wait(Func<bool> predicate, string name, float timeout = 10)
        {
            double deadline = EditorApplication.timeSinceStartup + timeout;
            while (!predicate()) { if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException(name); yield return .025f; }
        }
        static RectTransform FindRect(string name) => UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None).First(r => r.name == name);
        static Rect Box(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners); return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
        static void ClearControls(string panel)
        {
            Rect window = Box(FindRect(panel));
            foreach (string name in new[] { "Joystick", "JumpButton", "InteractButton" })
                Check(!window.Overlaps(Box(FindRect(name))), panel + " leaves " + name + " unobstructed at " + Screen.width + "×" + Screen.height);
        }
        static Button FindButton(string name) => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(b => b.name == name);
        static void Click(Button button)
        {
            Check(button.isActiveAndEnabled && button.interactable, button.name + " is an active normal UI action");
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)button.transform).TransformPoint(((RectTransform)button.transform).rect.center)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Check(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)), button.name + " receives the actual topmost raycast");
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        static IEnumerator Reveal(Button button)
        {
            var scroll = button.GetComponentInParent<ScrollRect>(); Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.content, button.transform);
            float available = scroll.content.rect.height - scroll.viewport.rect.height;
            scroll.verticalNormalizedPosition = available <= 0 ? 1 : 1 - Mathf.Clamp01((-bounds.center.y - scroll.viewport.rect.height * .5f) / available);
            yield return .1f;
        }
        static IEnumerator Capture(DevelopmentMonster monster)
        {
            var h = session.hunting; h.Request(KhezuCommand.Sleep); yield return .1f; selector.Select(monster); yield return .15f;
            Check(selector.Selected == monster, "Existing " + monster + " visual rig selected without replacement assets");
            var player = Player.LocalInstance; var cc = player.GetComponent<CharacterController>(); cc.enabled = false;
            player.transform.position = h.monsterSpawnPoint.position + h.transform.forward * 18 + Vector3.up * 1.1f;
            cc.enabled = true; player.ResetMotionAfterTeleport();
            var capture = h.GetSnapshot(); capture.state = (int)KhezuHuntState.Caught; capture.target = capture.caught = -1;
            capture.captureSerial++; capture.huntSerial++; capture.capturePosition = player.transform.position; capture.captureAnchorValid = true;
            capture.captureStartedAt = KhezuHuntingController.EncounterClock; capture.encounterPhase = 1; capture.awake = true; capture.pending = false;
            capture.animation = "Capture"; capture.animationSpeed = 1; capture.resolvedOutcome = 0; capture.barrierTimerSerial = 0;
            h.ApplySnapshot(capture, false);
            while (KhezuHuntingController.EncounterClock - capture.captureStartedAt < KhezuCinematicJumpscareController.Duration)
            { Check(!ui.BarrierVisible, monster + " math stays hidden until the cinematic finishes"); yield return .25f; }
            yield return Wait(() => ui.BarrierVisible && session.learningEncounter.LocalChallengeActive, "Actual math appears after cinematic"); yield return .15f;
            Check(Player.LocalInstance.IsMovementLocked && session.State.barrier.kind == (monster == DevelopmentMonster.Rajang ? 1 : 0), monster + " has its correct problem and existing complete capture lock");
            ClearControls("Real_Math_Barrier_Challenge");
            ScreenCapture.CaptureScreenshot("Temp/ArchiveLearningSprint/" + monster + "_Math_Challenge.png"); yield return .15f;
        }
        static IEnumerator Flow()
        {
            Status = "Tutorial, reading notation and control clearance";
            for (int topic = 0; topic < 10; topic++)
            {
                ui.OpenGuide(topic); yield return .06f;
                Check(ui.GuideVisible && !Player.LocalInstance.IsMovementLocked && !session.State.stations[topic].timerRunning, "T" + (topic + 1) + " tutorial reads without starting progress or movement locks");
            }
            ClearControls("Learning_Guide");
            ui.OpenGuide(6); yield return .1f; var guideScroll = ui.GetComponentsInChildren<ScrollRect>(true).First(s => s.name == "Guide_Reading");
            guideScroll.verticalNormalizedPosition = .35f; ScreenCapture.CaptureScreenshot("Temp/ArchiveLearningSprint/Guide_Symbols.png"); yield return .15f;
            ui.gameObject.SetActive(false); ui.gameObject.SetActive(true); yield return .1f;
            Status = "Khezu cinematic and real counterexample UI";
            yield return Capture(DevelopmentMonster.Khezu);
            int revision = session.State.barrier.revision; Click(FindButton("Barrier_Next_Hint")); yield return Wait(() => session.State.barrier.revision > revision, "Offline hint commits"); yield return .1f;
            var prisonScroll = ui.GetComponentsInChildren<ScrollRect>(true).First(s => s.name == "Barrier_Reading");
            Check(session.State.barrier.hintLevel == 1 && prisonScroll.verticalNormalizedPosition < .05f, "Barrier hint immediately scrolls its explanation into view");
            ScreenCapture.CaptureScreenshot("Temp/ArchiveLearningSprint/Khezu_Hint.png"); yield return .15f;
            var problem = session.BarrierProblem(); int answer = Enumerable.Range(0, problem.options.Length).First(i => problem.Validate(i, out _));
            var option = FindButton("Barrier_Choice_" + answer); yield return Reveal(option); Click(option); yield return .1f;
            Click(FindButton("Confirm_Barrier_Answer")); yield return Wait(() => session.State.barrier.outcome == 1, "Real offline confirm validated");
            Check(session.hunting.ResolvedOutcome == 1, "Actual Khezu choice and Confirm pointer events enter existing success flow");
            session.hunting.Request(KhezuCommand.Sleep); yield return .15f;
            Status = "Rajang cinematic and real inference UI";
            yield return Capture(DevelopmentMonster.Rajang);
            problem = session.BarrierProblem(); answer = Enumerable.Range(0, problem.options.Length).First(i => !problem.Validate(i, out _));
            int hearts = session.hunting.health.ActorHearts(-1);
            option = FindButton("Barrier_Choice_" + answer); yield return Reveal(option); Click(option); yield return .1f;
            Click(FindButton("Confirm_Barrier_Answer")); yield return Wait(() => session.hunting.FailureHitApplied, "Real Rajang incorrect confirm reaches attack contact");
            Check(session.State.barrier.outcome == 2 && session.hunting.health.ActorHearts(-1) == hearts - 1 && session.hunting.FailureStunActive, "Actual Rajang choice and Confirm pointer events trigger one heart and the existing stun");
            yield return Wait(() => !session.hunting.FailureStunActive && !Player.LocalInstance.IsMovementLocked, "Offline stun releases after five seconds");
            Check(!ui.BarrierVisible, "Math question closes after the encounter releases its player");
            Status = "Complete";
        }
    }
}
