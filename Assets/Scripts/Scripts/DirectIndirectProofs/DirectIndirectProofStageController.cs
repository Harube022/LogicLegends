using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Book-driven five-challenge proof trial with one shared countdown.</summary>
public sealed partial class DirectIndirectProofStageController : MonoBehaviour
{
    public enum TrialState { WaitingForActivation, Active, Validating, ChallengeComplete, StageComplete, GameOver }

#if UNITY_EDITOR
    // Lets editor play checks exercise the victory screen without changing account progress.
    public static bool SuppressProgressionForValidation { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetValidationFlag() { SuppressProgressionForValidation = false; }
#endif

    [SerializeField] private DirectIndirectProofCatalog catalog;
    [SerializeField] private Transform bookStatue;
    [SerializeField] private Transform boardInteractionPoint;
    [SerializeField] private TMP_Text boardText;
    [SerializeField] private ProofWorldPedestal[] methodPedestals = new ProofWorldPedestal[0];
    [SerializeField] private TrialProofTablet[] sourceTablets = new TrialProofTablet[0];
    [SerializeField] private ProofWorldPedestal[] stepSlots = new ProofWorldPedestal[0];
    [SerializeField] private Transform directAltar;
    [SerializeField] private Transform indirectAltar;
    [SerializeField] private Transform neutralAltar;
    [SerializeField] private Transform directNameplate;
    [SerializeField] private Transform indirectNameplate;
    [SerializeField] private TMP_Text neutralTabletText;
    [SerializeField] private Material contrapositiveMaterial;
    [SerializeField] private Material contradictionMaterial;
    [SerializeField] private GameObject directChoiceGlow;
    [SerializeField] private GameObject indirectChoiceGlow;
    [SerializeField] private GameObject bookErrorGlow;
    [SerializeField] private Material choiceYellow;
    [SerializeField] private Material choiceGreen;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private TMP_Text selectionText;
    [SerializeField] private GameObject readerPanel;
    [SerializeField] private TMP_Text readerTitle;
    [SerializeField] private TMP_Text readerBody;
    [SerializeField] private Button selectTabletButton;
    [SerializeField] private Button closeReaderButton;
    [SerializeField] private GameObject resultsPanel;
    [SerializeField] private TMP_Text resultsText;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button menuButton;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip correctSound;
    [SerializeField] private AudioClip incorrectSound;
    [SerializeField, Min(1f)] private float interactionRange = 3.5f;
    [SerializeField, Min(1f)] private float startingSeconds = 600f;
    [SerializeField, Min(0f)] private float wrongPenaltySeconds = 10f;

    private readonly int[] placedTabletIds = new int[5];
    private GameInput input;
    private Player player;
    private TrialState state;
    private int challengeIndex;
    private int selectedMethod = -1;
    private int selectedSource = -1;
    private int readingSource = -1;
    private int correctCount;
    private int mistakeCount;
    private float secondsLeft;
    private float activeSeconds;
    private bool timerRunning;
    private ProofAltarOrderingBoard altarBoard;
    private bool altarBoardOpened;
    private bool stepsReordered;
    private TrialProofChallenge activeChallenge;
    private int activeVariantIndex = -1;
    private Renderer[] bookRenderers;
    private Material[][] originalBookMaterials;
    private Material bookFlashMaterial;
    private bool bookIsFlashing;

    public TrialState State => state;
    public int ChallengeNumber => challengeIndex + 1;
    public int CorrectCount => correctCount;
    public int MistakeCount => mistakeCount;
    public float SecondsLeft => secondsLeft;
    public float ActiveSeconds => activeSeconds;
    public int SelectedMethod => selectedMethod;
    public int ActiveVariantIndex => activeVariantIndex;
    public bool IsReaderOpen => readerPanel != null && readerPanel.activeSelf ||
        altarBoard != null && altarBoard.IsOpen;
    public bool CanDebugClearChallenge => state == TrialState.Active;
    public int PlacedTabletId(int slot) => placedTabletIds[slot];
    private TrialProofChallenge Current => activeChallenge ?? catalog.challenges[challengeIndex];

    private void Awake()
    {
        if (catalog == null || catalog.challenges == null || catalog.challenges.Length != 5 ||
            catalog.variantSets == null || catalog.variantSets.Length != 5 ||
            bookStatue == null || boardInteractionPoint == null || boardText == null ||
            methodPedestals.Length != 3 || sourceTablets.Length != 5 || stepSlots.Length != 5 ||
            directAltar == null || indirectAltar == null || neutralAltar == null || neutralTabletText == null ||
            directChoiceGlow == null || indirectChoiceGlow == null || bookErrorGlow == null ||
            choiceYellow == null || choiceGreen == null ||
            timerText == null || progressText == null || promptText == null || selectionText == null ||
            readerPanel == null || readerTitle == null || readerBody == null ||
            selectTabletButton == null || closeReaderButton == null || resultsPanel == null ||
            resultsText == null || retryButton == null || menuButton == null)
        {
            Debug.LogError("Trial of Proofs has missing challenge, station, Book, or UI references.", this);
            enabled = false;
            return;
        }
        for (int i = 0; i < catalog.variantSets.Length; i++)
        {
            TrialProofChallenge[] variants = catalog.variantSets[i]?.variants;
            if (variants == null || variants.Length != 10 || Array.Exists(variants, v => v == null))
            {
                Debug.LogError("Challenge " + (i + 1) + " must have ten proof variants.", this);
                enabled = false;
                return;
            }
        }
        bookFlashMaterial = bookErrorGlow.GetComponent<Renderer>()?.sharedMaterial;
        bookRenderers = Array.FindAll(bookStatue.GetComponentsInChildren<Renderer>(true),
            renderer => renderer is MeshRenderer || renderer is SkinnedMeshRenderer);
        originalBookMaterials = Array.ConvertAll(bookRenderers, renderer => renderer.sharedMaterials);
        if (bookFlashMaterial == null || bookRenderers.Length == 0)
        {
            Debug.LogError("Book Statue renderers or red feedback material are missing.", this);
            enabled = false;
            return;
        }
        for (int i = 0; i < placedTabletIds.Length; i++) placedTabletIds[i] = -1;
        secondsLeft = startingSeconds;
        BuildQuestionSchedule();
        state = TrialState.WaitingForActivation;
        readerPanel.SetActive(false);
        resultsPanel.SetActive(false);
        directChoiceGlow.SetActive(false);
        indirectChoiceGlow.SetActive(false);
        bookErrorGlow.SetActive(false);
        HideStations();
        UpdateAltarLayout();
        ShowReadyMessage();
        RefreshTimer();
    }

    private void Start()
    {
        StagePresentation.AdoptResult(resultsPanel, resultsText, retryButton, menuButton);
        if (!enabled) return;
        var trialHud=StageUITheme.Rect("CurrentStageHud",timerText.transform.parent);
        StageUITheme.CompactSurface(trialHud.gameObject.AddComponent<Image>());
        trialHud.GetComponent<Image>().raycastTarget=false;
        var trialTitle=StageUITheme.Text("CurrentStageLabel",trialHud,TrialCatalog.ForScene("Direct&Indirect").Title,26);
        timerText.transform.SetParent(trialHud,false);progressText.transform.SetParent(trialHud,false);
        PrelimUIPresentation.CompactHud(trialHud,(RectTransform)trialHud.parent,trialTitle,timerText,progressText);
        StagePresentation.Hud(trialHud, trialTitle, timerText, progressText);
        selectTabletButton.onClick.AddListener(SelectReadTablet);
        closeReaderButton.onClick.AddListener(CloseReader);
        retryButton.onClick.AddListener(RetryStage);
        menuButton.onClick.AddListener(ReturnToMenu);
        altarBoard = timerText.GetComponentInParent<Canvas>().GetComponent<ProofAltarOrderingBoard>();
        if (altarBoard == null) Debug.LogError("Proof Altar Ordering Board is missing from the HUD.", this);
        Guide = gameObject.AddComponent<DirectProofsGuide>();
        Guide.Initialize(this, trialHud);
        WorldGuidance = gameObject.AddComponent<DirectProofsWorldGuidance>();
        WorldGuidance.Initialize(this, Guide.SafeArea, boardInteractionPoint);
    }

    private void OnEnable() { BindInput(); }

    private void OnDisable()
    {
        Guide?.Cancel();
        WorldGuidance?.Close();
        BookStatuePresentation.SetRequired(bookStatue, false);
        RestoreBookMaterials();
        if (input != null) input.OnInteractAction -= OnInteract;
        if (input != null) input.SetGameplayInputBlocked(false);
        if (player != null) player.ToggleControl(true);
    }

    private void BindInput()
    {
        BookStatuePresentation.BindHand(bookStatue, () => isActiveAndEnabled && !GuidanceBlocksGameplay && !IsReaderOpen && player != null && player.enabled && (state == TrialState.WaitingForActivation || state == TrialState.Active) && NearestTarget() == 0, () => OnInteract(this, EventArgs.Empty));
        GameInput current = GameInput.Instance != null ? GameInput.Instance : FindFirstObjectByType<GameInput>();
        if (current == input) return;
        if (input != null) input.OnInteractAction -= OnInteract;
        input = current;
        if (input != null && isActiveAndEnabled) input.OnInteractAction += OnInteract;
    }

    private void Update()
    {
        BookStatuePresentation.SetRequired(bookStatue, isActiveAndEnabled && !GuidanceBlocksGameplay && state == TrialState.WaitingForActivation);
        if (input == null) BindInput();
        if (player == null) player = Player.LocalInstance != null ? Player.LocalInstance : FindFirstObjectByType<Player>();
        if (timerRunning && !GuidanceBlocksGameplay)
        {
            float elapsed = Time.deltaTime;
            activeSeconds += elapsed;
            secondsLeft = Mathf.Max(0f, secondsLeft - elapsed);
            if (secondsLeft <= 0f) Finish(false);
        }
        RefreshTimer();
        if (GuidanceBlocksGameplay || IsViewingBoard) return;
        if (state == TrialState.StageComplete || state == TrialState.GameOver || IsReaderOpen) return;

        RefreshQuestionCounter();
        if (state == TrialState.ChallengeComplete)
        {
            promptText.text = "Proof accepted. The timer is paused.";
            return;
        }
        if (state == TrialState.Validating)
        {
            promptText.text = "Check the proof, then try again.";
            return;
        }
        int target = NearestTarget();
        if (target == 0) promptText.text = state == TrialState.Active ?
            "INTERACT • Confirm proof at the Book Statue" :
            BookStatuePresentation.HandPromptText;
        else if (target == 1) promptText.text = "INTERACT • Read the full problem";
        else if (target == 42) promptText.text = "INTERACT • Arrange the proof at the trial altar";
        else if (target == 40 || target == 41) promptText.text = challengeIndex == 0 ?
            "INTERACT • Select " + (target == 40 ? "Direct" : "Indirect") + " Proof" :
            "INTERACT • Open the proof-step board";
        else if (target >= 10 && target < 13) promptText.text =
            "INTERACT • Choose " + MethodName(target - 10);
        else if (target >= 20 && target < 25) promptText.text =
            "INTERACT • Read Tablet " + Letter(target - 20);
        else if (target >= 30 && target < 35) promptText.text = selectedSource >= 0 ?
            "INTERACT • Place Tablet " + Letter(selectedSource) + " in Step " + (target - 29) :
            placedTabletIds[target - 30] >= 0 ? "INTERACT • Remove the tablet from Step " + (target - 29) :
            "Read and select a tablet first.";
        else if (state == TrialState.Active && challengeIndex == 0) promptText.text =
            "Choose the Direct or Indirect Proof altar, then confirm at the Book Statue.";
        else if (state == TrialState.Active && challengeIndex >= 1) promptText.text =
            "Interact with the active altar to drag the proof steps into order.";
        else promptText.text = state == TrialState.WaitingForActivation ?
            "Approach the Book Statue to begin." :
            "Choose a strategy or arrange tablets, then confirm at the Book Statue.";
    }

    private int NearestTarget()
    {
        if (player == null) return -1;
        Vector3 position = player.transform.position;
        float best = interactionRange * interactionRange;
        int target = -1;
        Consider(bookStatue.position, position, 0, ref best, ref target);
        if (state != TrialState.Active) return target;
        Consider(boardInteractionPoint.position, position, 1, ref best, ref target);
        if (directAltar.gameObject.activeInHierarchy)
            Consider(AltarCenter(directAltar) + Vector3.forward * 3.7f, position, 40, ref best, ref target);
        if (indirectAltar.gameObject.activeInHierarchy)
            Consider(AltarCenter(indirectAltar) + Vector3.forward * 3.7f, position, 41, ref best, ref target);
        if (neutralAltar.gameObject.activeInHierarchy)
            Consider(AltarCenter(neutralAltar) + Vector3.forward * 3.7f, position, 42, ref best, ref target);
        for (int i = 0; i < methodPedestals.Length; i++)
            if (methodPedestals[i].gameObject.activeInHierarchy)
                Consider(methodPedestals[i].transform.position, position, 10 + i, ref best, ref target);
        for (int i = 0; i < sourceTablets.Length; i++)
            if (sourceTablets[i].gameObject.activeInHierarchy)
                Consider(sourceTablets[i].transform.position, position, 20 + i, ref best, ref target);
        for (int i = 0; i < stepSlots.Length; i++)
            if (stepSlots[i].gameObject.activeInHierarchy)
                Consider(stepSlots[i].transform.position, position, 30 + i, ref best, ref target);
        return target;
    }

    private static void Consider(Vector3 item, Vector3 playerPosition, int id, ref float best, ref int target)
    {
        float dx = item.x - playerPosition.x, dz = item.z - playerPosition.z;
        float distance = dx * dx + dz * dz;
        if (distance >= best) return;
        best = distance;
        target = id;
    }

    private void OnInteract(object sender, EventArgs args)
    {
        if (GuidanceBlocksGameplay || input != null && input.GameplayInputBlocked) return;
        if (IsReaderOpen || state == TrialState.Validating || state == TrialState.ChallengeComplete ||
            state == TrialState.StageComplete || state == TrialState.GameOver) return;
        int target = NearestTarget();
        if (target == 0)
        {
            if (state == TrialState.WaitingForActivation) ActivateChallenge();
            else SubmitProof();
        }
        else if (target == 1) OpenProblem();
        else if (target == 40 || target == 41) InteractAltar(target == 40);
        else if (target == 42) InteractNeutralAltar();
        else if (target >= 10 && target < 13) SelectMethod(target - 10);
        else if (target >= 20 && target < 25) ReadTablet(target - 20);
        else if (target >= 30 && target < 35) PlaceOrRemove(target - 30);
    }

    public void ActivateChallenge()
    {
        if (state != TrialState.WaitingForActivation || GuidanceBlocksGameplay) return;
        activeVariantIndex = selectedVariants[challengeIndex, questionIndex];
        activeChallenge = catalog.variantSets[challengeIndex].variants[activeVariantIndex];
        adminSelectedVariants[challengeIndex] = activeVariantIndex;
        TrialProofChallenge challenge = Current;
        selectedMethod = -1;
        selectedSource = -1;
        readingSource = -1;
        altarBoardOpened = false;
        stepsReordered = false;
        directChoiceGlow.SetActive(false);
        indirectChoiceGlow.SetActive(false);
        bookErrorGlow.SetActive(false);
        for (int i = 0; i < placedTabletIds.Length; i++) placedTabletIds[i] = -1;
        boardText.text = "CHALLENGE " + ChallengeNumber + " · QUESTION " + QuestionNumber + " OF 2\n" + challenge.title + "\n\n" + challenge.problem;
        UpdateAltarLayout();
        neutralTabletText.text = challengeIndex >= 3 ? ProofGoal(challenge.problem) : "";
        if (challengeIndex >= 1)
        {
            for (int i = 0; i < challenge.tablets.Length && i < placedTabletIds.Length; i++)
                placedTabletIds[i] = challenge.displayOrder != null && challenge.displayOrder.Length == challenge.tablets.Length ?
                    challenge.displayOrder[i] : i;
        }
        foreach (ProofWorldPedestal pedestal in methodPedestals) pedestal.gameObject.SetActive(false);
        for (int i = 0; i < sourceTablets.Length; i++)
        {
            sourceTablets[i].gameObject.SetActive(false);
            stepSlots[i].gameObject.SetActive(false);
        }
        state = TrialState.Active;
        timerRunning = true;
        if (challengeIndex == 0)
            selectionText.text = "Choose Direct or Indirect Proof at an altar";
        else if (challengeIndex >= 1)
            selectionText.text = "Open the active altar board and drag the steps";
        else RefreshSelection();
    }

    public void InteractAltar(bool direct)
    {
        if (GuidanceBlocksGameplay || state != TrialState.Active || challengeIndex > 2) return;
        if (challengeIndex == 0)
        {
            bool deselect = selectedMethod >= 0 && (selectedMethod == (int)TrialProofMethod.Direct) == direct;
            // The player chooses a proof family; contrapositive and contradiction are indirect.
            selectedMethod = deselect ? -1 : direct ? (int)TrialProofMethod.Direct :
                (int)(Current.correctMethod == TrialProofMethod.Direct ?
                    TrialProofMethod.Contrapositive : Current.correctMethod);
            directChoiceGlow.SetActive(!deselect && direct);
            indirectChoiceGlow.SetActive(!deselect && !direct);
            if (!deselect) (direct ? directChoiceGlow : indirectChoiceGlow).GetComponent<Renderer>().sharedMaterial = choiceYellow;
            selectionText.text = deselect ? "Selection cleared • Choose a proof altar" :
                "Selected: " + (direct ? "DIRECT" : "INDIRECT") + " PROOF  •  Confirm at the Book Statue";
            return;
        }
        if (direct != (challengeIndex == 1) || altarBoard == null) return;
        altarBoardOpened = true;
        altarBoard.Show(this, Current, placedTabletIds, false);
        SetGameplayEnabled(false);
    }

    public void InteractNeutralAltar()
    {
        if (GuidanceBlocksGameplay || state != TrialState.Active || challengeIndex < 3 || altarBoard == null) return;
        altarBoardOpened = true;
        altarBoard.Show(this, Current, placedTabletIds, true);
        SetGameplayEnabled(false);
    }

    public void CloseAltarBoard()
    {
        if (state == TrialState.Active) SetGameplayEnabled(true);
        selectionText.text = "Steps arranged  •  Confirm at the Book Statue";
    }

    public void OnAltarStepsReordered() { stepsReordered = true; }

    public void SelectMethod(int method)
    {
        if (GuidanceBlocksGameplay || state != TrialState.Active || Current.kind == TrialProofKind.ArrangeSteps || method < 0 || method > 2) return;
        selectedMethod = method;
        RefreshStationVisuals();
        RefreshSelection();
    }

    private void PositionProofStations(TrialProofMethod method)
    {
        bool direct = method == TrialProofMethod.Direct;
        // Keep the tablet/step pairs inside the chamber, facing across a short aisle.
        float sourceX = direct ? 66.6f : 46.8f;
        float slotX = direct ? 62.1f : 51.3f;
        Quaternion facingCenter = Quaternion.Euler(0f, direct ? 90f : -90f, 0f);
        for (int i = 0; i < sourceTablets.Length; i++)
        {
            float z = 50.2f + i * 4f;
            sourceTablets[i].MoveRack(new Vector3(sourceX, 1.42f, z), facingCenter);
            stepSlots[i].transform.SetPositionAndRotation(new Vector3(slotX, 1.42f, z), facingCenter);
        }
    }

    public void DebugClearCurrentChallenge()
    {
        if (!AdminAuthorization.TryUseGameplayAdmin()) return;
        if (!GuidanceBlocksGameplay && CanDebugClearChallenge) CompleteChallenge();
    }

    public void ReadTablet(int sourceIndex)
    {
        if (GuidanceBlocksGameplay || state != TrialState.Active || sourceIndex < 0 || sourceIndex >= sourceTablets.Length ||
            !sourceTablets[sourceIndex].gameObject.activeInHierarchy ||
            !sourceTablets[sourceIndex].InteractionEnabled) return;
        readingSource = sourceIndex;
        TrialProofTablet tablet = sourceTablets[sourceIndex];
        readerTitle.text = "TABLET " + Letter(sourceIndex);
        readerBody.text = tablet.Statement +
            (string.IsNullOrEmpty(tablet.Justification) ? "" : "\n\n" + tablet.Justification);
        selectTabletButton.gameObject.SetActive(true);
        readerPanel.SetActive(true);
        SetGameplayEnabled(false);
    }

    public void OpenProblem()
    {
        if (GuidanceBlocksGameplay || state != TrialState.Active) return;
        readingSource = -1;
        readerTitle.text = "CHALLENGE " + ChallengeNumber + " · QUESTION " + QuestionNumber + " OF 2\n" + Current.title;
        readerBody.text = Current.problem;
        selectTabletButton.gameObject.SetActive(false);
        readerPanel.SetActive(true);
        Canvas.ForceUpdateCanvases();
        var problemScroll = readerPanel.GetComponent<ScrollRect>(); if (problemScroll != null) problemScroll.verticalNormalizedPosition = 1;
        SetGameplayEnabled(false);
    }

    public void SelectReadTablet()
    {
        if (GuidanceBlocksGameplay) return;
        if (readingSource < 0 || readingSource >= sourceTablets.Length) return;
        if (selectedSource >= 0) sourceTablets[selectedSource].SetSelected(false);
        selectedSource = readingSource;
        sourceTablets[selectedSource].SetSelected(true);
        CloseReader();
        RefreshStationVisuals();
        RefreshSelection();
    }

    public void CloseReader()
    {
        readingSource = -1;
        readerPanel.SetActive(false);
        if (state == TrialState.Active) SetGameplayEnabled(true);
    }

    public void PlaceOrRemove(int slot)
    {
        if (GuidanceBlocksGameplay || state != TrialState.Active || slot < 0 || slot >= Current.tablets.Length ||
            !stepSlots[slot].gameObject.activeInHierarchy) return;
        if (selectedSource >= 0)
        {
            TrialProofTablet source = sourceTablets[selectedSource];
            int id = source.TabletId;
            if (source.AssignedSlot >= 0) placedTabletIds[source.AssignedSlot] = -1;
            int displacedId = placedTabletIds[slot];
            if (displacedId >= 0 && displacedId != id)
            {
                TrialProofTablet displaced = SourceWithId(displacedId);
                if (displaced != null) displaced.SetPlacement(-1);
            }
            placedTabletIds[slot] = id;
            source.SetPlacement(slot);
            selectedSource = -1;
        }
        else if (placedTabletIds[slot] >= 0)
        {
            TrialProofTablet source = SourceWithId(placedTabletIds[slot]);
            if (source != null) source.SetPlacement(-1);
            placedTabletIds[slot] = -1;
        }
        RefreshStationVisuals();
        RefreshSelection();
    }

    private TrialProofTablet SourceWithId(int id)
    {
        foreach (TrialProofTablet source in sourceTablets)
            if (source.gameObject.activeInHierarchy && source.TabletId == id) return source;
        return null;
    }

    public void SubmitProof()
    {
        if (GuidanceBlocksGameplay || state != TrialState.Active) return;
        if (challengeIndex == 0 && selectedMethod < 0)
        { promptText.text = "Choose the Direct or Indirect Proof altar first."; return; }
        if (challengeIndex >= 1 && (!altarBoardOpened || !stepsReordered))
        { promptText.text = "Open the altar board and drag the steps into order first."; return; }
        if (Current.kind == TrialProofKind.ChooseAndArrange && selectedMethod < 0)
        { promptText.text = "Choose a proof strategy on the altar board first."; return; }
        state = TrialState.Validating;
        int[] answer = new int[Current.tablets.Length];
        Array.Copy(placedTabletIds, answer, answer.Length);
        if (Current.IsCorrect(selectedMethod, answer)) CompleteChallenge();
        else
        {
            mistakeCount++;
            secondsLeft = Mathf.Max(0f, secondsLeft - wrongPenaltySeconds);
            Play(incorrectSound);
            if (secondsLeft <= 0f) timerRunning = false;
            StartCoroutine(WrongFeedback());
        }
    }

    private IEnumerator WrongFeedback()
    {
        FlashBookMaterials();
        SetAllActiveStates(ProofWorldPedestal.VisualState.Incorrect);
        SetGameplayEnabled(false);
        yield return new WaitForSeconds(.75f);
        RestoreBookMaterials();
        if (state != TrialState.Validating) yield break;
        if (secondsLeft <= 0f) { Finish(false); yield break; }
        state = TrialState.Active;
        RefreshStationVisuals();
        SetGameplayEnabled(true);
    }

    private void FlashBookMaterials()
    {
        if (bookIsFlashing) return;
        for (int i = 0; i < bookRenderers.Length; i++)
        {
            Material[] flash = new Material[originalBookMaterials[i].Length];
            for (int slot = 0; slot < flash.Length; slot++) flash[slot] = bookFlashMaterial;
            bookRenderers[i].sharedMaterials = flash;
        }
        bookIsFlashing = true;
    }

    private void RestoreBookMaterials()
    {
        if (!bookIsFlashing || bookRenderers == null) return;
        for (int i = 0; i < bookRenderers.Length; i++)
            if (bookRenderers[i] != null) bookRenderers[i].sharedMaterials = originalBookMaterials[i];
        bookIsFlashing = false;
    }

    private void CompleteChallenge()
    {
        correctCount++;
        timerRunning = false;
        state = TrialState.ChallengeComplete;
        SetAllActiveStates(ProofWorldPedestal.VisualState.Correct);
        if (challengeIndex == 0)
            (selectedMethod == (int)TrialProofMethod.Direct ? directChoiceGlow : indirectChoiceGlow)
                .GetComponent<Renderer>().sharedMaterial = choiceGreen;
        Play(correctSound);
        boardText.text = "CHALLENGE " + ChallengeNumber + " · QUESTION " + QuestionNumber + " COMPLETE\n" + Current.title +
            "\n\n" + Current.explanation + "\n\nThe timer is paused.";
        StartCoroutine(AdvanceChallenge());
    }

    private IEnumerator AdvanceChallenge()
    {
        yield return new WaitForSeconds(.9f);
        if (state != TrialState.ChallengeComplete) yield break;
        HideStations();
        directChoiceGlow.SetActive(false);
        indirectChoiceGlow.SetActive(false);
        bookErrorGlow.SetActive(false);
        questionIndex++;
        if (questionIndex >= QuestionsPerChallenge) { questionIndex = 0; challengeIndex++; }
        activeChallenge = null;
        activeVariantIndex = -1;
        if (challengeIndex >= catalog.challenges.Length) Finish(true);
        else
        {
            state = TrialState.WaitingForActivation;
            UpdateAltarLayout();
            ShowReadyMessage();
        }
    }

    private void ShowReadyMessage()
    {
        boardText.text = "CHALLENGE " + ChallengeNumber + " · QUESTION " + QuestionNumber + " OF 2\n\nUse the hand at the Book Statue to start. Read the problem, choose or arrange the proof at the active altar, then confirm at the book. The shared timer is paused.";
        neutralTabletText.text = "";
        RefreshQuestionCounter();
        selectionText.text = "Timer paused • Activate the Book Statue";
    }

    private void HideStations()
    {
        foreach (ProofWorldPedestal pedestal in methodPedestals) pedestal.gameObject.SetActive(false);
        foreach (TrialProofTablet tablet in sourceTablets) tablet.gameObject.SetActive(false);
        foreach (ProofWorldPedestal slot in stepSlots) slot.gameObject.SetActive(false);
    }

    private static Vector3 AltarCenter(Transform altar)
    {
        Renderer[] renderers = altar.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return altar.position;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds.center;
    }

    private static void MoveAltar(Transform altar, float x)
    {
        Vector3 center = AltarCenter(altar);
        altar.position += new Vector3(x - center.x, 0f, 45.5f - center.z);
    }

    private void UpdateAltarLayout()
    {
        bool showDirect = challengeIndex == 0 || challengeIndex == 1;
        bool showIndirect = challengeIndex == 0 || challengeIndex == 2;
        bool showNeutral = challengeIndex == 3 || challengeIndex == 4;
        MoveAltar(directAltar, challengeIndex == 1 ? 56.7f : 67.7f);
        MoveAltar(indirectAltar, challengeIndex == 2 ? 56.7f : 45.7f);
        MoveAltar(neutralAltar, 56.7f);
        directAltar.gameObject.SetActive(showDirect);
        indirectAltar.gameObject.SetActive(showIndirect);
        neutralAltar.gameObject.SetActive(showNeutral);
        // The main Information Board already contains the complete question.
        // Keep this serialized reference for existing data, but retire the redundant overhead tablet.
        neutralTabletText.transform.parent.gameObject.SetActive(false);
        PositionNameplate(directNameplate, directAltar, showDirect);
        PositionNameplate(indirectNameplate, indirectAltar, showIndirect);
        directChoiceGlow.transform.position = new Vector3(AltarCenter(directAltar).x, 2.4f, 45.5f);
        indirectChoiceGlow.transform.position = new Vector3(AltarCenter(indirectAltar).x, 2.4f, 45.5f);
    }

    private static void PositionNameplate(Transform nameplate, Transform altar, bool visible)
    {
        if (nameplate == null) return;
        Renderer[] renderers = altar.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            nameplate.position = new Vector3(bounds.center.x, bounds.max.y + .55f, bounds.center.z);
        }
        nameplate.gameObject.SetActive(visible);
    }

