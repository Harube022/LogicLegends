using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

public enum FunctionsRelationsTutorialStep { Movement, Jump, Interaction, Book, Timer, ViewBoard, Board, BoardBack, Domain, Input, Output, OutputChoice, Connection, Undo, Submit, Confirm }

/// <summary>Isolated practice using the real UI callbacks, without loading or grading a challenge.</summary>
[DefaultExecutionOrder(1300)]
[DisallowMultipleComponent]
public sealed partial class FunctionsRelationsControlsOnboarding : MonoBehaviour
{
    public static FunctionsRelationsControlsOnboarding Active { get; private set; }
    public bool IsActive { get; private set; }
    public FunctionsRelationsTutorialStep CurrentStep { get; private set; }
    public FunctionsRelationsStageManager Stage { get; private set; }
    public bool AllowsJump => IsActive && CurrentStep == FunctionsRelationsTutorialStep.Jump;
    public GameObject PracticeObject => practice;
    public string PracticeInput { get; private set; }
    public string PracticeOutput { get; private set; }
    public Button YesButton { get; private set; }
    public Button NoButton { get; private set; }
    [Range(.15f, .8f)] public float joystickDeadZone = .3f;
    [Min(.1f)] public float movementDistance = 1.5f;
    [Min(.1f)] public float intentionalMovementSeconds = .7f;
    private readonly string[] practiceInputs = { "6", "9" }, practiceOutputs = { "X", "Y" };
    private FunctionsRelationsHelpUI help;
    private FunctionsRelationsPresentation ui;
    private GameInput input;
    private Player player;
    private MobileInputUI mobile;
    private RectTransform safe, joystick, jump, hand, timer, highlight, arrow, instructions, worldRect, practiceLabel;
    private TMP_Text instruction;
    private Button next, settings;
    private GameObject confirmation, practice, practiceRoot;
    private Material practiceMaterial;
    private LineRenderer practiceBeam;
    private Transform worldTarget;
    private PrelimTutorialSkipControl skip;
    private Vector3 movementStart, initialPosition;
    private Quaternion initialRotation;
    private float intentionalTime, jumpY, originalBoardPadding;
    private bool jumpSucceeded, playerEnabled, inputBlocked, settingsEnabled, cursorVisible;
    private CursorLockMode cursorLock;
    private readonly Dictionary<GameObject, bool> visible = new Dictionary<GameObject, bool>();
    private readonly Dictionary<TMP_Text, string> texts = new Dictionary<TMP_Text, string>();
    private readonly Dictionary<Button, bool> clickable = new Dictionary<Button, bool>();
    private readonly List<Behaviour> cameraInputs = new List<Behaviour>();
    private readonly List<RectTransform> nodeLabels = new List<RectTransform>();

    public void Begin(FunctionsRelationsStageManager owner, FunctionsRelationsHelpUI guide)
    {
        if (IsActive || owner == null || !owner.OnboardingPending || Active != null) return;
        Stage = owner; help = guide;
        ui = GetComponent<FunctionsRelationsPresentation>();
        player = Player.LocalInstance; input = GameInput.Instance;
        var controls = GameObject.Find("Canvas 1");
        if (ui == null || player == null || input == null || controls == null || owner.BoardView == null) return;
        safe = ui.safeArea;
        mobile = MobileInputUI.FindJoystick();
        foreach (var rect in controls.GetComponentsInChildren<RectTransform>(true))
        {
            if (rect.name == "Joystick") joystick = rect;
            if (rect.name == "JumpButton") jump = rect;
            if (rect.name == "InteractButton") hand = rect;
            if (rect.name == "SettingsButton") settings = rect.GetComponent<Button>();
        }
        timer = ui.hud.Find("StageCountdown") as RectTransform;
        if (mobile == null || joystick == null || jump == null || hand == null || timer == null) return;
        visible.Clear(); texts.Clear(); clickable.Clear();
        foreach (var obj in new[] { joystick.gameObject, jump.gameObject, hand.gameObject, ui.hud.gameObject,
            ui.status.gameObject, ui.interaction.gameObject, ui.viewBoard.gameObject, ui.submit.gameObject,
            ui.domainPanel.gameObject, ui.rangePanel.gameObject, ui.boardBack.gameObject, help.HelpButton.gameObject,
            settings != null ? settings.gameObject : null }) Remember(obj);
        foreach (var button in Stage.DomainButtons) RememberButton(button);
        foreach (var button in Stage.RangeButtons) RememberButton(button);
        RememberButton(Stage.UndoButton);
        foreach (var text in Stage.WorldBoardLabels) RememberText(text);
        RememberText(ui.domainHint); RememberText(ui.rangeHint);
        RememberText(Stage.InteractionButton.GetComponentInChildren<TMP_Text>());
        playerEnabled = player.enabled; inputBlocked = input.GameplayInputBlocked;
        initialPosition = player.transform.position; initialRotation = player.transform.rotation;
        settingsEnabled = settings != null && settings.interactable;
        cursorLock = Cursor.lockState; cursorVisible = Cursor.visible;
        originalBoardPadding = Stage.BoardView.framingPadding;
        cameraInputs.Clear();
        foreach (var b in FindObjectsByType<Behaviour>(FindObjectsSortMode.None))
            if (b.enabled && (b is CinemachineInputAxisController || b is CinemachinePinchZoom || b is ThirdPersonCameraController))
            { cameraInputs.Add(b); b.enabled = false; }
        IsActive = true; Active = this;
        player.OnJumpStarted += SuccessfulJump;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        if (settings != null) settings.interactable = false;
        BuildTutorialUI();
        skip = PrelimTutorialSkipControl.Create(safe, SkipTutorial);
        skip.Reserve(ui.hud, ui.boardBack, (RectTransform)help.HelpButton.transform, settings != null ? (RectTransform)settings.transform : null);
        BeginMovement();
    }

