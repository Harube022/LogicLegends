using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Five Book-confirmed proof challenges share one timer and the existing mobile input.
/// </summary>
public sealed partial class ProofsLibraryStageController : MonoBehaviour
{
    [SerializeField] private Transform bookStatue;
    [SerializeField] private ProofChallengeCatalog catalog;
    [SerializeField] private Transform stepBoardWorld;
    [SerializeField] private ProofWorldBoardController worldBoard;
    [SerializeField] private Button editBoardButton;
    [SerializeField] private Transform[] scrolls = new Transform[0];
    [SerializeField] private ProofWorldPedestal[] scrollSources = new ProofWorldPedestal[0];
    [SerializeField] private TMP_Text[] scrollStatus = new TMP_Text[0];
    [SerializeField] private Transform[] pedestals = new Transform[0];
    [SerializeField] private Renderer[] pedestalEmblems = new Renderer[0];
    [SerializeField] private TMP_Text[] pedestalCounts = new TMP_Text[0];
    [SerializeField] private GameObject[] carriedVisuals = new GameObject[0];
    [SerializeField] private TMP_Text boardText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private GameObject readerPanel;
    [SerializeField] private TMP_Text readerTitle;
    [SerializeField] private TMP_Text readerBody;
    [SerializeField] private Button carryButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private GameObject resultsPanel;
    [SerializeField] private TMP_Text resultsText;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button menuButton;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip correctSound;
    [SerializeField] private AudioClip incorrectSound;
    [SerializeField, Min(1f)] private float interactionRange = 3f;
    [SerializeField, Min(1f)] private float startingSeconds = 600f;
    [SerializeField, Min(0f)] private float wrongPenalty = 10f;

    private readonly Color neutral = new Color(.14f, .50f, .90f);
    private readonly Color informal = new Color(.90f, .20f, .12f);
    private readonly Color success = new Color(.10f, .95f, .36f);
    private readonly Color failure = new Color(1f, .13f, .12f);
    private int[] placements;
    private int carriedScroll = -1;
    private int readingScroll = -1;
    private GameInput input;
    private Player player;
    private GameObject takeBackPanel;
    private TMP_Text takeBackTitle;
    private readonly Button[] takeBackButtons = new Button[4];
    private readonly TMP_Text[] placedScrollLabels = new TMP_Text[4];
    private int takeBackPedestal = -1;
    private float secondsLeft;
    private bool started;
    private bool finished;
    private bool flashing;
    private bool transitioning;
    private int challengeIndex;
    private int questionIndex;
    public const int QuestionsPerChallenge = 2;
    private readonly int[][] selectedVariants = new int[5][];
    private int correctCount;
    private int mistakeCount;
    private float activeGameplaySeconds;
    private ProofWorldPedestal nearestWorldPedestal;
    private ProofChallengeData activeChallenge;
    private static readonly int[] LastVariant = { -1, -1, -1, -1, -1 };

    public bool Started => started;
    public bool Finished => finished;
    public float SecondsLeft => secondsLeft;
    public int CarriedScroll => carriedScroll;
    public int ReadingScroll => readingScroll;
    public int ChallengeNumber => Mathf.Min(challengeIndex + 1, ChallengeCount);
    public int QuestionNumber => questionIndex + 1;
    public int CompletedChallenges => Mathf.Min(challengeIndex, ChallengeCount);
    public bool WaitingForBook => !started && !finished && !transitioning;
    public bool IsSelectedVariant(int challenge, int variant) => challenge >= 0 && challenge < selectedVariants.Length &&
        selectedVariants[challenge] != null && Array.IndexOf(selectedVariants[challenge], variant) >= 0;
    public int CorrectCount => correctCount;
    public int MistakeCount => mistakeCount;
    public float ActiveGameplaySeconds => activeGameplaySeconds;
    public bool CanDebugClearChallenge => !InputSuppressed && started && !finished && !transitioning && !flashing &&
        !readerPanel.activeSelf && !takeBackPanel.activeSelf;
    public int Placement(int index) => placements[index];

