using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One quiz interaction point for the six Truth_Table house doors.
/// </summary>
public class TruthTableDoorInteraction : MonoBehaviour
{
    private const string DoorPromptText = "INTERACT WITH DOOR";

    private static int[] sessionQuestionIndices;
    private static bool[] sessionAnswered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        sessionQuestionIndices = null;
        sessionAnswered = null;
    }

    public static void ClearSessionForRestart() { ResetSession(); }

    [SerializeField] private Collider[] doors;
    [SerializeField] private TruthTableDoorQuestionBank questionBank;
    [SerializeField, Min(0.5f)] private float interactionRange = 4.5f;
    [SerializeField, Range(80f, 130f)] private float openAngle = 105f;
    [SerializeField, Min(0.05f)] private float openSeconds = 0.5f;
    [SerializeField, Min(0.1f)] private float feedbackSeconds = 1.8f;
    [SerializeField] private GameObject interactionPrompt;
    [SerializeField] private Button promptButton;
    [SerializeField] private GameObject blankPanel;
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI questionText;
    [SerializeField] private Button[] answerButtons;
    [SerializeField] private TextMeshProUGUI[] answerTexts;
    [SerializeField] private TextMeshProUGUI feedbackText;
    [SerializeField] private TruthTableMinimap minimap;

    [Header("Truth Table Audio")]
    [SerializeField] private AudioClip backgroundMusic;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.35f;
    [SerializeField] private AudioClip correctAnswerSound;
    [SerializeField, Range(0f, 1f)] private float correctAnswerVolume = 0.7f;
    [SerializeField] private AudioClip incorrectAnswerSound;
    [SerializeField, Range(0f, 1f)] private float incorrectAnswerVolume = 0.7f;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource feedbackSource;

    private TruthTableStageClock stageClock;
    [SerializeField] private Transform bookStatue;
    private TextMeshPro bookStartIndicator;
    private float bookIndicatorHeight;
    private TMP_Text promptLabel;
    private string doorPromptText;
    private bool warnedAboutMissingBook;

    public bool IsStageRunning => stageClock != null && stageClock.IsRunning;
    public bool IsQuizOpen => panelOpen;
    public RectTransform UiHost => interactionPrompt != null ? interactionPrompt.transform.parent as RectTransform : null;
    public TMP_Text UiTextStyle => questionText;
    public RectTransform MinimapPanel => minimap != null ? minimap.Panel : null;

    private TruthTableDoorQuestionBank.Question[] assignedQuestions;
    private bool[] answered;
    private Transform[] hinges;
    private Quaternion[] closedRotations;
    private GameInput gameInput;
    private Player player;
    private Collider focusedDoor;
    private int currentDoorIndex = -1;
    private bool panelOpen;
    private bool answerSubmitted;
    private bool playerWasEnabled;
    private bool inputWasBlocked;
    private bool minimapWasEnabled;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;

    private bool IsTruthTableActive
    {
        get { return SceneManager.GetActiveScene().name == "PRELIM" && StageSelectionState.SelectedStage == 2; }
    }

    private void Awake()
    {
        if (!ValidateSetup())
        {
            enabled = false;
            return;
        }
        AssignQuestions();
        CreateDoorHinges();
        promptLabel = interactionPrompt != null ? interactionPrompt.GetComponentInChildren<TMP_Text>(true) : null;
        doorPromptText = DoorPromptText;
        if (promptLabel != null) promptLabel.text = doorPromptText;
        ConfigureAudio();
        stageClock = GetComponent<TruthTableStageClock>();
        if (stageClock == null) stageClock = gameObject.AddComponent<TruthTableStageClock>();
        if (bookStatue == null) FindBookStatue();
        else SetupBookStatue();
    }

    private void ConfigureAudio()
    {
        if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
        if (feedbackSource == null || feedbackSource == musicSource)
            feedbackSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.clip = backgroundMusic;
        musicSource.volume = musicVolume;
        feedbackSource.playOnAwake = false;
        feedbackSource.loop = false;
        feedbackSource.spatialBlend = 0f;
        feedbackSource.volume = 1f;
        AudioVolumeSettings.Route(musicSource, GameAudioChannel.Music);
        AudioVolumeSettings.Route(feedbackSource, GameAudioChannel.SoundFX);
    }

    private void StartMusic()
    {
        if (musicSource == null || backgroundMusic == null || musicSource.isPlaying) return;
        musicSource.clip = backgroundMusic;
        musicSource.volume = musicVolume;
        musicSource.Play();
    }

    private void StopMusic()
    {
        if (musicSource != null) musicSource.Stop();
    }

    public void PlayAnswerFeedback(bool correct)
    {
        AudioClip clip = correct ? correctAnswerSound : incorrectAnswerSound;
        if (feedbackSource != null && clip != null)
            feedbackSource.PlayOneShot(clip, correct ? correctAnswerVolume : incorrectAnswerVolume);
    }

    private bool ValidateSetup()
    {
        if (doors == null || doors.Length != 6 || questionBank == null ||
            questionBank.questions == null || questionBank.questions.Count <= doors.Length ||
            blankPanel == null || questionText == null || feedbackText == null ||
            answerButtons == null || answerButtons.Length != 4 ||
            answerTexts == null || answerTexts.Length != 4)
        {
            Debug.LogError("Truth_Table doors need six colliders, at least seven questions, and quiz UI references.", this);
            return false;
        }
        foreach (Collider door in doors) if (door == null) return false;
        foreach (TruthTableDoorQuestionBank.Question question in questionBank.questions)
        {
            if (question == null || string.IsNullOrEmpty(question.prompt) || question.choices == null ||
                question.choices.Length != 4 || question.correctChoice < 0 || question.correctChoice > 3)
            {
                Debug.LogError("A door question must have four choices and one valid answer index.", questionBank);
                return false;
            }
            foreach (string choice in question.choices)
                if (string.IsNullOrEmpty(choice)) return false;
        }
        return true;
    }

    private void AssignQuestions()
    {
        if (sessionQuestionIndices == null || sessionQuestionIndices.Length != doors.Length ||
            sessionAnswered == null || sessionAnswered.Length != doors.Length)
        {
            sessionQuestionIndices = new int[doors.Length];
            sessionAnswered = new bool[doors.Length];
            DealQuestions(null);
        }
        assignedQuestions = new TruthTableDoorQuestionBank.Question[doors.Length];
        answered = sessionAnswered;
        for (int i = 0; i < doors.Length; i++)
            assignedQuestions[i] = questionBank.questions[sessionQuestionIndices[i]];
    }

    private void DealQuestions(int[] previous)
    {
        List<int> available = new List<int>();
        for (int i = 0; i < questionBank.questions.Count; i++) available.Add(i);
        for (int doorIndex = 0; doorIndex < doors.Length; doorIndex++)
        {
            int excluded = previous == null ? -1 : previous[doorIndex];
            int eligible = available.Count - (available.Contains(excluded) ? 1 : 0);
            int selection = UnityEngine.Random.Range(0, eligible);
            int chosen = -1;
            foreach (int candidate in available)
            {
                if (candidate == excluded) continue;
                if (selection-- == 0)
                {
                    chosen = candidate;
                    break;
                }
            }
            sessionQuestionIndices[doorIndex] = chosen;
            available.Remove(chosen);
        }
    }

    // Shared reset for a submitted column or a rejected block placement.
    public void ResetAfterColumnEvaluation()
    {
        if (assignedQuestions == null || !IsStageRunning) return;
        ClosePanel();
        StopAllCoroutines();
        int[] previous = (int[])sessionQuestionIndices.Clone();
        DealQuestions(previous);
        for (int i = 0; i < doors.Length; i++)
        {
            answered[i] = false;
            assignedQuestions[i] = questionBank.questions[sessionQuestionIndices[i]];
            if (hinges[i] != null) hinges[i].localRotation = closedRotations[i];
            foreach (Collider collider in doors[i].GetComponents<Collider>()) collider.enabled = true;
        }
        focusedDoor = null;
        SetPromptVisible(false);
    }

    private void CreateDoorHinges()
    {
        hinges = new Transform[doors.Length];
        closedRotations = new Quaternion[doors.Length];
        for (int i = 0; i < doors.Length; i++)
        {
            Transform door = doors[i].transform;
            MeshFilter mesh = door.GetComponent<MeshFilter>();
            if (mesh == null || mesh.sharedMesh == null)
            {
                Debug.LogError("Door has no mesh for hinge placement: " + door.name, door);
                continue;
            }
            Bounds bounds = mesh.sharedMesh.bounds;
            Vector3 leftEdge = new Vector3(bounds.min.x, bounds.center.y, bounds.center.z);
            GameObject hinge = new GameObject(door.parent.name + " QuizDoorHinge");
            hinge.transform.SetParent(door.parent, false);
            hinge.transform.position = door.TransformPoint(leftEdge);
            hinge.transform.rotation = door.rotation;
            door.SetParent(hinge.transform, true);
            hinges[i] = hinge.transform;
            closedRotations[i] = hinge.transform.localRotation;
            if (answered[i])
            {
                foreach (Collider collider in door.GetComponents<Collider>()) collider.enabled = false;
                hinge.transform.localRotation = closedRotations[i] * Quaternion.Euler(0f, openAngle, 0f);
            }
        }
    }

    private void OnEnable()
    {
        if (interactionPrompt != null) interactionPrompt.SetActive(false);
        if (blankPanel != null) blankPanel.SetActive(false);
        if (promptButton != null) promptButton.onClick.AddListener(ActivateFocusedInteraction);
        if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);
        if (answerButtons != null && answerButtons.Length == 4)
        {
            answerButtons[0].onClick.AddListener(Answer0);
            answerButtons[1].onClick.AddListener(Answer1);
            answerButtons[2].onClick.AddListener(Answer2);
            answerButtons[3].onClick.AddListener(Answer3);
        }
        FindInput();
    }

    private void Update()
    {
        if (!IsTruthTableActive)
        {
            if (panelOpen) ClosePanel();
            StopMusic();
            if (bookStartIndicator != null) bookStartIndicator.gameObject.SetActive(false);
            SetPromptVisible(false);
            return;
        }

        if (gameInput == null) FindInput();
        if (player == null || !player.gameObject.activeInHierarchy)
        {
            player = Player.LocalInstance;
            if (player == null) player = FindFirstObjectByType<Player>();
        }

        if (stageClock == null || stageClock.IsGameOver || !stageClock.IsWaitingForBook && !stageClock.IsRunning)
        {
            focusedDoor = null;
            SetPromptVisible(false);
            return;
        }

        if (stageClock.IsWaitingForBook)
        {
            if (bookStatue == null) FindBookStatue();
            UpdateBookIndicator();
            if (promptLabel != null) promptLabel.text = "Interact to start";
            SetPromptVisible(IsPlayerNearBook());
            return;
        }

        if (bookStartIndicator != null) bookStartIndicator.gameObject.SetActive(false);

        if (promptLabel != null && promptLabel.text != doorPromptText) promptLabel.text = doorPromptText;

        if (panelOpen)
        {
            if (!answerSubmitted && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                ClosePanel();
            return;
        }

        focusedDoor = null;
        if (player != null && player.enabled && doors != null)
        {
            Vector3 playerCenter = player.transform.position + Vector3.up * 1.5f;
            float closestSquared = interactionRange * interactionRange;
            for (int i = 0; i < doors.Length; i++)
            {
                Collider door = doors[i];
                if (answered[i] || door == null || !door.enabled || !door.gameObject.activeInHierarchy) continue;
                float distanceSquared = (door.ClosestPoint(playerCenter) - playerCenter).sqrMagnitude;
                if (distanceSquared < closestSquared)
                {
                    focusedDoor = door;
                    closestSquared = distanceSquared;
                }
            }
        }
        SetPromptVisible(focusedDoor != null);
    }

    private void FindInput()
    {
        GameInput found = FindFirstObjectByType<GameInput>();
        if (found == gameInput) return;
        if (gameInput != null) gameInput.OnInteractAction -= OnInteract;
        gameInput = found;
        if (gameInput != null) gameInput.OnInteractAction += OnInteract;
    }

    private void OnInteract(object sender, EventArgs args)
    {
        ActivateFocusedInteraction();
    }

    private void FindBookStatue()
    {
        bookStatue = transform.root.Find("Book_Statue");
        if (bookStatue != null) SetupBookStatue();
        if (bookStatue == null && !warnedAboutMissingBook)
        {
            warnedAboutMissingBook = true;
            Debug.LogError("Truth_Table needs a saved child named Book_Statue to start the stage.", this);
        }
    }

    private void SetupBookStatue()
    {
        EnsureBookPedestalCollider();
        if (bookStartIndicator != null) return;

        var indicator = new GameObject("TruthTableChallengeStartIndicator", typeof(RectTransform),
            typeof(TextMeshPro));
        indicator.transform.SetParent(transform.root, true);
        bookStartIndicator = indicator.GetComponent<TextMeshPro>();
        if (questionText != null) bookStartIndicator.font = questionText.font;
        bookStartIndicator.alignment = TextAlignmentOptions.Center;
        bookStartIndicator.fontStyle = FontStyles.Bold;
        bookStartIndicator.color = new Color(1f, 0.93f, 0.7f);
        bookStartIndicator.fontSize = 5f;
        bookStartIndicator.enableAutoSizing = false;
        bookStartIndicator.textWrappingMode = TextWrappingModes.NoWrap;
        bookStartIndicator.outlineColor = new Color32(0, 0, 0, 255);
        bookStartIndicator.outlineWidth = 0.25f;
        bookStartIndicator.rectTransform.pivot = new Vector2(0.5f, 0f);
        bookStartIndicator.rectTransform.sizeDelta = new Vector2(44f, 12f);
        indicator.transform.localScale = Vector3.one * 0.48f;

        float top = bookStatue.position.y;
        foreach (Renderer renderer in bookStatue.GetComponentsInChildren<Renderer>(true))
            if (renderer.bounds.max.y > top) top = renderer.bounds.max.y;
        bookIndicatorHeight = top - bookStatue.position.y + 1.2f;
        UpdateBookIndicator();
    }

    private void EnsureBookPedestalCollider()
    {
        foreach (Collider existing in bookStatue.GetComponentsInChildren<Collider>(true))
            if (existing.enabled && !existing.isTrigger) return;

        Bounds bounds = new Bounds();
        bool hasBounds = false;
        foreach (MeshFilter filter in bookStatue.GetComponentsInChildren<MeshFilter>(true))
        {
            string part = filter.name.ToLowerInvariant();
            if (filter.sharedMesh == null ||
                !(part.Contains("foundation") || part.Contains("step") ||
                  part.Contains("pedestal") || part.Contains("pillar") || part.Contains("capital")))
                continue;

            Bounds meshBounds = filter.sharedMesh.bounds;
            foreach (float x in new[] { meshBounds.min.x, meshBounds.max.x })
            foreach (float y in new[] { meshBounds.min.y, meshBounds.max.y })
            foreach (float z in new[] { meshBounds.min.z, meshBounds.max.z })
            {
                Vector3 localPoint = bookStatue.InverseTransformPoint(
                    filter.transform.TransformPoint(new Vector3(x, y, z)));
                if (!hasBounds) { bounds = new Bounds(localPoint, Vector3.zero); hasBounds = true; }
                else bounds.Encapsulate(localPoint);
            }
        }
        if (!hasBounds)
        {
            Debug.LogError("Book_Statue pedestal meshes were not found for collision.", bookStatue);
            return;
        }
        BoxCollider collider = bookStatue.gameObject.AddComponent<BoxCollider>();
        collider.center = bounds.center;
        collider.size = bounds.size;
        collider.isTrigger = false;
    }

    private void UpdateBookIndicator()
    {
        if (bookStartIndicator == null || bookStatue == null || stageClock == null) return;
        bool visible = IsTruthTableActive && stageClock.IsWaitingForBook;
        if (bookStartIndicator.gameObject.activeSelf != visible)
            bookStartIndicator.gameObject.SetActive(visible);
        if (!visible) return;
        string message = "Interact to start\nChallenge " + stageClock.CurrentChallengeNumber;
        if (bookStartIndicator.text != message) bookStartIndicator.text = message;
        bookStartIndicator.transform.position = bookStatue.position + Vector3.up * bookIndicatorHeight;
        Camera view = Camera.main;
        if (view != null) bookStartIndicator.transform.rotation = view.transform.rotation;
    }

    private bool IsPlayerNearBook()
    {
        return bookStatue != null && bookStatue.gameObject.activeInHierarchy &&
               player != null && player.enabled &&
               (player.transform.position - bookStatue.position).sqrMagnitude <=
               interactionRange * interactionRange;
    }

    public void ActivateFocusedInteraction()
    {
        if (!IsTruthTableActive || stageClock == null || stageClock.IsTutorialOpen) return;
        if (stageClock.IsWaitingForBook)
        {
            if (!IsPlayerNearBook()) return;
            if (!stageClock.StartFromBook()) return;
            StartMusic();
            UpdateBookIndicator();
            foreach (TruthBlockSpawner spawner in transform.root.GetComponentsInChildren<TruthBlockSpawner>(true))
                if (spawner.gameObject.activeInHierarchy) spawner.RerollActiveBlocks();
            SetPromptVisible(false);
            return;
        }
        OpenFocusedDoor();
    }

    public void OpenFocusedDoor()
    {
        if (!IsTruthTableActive || !IsStageRunning || stageClock.IsTutorialOpen || panelOpen || focusedDoor == null || player == null ||
            blankPanel == null || gameInput == null) return;

        int index = Array.IndexOf(doors, focusedDoor);
        if (index < 0 || answered[index]) return;

        // Check again at activation time so a stale prompt cannot open a distant door.
        Vector3 playerCenter = player.transform.position + Vector3.up * 1.5f;
        if ((focusedDoor.ClosestPoint(playerCenter) - playerCenter).sqrMagnitude >
            interactionRange * interactionRange) return;

        currentDoorIndex = index;
        answerSubmitted = false;
        TruthTableDoorQuestionBank.Question question = assignedQuestions[index];
        questionText.text = question.prompt;
        feedbackText.text = string.Empty;
        for (int i = 0; i < 4; i++)
        {
            answerTexts[i].text = ((char)('A' + i)) + ".  " + question.choices[i];
            answerButtons[i].interactable = true;
        }
        if (closeButton != null) closeButton.interactable = true;

        panelOpen = true;
        stageClock.SetQuizOpen(true);
        playerWasEnabled = player.enabled;
        inputWasBlocked = gameInput.GameplayInputBlocked;
        if (minimap != null)
        {
            minimapWasEnabled = minimap.enabled;
            minimap.enabled = false;
        }
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        gameInput.SetGameplayInputBlocked(true);
        player.ToggleControl(false);
        SetPromptVisible(false);
        blankPanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Answer0() { SubmitAnswer(0); }
    private void Answer1() { SubmitAnswer(1); }
    private void Answer2() { SubmitAnswer(2); }
    private void Answer3() { SubmitAnswer(3); }

    public void SubmitAnswer(int choiceIndex)
    {
        if (!IsStageRunning || !panelOpen || answerSubmitted || currentDoorIndex < 0 || choiceIndex < 0 || choiceIndex > 3) return;
        answerSubmitted = true;
        int index = currentDoorIndex;
        answered[index] = true;
        bool correct = choiceIndex == assignedQuestions[index].correctChoice;
        feedbackText.text = correct ? "correct" : "wrong";
        feedbackText.color = correct ? new Color(0.25f, 0.95f, 0.35f) : new Color(1f, 0.27f, 0.27f);
        PlayAnswerFeedback(correct);
        foreach (Button button in answerButtons) button.interactable = false;
        if (closeButton != null) closeButton.interactable = false;
        if (!correct) stageClock.AdjustSeconds(-10);
        if (!IsStageRunning) return;
        OpenDoor(index);
        StartCoroutine(CloseAfterFeedback());
    }

    private void OpenDoor(int index)
    {
        // HOUSE (2) also has a MeshCollider, so disable every collider on the door.
        foreach (Collider collider in doors[index].GetComponents<Collider>()) collider.enabled = false;
        if (hinges[index] != null) StartCoroutine(SwingDoor(index));
    }

    private IEnumerator SwingDoor(int index)
    {
        Transform hinge = hinges[index];
        Quaternion start = closedRotations[index];
        Quaternion end = start * Quaternion.Euler(0f, openAngle, 0f);
        float elapsed = 0f;
        while (elapsed < openSeconds)
        {
            elapsed += Time.deltaTime;
            hinge.localRotation = Quaternion.Slerp(start, end, Mathf.Clamp01(elapsed / openSeconds));
            yield return null;
        }
        hinge.localRotation = end;
    }

    private IEnumerator CloseAfterFeedback()
    {
        yield return new WaitForSecondsRealtime(feedbackSeconds);
        ClosePanel();
    }

    public void ClosePanel()
    {
        if (!panelOpen) return;
        panelOpen = false;
        if (stageClock != null) stageClock.SetQuizOpen(false);
        if (blankPanel != null) blankPanel.SetActive(false);
        if (stageClock == null || !stageClock.IsGameOver)
        {
            if (gameInput != null) gameInput.SetGameplayInputBlocked(inputWasBlocked);
            if (player != null) player.ToggleControl(playerWasEnabled);
        }
        if (minimap != null) minimap.enabled = minimapWasEnabled;
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
        currentDoorIndex = -1;
        focusedDoor = null;
    }

    public void ApplyIncorrectColumnPenalty()
    {
        if (IsStageRunning) stageClock.AdjustSeconds(-20);
    }

    public void CompleteStage()
    {
        if (IsStageRunning) stageClock.CompleteStage();
        StopMusic();
        SetPromptVisible(false);
    }

    public void PrepareNextChallenge()
    {
        if (!IsStageRunning) return;
        ResetAfterColumnEvaluation();
        stageClock.PrepareNextChallenge();
        StopMusic();
        UpdateBookIndicator();
        focusedDoor = null;
        SetPromptVisible(false);
    }

    public void EndForGameOver()
    {
        StopAllCoroutines();
        StopMusic();
        ClosePanel();
        SetPromptVisible(false);
        focusedDoor = null;
        if (gameInput != null) gameInput.SetGameplayInputBlocked(true);
        if (player != null) player.ToggleControl(false);
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.SetInventoryVisibility(false);
    }

    private void SetPromptVisible(bool visible)
    {
        if (promptButton != null && promptButton.interactable != visible)
            promptButton.interactable = visible;
        if (interactionPrompt != null && interactionPrompt.activeSelf != visible)
            interactionPrompt.SetActive(visible);
    }

    private void OnDisable()
    {
        StopMusic();
        if (feedbackSource != null) feedbackSource.Stop();
        if (bookStartIndicator != null) bookStartIndicator.gameObject.SetActive(false);
        ClosePanel();
        SetPromptVisible(false);
        if (gameInput != null) gameInput.OnInteractAction -= OnInteract;
        // Force FindInput to subscribe again when this stage is re-enabled.
        gameInput = null;
        if (promptButton != null) promptButton.onClick.RemoveListener(ActivateFocusedInteraction);
        if (closeButton != null) closeButton.onClick.RemoveListener(ClosePanel);
        if (answerButtons != null && answerButtons.Length == 4)
        {
            answerButtons[0].onClick.RemoveListener(Answer0);
            answerButtons[1].onClick.RemoveListener(Answer1);
            answerButtons[2].onClick.RemoveListener(Answer2);
            answerButtons[3].onClick.RemoveListener(Answer3);
        }
    }

    private void OnDestroy()
    {
        if (bookStartIndicator != null) Destroy(bookStartIndicator.gameObject);
    }
}
