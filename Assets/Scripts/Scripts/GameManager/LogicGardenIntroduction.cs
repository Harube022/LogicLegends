using System.Collections;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;
using System.Collections.Generic;

/// <summary>Scene-local introduction. The library portal remains locked until the last line is acknowledged.</summary>
public sealed partial class LogicGardenIntroduction : MonoBehaviour
{
    [SerializeField] private Transform lefyahj;
    [SerializeField] private InteractPortal libraryDoor;
    [SerializeField] private TextMeshProUGUI objectivesText;
    [SerializeField] private DialogueManager dialogue;
    [SerializeField] private TextMeshProUGUI speakerText;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button contextualButton;
    [SerializeField] private TextMeshProUGUI contextualButtonText;
    [SerializeField] private TextMeshProUGUI locationTitle;
    [SerializeField] private Button returnToGardenButton;
    [SerializeField] private Transform gardenEntry;
    [SerializeField] private StudyLibraryController studyLibrary;
    [SerializeField] private InteractPortal[] otherLibraryEntrances;
    [SerializeField] private TextMeshProUGUI wizardNotification;
    [SerializeField] private GameObject wizardAttentionMarker;
    [SerializeField] private Shader attentionOverlayShader;
    [SerializeField] private CinemachineCamera libraryDialogueCamera;
    [SerializeField, Min(0.1f)] private float guidedCameraBlendSeconds = 1.2f;
    [SerializeField, Min(1f)] private float interactionRange = 4f;
    [SerializeField, Range(0f, 1f)] private float cameraFacingThreshold = 0.45f;
    [SerializeField] private LogicGardenControlsOnboarding onboarding;
    public LogicGardenControlsOnboarding Onboarding => onboarding;
    public bool OnboardingBusy => (onboarding != null && (onboarding.IsActive || onboarding.IsPending)) || (libraryTutorial!=null&&libraryTutorial.IsBusy);
    public bool Talking => talking;
    public bool InLibrary => inLibrary;
    public Transform Wizard => lefyahj;

    private static readonly string[] Lines =
    {
        "Welcome to LogicGarden. I’m Lefyahj, your guide.",
        "This is a safe realm where you can prepare for your journey through Discrete Mathematics.",
        "The Logic Library holds the topics you’ll explore, from prelims through finals.",
        "Visit the library to learn about these topics and prepare for the challenges ahead.",
        "The entrance is now open. Head to the Logic Library when you’re ready."
    };
    private static readonly string[] LibraryLines =
    {
        "Welcome to the Logic Library.",
        "Here, you can explore Discrete Mathematics topics from prelims through finals.",
        "Approach a book and select Read to review its lesson.",
        "Take your time learning. You can return to LogicGarden whenever you’re ready."
    };

    private GameInput input;
    private Player player;
    private bool talking;
    private bool conversationComplete;
    private bool libraryReached;
    private bool previousInputBlocked;
    private int line;
    private bool teleportPending;
    private bool inLibrary;
    private bool libraryDialogue;
    private CinemachineBrain cameraBrain;
    private CinemachineBlendDefinition previousBlend;
    private readonly List<Behaviour> pausedCameraInputs = new List<Behaviour>();
    private Material attentionMaterial;

