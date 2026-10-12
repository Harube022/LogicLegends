using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum FunctionsRelationsState
{
    WAITING_FOR_ACTIVATION,
    ACTIVE,
    VALIDATING,
    CHALLENGE_COMPLETE,
    STAGE_COMPLETE,
    GAME_OVER
}

[DisallowMultipleComponent]
public sealed class FunctionsRelationsStageManager : MonoBehaviour
{
    [Header("Gameplay")]
    [SerializeField] private MappingChallengeCatalog catalog;
    [SerializeField] private LaserMappingController mapping;
    [SerializeField] private Transform bookStatue;
    [SerializeField] private Collider bookInteractionCollider;
    [SerializeField, Min(1f)] private float bookUseDistance = 5f;
    [SerializeField, Min(1f)] private float startingSeconds = 600f;
    [SerializeField, Min(0f)] private float wrongAnswerPenalty = 10f;
    [SerializeField, Min(0f)] private float wrongFlashSeconds = .65f;
    [SerializeField, Min(0f)] private float challengeCompleteSeconds = 1.5f;
    [SerializeField, Min(1)] private int stageNumber = 5;

    [Header("Board UI")]
    [SerializeField] private CanvasGroup board;
    [SerializeField] private CanvasGroup gameplayControls;
    [SerializeField] private Image boardBackground;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text hudTimerText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text interactionPromptText;
    [SerializeField] private TMP_Text worldProblemText;
    [SerializeField] private Transform[] classificationPedestals = new Transform[0];
    [SerializeField] private Material[] pedestalOrbMaterials = new Material[3];
    [SerializeField] private Material[] pedestalIconMaterials = new Material[3];
    [SerializeField] private Material neutralPedestalOrbMaterial;
    [SerializeField] private Material neutralPedestalIconMaterial;
    [SerializeField, Min(1f)] private float pedestalUseDistance = 3.5f;
    [SerializeField] private RectTransform domainChoicesPanel;
    [SerializeField] private RectTransform rangeChoicesPanel;
    [SerializeField] private Button[] domainChoiceButtons = new Button[4];
    [SerializeField] private Button[] rangeChoiceButtons = new Button[4];
    [SerializeField] private Button undoConnectionButton;
    [SerializeField, Min(1f)] private float diamondUseDistance = 5f;
    [SerializeField] private TMP_Text rangeText;
    [SerializeField] private TMP_Text resultsText;
    [SerializeField] private GameObject resultsPanel;
    [SerializeField] private GameObject classificationChoices;
    [SerializeField] private GameObject propertyChoices;
    [SerializeField] private Button submitButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button openButton;
    [SerializeField] private Button functionButton;
    [SerializeField] private Button notFunctionButton;
    [SerializeField] private Button[] propertyButtons = new Button[4];
    [SerializeField] private Button retryButton;
    [SerializeField] private Button menuButton;