    private void Awake()
    {
        if (catalog == null || catalog.challenges.Length != 5 || catalog.variantSets.Length != 5 ||
            scrolls.Length != 4 || scrollSources.Length != 4 || scrollStatus.Length != 4 || carriedVisuals.Length != 4 ||
            pedestals.Length != 2 || pedestalEmblems.Length != 2 || pedestalCounts.Length != 2 ||
            bookStatue == null || boardText == null || timerText == null || promptText == null ||
            readerPanel == null || readerTitle == null || readerBody == null ||
            carryButton == null || closeButton == null || editBoardButton == null ||
            worldBoard == null ||
            resultsPanel == null ||
            resultsText == null || retryButton == null || menuButton == null)
        {
            Debug.LogError("Proofs Library needs five challenge definitions, four reusable scrolls, the step board, two altars, and Book/UI references.", this);
            enabled = false;
            return;
        }
        placements = new[] { -1, -1, -1, -1 };
        SelectPlaythroughQuestions();
        secondsLeft = startingSeconds;
        boardText.text = "PROOFS LIBRARY\nActivate the Book Statue to reveal the challenge.";
        foreach (Transform scroll in scrolls) scroll.gameObject.SetActive(false);
        foreach (ProofWorldPedestal source in scrollSources) source.gameObject.SetActive(false);
        foreach (GameObject visual in carriedVisuals) visual.SetActive(false);
        readerPanel.SetActive(false);
        resultsPanel.SetActive(false);
        editBoardButton.gameObject.SetActive(false);
        BuildTakeBackPanel();
        BuildPlacedScrollLabels();
        worldBoard.Hide();
        RefreshVisuals();
    }

    private void Start()
    {
        StagePresentation.AdoptResult(resultsPanel, resultsText, retryButton, menuButton);
        UnlockGameplay();
        carryButton.onClick.AddListener(CarryReadScroll);
        closeButton.onClick.AddListener(CloseReader);
        retryButton.onClick.AddListener(RetryStage);
        menuButton.onClick.AddListener(ReturnToMenu);
        Presentation = gameObject.AddComponent<ProofsStageUI>();
        Presentation.Initialize(this);
        Introductions = gameObject.AddComponent<ProofsChallengeIntroductions>();
        Introductions.Initialize(this, Presentation);
        gameObject.AddComponent<ProofsPedestalIndicators>().Initialize(this);
    }

    private void OnEnable()
    {
        BindInput();
        if (worldBoard != null) worldBoard.BindScrollInteraction(ReadWorldScroll, RefreshWorldCarryVisual);
    }

    private void OnDisable()
    {
        BookStatuePresentation.SetRequired(bookStatue, false);
        Introductions?.Cancel();
        RestoreReaderInput();
        if (worldBoard != null) worldBoard.BindScrollInteraction(null, null);
        if (input != null) input.OnInteractAction -= OnInteract;
        UnlockGameplay();
    }

    private void BindInput()
    {
        BookStatuePresentation.BindHand(bookStatue, () => isActiveAndEnabled && !InputSuppressed && !finished && !transitioning && !HasReaderOpen && player != null && player.enabled && secondsLeft > 0 && NearestTarget() == 0, InteractNearby);
        GameInput current = GameInput.Instance != null ? GameInput.Instance : FindFirstObjectByType<GameInput>();
        if (current == input) return;
        if (input != null) input.OnInteractAction -= OnInteract;
        input = current;
        if (input != null && isActiveAndEnabled) input.OnInteractAction += OnInteract;
    }

    private void Update()
    {
        BookStatuePresentation.SetRequired(bookStatue, isActiveAndEnabled && !InputSuppressed && !started &&
            !finished && !transitioning && secondsLeft > 0f);
        if (input == null) BindInput();
        if (player == null) player = Player.LocalInstance != null ? Player.LocalInstance : FindFirstObjectByType<Player>();
        if (started && !finished && !GameplayPaused)
        {
            secondsLeft = Mathf.Max(0f, secondsLeft - Time.deltaTime);
            activeGameplaySeconds += Time.deltaTime;
            if (secondsLeft <= 0f) Finish(false);
        }
        RefreshTimer();
        if (InputSuppressed) { promptText.gameObject.SetActive(false); return; }
        if (finished)
        {
            promptText.text = secondsLeft <= 0f ? "TIME'S UP" : "CHALLENGE COMPLETE";
            return;
        }
        if (readerPanel.activeSelf || takeBackPanel.activeSelf || player == null) return;
        promptText.gameObject.SetActive(true);
        int target = NearestTarget();
        if (transitioning) { promptText.text = QuestionNumber == QuestionsPerChallenge ? "CHALLENGE COMPLETE" : "QUESTION COMPLETE"; return; }
        if (target == 0) promptText.text = started ?
            "INTERACT  •  Confirm Question " + QuestionNumber + " of " + QuestionsPerChallenge :
            BookStatuePresentation.HandPromptText;
        else if (target >= 1 && target <= 4 && Current.kind == ProofChallengeKind.SortScrolls) promptText.text =
            "INTERACT  •  Read Scroll " + Letter(target - 1);
        else if (target >= 5 && target <= 6 && Current.kind == ProofChallengeKind.SortScrolls) promptText.text = carriedScroll >= 0 ?
            "INTERACT  •  Place Scroll " + Letter(carriedScroll) + " on the " +
            (target == 5 ? "FORMAL" : "INFORMAL") + " altar" :
            CountOnPedestal(target - 5) > 0 ? "INTERACT  •  Choose a scroll to take back" :
            "Carry a scroll here to classify it.";
        else if (target == 8 && Current.kind != ProofChallengeKind.SortScrolls)
            promptText.text = worldBoard.Prompt(nearestWorldPedestal);
        else promptText.text = !started ? "Approach the Book Statue to begin." :
            carriedScroll >= 0 ? "Carry Scroll " + Letter(carriedScroll) + " to an altar." :
            Current.kind == ProofChallengeKind.SortScrolls ? "Read a scroll at its pedestal, then carry it to an altar." :
            "Read a scroll, choose Get This Scroll, place it on a numbered pedestal, then confirm at the Book Statue.";
    }

