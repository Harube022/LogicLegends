using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace LogicLegends.Inference
{
    public enum InferencePuzzleStage { CompletingPremises, FindingDiamond, Solved }
    public partial class InferenceChallenge : MonoBehaviour
    {
        [Header("Editable challenge content")]
        public InferenceRule[] rules;
        [Min(1)] public int requiredRounds = 1;
        [Header("Existing camera / player integration")]
        public GameObject focusCamera;
        public GameObject gameplayHud;
        [Header("Board")]
        public GameObject board;
        public RectTransform premiseContainer;
        public TMP_FontAsset font;
        public TMP_Text heading, legend, wordBank, feedback, conclusionLabel;
        public UnityEngine.UI.Button placeButton, validateButton, exploreButton, nextButton;
        public CanvasGroup boardInput;
        [Header("Optional drag-and-drop board (standalone scene)")]
        public RulesOfInferenceBoard dragPuzzle;
        [Header("Physical crystals")]
        public InferenceCrystal crystalPrefab;
        [Tooltip("Optional pillar crystal used only by the standalone crystal search.")]
        public InferenceCrystal pillarCrystalPrefab;
        [Tooltip("How far below each pillar-top crystal its pickup volume reaches toward the player.")]
        [Min(0f)] public float crystalPickupDrop = 3f;
        [Tooltip("Pickup volume around a pillar. This is a trigger, so it does not block movement.")]
        [Min(0.5f)] public float crystalPickupRadius = 3.5f;
        public Transform[] crystalSpawns;
        public Transform conclusionSocket;
        public GameObject completionMarker;
        public UnityEvent onChallengeCompleted = new UnityEvent();
        public event Action<int> onRoundCompleted;
        public event Action<int> onBookStarted;
        public event Action onBoardClosed;
        public bool PuzzleCompleted { get; private set; }
        public bool HasStarted { get; private set; }
        public bool IsBoardOpen { get; private set; }
        public InferenceQuestion Question { get; private set; }
        public int SolvedRounds { get; private set; }
        public InferencePuzzleStage Stage { get; private set; }
        public IReadOnlyList<InferenceCrystal> Crystals => crystals;
        public InferenceCrystal PlacedCrystal => placed;
        [Range(4, 9)] public int ruleDiamondCount = 4;
        public GameObject diamondPlacementMarker;

        readonly List<TMP_InputField> fields = new List<TMP_InputField>();
        readonly List<string> tokens = new List<string>();
        readonly List<InferenceCrystal> crystals = new List<InferenceCrystal>();
        readonly List<Behaviour> pausedCameraInputs = new List<Behaviour>();
        InferenceDeck deck;
        InferenceQuestion[] stageQuestions;
        InferenceCrystal placed;
        Player player;
        bool restorePlayerControl, hudWasActive, previousCursorVisible;
        CursorLockMode previousCursorLock;
        bool roundSolved;

        void OnValidate()
        {
            if (dragPuzzle != null && gameObject.scene.name == "RulesOfInference") requiredRounds = InferenceStageQuestions.ChallengeCount;
        }

        void Awake()
        {
            deck = new InferenceDeck(Guid.NewGuid().GetHashCode());
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "RulesOfInference" && dragPuzzle != null)
            {
                requiredRounds = InferenceStageQuestions.ChallengeCount;
                stageQuestions = InferenceStageQuestions.Select(rules, Guid.NewGuid().GetHashCode());
                var access = gameObject.AddComponent<InferenceAltarAccess>(); access.Build();
                var interaction = gameObject.AddComponent<InferenceCrystalInteraction>(); interaction.Initialize(this);
                if (GetComponent<InferenceCrystalQuiz>() == null) gameObject.AddComponent<InferenceCrystalQuiz>();
            }
            placeButton.onClick.AddListener(PlaceHeldCrystal);
            validateButton.onClick.AddListener(ValidateBoard);
            exploreButton.onClick.AddListener(CloseBoard);
            nextButton.onClick.AddListener(NextChallenge);
        }

        void Update()
        {
            if (dragPuzzle == null || !IsBoardOpen || !placeButton.gameObject.activeSelf) return;
            var held = player == null ? null : player.GetHeldObject();
            var crystal = held == null ? null : held.GetComponent<InferenceCrystal>();
            placeButton.interactable = crystal != null && crystal.QuestionId == Question.Id && held.GetComponent<RuleDiamond>() != null;
        }

        void OnEnable()
        {
            board.SetActive(false); focusCamera.SetActive(false);
            if (diamondPlacementMarker != null) diamondPlacementMarker.SetActive(false);
            Question = null;
            HasStarted = false;
        }

        void OnDisable()
        {
            if (diamondPlacementMarker != null) diamondPlacementMarker.SetActive(false);
            CloseBoard();
            ClearCrystals();
            Question = null;
            HasStarted = false;
        }

        public bool StartFromBook()
        {
            if (!isActiveAndEnabled || HasStarted || PuzzleCompleted || IsBoardOpen ||
                (InferenceControlsOnboarding.Active != null && InferenceControlsOnboarding.Active.IsActive)) return false;
            HasStarted = true;
            onBookStarted?.Invoke(SolvedRounds + 1);
            return true;
        }

        public bool DebugClearCurrentChallenge()
        {
            if (!HasStarted || PuzzleCompleted) return false;
            CompleteRound();
            return true;
        }

        public void OpenBoard(Player enteringPlayer)
        {
            if (IsBoardOpen || enteringPlayer == null || !isActiveAndEnabled ||
                (InferenceControlsOnboarding.Active != null && InferenceControlsOnboarding.Active.IsActive)) return;
            player = enteringPlayer;
            // TMP must initialize before measuring the first set of premise labels.
            if (dragPuzzle != null) board.SetActive(true);
            if (HasStarted)
            {
                premiseContainer.gameObject.SetActive(true);
                conclusionLabel.gameObject.SetActive(true);
                if (dragPuzzle != null)
                {
                    dragPuzzle.wordBank.transform.parent.parent.gameObject.SetActive(true);
                    dragPuzzle.conclusion.label.transform.parent.gameObject.SetActive(true);
                }
                if (Question == null) NewRound();
            }
            else ShowReadOnlyBoard();
            restorePlayerControl = player.enabled;
            player.ToggleControl(false);
            previousCursorVisible = Cursor.visible; previousCursorLock = Cursor.lockState;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            hudWasActive = gameplayHud != null && gameplayHud.activeSelf;
            if (gameplayHud != null) gameplayHud.SetActive(false);
            exploreButton.gameObject.SetActive(true);
            var backLabel = exploreButton.GetComponentInChildren<TMP_Text>();
            if (backLabel != null) backLabel.text = "Back";
            foreach (var input in FindObjectsByType<Unity.Cinemachine.CinemachineInputAxisController>(FindObjectsSortMode.None))
                if (input.enabled) { pausedCameraInputs.Add(input); input.enabled = false; }
            foreach (var input in FindObjectsByType<CinemachinePinchZoom>(FindObjectsSortMode.None))
                if (input.enabled) { pausedCameraInputs.Add(input); input.enabled = false; }
            IsBoardOpen = true;
            if (completionMarker != null) completionMarker.SetActive(false);
            focusCamera.SetActive(true); board.SetActive(true);
            var canvas = board.GetComponent<Canvas>();
            if (canvas != null) canvas.worldCamera = Camera.main;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            // Wait for the existing Cinemachine blend before accepting board clicks.
            boardInput.interactable = false; boardInput.blocksRaycasts = false;
            var brain = Camera.main == null ? null : Camera.main.GetComponent<Unity.Cinemachine.CinemachineBrain>();
            Invoke(nameof(EnableBoardInput), brain == null ? 0.1f : brain.DefaultBlend.Time + 0.1f);
        }

        void ShowReadOnlyBoard()
        {
            heading.text = "RULES OF INFERENCE";
            legend.gameObject.SetActive(false);
            wordBank.gameObject.SetActive(false);
            premiseContainer.gameObject.SetActive(false);
            conclusionLabel.gameObject.SetActive(false);
            placeButton.gameObject.SetActive(false);
            validateButton.gameObject.SetActive(false);
            nextButton.gameObject.SetActive(false);
            if (dragPuzzle != null)
            {
                dragPuzzle.wordBank.transform.parent.parent.gameObject.SetActive(false);
                dragPuzzle.conclusion.label.transform.parent.gameObject.SetActive(false);
            }
            feedback.text = "Board preview. Interact with the Book Statue to start the game.";
            feedback.color = new Color(0.8f, 0.88f, 0.88f);
        }

        void EnableBoardInput()
        {
            if (!IsBoardOpen) return;
            boardInput.interactable = true; boardInput.blocksRaycasts = true;
        }

        public void CloseBoard()
        {
            CancelInvoke(nameof(EnableBoardInput));
            if (!IsBoardOpen) return;
            foreach (var field in fields) field.DeactivateInputField();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            board.SetActive(false); focusCamera.SetActive(false);
            if (completionMarker != null) completionMarker.SetActive(PuzzleCompleted);
            if (player != null && restorePlayerControl) player.ToggleControl(true);
            if (gameplayHud != null) gameplayHud.SetActive(hudWasActive);
            foreach (var input in pausedCameraInputs) if (input != null) input.enabled = true;
            pausedCameraInputs.Clear();
            Cursor.lockState = previousCursorLock; Cursor.visible = previousCursorVisible;
            IsBoardOpen = false;
            if (roundSolved && !PuzzleCompleted)
            {
                ClearCrystals(); Question = null; roundSolved = false;
            }
            onBoardClosed?.Invoke();
        }

        public void NextChallenge()
        {
            // Every new round must be started through the book statue.
            if (!IsBoardOpen || !roundSolved) return;
            CloseBoard();
        }

        void NewRound()
        {
            ClearCrystals();
            Question = stageQuestions != null ? stageQuestions[SolvedRounds] : deck.Draw(rules, dragPuzzle != null);
            roundSolved = false;
            if (dragPuzzle != null)
            {
                Stage = InferencePuzzleStage.CompletingPremises;
                heading.text = stageQuestions == null ? "RULES OF INFERENCE" : "RULES OF INFERENCE — CHALLENGE " + (SolvedRounds + 1) + " OF 5";
                dragPuzzle.Build(Question, rules);
                legend.gameObject.SetActive(false);
                wordBank.text = "WORD BANK"; wordBank.gameObject.SetActive(true);
                feedback.text = "Drag the words into the blanks, then check the argument.";
                feedback.color = new Color(0.8f, 0.88f, 0.88f);
                placeButton.gameObject.SetActive(false);
                nextButton.gameObject.SetActive(false);
                validateButton.interactable = true;
                RefreshStageButtons();
                return;
            }
            heading.text = Question.Rule.ruleName + "  /  " + Question.Rule.abbreviation;
            legend.text = "";
            var used = new HashSet<char>();
            foreach (var premise in Question.Form.premises)
                foreach (string token in premise.tokens)
                    foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(token, @"\{([pqrs])\}")) used.Add(m.Groups[1].Value[0]);
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(Question.Form.conclusion, @"\{([pqrs])\}")) used.Add(m.Groups[1].Value[0]);
            for (int i = 0; i < Question.Example.statements.Length; i++)
                if (used.Contains((char)('p' + i))) legend.text += (char)('p' + i) + ": " + Question.Example.statements[i] + "\n";
            var words = new List<string>();
            foreach (var premise in Question.Form.premises) foreach (string token in premise.tokens)
                if (token.StartsWith("[") && !words.Contains(token.Trim('[', ']'))) words.Add(token.Trim('[', ']'));
            wordBank.text = "WORD BANK    " + string.Join("     /     ", words);
            wordBank.gameObject.SetActive(words.Count > 0);
            BuildPremises();
            conclusionLabel.text = "Therefore, __________________________";
            feedback.text = Question.Rule.instruction + "\nConstruct the premises, then bring the conclusion crystal to the altar.";
            feedback.color = new Color(0.8f, 0.88f, 0.88f);
            nextButton.gameObject.SetActive(false);
            placeButton.interactable = validateButton.interactable = true;
            SpawnCrystals();
        }

        void SpawnRuleDiamonds()
        {
            bool nineTowers = gameObject.scene.name == "RulesOfInference" && pillarCrystalPrefab != null;
            int count = nineTowers ? 9 : Mathf.Clamp(ruleDiamondCount, 4, pillarCrystalPrefab != null ? 9 : 5);
            if (crystalSpawns.Length < count) throw new InvalidOperationException("Not enough rule diamond spawn points.");
            // Standalone towers have permanent rule identities, separate from pickup quiz topics.
            string[] codes = { "MP", "MT", "HS", "DS", "CD", "SIMP", "CONJ", "ADD", "DN" };
            var choices = nineTowers ? codes.Select(code => rules.Single(r => r.abbreviation == code)).ToArray()
                : RuleDiamond.Choose(Question.Rule, rules, count, new System.Random(Guid.NewGuid().GetHashCode()));
            for (int i = 0; i < choices.Length; i++)
            {
                var rule = choices[i];
                var spawn = crystalSpawns[i];
                var crystal = Instantiate(pillarCrystalPrefab != null ? pillarCrystalPrefab : crystalPrefab,
                    spawn.position, Quaternion.identity, transform);
                if (pillarCrystalPrefab != null)
                {
                    // Keep the original pillar ornaments as visual/spawn references.
                    var source = spawn.GetComponent<MeshRenderer>();
                    var visual = crystal.transform.Find("CrystalMesh");
                    if (source != null && visual != null)
                    {
                        visual.localRotation = spawn.rotation;
                        visual.localScale = spawn.lossyScale;
                        visual.GetComponent<MeshRenderer>().sharedMaterials = source.sharedMaterials;
                    }
                }
                crystal.name = "RuleDiamond_" + rule.abbreviation;
                ConfigureCrystalPickup(crystal);
                crystal.Configure(Question.Id, rule.ruleName + " (" + rule.abbreviation + ")", transform);
                crystal.gameObject.AddComponent<RuleDiamond>().Configure(rule);
                if (pillarCrystalPrefab != null) crystal.ConfigureTowerLabel(spawn.parent);
                crystals.Add(crystal);
            }
        }

        void RefreshStageButtons()
        {
            if (diamondPlacementMarker != null) diamondPlacementMarker.SetActive(Stage == InferencePuzzleStage.FindingDiamond);
            if (diamondPlacementMarker != null && diamondPlacementMarker.GetComponent<TMP_Text>() != null)
                diamondPlacementMarker.GetComponent<TMP_Text>().text = "PLACE RULE CRYSTAL HERE";
            placeButton.gameObject.SetActive(Stage == InferencePuzzleStage.FindingDiamond);
            placeButton.GetComponentInChildren<TMP_Text>().text = "Place Rule Crystal";
            validateButton.gameObject.SetActive(Stage == InferencePuzzleStage.CompletingPremises);
            validateButton.GetComponentInChildren<TMP_Text>().text = "Check argument";
        }

        void SpawnCrystals()
        {
            var answers = new List<string> { Question.Conclusion };
            foreach (string candidate in Question.Form.distractors)
            {
                string answer = Question.Resolve(candidate);
                if (!answers.Contains(answer)) answers.Add(answer);
            }
            // Shuffle answers independently of rule selection, so the correct crystal has no fixed location.
            var random = new System.Random(Guid.NewGuid().GetHashCode());
            for (int i = answers.Count - 1; i > 0; i--)
            { int j = random.Next(i + 1); string swap = answers[i]; answers[i] = answers[j]; answers[j] = swap; }
            if (answers.Count > crystalSpawns.Length) throw new InvalidOperationException("Not enough crystal spawn points.");
            for (int i = 0; i < answers.Count; i++)
            {
                var crystal = Instantiate(crystalPrefab, crystalSpawns[i].position, Quaternion.identity, transform);
                crystal.name = "ConclusionCrystal_" + (i + 1);
                ConfigureCrystalPickup(crystal);
                crystal.Configure(Question.Id, answers[i], transform);
                crystals.Add(crystal);
            }
        }

        void ConfigureCrystalPickup(InferenceCrystal crystal)
        {
            // The art is displayed on top of tall pillars, while the player stands at their base.
            // The existing Player/GrabbableObject overlap pickup needs a reachable collider on
            // the same GameObject. Keep the art in place and extend its pickup volume downward.
            crystal.ConfigureReachablePickup(crystalPickupDrop, crystalPickupRadius);
        }

        void ClearCrystals()
        {
            var local = player != null ? player : Player.LocalInstance;
            foreach (var crystal in crystals)
            {
                if (crystal == null) continue;
                if (local != null && local.GetHeldObject() == crystal.GetComponent<GrabbableObject>()) local.SetHeldObjectSilently(null);
                crystal.gameObject.SetActive(false);
                Destroy(crystal.gameObject);
            }
            crystals.Clear(); placed = null;
        }

        public void PlaceHeldCrystal()
        {
            if (!HasStarted || Question == null || !IsBoardOpen || roundSolved || player == null) return;
            if (dragPuzzle != null && Stage == InferencePuzzleStage.CompletingPremises) return;
            var held = player.GetHeldObject();
            var crystal = held == null ? null : held.GetComponent<InferenceCrystal>();
            if (crystal == null) { Feedback("Bring a crystal from the island, then choose Place crystal.", false); return; }
            if (crystal.QuestionId != Question.Id) { Feedback("This crystal belongs to another challenge.", false); return; }
            if (dragPuzzle != null && crystal.GetComponent<RuleDiamond>() == null) { Feedback("Bring a Rule Crystal to the altar.", false); return; }
            if (placed != null) placed.ReturnToOrigin();
            held.Drop(); player.SetHeldObjectSilently(null);
            crystal.Place(conclusionSocket); placed = crystal;
            if (dragPuzzle != null)
            {
                var diamond = crystal.GetComponent<RuleDiamond>();
                diamond.ShowLabel();
                if (diamond.RuleType != dragPuzzle.Puzzle.RuleType)
                {
                    if (pillarCrystalPrefab != null)
                    {
                        RemoveWrongPillarCrystal();
                        return;
                    }
                    placed.ReturnToOrigin(); placed = null;
                    Stage = InferencePuzzleStage.FindingDiamond; RefreshStageButtons();
                    WorldStageAudio.PlayAnswerFeedback(false);
                    Feedback("Incorrect rule. Collect another Rule Crystal that fits the premises.", false);
                    return;
                }
                // Correct diamond placed -> instantly solve and reveal conclusion!
                dragPuzzle.Solve();
                Stage = InferencePuzzleStage.Solved;
                RefreshStageButtons();
                CompleteRound();
                Feedback("Correct! Conclusion revealed.", true);
                return;
            }
            conclusionLabel.text = "Therefore, " + crystal.Answer;
            Feedback("Crystal placed. Check the whole argument when you are ready.", true);
        }

        public void ValidateBoard()
        {
            if (!HasStarted || Question == null || !IsBoardOpen || roundSolved) return;
            if (dragPuzzle != null)
            {
                if (Stage == InferencePuzzleStage.CompletingPremises)
                {
                    if (!dragPuzzle.CheckPremises())
                    {
                        WorldStageAudio.PlayAnswerFeedback(false);
                        Feedback("Some blanks are incorrect.", false);
                        return;
                    }
                    WorldStageAudio.PlayAnswerFeedback(true);
                    dragPuzzle.LockPremises(); wordBank.gameObject.SetActive(false);
                    SpawnRuleDiamonds(); Stage = InferencePuzzleStage.FindingDiamond; RefreshStageButtons();
                    Feedback("Premises complete. Collect the Rule Crystal whose label fits this reasoning pattern, then return to the altar.", true);
                    return;
                }
                return;
            }
            int invalid = 0;
            for (int i = 0; i < fields.Count; i++)
            {
                bool correct = Question.Accepts(tokens[i], fields[i].text);
                fields[i].GetComponent<UnityEngine.UI.Image>().color = correct ? new Color(0.12f, 0.3f, 0.28f) : new Color(0.42f, 0.17f, 0.19f);
                if (!correct) invalid++;
            }
            if (invalid > 0)
            {
                WorldStageAudio.PlayAnswerFeedback(false);
                Feedback("Check " + invalid + " highlighted blank(s). Use the word bank and proposition key.", false);
                return;
            }
            if (placed == null)
            {
                WorldStageAudio.PlayAnswerFeedback(false);
                Feedback("The premises are correct. Explore and bring back a conclusion crystal.", false);
                return;
            }
            if (placed.QuestionId != Question.Id || placed.Answer != Question.Conclusion)
            {
                WorldStageAudio.PlayAnswerFeedback(false);
                placed.ReturnToOrigin(); placed = null;
                conclusionLabel.text = "Therefore, __________________________";
                Feedback("That crystal does not match this rule's conclusion. It has returned to its starting point.", false); return;
            }
            CompleteRound();
        }

        void RemoveWrongPillarCrystal()
        {
            var rejected = placed;
            placed = null;
            crystals.Remove(rejected);
            rejected.gameObject.SetActive(false);
            Destroy(rejected.gameObject);
            Stage = InferencePuzzleStage.FindingDiamond;
            RefreshStageButtons();
            WorldStageAudio.PlayAnswerFeedback(false);
            Feedback("Incorrect rule. The crystal is gone. Find another crystal at the pillars.", false);
        }

        void CompleteRound()
        {
            WorldStageAudio.PlayAnswerFeedback(true);
            roundSolved = true; SolvedRounds++;
            foreach (var field in fields) field.readOnly = true;
            placeButton.interactable = validateButton.interactable = false;
            nextButton.gameObject.SetActive(IsBoardOpen);
            nextButton.GetComponentInChildren<TMP_Text>().text = "Continue";
            bool newlyCompleted = !PuzzleCompleted && SolvedRounds >= requiredRounds;
            if (newlyCompleted)
            {
                PuzzleCompleted = true;
                StageCompleteManager.UnlockStage(4);
                if (completionMarker != null) completionMarker.SetActive(!IsBoardOpen);
                onChallengeCompleted.Invoke();
                Feedback("All challenges completed!", true);
                return;
            }
            // Leave the solved board and wait for a fresh book interaction. The next
            // question is drawn only when the player opens the board after that start.
            HasStarted = false;
            // Retain the solved argument, placed crystal and revealed conclusion until
            // Back/Continue. Only the next real book interaction prepares another round.
            if (!IsBoardOpen) { ClearCrystals(); Question = null; roundSolved = false; }
            onRoundCompleted?.Invoke(SolvedRounds);
        }

        void Feedback(string message, bool good)
        {
            feedback.text = message;
            feedback.color = good ? new Color(0.55f, 1f, 0.78f) : new Color(1f, 0.72f, 0.57f);
        }

        void BuildPremises()
        {
            fields.Clear(); tokens.Clear();
            foreach (Transform child in premiseContainer) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            int number = 0;
            foreach (var premise in Question.Form.premises)
            {
                var row = MakeRow("Premise " + (++number));
                foreach (string token in premise.tokens)
                {
                    if (token == "\n") { row = MakeRow("continued"); continue; }
                    if (InferenceQuestion.IsBlank(token))
                    {
                        var input = InferenceBoardUI.Input(row, token.StartsWith("{") ? "proposition " + token.Trim('{', '}') : "word", font, token.StartsWith("{") ? 310 : 120);
                        fields.Add(input); tokens.Add(token);
                    }
                    else InferenceBoardUI.Text(row, token, font, 28, 95);
                }
            }
        }

        Transform MakeRow(string title)
        {
            var row = InferenceBoardUI.Rect(title, premiseContainer);
            var group = row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            group.spacing = 12; group.childAlignment = TextAnchor.MiddleLeft;
            group.childControlHeight = group.childControlWidth = true;
            group.childForceExpandWidth = false; group.childForceExpandHeight = true;
            var size = row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>(); size.preferredHeight = 70;
            InferenceBoardUI.Text(row, title == "continued" ? "" : title, font, 22, 135);
            return row;
        }
    }
}
