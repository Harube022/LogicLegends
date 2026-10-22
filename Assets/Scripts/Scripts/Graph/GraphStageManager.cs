using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LogicLegends.Graph
{
    public enum GraphStageState { WAITING_FOR_ACTIVATION, ACTIVE, VALIDATING, CHALLENGE_COMPLETE, STAGE_COMPLETE, GAME_OVER }

    public sealed class GraphStageManager : MonoBehaviour
    {
        public GraphChallengeCatalog catalog;
        public GraphVertex vertexPrefab;
        public GraphEdge edgePrefab;
        public Transform arena, bookStatue, spawnPoint;
        public GraphUIController ui;
        public GraphStageClock timer;
        public GameOverManager gameOver;
        public GraphCompletion completion;
        public GraphControlsOnboarding controlsTutorial;
        private bool onboardingPending = true;
        public bool TutorialActive => controlsTutorial != null && controlsTutorial.IsActive;
        public AudioClip correctSound, wrongSound;
        [Min(30)] public float countdownSeconds = 600;
        [Min(.1f)] public float feedbackSeconds = 1.2f;
        public GraphStageState State { get; private set; } = GraphStageState.WAITING_FOR_ACTIVATION;
        public GraphTraversalTracker Tracker { get; } = new GraphTraversalTracker();
        public int ChallengeIndex { get; private set; }
        public int Correct { get; private set; }
        public int Mistakes { get; private set; }
        public int? Answer { get; private set; }
        private List<int> runChallengeIndices;
        public IReadOnlyList<int> RunChallengeIndices => runChallengeIndices.AsReadOnly();
        public int ChallengeCount => runChallengeIndices.Count;
        public int CatalogIndex => runChallengeIndices[ChallengeIndex];
        public int ActiveVariantIndex => CatalogIndex;
        private GraphChallengeData activeChallenge;
        public GraphChallengeData Challenge => activeChallenge ?? catalog.variantSets[ChallengeIndex].variants[ActiveVariantIndex];
        public Player LocalPlayer { get; private set; }
        public bool Ready { get; private set; }
        public bool Moving { get; private set; }
        public string Feedback { get; private set; } = "Approach the Book Statue and tap the hand button.";
        private readonly List<GraphEdge> bridges = new List<GraphEdge>();
        private readonly List<GraphVertex> vertices = new List<GraphVertex>();
        private GameInput input;
        private AudioSource audioSource;
        private bool lessonOpen;
        public bool HelpOpen => lessonOpen;
        private bool lessonTimerWasRunning, lessonPlayerWasEnabled, lessonInputWasBlocked, lessonCursorWasVisible;
        private bool lessonSettingsWereEnabled;
        private float lessonTimeScale;
        private CursorLockMode lessonCursorLock;
        private UnityEngine.UI.Button lessonSettings;
        private readonly List<Behaviour> lessonCameraInputs = new List<Behaviour>();
        private float playerFootprint = .78f;
        private bool navigating;
        public const string NextStageScene = "Trees";
        private void Awake() => StageSelectionState.Select(9);
        public bool CanAct => Ready && !Moving && !lessonOpen && !TutorialActive && Time.timeScale > 0 &&
            (input == null || !input.GameplayInputBlocked) &&
            (TeleportManager.Instance == null || !TeleportManager.Instance.IsTransitioning);
        public bool NearBook => LocalPlayer != null && Vector3.Distance(LocalPlayer.transform.position, bookStatue.position) < 5f * GraphGeometry.VertexScale;

        private IEnumerator Start()
        {
            var previous = new int[GraphChallengeCatalog.ChallengesPerRun];
            for (int round = 0; round < previous.Length; round++)
                previous[round] = PlayerPrefs.GetInt("LogicLegends.Graph.LastVariant." + round, -1);
            runChallengeIndices = catalog.SelectRun(new System.Random(Guid.NewGuid().GetHashCode()), previous);
            for (int round = 0; round < runChallengeIndices.Count; round++)
                PlayerPrefs.SetInt("LogicLegends.Graph.LastVariant." + round, runChallengeIndices[round]);
            PlayerPrefs.Save();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0;
            AudioVolumeSettings.Route(audioSource, GameAudioChannel.SoundFX);
            if (correctSound != null) correctSound.LoadAudioData();
            if (wrongSound != null) wrongSound.LoadAudioData();
            GraphGeometry.EnsureBookCollision(bookStatue);
            ui.Build(this);
            timer.Initialize(countdownSeconds, HandleGameOver);
            // Database character selection is asynchronous in a signed-in session.
            while (Player.LocalInstance == null) yield return null;
            LocalPlayer = Player.LocalInstance;
            var look = LocalPlayer.GetComponentInChildren<CameraTargetController>();
            if (look != null) look.SetViewAngles(32, 0);
            input = GameInput.Instance;
            input.SetGameplayInputBlocked(false);
            input.OnInteractAction += OnInteract;
            BuildChallenge();
            // Position the capsule on the imported floor before the startup
            // fade reveals it; do not depenetrate after spawning inside it.
            UpdateSpawnPoint();
            PrelimTeleportTransition.MovePlayerImmediately(LocalPlayer.gameObject, spawnPoint.position, spawnPoint.rotation);
            ResetTracker(arena.InverseTransformPoint(LocalPlayer.transform.position));
            Ready = true;
            BookStatuePresentation.BindHand(bookStatue, () => isActiveAndEnabled && CanAct && NearBook && (State == GraphStageState.WAITING_FOR_ACTIVATION || State == GraphStageState.ACTIVE), InteractBook);
            // Wait for the shared spawn fade and any existing pause to finish.
            // Opening Help is separate from book activation and never resumes the clock.
            while (!CanAct) yield return null;
            SetLesson(true);
        }
        private void OnDestroy()
        {
            SetLesson(false);
            if (input != null) input.OnInteractAction -= OnInteract;
        }
        private void OnDisable() { BookStatuePresentation.SetRequired(bookStatue, false); SetLesson(false); }
        private void LateUpdate()
        {
            BookStatuePresentation.SetRequired(bookStatue, isActiveAndEnabled && Ready && !Moving &&
                !TutorialActive && !lessonOpen && State == GraphStageState.WAITING_FOR_ACTIVATION);
            if (!Ready) return;
            if (State == GraphStageState.ACTIVE && !Moving && !lessonOpen && Time.timeScale > 0)
            {
                Tracker.Sample(arena.InverseTransformPoint(LocalPlayer.transform.position), Time.deltaTime,
                    TeleportManager.Instance != null && TeleportManager.Instance.IsTeleporting);
                if (!Tracker.IsContinuous) Feedback = "Route interrupted. Tap RETRY ROUTE to return to " + Challenge.start + "; time and mistakes are kept.";
            }
            foreach (var edge in bridges)
            {
                edge.UpdateGate(Tracker.Current, LocalPlayer.transform.position, playerFootprint);
                edge.RefreshTraversal(Tracker);
            }
            if (CanAct && LocalPlayer.transform.position.y < arena.position.y - 2.5f &&
                (State == GraphStageState.ACTIVE || State == GraphStageState.WAITING_FOR_ACTIVATION))
                StartCoroutine(ResetPosition(true));
            ui.Refresh();
        }
        private void OnInteract(object sender, EventArgs args)
        {
            if (CanAct && NearBook) InteractBook();
        }
        public void InteractBook()
        {
            if (!CanAct || !NearBook) return;
            if (State == GraphStageState.WAITING_FOR_ACTIVATION)
            {
                // Every activation begins at the configured start, even after exploration.
                StartCoroutine(Activate());
            }
            else Confirm();
        }
        private IEnumerator Activate()
        {
            Moving = true; // consumes repeated activation taps immediately
            ui.HideAnnouncement();
            yield return ResetPosition(false);
            if (State != GraphStageState.WAITING_FOR_ACTIVATION) yield break;
            State = GraphStageState.ACTIVE;
            Feedback = Challenge.objective;
            timer.Resume();
            ui.ShowStartAnnouncement();
        }
        public void SelectAnswer(int number)
        {
            if (!CanAct || State != GraphStageState.ACTIVE || Challenge.type != GraphChallengeType.Degree) return;
            Answer = number; Feedback = "Selected " + number + ". Confirm at the Book Statue or tap CONFIRM.";
        }
        public void ClearSelection()
        {
            if (!CanAct || State != GraphStageState.ACTIVE) return;
            Answer = null; Feedback = "Selection cleared. Count the incident edges, then choose a number.";
        }
        public void RetryRoute()
        {
            if (!CanAct || State != GraphStageState.ACTIVE) return;
            StartCoroutine(ResetPosition(true));
        }
        private IEnumerator ResetPosition(bool retry, bool nextChallenge = false)
        {
            Moving = true;
            while (TeleportManager.Instance.IsTransitioning) yield return null;
            yield return TeleportManager.Instance.TeleportAndWait(LocalPlayer.gameObject, spawnPoint, () =>
            {
                // Keep the old platforms under the player until the fade is
                // fully opaque. The destination is read after this callback.
                if (nextChallenge)
                {
                    ChallengeIndex++;
                    BuildChallenge();
                }
                UpdateSpawnPoint();
            });
            ResetTracker(arena.InverseTransformPoint(LocalPlayer.transform.position));
            Moving = false;
            if (State == GraphStageState.GAME_OVER)
            { LocalPlayer.ToggleControl(false); input.SetGameplayInputBlocked(true); }
            if (retry) Feedback = "Route reset to " + Challenge.start + ". Countdown and mistakes are unchanged.";
        }
        private void UpdateSpawnPoint()
        {
            Physics.SyncTransforms();
            var position = arena.TransformPoint(Challenge.Vertex(Challenge.start).position);
            var startVertex = vertices.Find(vertex => vertex.vertexId == Challenge.start);
            if (startVertex == null || !startVertex.TryGetSurfaceHeight(position, out float height))
                throw new InvalidOperationException("Graph start vertex needs its generated Bilog floor collider.");
            var controller = LocalPlayer.GetComponent<CharacterController>();
            float capsuleBottom = LocalPlayer.transform.TransformVector(controller.center - Vector3.up * controller.height * .5f).y;
            position.y = height - capsuleBottom + controller.skinWidth * Mathf.Abs(LocalPlayer.transform.lossyScale.y);
            spawnPoint.position = position;
        }
        private void BuildChallenge()
        {
            activeChallenge = GraphGeometry.Layout(catalog.variantSets[ChallengeIndex].variants[ActiveVariantIndex]);
            foreach (Transform child in arena) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            bridges.Clear();
            vertices.Clear();
            foreach (var vertex in Challenge.vertices)
            {
                var view = Instantiate(vertexPrefab, arena);
                view.Configure(vertex);
                vertices.Add(view);
            }
            foreach (var edge in Challenge.edges)
            {
                var view = Instantiate(edgePrefab, arena);
                view.Configure(edge, Challenge.Vertex(edge.source).position, Challenge.Vertex(edge.destination).position);
                bridges.Add(view);
            }
            bookStatue.position = arena.TransformPoint(Challenge.Vertex(Challenge.start).position + GraphGeometry.BookOffset);
            Answer = null;
            ResetTracker(Challenge.Vertex(Challenge.start).position);
            ui.RebuildMap();
        }
        private void ResetTracker(Vector3 position)
        {
            var controller = LocalPlayer.GetComponent<CharacterController>();
            float playerScale = Mathf.Max(LocalPlayer.transform.lossyScale.x, LocalPlayer.transform.lossyScale.z);
            float arenaScale = Mathf.Max(.001f, Mathf.Min(arena.lossyScale.x, arena.lossyScale.z));
            playerFootprint = controller == null ? .78f : (controller.radius + controller.skinWidth) * playerScale;
            Tracker.Reset(Challenge, position, playerFootprint / arenaScale, GraphGeometry.VertexRadius, GraphGeometry.BridgeHalfWidth);
        }
        public void Confirm()
        {
            if (!CanAct || State != GraphStageState.ACTIVE) return;
            Tracker.Sample(arena.InverseTransformPoint(LocalPlayer.transform.position), Time.deltaTime);
            State = GraphStageState.VALIDATING;
            bool valid = GraphAnswerValidator.Validate(Challenge, Tracker.Route, Answer,
                Tracker.IsContinuous && Tracker.AtVertex, out string reason);
            // Queue the preloaded sound immediately, before starting particle feedback.
            audioSource.PlayOneShot(valid ? correctSound : wrongSound);
            foreach (var vertex in vertices) vertex.ShowAnswerFeedback(valid, feedbackSeconds);
            if (!valid)
            {
                Mistakes++;
                Feedback = "INCORRECT  /  -10 seconds\n" + reason;
                ui.ShowPenalty();
                timer.Penalize(10);
                if (State == GraphStageState.GAME_OVER) return;
                StartCoroutine(WrongFeedback());
                return;
            }
            Correct++;
            timer.Pause();
            State = GraphStageState.CHALLENGE_COMPLETE;
            Feedback = "";
            if (Correct < ChallengeCount) ui.ShowCompletionAnnouncement(ChallengeIndex + 1, ChallengeIndex + 2);
            else ui.HideAnnouncement();
            StartCoroutine(Advance());
        }
        public bool CanDebugClearChallenge => CanAct && State == GraphStageState.ACTIVE;
        public void DebugClearCurrentChallenge()
        {
        if (!AdminAuthorization.TryUseGameplayAdmin()) return;
            if (!CanDebugClearChallenge) return;
            Correct++; timer.Pause(); State = GraphStageState.CHALLENGE_COMPLETE;
            Feedback = "Challenge cleared by Admin."; StartCoroutine(Advance());
        }
        private IEnumerator WrongFeedback()
        {
            LocalPlayer.ToggleControl(false);
            yield return new WaitForSeconds(feedbackSeconds);
            if (State != GraphStageState.VALIDATING) yield break;
            LocalPlayer.ToggleControl(true);
            State = GraphStageState.ACTIVE;
        }
        private IEnumerator Advance()
        {
            LocalPlayer.ToggleControl(false);
            yield return new WaitForSeconds(feedbackSeconds);
            if (Correct == ChallengeCount)
            {
                State = GraphStageState.STAGE_COMPLETE;
                input.SetGameplayInputBlocked(true);
                completion.Complete(Mistakes);
                ui.ShowResult(true);
                yield break;
            }
            yield return ResetPosition(false, true);
            LocalPlayer.ToggleControl(true);
            State = GraphStageState.WAITING_FOR_ACTIVATION;
            Feedback = "Approach the Book Statue when you are ready.";
        }
        public void HandleGameOver()
        {
            if (State == GraphStageState.GAME_OVER || State == GraphStageState.STAGE_COMPLETE) return;
            State = GraphStageState.GAME_OVER;
            timer.Pause();
            LocalPlayer?.ToggleControl(false);
            input?.SetGameplayInputBlocked(true);
            gameOver.ShowGameOver();
            ui.ShowResult(false);
        }
        public void SetLesson(bool open)
        {
            if (open)
            {
                if (!CanAct || (State != GraphStageState.WAITING_FOR_ACTIVATION && State != GraphStageState.ACTIVE)) return;
                lessonTimerWasRunning = timer.Running;
                lessonPlayerWasEnabled = LocalPlayer.enabled;
                lessonInputWasBlocked = input.GameplayInputBlocked;
                lessonTimeScale = Time.timeScale;
                lessonCursorLock = Cursor.lockState;
                lessonCursorWasVisible = Cursor.visible;
                lessonSettings = ui.settingsButton == null ? null : ui.settingsButton.GetComponent<UnityEngine.UI.Button>();
                if (lessonSettings != null)
                { lessonSettingsWereEnabled = lessonSettings.interactable; lessonSettings.interactable = false; }
                lessonCameraInputs.Clear();
                foreach (var cameraInput in FindObjectsByType<CameraTargetController>(FindObjectsSortMode.None)) PauseLessonCamera(cameraInput);
                foreach (var cameraInput in FindObjectsByType<Unity.Cinemachine.CinemachineInputAxisController>(FindObjectsSortMode.None)) PauseLessonCamera(cameraInput);
                foreach (var cameraInput in FindObjectsByType<CinemachinePinchZoom>(FindObjectsSortMode.None)) PauseLessonCamera(cameraInput);
                foreach (var cameraInput in FindObjectsByType<ThirdPersonCameraController>(FindObjectsSortMode.None)) PauseLessonCamera(cameraInput);
                lessonOpen = true;
                timer.Pause();
                input.SetGameplayInputBlocked(true);
                LocalPlayer.ToggleControl(false);
                MobileLookInput.ResetDelta();
                Time.timeScale = 0;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                ui.ShowLesson(true);
                return;
            }
            if (!lessonOpen) return;
            lessonOpen = false;
            if (ui != null) ui.ShowLesson(false);
            Time.timeScale = lessonTimeScale;
            bool playable = State == GraphStageState.ACTIVE || State == GraphStageState.WAITING_FOR_ACTIVATION;
            if (input != null) input.SetGameplayInputBlocked(playable ? lessonInputWasBlocked : true);
            if (LocalPlayer != null) LocalPlayer.ToggleControl(playable && lessonPlayerWasEnabled);
            foreach (var cameraInput in lessonCameraInputs) if (cameraInput != null) cameraInput.enabled = true;
            lessonCameraInputs.Clear();
            MobileLookInput.ResetDelta();
            if (lessonSettings != null) lessonSettings.interactable = lessonSettingsWereEnabled;
            Cursor.lockState = lessonCursorLock;
            Cursor.visible = lessonCursorWasVisible;
            if (lessonTimerWasRunning && State == GraphStageState.ACTIVE && timer != null) timer.Resume();
        }
        public void BeginFromHelp()
        {
            if (!lessonOpen) return;
            SetLesson(false);
            if (!onboardingPending || State != GraphStageState.WAITING_FOR_ACTIVATION || ChallengeIndex != 0) return;
            if (controlsTutorial == null) controlsTutorial = GetComponent<GraphControlsOnboarding>();
            if (controlsTutorial == null) controlsTutorial = gameObject.AddComponent<GraphControlsOnboarding>();
            if (controlsTutorial.Begin(this)) onboardingPending = false;
        }
        private void PauseLessonCamera(Behaviour cameraInput)
        {
            if (!cameraInput.enabled) return;
            lessonCameraInputs.Add(cameraInput);
            cameraInput.enabled = false;
        }
        public void RestartStage()
        {
        TrialRewards.RecordRetry(9);
            Navigate("Graph", 9);
        }
        public void ReturnToMenu()
        {
            Navigate("Main Menu", 0);
        }
        public void PlayNextStage()
        {
            if (State != GraphStageState.STAGE_COMPLETE) return;
            TrialRewards.PrepareNewAttempt(10);
            Navigate(NextStageScene, 10);
        }
        private void Navigate(string scene, int destinationStage)
        {
            if (navigating || (TeleportManager.Instance != null && TeleportManager.Instance.IsTransitioning)) return;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            { Debug.LogError("Graph destination is missing from Build Settings: " + scene, this); return; }
            navigating = true;
            ui.PrepareResultNavigation();
            Time.timeScale = 1; input?.SetGameplayInputBlocked(false);
            if (destinationStage > 0) StageSelectionState.Select(destinationStage);
            if (TeleportManager.Instance != null) TeleportManager.Instance.LoadSceneWithFade(scene);
            else TeleportManager.Navigate(scene);
        }
    }
}
