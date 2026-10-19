# Direct and indirect proofs

The configured scene contains `DirectIndirectProofStageManager` and `TrialOfProofs`.
Author question content in `Assets/Resources/DirectIndirectProofChallenges.asset`.
`TrialProofChallenge.IsCorrect` is unchanged; add another complete sequence to
`acceptedOrders` when a second proof ordering should be accepted.

`DirectIndirectProofStageController` retains the five existing tasks and their
ten-entry banks. At run initialization it selects two distinct bank indices for
each challenge. Each of the ten questions requires a fresh Book Statue activation;
only completing both questions advances the challenge. Retry reloads a new schedule.
The Admin answer viewer marks both selected questions using that same schedule.

Challenge 1 classifies a supplied argument at the Direct or Indirect altar.
Challenges 2–4 order direct, contrapositive, and contradiction arguments on the
active altar's drag-and-swap board. Challenge 5 also selects a proof strategy.
Back keeps the order; the book confirms the entire answer. There is no scroll
collection requirement in the active scene's altar-based gameplay.

The shared 600-second countdown, normal reading/ordering time, validation, retained
wrong arrangements, and 10-second validated-answer penalty are preserved. Correct
answers pause time until another book interaction. CorrectCount now counts ten
questions; the HUD, board, full-problem reader, results, and Help distinguish question
progress from the five challenges.

`DirectProofsGuide` provides the PRELIM-themed first-entry/reopenable Help, isolated
interactive practice, and one introduction per challenge per run. Movement uses
PRELIM's 0.3 joystick dead zone, 0.7 seconds of intentional movement, and 1.5 meters
of actual travel. Jump practice observes the player's successful jump and ascent.
Practice interaction uses a visible cloned stand and the existing hand button.
Classification and actual drag-card practice use a separate cord-length example;
`ProofAltarOrderingBoard.ShowPractice` owns a cloned order and never calls the real
stage selection, start, or validation methods.

Tutorials share `PrelimTutorialSkipControl`. Initial Skip opens the same Yes/No
confirmation; challenge-introduction Skip returns directly to the waiting book.
Help and all tutorials suspend the real countdown. The guide restores prior input,
cursor, control visibility, camera input drivers, and time scale. A temporary Cinemachine camera smoothly hands control back to the existing gameplay camera; the guide never writes the rendered camera transform or disables its Brain. Practice
content and event handlers are removed on completion, Skip, cancellation, or disable.

Introductions begin with a bird's-eye view, then an altar close-up and the book.
Named ceiling renderers preserve their prior enabled states. This room's main
ceiling is part of one combined Stage mesh, so `Resources/DirectProofs/ChamberCutaway`
temporarily clips only its upper visual geometry using runtime materials. The
original material references are restored afterward; mesh and collision never
change. Gameplay camera settings are not permanently modified.

The guide uses the existing Canvas, fonts, artwork, safe-area handling, and hand,
jump, and joystick controls. Portrait touch geometry is fitted for this stage and
restored on landscape/exit. Highlights remain children of their actual targets.
Long Help and problem text can scroll; the proof ordering panel scales within its
view area without changing the authored row coordinates used by drag validation.

## PRELIM presentation reuse

`Resources/StageUI/PrelimHelpBook` and `PrelimHelpButton` are reusable visual
prefabs extracted from Propositional Logic's existing Help UI. They retain its
wood, parchment, fonts, green Begin button, page indicator, and responsive book
layout. Runtime callbacks and Direct-specific explanations are bound by the guide;
there are no retained Propositional scene references or duplicate Help panels.
Long pages use a masked scrolling reading area with the original typography.

`PrelimUIPresentation` is shared with Propositional Logic for the 500-by-136
safe-area HUD, control tutorial buttons, and yellow borders. Direct retains its
Trial 5 title, live timer, challenge counter, and two-question counter. Tutorial
instructions use PRELIM's translucent navy panels, target-bound arrows,
`ControlTutorialLayout`, centered Yes/No confirmation, and existing shared Skip.

`PrelimTutorialCameraRig` uses PRELIM's 0.7-second transition timing and exponential
movement. Cinemachine's Brain stays enabled and owns the rendered camera. Blends
use unscaled time during paused introductions; original blend settings are restored
after returning. Ceiling and avatar visuals restore after the return blend, with
immediate cleanup if the guide is disabled or the scene is unloaded.

## Preserved scene wiring and authoring

The manager's Inspector retains its catalog, Book, board, method pedestals, source
tablets, slots, altars, sounds, markers, reader, and results references. The old
physical tablet/rack components remain wired for compatibility; the current five
tasks use the altar interactions described above. `Trial Player Spawn` remains the
spawn reference on `LevelSpawner`. No scene objects or gameplay colliders are
replaced by this onboarding update.

`Logic Legends > Configure Direct and Indirect Proofs Stage` remains a one-time
builder that refuses to run if `TrialOfProofs` exists. The existing room organizer
and label-restoration tools remain separate; do not rerun builders to install the
runtime guide. The chamber's lightweight floor MeshCollider, fitted Book box, and
existing altar/board/scenery colliders are preserved. Floating text stays visual.

`Admin Debug` retains the shared `AdminDebugController`, its visibility preference,
and player-build setting. Clearing an active question uses the same completion
path as an accepted answer; two completed questions are still required per challenge.
The read-only answer viewer shows the actual active question, full banks, methods,
accepted orders, and both preselected questions per challenge. Victory continues
through the existing `StageCompleteManager` unlock path.

## Offline Play Mode checks

Open `Assets/Scenes/Direct&Indirect.unity` outside Play Mode and invoke
`DirectProofsGuidePlayChecks.Begin(size)` through the existing Editor C# tool.
GameView indices used here: `6` desktop, `2` compact landscape, `5` portrait.
The opt-in runner spawns an offline avatar, suppresses account progression writes,
and restores GameView/safe-area settings on exit. It checks intentional controls,
practice isolation, Skip/No/Yes, scrolling/readers, actual raycast access to Skip and
Back, roof/material/camera cleanup, all ten submissions, both distinct questions,
and exact penalties with retained method/order. Reports go to
`Library/DirectProofsGuideChecks.txt`; screenshots go to `Temp/DirectProofsQA/`.
