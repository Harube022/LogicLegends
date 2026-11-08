ANDROID UI / TUTORIAL AUDIT — 8 NOVEMBER 2026

Scope
Trials 1–10. Local changes only. No commits, pushes, merges, package upgrades,
Editor restarts, or production-backend changes were performed for this audit.

Confirmed issues and changes

1. Rules of Inference tutorial camera
The animated Book Statue page reports transient world bounds tens of thousands
of units wide while it flips. The previous tutorial combined every renderer's
bounds to choose its target, moving the camera far away from the actual stage.
The main camera's culling mask and clipping distances were also inspected.

TutorialWorldBounds now measures authored rigid mesh geometry and excludes
animated/skinned pages, text and effects. The world highlight uses the same
stable geometry. InferenceControlsOnboarding now uses PrelimTutorialCameraRig
instead of separately owning the Cinemachine Brain's blend configuration.
This retains a single rendered-camera controller and restores its prior blend
settings through the existing shared lease/return mechanism.

Evidence: InferenceCameraBefore.txt/.png and InferenceCameraAfter.txt/.png.
The after capture includes camera, bounds, instruction-text and culling details.
The book remains in the camera frustum through four additional page-flip phases.

2. Tutorial Options/Settings restriction
TutorialSettingsLock reads each trial's existing tutorial state, including
challenge introductions and confirmation/repeat states. It applies a separate
CanvasGroup gate to Settings, visibly dims it, prevents touches, and restores
the previous values. LevelMenu also rejects direct pause requests while locked.
The gate remains active through tutorial camera return; normal board views are
not classified as tutorials. No fullscreen input-blocking overlay was added.

3. Truth Table inventory taps
The saved inventory slots still contained legacy serialized select-only
callbacks. MobileInventoryUI now binds each existing slot to its equip/hide
toggle exactly once and makes decorative slot graphics non-raycasting.
Counts, block identities, placement and inventory data are unchanged.
Repeated touch equip/hide/select tests passed with two isolated T/F samples.

Production files
Assets/Scripts/Scripts/GameManager/TutorialWorldBounds.cs
Assets/Scripts/Scripts/GameManager/TutorialSettingsLock.cs
Assets/Scripts/Scripts/GameManager/PrelimTutorialCameraRig.cs
Assets/Scripts/Scripts/PRELIM/RulesOfInference/InferenceControlsOnboarding.cs
Assets/Scripts/Scripts/LevelMenu.cs
Assets/Scripts/Scripts/PRELIM/Truth_Table_Script/MobileInventoryUI.cs
Assets/Scripts/Scripts/DirectIndirectProofs/DirectProofsGuide.Camera.cs
Assets/Scripts/Scripts/Graph/GraphChallengeIntroductions.cs
The last two files mark their shared camera rigs as tutorial-owned.

Test environment and coverage
Unity 6000.3.6f1 Editor, Android active build target, Input System touch events.
Landscape sizes: 800x480, 1280x720, 1920x1080 and 2960x1440.
Safe-area/control anchoring assertions and Help layout captures were included.

Saved scenes tested:
Trial 1: PRELIM (Propositional Logic)
Trial 2: TruthTable
Trial 3: RulesOfInference
Trial 4: Formal vs. Informal Proofs
Trial 5: Direct&Indirect
Trial 6: Induction
Trial 7: Setssss
Trial 8: Functions and Relations
Trial 9: Graph
Trial 10: Trees

All ten passed the main Help/layout, board-return, control ownership and
scene-unload/re-entry checks. All ten passed retry/result touch checks.
Help scrolling, gameplay-video playback, Begin exit and timer/input snapshots
were checked at all four landscape sizes. Board Back, movement-control layouts,
UI touch/gesture ownership and one active EventSystem/input module were checked.

Initial control tutorials were checked with two Skip -> No repeat cycles,
single shared Skip restoration, consumed hand-practice input, confirmation
Settings restrictions and post-tutorial restoration. Trees uses its existing
board-facing challenge practice and real Skip action rather than the joystick
walkthrough. Available initial challenge introductions were also checked.
This audit did not manually traverse every later challenge tutorial step.

Additional panel checks:
- Truth Table: all four quiz choices, unanswered/feedback Back, countdown and
  penalty checks; saved inventory slot equip/hide/select using real UI touches.
- Rules of Inference: all four pickup-quiz choices, unanswered Back and
  collection/Continue close; Book tutorial camera and page-flip stability.
- Formal/Informal: scroll reader Back, Get This Scroll and Return Scroll.
- Induction: catalogue choice, Get Scroll, Return Scroll and catalogue Close.
- Functions/Relations: Domain then Range selection, individual connection
  removal and panel input restoration.
- All trials: real result buttons, first/second retry, third-retry rejection,
  Study option, Start Over and duplicate completion payout checks.

Reward tests used an in-memory transport and unique disposable test identity;
they did not call the live commerce backend. Retry navigation destinations were
intercepted to inspect the requested transition. Separate main-suite tests
performed actual saved-scene unload/re-entry. Full Main Menu navigation and a
physical app suspend/resume cycle were not exercised.

Evidence locations
output/AndroidUIAudit/Flows.txt — appended touch/state/camera assertions
Library/StageHelpPlayChecks.txt — latest main-suite reruns
Library/SoloEconomyPlayChecks.txt — all ten retry/result checks
Library/MobileTutorialInteractionChecks.txt — detailed quiz checks
Library/SceneReliabilityPlayChecks.txt — control/board/gesture checks
Library/SharedHelp_<trial>_<width>x<height>.png — Help layout captures
Assets/Editor/AndroidTrialUiChecks.cs — opt-in audit harness

Device verification still required
ADB reported no attached Android device or emulator. No actual Android build
was executed. Editor results do not certify device-specific rendering, touch,
video decoding, safe-area behavior or performance. Test the following on-device:
Help -> Begin -> tutorial -> Skip/No/repeat -> gameplay; board/quiz/reader Back;
inventory selection; retry; Main Menu return/re-entry; background/resume; and
Rules of Inference Book camera during repeated page flips.

Preservation
The originally open PRELIM scene was left unsaved rather than overwritten.
The harness uses playModeStartScene and saves a separate recovery copy before
testing a dirty scene. Existing unrelated workspace changes were preserved.
Source trial scenes were not intentionally saved by this audit.