    private void RefreshStationVisuals()
    {
        for (int i = 0; i < methodPedestals.Length; i++)
            if (methodPedestals[i].gameObject.activeInHierarchy)
                methodPedestals[i].SetState(i == selectedMethod ?
                    ProofWorldPedestal.VisualState.Selected : ProofWorldPedestal.VisualState.Normal);
        for (int i = 0; i < sourceTablets.Length; i++)
        {
            if (sourceTablets[i].gameObject.activeInHierarchy)
            {
                TrialProofTablet tablet = sourceTablets[i];
                ProofWorldPedestal pedestal = tablet.GetComponent<ProofWorldPedestal>();
                string location = tablet.AssignedSlot >= 0 ? "IN STEP " + (tablet.AssignedSlot + 1) + "\n" : "";
                pedestal.SetContent("TABLET " + Letter(i), location + Preview(tablet.Statement, 50));
                pedestal.SetState(i == selectedSource ?
                    ProofWorldPedestal.VisualState.Selected : ProofWorldPedestal.VisualState.Normal);
            }
            if (!stepSlots[i].gameObject.activeInHierarchy) continue;
            int id = placedTabletIds[i];
            stepSlots[i].SetContent("STEP " + (i + 1), id < 0 ? "EMPTY" :
                "TABLET " + Letter(SourceIndexWithId(id)) + "\n" + Preview(Current.tablets[id].statement, 42));
            stepSlots[i].SetState(id >= 0 ? ProofWorldPedestal.VisualState.Selected :
                ProofWorldPedestal.VisualState.Normal);
        }
    }