    private int NearestTarget()
    {
        if (player == null) return -1;
        Vector3 p = player.transform.position;
        float best = interactionRange * interactionRange;
        int target = -1;
        float distance = FlatDistanceSquared(p, bookStatue.position);
        if (distance < best) { best = distance; target = 0; }
        if (started && Current.kind == ProofChallengeKind.SortScrolls && carriedScroll < 0)
            for (int i = 0; i < scrolls.Length; i++)
            {
                if (!scrolls[i].gameObject.activeInHierarchy) continue;
                distance = FlatDistanceSquared(p, scrolls[i].position);
                if (distance < best) { best = distance; target = i + 1; }
            }
        if (started && Current.kind == ProofChallengeKind.SortScrolls)
            for (int i = 0; i < pedestals.Length; i++)
            {
                distance = FlatDistanceSquared(p, pedestals[i].position);
                if (distance < best) { best = distance; target = i + 5; }
            }
        nearestWorldPedestal = null;
        if (started && Current.kind != ProofChallengeKind.SortScrolls)
        {
            float stationDistance;
            nearestWorldPedestal = worldBoard.Nearest(p, best, out stationDistance);
            if (nearestWorldPedestal != null) target = 8;
        }
        return target;
    }

    private static float FlatDistanceSquared(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x, dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    private static string Letter(int index) { return ((char)('A' + index)).ToString(); }

    private ProofChallengeData Current => activeChallenge ?? catalog.challenges[Mathf.Min(challengeIndex, catalog.challenges.Length - 1)];

    // Draw once per run, without replacement in each bank. Opening Help/Admin never redraws.
    private void SelectPlaythroughQuestions()
    {
        for (int c = 0; c < selectedVariants.Length; c++)
        {
            int count = catalog.variantSets[c].variants.Length;
            if (count < QuestionsPerChallenge) throw new InvalidOperationException("Each proof challenge needs at least two questions.");
            int previous = LastVariant[c] >= 0 ? LastVariant[c] : PlayerPrefs.GetInt("LogicLegends.ProofsLibrary.LastVariant." + c, -1);
            int first = UnityEngine.Random.Range(0, count - (previous >= 0 && previous < count ? 1 : 0));
            if (previous >= 0 && previous < count && first >= previous) first++;
            int second = UnityEngine.Random.Range(0, count - 1);
            if (second >= first) second++;
            selectedVariants[c] = new[] { first, second };
        }
    }

    private void ChooseVariant()
    {
        ProofChallengeData[] variants = catalog.variantSets[challengeIndex].variants;
        string historyKey = "LogicLegends.ProofsLibrary.LastVariant." + challengeIndex;
        int choice = selectedVariants[challengeIndex][questionIndex];
        activeChallenge = variants[choice];
        LastVariant[challengeIndex] = choice;
        PlayerPrefs.SetInt(historyKey, choice);
        PlayerPrefs.Save();
    }

    private void OnInteract(object sender, EventArgs args)
    {
        if (Tutorial != null) { Tutorial.TryPracticeInteraction(); return; }
        if (InputSuppressed || input != null && input.GameplayInputBlocked) return;
        if (readerPanel.activeSelf || takeBackPanel.activeSelf || finished || transitioning) return;
        int target = NearestTarget();
        if (target == 0)
        {
            if (!started) Activate();
            else Submit();
        }
        else if (target >= 1 && target <= 4 && Current.kind == ProofChallengeKind.SortScrolls) ReadScroll(target - 1);
        else if (target >= 5 && target <= 6 && Current.kind == ProofChallengeKind.SortScrolls)
        {
            if (carriedScroll >= 0) PlaceOnPedestal(target - 5);
            else RetrieveFromPedestal(target - 5);
        }
        else if (target == 8) worldBoard.Interact(nearestWorldPedestal);
    }

    public void Activate()
    {
        if (Tutorial != null) { Tutorial.TryPracticeInteraction(); return; }
        if (InputSuppressed) return;
        if (started || finished || transitioning) return;
        ChooseVariant();
        started = true;
        if (Current.kind == ProofChallengeKind.SortScrolls)
        {
            worldBoard.Hide();
            foreach (Transform pedestal in pedestals) pedestal.gameObject.SetActive(true);
            for (int i = 0; i < scrolls.Length; i++)
            {
                bool visible = i < Current.scrolls.Length;
                scrollSources[i].gameObject.SetActive(visible);
                if (visible)
                {
                    PositionScrollSource(i, Current.scrolls.Length);
                    scrollSources[i].SetTabletVisible(true);
                    scrollSources[i].SetContent("SCROLL " + Letter(i), Preview(Current.scrolls[i].content, 42));
                    scrollSources[i].SetState(ProofWorldPedestal.VisualState.Normal);
                }
                scrolls[i].gameObject.SetActive(visible);
                if (visible)
                {
                    scrolls[i].localPosition = scrollSources[i].transform.localPosition;
                    scrolls[i].Find("Proof Text").GetComponent<TMP_Text>().text =
                        Current.scrolls[i].title + "\n" + Current.scrolls[i].content;
                }
            }
        }
        else
        {
            foreach (Transform pedestal in pedestals) pedestal.gameObject.SetActive(false);
            foreach (ProofWorldPedestal source in scrollSources) source.gameObject.SetActive(false);
            worldBoard.Configure(Current);
        }
        editBoardButton.gameObject.SetActive(false);
        ShowChallenge();
        RefreshVisuals();
    }

    private void PositionScrollSource(int index, int count)
    {
        bool left = count == 2 ? index == 0 : index < 2;
        float x = left ? -9f : 9f;
        float z = count == 2 ? -1.2f : (index % 2 == 0 ? -3.3f : .2f);
        Transform station = scrollSources[index].transform;
        station.localPosition = new Vector3(x, 0f, z);
        station.localRotation = Quaternion.Euler(0f, left ? -90f : 90f, 0f);
    }

    private static string Preview(string content, int maxLength)
    {
        if (string.IsNullOrEmpty(content)) return "INTERACT TO READ";
        string compact = content.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return compact.Length <= maxLength ? compact : compact.Substring(0, maxLength).TrimEnd() + "…";
    }

    private void ShowChallenge()
    {
        string context = Current.kind != ProofChallengeKind.SortScrolls && !string.IsNullOrEmpty(Current.context) ?
            "\n" + Current.context : "";
        boardText.text = "CHALLENGE " + ChallengeNumber + " — " + Current.title.ToUpperInvariant() +
            "\nQuestion " + QuestionNumber + " of " + QuestionsPerChallenge +
            context + "\n" + Current.instructions + "\nConfirm at the Book Statue.";
    }

    public void ReadScroll(int index)
    {
        if (Tutorial != null) { Tutorial.ReadPracticeScroll(); return; }
        if (InputSuppressed) return;
        if (!started || finished || readerPanel.activeSelf || takeBackPanel.activeSelf || Current.kind != ProofChallengeKind.SortScrolls ||
            index < 0 || index >= Current.scrolls.Length || carriedScroll >= 0 ||
            placements[index] >= 0 || !scrolls[index].gameObject.activeInHierarchy) return;
        readingScroll = index;
        readerTitle.text = "SCROLL " + Letter(index) + " — " + Current.scrolls[index].title;
        readerBody.text = Current.scrolls[index].content;
        promptText.gameObject.SetActive(false);
        readerPanel.SetActive(true);
        Presentation?.RefreshReader(true);
        HoldReaderInput();
    }

    public void CarryReadScroll()
    {
        if (Tutorial != null) { Tutorial.CollectPracticeScroll(); return; }
        if (InputSuppressed) return;
        if (worldReadingSource != null)
        {
            if (started && !finished && readerPanel.activeSelf && worldBoard.CollectReadScroll(worldReadingSource)) CloseReader();
            return;
        }
        if (readingScroll < 0 || finished || !readerPanel.activeSelf || carriedScroll >= 0 || placements[readingScroll] >= 0) return;
        int index = readingScroll;
        CloseReader();
        Carry(index);
    }

    private void Carry(int index)
    {
        if (player == null) player = Player.LocalInstance != null ? Player.LocalInstance : FindFirstObjectByType<Player>();
        if (player == null) return;
        carriedScroll = index;
        placements[index] = -1;
        scrolls[index].gameObject.SetActive(false);
        scrollSources[index].SetTabletVisible(false);
        placedScrollLabels[index].gameObject.SetActive(false);
        Transform visual = carriedVisuals[index].transform;
        visual.SetParent(player.transform, false);
        visual.localPosition = new Vector3(.43f, 1.12f, .22f);
        visual.localRotation = Quaternion.Euler(0, 0, 28f);
        visual.localScale = Vector3.one * .55f;
        visual.gameObject.SetActive(true);
        RefreshVisuals();
    }

    public void CloseReader()
    {
        if (Tutorial != null) { Tutorial.ClosePracticeReader(); return; }
        readingScroll = -1;
        worldReadingSource = null;
        readerPanel.SetActive(false);
        Presentation?.RefreshReader();
        RestoreReaderInput();
        promptText.gameObject.SetActive(!InputSuppressed && !finished);
    }

    private void LockGameplay()
    {
        if (input != null) input.SetGameplayInputBlocked(true);
        if (player != null) player.ToggleControl(false);
    }

    private void UnlockGameplay()
    {
        if (input != null) input.SetGameplayInputBlocked(false);
        if (player != null) player.ToggleControl(true);
    }

    private void BuildTakeBackPanel()
    {
        Transform hud = timerText.transform.parent;
        takeBackPanel = new GameObject("Take Back Scroll", typeof(RectTransform), typeof(Image));
        takeBackPanel.transform.SetParent(hud, false);
        RectTransform panelRect = takeBackPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(.5f, .5f);
        panelRect.sizeDelta = new Vector2(1040f, 670f);
        takeBackPanel.GetComponent<Image>().color = new Color(.055f, .085f, .14f, .98f);

        takeBackTitle = TakeBackText("Title", takeBackPanel.transform, "TAKE BACK A SCROLL",
            new Vector2(0f, 278f), new Vector2(940f, 66f), 43f, new Color(1f, .82f, .52f));
        TakeBackText("Instructions", takeBackPanel.transform,
            "Read each preview, then choose the scroll to take back.",
            new Vector2(0f, 214f), new Vector2(940f, 70f), 27f, new Color(.91f, .87f, .78f));

        for (int i = 0; i < takeBackButtons.Length; i++)
        {
            int selectedScroll = i;
            GameObject buttonObject = new GameObject("Take Back Scroll " + Letter(i),
                typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(takeBackPanel.transform, false);
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(.5f, .5f);
            buttonRect.anchoredPosition = new Vector2(i % 2 == 0 ? -242f : 242f,
                i < 2 ? 91f : -82f);
            buttonRect.sizeDelta = new Vector2(462f, 150f);
            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(.14f, .31f, .46f);
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => TakeBackScroll(selectedScroll));
            takeBackButtons[i] = button;
            TMP_Text preview = TakeBackText("Label", buttonObject.transform, "SCROLL " + Letter(i),
                Vector2.zero, new Vector2(430f, 132f), 27f, Color.white);
            preview.enableAutoSizing = true;
            preview.fontSizeMin = 20f;
            preview.fontSizeMax = 27f;
        }

        GameObject closeObject = new GameObject("Close Take Back", typeof(RectTransform),
            typeof(Image), typeof(Button));
        closeObject.transform.SetParent(takeBackPanel.transform, false);
        RectTransform closeRect = closeObject.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(.5f, .5f);
        closeRect.anchoredPosition = new Vector2(0f, -275f);
        closeRect.sizeDelta = new Vector2(260f, 74f);
        Image closeImage = closeObject.GetComponent<Image>();
        closeImage.color = new Color(.35f, .24f, .20f);
        Button closeButton = closeObject.GetComponent<Button>();
        closeButton.targetGraphic = closeImage;
        closeButton.onClick.AddListener(CloseTakeBackPicker);
        TakeBackText("Label", closeObject.transform, "CANCEL", Vector2.zero,
            new Vector2(240f, 68f), 31f, Color.white);
        takeBackPanel.SetActive(false);
    }

    private void BuildPlacedScrollLabels()
    {
        for (int i = 0; i < placedScrollLabels.Length; i++)
        {
            GameObject labelObject = new GameObject("Placed Scroll Label " + Letter(i), typeof(TextMeshPro));
            labelObject.transform.SetParent(transform, false);
            TextMeshPro label = labelObject.GetComponent<TextMeshPro>();
            label.text = "SCROLL " + Letter(i);
            label.fontSize = 2.7f;
            label.enableAutoSizing = true;
            label.fontSizeMin = 1.7f;
            label.fontSizeMax = 2.7f;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(1.45f, .52f);
            label.color = new Color(1f, .88f, .58f);
            label.outlineColor = new Color(.08f, .05f, .03f);
            label.outlineWidth = .25f;
            labelObject.SetActive(false);
            placedScrollLabels[i] = label;
        }
    }

    private static TMP_Text TakeBackText(string name, Transform parent, string value,
        Vector2 position, Vector2 size, float fontSize, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = value;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    public void PlaceOnPedestal(int pedestalIndex)
    {
        if (Tutorial != null) { Tutorial.PlacePracticeScroll(pedestalIndex); return; }
        if (InputSuppressed) return;
        if (!started || finished || carriedScroll < 0 || pedestalIndex < 0 || pedestalIndex >= 2) return;
        int index = carriedScroll;
        placements[index] = pedestalIndex;
        carriedScroll = -1;
        Transform visual = carriedVisuals[index].transform;
        visual.SetParent(pedestals[pedestalIndex], false);
        placedScrollLabels[index].transform.SetParent(pedestals[pedestalIndex], false);
        placedScrollLabels[index].gameObject.SetActive(true);
        ArrangePlacedScrolls(pedestalIndex);
        RefreshVisuals();
    }

    public void RetrieveFromPedestal(int pedestalIndex)
    {
        if (Tutorial != null) { Tutorial.OpenPracticeTakeBack(); return; }
        if (InputSuppressed) return;
        if (!started || finished || carriedScroll >= 0 || pedestalIndex < 0 || pedestalIndex >= 2) return;
        if (CountOnPedestal(pedestalIndex) == 0) return;
        takeBackPedestal = pedestalIndex;
        takeBackTitle.text = "TAKE BACK FROM " + (pedestalIndex == 0 ? "FORMAL" : "INFORMAL");
        for (int i = 0; i < takeBackButtons.Length; i++)
        {
            takeBackButtons[i].gameObject.SetActive(placements[i] == pedestalIndex);
            if (i < Current.scrolls.Length)
                takeBackButtons[i].transform.Find("Label").GetComponent<TMP_Text>().text =
                    "SCROLL " + Letter(i) + "\n" + Preview(Current.scrolls[i].content, 115) +
                    "\nTAP TO TAKE BACK";
        }
        promptText.gameObject.SetActive(false);
        takeBackPanel.SetActive(true);
        LockGameplay();
    }

    public void TakeBackScroll(int index)
    {
        if (Tutorial != null) { Tutorial.TakeBackPracticeScroll(); return; }
        if (InputSuppressed) return;
        if (!takeBackPanel.activeSelf || !started || finished || carriedScroll >= 0 ||
            index < 0 || index >= placements.Length || placements[index] != takeBackPedestal) return;
        int pedestalIndex = takeBackPedestal;
        CloseTakeBackPicker();
        Carry(index);
        ArrangePlacedScrolls(pedestalIndex);
    }

    public void CloseTakeBackPicker()
    {
        if (Tutorial != null) { Tutorial.ClosePracticeTakeBack(); return; }
        takeBackPedestal = -1;
        takeBackPanel.SetActive(false);
        promptText.gameObject.SetActive(true);
        UnlockGameplay();
    }

    private void ArrangePlacedScrolls(int pedestalIndex)
    {
        int count = CountOnPedestal(pedestalIndex);
        int slot = 0;
        for (int i = 0; i < placements.Length; i++)
        {
            if (placements[i] != pedestalIndex) continue;
            Transform visual = carriedVisuals[i].transform;
            if (count == 1)
                visual.localPosition = new Vector3(0f, 1.74f, 0f);
            else if (count == 2)
                visual.localPosition = new Vector3(slot == 0 ? -.65f : .65f, 1.74f, 0f);
            else
                visual.localPosition = new Vector3(slot % 2 == 0 ? -.64f : .64f,
                    1.74f, slot < 2 ? -.43f : .43f);
            // The source mesh is long on Z, so turn it toward the approaching player.
            visual.localRotation = Quaternion.Euler(0f, 90f, 0f);
            visual.localScale = Vector3.one * (count == 1 ? 4f : count == 2 ? 2.3f : 2f);
            Transform label = placedScrollLabels[i].transform;
            float labelX = count == 1 ? 0f : slot % 2 == 0 ? -.85f : .85f;
            float labelY = visual.localPosition.y + (count > 2 && slot >= 2 ? 1.28f : .85f);
            label.localPosition = new Vector3(labelX, labelY, visual.localPosition.z - .35f);
            label.localRotation = Quaternion.identity;
            label.localScale = Vector3.one;
            slot++;
        }
    }

    private int CountOnPedestal(int pedestalIndex)
    {
        int count = 0;
        foreach (int placement in placements) if (placement == pedestalIndex) count++;
        return count;
    }

    public bool IsPedestalEmpty(int index) => index >= 0 && index < pedestals.Length && CountOnPedestal(index) == 0;

    public void Submit()
    {
        if (Tutorial != null) { Tutorial.ConfirmPracticeArrangement(); return; }
        if (InputSuppressed) return;
        if (!started || finished || flashing || transitioning) return;
        bool complete = Current.kind == ProofChallengeKind.SortScrolls;
        if (complete)
        {
            for (int i = 0; i < Current.scrolls.Length; i++) if (placements[i] < 0) complete = false;
            for (int i = Current.scrolls.Length; i < placements.Length; i++) if (placements[i] >= 0) complete = false;
        }
        else complete = worldBoard.IsComplete();
        if (!complete)
        {
            FailSubmission("INCOMPLETE ANSWER\n−10 seconds. Complete every required placement.");
            return;
        }
        bool correct = Current.kind == ProofChallengeKind.SortScrolls ? ValidateScrolls() : worldBoard.IsCorrect();
        if (correct) CompleteCurrentChallenge();
        else FailSubmission("INCORRECT ANSWER\n−10 seconds. Review your work and confirm again.");
    }

    private void FailSubmission(string message)
    {
        mistakeCount++;
        secondsLeft = Mathf.Max(0f, secondsLeft - wrongPenalty);
        boardText.text = message;
        TintPedestals(failure);
        SetScrollSourceState(ProofWorldPedestal.VisualState.Incorrect);
        if (Current.kind != ProofChallengeKind.SortScrolls) worldBoard.SetFeedback(false);
        if (audioSource != null && incorrectSound != null) audioSource.PlayOneShot(incorrectSound);
        if (secondsLeft <= 0f) Finish(false);
        else StartCoroutine(ClearIncorrectFlash());
        RefreshTimer();
    }

    public bool DebugClearCurrentChallenge()
    {
        if (!CanDebugClearChallenge) return false;
        // The existing Admin command clears the whole challenge, including remaining questions.
        correctCount += QuestionsPerChallenge - QuestionNumber;
        questionIndex = QuestionsPerChallenge - 1;
        CompleteCurrentChallenge();
        return true;
    }

    private void CompleteCurrentChallenge()
    {
        correctCount++;
        started = false;
        transitioning = true;
        boardText.text = (QuestionNumber == QuestionsPerChallenge ? "CHALLENGE COMPLETE" : "QUESTION COMPLETE") +
            "\n" + Current.title + " — Question " + QuestionNumber + " solved. The timer is paused.";
        TintPedestals(success);
        SetScrollSourceState(ProofWorldPedestal.VisualState.Correct);
        if (Current.kind != ProofChallengeKind.SortScrolls) worldBoard.SetFeedback(true);
        if (audioSource != null && correctSound != null) audioSource.PlayOneShot(correctSound);
        StartCoroutine(AdvanceChallenge());
    }

    private bool ValidateScrolls()
    {
        for (int i = 0; i < Current.scrolls.Length; i++)
            if ((placements[i] == 0) != Current.scrolls[i].isFormal) return false;
        return true;
    }

    private IEnumerator AdvanceChallenge()
    {
        yield return new WaitForSeconds(.85f);
        if (finished) yield break;
        for (int i = 0; i < scrolls.Length; i++)
        {
            scrolls[i].gameObject.SetActive(false);
            scrollSources[i].gameObject.SetActive(false);
            placedScrollLabels[i].gameObject.SetActive(false);
            carriedVisuals[i].SetActive(false);
            carriedVisuals[i].transform.SetParent(transform, false);
            placements[i] = -1;
        }
        carriedScroll = -1;
        readingScroll = -1;
        worldBoard.Hide();
        editBoardButton.gameObject.SetActive(false);
        questionIndex++;
        if (questionIndex >= QuestionsPerChallenge) { questionIndex = 0; challengeIndex++; }
        activeChallenge = null;
        transitioning = false;
        RestorePedestals();
        RefreshVisuals();
        if (challengeIndex >= catalog.challenges.Length) Finish(true);
        else boardText.text = "CHALLENGE " + ChallengeNumber + " — QUESTION " + QuestionNumber + " OF " + QuestionsPerChallenge +
            " READY\nActivate the Book Statue to continue. The timer is paused.";
    }

    private IEnumerator ClearIncorrectFlash()
    {
        flashing = true;
        yield return new WaitForSeconds(.7f);
        flashing = false;
        if (!finished)
        {
            ShowChallenge();
            RestorePedestals();
            RefreshScrollSources();
            worldBoard.ClearFeedback();
        }
    }

    private void RefreshVisuals()
    {
        if (placements == null) return;
        for (int i = 0; i < placements.Length; i++)
        {
            if (scrollStatus[i] != null)
                scrollStatus[i].text = carriedScroll == i ? "CARRIED" :
                    placements[i] == 0 ? "ON FORMAL" :
                    placements[i] == 1 ? "ON INFORMAL" : "READ AND CARRY";
        }
        if (pedestalCounts[0] != null) pedestalCounts[0].text = CountOnPedestal(0) + " SCROLL" + (CountOnPedestal(0) == 1 ? "" : "S");
        if (pedestalCounts[1] != null) pedestalCounts[1].text = CountOnPedestal(1) + " SCROLL" + (CountOnPedestal(1) == 1 ? "" : "S");
        if (!flashing && !transitioning) RefreshScrollSources();
        if (!finished && !flashing) RestorePedestals();
        RefreshTimer();
    }

    private void RefreshScrollSources()
    {
        for (int i = 0; i < scrollSources.Length; i++)
            if (scrollSources[i] != null && scrollSources[i].gameObject.activeSelf)
            {
                string status = carriedScroll == i ? "CARRIED" :
                    placements[i] == 0 ? "ON FORMAL ALTAR" :
                    placements[i] == 1 ? "ON INFORMAL ALTAR" :
                    Preview(Current.scrolls[i].content, 42);
                scrollSources[i].SetContent("SCROLL " + Letter(i), status);
                scrollSources[i].SetState(placements[i] >= 0 || carriedScroll == i ?
                    ProofWorldPedestal.VisualState.Selected : ProofWorldPedestal.VisualState.Normal);
            }
    }

    private void SetScrollSourceState(ProofWorldPedestal.VisualState state)
    {
        foreach (ProofWorldPedestal source in scrollSources)
            if (source != null && source.gameObject.activeSelf) source.SetState(state);
    }

    private void TintPedestals(Color color)
    {
        for (int i = 0; i < pedestalEmblems.Length; i++) SetEmblemColor(pedestalEmblems[i], color);
    }

    private void RestorePedestals()
    {
        if (pedestalEmblems.Length > 0) SetEmblemColor(pedestalEmblems[0], neutral);
        if (pedestalEmblems.Length > 1) SetEmblemColor(pedestalEmblems[1], informal);
    }

    private static void SetEmblemColor(Renderer emblem, Color color)
    {
        if (emblem == null) return;
        var block = new MaterialPropertyBlock();
        emblem.GetPropertyBlock(block);
        block.SetColor("_BaseColor", color);
        block.SetColor("_EmissionColor", color * 1.5f);
        emblem.SetPropertyBlock(block);
    }

    private void RefreshTimer()
    {
        if (timerText == null) return;
        int whole = Mathf.CeilToInt(secondsLeft);
        timerText.text = (whole / 60).ToString("00") + ":" + (whole % 60).ToString("00");
    }

    private void Finish(bool won)
    {
        if (finished) return;
        Presentation?.CloseForResult();
        finished = true;
        started = false;
        transitioning = false;
        if (readerPanel.activeSelf) CloseReader();
        if (takeBackPanel.activeSelf) CloseTakeBackPicker();
        worldBoard.Hide();
        foreach (ProofWorldPedestal source in scrollSources) source.gameObject.SetActive(false);
        foreach (TMP_Text label in placedScrollLabels) if (label != null) label.gameObject.SetActive(false);
        editBoardButton.gameObject.SetActive(false);
        boardText.text = won ?
            "STAGE COMPLETE\nYou completed all five Proofs Library challenges." :
            "TIME'S UP\nTry the Proofs Library again.";
        int seconds = Mathf.FloorToInt(activeGameplaySeconds);
        resultsText.text = won ? string.Format(
            "FORMAL VS. INFORMAL PROOFS STAGE COMPLETE!\nTime Played: {0:00}:{1:00}\nQuestions completed: {2}/10\nChallenges completed: 5/5\nMistakes: {3}",
            seconds / 60, seconds % 60, correctCount, mistakeCount) :
            "TIME'S UP\nChallenges completed: " + CompletedChallenges + "/5\nQuestions completed: " + correctCount + "/10\nMistakes: " + mistakeCount;
        resultsPanel.SetActive(true);
        if (won) StagePresentation.Completed(resultsPanel, 4);
        LockGameplay();
#if UNITY_EDITOR
        if (won && !SuppressProgressionForValidation) StageCompleteManager.UnlockStage(Mathf.Min(5, StageSelectionState.LastStage));
#else
        if (won) StageCompleteManager.UnlockStage(Mathf.Min(5, StageSelectionState.LastStage));
#endif
    }

    public void RetryStage()
    {
        UnlockGameplay();
        Time.timeScale = 1f;
        TeleportManager.Navigate(SceneManager.GetActiveScene().name);
    }

    public void ReturnToMenu()
    {
        UnlockGameplay();
        Time.timeScale = 1f;
        TeleportManager.Navigate("Main Menu");
    }
}
