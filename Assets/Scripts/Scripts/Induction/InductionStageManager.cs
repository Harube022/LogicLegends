using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LogicLegends.Graph;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LogicLegends.Induction
{
    public enum InductionState { WaitingForBook, Active, StageComplete, GameOver }

    public sealed class InductionStageManager : MonoBehaviour
    {
        public static InductionStageManager Instance { get; private set; }
        [Header("Shared countdown for the entire stage")]
        [Min(1)] public float startingSeconds = 1080;
        [Min(0)] public float incorrectPenalty = 10;
        [Header("Existing scene objects")]
        public Transform book, scrollStand, informationBoard;
        public Transform[] stations, tabletSpawns;
        public InductionSlot[] slots;
        public GameObject tabletPrefab;
        public TMP_Text boardTitle, boardFormula, boardInstructions, bookPrompt;
        public TMP_FontAsset readingFont;
        public Light[] completionSeals;
        public Light staircaseLight;
        public Transform uiHost;
        public GraphStageClock timer;
        public GraphCompletion completion;
        public InductionUI ui;
        public InductionBoardView boardView;
        public InductionHelpUI help;
        public InductionAltarIndicators altarIndicators;
        public InductionControlsOnboarding onboarding;
        [Header("Interaction")]
        [Min(1)] public float interactionRange = 5;
        [Range(-1,1)] public float targetDot = .35f;
        public int stageNumber = 6;
        public string nextScene = "Counting";
        public InductionState State { get; private set; } = InductionState.WaitingForBook;
        public int ChallengeIndex { get; private set; }
        public int Correct { get; private set; }
        public int Mistakes { get; private set; }
        public bool Ready { get; private set; }
        public bool Reading { get; private set; }
        public bool BrowsingScrolls { get; private set; }
        public string Feedback { get; private set; } = "Interact with the Book Statue to begin Challenge 1.";
        public IReadOnlyList<InductionTablet> Tablets => tablets;
        public InductionChallenge Challenge => InductionChallenges.All[ChallengeIndex];
        public InductionTablet Held => Player.LocalInstance != null && Player.LocalInstance.GetHeldObject() != null
            ? Player.LocalInstance.GetHeldObject().GetComponent<InductionTablet>() : null;
        public bool CanAct => Ready && !Reading && (onboarding == null || !onboarding.IsActive) && Time.timeScale > 0 &&
            (GameInput.Instance == null || !GameInput.Instance.GameplayInputBlocked) &&
            (TeleportManager.Instance == null || !TeleportManager.Instance.IsTransitioning && TeleportManager.Instance.FadeAlpha <= .001f) &&
            State != InductionState.GameOver && State != InductionState.StageComplete;
        public bool CanSubmit => CanAct && State == InductionState.Active && slots.Any(s => s.Tablet != null);
        public Component Target { get; private set; }
        public string TargetAction { get; private set; } = "";
        readonly List<InductionTablet> tablets = new List<InductionTablet>();
        readonly List<Behaviour> pausedLook = new List<Behaviour>();
        bool previousBlocked, previousPlayerEnabled;
        float previousTimeScale;
        CursorLockMode previousCursorLock;
        bool previousCursorVisible;
        int evaluatedFrame = -1;
        int interactionFrame = -1;

        void Awake() { Instance = this; StageSelectionState.Select(stageNumber); }
        IEnumerator Start()
        {
            // Tick explicitly, so Settings/reading/input pauses cannot consume time.
            timer.enabled = false; timer.Initialize(startingSeconds, HandleGameOver);
            if (boardView == null) boardView = gameObject.AddComponent<InductionBoardView>();
            boardView.Initialize(this);
            ui.Build(this, uiHost); Prepare();
            if (altarIndicators == null) altarIndicators = gameObject.AddComponent<InductionAltarIndicators>();
            altarIndicators.Initialize(this);
            if (help == null) help = gameObject.AddComponent<InductionHelpUI>();
            if (onboarding == null) onboarding = gameObject.AddComponent<InductionControlsOnboarding>();
            help.Build(this, uiHost);
            while (Player.LocalInstance == null || GameInput.Instance == null) yield return null;
            Ready = true;
            if (help.showOnEntry)
            {
                while (!CanAct) yield return null;
                help.Open();
            }
        }
        void OnDestroy()
        {
            if (Reading) CloseReading();
            if (Instance == this) Instance = null;
        }
        void Update()
        {
            if (!Ready) return;
            if (State == InductionState.Active && CanAct) timer.Tick(Time.deltaTime);
            RefreshTarget(); ui.Refresh();
            if (bookPrompt != null && Camera.main != null)
            {
                bookPrompt.gameObject.SetActive(State == InductionState.WaitingForBook && !help.OnboardingPending);
                bookPrompt.text = "Interact to start Challenge " + (ChallengeIndex + 1);
                bookPrompt.transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward, Camera.main.transform.up);
            }
        }
        void Prepare()
        {
            CleanupTablets();
            foreach (var slot in slots)
            {
                slot.Remove(); slot.gameObject.SetActive(Challenge.answers.ContainsKey(slot.key));
                slot.displayName = slot.key == "Step:1" && ChallengeIndex == 2 ? "Inductive Step (goal)" : SlotName(slot.key);
                slot.MarkError(false);
            }
            var shuffled = Challenge.choices.ToArray(); Shuffle(shuffled);
            for (int i = 0; i < shuffled.Length; i++)
            {
                var instance = Instantiate(tabletPrefab, scrollStand.position, scrollStand.rotation, transform);
                var tablet = instance.GetComponent<InductionTablet>();
                tablet.Initialize(shuffled[i], scrollStand.position); tablets.Add(tablet);
                tablet.ReturnToStand();
            }
            for (int i = 0; i < stations.Length; i++)
                foreach (var slot in slots.Where(s => s.key.StartsWith(new[] {"Base:","Hypothesis:","Step:"}[i], StringComparison.Ordinal)))
                    slot.gameObject.SetActive(Challenge.answers.ContainsKey(slot.key));
            UpdateBoard(false);
        }
        static string SlotName(string key) => key.StartsWith("Base:") ? "Base Case altar" : key.StartsWith("Hypothesis:") ? "Inductive Hypothesis altar" : "Inductive Step line " + key.Substring(5);
        static void Shuffle<T>(T[] values)
        { for (int i = values.Length - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); T old = values[i]; values[i] = values[j]; values[j] = old; } }
        void UpdateBoard(bool active)
        {
            boardTitle.text = "Challenge " + (ChallengeIndex + 1) + "\n" + Challenge.title;
            boardFormula.text = active ? Challenge.theorem : "The Induction Tower\nStairway of Infinity";
            boardInstructions.text = active ? Challenge.instructions : "Interact with the Book Statue to begin.\nThe shared countdown is paused.";
        }
        public bool IsTargetable(Vector3 point)
        {
            var p = Player.LocalInstance; if (p == null) return false;
            Vector3 delta = point - p.transform.position; delta.y = 0;
            return delta.magnitude <= interactionRange && (delta.sqrMagnitude < .01f || Vector3.Dot(p.transform.forward, delta.normalized) >= targetDot);
        }
        void RefreshTarget()
        {
            Target = null; TargetAction = "";
            if (!CanAct) return;
            var p = Player.LocalInstance;
            float best = float.MaxValue;
            Action<Component, Vector3, string> candidate = (component, point, action) =>
            {
                if (!IsTargetable(point)) return;
                Vector3 delta = point - p.transform.position; delta.y = 0;
                float score = delta.magnitude + (1 - Vector3.Dot(p.transform.forward, delta.normalized)) * 2;
                if (score < best) { best = score; Target = component; TargetAction = action; }
            };
            if (State == InductionState.WaitingForBook && !help.OnboardingPending) candidate(book, book.position, "Start Challenge");
            candidate(scrollStand, scrollStand.position, "Formula Scrolls");
            if (State != InductionState.Active) return;
            if (Held != null)
            {
                foreach (var slot in slots.Where(s => s.gameObject.activeSelf))
                    candidate(slot, slot.transform.position, slot.Tablet == null ? "Place in " + slot.displayName : "Replace in " + slot.displayName);
            }
            else
            {
                foreach (var tablet in tablets.Where(t => t != null && !t.AtStand && t.gameObject.activeInHierarchy))
                    candidate(tablet, tablet.transform.position, tablet.Slot == null ? "Pick Up Tablet" : "Remove Tablet");
            }
        }
        // Player routes hand/E here before generic pickup, preventing duplicate actions.
        public bool TryHandleInteraction()
        {
            if (onboarding != null && onboarding.IsActive) { onboarding.TryPracticeInteraction(); return true; }
            if (!CanAct) return true;
            if (interactionFrame == Time.frameCount) return true;
            interactionFrame = Time.frameCount;
            RefreshTarget();
            if (Target == book) InteractBook();
            else if (Target == scrollStand) OpenScrollCollection();
            else if (Target is InductionSlot slot) Place(slot);
            else if (Target is InductionTablet tablet) PickUp(tablet);
            return true;
        }
        public void InteractBook()
        {
            if (!CanAct || help.OnboardingPending || State != InductionState.WaitingForBook || !IsTargetable(book.position)) return;
            if (State == InductionState.WaitingForBook)
            {
                if (evaluatedFrame == Time.frameCount) return;
                evaluatedFrame = Time.frameCount;
                State = InductionState.Active; timer.Resume(); UpdateBoard(true);
                Feedback = "Collect scrolls from the Formula Scrolls stand, arrange them at the active altars, then press Submit.";
                ui.Announce("Start!", 1.25f); return;
            }
        }
        public bool PickUp(InductionTablet tablet)
        {
            if (!CanAct || State != InductionState.Active || Held != null || tablet == null ||
                !tablets.Contains(tablet) || tablet.AtStand || !tablet.gameObject.activeInHierarchy || !IsTargetable(tablet.transform.position)) return false;
            if (tablet.Slot != null) tablet.Slot.Remove();
            tablet.Grabber.Grab(Player.LocalInstance.GetHoldPoint());
            Player.LocalInstance.SetHeldObjectSilently(tablet.Grabber);
            Feedback = "Carrying a scroll. Place it at an active altar or use Return Scroll."; return true;
        }
        public bool Place(InductionSlot slot)
        {
            var held = Held;
            if (!CanAct || State != InductionState.Active || held == null || slot == null ||
                !slots.Contains(slot) || !slot.gameObject.activeSelf || !IsTargetable(slot.transform.position)) return false;
            var replaced = slot.Remove();
            held.Grabber.Drop(); Player.LocalInstance.SetHeldObjectSilently(null); slot.SetTablet(held);
            if (replaced != null)
            {
                replaced.Grabber.Grab(Player.LocalInstance.GetHoldPoint());
                Player.LocalInstance.SetHeldObjectSilently(replaced.Grabber);
            }
            Feedback = "Placement updated. Press Submit to check the entire challenge arrangement.";
            ui.Refresh(); return true;
        }
        public void ReturnHeld()
        {
            if (!CanAct || State != InductionState.Active || Held == null) return;
            Held.ReturnToStand(); Player.LocalInstance.SetHeldObjectSilently(null); Feedback = "Scroll returned to the Formula Scrolls stand.";
        }
        public void Submit()
        {
            if (!CanSubmit || evaluatedFrame == Time.frameCount) return;
            evaluatedFrame = Time.frameCount;
            string[] errors = InductionChallenges.IncorrectSlots(Challenge, slots);
            foreach (var slot in slots) slot.MarkError(errors.Contains(slot.key));
            bool correct = errors.Length == 0;
            WorldStageAudio.PlayAnswerFeedback(correct);
            if (!correct)
            {
                Mistakes++; Feedback = "Incorrect. Fix the marked scrolls and submit again. −10 seconds.";
                if (errors.Any(k => slots.First(s => s.key == k).Tablet == null)) Feedback += "\nSome required tablets are missing.";
                timer.Penalize(incorrectPenalty); ui.SetFeedback(false); return;
            }
            Correct++; timer.Pause();
            if (completionSeals.Length > ChallengeIndex) completionSeals[ChallengeIndex].enabled = true;
            if (Correct == InductionChallenges.All.Length)
            {
                State = InductionState.StageComplete; Feedback = "All challenges completed!";
                CleanupTablets(); if (staircaseLight != null) staircaseLight.enabled = true;
                Player.LocalInstance.ToggleControl(false); GameInput.Instance.SetGameplayInputBlocked(true);
                completion.Complete(Mistakes); ui.ShowResult(true); WorldStageAudio.PlayStageCompletion(); return;
            }
            int completed = ChallengeIndex + 1;
            ChallengeIndex++; State = InductionState.WaitingForBook; Prepare();
            Feedback = "Correct"; ui.SetFeedback(true);
            ui.Announce("Challenge " + completed + " completed! Interact with the book statue to start Challenge " + (ChallengeIndex + 1) + ".", -1);
            ui.Refresh();
        }
        void CleanupTablets()
        {
            if (Held != null) { Held.Grabber.Drop(); Player.LocalInstance.SetHeldObjectSilently(null); }
            foreach (var slot in slots) slot.Remove();
            foreach (var tablet in tablets) if (tablet != null) { tablet.gameObject.SetActive(false); Destroy(tablet.gameObject); }
            tablets.Clear();
        }
        public void HandleGameOver()
        {
            if (State == InductionState.GameOver || State == InductionState.StageComplete) return;
            State = InductionState.GameOver; timer.Pause();
            Player.LocalInstance?.ToggleControl(false); GameInput.Instance?.SetGameplayInputBlocked(true);
            ui.ShowResult(false);
        }
        public void OpenReading(string kind)
        {
            if (kind != "board" || !BeginModal()) return;
            ui.OpenReader();
        }
        public bool BeginModal()
        {
            if (!CanAct) return false;
            Reading = true;
            previousTimeScale = Time.timeScale; previousBlocked = GameInput.Instance.GameplayInputBlocked;
            previousPlayerEnabled = Player.LocalInstance.enabled;
            previousCursorLock = Cursor.lockState; previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            Player.LocalInstance.ToggleControl(false); GameInput.Instance.SetGameplayInputBlocked(true);
            foreach (var look in FindObjectsByType<CameraTargetController>(FindObjectsSortMode.None).Cast<Behaviour>()
                .Concat(FindObjectsByType<Unity.Cinemachine.CinemachineInputAxisController>(FindObjectsSortMode.None))
                .Concat(FindObjectsByType<CinemachinePinchZoom>(FindObjectsSortMode.None))
                .Concat(FindObjectsByType<ThirdPersonCameraController>(FindObjectsSortMode.None)))
                if (look.enabled) { pausedLook.Add(look); look.enabled = false; }
            Time.timeScale = 0; return true;
        }
        public void OpenScrollCollection()
        {
            if (!CanAct || !IsTargetable(scrollStand.position) || !BeginModal()) return;
            BrowsingScrolls = true; ui.OpenCollection();
        }
        public bool CanCollect(InductionTablet tablet) => BrowsingScrolls && Reading && Ready &&
            State == InductionState.Active && Held == null && tablet != null && tablets.Contains(tablet) &&
            tablet.AtStand && IsTargetable(scrollStand.position);
        public bool CollectScroll(InductionTablet tablet)
        {
            if (!CanCollect(tablet)) return false;
            CloseReading();
            tablet.TakeFromStand();
            tablet.Grabber.Grab(Player.LocalInstance.GetHoldPoint());
            Player.LocalInstance.SetHeldObjectSilently(tablet.Grabber);
            Feedback = "Carrying the selected scroll. Place it at an active altar.";
            return true;
        }
        public void CloseReading()
        {
            if (!Reading) return;
            help?.Hide();
            boardView?.RestoreImmediately();
            Reading = false; BrowsingScrolls = false; ui.CloseReader();
            GameInput.Instance?.SetGameplayInputBlocked(previousBlocked);
            Player.LocalInstance?.ToggleControl(previousPlayerEnabled);
            foreach (var look in pausedLook) if (look != null) look.enabled = true;
            pausedLook.Clear(); MobileLookInput.ResetDelta(); Time.timeScale = previousTimeScale;
            Cursor.lockState = previousCursorLock; Cursor.visible = previousCursorVisible;
        }
        public void Navigate(string scene)
        {
            if (Reading) CloseReading(); Time.timeScale = 1; GameInput.Instance?.SetGameplayInputBlocked(false);
            if (TeleportManager.Instance != null) TeleportManager.Instance.LoadSceneWithFade(scene); else SceneManager.LoadScene(scene);
        }
        public void Restart() => Navigate("Induction");
    }
}