    private int SourceIndexWithId(int id)
    {
        for (int i = 0; i < sourceTablets.Length; i++)
            if (sourceTablets[i].gameObject.activeInHierarchy && sourceTablets[i].TabletId == id) return i;
        return id;
    }

    private void SetAllActiveStates(ProofWorldPedestal.VisualState visual)
    {
        foreach (ProofWorldPedestal pedestal in methodPedestals)
            if (pedestal.gameObject.activeInHierarchy) pedestal.SetState(visual);
        foreach (TrialProofTablet tablet in sourceTablets)
            if (tablet.gameObject.activeInHierarchy) tablet.GetComponent<ProofWorldPedestal>().SetState(visual);
        foreach (ProofWorldPedestal slot in stepSlots)
            if (slot.gameObject.activeInHierarchy) slot.SetState(visual);
    }

    private void RefreshSelection()
    {
        string method = selectedMethod < 0 ? "None" : MethodName(selectedMethod);
        string tablet = selectedSource < 0 ? "None" : "Tablet " + Letter(selectedSource);
        selectionText.text = "Strategy: " + method + "  •  Carrying: " + tablet;
    }

    private void RefreshTimer()
    {
        int whole = Mathf.CeilToInt(secondsLeft);
        timerText.text = (whole / 60).ToString("00") + ":" + (whole % 60).ToString("00");
    }