    [Header("Feedback")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip correctSound;
    [SerializeField] private AudioClip incorrectSound;
    [SerializeField] private UnityEvent onStageComplete;
    [SerializeField] private UnityEvent onGameOver;

    private static readonly MappingProperty[] PropertyOptions =
        { MappingProperty.Injective, MappingProperty.Surjective, MappingProperty.Bijective, MappingProperty.ManyToOne };
    private static readonly Color NormalBoard = new Color(.055f, .09f, .15f, 1f);
    private const string LockedBoardMessage = "FUNCTIONS & RELATIONS\nActivate the Book Statue to reveal the challenge.";

    private GameInput input;
    private Player player;
    private FunctionsRelationsState state = FunctionsRelationsState.WAITING_FOR_ACTIVATION;
    private int challengeIndex;
    private readonly int[] selectedVariantIndices = new int[5];
    private int correctCount;
    private int mistakeCount;
    private float remaining;
    private float activePlaySeconds;
    private bool boardOpen;
    private int? classificationAnswer;
    private MappingProperty chosenProperties;
    private bool domainChoicesOpen;
    private bool rangeChoicesOpen;
    private bool timerStarted;
    private readonly Dictionary<Renderer, MaterialPropertyBlock[]> bookPropertyBlocks =
        new Dictionary<Renderer, MaterialPropertyBlock[]>();

    public FunctionsRelationsState State => state;
    public int ChallengeIndex => challengeIndex;
    public int CurrentVariantIndex => challengeIndex < selectedVariantIndices.Length ? selectedVariantIndices[challengeIndex] : -1;
    public int CorrectCount => correctCount;
    public int MistakeCount => mistakeCount;
    public float TimeRemaining => remaining;
    public float ActivePlaySeconds => activePlaySeconds;
    public MappingProperty ChosenProperties => chosenProperties;
    public MappingChallengeData CurrentChallenge => catalog != null ? catalog.GetVariant(challengeIndex, CurrentVariantIndex) : null;

    private void Awake()
    {
        if (catalog == null) catalog = Resources.Load<MappingChallengeCatalog>("FunctionsRelationsChallenges");
        if (mapping == null) mapping = GetComponentInChildren<LaserMappingController>(true);
        if (bookStatue == null)
        {
            var book = GameObject.Find("Book_Statue");
            if (book != null) bookStatue = book.transform;
        }
        remaining = startingSeconds;
        if (boardBackground != null) boardBackground.color = NormalBoard;
    }

    private void OnEnable()
    {
        BindInput();
        if (mapping != null) mapping.Feedback += OnMappingFeedback;
    }

    private void OnDisable()
    {
        FlashBookStatue(false);
        if (input != null) input.OnInteractAction -= OnInteract;
        if (mapping != null) mapping.Feedback -= OnMappingFeedback;
        if (input != null) input.SetGameplayInputBlocked(false);
        if (player != null && boardOpen) player.ToggleControl(true);
        if (gameplayControls != null)
        {
            gameplayControls.alpha = 1f;
            gameplayControls.interactable = true;
            gameplayControls.blocksRaycasts = true;
        }
    }

    private void Start()
    {
        BindInput();
        player = Player.LocalInstance != null ? Player.LocalInstance : FindFirstObjectByType<Player>();
        if (catalog == null || catalog.ChallengeCount != 5 || mapping == null || board == null || bookStatue == null ||
            mapping.DomainDiamonds.Length < 4 || mapping.RangeDiamonds.Length < 4 ||
            domainChoiceButtons.Length < 4 || rangeChoiceButtons.Length < 4)
        {
            Debug.LogError("Functions and Relations needs five challenge groups, four diamond choices on each side, and its Book Statue references.", this);
            enabled = false;
            return;
        }
        for (int i = 0; i < catalog.ChallengeCount; i++)
        {
            var group = catalog.ChallengeGroups[i];
            if (group == null || group.variants == null || group.variants.Count != 10)
            {
                Debug.LogError("Functions and Relations challenge " + (i + 1) + " needs ten variants.", this);
                enabled = false;
                return;
            }
            string key = "FunctionsRelations.LastVariant." + i;
            int previous = PlayerPrefs.GetInt(key, -1);
            int chosen = UnityEngine.Random.Range(0, group.variants.Count - (previous >= 0 && previous < group.variants.Count ? 1 : 0));
            if (previous >= 0 && previous < group.variants.Count && chosen >= previous) chosen++;
            selectedVariantIndices[i] = chosen;
            PlayerPrefs.SetInt(key, chosen);
        }
        PlayerPrefs.Save();
        if (submitButton != null) submitButton.onClick.AddListener(Submit);
        if (closeButton != null) closeButton.onClick.AddListener(CloseBoard);
        if (openButton != null) openButton.onClick.AddListener(OpenBoard);
        for (int i = 0; i < propertyButtons.Length && i < PropertyOptions.Length; i++)
        {
            int option = i;
            if (propertyButtons[i] != null) propertyButtons[i].onClick.AddListener(() => ToggleProperty(option));
        }
        if (retryButton != null) retryButton.onClick.AddListener(RetryStage);
        if (menuButton != null) menuButton.onClick.AddListener(ReturnToMenu);
        for (int i = 0; i < domainChoiceButtons.Length; i++)
        {
            int index = i;
            if (domainChoiceButtons[i] != null) domainChoiceButtons[i].onClick.AddListener(() => ChooseDomain(index));
        }
        for (int i = 0; i < rangeChoiceButtons.Length; i++)
        {
            int index = i;
            if (rangeChoiceButtons[i] != null) rangeChoiceButtons[i].onClick.AddListener(() => ChooseRange(index));
        }
        if (undoConnectionButton != null) undoConnectionButton.onClick.AddListener(UndoRangeConnection);
        SetBoardOpen(false);
        RefreshUI();
    }

    private void BindInput()
    {
        GameInput current = GameInput.Instance != null ? GameInput.Instance : FindFirstObjectByType<GameInput>();
        if (current == input) return;
        if (input != null) input.OnInteractAction -= OnInteract;
        input = current;
        if (input != null && isActiveAndEnabled) input.OnInteractAction += OnInteract;
    }

    private void Update()
    {
        if (input == null) BindInput();
        if (player == null) player = Player.LocalInstance != null ? Player.LocalInstance : FindFirstObjectByType<Player>();
        if (timerStarted && state == FunctionsRelationsState.ACTIVE)
        {
            float elapsed = Time.deltaTime;
            activePlaySeconds += elapsed;
            remaining = Mathf.Max(0f, remaining - elapsed);
            if (remaining <= 0f) { EnterGameOver(); return; }
        }
        RefreshTimer();
        RefreshInteractionPrompt();
        RefreshDiamondPanels();
        if (statusText != null && state == FunctionsRelationsState.WAITING_FOR_ACTIVATION)
            statusText.text = "Challenge " + (challengeIndex + 1) + "/5  •  Approach the Book Statue and press Interact";
    }

    private void OnInteract(object sender, EventArgs args)
    {
        if (state != FunctionsRelationsState.WAITING_FOR_ACTIVATION && state != FunctionsRelationsState.ACTIVE) return;
        if (player == null || boardOpen) return;
        float bookDistance = BookDistance();
        if (state == FunctionsRelationsState.ACTIVE && UsesAnswerPedestals(CurrentChallenge))
        {
            int pedestal = NearestPedestalIndex();
            if (pedestal >= 0 && (bookDistance > bookUseDistance || PedestalDistance(pedestal) < bookDistance))
            {
                ChooseClassificationPedestal(pedestal);
                return;
            }
        }
        if (bookDistance <= bookUseDistance)
        {
            if (state == FunctionsRelationsState.WAITING_FOR_ACTIVATION) ActivateChallenge();
            else Submit();
        }
        else if (state == FunctionsRelationsState.ACTIVE && CurrentChallenge.questionKind == MappingQuestionKind.BuildExactRelation)
        {
            if (DiamondDistance(mapping.DomainDiamond) <= diamondUseDistance)
            {
                int aimed = AimedDiamondIndex(mapping.DomainDiamonds);
                if (aimed >= 0 && aimed < CurrentChallenge.domain.Length)
                {
                    mapping.SelectDomain(CurrentChallenge.domain[aimed]);
                    domainChoicesOpen = false;
                }
                else domainChoicesOpen = !domainChoicesOpen;
                rangeChoicesOpen = false;
            }
            else if (DiamondDistance(mapping.RangeDiamond) <= diamondUseDistance)
            {
                int aimed = AimedDiamondIndex(mapping.RangeDiamonds);
                if (aimed >= 0 && aimed < CurrentChallenge.codomain.Length && mapping.SelectedDomain != null)
                {
                    mapping.SelectCodomain(CurrentChallenge.codomain[aimed]);
                    rangeChoicesOpen = false;
                }
                else rangeChoicesOpen = !rangeChoicesOpen;
                domainChoicesOpen = false;
            }
            RefreshDiamondPanels();
        }
    }

    private static int AimedDiamondIndex(Transform[] diamonds)
    {
        if (Camera.main == null || diamonds == null) return -1;
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(.5f, .5f, 0f));
        var hits = Physics.RaycastAll(ray, 30f, ~0, QueryTriggerInteraction.Collide);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
            for (int i = 0; i < diamonds.Length; i++)
                if (diamonds[i] != null && hit.collider.transform == diamonds[i]) return i;
        return -1;
    }

