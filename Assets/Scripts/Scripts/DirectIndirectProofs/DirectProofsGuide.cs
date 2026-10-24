using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum DirectProofsPracticeStep { Movement, Jump, Interaction, Timer, Progress, Book, Instructions, Altars, Choose, OpenBoard, Arrange, Correction, ConfirmBook, Confirmation }

/// <summary>Paused guidance with separate example data; never invokes real answer or start methods.</summary>
[DefaultExecutionOrder(1300), DisallowMultipleComponent]
public sealed partial class DirectProofsGuide : MonoBehaviour
{
    enum Mode { None, Help, Practice, Introduction }
    Mode mode;
    public static DirectProofsGuide Active { get; private set; }
    public bool IsBusy => mode != Mode.None;
    public bool IsPractice => mode == Mode.Practice;
    public bool AllowsJump => IsPractice && Step == DirectProofsPracticeStep.Jump;
    public DirectProofsPracticeStep Step { get; private set; }
    public bool NeedsIntroduction => stage != null && !stage.Finished && !stage.OnboardingPending &&
        stage.State == DirectIndirectProofStageController.TrialState.WaitingForActivation && !seen[stage.ChallengeNumber - 1];
    public bool IntroductionSeen(int challenge) => seen[challenge - 1];
    public GameObject PracticeObject => null;
    public Button HelpButton { get; private set; }
    public Button NextButton { get; private set; }
    public Button BeginButton { get; private set; }
    public Button PreviousButton { get; private set; }
    public Button ActionButton { get; private set; }
    public Button AlternateButton { get; private set; }
    public RectTransform SafeArea { get; private set; }
    DirectIndirectProofStageController stage;
    Player player;
    GameInput input;
    MobileInputUI mobile;
    RectTransform hud, joystick, jump, hand, settings;
    readonly bool[] seen = new bool[5];
    readonly Dictionary<GameObject, bool> visibility = new Dictionary<GameObject, bool>();
    readonly Dictionary<Behaviour, bool> cameraInputs = new Dictionary<Behaviour, bool>();
    bool autoShown, oldPlayer, oldBlocked, oldCursor, jumped, practiceSwapped;
    float oldTime, enteredAt, intentionalTime, jumpHeight;
    CursorLockMode oldLock;
    Vector3 startPosition, movementStart;
    Quaternion startRotation;
    PrelimTutorialSkipControl skip;
    Button adminButton;
    int page;
    bool Readable => Time.unscaledTime - enteredAt >= 1f;

