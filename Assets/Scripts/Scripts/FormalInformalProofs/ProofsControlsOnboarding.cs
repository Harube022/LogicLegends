using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

public enum ProofsTutorialStep { Movement, Interaction, Jump, Timer, Progress, Book, FormalAltar, InformalAltar, Scroll, ReadScroll, Reader, Place, TakeBack, TakeBackPicker, Replace, ConfirmBook, ViewBoard, Board, BoardBack, Confirm }

/// <summary>Separate practice state; real placements, question selection and validation are never called.</summary>
[DefaultExecutionOrder(1300)]
[DisallowMultipleComponent]
public sealed partial class ProofsControlsOnboarding : MonoBehaviour
{
    public static ProofsControlsOnboarding Active { get; private set; }
    public bool IsActive { get; private set; }
    public ProofsTutorialStep CurrentStep { get; private set; }
    public bool AllowsJump => IsActive && CurrentStep == ProofsTutorialStep.Jump;
    public bool WantsViewBoard => CurrentStep == ProofsTutorialStep.ViewBoard;
    public bool WantsInteraction => CurrentStep == ProofsTutorialStep.ReadScroll ||
        CurrentStep == ProofsTutorialStep.Place || CurrentStep == ProofsTutorialStep.TakeBack || CurrentStep == ProofsTutorialStep.Replace;
    public Button YesButton { get; private set; }
    public Button NoButton { get; private set; }
    public GameObject PracticeObject => practiceObject;
    public bool PracticeCarried { get; private set; }
    public int PracticePlacement { get; private set; } = -1;
    public const string PracticePassage = "PRACTICE ONLY — This example is separate from the challenge bank.\n\nThree equal ribbons together measure 12 cm. Since all three have the same length, each measures 12 ÷ 3 = 4 cm. This explanatory prose is a valid informal proof.";
    public float joystickDeadZone = .3f, movementDistance = 1.5f, intentionalMovementSeconds = .7f;
    ProofsLibraryStageController stage;
    ProofsStageUI ui;
    ProofsHelpUI help;
    Player player;
    GameInput input;
    MobileInputUI mobile;
    RectTransform safe, joystick, jump, hand, instructions, highlight, arrow, worldRect, practiceLabel;
    TMP_Text instruction;
    Button next, settings;
    GameObject confirmation, practiceObject, practiceRoot, practiceScroll;
    ProofWorldPedestal practiceStand;
    Material practiceMaterial;
    PrelimTutorialSkipControl skip;
    Transform worldTarget;
    Vector3 initialPosition, movementStart;
    Quaternion initialRotation;
    float intentionalTime, jumpY, enteredAt;
    bool jumped, previousPlayer, previousBlocked, settingsEnabled, cursorVisible;
    CursorLockMode cursorLock;
    readonly Dictionary<GameObject, bool> visible = new Dictionary<GameObject, bool>();
    readonly Dictionary<TMP_Text, string> texts = new Dictionary<TMP_Text, string>();
    readonly Dictionary<Button, bool> clickable = new Dictionary<Button, bool>();
    readonly List<Behaviour> cameraInputs = new List<Behaviour>();

