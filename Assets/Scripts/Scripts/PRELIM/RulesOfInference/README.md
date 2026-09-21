# Rules of Inference

The PRELIM scene's existing `Rules_Of_Inference` group contains `InferenceGameplay`.
`AreaVisibilityManager.TransitionToRulesOfInference()` continues to activate this group
after the Truth Table challenge. The group remains hidden at scene startup.

## Play flow

Approach the existing central altar. Its trigger opens the board and enables
`VCam_RulesOfInference`, using the existing Cinemachine Brain's blend just like
`TruthTableCameraTrigger`. The player is temporarily stopped while typing.
Fill word blanks from the word bank and proposition blanks from the proposition key.
The conclusion is not a text input: use **Explore / continue**, collect a physical
crystal using the existing interaction button, return to the altar, and choose
**Place crystal**, then **Check argument**.

Leaving the board to collect a crystal preserves the current question and typed
answers. Wrong answers highlight the relevant fields; a wrong crystal returns to
its spawn. A dropped crystal that falls off the island returns automatically.
After success, **Next challenge** draws a new rule and example. Disabling and
reactivating the area clears the unfinished question and draws anew on entry.

## Editable content

Ten `InferenceRule` assets live in `Assets/Gameplay/RulesOfInference`.
Each contains a name, abbreviation, instructions, alternative forms, and examples.
Each form defines its premise token rows, conclusion template, and three distractors.

- `{p}`, `{q}`, `{r}`, `{s}`: input blanks for example propositions.
- `[IF]`, `[THEN]`, `[AND]`, `[OR]`, `[NOT]`: operator/word blanks.
- Other tokens: fixed labels, parentheses, or a newline for wrapping long premises.
- Conclusion/distractor templates substitute `{p}` through `{s}` into crystal labels.
- Examples contain four short statements and optional pipe-separated accepted aliases.

Matching ignores capitalization, punctuation, and repeated whitespace. It does not
try to infer arbitrary paraphrases; add accepted alternatives to the example aliases.
Longer content may need wider input fields or more explicit line breaks.

Rules draw from a shuffled bag: all ten appear before the next cycle, with no
immediate repeat across cycles. Each rule avoids repeating its last example.
Crystal locations are shuffled separately; identical visuals do not reveal correctness.

## Curriculum source

Forms follow the supplied **IT 105_Discrete Structures 1_2nd Sem_Prelim_Week 3.pptx**:
MP (slide 8), MT (10), HS (12), DS (14), CD (16–17), SIMP (19), CONJ (21),
ADD (23), DN (25), and TAU (27). CD preserves the combined conditional premise.
DN supports removal and introduction of double negation. TAU follows slide 27's
idempotence forms `p OR p -> p` and `p AND p -> p`; it does not treat an arbitrary
proposition as true without premises. Each rule has six short scenario examples.
The board follows Board.png's word bank, incomplete premises, and separate conclusion socket.

## Integration and authoring

- `InferenceChallenge.requiredRounds` defaults to one. Raise it for a multi-round area.
- `onChallengeCompleted` fires once when the required round count is reached.
  Connect later progression here; the existing scene provides no next-area destination.
- `PuzzleCompleted` and `SolvedRounds` expose progress to other systems.
- The current challenge is local to the player, like the existing PRELIM puzzle logic;
  this does not add shared multiplayer puzzle-state synchronization or saved-game persistence.
- Crystal carrying reuses `GrabbableObject` and `Player`'s existing hold point.
  It does not add conclusion crystals to the Boolean-only Truth Table inventory.
- Player movement, input bindings, camera Brain, and Truth Table scripts remain in place.
  The only player script change prevents subscribed interaction/jump events while control is disabled.
- The altar's visible meshes receive mesh colliders because its imported root collider had no mesh.

`InferenceSetup.Build()` is a one-time Editor authoring helper; it refuses to duplicate
an existing setup and never overwrites edited rule assets. Edit the resulting scene
objects/assets normally. `InferenceSetup.ValidateContent()` checks all configured
forms/examples, rejection of invalid inputs, distractors, and shuffled draws.

## Manual checks

Test stationary entry, mobile input, leaving/re-entering with partially filled blanks,
all ten rules, incorrect and correct crystals, dropping/recovering a crystal, replay,
and disabling the area with a board open. Confirm the player and gameplay camera
regain control after closing. For isolated testing, activate the inference area in
Play mode through the existing AreaVisibilityManager.