    private void SetGameplayEnabled(bool enabledForPlayer)
    {
        if (input != null) input.SetGameplayInputBlocked(!enabledForPlayer);
        if (player != null) player.ToggleControl(enabledForPlayer);
    }

    private void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    private void Finish(bool won)
    {
        if (state == TrialState.StageComplete || state == TrialState.GameOver) return;
        RestoreBookMaterials();
        timerRunning = false;
        state = won ? TrialState.StageComplete : TrialState.GameOver;
        Guide?.Cancel();
        readerPanel.SetActive(false);
        if (altarBoard != null) altarBoard.ForceClose();
        HideStations();
        SetGameplayEnabled(false);
        boardText.text = won ? "TRIAL OF PROOFS COMPLETE\nAll five seals are restored." :
            "TIME'S UP\nReturn to the Book Statue and try again.";
        int played = Mathf.FloorToInt(activeSeconds);
        resultsText.text = won ? string.Format(
            "DIRECT AND INDIRECT PROOFS STAGE COMPLETE!\nTime Played: {0:00}:{1:00}\nCorrect: {2}\nMistakes: {3}",
            played / 60, played % 60, correctCount, mistakeCount) :
            "TIME'S UP\nQuestions completed: " + correctCount + "/10\nChallenges completed: " + Mathf.Min(challengeIndex, 5) + "/5\nMistakes: " + mistakeCount;
        resultsPanel.SetActive(true);
        if (won) StagePresentation.Completed(resultsPanel, 5);
        if (!won) return;
#if UNITY_EDITOR
        if (SuppressProgressionForValidation) return;
#endif
        StageCompleteManager.UnlockStage(Mathf.Min(6, StageSelectionState.LastStage));
    }