    private void Start()
    {
        LogicGardenProgress.Load();
        conversationComplete = LogicGardenProgress.ConversationCompleted;
        libraryReached = LogicGardenProgress.LibraryVisited;
        input = GameInput.Instance != null ? GameInput.Instance : FindFirstObjectByType<GameInput>();
        if (input != null) input.OnInteractAction += OnInteract;
        if (nextButton != null)
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(Next);
        }
        if (contextualButton != null)
        {
            contextualButton.onClick.RemoveAllListeners();
            contextualButton.onClick.AddListener(Interact);
            contextualButton.gameObject.SetActive(false);
        }
        if (returnToGardenButton != null)
        {
            returnToGardenButton.onClick.RemoveAllListeners();
            returnToGardenButton.onClick.AddListener(ReturnToGarden);
            returnToGardenButton.gameObject.SetActive(false);
        }
        if (libraryDoor != null) libraryDoor.SetInteractionUnlocked(conversationComplete);
        if (otherLibraryEntrances != null)
            foreach (InteractPortal portal in otherLibraryEntrances)
                if (portal != null)
                {
                    portal.SetInteractionUnlocked(conversationComplete);
                    portal.onTeleport.RemoveListener(OnOtherLibraryEntranceUsed);
                    portal.onTeleport.AddListener(OnOtherLibraryEntranceUsed);
                }
        if (libraryDialogueCamera != null) libraryDialogueCamera.gameObject.SetActive(false);
        if(wizardAttentionMarker!=null&&attentionOverlayShader!=null)
        {
            var marker=wizardAttentionMarker.GetComponent<TMP_Text>();
            attentionMaterial=new Material(marker.fontSharedMaterial);attentionMaterial.shader=attentionOverlayShader;
            marker.fontSharedMaterial=attentionMaterial;marker.fontSize=11;
        }
        UpdateWizardAttention();
        UpdateObjectives();
        if (locationTitle != null)
        {
            locationTitle.text = "LogicGarden";
            locationTitle.gameObject.SetActive(true);
        }
        if (onboarding == null) onboarding = GetComponent<LogicGardenControlsOnboarding>();
        if (onboarding == null) onboarding = gameObject.AddComponent<LogicGardenControlsOnboarding>();
        onboarding.Initialize(this, locationTitle.transform.parent as RectTransform);
        if(libraryTutorial==null)libraryTutorial=GetComponent<LogicLibraryTutorialController>();
        if(libraryTutorial==null)libraryTutorial=gameObject.AddComponent<LogicLibraryTutorialController>();
        libraryTutorial.Initialize(this,studyLibrary);
        UpdateWizardAttention();
    }

    private void OnDisable()
    {
        if (input != null) input.OnInteractAction -= OnInteract;
        if(libraryTutorial!=null)libraryTutorial.Cancel(false);
        if (talking) CloseDialogue(false);
        RestoreGameplayCamera();
        if (otherLibraryEntrances != null)
            foreach (InteractPortal portal in otherLibraryEntrances)
                if (portal != null) portal.onTeleport.RemoveListener(OnOtherLibraryEntranceUsed);
        if (contextualButton != null) contextualButton.gameObject.SetActive(false);
    }
    private void OnEnable()
    {
        if(input!=null){input.OnInteractAction-=OnInteract;input.OnInteractAction+=OnInteract;}
        if(otherLibraryEntrances!=null)
            foreach(var portal in otherLibraryEntrances)if(portal!=null){portal.onTeleport.RemoveListener(OnOtherLibraryEntranceUsed);portal.onTeleport.AddListener(OnOtherLibraryEntranceUsed);}
    }
    private void OnDestroy(){if(attentionMaterial!=null)Destroy(attentionMaterial);}

    private void Update()
    {
        if (OnboardingBusy || talking || teleportPending || inLibrary || (input != null && input.GameplayInputBlocked))
        {
            if (contextualButton != null) contextualButton.gameObject.SetActive(false);
            return;
        }
        if (player == null) player = FindLocalPlayer();
        bool wizardTarget = IsTargeted(lefyahj);
        bool doorTarget = conversationComplete && libraryDoor != null && IsTargeted(libraryDoor.transform);
        if (contextualButton != null)
        {
            contextualButton.gameObject.SetActive(wizardTarget || doorTarget);
            if (contextualButtonText != null)
                contextualButtonText.text = wizardTarget ? "Talk with Lefyahj" : "Enter Logic Library";
        }
    }

    private Player FindLocalPlayer()
    {
        foreach (Player candidate in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            if (!candidate.gameObject.activeInHierarchy) continue;
            PhotonView view = candidate.GetComponent<PhotonView>();
            if (view == null || view.IsMine) return candidate;
        }
        return null;
    }

    private bool IsTargeted(Transform target)
    {
        if (target == null || player == null || Camera.main == null) return false;
        Vector3 aim = target.position + Vector3.up * (target == lefyahj ? 2.8f : 1.5f);
        if (Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z),
            new Vector2(target.position.x, target.position.z)) > interactionRange) return false;
        Vector3 direction = (aim - Camera.main.transform.position).normalized;
        return Vector3.Dot(Camera.main.transform.forward, direction) >= cameraFacingThreshold;
    }

    private void OnInteract(object sender, System.EventArgs args) => Interact();

    public void Interact()
    {
        if (OnboardingBusy || talking || teleportPending || inLibrary || (input != null && input.GameplayInputBlocked)) return;
        if (player == null) player = FindLocalPlayer();
        if (IsTargeted(lefyahj))
        {
            BeginDialogue();
            return;
        }
        if (conversationComplete && libraryDoor != null && IsTargeted(libraryDoor.transform) &&
            libraryDoor.TryInteract(player.gameObject))
        {
            teleportPending = true;
            StartCoroutine(CompleteLibraryObjectiveAfterTeleport());
        }
    }

    private void BeginDialogue()
    {
        if (dialogue == null || dialogue.dialoguePanel == null || nextButton == null) return;
        libraryDialogue = false;
        OpenDialogue(Lines);
    }

    private void BeginLibraryDialogue()
    {
        if (dialogue == null || dialogue.dialoguePanel == null || nextButton == null) return;
        libraryDialogue = true;
        OpenDialogue(LibraryLines);
    }

    private void OpenDialogue(string[] lines)
    {
        talking = true;
        UpdateWizardAttention();
        line = 0;
        previousInputBlocked = input != null && input.GameplayInputBlocked;
        if (input != null) input.SetGameplayInputBlocked(true);
        if (player != null) player.ToggleControl(false);
        if (speakerText != null) speakerText.text = "Lefyahj";
        if (returnToGardenButton != null) returnToGardenButton.gameObject.SetActive(false);
        dialogue.ShowDialoguePanel(lines[line]);
    }

    public void Next()
    {
        if(libraryTutorial!=null&&libraryTutorial.TourActive){libraryTutorial.Next();return;}
        if (!talking) return;
        line++;
        string[] lines = libraryDialogue ? LibraryLines : Lines;
        if (line < lines.Length)
        {
            dialogue.UpdateText(lines[line]);
            if (!libraryDialogue && line == 3) ShowLibraryCamera();
            return;
        }
        CloseDialogue(true);
    }

    private void CloseDialogue(bool acknowledgedFinalLine)
    {
        talking = false;
        RestoreGameplayCamera();
        if (dialogue != null) dialogue.HideDialoguePanel();
        if (input != null) input.SetGameplayInputBlocked(previousInputBlocked);
        if (player != null && !previousInputBlocked) player.ToggleControl(true);
        if (returnToGardenButton != null) returnToGardenButton.gameObject.SetActive(inLibrary);
        if (acknowledgedFinalLine && libraryDialogue)
        {
            if(replayDialogue){FinishReplayDialogue(acknowledgedFinalLine);return;}
            LogicGardenProgress.SeeIntroduction();
            libraryTutorial.BeginTour(true);
            return;
        }
        if(replayDialogue){FinishReplayDialogue(acknowledgedFinalLine);return;}
        if (acknowledgedFinalLine && !conversationComplete)
        {
            conversationComplete = true;
            LogicGardenProgress.CompleteConversation();
            if (libraryDoor != null) libraryDoor.SetInteractionUnlocked(true);
            if (otherLibraryEntrances != null)
                foreach (InteractPortal portal in otherLibraryEntrances)
                    if (portal != null) portal.SetInteractionUnlocked(true);
            UpdateObjectives();
        }
        UpdateWizardAttention();
    }

    private void UpdateWizardAttention()
    {
        bool incomplete = !conversationComplete && !inLibrary;
        if (wizardAttentionMarker != null) wizardAttentionMarker.SetActive(incomplete);
        if (wizardNotification != null) wizardNotification.gameObject.SetActive(incomplete && !talking && !OnboardingBusy);
    }
    public void RefreshAfterControlsTutorial(){UpdateWizardAttention();UpdateObjectives();}

    private void ShowLibraryCamera()
    {
        if (libraryDialogueCamera == null || Camera.main == null) return;
        cameraBrain = Camera.main.GetComponent<CinemachineBrain>();
        if (cameraBrain != null)
        {
            previousBlend = cameraBrain.DefaultBlend;
            cameraBrain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.EaseInOut, guidedCameraBlendSeconds);
        }
        foreach (CinemachineInputAxisController controller in
                 FindObjectsByType<CinemachineInputAxisController>(FindObjectsSortMode.None))
            if (controller.enabled) { controller.enabled = false; pausedCameraInputs.Add(controller); }
        foreach (CinemachinePinchZoom zoom in
                 FindObjectsByType<CinemachinePinchZoom>(FindObjectsSortMode.None))
            if (zoom.enabled) { zoom.enabled = false; pausedCameraInputs.Add(zoom); }
        libraryDialogueCamera.gameObject.SetActive(true);
    }

    private void RestoreGameplayCamera()
    {
        if (libraryDialogueCamera != null) libraryDialogueCamera.gameObject.SetActive(false);
        foreach (Behaviour control in pausedCameraInputs)
            if (control != null) control.enabled = true;
        pausedCameraInputs.Clear();
        if (cameraBrain != null) cameraBrain.DefaultBlend = previousBlend;
        cameraBrain = null;
    }

    private void OnOtherLibraryEntranceUsed()
    {
        if (!conversationComplete || teleportPending || inLibrary) return;
        if (player == null) player = FindLocalPlayer();
        if (player == null) return;
        teleportPending = true;
        StartCoroutine(CompleteLibraryObjectiveAfterTeleport());
    }

    private IEnumerator CompleteLibraryObjectiveAfterTeleport()
    {
        Transform destination = libraryDoor.TeleportDestination;
        float deadline = Time.realtimeSinceStartup + 8f;
        while (player != null && destination != null && Time.realtimeSinceStartup < deadline)
        {
            // CharacterController can settle below the destination Y after teleport.
            bool arrived = TeleportManager.Instance != null && !TeleportManager.Instance.IsTeleporting &&
                Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z),
                    new Vector2(destination.position.x, destination.position.z)) < 25f;
            if (arrived)
            {
                libraryReached = true;
                LogicGardenProgress.VisitLibrary();
                inLibrary = true;
                teleportPending = false;
                if (locationTitle != null) locationTitle.text = "Logic Library";
                if (returnToGardenButton != null) returnToGardenButton.gameObject.SetActive(true);
                UpdateWizardAttention();
                UpdateObjectives();
                if (!LogicGardenProgress.IntroductionSeen) BeginLibraryDialogue();
                else if(!LogicGardenProgress.LibraryTourCompleted)libraryTutorial.BeginTour(true);
                yield break;
            }
            yield return null;
        }
        teleportPending = false;
    }

    public void ReturnToGarden()
    {
        if (OnboardingBusy || !inLibrary || talking || teleportPending || player == null || gardenEntry == null ||
            TeleportManager.Instance == null || TeleportManager.Instance.IsTeleporting) return;
        if (studyLibrary != null) studyLibrary.ExitActiveShelf();
        teleportPending = true;
        if (returnToGardenButton != null) returnToGardenButton.gameObject.SetActive(false);
        TeleportManager.Instance.StartTeleport(player.gameObject, gardenEntry);
        StartCoroutine(FinishReturnToGarden());
    }

    private IEnumerator FinishReturnToGarden()
    {
        float deadline = Time.realtimeSinceStartup + 8f;
        while (player != null && Time.realtimeSinceStartup < deadline)
        {
            if (!TeleportManager.Instance.IsTeleporting && Vector3.Distance(player.transform.position, gardenEntry.position) < 3f)
            {
                inLibrary = false;
                teleportPending = false;
                if (locationTitle != null) locationTitle.text = "LogicGarden";
                yield break;
            }
            yield return null;
        }
        teleportPending = false;
        if (returnToGardenButton != null) returnToGardenButton.gameObject.SetActive(inLibrary);
    }

    private void UpdateObjectives()
    {
        if (objectivesText == null) return;
        objectivesText.text = conversationComplete
            ? "<color=#267C39><s>Talk with Lefyahj.</s></color>\n" +
              (libraryReached ? "<color=#267C39><s>Go to Logic Library.</s></color>" : "Go to Logic Library.")
            : "Talk with Lefyahj.";
    }
}