    public void Begin(ProofsLibraryStageController owner, ProofsHelpUI guide)
    {
        if (IsActive || Active != null || owner == null || !owner.OnboardingPending || Player.LocalInstance == null) return;
        stage = owner; ui = stage.Presentation; help = guide; safe = ui.SafeArea;
        player = Player.LocalInstance; input = GameInput.Instance; mobile = MobileInputUI.FindJoystick();
        var controls = GameObject.Find("Canvas 1");
        foreach (var rect in controls.GetComponentsInChildren<RectTransform>(true))
        {
            if (rect.name == "Joystick") joystick = rect;
            if (rect.name == "JumpButton") jump = rect;
            if (rect.name == "InteractButton") hand = rect;
            if (rect.name == "SettingsButton") settings = rect.GetComponent<Button>();
        }
        if (joystick == null || jump == null || hand == null || mobile == null || input == null) return;
        previousPlayer = player.enabled; previousBlocked = input.GameplayInputBlocked;
        initialPosition = player.transform.position; initialRotation = player.transform.rotation;
        cursorLock = Cursor.lockState; cursorVisible = Cursor.visible;
        visible.Clear(); texts.Clear(); clickable.Clear();
        foreach (var obj in new[] { joystick.gameObject, jump.gameObject, hand.gameObject, ui.Hud.gameObject, ui.ViewButton.gameObject,
            ui.InteractionButton.gameObject, ui.BackButton.gameObject, stage.PromptText.gameObject, stage.ReaderPanel, stage.TakeBackPanel, help.HelpButton.gameObject }) Remember(obj);
        if (settings != null) { Remember(settings.gameObject); settingsEnabled = settings.interactable; settings.interactable = false; }
        foreach (var admin in controls.GetComponentsInChildren<Button>(true).Where(b => b.name == "AdminButton")) { Remember(admin.gameObject); admin.gameObject.SetActive(false); }
        foreach (var text in stage.ReaderPanel.GetComponentsInChildren<TMP_Text>(true)) texts[text] = text.text;
        foreach (var text in stage.TakeBackPanel.GetComponentsInChildren<TMP_Text>(true)) texts[text] = text.text;
        texts[stage.BoardInstructions] = stage.BoardInstructions.text;
        texts[ui.InteractionButton.GetComponentInChildren<TMP_Text>()] = ui.InteractionButton.GetComponentInChildren<TMP_Text>().text;
        foreach (var button in stage.ReaderPanel.GetComponentsInChildren<Button>(true).Concat(stage.TakeBackPanel.GetComponentsInChildren<Button>(true)))
        { Remember(button.gameObject); clickable[button] = button.interactable; }
        foreach (var b in FindObjectsByType<Behaviour>(FindObjectsSortMode.None))
            if (b.enabled && (b is CinemachineInputAxisController || b is CinemachinePinchZoom || b is ThirdPersonCameraController))
            { cameraInputs.Add(b); b.enabled = false; }
        IsActive = true; Active = this; player.OnJumpStarted += SuccessfulJump;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        BuildUI();
        skip = PrelimTutorialSkipControl.Create(safe, SkipTutorial);
        skip.Reserve(ui.Hud, (RectTransform)help.HelpButton.transform, settings != null ? (RectTransform)settings.transform : null);
        BeginMovement();
    }
    void Remember(GameObject obj) { if (obj != null && !visible.ContainsKey(obj)) visible.Add(obj, obj.activeSelf); }
    bool Readable => Time.unscaledTime - enteredAt >= .65f;
    public bool TryGetMovementOverride(out Vector2 movement)
    {
        movement = Vector2.zero; if (!IsActive) return false;
        if (CurrentStep == ProofsTutorialStep.Movement && mobile.CurrentInput.sqrMagnitude > joystickDeadZone * joystickDeadZone) movement = mobile.CurrentInput.normalized;
        return true;
    }
    void BeginMovement()
    {
        ResetPractice(); RestoreCamera();
        PrelimTeleportTransition.MovePlayerImmediately(player.gameObject, initialPosition, initialRotation);
        player.ToggleControl(true); input.SetGameplayInputBlocked(false); mobile.ResetJoystick();
        movementStart = player.transform.position; intentionalTime = 0; jumped = false;
        confirmation.SetActive(false); skip.Show(true);
        ShowStep(ProofsTutorialStep.Movement, "Use this joystick to move.", joystick);
    }
    void SuccessfulJump() { if (AllowsJump) { jumped = true; jumpY = player.transform.position.y; } }
    void Update()
    {
        if (!IsActive || player == null || Time.timeScale <= 0) return;
        if (CurrentStep == ProofsTutorialStep.Movement)
        {
            if (mobile.CurrentInput.sqrMagnitude > joystickDeadZone * joystickDeadZone && player.IsWalking()) intentionalTime += Time.deltaTime; else intentionalTime = 0;
            var p = player.transform.position;
            if (intentionalTime >= intentionalMovementSeconds && Vector2.Distance(new Vector2(p.x, p.z), new Vector2(movementStart.x, movementStart.z)) >= movementDistance)
            {
                mobile.ResetJoystick(); CreatePracticeObject();
                ShowStep(ProofsTutorialStep.Interaction, "Use this button to interact with nearby objects.", hand);
            }
        }
        else if (CurrentStep == ProofsTutorialStep.Jump && jumped && player.IsJumping() && player.transform.position.y > jumpY + .1f)
        {
            player.ToggleControl(false);
            ShowStep(ProofsTutorialStep.Timer, "The book starts or resumes one shared " + StageJourneyUI.FormatTime(stage.StartingSeconds) + " timer. It pauses after a correct answer and does not reset between challenges. Help and this tutorial pause it.", stage.ClockText.rectTransform, true);
        }
        AnimateCamera();
    }
    public bool TryPracticeInteraction()
    {
        if (!IsActive || input.GameplayInputBlocked || Time.timeScale <= 0 || !Readable) return false;
        switch (CurrentStep)
        {
            case ProofsTutorialStep.Interaction:
                if (practiceObject == null) return false;
                var delta = practiceObject.transform.position - player.transform.position; delta.y = 0;
                if (delta.magnitude > 3.5f || Vector3.Dot(player.transform.forward, delta.normalized) < .25f) return false;
                DestroyPracticeObject(); jumped = false;
                ShowStep(ProofsTutorialStep.Jump, "Tap this button to jump.", jump); return true;
            case ProofsTutorialStep.Book:
                Focus(stage.AltarTargets[0], Vector3.forward);
                ShowStep(ProofsTutorialStep.FormalAltar, "The Formal Proof altar receives proofs written as explicit mathematical statements with justified logical steps. Read the proof's presentation before choosing its altar.", null, true, stage.AltarTargets[0]); return true;
            case ProofsTutorialStep.ReadScroll: ReadPracticeScroll(); return true;
            case ProofsTutorialStep.Place:
            case ProofsTutorialStep.Replace: PlacePracticeScroll(1); return true;
            case ProofsTutorialStep.TakeBack: OpenPracticeTakeBack(); return true;
            case ProofsTutorialStep.ConfirmBook: ConfirmPracticeArrangement(); return true;
        }
        return false;
    }
    public void NextStep()
    {
        if (!IsActive || !Readable || !next.gameObject.activeSelf) return;
        switch (CurrentStep)
        {
            case ProofsTutorialStep.Timer:
                ShowStep(ProofsTutorialStep.Progress, "This shows the current challenge out of " + stage.ChallengeCount + " and question out of two. Complete two distinct questions per challenge. Correct work pauses the timer; return to the book to start the next question. After both questions, the next challenge becomes available.", ui.Progress.rectTransform, true); break;
            case ProofsTutorialStep.Progress:
                StartCamera(); Focus(stage.BookTarget, Vector3.forward);
                ShowStep(ProofsTutorialStep.Book, "Interact with the book statue to start each challenge. After completing your arrangement, interact with it again to confirm. Tap the hand for a practice interaction; it cannot start the real timer.", hand, false, stage.BookTarget); break;
            case ProofsTutorialStep.FormalAltar:
                Focus(stage.AltarTargets[1], Vector3.forward);
                ShowStep(ProofsTutorialStep.InformalAltar, "The Informal Proof altar receives valid reasoning explained in prose. Informal does not mean incorrect. In Challenges 1 and 2, classify each scroll by how it presents its reasoning.", null, true, stage.AltarTargets[1]); break;
            case ProofsTutorialStep.InformalAltar:
                CreatePracticeScroll(); Focus(practiceStand.transform, ScrollForward());
                ShowStep(ProofsTutorialStep.Scroll, "A scroll contains a proof or reasoning passage to examine. This clearly marked practice scroll uses a separate example. Read the full passage rather than guessing from the preview.", null, true, practiceStand.transform); break;
            case ProofsTutorialStep.Scroll:
                ShowStep(ProofsTutorialStep.ReadScroll, "Use the hand or Interact button to open the practice scroll's existing reading panel.", hand, false, practiceStand.transform); break;
            case ProofsTutorialStep.Board:
                ShowStep(ProofsTutorialStep.BoardBack, "Tap Back to leave board view and restore the gameplay camera. View Board never starts a challenge or confirms an answer.", (RectTransform)ui.BackButton.transform); break;
        }
    }
    public void ReadPracticeScroll()
    {
        if (!IsActive || CurrentStep != ProofsTutorialStep.ReadScroll || !Readable) return;
        stage.ReaderTitle.text = "PRACTICE SCROLL — READ AND COLLECT"; stage.ReaderBody.text = PracticePassage;
        var scroll = stage.ReaderPanel.GetComponentInChildren<ScrollRect>(true); if (scroll != null) scroll.verticalNormalizedPosition = 1;
        stage.ReaderPanel.SetActive(true);
        ui.RefreshReader(true);
        ShowStep(ProofsTutorialStep.Reader, "Read this practice proof. Get This Scroll collects it; Back leaves it at the stand.", (RectTransform)stage.CollectButton.transform);
    }
    public void CollectPracticeScroll()
    {
        if (!IsActive || CurrentStep != ProofsTutorialStep.Reader || !Readable) return;
        stage.ReaderPanel.SetActive(false); PracticeCarried = true; practiceScroll.SetActive(false);
        ui.RefreshReader();
        practiceStand.SetTabletVisible(false); Focus(stage.AltarTargets[1], Vector3.forward);
        ShowStep(ProofsTutorialStep.Place, "You are carrying the practice scroll. Its valid prose belongs on the Informal Proof altar. Use Place Scroll or the hand to place it. Placement alone does not submit your answer.", hand, false, stage.AltarTargets[1]);
    }
    public void ClosePracticeReader()
    {
        if (!IsActive || CurrentStep != ProofsTutorialStep.Reader) return;
        stage.ReaderPanel.SetActive(false);
        ui.RefreshReader();
        ShowStep(ProofsTutorialStep.ReadScroll, "Interact with the practice scroll to read it again, then choose Get This Scroll.", hand, false, practiceStand.transform);
    }
    public void PlacePracticeScroll(int altar)
    {
        if (!IsActive || !PracticeCarried || altar != 1 || !Readable || CurrentStep != ProofsTutorialStep.Place && CurrentStep != ProofsTutorialStep.Replace) return;
        bool replacing = CurrentStep == ProofsTutorialStep.Replace;
        PracticeCarried = false; PracticePlacement = altar;
        practiceScroll.transform.SetPositionAndRotation(stage.AltarTargets[altar].TransformPoint(new Vector3(0, 1.74f, 0)), Quaternion.Euler(0, 90, 0));
        practiceScroll.transform.localScale = stage.AltarTargets[altar].lossyScale * 4; practiceScroll.SetActive(true);
        if (replacing)
        {
            Focus(stage.BookTarget, Vector3.forward);
            ShowStep(ProofsTutorialStep.ConfirmBook, "Return to the book to confirm the entire arrangement. Incorrect or incomplete confirmations subtract " + stage.WrongPenalty.ToString("0.##") + " seconds and preserve placements. Correct answers pause time. Try confirming this practice arrangement now.", hand, false, stage.BookTarget);
        }
        else ShowStep(ProofsTutorialStep.TakeBack, "With empty hands, interact with an occupied altar to choose a scroll to take back. This lets you correct a classification before confirming. Try Take Back now.", hand, false, stage.AltarTargets[1]);
    }
    public void OpenPracticeTakeBack()
    {
        if (!IsActive || CurrentStep != ProofsTutorialStep.TakeBack || !Readable) return;
        stage.TakeBackTitle.text = "TAKE BACK — PRACTICE SCROLL";
        for (int i = 0; i < stage.TakeBackButtons.Length; i++)
        {
            stage.TakeBackButtons[i].gameObject.SetActive(i == 0);
            if (i == 0) stage.TakeBackButtons[i].GetComponentInChildren<TMP_Text>().text = "PRACTICE SCROLL\nThree equal ribbons measure 12 cm.\nTAP TO TAKE BACK";
        }
        stage.TakeBackPanel.SetActive(true);
        ShowStep(ProofsTutorialStep.TakeBackPicker, "Choose the practice scroll to carry it again. Cancel closes this picker without changing placements.", (RectTransform)stage.TakeBackButtons[0].transform);
    }
    public void TakeBackPracticeScroll()
    {
        if (!IsActive || CurrentStep != ProofsTutorialStep.TakeBackPicker || !Readable) return;
        stage.TakeBackPanel.SetActive(false); PracticePlacement = -1; PracticeCarried = true; practiceScroll.SetActive(false);
        ShowStep(ProofsTutorialStep.Replace, "The practice scroll is selected again. Place it back on the Informal altar. In real play, take back and move any misplaced scroll to the altar that matches its presentation.", hand, false, stage.AltarTargets[1]);
    }
    public void ClosePracticeTakeBack()
    {
        if (!IsActive || CurrentStep != ProofsTutorialStep.TakeBackPicker) return;
        stage.TakeBackPanel.SetActive(false);
        ShowStep(ProofsTutorialStep.TakeBack, "Interact with the occupied altar and choose the practice scroll to take it back.", hand, false, stage.AltarTargets[1]);
    }
    public void ConfirmPracticeArrangement()
    {
        if (!IsActive || CurrentStep != ProofsTutorialStep.ConfirmBook || !Readable) return;
        ShowStep(ProofsTutorialStep.ViewBoard, "Practice confirmation complete. Tap View Board to read the Information Board up close. This view is also available during normal gameplay.", (RectTransform)ui.ViewButton.transform);
    }
    public void OpenPracticeBoard()
    {
        if (!IsActive || CurrentStep != ProofsTutorialStep.ViewBoard || !Readable) return;
        stage.BoardInstructions.text = "PRACTICE — INFORMATION BOARD\nObjective: read and classify the sample reasoning.\nThree equal ribbons measure 12 cm altogether.\nEach ribbon measures 4 cm because 12 ÷ 3 = 4.\nThis example does not belong to a real challenge.";
        ui.BoardView.readingArea = new Rect(.40f, .23f, .57f, .66f);
        if (!ui.BoardView.OpenView(BoardClosed)) return;
        ShowStep(ProofsTutorialStep.Board, "This board displays the current challenge, objectives, and instructions. The clearly labeled practice example leaves the real question bank untouched. During a real active challenge, the timer continues while reading.", null, true, stage.BoardTarget);
    }
    void BoardClosed() { if (IsActive && CurrentStep == ProofsTutorialStep.BoardBack) Confirm(); }
    public void SkipTutorial() { if (IsActive && CurrentStep != ProofsTutorialStep.Confirm) Confirm(); }
    void Confirm()
    {
        ui.BoardView.RestoreImmediately(false); ResetPractice(); RestoreCamera();
        CurrentStep = ProofsTutorialStep.Confirm; player.ToggleControl(false); input.SetGameplayInputBlocked(true);
        foreach (var rect in new[] { instructions, highlight, worldRect, joystick, hand, jump }) rect.gameObject.SetActive(false);
        ui.InteractionButton.gameObject.SetActive(false); ui.ViewButton.gameObject.SetActive(false); ui.BackButton.gameObject.SetActive(false);
        skip.Show(false); confirmation.SetActive(true); confirmation.transform.SetAsLastSibling(); mobile.ResetJoystick();
    }
    public void ConfirmNo() { if (IsActive && CurrentStep == ProofsTutorialStep.Confirm) BeginMovement(); }
    public void ConfirmYes() { if (IsActive && CurrentStep == ProofsTutorialStep.Confirm) { Cancel(); stage.FinishOnboarding(); } }
    void ResetPractice()
    {
        DestroyPracticeObject();
        if (practiceRoot != null) { practiceRoot.SetActive(false); Destroy(practiceRoot); }
        practiceRoot = null; practiceStand = null; practiceScroll = null; PracticeCarried = false; PracticePlacement = -1;
        foreach (var pair in texts) if (pair.Key != null) pair.Key.text = pair.Value;
        foreach (var pair in layouts) if (pair.Key != null) pair.Value.Restore(pair.Key);
        if (stage != null)
        {
            if (stage.ReaderPanel != null) stage.ReaderPanel.SetActive(false);
            ui?.RefreshReader();
            if (stage.TakeBackPanel != null) stage.TakeBackPanel.SetActive(false);
        }
        if (ui != null && ui.BoardView != null) ui.BoardView.readingArea = new Rect(.05f, .16f, .9f, .78f);
    }
    public void Cancel()
    {
        if (!IsActive) return;
        if (ui != null && ui.BoardView != null) ui.BoardView.RestoreImmediately(false);
        ResetPractice(); RestoreCamera();
        IsActive = false; if (Active == this) Active = null;
        if (player != null) { player.OnJumpStarted -= SuccessfulJump; player.ToggleControl(previousPlayer); }
        if (input != null) input.SetGameplayInputBlocked(previousBlocked);
        if (mobile != null) mobile.ResetJoystick();
        foreach (var pair in visible) if (pair.Key != null) pair.Key.SetActive(pair.Value);
        foreach (var pair in clickable) if (pair.Key != null) pair.Key.interactable = pair.Value;
        if (settings != null) settings.interactable = settingsEnabled;
        foreach (var b in cameraInputs) if (b != null) b.enabled = true; cameraInputs.Clear();
        Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
        if (skip != null) skip.Release(); skip = null;
        foreach (var rect in new[] { instructions, highlight, worldRect }) if (rect != null) { rect.gameObject.SetActive(false); Destroy(rect.gameObject); }
        if (confirmation != null) { confirmation.SetActive(false); Destroy(confirmation); }
    }
    void OnDisable() { Cancel(); }
}