    private void Remember(GameObject obj) { if (obj != null && !visible.ContainsKey(obj)) visible.Add(obj, obj.activeSelf); }
    private void RememberText(TMP_Text text) { if (text != null && !texts.ContainsKey(text)) texts.Add(text, text.text); }
    private void RememberButton(Button button)
    {
        if (button == null) return;
        Remember(button.gameObject); RememberText(button.GetComponentInChildren<TMP_Text>(true));
        if (!clickable.ContainsKey(button)) clickable.Add(button, button.interactable);
    }
    private void SuccessfulJump() { if (AllowsJump) { jumpSucceeded = true; jumpY = player.transform.position.y; } }
    public bool TryGetMovementOverride(out Vector2 movement)
    {
        movement = Vector2.zero;
        if (!IsActive) return false;
        if (CurrentStep == FunctionsRelationsTutorialStep.Movement && mobile.CurrentInput.sqrMagnitude > joystickDeadZone * joystickDeadZone)
            movement = mobile.CurrentInput.normalized;
        return true;
    }
    private void BeginMovement()
    {
        ResetPractice(); RestoreCamera();
        PrelimTeleportTransition.MovePlayerImmediately(player.gameObject, initialPosition, initialRotation);
        player.ToggleControl(true); input.SetGameplayInputBlocked(false); mobile.ResetJoystick();
        movementStart = player.transform.position; intentionalTime = 0f; jumpSucceeded = false;
        confirmation.SetActive(false); skip.Show(true);
        ShowStep(FunctionsRelationsTutorialStep.Movement, "Use this joystick to move.", joystick);
    }
    private void Update()
    {
        if (!IsActive || player == null || Time.timeScale <= 0f) return;
        if (CurrentStep == FunctionsRelationsTutorialStep.Movement)
        {
            // PRELIM checks deliberate joystick input and net XZ displacement, not accumulated jitter.
            if (mobile.CurrentInput.sqrMagnitude > joystickDeadZone * joystickDeadZone && player.IsWalking()) intentionalTime += Time.deltaTime;
            else intentionalTime = 0f;
            Vector3 p = player.transform.position;
            if (intentionalTime >= intentionalMovementSeconds && Vector2.Distance(new Vector2(p.x, p.z), new Vector2(movementStart.x, movementStart.z)) >= movementDistance)
            {
                mobile.ResetJoystick(); jumpSucceeded = false;
                ShowStep(FunctionsRelationsTutorialStep.Jump, "Tap this button to jump.", jump);
            }
        }
        else if (CurrentStep == FunctionsRelationsTutorialStep.Jump && jumpSucceeded && player.IsJumping() && player.transform.position.y > jumpY + .1f)
        {
            CreatePracticeObject();
            ShowStep(FunctionsRelationsTutorialStep.Interaction, "Use this button to interact with nearby objects.", hand);
        }
        AnimateCamera();
    }
    public bool TryPracticeInteraction()
    {
        if (!IsActive || input.GameplayInputBlocked || Time.timeScale <= 0f) return false;
        switch (CurrentStep)
        {
            case FunctionsRelationsTutorialStep.Interaction:
                if (practice == null) return false;
                Vector3 delta = practice.transform.position - player.transform.position; delta.y = 0f;
                if (delta.magnitude > 3.5f || Vector3.Dot(player.transform.forward, delta.normalized) < .25f) return false;
                DestroyPracticeObject(); player.ToggleControl(false); StartCameraGuide(); Focus(Stage.BookTarget);
                ShowStep(FunctionsRelationsTutorialStep.Book, "Interact with the book statue to start each challenge. Try the hand or the nearby interaction button. This is practice; the real timer stays paused.", null, false, Stage.BookTarget);
                return true;
            case FunctionsRelationsTutorialStep.Book:
                ShowStep(FunctionsRelationsTutorialStep.Timer, "The book starts or resumes the shared " + StageJourneyUI.FormatTime(Stage.StartingSeconds) + " countdown. Correct submissions pause it; it does not reset between challenges. Incorrect submissions subtract " + Stage.WrongAnswerPenalty.ToString("0.##") + " seconds.", timer, true);
                return true;
            case FunctionsRelationsTutorialStep.Domain:
                ui.domainPanel.gameObject.SetActive(true);
                ShowStep(FunctionsRelationsTutorialStep.Input, "PRACTICE: Choose an input from the Domain panel. These numbers are examples, not challenge answers.", Stage.DomainButtons[0].transform as RectTransform);
                return true;
            case FunctionsRelationsTutorialStep.Output:
                ui.rangePanel.gameObject.SetActive(true);
                ShowStep(FunctionsRelationsTutorialStep.OutputChoice, "PRACTICE: Choose X or Y to connect it to input " + PracticeInput + ". The codomain is {X, Y}; the range will contain only the output you actually use.", Stage.RangeButtons[0].transform as RectTransform);
                return true;
        }
        return false;
    }
    public void PracticeViewBoard()
    {
        if (!IsActive || CurrentStep != FunctionsRelationsTutorialStep.ViewBoard) return;
        Stage.WorldBoardLabels[0].text = "PRACTICE — INFORMATION BOARD";
        Stage.WorldBoardLabels[1].text = "Domain = {6, 9}\nCodomain = {X, Y}";
        Stage.WorldBoardLabels[2].text = "Practice: choose one input, then one output.\nSubmit checks your connections.\nThe real challenge and timer are waiting.";
        Stage.BoardView.framingPadding = 1.5f;
        if (!Stage.BoardView.OpenView(() => { if (IsActive && CurrentStep == FunctionsRelationsTutorialStep.BoardBack) BeginDomain(); })) return;
        ShowStep(FunctionsRelationsTutorialStep.Board, "This InformationBoard displays the current challenge, mapping requirements, and instructions. This is a clearly labeled practice condition, separate from the real challenge.", null, true, Stage.BoardView.informationBoard);
    }
    public void PracticeBack()
    {
        if (IsActive && CurrentStep == FunctionsRelationsTutorialStep.BoardBack) Stage.BoardView.Back();
    }
    private void BeginDomain()
    {
        Stage.BoardView.framingPadding = originalBoardPadding;
        SetPracticeChoices(); CreateNodeLabels(); Focus(Stage.DomainTarget);
        ShowStep(FunctionsRelationsTutorialStep.Domain, "The domain contains the input values. Interact with Domain to open its number-selection panel, then choose a practice input.", null, false, Stage.DomainTarget);
    }
    public void ChoosePracticeInput(int index)
    {
        if (!IsActive || CurrentStep != FunctionsRelationsTutorialStep.Input || index < 0 || index >= practiceInputs.Length) return;
        PracticeInput = practiceInputs[index]; ui.domainPanel.gameObject.SetActive(false);
        Focus(Stage.RangeTarget);
        ShowStep(FunctionsRelationsTutorialStep.Output, "At the output station, choose an output for your selected input. Codomain means all available possible outputs; range means the outputs actually used. Interact to open the practice choices.", null, false, Stage.RangeTarget);
    }
    public void ClosePracticeChoices()
    {
        if (!IsActive) return;
        if (CurrentStep == FunctionsRelationsTutorialStep.Input)
            ShowStep(FunctionsRelationsTutorialStep.Domain, "Interact with Domain to open the practice input choices.", null, false, Stage.DomainTarget);
        else if (CurrentStep == FunctionsRelationsTutorialStep.OutputChoice || CurrentStep == FunctionsRelationsTutorialStep.Connection || CurrentStep == FunctionsRelationsTutorialStep.Undo)
            ShowStep(FunctionsRelationsTutorialStep.Output, "Interact with the output station to reopen the practice choices. Codomain means available outputs; range means outputs actually used.", null, false, Stage.RangeTarget);
    }
    public void ChoosePracticeOutput(int index)
    {
        if (!IsActive || CurrentStep != FunctionsRelationsTutorialStep.OutputChoice || index < 0 || index >= practiceOutputs.Length) return;
        PracticeOutput = practiceOutputs[index]; CreatePracticeBeam();
        ShowStep(FunctionsRelationsTutorialStep.Connection, "Practice connection: " + PracticeInput + " to " + PracticeOutput + ". The codomain is {X, Y}, while the range is {" + PracticeOutput + "}. In a challenge, repeat input/output selection until all required connections are built.", null, true, Stage.RangeTarget);
    }
    public void UndoPracticeConnection()
    {
        if (!IsActive || CurrentStep != FunctionsRelationsTutorialStep.Undo) return;
        if (practiceBeam != null) Destroy(practiceBeam.gameObject); practiceBeam = null; PracticeOutput = null;
        ui.rangePanel.gameObject.SetActive(false);
        ShowStep(FunctionsRelationsTutorialStep.Submit, "Submit checks the entire current connection arrangement. An incorrect submission subtracts " + Stage.WrongAnswerPenalty.ToString("0.##") + " seconds and preserves connections; correct answers pause the shared timer. The book starts challenges only. Later challenges use answer/property pedestals, followed by this same Submit button.", ui.submit, true);
    }
    public void NextStep()
    {
        if (!IsActive || !next.gameObject.activeSelf) return;
        switch (CurrentStep)
        {
            case FunctionsRelationsTutorialStep.Timer:
                ShowStep(FunctionsRelationsTutorialStep.ViewBoard, "Tap View Board to read the InformationBoard up close. Use Back to return; viewing does not submit an answer.", ui.viewBoard); break;
            case FunctionsRelationsTutorialStep.Board:
                ShowStep(FunctionsRelationsTutorialStep.BoardBack, "Tap Back to return from the board view before choosing your inputs and outputs.", ui.boardBack); break;
            case FunctionsRelationsTutorialStep.Connection:
                Stage.UndoButton.interactable = true;
                Stage.UndoButton.GetComponentInChildren<TMP_Text>().text = "UNDO PRACTICE CONNECTION";
                ShowStep(FunctionsRelationsTutorialStep.Undo, "Undo at the output station removes the last editable connection. Fixed preset connections cannot be undone. Tap Undo to remove this practice laser, then select an input/output again when correcting real work.", Stage.UndoButton.transform as RectTransform); break;
            case FunctionsRelationsTutorialStep.Submit: Confirm(); break;
        }
    }
    public void SkipTutorial() { if (IsActive && CurrentStep != FunctionsRelationsTutorialStep.Confirm) Confirm(); }
    private void Confirm()
    {
        Stage.BoardView.RestoreImmediately(false); ResetPractice(); RestoreCamera();
        CurrentStep = FunctionsRelationsTutorialStep.Confirm; player.ToggleControl(false); input.SetGameplayInputBlocked(true);
        foreach (var rect in new[] { highlight, arrow, instructions, worldRect }) rect.gameObject.SetActive(false);
        ui.domainPanel.gameObject.SetActive(false); ui.rangePanel.gameObject.SetActive(false);
        ui.viewBoard.gameObject.SetActive(false); ui.submit.gameObject.SetActive(false); ui.interaction.gameObject.SetActive(false); ui.boardBack.gameObject.SetActive(false);
        joystick.gameObject.SetActive(false); jump.gameObject.SetActive(false); hand.gameObject.SetActive(false);
        confirmation.SetActive(true); confirmation.transform.SetAsLastSibling(); skip.Show(false);
    }
    public void ConfirmYes()
    {
        if (!IsActive || CurrentStep != FunctionsRelationsTutorialStep.Confirm) return;
        var owner = Stage; Cancel(); owner.FinishOnboarding();
    }
    public void ConfirmNo() { if (IsActive && CurrentStep == FunctionsRelationsTutorialStep.Confirm) BeginMovement(); }
    public void Cancel()
    {
        if (!IsActive) return;
        Stage.BoardView.RestoreImmediately(false); ResetPractice(); RestoreCamera();
        IsActive = false; if (Active == this) Active = null;
        if (skip != null) skip.Release(); skip = null;
        if (player != null) { player.OnJumpStarted -= SuccessfulJump; player.ToggleControl(playerEnabled); }
        if (mobile != null) mobile.ResetJoystick();
        foreach (var pair in visible) if (pair.Key != null) pair.Key.SetActive(pair.Value);
        foreach (var pair in clickable) if (pair.Key != null) pair.Key.interactable = pair.Value;
        foreach (var pair in texts) if (pair.Key != null) pair.Key.text = pair.Value;
        if (settings != null) settings.interactable = settingsEnabled;
        foreach (var b in cameraInputs) if (b != null) b.enabled = true; cameraInputs.Clear();
        if (input != null) input.SetGameplayInputBlocked(inputBlocked);
        Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
        foreach (var rect in new[] { highlight, instructions, worldRect }) if (rect != null) { rect.gameObject.SetActive(false); Destroy(rect.gameObject); }
        if (confirmation != null) { confirmation.SetActive(false); Destroy(confirmation); }
    }
    private void OnDisable() { Cancel(); }
}
