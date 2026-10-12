# Formal vs. Informal Proofs stage

Open `Assets/Scenes/Formal vs. Informal Proofs.unity`. The existing Proofs Library
room, Player, GameInput, mobile joystick/jump/interact controls, Book Statue model,
S2L4 Board, low-poly scenery, audio clips, EventSystem, and result panel are reused.
`ProofsLibraryStageController` owns this stage's ten-minute countdown and five
challenge state transitions. Completing challenge 5 calls the shared
`StageCompleteManager.UnlockStage(7)` progression path. The stage uses its own
result panel for victory and time-out because the shared `GameOverManager` and
`StageCompleteManager` components depend on other stages' LevelManager UI and
reward setup; attaching them here would disturb those stages.

The Book begins/resumes each challenge and validates only on a later interaction.
The timer starts at 10:00, pauses after each correct answer, never resets between
challenges, and loses 10 seconds on every incomplete or incorrect Book confirmation.
All scroll placements and physical pedestal slots remain editable after a mistake.
Time played counts active
challenge time separately from penalties. Stage victory displays time played,
five correct submissions, and the number of incorrect confirmations.

`Assets/Resources/ProofChallenges.asset` stores the five configurations:

1. Two valid Modus Ponens proofs of the **same** argument, one explicit formal
   derivation and one coherent informal explanation.
2. Four valid scrolls: two formal derivations and two informal explanations.
3. Shuffled step tablets for deriving R from P, P→Q, Q→R. Six premise orders are
   configured as valid; prerequisite IDs must appear before either derived step.
4. A missing justification with four rule tablets. Modus Ponens is required.
5. An informal P→Q→R explanation and separate shuffled statement and rule
   tablets. The selected proof must finish with R and justify both derived steps.

Challenges 1–2 use the same parchment-and-roller `ProofWorldPedestal.prefab` as
the later puzzles. Two source pedestals face each other across a wide central walkway
in Challenge 1; Challenge 2 uses two facing pairs beside the outer walls, clear of
the Formal and Informal altars. The older
`ProofScrollDisplay.prefab` objects remain as hidden interaction references so
their scene and controller references stay intact. There is no central scroll table.
Each source shows its scroll letter and a short proof preview. Interact opens the
full scroll reader with Carry and Close buttons; a small scroll model follows the player
and is reparented to its altar on placement. Placed scroll models scale up and
rearrange into a one-, two-, or four-position display so they remain visible.
Interacting with an altar while empty-handed opens **Take Back a Scroll**, which
lists only the scrolls on that altar by letter. The player chooses exactly which
one to carry again; the other placements remain in place. Cancel closes the
picker without changing anything. Placed scrolls show their letters in world
space, and the take-back picker shows a proof excerpt for each available letter.
The picker does not identify incorrect
classifications. The Formal and Informal altars use the two existing
`ProofClassificationPedestal.prefab` instances.

Challenges 3–5 use the reusable `ProofWorldPedestal.prefab` with parchment faces
and wooden scroll rollers.
The player walks to a statement or rule tablet and presses Interact, then walks
to a numbered STEP or RULE pedestal and presses Interact again to place it.
Selecting another tablet replaces an occupied slot; interacting with a filled
slot while empty-handed removes it. Challenge 3 uses five statement sources and
five step slots in paired rows toward the sides; Challenge 4 uses two inward-facing
pairs of rule sources and one centered rule slot; Challenge 5 uses all four side columns. The
enlarged back-wall board shows the current
problem and instructions; the Book Statue confirms the completed arrangement.
Pedestals are gray normally, blue while selected/occupied, green after a correct
confirmation, and briefly red after an incorrect one. Red feedback applies to
all active pedestals, so it does not reveal an individual mistake. The proof is
checked against acceptable orders, unique placements, prerequisites, matching
justifications, and the final conclusion.

The scene also has the shared `Admin Debug` controller from PRELIM and Setssss.
Its Admin button starts visible and opens the debug panel above `Proofs HUD`.
Use **Admin Button Enabled** on `Admin Debug` in the Inspector to show or hide
the button in this scene, or call `AdminButtonOn` / `AdminButtonOff` at runtime.
**Clear Current Challenge** is available only during an active challenge; it
uses the normal completion flow and pauses the timer before the next challenge.
**Clear Current Condition** remains disabled because this stage has no condition
sub-rounds. The panel pauses gameplay while open and restores it when closed.

Exact scene wiring on `ProofsLibraryStageManager`:

- `catalog` → `Assets/Resources/ProofChallenges.asset`; `bookStatue` →
  `ProofsLibrary/Book Statue`; `worldBoard` → `ProofsLibrary/Physical Proof Stations`.
- `scrolls`, `scrollSources`, `scrollStatus`, and `carriedVisuals` → four A–D entries in that order;
  `pedestals`, `pedestalEmblems`, and `pedestalCounts` → Formal, Informal.
- `boardText` → `ProofsLibrary/Current Challenge`; timer, prompt, reader panel,
  reader title/body, Carry/Close, results panel/text, Retry/Main Menu
  → their names under `Proofs HUD`.
- `audioSource` → manager AudioSource; correct/incorrect clips → existing
  SOFTWIN and DAMAGETAKEN1 clips. Interaction range is 3 m, starting seconds
  600, wrong penalty 10.

Editor commands: **Logic Legends > Configure Formal vs. Informal Proofs Scene**
builds an empty/unconfigured scene, while **Logic Legends > Update Proofs Library
Five-Challenge Scenario** upgrades a scene that already has `ProofsLibrary`.
**Logic Legends > Arrange Proofs Library Layout** reapplies the spacious room
layout without resetting challenge data: enlarged board on the back wall,
altars apart on the sides, scroll source pedestals in the middle, and Book Statue
near the entrance. It also
aligns the Book and board colliders with their visible meshes. The decorative
archive/sign are kept inactive in the scene.
**Logic Legends > Add Proofs Library Admin Button** restores the scene's Admin
component if it is missing, while preserving an existing on/off setting.
**Logic Legends > Build Physical Proof Stations** restores the reusable pedestal
prefab, four shared URP color-state materials, the source pedestals, scroll
appearance, and board fit without rebuilding the room.
Both preserve the original heavy S2L4 Stage object inactive. The S2L4 Board and
Stage importers map their StoneMat slot to the URP material in the same folder.

Editor Play-mode checks completed: initial timer paused and all scrolls hidden;
Challenge 1 formal/informal scroll text and correct Book confirmation; Challenge 2
four scrolls and timer continuity; Challenge 3 physical step slots, removal,
replacement, and valid derivation; Challenge 4 wrong rule (red feedback and
preserved selection), removal, corrected rule, and green feedback; Challenge 5
all 20 physical stations, matching steps/rules, and correct confirmation.
A separate wrong Challenge 1 submission preserved both placements
and allowed retrieval; two consecutive wrong submissions each deducted 10 seconds.
An incomplete Book confirmation also deducted 10 seconds. Scene reload retained
the catalog and references; Unity
reported no compilation errors, missing scripts, or missing render materials.

Manual device checklist: walk to the Book, altars, tablet sources, and numbered
pedestals using the joystick; tap Interact, Carry, Close, Retry, and Main Menu;
check the reading panel and pedestal text on a target phone aspect
ratio; complete one full five-challenge run; verify audio and scene transition
to the main menu. These touchscreen and device-size checks were not automated.