    public void RetryStage()
    {
        TrialRewards.RecordRetry(5);
        Guide?.Cancel();
        if (input != null) input.SetGameplayInputBlocked(false);
        Time.timeScale = 1f;
        TeleportManager.Navigate(SceneManager.GetActiveScene().name);
    }

    public void ReturnToMenu()
    {
        Guide?.Cancel();
        if (input != null) input.SetGameplayInputBlocked(false);
        Time.timeScale = 1f;
        TeleportManager.Navigate("Main Menu");
    }

    private static string Letter(int index) { return ((char)('A' + index)).ToString(); }
    private static string ProofGoal(string problem)
    {
        if (string.IsNullOrEmpty(problem)) return "";
        int firstPeriod = problem.IndexOf('.');
        return firstPeriod >= 0 ? problem.Substring(0, firstPeriod + 1) : problem;
    }
    private static string MethodName(int index) { return index == 0 ? "DIRECT PROOF" :
        index == 1 ? "CONTRAPOSITIVE" : "CONTRADICTION"; }
    private static string MethodHint(int index) { return index == 0 ? "Assume the premise; derive the result." :
        index == 1 ? "Prove not Q implies not P." : "Assume the opposite; reach an impossibility."; }
    private static string Preview(string value, int limit)
    {
        if (string.IsNullOrEmpty(value)) return "";
        string compact = value.Replace('\n', ' ').Trim();
        return compact.Length <= limit ? compact : compact.Substring(0, limit - 1) + "…";
    }
}