    public void Initialize(DirectIndirectProofStageController owner, RectTransform status)
    {
        stage = owner; hud = status;
        var canvas = stage.Clock.GetComponentInParent<Canvas>(); canvas.sortingOrder = 210;
        SafeArea = StageUITheme.Rect("Direct Proofs SafeArea", canvas.transform);
        StageUITheme.Stretch(SafeArea, Vector2.zero, Vector2.one); SafeArea.gameObject.AddComponent<Crystal.SafeArea>();
        hud.SetParent(SafeArea, false);
        BuildUI();
        stage.ConfigureGuidePresentation(SafeArea);
        StartCoroutine(FirstEntry());
    }
    IEnumerator FirstEntry()
    {
        while (Player.LocalInstance == null || GameInput.Instance == null || Time.timeScale <= 0 ||
            GameInput.Instance.GameplayInputBlocked || !Player.LocalInstance.GetComponent<CharacterController>().enabled) yield return null;
        if (autoShown || stage.Finished) yield break;
        autoShown = true; OpenHelp();
    }
    public void OpenHelp()
    {
        if (IsBusy || stage.Finished || stage.IsReaderOpen || Player.LocalInstance == null || GameInput.Instance == null || Time.timeScale <= 0) return;
        BeginSession(Mode.Help); Time.timeScale = 0; page = 0; ShowHelpPage();
    }
    void BeginSession(Mode value)
    {
        player = Player.LocalInstance; input = GameInput.Instance; mobile = MobileInputUI.FindJoystick();
        oldPlayer = player.enabled; oldBlocked = input.GameplayInputBlocked; oldTime = Time.timeScale;
        oldCursor = Cursor.visible; oldLock = Cursor.lockState;
        startPosition = player.transform.position; startRotation = player.transform.rotation;
        visibility.Clear(); cameraInputs.Clear();
        var controls = GameObject.Find("Canvas 1")?.transform.Find("SafeArea/Gameplay_Interface");
        joystick = controls?.Find("Joystick") as RectTransform; jump = controls?.Find("JumpButton") as RectTransform;
        hand = controls?.Find("InteractButton") as RectTransform; settings = controls?.Find("SettingsButton") as RectTransform;
        foreach (Transform child in stage.Clock.GetComponentInParent<Canvas>().transform)
            if (child != SafeArea) Remember(child.gameObject, false);
        foreach (Transform child in SafeArea) if (child != root && child != highlight) Remember(child.gameObject, false);
        if (controls != null)
            foreach (Transform child in controls) Remember(child.gameObject, false);
        foreach (var button in FindObjectsByType<Button>(FindObjectsSortMode.None))
            if (button.name.Replace(" ", "").Equals("AdminButton", StringComparison.OrdinalIgnoreCase)) Remember(button.gameObject, false);
        foreach (var behaviour in FindObjectsByType<Behaviour>(FindObjectsSortMode.None))
            if (behaviour.enabled && (behaviour is Unity.Cinemachine.CinemachineInputAxisController || behaviour is CinemachinePinchZoom || behaviour is ThirdPersonCameraController))
            { cameraInputs[behaviour] = true; behaviour.enabled = false; }
        mode = value; Active = this; player.OnJumpStarted += OnSuccessfulJump;
        input.SetGameplayInputBlocked(true); mobile?.ResetJoystick(); player.enabled = false;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        root.gameObject.SetActive(true); root.SetAsLastSibling(); highlight.gameObject.SetActive(false);
    }
    void Remember(GameObject item, bool show)
    { if (item == null) return; if (!visibility.ContainsKey(item)) visibility[item] = item.activeSelf; item.SetActive(show); }
    void ShowHelpPage()
    {
        var lesson = HelpPages[page]; SetCard(lesson.title, lesson.body);
        PreviousButton.gameObject.SetActive(true); PreviousButton.interactable = page > 0;
        NextButton.gameObject.SetActive(true); NextButton.interactable = page < HelpPages.Length - 1;
        BeginButton.gameObject.SetActive(true); BeginButton.GetComponentInChildren<TMP_Text>().text = stage.OnboardingPending ? "Let's Begin" : "Back to Play";
        ActionButton.gameObject.SetActive(false); AlternateButton.gameObject.SetActive(false);
    }
    public void Previous() { if (mode == Mode.Help && page > 0) { page--; ShowHelpPage(); } }
    public void BeginFromHelp()
    {
        if (mode != Mode.Help) return;
        bool practice = stage.OnboardingPending; Cancel();
        if (practice) { BeginSession(Mode.Practice); BeginMovement(); }
    }
    void BeginMovement()
    {
        CleanPractice(); RestoreCamera();
        PrelimTeleportTransition.MovePlayerImmediately(player.gameObject, startPosition, startRotation);
        Time.timeScale = oldTime; mobile?.ResetJoystick(); intentionalTime = 0; jumped = false;
        movementStart = player.transform.position;
        if (skip == null) skip = PrelimTutorialSkipControl.Create(SafeArea, Skip);
        skip.Show(true); skip.Reserve(hud);
        ShowStep(DirectProofsPracticeStep.Movement, "Use this joystick to move.", joystick);
    }
    public bool TryGetMovementOverride(out Vector2 movement)
    {
        movement = Vector2.zero;
        if (!IsPractice) return false;
        if (Step == DirectProofsPracticeStep.Movement && mobile != null && mobile.CurrentInput.sqrMagnitude > .3f * .3f)
            movement = mobile.CurrentInput.normalized;
        return true;
    }
    void OnSuccessfulJump() { if (AllowsJump) { jumped = true; jumpHeight = player.transform.position.y; } }
    void Update()
    {
        if (stage == null) return;
        LayoutUI();
        HelpButton.gameObject.SetActive(!IsBusy && !stage.Finished && !stage.IsReaderOpen);
        if (!IsBusy)
        {
            hud.gameObject.SetActive(!stage.IsReaderOpen && !stage.Finished);
            stage.Prompt.gameObject.SetActive(!stage.IsReaderOpen && !stage.Finished);
            stage.Selection.gameObject.SetActive(!stage.IsReaderOpen && !stage.Finished);
            if (NeedsIntroduction && Player.LocalInstance != null && GameInput.Instance != null && !GameInput.Instance.GameplayInputBlocked && Time.timeScale > 0) BeginIntroduction();
            return;
        }
        // Admin may be created after this stage's first Help opens.
        if (adminButton == null) adminButton = FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.name == "AdminButton");
        if (adminButton != null && adminButton.gameObject.activeSelf) Remember(adminButton.gameObject, false);
        if (mode == Mode.Practice)
        {
            if (Step == DirectProofsPracticeStep.Movement && Time.timeScale > 0)
            {
                if (mobile != null && mobile.CurrentInput.sqrMagnitude > .09f && player.IsWalking()) intentionalTime += Time.deltaTime;
                else intentionalTime = 0;
                Vector3 delta = player.transform.position - movementStart; delta.y = 0;
                if (intentionalTime >= .7f && delta.magnitude >= 1.5f)
                { mobile.ResetJoystick(); ShowStep(DirectProofsPracticeStep.Jump, "Tap this button to jump.", jump); }
            }
            else if (Step == DirectProofsPracticeStep.Jump && jumped && player.IsJumping() && player.transform.position.y > jumpHeight + .1f)
            { ShowStep(DirectProofsPracticeStep.Interaction, "Tap the hand button to interact with nearby objects.", hand); }
        }
        if (mode != Mode.Help && NextButton.gameObject.activeSelf) NextButton.interactable = Readable;
    }
    void LateUpdate() { if (IsBusy) { AnimateCamera(); LayoutUI(); } }
    void ShowStep(DirectProofsPracticeStep step, string message, RectTransform control = null, bool explanation = false, Transform subject = null)
    {
        Step = step; enteredAt = Time.unscaledTime;
        SetCard("Practice — " + step.ToString(), message);
        PreviousButton.gameObject.SetActive(false); BeginButton.gameObject.SetActive(false);
        NextButton.gameObject.SetActive(explanation); NextButton.interactable = false;
        ActionButton.gameObject.SetActive(false); AlternateButton.gameObject.SetActive(false);
        SetHighlight(control); worldSubject = subject;
        bool moving = step == DirectProofsPracticeStep.Movement || step == DirectProofsPracticeStep.Jump || step == DirectProofsPracticeStep.Interaction;
        player.enabled = moving; input.SetGameplayInputBlocked(!moving && control != hand);
        if (joystick != null) joystick.gameObject.SetActive(step == DirectProofsPracticeStep.Movement);
        if (jump != null) jump.gameObject.SetActive(step == DirectProofsPracticeStep.Jump);
        if (hand != null) hand.gameObject.SetActive(control == hand);
        hud.gameObject.SetActive(step == DirectProofsPracticeStep.Timer || step == DirectProofsPracticeStep.Progress);
    }
    public bool TryPracticeInteraction()
    {
        if (!IsPractice || (Step != DirectProofsPracticeStep.Interaction && !Readable) || input == null || input.GameplayInputBlocked) return false;
        if (Step == DirectProofsPracticeStep.Interaction)
        {
            CleanPractice(); RestoreCamera(); ShowStep(DirectProofsPracticeStep.Timer, "The book starts or resumes one shared " + StageJourneyUI.FormatTime(stage.StartingSeconds) + " countdown. Correct answers pause it; it never resets between questions. Help and tutorials use no countdown time.", stage.Clock.rectTransform, true); return true;
        }
        if (Step == DirectProofsPracticeStep.Book)
        {
            Focus(stage.InformationBoard, false); ShowStep(DirectProofsPracticeStep.Instructions, "The Information Board shows the current problem and instructions after the book starts a question. Approach it and use the hand to read the full problem. The timer keeps running during normal reading and altar ordering.", null, true, stage.InformationBoard); return true;
        }
        if (Step == DirectProofsPracticeStep.ConfirmBook) { Confirm(); return true; }
        return false;
    }
    public void Next()
    {
        if (mode == Mode.Help) { if (page < HelpPages.Length - 1) { page++; ShowHelpPage(); } return; }
        if (!Readable || !NextButton.gameObject.activeSelf) return;
        if (mode == Mode.Introduction) { if (++page >= IntroductionPages.Length) EndIntroduction(); else ShowIntroductionPage(); return; }
        switch (Step)
        {
            case DirectProofsPracticeStep.Timer:
                ShowStep(DirectProofsPracticeStep.Progress, "There are five challenges and two distinct questions per challenge: ten questions total. Solve both before the next challenge. Every question waits for a new book interaction.", stage.Progress.rectTransform, true); break;
            case DirectProofsPracticeStep.Progress:
                Focus(stage.Book, false); ShowStep(DirectProofsPracticeStep.Book, "Use the hand at the Book Statue to start each question, then return to the book to confirm your finished answer. Try the hand now: this practice interaction cannot start a real question.", hand, false, stage.Book); break;
            case DirectProofsPracticeStep.Instructions:
                FocusOverview(); ShowStep(DirectProofsPracticeStep.Altars, "Challenge 1 uses the Direct and Indirect altars to classify a supplied proof. Later challenges use the active altar's proof board. Read every statement; drag one row onto another to swap their positions. The numbered rows are the proof order.", null, true); break;
            case DirectProofsPracticeStep.Altars:
                ShowStep(DirectProofsPracticeStep.Choose, "PRACTICE ONLY: Four equal cords total 28 cm. Writing 4x = 28 and solving x = 7 directly establishes each length. Choose the proof family for this separate example.");
                SetActions("Direct Proof", "Indirect Proof"); break;
            case DirectProofsPracticeStep.Correction:
                Focus(stage.Book, false); ShowStep(DirectProofsPracticeStep.ConfirmBook, "In real play, return to the Book Statue to confirm the entire proof. An incorrect answer subtracts " + stage.WrongPenalty.ToString("0.##") + " seconds and preserves your chosen method and order. Reopen the altar to fix it. Use the hand to confirm this practice example; no time or score changes.", hand, false, stage.Book); break;
        }
    }
    public void PracticeAction(bool alternate = false)
    {
        if (!IsPractice || !Readable) return;
        if (Step == DirectProofsPracticeStep.Confirmation) { if (alternate) BeginMovement(); else FinishPractice(); return; }
        if (Step == DirectProofsPracticeStep.Choose)
        {
            if (alternate) { body.text = "This practice derivation starts from the given facts and solves directly, so choose Direct Proof. This practice correction has no penalty."; return; }
            Focus(stage.Altars[0], false);
            ShowStep(DirectProofsPracticeStep.OpenBoard, "Open the practice altar. Arrange all five rows into a valid sequence: givens, equation, calculation, result, and conclusion. These cord lengths are separate from the real question bank.", null, false, stage.Altars[0]);
            SetActions("Open Practice Altar"); return;
        }
        if (Step == DirectProofsPracticeStep.OpenBoard) OpenPracticeOrdering();
    }
    static TrialProofChallenge Example => new TrialProofChallenge {
        kind = TrialProofKind.ArrangeSteps, station = TrialProofMethod.Direct, correctMethod = TrialProofMethod.Direct,
        tablets = new[] {
            new TrialProofTabletData { statement = "Four equal cords have total length 28 cm.", justification = "Given." },
            new TrialProofTabletData { statement = "Let each cord have length x cm, so 4x = 28.", justification = "Equal lengths." },
            new TrialProofTabletData { statement = "Divide both sides by 4: x = 28 / 4.", justification = "Preserve equality." },
            new TrialProofTabletData { statement = "Thus x = 7.", justification = "Calculate." },
            new TrialProofTabletData { statement = "Each cord is therefore 7 cm long.", justification = "Conclusion." } },
        displayOrder = new[] { 0, 2, 1, 3, 4 }, acceptedOrders = new[] { new TrialProofOrder { tabletIds = new[] { 0, 1, 2, 3, 4 } } } };
    void OpenPracticeOrdering()
    {
        ShowStep(DirectProofsPracticeStep.Arrange, "Drag a statement onto another row to swap them. Read the whole statement and justification. Move the misplaced rows into a logically valid order, then press Back to Room. You can reopen and swap again to correct mistakes.");
        practiceSwapped = false;
        stage.OrderingBoard.ShowPractice(Example, () => practiceSwapped = true, null, PracticeBoardClosed);
        SafeArea.SetAsLastSibling(); root.SetAsLastSibling(); skip.Show(true);
    }
    void PracticeBoardClosed()
    {
        if (!IsPractice || Step != DirectProofsPracticeStep.Arrange) return;
        if (!practiceSwapped || !Example.IsCorrect(0, stage.OrderingBoard.CurrentOrder))
        {
            ShowStep(DirectProofsPracticeStep.OpenBoard, "The practice rows still need correcting. Read the givens first, then the equation, division, result, and conclusion. Reopen the example and swap the middle rows. Practice has no penalty."); SetActions("Try Again"); return;
        }
        ShowStep(DirectProofsPracticeStep.Correction, "Practice arrangement complete. Back returns to the room without submitting. During a real question, reopen the same altar to swap any misplaced rows; your work is retained after an incorrect confirmation. In Challenge 5, also select a strategy on the board.", null, true);
    }

    void CleanPractice()
    {
        if (stage != null && stage.OrderingBoard != null && stage.OrderingBoard.IsPractice) stage.OrderingBoard.ForceClose();
    }
    public void Skip()
    { if (mode == Mode.Introduction) EndIntroduction(); else if (IsPractice && Step != DirectProofsPracticeStep.Confirmation) Confirm(); }
    void Confirm()
    {
        CleanPractice(); RestoreCamera();
        ShowStep(DirectProofsPracticeStep.Confirmation, "Let's start? Yes prepares Challenge 1. Its question and shared countdown still wait until you interact with the real Book Statue. No repeats this practice from movement.");
        heading.text = "Let's start?"; SetActions("Yes", "No"); skip?.Show(false);
    }
    void FinishPractice()
    { stage.CompleteOnboarding(); PrelimTeleportTransition.MovePlayerImmediately(player.gameObject, startPosition, startRotation); Cancel(); }
    public void Cancel()
    {
        if (!IsBusy) return;
        bool practice = IsPractice;
        CleanPractice(); RestoreCamera(); SetHighlight(null);
        if (skip != null) skip.Release(); skip = null;
        mode = Mode.None; if (Active == this) Active = null;
        if (root != null) root.gameObject.SetActive(false);
        if (player != null) { player.OnJumpStarted -= OnSuccessfulJump; if (practice) PrelimTeleportTransition.MovePlayerImmediately(player.gameObject, startPosition, startRotation); player.enabled = oldPlayer; }
        foreach (var entry in visibility) if (entry.Key != null) entry.Key.SetActive(entry.Value);
        foreach (var entry in cameraInputs) if (entry.Key != null) entry.Key.enabled = entry.Value;
        visibility.Clear(); cameraInputs.Clear();
        Time.timeScale = oldTime; mobile?.ResetJoystick(); input?.SetGameplayInputBlocked(oldBlocked);
        Cursor.lockState = oldLock; Cursor.visible = oldCursor;
    }
    void OnDisable() { Cancel(); FinishCameraImmediately(); RestoreTouchLayout(); }
    void OnDestroy() { Cancel(); FinishCameraImmediately(); RestoreTouchLayout(); if (SafeArea != null) Destroy(SafeArea.gameObject); }
}
