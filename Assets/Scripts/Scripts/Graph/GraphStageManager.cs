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
        public AudioClip correctSound, wrongSound;
        [Min(30)] public float countdownSeconds = 300;
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
        private GraphChallengeData activeChallenge;
        public GraphChallengeData Challenge => activeChallenge ?? catalog.challenges[CatalogIndex];
        public Player LocalPlayer { get; private set; }
        public bool Ready { get; private set; }
        public bool Moving { get; private set; }
        public string Feedback { get; private set; } = "Approach the Book Statue and tap INTERACT to begin.";
        private readonly List<GraphEdge> bridges = new List<GraphEdge>();
        private readonly List<GraphVertex> vertices = new List<GraphVertex>();
        private GameInput input;
        private AudioSource audioSource;
        private bool lessonOpen;
        private float playerFootprint = .78f;
        private void Awake() => StageSelectionState.Select(9);
        public bool CanAct => Ready && !Moving && !lessonOpen && Time.timeScale > 0 &&
            (input == null || !input.GameplayInputBlocked) &&
            (TeleportManager.Instance == null || !TeleportManager.Instance.IsTransitioning);
        public bool NearBook => LocalPlayer != null && Vector3.Distance(LocalPlayer.transform.position, bookStatue.position) < 5f * GraphGeometry.VertexScale;

        private IEnumerator Start()
        {
            runChallengeIndices = catalog.SelectRun(new System.Random(Guid.NewGuid().GetHashCode()));
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0;
            AudioVolumeSettings.Route(audioSource, GameAudioChannel.SoundFX);
            if (correctSound != null) correctSound.LoadAudioData();
            if (wrongSound != null) wrongSound.LoadAudioData();
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
        }
        private void OnDestroy()
        {
            if (input != null) input.OnInteractAction -= OnInteract;
        }
        private void LateUpdate()
        {
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
            if (!Moving && LocalPlayer.transform.position.y < arena.position.y - 2.5f &&
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
            yield return ResetPosition(false);
            if (State != GraphStageState.WAITING_FOR_ACTIVATION) yield break;
            State = GraphStageState.ACTIVE;
            Feedback = Challenge.objective;
            timer.Resume();
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
            activeChallenge = GraphGeometry.Layout(catalog.challenges[CatalogIndex]);
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
            Feedback = "CHALLENGE " + (ChallengeIndex + 1) + " COMPLETE!";
            StartCoroutine(Advance());
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
            Feedback = "Challenge complete. Interact with the Book Statue to start the next challenge.";
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
            if (!Ready || Moving || (State != GraphStageState.WAITING_FOR_ACTIVATION && State != GraphStageState.ACTIVE)) return;
            lessonOpen = open;
            LocalPlayer.ToggleControl(!open);
            input.SetGameplayInputBlocked(open);
            // Lesson reading is outside active gameplay duration.
            if (State == GraphStageState.ACTIVE)
            { if (open) timer.Pause(); else timer.Resume(); }
            ui.ShowLesson(open);
        }
        public void RestartStage()
        {
            Time.timeScale = 1; input?.SetGameplayInputBlocked(false);
            TeleportManager.Instance.LoadSceneWithFade("Graph");
        }
        public void ReturnToMenu()
        {
            Time.timeScale = 1; input?.SetGameplayInputBlocked(false);
            TeleportManager.Instance.LoadSceneWithFade("Main Menu");
        }
    }
}
