# Rules of Inference

## Standalone RulesOfInference scene

`Assets/Scenes/RulesOfInference.unity` reuses the existing altar, player, camera,
crystal prefab and input system. `StandaloneStageSelection` selects stage 3 before
shared managers initialize. PRELIM retains its existing puzzle mode.

The standalone stage now selects one question from each of five banks in
`InferenceStageQuestions`: ten distinct premise structures per bank, five
questions total per playthrough. The correct-rule families are disjoint:
Simplification, Modus Ponens, Modus Tollens, Hypothetical Syllogism, and
Constructive Dilemma. Compound propositions, negation, longer chains, and
combined dilemma premises increase the difficulty; changing the scenario is
additional variation. All ten existing curriculum rules remain crystal choices.
`requiredRounds` is set to five at runtime in the standalone scene; PRELIM keeps
its existing mode. Each bank's inference was checked against all 16 assignments
of p, q, r, s and must have at least one satisfying assignment.

The standalone puzzle has two separate checks:

1. Approach the altar and drag the small sentence word bank into gray blanks.
   Only required words are offered, including the logical connectors relevant
   to the selected question.
   **Check argument** validates word placement only. Wrong answers remain editable
   with generic feedback; no specific blank or rule is revealed.
2. Correct premises lock and hide the word bank. The configured four to eight
   Rule Crystals appear at the existing towers. The correct rule is included with
   randomized distractors and shuffled positions. Use **Back**, then the hand
   or **Collect Rule Crystal** within pickup range. Return to **View Board** and
   tap **Place Rule Crystal**; this validates immediately. A wrong crystal is
   rejected and removed, while the correct one remains available. A correct
   match reveals the conclusion and completes one challenge. The next challenge
   waits for a new book interaction. The shared 18-minute exploration countdown
   pauses for Help, practice, board viewing, and the wait between challenges.

The board never shows the current rule, proposition-key formula, or conclusion in
advance. Leaving and returning preserves unfinished sentence placements and the
current crystal search. Rule labels remain visible during carrying and placement.
The altar socket is a physical location independent of the camera-facing board.

`InferenceAltarAccess` creates an invisible stair support from the existing altar
mesh without changing its appearance or transforms. Movement and jumping remain
available during tutorial altar approach. `InferenceCrystalInteraction` shares
pickup logic between the hand and the context button. `InferenceCrystal` restores
solid collision synchronously before dropping, chooses a clear supported landing
point, and returns the same object to its origin after a fall or sustained
penetration into geometry. Recovery never spawns a duplicate crystal.

`RuleOfInferencePuzzle` stores sentence parts, expected words, hidden correct rule,
conclusion and puzzle ID. `DraggableWord` / `DropZone` handle pointer drag and snap,
replacement and removal. `InferenceChallenge.Stage` owns the two-stage flow.
`RuleDiamond` holds rule type, display name, abbreviation, crystal and world label.
`ConclusionReveal` owns the success fade, sound and particles. Crystal carrying
uses `GrabbableObject` and the existing player's hold point and interaction action.

## Validation and authoring

`InferenceStagePlayChecks` is opt-in via the Editor SessionState flag
`Inference.StageChecks`. It checks Help pages, actual jumping and controller stair
walking, tutorial board and crystal collection, both real pickup methods,
immediate drops, fall and trapped-object recovery, and five-question progression.
The last QA answer bypasses the production account unlock branch and previews
the completion UI without modifying player progression. Existing legacy checks
remain available for PRELIM's original content.

`InferenceDragSetup.Configure()` is an opt-in authoring helper restricted to the
standalone scene outside Play Mode. It updates the existing board hierarchy,
camera framing, physical socket, spawn points and neutral MP preview. Nothing
runs automatically in the Editor. `ruleDiamondCount` supports four to eight choices.
`requiredRounds` is five in the standalone scene. `onChallengeCompleted` and the existing stage
unlock are triggered once when the required number of puzzles is solved.

## Existing PRELIM flow

PRELIM's `InferenceChallenge` has no `dragPuzzle` assigned and retains its original
typed-premise and conclusion-crystal flow. `InferenceSetup` and `InferencePlayChecks`
remain the legacy authoring/check tools. The standalone changes do not add shared
multiplayer puzzle-state synchronization or saved-game persistence.

## Curriculum source

The five bank patterns also follow the standard forms in
[Deductive reasoning and argument forms](https://people.math.carleton.ca/~kcheung/math/books/giam-ON/html/sec-deduct.html).

Forms follow **IT 105_Discrete Structures 1_2nd Sem_Prelim_Week 3.pptx**:
MP (slide 8), MT (10), HS (12), DS (14), CD (16–17), SIMP (19), CONJ (21),
ADD (23), DN (25), and TAU (27). CD preserves the combined conditional premise.
TAU follows the slides' idempotence forms `p OR p -> p` and `p AND p -> p`.
