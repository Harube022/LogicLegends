using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.Graph
{
    public enum GraphTutorialStep { Movement, Jump, Interaction, Book, Minimap, Objective, Route, Timer,
        Vertices, Degree, Path, DirectedPath, Cycle, Retry, Submit, Progress, Confirm }

    /// <summary>Isolated control practice between the initial Help book and real book activation.</summary>
    [DefaultExecutionOrder(1100)]
    [DisallowMultipleComponent]
    public sealed class GraphControlsOnboarding : MonoBehaviour
    {
        [SerializeField, Range(.15f, .8f)] private float joystickDeadZone = .3f;
        [SerializeField, Min(.1f)] private float movementDistance = .35f;
        [SerializeField, Min(1f)] private float interactionRange = 2.5f;
        [Header("Guided camera")]
        [SerializeField, Min(.1f)] private float cameraTransitionSeconds = 1.25f;
        [SerializeField, Min(5f)] private float bookViewDistance = 12f;
        [SerializeField, Min(30f)] private float graphViewHeight = 85f;
        public static GraphControlsOnboarding Active { get; private set; }
        public bool IsActive { get; private set; }
        public GraphTutorialStep CurrentStep { get; private set; }
        public bool AllowsJump => IsActive && CurrentStep == GraphTutorialStep.Jump;
        public GameObject PracticeObject => null;
        public bool ShowingGameplayPreview { get; private set; }
        public GraphChallengeData PracticeGraph { get; private set; }
        public int? PracticeAnswer { get; private set; }
        public List<string> PracticeRoute { get; } = new List<string>();
        public string PracticeTitle { get; private set; } = "INTRODUCTION";
        public string PracticeObjective { get; private set; } = "Tap View Board near the book or board to read the task. Back returns without submitting.";
        public string PracticeFeedback { get; private set; } = "PRACTICE ONLY — no time or progress changes.";
        public bool CanPracticeConfirm => IsActive && (CurrentStep == GraphTutorialStep.Book || CurrentStep == GraphTutorialStep.Degree);

        private GraphStageManager stage;
        private GameInput input;
        private Player player;
        private MobileInputUI joystickInput;
        private RectTransform safeArea, joystick, jump, hand, instructions, highlight, arrow, card;
        private TMP_Text instructionText;
        private GameObject confirmation;
        private Button settings;
        private bool settingsWasEnabled, playerWasEnabled, inputWasBlocked, cursorWasVisible;
        private bool joystickWasVisible, jumpWasVisible, handWasVisible;
        private CursorLockMode cursorLock;
        private float previousTimeScale, movedDistance, jumpBaseY;
        private Vector3 originalPosition, lastPosition;
        private Quaternion originalRotation;
        private bool jumpSucceeded;
        private RectTransform[] blockers;
        private PrelimTutorialSkipControl skipControl;
        private RectTransform nextButton, uiTarget;
        private CinemachineCamera demonstrationCamera;
        private Vector3 cameraGoal;
        private Quaternion cameraRotationGoal;
        private readonly List<Behaviour> pausedCameraInputs = new List<Behaviour>();
        private readonly Dictionary<Renderer, bool> hiddenPlayer = new Dictionary<Renderer, bool>();
        private readonly Dictionary<GameObject, bool> hiddenArena = new Dictionary<GameObject, bool>();
        private Dictionary<Renderer,bool> overheadRoof;
        private readonly List<Renderer> worldTargets = new List<Renderer>();
        private GameObject practiceArena;
        private int acceptedInteractionFrame = -1;
        private RectTransform worldObstacle;
        private Renderer[] instructionWorldObjects;

        public bool Begin(GraphStageManager manager)
        {
            if (IsActive || manager.State != GraphStageState.WAITING_FOR_ACTIVATION || manager.ChallengeIndex != 0) return false;
            stage = manager;
            input = GameInput.Instance;
            player = manager.LocalPlayer;
            safeArea = manager.ui.host;
            var joystickHost = safeArea.Find("Gameplay_Interface/Joystick");
            joystickInput = joystickHost != null ? joystickHost.GetComponentInChildren<MobileInputUI>(true) : null;
            joystick = joystickInput != null ? joystickInput.transform as RectTransform : null;
            jump = safeArea.Find("Gameplay_Interface/JumpButton") as RectTransform;
            hand = safeArea.Find("Gameplay_Interface/InteractButton") as RectTransform;
            if (input == null || player == null || joystick == null || jump == null || hand == null)
            { Debug.LogError("Graph onboarding needs the existing Canvas joystick, jump and hand controls.", this); return false; }

            originalPosition = player.transform.position;
            originalRotation = player.transform.rotation;
            playerWasEnabled = player.enabled;
            inputWasBlocked = input.GameplayInputBlocked;
            previousTimeScale = Time.timeScale;
            cursorLock = Cursor.lockState; cursorWasVisible = Cursor.visible;
            joystickWasVisible = joystick.gameObject.activeSelf;
            jumpWasVisible = jump.gameObject.activeSelf;
            handWasVisible = hand.gameObject.activeSelf;
            settings = manager.ui.settingsButton != null ? manager.ui.settingsButton.GetComponent<Button>() : null;
            if (settings != null) { settingsWasEnabled = settings.interactable; settings.interactable = false; }
            BuildUI();
            Active = this; IsActive = true;
            player.OnJumpStarted += OnSuccessfulJump;
            stage.timer.Pause();
            stage.ui.SetControlsTutorialVisible(true);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            BeginMovement();
            return true;
        }

        private void BuildUI()
        {
            instructions = StageUITheme.Rect("Graph Control Instructions", safeArea);
            var backing = instructions.gameObject.AddComponent<Image>();
            backing.color = new Color(.06f, .13f, .19f, .95f); backing.raycastTarget = false;
            instructionText = StageUITheme.Text("Instruction", instructions, "", 36, true);
            instructionText.color = Color.white;
            MakeButton("Next", instructions, Vector2.zero, Vector2.one, AdvanceExplanation);
            nextButton = instructions.Find("Next Button") as RectTransform;
            nextButton.gameObject.SetActive(false);

            highlight = StageUITheme.Rect("Graph Control Highlight", safeArea);
            for (int i = 0; i < 4; i++)
            {
                var edge = StageUITheme.Rect("Highlight Edge", highlight);
                var image = edge.gameObject.AddComponent<Image>();
                image.color = new Color(1f, .86f, .22f, .95f); image.raycastTarget = false;
                edge.anchorMin = i == 0 ? new Vector2(0,1) : i == 3 ? new Vector2(1,0) : Vector2.zero;
                edge.anchorMax = i == 1 ? new Vector2(1,0) : i == 2 ? new Vector2(0,1) : Vector2.one;
                edge.sizeDelta = i < 2 ? new Vector2(0,7) : new Vector2(7,0);
                edge.anchoredPosition = Vector2.zero;
            }
            var arrowText = StageUITheme.Text("Arrow", highlight, "▼", 48, true);
            arrowText.color = new Color(1f,.9f,.25f); arrow = arrowText.rectTransform;

            var shade = StageUITheme.Rect("Graph Tutorial Confirmation", safeArea);
            StageUITheme.Stretch(shade, Vector2.zero, Vector2.one);
            shade.gameObject.AddComponent<Image>().color = new Color(0,0,0,.55f);
            confirmation = shade.gameObject;
            card = StageUITheme.Rect("Confirmation Card", shade);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f,.5f);
            card.gameObject.AddComponent<Image>().color = new Color(.06f,.16f,.22f,.98f);
            var question = StageUITheme.Text("Ready Question", card, "Let's start?", 48, true);
            question.color = Color.white;
            StageUITheme.Stretch(question.rectTransform, new Vector2(.06f,.54f), new Vector2(.94f,.88f));
            MakeButton("Yes", card, new Vector2(.07f,.1f), new Vector2(.46f,.43f), ConfirmYes);
            MakeButton("No", card, new Vector2(.54f,.1f), new Vector2(.93f,.43f), ConfirmNo);
            confirmation.SetActive(false);
            skipControl = PrelimTutorialSkipControl.Create(safeArea, SkipTutorial);
            worldObstacle = StageUITheme.Rect("Graph Tutorial World Subject", safeArea);
            worldObstacle.anchorMin = worldObstacle.anchorMax = worldObstacle.pivot = new Vector2(.5f,.5f);
            worldObstacle.gameObject.SetActive(false);
            var obstacles = new List<RectTransform>(ControlTutorialLayout.FindBlockers());
            // The practice label joins the obstacle list when it is created.
            blockers = obstacles.ToArray();
        }

        private static void MakeButton(string label, Transform parent, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction callback)
        {
            var rect = StageUITheme.Rect(label + " Button", parent);
            StageUITheme.Stretch(rect, min, max);
            rect.gameObject.AddComponent<Image>().color = new Color(.12f,.48f,.43f);
            var button = rect.gameObject.AddComponent<Button>(); button.onClick.AddListener(callback);
            var text = StageUITheme.Text(label, rect, label, 40, true); text.color = Color.white;
            text.enableAutoSizing = true; text.fontSizeMin = 24; text.fontSizeMax = 40;
            StageUITheme.Stretch(text.rectTransform, new Vector2(.04f,.08f), new Vector2(.96f,.92f));
        }

        private void BeginMovement()
        {
            RestoreGuidedView();

            stage.ui.SetControlsTutorialVisible(true);
            skipControl.Show(true);
            Time.timeScale = previousTimeScale;
            input.SetGameplayInputBlocked(false);
            player.ToggleControl(true);
            PrelimTeleportTransition.MovePlayerImmediately(player.gameObject, originalPosition, originalRotation);
            joystickInput.ResetJoystick(); movedDistance = 0; lastPosition = player.transform.position;
            ShowStep(GraphTutorialStep.Movement, "Use this joystick to move.", joystick);
        }

        private void ShowStep(GraphTutorialStep next, string text, RectTransform target)
        {
            CurrentStep = next; instructionText.text = text;
            confirmation.SetActive(false); instructions.gameObject.SetActive(true);
            nextButton.gameObject.SetActive(false); uiTarget = target; worldTargets.Clear();
            joystick.gameObject.SetActive(next == GraphTutorialStep.Movement || next == GraphTutorialStep.Interaction);
            jump.gameObject.SetActive(next == GraphTutorialStep.Jump);
            hand.gameObject.SetActive(next == GraphTutorialStep.Interaction);
            highlight.SetParent(target, false);
            StageUITheme.Stretch(highlight, Vector2.zero, Vector2.one);
            highlight.offsetMin = -Vector2.one * 10; highlight.offsetMax = Vector2.one * 10;
            highlight.gameObject.SetActive(true); highlight.SetAsLastSibling(); instructions.SetAsLastSibling();
        }

        /// <summary>The movement lesson requires the actual joystick, rather than keyboard movement.</summary>
        public bool TryGetMovementOverride(out Vector2 movement)
        {
            movement = Vector2.zero;
            if (!IsActive) return false;
            if (CurrentStep == GraphTutorialStep.Movement)
            {
                if (joystickInput.CurrentInput.sqrMagnitude > joystickDeadZone * joystickDeadZone)
                    movement = joystickInput.CurrentInput.normalized;
                return true;
            }
            return CurrentStep != GraphTutorialStep.Interaction;
        }

        private void OnSuccessfulJump()
        { if (AllowsJump) jumpSucceeded = true; }

        private void Update()
        {
            if (!IsActive || player == null || Time.timeScale <= 0) return;
            Vector3 position = player.transform.position;
            if (position.y < stage.arena.position.y - 2.5f)
            { BeginMovement(); return; } // Recover practice falls without touching the gameplay route.
            if (CurrentStep == GraphTutorialStep.Movement)
            {
                if (joystickInput.CurrentInput.sqrMagnitude > joystickDeadZone * joystickDeadZone)
                    movedDistance += Vector2.Distance(new Vector2(lastPosition.x,lastPosition.z), new Vector2(position.x,position.z));
                if (movedDistance >= movementDistance)
                {
                    joystickInput.ResetJoystick(); jumpBaseY = position.y; jumpSucceeded = false;
                    ShowStep(GraphTutorialStep.Jump, "Tap this button to jump.", jump);
                }
            }
            else if (CurrentStep == GraphTutorialStep.Jump && jumpSucceeded && player.IsJumping() && position.y > jumpBaseY + .1f)
            {

                ShowStep(GraphTutorialStep.Interaction, "Tap the hand button to interact with nearby objects.", hand);
            }
            lastPosition = position;
        }

        private void LateUpdate()
        {
            if (!IsActive) return;
            card.sizeDelta = new Vector2(Mathf.Min(660, safeArea.rect.width - 40), 280);
            if (demonstrationCamera != null)
            {
                float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 5f / cameraTransitionSeconds);
                demonstrationCamera.transform.position = Vector3.Lerp(demonstrationCamera.transform.position, cameraGoal, blend);
                demonstrationCamera.transform.rotation = Quaternion.Slerp(demonstrationCamera.transform.rotation, cameraRotationGoal, blend);
            }

            if (CurrentStep == GraphTutorialStep.Confirm) return;
            var obstacles = new List<RectTransform>(blockers);
            foreach (string name in new[] { "Objective", "Recorded route", "Clock", "Graph minimap", "Feedback", "Confirm", "Retry", "Degree choices" })
                obstacles.Add(stage.ui.TutorialTarget(name));
            UpdateWorldHighlight();
            worldObstacle.gameObject.SetActive(instructionWorldObjects != null && ProjectWorldBounds(instructionWorldObjects, worldObstacle));
            obstacles.Add(worldObstacle);
            ControlTutorialLayout.Place(instructions, instructionText, nextButton, safeArea,
                worldTargets.Count > 0 ? highlight : uiTarget,
                false, obstacles.ToArray(), arrow);
        }

        public void TryPracticeInteraction()
        {
            if (IsActive && CurrentStep == GraphTutorialStep.Book)
            { PracticeBook(); return; }
            if (!IsActive || CurrentStep != GraphTutorialStep.Interaction ||
                input.GameplayInputBlocked || !player.enabled || Time.timeScale <= 0) return;
            acceptedInteractionFrame = Time.frameCount;

            BeginGuidedView();
        }

        private void BeginGuidedView()
        {
            overheadRoof = GraphOverheadVisibility.HideCeiling();
            joystickInput.ResetJoystick(); player.ToggleControl(false);
            PrelimTeleportTransition.MovePlayerImmediately(player.gameObject, originalPosition, originalRotation);
            foreach (var renderer in player.GetComponentsInChildren<Renderer>(true))
            { hiddenPlayer[renderer] = renderer.enabled; renderer.enabled = false; }
            foreach (var item in FindObjectsByType<CameraTargetController>(FindObjectsSortMode.None)) PauseCamera(item);
            foreach (var item in FindObjectsByType<CinemachineInputAxisController>(FindObjectsSortMode.None)) PauseCamera(item);
            foreach (var item in FindObjectsByType<CinemachinePinchZoom>(FindObjectsSortMode.None)) PauseCamera(item);
            foreach (var item in FindObjectsByType<ThirdPersonCameraController>(FindObjectsSortMode.None)) PauseCamera(item);
            MobileLookInput.ResetDelta();
            var rendered = Camera.main;
            demonstrationCamera = new GameObject("Graph Tutorial Camera").AddComponent<CinemachineCamera>();
            demonstrationCamera.transform.SetPositionAndRotation(rendered.transform.position, rendered.transform.rotation);
            var lens = demonstrationCamera.Lens; lens.FieldOfView = rendered.fieldOfView; demonstrationCamera.Lens = lens;
            var priority = demonstrationCamera.Priority; priority.Value = 1000; demonstrationCamera.Priority = priority;
            Vector3 forward = rendered.transform.forward; forward.y = 0; forward.Normalize();
            Vector3 center = BookCenter();
            cameraGoal = center - forward * bookViewDistance + Vector3.up * 3f;
            cameraRotationGoal = Quaternion.LookRotation(center - cameraGoal);
            ShowingGameplayPreview = true;
            // The preview uses X/Y/Z/W, independent of the selected run and catalog assets.
            PracticeGraph = CreatePracticeGraph(GraphChallengeType.Path);
            stage.ui.RebuildMap(PracticeGraph);
            SetGuidedStep(GraphTutorialStep.Book,
                "Interact with this book statue to start each challenge. Tap the hand button. This is practice only.",
                hand, false);
            SetWorldTargets(stage.bookStatue.GetComponentsInChildren<Renderer>());
            hand.gameObject.SetActive(true);
        }

        private void PauseCamera(Behaviour item)
        { if (item.enabled) { pausedCameraInputs.Add(item); item.enabled = false; } }

        private Vector3 BookCenter()
        {
            var renderers = stage.bookStatue.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return stage.bookStatue.position + Vector3.up * 2;
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds.center;
        }

        private void SetGuidedStep(GraphTutorialStep step, string message, RectTransform target, bool next, params string[] elements)
        {
            CurrentStep = step; uiTarget = target; worldTargets.Clear(); instructionWorldObjects = null;
            instructionText.text = message; instructions.gameObject.SetActive(true);
            confirmation.SetActive(false); nextButton.gameObject.SetActive(next);
            joystick.gameObject.SetActive(false); jump.gameObject.SetActive(false); hand.gameObject.SetActive(false);
            stage.ui.ShowPracticeElements(elements);
            if (target != null)
            {
                highlight.SetParent(target, false); StageUITheme.Stretch(highlight, Vector2.zero, Vector2.one);
                highlight.offsetMin = -Vector2.one * 10; highlight.offsetMax = Vector2.one * 10;
            }
            highlight.gameObject.SetActive(target != null);
            instructions.SetAsLastSibling(); skipControl.Show(true);
        }

        private void SetWorldTargets(params Renderer[] renderers)
        {
            worldTargets.Clear(); worldTargets.AddRange(renderers);
            highlight.SetParent(safeArea, false);
            highlight.anchorMin = highlight.anchorMax = highlight.pivot = new Vector2(.5f,.5f);
            arrow.gameObject.SetActive(false); highlight.gameObject.SetActive(true);
        }

        private void UpdateWorldHighlight()
        {
            arrow.gameObject.SetActive(worldTargets.Count == 0);
            if (worldTargets.Count == 0 || Camera.main == null) return;
            highlight.gameObject.SetActive(ProjectWorldBounds(worldTargets, highlight));
        }
        private bool ProjectWorldBounds(IEnumerable<Renderer> renderers, RectTransform bounds)
        {
            if (Camera.main == null) return false;
            Vector2 min = new Vector2(float.MaxValue,float.MaxValue), max = -min; bool visible = false;
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                Bounds b = renderer.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    Vector3 screen = Camera.main.WorldToScreenPoint(corner);
                    if (screen.z <= 0) continue;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(safeArea, screen, null, out Vector2 p))
                    { min = Vector2.Min(min,p); max = Vector2.Max(max,p); visible = true; }
                }
            }
            if (!visible) return false;
            min = Vector2.Max(min - Vector2.one * 12, safeArea.rect.min + Vector2.one * 12);
            max = Vector2.Min(max + Vector2.one * 12, safeArea.rect.max - Vector2.one * 12);
            bounds.anchoredPosition = (min + max) * .5f - safeArea.rect.center;
            bounds.sizeDelta = Vector2.Max(Vector2.one * 20, max - min);
            return true;
        }

        private void PracticeBook()
        {
            if (!IsActive || CurrentStep != GraphTutorialStep.Book || !stage.NearBook || acceptedInteractionFrame == Time.frameCount) return;
            acceptedInteractionFrame = Time.frameCount;
            SetGuidedStep(GraphTutorialStep.Minimap,
                "Use this minimap to navigate the stage and locate important areas. Points are platforms; lines are bridges. Tap the minimap for a larger view, then Close to return.",
                stage.ui.TutorialTarget("Graph minimap"), true, "Graph minimap");
        }

        public void PracticeSelect(int value)
        {
            if (!IsActive || CurrentStep != GraphTutorialStep.Degree || value < 0 || value > 6) return;
            PracticeAnswer = value; PracticeFeedback = "PRACTICE: selected " + value + ". Tap CONFIRM to check.";
            stage.ui.Refresh();
        }
        public void PracticeConfirm()
        {
            if (!IsActive) return;
            if (CurrentStep == GraphTutorialStep.Book) { PracticeBook(); return; }
            if (CurrentStep != GraphTutorialStep.Degree) return;
            if (!GraphAnswerValidator.Validate(PracticeGraph, PracticeRoute, PracticeAnswer, true, out string reason))
            {
                PracticeFeedback = "PRACTICE: Incorrect. Count the bridges touching W and try again. No penalty.";
                foreach (var vertex in practiceArena.GetComponentsInChildren<GraphVertex>()) vertex.ShowAnswerFeedback(false, 1.2f);
                stage.ui.Refresh(); return;
            }
            foreach (var vertex in practiceArena.GetComponentsInChildren<GraphVertex>()) vertex.ShowAnswerFeedback(true, 1.2f);
            PracticeFeedback = "PRACTICE: Correct! Real correct answers prepare the next graph.";
            BeginPathExplanation();
        }
        public void PracticeRetry()
        {
            if (!IsActive) return;
            if (CurrentStep == GraphTutorialStep.Degree)
            { PracticeAnswer = null; PracticeFeedback = "PRACTICE: selection cleared."; stage.ui.Refresh(); return; }
            if (CurrentStep != GraphTutorialStep.Retry) return;
            PracticeRoute.Clear(); PracticeRoute.Add("X");
            PracticeFeedback = "PRACTICE: route reset. Real retries preserve time and mistakes.";
            foreach (var edge in practiceArena.GetComponentsInChildren<GraphEdge>()) edge.SetWalked(false);
            SetGuidedStep(GraphTutorialStep.Submit,
                "CONFIRM checks the selected degree or full recorded route. Stop on the destination platform to submit; you do not need to return to the book.",
                stage.ui.TutorialTarget("Confirm"), true, "Confirm", "Feedback");
        }

        public void AdvanceExplanation()
        {
            if (!IsActive || !nextButton.gameObject.activeSelf) return;
            switch (CurrentStep)
            {
                case GraphTutorialStep.Minimap:
                    SetGuidedStep(GraphTutorialStep.Objective,
                        "Tap View Board near the book or board to read the task. Back returns without submitting. Finish two questions per challenge.",
                        stage.ui.TutorialTarget("Objective"), true, "Objective"); break;
                case GraphTutorialStep.Objective:
                    PracticeRoute.Clear(); PracticeRoute.AddRange(new[] { "X", "Y" });
                    SetGuidedStep(GraphTutorialStep.Route,
                        "The minimap footer records completed bridge crossings. Orange bridges are available; blue means crossed. Degree questions show your selected number here.",
                        stage.ui.TutorialTarget("Recorded route"), true, "Recorded route"); break;
                case GraphTutorialStep.Route:
                    SetGuidedStep(GraphTutorialStep.Timer,
                        "One " + StageJourneyUI.FormatTime(stage.countdownSeconds) + " countdown covers the whole run. It starts at the book, pauses for Help, board viewing and between challenges, and carries over. Incorrect answers cost 10 seconds.",
                        stage.ui.TutorialTarget("Clock"), true, "Clock"); break;
                case GraphTutorialStep.Timer: BuildPracticeArena(GraphChallengeType.VerticesAndEdges);
                    PracticeTitle = "VERTICES AND EDGES"; PracticeObjective = "Practice graph — platforms are vertices and bridges are edges.";
                    SetGuidedStep(GraphTutorialStep.Vertices,
                        "Platforms are vertices; bridges are edges. For travel tasks, cross a connecting bridge from the stated start to the destination, then press CONFIRM there.",
                        null, true, "Objective"); FocusPracticeGraph(); break;
                case GraphTutorialStep.Vertices: BuildPracticeArena(GraphChallengeType.Degree);
                    PracticeTitle = "VERTEX DEGREE"; PracticeObjective = "Count the bridges meeting practice vertex W. Choose a number, then CONFIRM.";
                    PracticeAnswer = null; PracticeFeedback = "PRACTICE ONLY: inspect W. No time or penalties.";
                    SetGuidedStep(GraphTutorialStep.Degree,
                        "A vertex's degree counts its incident edges. Count the bridges touching W, choose a number, and tap CONFIRM. CLEAR SELECTION lets you choose again.",
                        stage.ui.TutorialTarget("Degree choices"), false, "Objective", "Degree choices", "Confirm", "Retry", "Feedback");
                    FocusPracticeVertex("W"); break;
                case GraphTutorialStep.Path: BuildPracticeArena(GraphChallengeType.DirectedPath);
                    PracticeTitle = "DIRECTED PATH"; PracticeObjective = "Practice arrows show the allowed direction.";
                    SetGuidedStep(GraphTutorialStep.DirectedPath,
                        "In a directed path, follow the arrows from the stated start to the destination. Reverse travel is blocked. A simple path must not repeat any vertex.",
                        null, true, "Objective", "Graph minimap"); FocusPracticeGraph(); break;
                case GraphTutorialStep.DirectedPath: BuildPracticeArena(GraphChallengeType.Cycle);
                    PracticeTitle = "CYCLE"; PracticeObjective = "Practice cycle: X → Y → Z → X.";
                    PracticeRoute.Clear(); PracticeRoute.AddRange(new[] { "X", "Y", "Z", "X" }); ShowPracticeRoute();
                    SetGuidedStep(GraphTutorialStep.Cycle,
                        "A cycle returns to its start after visiting at least three distinct vertices. Do not repeat an intermediate vertex. Only the start repeats to close the cycle.",
                        null, true, "Objective", "Recorded route"); FocusPracticeGraph(); break;
                case GraphTutorialStep.Cycle:
                    SetGuidedStep(GraphTutorialStep.Retry,
                        "Leaving a connecting bridge interrupts your route. RETRY ROUTE returns you to the start and clears the route, keeping time and mistakes. Try the practice Retry button.",
                        stage.ui.TutorialTarget("Retry"), false, "Retry", "Recorded route"); break;
                case GraphTutorialStep.Submit:
                    PracticeTitle = "PROGRESSION"; PracticeObjective = "PRACTICE — no challenges completed.";
                    PracticeFeedback = "Incorrect: -10 seconds and a brief movement stop. Correct: next graph, then a fresh book interaction.";
                    SetGuidedStep(GraphTutorialStep.Progress,
                        "Wrong answers keep your route or selection for correction.\nCorrect answers pause time. Start again at the book.\nFinish two questions in each of five challenges.",
                        stage.ui.TutorialTarget("Feedback"), true, "Feedback"); break;
                case GraphTutorialStep.Progress: BeginConfirmation(); break;
            }
        }

        private void BeginPathExplanation()
        {
            BuildPracticeArena(GraphChallengeType.Path);
            PracticeTitle = "SIMPLE PATH"; PracticeObjective = "Practice route: X → Y → Z → W.";
            PracticeRoute.Clear(); PracticeRoute.AddRange(new[] { "X", "Y", "Z", "W" }); ShowPracticeRoute();
            SetGuidedStep(GraphTutorialStep.Path,
                "A simple path follows connected edges to the objective's destination without repeating a vertex. Cross bridges fully to record the route, then CONFIRM at the destination.",
                null, true, "Objective", "Recorded route", "Feedback"); FocusPracticeGraph();
        }

        private static GraphChallengeData CreatePracticeGraph(GraphChallengeType type)
        {
            var source = new GraphChallengeData { type = type, start = "X", destination = "W", degreeVertex = "W" };
            source.vertices.AddRange(new[] { new GraphVertexData("X",-12,0),new GraphVertexData("Y",-6,18),
                new GraphVertexData("Z",0,0),new GraphVertexData("W",12,0) });
            bool directed = type == GraphChallengeType.DirectedPath;
            source.edges.AddRange(new[] { new GraphEdgeData("X","Y",directed),new GraphEdgeData("Y","Z",directed),
                new GraphEdgeData("Z","X",directed),new GraphEdgeData("Z","W",directed) });
            return GraphGeometry.Layout(source);
        }
        private void BuildPracticeArena(GraphChallengeType type)
        {
            if (practiceArena != null) { practiceArena.SetActive(false); Destroy(practiceArena); }
            foreach (Transform child in stage.arena)
            { if (!hiddenArena.ContainsKey(child.gameObject)) hiddenArena[child.gameObject] = child.gameObject.activeSelf; child.gameObject.SetActive(false); }
            practiceArena = new GameObject("Graph Tutorial Practice Graph");
            practiceArena.transform.SetPositionAndRotation(stage.arena.position, stage.arena.rotation);
            practiceArena.transform.localScale = stage.arena.lossyScale;
            PracticeGraph = CreatePracticeGraph(type); PracticeRoute.Clear(); PracticeRoute.Add("X");
            foreach (var data in PracticeGraph.vertices)
            {
                var vertex = Instantiate(stage.vertexPrefab, practiceArena.transform); vertex.Configure(data);
                if (vertex.label != null)
                { vertex.label.fontSize *= 2; vertex.label.rectTransform.sizeDelta *= 2; }
            }
            foreach (var data in PracticeGraph.edges)
                Instantiate(stage.edgePrefab, practiceArena.transform).Configure(data,PracticeGraph.Vertex(data.source).position,PracticeGraph.Vertex(data.destination).position);
            // Walkthrough views use no real collision, arrival triggers, or gate controllers.
            foreach (var collider in practiceArena.GetComponentsInChildren<Collider>()) collider.enabled = false;
            stage.ui.RebuildMap(PracticeGraph);
        }
        private void ShowPracticeRoute()
        {
            foreach (var edge in practiceArena.GetComponentsInChildren<GraphEdge>())
                for (int i = 1; i < PracticeRoute.Count; i++)
                    if (PracticeRoute[i-1] == edge.data.source && PracticeRoute[i] == edge.data.destination) edge.SetWalked(true);
        }
        private void FocusPracticeGraph()
        {
            Vector3 center = practiceArena.transform.TransformPoint(new Vector3(0,0,9) * GraphGeometry.LayoutScale);
            cameraGoal = center + new Vector3(0,graphViewHeight,-graphViewHeight * .8f);
            cameraRotationGoal = Quaternion.LookRotation(center + Vector3.up * 10 - cameraGoal);
            instructionWorldObjects = practiceArena.GetComponentsInChildren<Renderer>();
            // Outline a representative bridge, rather than covering the whole view.
            SetWorldTargets(practiceArena.GetComponentInChildren<GraphEdge>().GetComponentsInChildren<Renderer>());
        }
        private void FocusPracticeVertex(string id)
        {
            foreach (var vertex in practiceArena.GetComponentsInChildren<GraphVertex>())
                if (vertex.vertexId == id)
                {
                    Vector3 center = vertex.transform.position + Vector3.up * 3;
                    Vector3 lookAt = center + Vector3.right * 12;
                    cameraGoal = lookAt + new Vector3(0,26,-38); cameraRotationGoal = Quaternion.LookRotation(lookAt-cameraGoal);
                    instructionWorldObjects = vertex.GetComponentsInChildren<Renderer>();
                    // Keep the choices highlighted while framing the vertex and its incident bridge.
                }
        }

        private void RestoreGuidedView()
        {
            GraphOverheadVisibility.Restore(overheadRoof);
            worldTargets.Clear(); uiTarget = null; instructionWorldObjects = null;
            if (worldObstacle != null) worldObstacle.gameObject.SetActive(false);
            if (demonstrationCamera != null) PrelimTutorialSkipControl.CancelCamera(demonstrationCamera);
            demonstrationCamera = null;
            foreach (var item in pausedCameraInputs) if (item != null) item.enabled = true;
            pausedCameraInputs.Clear(); MobileLookInput.ResetDelta();
            foreach (var item in hiddenPlayer) if (item.Key != null) item.Key.enabled = item.Value;
            hiddenPlayer.Clear();
            foreach (var item in hiddenArena) if (item.Key != null) item.Key.SetActive(item.Value);
            hiddenArena.Clear();
            if (practiceArena != null) { practiceArena.SetActive(false); Destroy(practiceArena); }
            practiceArena = null;
            if (ShowingGameplayPreview) stage.ui.RestorePracticeElements();
            ShowingGameplayPreview = false;
            PracticeGraph = null; PracticeAnswer = null; PracticeRoute.Clear();
        }

        public void SkipTutorial()
        { if (IsActive && CurrentStep != GraphTutorialStep.Confirm) BeginConfirmation(); }
        private void BeginConfirmation()
        {
            CurrentStep = GraphTutorialStep.Confirm; // Consume taps before cancelling any view.
             RestoreGuidedView();
            highlight.gameObject.SetActive(false); instructions.gameObject.SetActive(false);
            stage.ui.SetControlsTutorialVisible(true); skipControl.Show(false);
            joystickInput.ResetJoystick(); input.SetGameplayInputBlocked(true); player.ToggleControl(false);
            PrelimTeleportTransition.MovePlayerImmediately(player.gameObject, originalPosition, originalRotation);
            Time.timeScale = 0; confirmation.SetActive(true); confirmation.transform.SetAsLastSibling();
        }

        public void ConfirmNo()
        { if (IsActive && CurrentStep == GraphTutorialStep.Confirm) BeginMovement(); }
        public void ConfirmYes()
        { if (IsActive && CurrentStep == GraphTutorialStep.Confirm) Cleanup(); }

        private void Cleanup()
        {
            if (!IsActive) return;
            IsActive = false; if (Active == this) Active = null;
            RestoreGuidedView();
            if (skipControl != null) skipControl.Release(); skipControl = null;

            if (player != null)
            {
                player.OnJumpStarted -= OnSuccessfulJump;
                PrelimTeleportTransition.MovePlayerImmediately(player.gameObject, originalPosition, originalRotation);
                player.ToggleControl(playerWasEnabled);
            }
            if (input != null) input.SetGameplayInputBlocked(inputWasBlocked);
            if (joystickInput != null) joystickInput.ResetJoystick();
            if (joystick != null) joystick.gameObject.SetActive(joystickWasVisible);
            if (jump != null) jump.gameObject.SetActive(jumpWasVisible);
            if (hand != null) hand.gameObject.SetActive(handWasVisible);
            if (settings != null) settings.interactable = settingsWasEnabled;
            foreach (var item in new[] { highlight, instructions, worldObstacle })
                if (item != null) { item.gameObject.SetActive(false); Destroy(item.gameObject); }
            if (confirmation != null) { confirmation.SetActive(false); Destroy(confirmation); }
            if (stage != null && stage.ui != null) stage.ui.SetControlsTutorialVisible(false);
            Time.timeScale = previousTimeScale;
            Cursor.lockState = cursorLock; Cursor.visible = cursorWasVisible;
        }
        private void OnDisable() => Cleanup();
    }
}