    private float BookDistance()
    {
        if (player == null || bookStatue == null) return float.MaxValue;
        if (bookInteractionCollider != null)
            return Vector3.Distance(player.transform.position, bookInteractionCollider.bounds.center);
        return Vector3.Distance(player.transform.position, bookStatue.position);
    }

    private float DiamondDistance(Transform diamond)
    {
        if (player == null || diamond == null) return float.MaxValue;
        var renderer = diamond.GetComponent<Renderer>();
        Vector3 center = renderer != null ? renderer.bounds.center : diamond.position;
        return Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z),
            new Vector2(center.x, center.z));
    }

    private int NearestPedestalIndex()
    {
        if (player == null) return -1;
        int nearest = -1;
        float distance = pedestalUseDistance;
        for (int i = 0; i < classificationPedestals.Length; i++)
        {
            var pedestal = classificationPedestals[i];
            if (pedestal == null || !pedestal.gameObject.activeInHierarchy) continue;
            float candidate = PedestalDistance(i);
            if (candidate >= distance) continue;
            distance = candidate;
            nearest = i;
        }
        return nearest;
    }

    private float PedestalDistance(int index)
    {
        if (player == null || index < 0 || index >= classificationPedestals.Length ||
            classificationPedestals[index] == null) return float.MaxValue;
        var pedestal = classificationPedestals[index];
        var renderer = pedestal.GetComponent<Renderer>();
        Vector3 center = renderer != null ? renderer.bounds.center : pedestal.position;
        return Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z),
            new Vector2(center.x, center.z));
    }

    public void ChooseClassificationPedestal(int index)
    {
        if (state != FunctionsRelationsState.ACTIVE || CurrentChallenge == null ||
            !UsesAnswerPedestals(CurrentChallenge) ||
            index < 0 || index >= classificationPedestals.Length || classificationPedestals[index] == null) return;
        if (CurrentChallenge.questionKind == MappingQuestionKind.SelectProperties)
            chosenProperties ^= PropertyOptions[index];
        else
            classificationAnswer = index;
        TintClassificationPedestals(LaserVisualState.Blue);
        SetFeedback(CurrentChallenge.questionKind == MappingQuestionKind.SelectProperties ?
            "Toggle every applicable property, then confirm at the Book Statue." :
            "Choice selected. Return to the Book Statue to confirm.");
        if (statusText != null) statusText.text = PedestalName(index, CurrentChallenge.questionKind) +
            (CurrentChallenge.questionKind == MappingQuestionKind.SelectProperties &&
             (chosenProperties & PropertyOptions[index]) == 0 ? " deselected." : " selected.") +
            " Confirm at the Book Statue when ready.";
    }

    private static bool UsesAnswerPedestals(MappingChallengeData challenge)
    {
        return challenge != null && (challenge.questionKind == MappingQuestionKind.ClassifyFunction ||
            challenge.questionKind == MappingQuestionKind.SelectProperties);
    }

    private static string PedestalName(int index, MappingQuestionKind kind)
    {
        if (kind == MappingQuestionKind.SelectProperties)
            return index == 0 ? "Injective" : index == 1 ? "Surjective" : "Bijective";
        return index == 0 ? "Function Only" : index == 1 ? "Not a Function" : "Relation Only";
    }

    private void TintClassificationPedestals(LaserVisualState stateForSelected)
    {
        for (int i = 0; i < classificationPedestals.Length; i++)
        {
            var pedestal = classificationPedestals[i];
            if (pedestal == null) continue;
            var ui = pedestal.Find("FR_AnswerUI");
            if (ui == null) continue;
            var emblem = ui.Find("Emblem");
            var label = ui.Find("Label");
            bool properties = CurrentChallenge != null && CurrentChallenge.questionKind == MappingQuestionKind.SelectProperties;
            bool selected = properties ? (chosenProperties & PropertyOptions[i]) != 0 : classificationAnswer == i;
            Color color = !selected ? new Color(1f, .96f, .84f) :
                stateForSelected == LaserVisualState.Green ? new Color(.35f, 1f, .5f) :
                stateForSelected == LaserVisualState.Red ? new Color(1f, .32f, .36f) :
                new Color(.25f, .85f, 1f);
            if (emblem != null) emblem.GetComponent<TMP_Text>().color = properties ?
                new Color(.25f, .85f, 1f) : i == 0 ?
                new Color(.16f, 1f, .48f) : i == 1 ?
                new Color(1f, .21f, .28f) : new Color(1f, .62f, .12f);
            if (label != null)
            {
                label.GetComponent<TMP_Text>().text = properties ?
                    PedestalName(i, CurrentChallenge.questionKind).ToUpperInvariant() +
                    (selected ? "\nSELECTED" : "\nTOGGLE") :
                    (i == 0 ? "FUNCTION\nONLY" : i == 1 ? "NOT A\nFUNCTION" : "RELATION\nONLY");
                label.GetComponent<TMP_Text>().color = color;
            }
        }
    }

    private void RefreshClassificationPedestals()
    {
        bool visible = UsesAnswerPedestals(CurrentChallenge) &&
            (state == FunctionsRelationsState.ACTIVE || state == FunctionsRelationsState.VALIDATING ||
             state == FunctionsRelationsState.CHALLENGE_COMPLETE);
        for (int i = 0; i < classificationPedestals.Length; i++)
        {
            var pedestal = classificationPedestals[i];
            if (pedestal == null) continue;
            pedestal.gameObject.SetActive(visible);
            if (!visible) continue;
            bool properties = CurrentChallenge.questionKind == MappingQuestionKind.SelectProperties;
            var ui = pedestal.Find("FR_AnswerUI");
            if (ui == null) continue;
            var orb = ui.Find("EmblemOrb");
            var ring = ui.Find("EmblemRing");
            var emblem = ui.Find("Emblem");
            if (orb != null && i < pedestalOrbMaterials.Length)
                orb.GetComponent<Renderer>().sharedMaterial = properties ? neutralPedestalOrbMaterial : pedestalOrbMaterials[i];
            if (ring != null && i < pedestalIconMaterials.Length)
                ring.GetComponent<LineRenderer>().sharedMaterial = properties ? neutralPedestalIconMaterial : pedestalIconMaterials[i];
            if (emblem != null) emblem.GetComponent<TMP_Text>().text = properties ?
                (i == 0 ? "I" : i == 1 ? "S" : "B") : (i == 2 ? "?" : "");
            foreach (string mark in new[] { "CheckMark", "CrossOne", "CrossTwo" })
            {
                var symbol = ui.Find(mark);
                if (symbol != null) symbol.gameObject.SetActive(!properties);
            }
        }
        if (visible) TintClassificationPedestals(mapping.VisualState);
    }

    private void RefreshInteractionPrompt()
    {
        if (interactionPromptText == null) return;
        string prompt = null;
        if (!boardOpen && player != null && (state == FunctionsRelationsState.ACTIVE || state == FunctionsRelationsState.WAITING_FOR_ACTIVATION))
        {
            float bookDistance = BookDistance();
            int pedestal = state == FunctionsRelationsState.ACTIVE && UsesAnswerPedestals(CurrentChallenge) ?
                NearestPedestalIndex() : -1;
            if (pedestal >= 0 && (bookDistance > bookUseDistance || PedestalDistance(pedestal) < bookDistance))
                prompt = "INTERACT  •  " + (CurrentChallenge.questionKind == MappingQuestionKind.SelectProperties ? "Toggle " : "Select ") +
                    PedestalName(pedestal, CurrentChallenge.questionKind) + " at this pedestal";
            else if (bookDistance <= bookUseDistance)
                prompt = state == FunctionsRelationsState.WAITING_FOR_ACTIVATION ?
                    "INTERACT  •  Activate Book Statue and start timer" :
                    "INTERACT  •  Confirm mapping at Book Statue";
            else if (state == FunctionsRelationsState.ACTIVE && CurrentChallenge.questionKind == MappingQuestionKind.BuildExactRelation)
            {
                if (DiamondDistance(mapping.DomainDiamond) <= diamondUseDistance)
                    prompt = "INTERACT  •  Choose an input at the DOMAIN diamond";
                else if (DiamondDistance(mapping.RangeDiamond) <= diamondUseDistance)
                    prompt = "INTERACT  •  Choose an output or undo at the RANGE diamond";
            }
        }
        interactionPromptText.transform.parent.gameObject.SetActive(prompt != null);
        if (prompt != null) interactionPromptText.text = prompt;
    }

    private void RefreshDiamondPanels()
    {
        bool editable = state == FunctionsRelationsState.ACTIVE && !boardOpen &&
            CurrentChallenge != null && CurrentChallenge.questionKind == MappingQuestionKind.BuildExactRelation;
        if (!editable || DiamondDistance(mapping.DomainDiamond) > diamondUseDistance) domainChoicesOpen = false;
        if (!editable || DiamondDistance(mapping.RangeDiamond) > diamondUseDistance) rangeChoicesOpen = false;
        if (domainChoicesPanel != null)
        {
            domainChoicesPanel.gameObject.SetActive(domainChoicesOpen);
            if (domainChoicesOpen) PositionPanel(domainChoicesPanel, mapping.DomainDiamond);
        }
        if (rangeChoicesPanel != null)
        {
            rangeChoicesPanel.gameObject.SetActive(rangeChoicesOpen);
            if (rangeChoicesOpen) PositionPanel(rangeChoicesPanel, mapping.RangeDiamond);
        }
        if (undoConnectionButton != null)
        {
            var pair = mapping.LastUndoablePair;
            undoConnectionButton.interactable = pair.HasValue;
            var text = undoConnectionButton.GetComponentInChildren<TMP_Text>();
            if (text != null) text.text = pair.HasValue ? "UNDO " + pair.Value : "NO CONNECTION TO UNDO";
        }
    }

    private static void PositionPanel(RectTransform panel, Transform diamond)
    {
        if (panel == null || diamond == null || Camera.main == null) return;
        var renderer = diamond.GetComponent<Renderer>();
        Vector3 anchor = renderer != null ? renderer.bounds.center + Vector3.up * (renderer.bounds.extents.y + 1.1f) :
            diamond.position + Vector3.up * 3f;
        Vector3 screen = Camera.main.WorldToScreenPoint(anchor);
        if (screen.z <= 0f) return;
        var parent = panel.parent as RectTransform;
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out local)) return;
        float x = Mathf.Clamp(local.x, parent.rect.xMin + panel.rect.width * .5f, parent.rect.xMax - panel.rect.width * .5f);
        float y = Mathf.Clamp(local.y, parent.rect.yMin + panel.rect.height * .5f, parent.rect.yMax - panel.rect.height * .5f);
        panel.anchoredPosition = new Vector2(x, y);
    }

    public void ChooseDomain(int index)
    {
        var challenge = CurrentChallenge;
        if (state != FunctionsRelationsState.ACTIVE || boardOpen || !domainChoicesOpen ||
            DiamondDistance(mapping.DomainDiamond) > diamondUseDistance ||
            challenge == null || index < 0 || index >= challenge.domain.Length) return;
        if (mapping.SelectDomain(challenge.domain[index]))
        {
            domainChoicesOpen = false;
            RefreshDiamondPanels();
        }
    }

    public void ChooseRange(int index)
    {
        var challenge = CurrentChallenge;
        if (state != FunctionsRelationsState.ACTIVE || boardOpen || !rangeChoicesOpen ||
            DiamondDistance(mapping.RangeDiamond) > diamondUseDistance ||
            challenge == null || index < 0 || index >= challenge.codomain.Length) return;
        mapping.SelectCodomain(challenge.codomain[index]);
        RefreshRange();
        RefreshDiamondPanels();
    }

    public void UndoRangeConnection()
    {
        if (state != FunctionsRelationsState.ACTIVE || boardOpen || !rangeChoicesOpen ||
            DiamondDistance(mapping.RangeDiamond) > diamondUseDistance) return;
        mapping.UndoLastConnection();
        RefreshRange();
        RefreshDiamondPanels();
    }

    public void ActivateChallenge()
    {
        if (state != FunctionsRelationsState.WAITING_FOR_ACTIVATION || CurrentChallenge == null) return;
        classificationAnswer = null;
        chosenProperties = MappingProperty.None;
        mapping.LoadChallenge(CurrentChallenge);
        state = FunctionsRelationsState.ACTIVE;
        timerStarted = true;
        SetBoardOpen(false);
        SetFeedback(CurrentChallenge.questionKind == MappingQuestionKind.ClassifyFunction ?
            "Choose one of the three answer pedestals, then return to the Book to confirm." :
            CurrentChallenge.questionKind == MappingQuestionKind.SelectProperties ?
            "Inspect the preset lasers. Toggle every applicable property at the three pedestals, then confirm at the Book." :
            "Timer started. Choose an input at Domain, then an output at Range. Return to the Book to confirm.");
        RefreshUI();
    }

    public void Submit()
    {
        if (state != FunctionsRelationsState.ACTIVE) return;
        state = FunctionsRelationsState.VALIDATING;
        mapping.SetInteractive(false);
        var challenge = CurrentChallenge;
        var pairs = mapping.Snapshot();
        bool valid = MappingRuleEvaluator.HasValidEndpoints(pairs, challenge.domain, challenge.codomain);
        if (challenge.questionKind == MappingQuestionKind.BuildExactRelation)
            valid &= MappingRuleEvaluator.ExactPairs(pairs, challenge.requiredPairs) && MappingRuleEvaluator.MeetsRules(pairs, challenge);
        else if (challenge.questionKind == MappingQuestionKind.ClassifyFunction)
        {
            int actual = MappingRuleEvaluator.IsFunction(pairs, challenge.domain) ? 0 : 2;
            valid &= classificationAnswer.HasValue && classificationAnswer.Value == actual &&
                classificationAnswer.Value == challenge.correctClassificationIndex;
        }
        else
        {
            MappingProperty actual = MappingRuleEvaluator.Properties(pairs, challenge.domain, challenge.codomain);
            MappingProperty visible = MappingProperty.Injective | MappingProperty.Surjective | MappingProperty.Bijective | MappingProperty.ManyToOne;
            valid &= chosenProperties == (actual & visible) && chosenProperties == challenge.correctProperties;
        }
        if (valid) StartCoroutine(CorrectSubmission());
        else StartCoroutine(WrongSubmission());
    }

    private IEnumerator WrongSubmission()
    {
        mistakeCount++;
        remaining = Mathf.Max(0f, remaining - wrongAnswerPenalty);
        if (audioSource != null && incorrectSound != null) audioSource.PlayOneShot(incorrectSound);
        mapping.SetVisualState(LaserVisualState.Red);
        FlashBookStatue(true);
        TintClassificationPedestals(LaserVisualState.Red);
        SetFeedback("Incorrect submission. −" + wrongAnswerPenalty.ToString("0") + " seconds. Try again.");
        if (statusText != null) statusText.text = CurrentChallenge.questionKind == MappingQuestionKind.BuildExactRelation ?
            "Incorrect. −10 seconds. Lasers can be changed at Range." :
            "Incorrect. −10 seconds. Change your pedestal choice and try again.";
        RefreshTimer();
        yield return new WaitForSeconds(wrongFlashSeconds);
        FlashBookStatue(false);
        mapping.SetVisualState(LaserVisualState.Blue);
        TintClassificationPedestals(LaserVisualState.Blue);
        if (remaining <= 0f) { EnterGameOver(); yield break; }
        state = FunctionsRelationsState.ACTIVE;
        mapping.SetInteractive(CurrentChallenge.questionKind == MappingQuestionKind.BuildExactRelation);
        if (player != null && !boardOpen) player.ToggleControl(true);
        RefreshUI();
    }

    private IEnumerator CorrectSubmission()
    {
        correctCount++;
        state = FunctionsRelationsState.CHALLENGE_COMPLETE;
        if (audioSource != null && correctSound != null) audioSource.PlayOneShot(correctSound);
        mapping.SetVisualState(LaserVisualState.Green);
        TintClassificationPedestals(LaserVisualState.Green);
        if (worldProblemText != null) worldProblemText.color = new Color(.45f, 1f, .55f);
        SetFeedback("Correct! Challenge " + (challengeIndex + 1) + " complete.");
        if (statusText != null) statusText.text = "Correct! Challenge " + (challengeIndex + 1) + " complete.";
        RefreshUI();
        yield return new WaitForSeconds(challengeCompleteSeconds);
        if (challengeIndex + 1 >= catalog.ChallengeCount)
        {
            EnterStageComplete();
            yield break;
        }
        challengeIndex++;
        mapping.Clear();
        if (worldProblemText != null) worldProblemText.color = new Color(.92f, .97f, 1f);
        classificationAnswer = null;
        state = FunctionsRelationsState.WAITING_FOR_ACTIVATION;
        SetBoardOpen(false);
        SetFeedback("Challenge complete. Return to the Book Statue to start the next one.");
        RefreshUI();
    }

    private void FlashBookStatue(bool red)
    {
        if (bookStatue == null) return;
        if (red)
        {
            bookPropertyBlocks.Clear();
            foreach (var renderer in bookStatue.GetComponentsInChildren<Renderer>(true))
            {
                var saved = new MaterialPropertyBlock[renderer.sharedMaterials.Length];
                for (int i = 0; i < saved.Length; i++)
                {
                    saved[i] = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(saved[i], i);
                    var flash = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(flash, i);
                    flash.SetColor("_BaseColor", new Color(2.3f, .07f, .1f, 1f));
                    flash.SetColor("_EmissionColor", new Color(2.3f, .04f, .07f, 1f));
                    renderer.SetPropertyBlock(flash, i);
                }
                bookPropertyBlocks[renderer] = saved;
            }
        }
        else
        {
            foreach (var entry in bookPropertyBlocks)
                if (entry.Key != null)
                    for (int i = 0; i < entry.Value.Length; i++)
                        entry.Key.SetPropertyBlock(entry.Value[i], i);
            bookPropertyBlocks.Clear();
        }
    }

    private void EnterStageComplete()
    {
        state = FunctionsRelationsState.STAGE_COMPLETE;
        SetBoardOpen(true);
        if (resultsText != null)
        {
            if (resultsPanel != null) resultsPanel.SetActive(true);
            int seconds = Mathf.FloorToInt(activePlaySeconds);
            resultsText.text = string.Format("FUNCTIONS AND RELATIONS COMPLETE\nTime Played  {0:00}:{1:00}\nCorrect  {2}\nMistakes  {3}", seconds / 60, seconds % 60, correctCount, mistakeCount);
        }
        if (submitButton != null) submitButton.gameObject.SetActive(false);
        if (retryButton != null) retryButton.gameObject.SetActive(false);
        if (menuButton != null) menuButton.gameObject.SetActive(true);
        if (classificationChoices != null) classificationChoices.SetActive(false);
        if (propertyChoices != null) propertyChoices.SetActive(false);
        if (statusText != null) statusText.text = "Stage complete";
        StageCompleteManager.UnlockStage(Mathf.Min(stageNumber + 1, StageSelectionState.LastStage));
        onStageComplete?.Invoke();
    }

    private void EnterGameOver()
    {
        if (state == FunctionsRelationsState.GAME_OVER) return;
        state = FunctionsRelationsState.GAME_OVER;
        RefreshClassificationPedestals();
        remaining = 0f;
        mapping.SetInteractive(false);
        SetBoardOpen(true);
        if (resultsText != null)
        {
            if (resultsPanel != null) resultsPanel.SetActive(true);
            resultsText.text = "TIME UP\nChallenges completed  " + correctCount + "/5\nMistakes  " + mistakeCount;
        }
        if (submitButton != null) submitButton.gameObject.SetActive(false);
        if (retryButton != null) retryButton.gameObject.SetActive(true);
        if (menuButton != null) menuButton.gameObject.SetActive(true);
        if (statusText != null) statusText.text = "Time up";
        RefreshTimer();
        onGameOver?.Invoke();
    }

    public void CloseBoard()
    {
        if (state == FunctionsRelationsState.STAGE_COMPLETE || state == FunctionsRelationsState.GAME_OVER) return;
        SetBoardOpen(false);
        if (state == FunctionsRelationsState.ACTIVE) SetFeedback("Press Interact at the Book Statue to submit your answer.");
    }

    public void OpenBoard()
    {
        if (state == FunctionsRelationsState.ACTIVE) SetBoardOpen(true);
    }

    public void RetryStage()
    {
        Time.timeScale = 1f;
        if (input != null) input.SetGameplayInputBlocked(false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ReturnToMenu()
    {
        Time.timeScale = 1f;
        if (input != null) input.SetGameplayInputBlocked(false);
        SceneManager.LoadScene("Main Menu");
    }

    private void SetBoardOpen(bool value)
    {
        boardOpen = value;
        if (value) { domainChoicesOpen = false; rangeChoicesOpen = false; }
        if (board != null)
        {
            board.alpha = value ? 1f : 0f;
            board.interactable = value;
            board.blocksRaycasts = value;
        }
        if (input != null) input.SetGameplayInputBlocked(value);
        if (player != null) player.ToggleControl(!value);
        if (gameplayControls != null)
        {
            gameplayControls.alpha = value ? 0f : 1f;
            gameplayControls.interactable = !value;
            gameplayControls.blocksRaycasts = !value;
        }
        if (openButton != null) openButton.gameObject.SetActive(!value && state == FunctionsRelationsState.ACTIVE);
        if (statusText != null) statusText.gameObject.SetActive(!value);
    }

    private void ToggleProperty(int option)
    {
        if (state != FunctionsRelationsState.ACTIVE || option < 0 || option >= PropertyOptions.Length) return;
        chosenProperties ^= PropertyOptions[option];
        for (int i = 0; i < propertyButtons.Length && i < PropertyOptions.Length; i++)
            TintButton(propertyButtons[i], (chosenProperties & PropertyOptions[i]) != 0);
        SetFeedback("Select every property that applies, then submit.");
    }

    private static void TintButton(Button button, bool selected)
    {
        if (button != null && button.image != null)
            button.image.color = selected ? new Color(.20f, .65f, .55f, 1f) : new Color(.15f, .28f, .40f, 1f);
    }

    private void OnMappingFeedback(string message)
    {
        SetFeedback(message);
        if (statusText != null) statusText.text = message;
        RefreshRange();
    }
    private void SetFeedback(string message) { if (feedbackText != null) feedbackText.text = message; }

    private void RefreshUI()
    {
        var challenge = CurrentChallenge;
        if (challenge == null) return;
        if (titleText != null) titleText.text = "CHALLENGE " + (challengeIndex + 1) + "/5  •  " + challenge.title;
        if (instructionText != null) instructionText.text = challenge.instruction;
        for (int i = 0; i < domainChoiceButtons.Length; i++)
        {
            if (domainChoiceButtons[i] == null) continue;
            bool available = i < challenge.domain.Length;
            domainChoiceButtons[i].gameObject.SetActive(available);
            var label = domainChoiceButtons[i].GetComponentInChildren<TMP_Text>();
            if (available && label != null) label.text = challenge.domain[i];
        }
        for (int i = 0; i < rangeChoiceButtons.Length; i++)
        {
            if (rangeChoiceButtons[i] == null) continue;
            bool available = i < challenge.codomain.Length;
            rangeChoiceButtons[i].gameObject.SetActive(available);
            var label = rangeChoiceButtons[i].GetComponentInChildren<TMP_Text>();
            if (available && label != null) label.text = challenge.codomain[i];
        }
        if (classificationChoices != null) classificationChoices.SetActive(false);
        if (propertyChoices != null) propertyChoices.SetActive(false);
        if (submitButton != null) submitButton.gameObject.SetActive(false);
        if (worldProblemText != null)
            worldProblemText.text = state == FunctionsRelationsState.WAITING_FOR_ACTIVATION ?
                LockedBoardMessage :
                "CHALLENGE " + (challengeIndex + 1) + ": " + challenge.title + "\n" + challenge.instruction;
        if (statusText != null && state == FunctionsRelationsState.ACTIVE)
            statusText.text = UsesAnswerPedestals(challenge) ?
                "Challenge " + (challengeIndex + 1) + "/5  •  " +
                (challenge.questionKind == MappingQuestionKind.SelectProperties ? "Toggle applicable pedestals" : "Choose a pedestal") +
                ", then confirm at the Book Statue" :
                "Challenge " + (challengeIndex + 1) + "/5  •  Build your answer, then submit at the Book Statue";
        RefreshRange();
        RefreshTimer();
        RefreshClassificationPedestals();
    }

    private void RefreshRange()
    {
        if (rangeText != null)
        {
            var used = new System.Collections.Generic.HashSet<string>();
            if (mapping != null) foreach (var pair in mapping.Pairs) used.Add(pair.codomain);
            var ordered = new System.Collections.Generic.List<string>(used);
            ordered.Sort(StringComparer.Ordinal);
            rangeText.text = "Range = { " + string.Join(", ", ordered) + " }";
        }
    }

    private void RefreshTimer()
    {
        int seconds = Mathf.CeilToInt(remaining);
        string value = string.Format("{0:00}:{1:00}", seconds / 60, seconds % 60);
        if (timerText != null) timerText.text = value;
        if (hudTimerText != null) hudTimerText.text = value;
    }
}
