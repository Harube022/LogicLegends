# Rules of Inference

## Standalone RulesOfInference scene

`Assets/Scenes/RulesOfInference.unity` reuses the existing altar, player, camera,
crystal prefab and input system. `StandaloneStageSelection` selects stage 3 before
shared managers initialize. PRELIM retains its existing puzzle mode.

The standalone puzzle has two separate checks:

1. Approach the altar and drag the small sentence word bank into gray blanks.
   Only required words are offered; AND, OR and NOT remain printed in the argument.
   **Check argument** validates word placement only. Wrong answers remain editable
   with generic feedback; no specific blank or rule is revealed.
2. Correct premises lock and hide the word bank. Four visually identical physical
   diamonds appear around the altar, each labeled with a different rule. The correct
   rule is always included with three randomized distractors and shuffled positions.
   Choose **Explore / return**, use the existing interaction control to pick up a
   diamond, and carry it back to the altar. **Place diamond here** places the held
   diamond. Only then does **Check rule** appear. A wrong diamond returns to its
   original position; the conclusion stays hidden. A correct check fades in the
   data-derived conclusion, plays the WIN sound and altar particles, completes and
   locks the puzzle. **Next challenge** starts a fresh question.

The board never shows the current rule, proposition-key formula, or conclusion in
advance. Leaving and returning preserves unfinished sentence placements and the
current diamond stage. Rule labels remain visible during carrying and placement.
The altar socket is a physical location independent of the camera-facing board.

Ten existing `InferenceRule` assets supply six scenarios per rule. A shuffled bag
covers all rules before repetition and avoids each rule's previous scenario.
Recognition uses double-negation elimination so its premises differ from Addition;
Tautology uses the curriculum's repeated-proposition forms. Natural sentence blanks
and conclusions derive from those same statements and logical forms. Shared rule
assets and PRELIM scene objects were not rewritten.

`RuleOfInferencePuzzle` stores sentence parts, expected words, hidden correct rule,
conclusion and puzzle ID. `DraggableWord` / `DropZone` handle pointer drag and snap,
replacement and removal. `InferenceChallenge.Stage` owns the two-stage flow.
`RuleDiamond` holds rule type, display name, abbreviation, crystal and world label.
`ConclusionReveal` owns the success fade, sound and particles. Crystal carrying
uses `GrabbableObject` and the existing player's hold point and interaction action.

## Validation and authoring

`InferenceDragChecks.Content()` checks all 66 recognition scenarios, truth-table
entailment for each form, simple word banks, invalid answers and shuffled draws.
With a spawned player in standalone Play Mode, run
`InferenceDragChecks.StartPlayChecks()` and read `InferenceDragChecks.PlayReport`.
It covers all ten rules with pointer raycasts and drag/drop, premise locking,
4–5 unique diamonds, actual player interaction pickup, carrying, altar placement,
wrong-diamond retry, concealed conclusions, success and replay. Tests suppress
progression writes during their temporary Play Mode rounds.

`InferenceDragSetup.Configure()` is an opt-in authoring helper restricted to the
standalone scene outside Play Mode. It updates the existing board hierarchy,
camera framing, physical socket, spawn points and neutral MP preview. Nothing
runs automatically in the Editor. `ruleDiamondCount` supports four or five choices.
`requiredRounds` defaults to one. `onChallengeCompleted` and the existing stage
unlock are triggered once when the required number of puzzles is solved.

## Existing PRELIM flow

PRELIM's `InferenceChallenge` has no `dragPuzzle` assigned and retains its original
typed-premise and conclusion-crystal flow. `InferenceSetup` and `InferencePlayChecks`
remain the legacy authoring/check tools. The standalone changes do not add shared
multiplayer puzzle-state synchronization or saved-game persistence.

## Curriculum source

Forms follow **IT 105_Discrete Structures 1_2nd Sem_Prelim_Week 3.pptx**:
MP (slide 8), MT (10), HS (12), DS (14), CD (16–17), SIMP (19), CONJ (21),
ADD (23), DN (25), and TAU (27). CD preserves the combined conditional premise.
TAU follows the slides' idempotence forms `p OR p -> p` and `p AND p -> p`.
