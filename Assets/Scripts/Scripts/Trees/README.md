# Trees gameplay

Implemented in `Assets/Scenes/Trees.unity` for Android landscape, using temporary assets already in the project. Open Trees and press Play; no manual Inspector wiring is required. The existing final-term Trees menu card now loads this scene.

## Created scripts and responsibilities

| Script | Responsibility |
| --- | --- |
| `TreeChallengeCatalog.cs` | ScriptableObject catalog, serializable node/edge/challenge data, five default configurations. |
| `TreeAnswerValidator.cs` | Root/parent/child/leaf identification, directed rooted-tree validation, exact required edges, depth, height, connectedness, cycles and spanning trees. |
| `TreeNode.cs` | Editable world label, selected/target states and green/red confirmation feedback without modifying shared materials. |
| `TreeBoardController.cs` | Node spawning, lightweight connection rendering, endpoint selection, adding/removing edges, cleanup and edit guards. |
| `TreeStageManager.cs` | Book state machine, input subscription, timing, feedback/stun, covered transitions, lesson visibility and results. |
| `TreeUIController.cs` | Trees-owned runtime HUD, interactive tree minimap, actions, number choices, two-page field guide and result screens. |
| `TreesPlayChecks.cs` | Editor-only Play Mode flow checks; absent from Android builds. Suppresses reward persistence while testing. |
| `Assets/Editor/TreesStageSetup.cs` | One-time scene authoring; refuses to overwrite configured Trees. Copies a disposable PRELIM snapshot to preserve prefab links and cross-root references, then deletes only that snapshot. |
| `Assets/Editor/TreesStageChecks.cs` | Reproducible validator assertions, including every subset of the final graph. |

## Existing files changed by this task

- `TeleportManager.cs`: adds Trees to the existing covered-transition scene check. Other pre-existing edits in this file are preserved.
- `Main Menu.unity`: assigns `Trees` to the existing stage 10 card's scene-name field. Other pre-existing menu edits are preserved.
- `EditorBuildSettings.asset`: enables/adds Trees while preserving the existing scene list.
- `Trees.unity`: authors the previously blank new scene. Shared prefabs, PRELIM, Graph and `artstyle.md` are not rewritten by this task.

## Copied PRELIM systems

Main Camera, Directional Light, CameraPivot, GameInput, EventSystem, LevelSpawner, LevelMenuManager, MobileInputUI, Canvas 1, CM_ThirdPersonCam, Inventory_Managers and TeleportManager are copied from PRELIM. CameraPivot, LevelSpawner, LevelMenuManager, Canvas 1 and CM_ThirdPersonCam retain their original prefab connections; nothing is unpacked. Cross-root references are preserved through scene copying, and Trees-specific spawn/camera/pause references are assigned as instance overrides.

Unused truth-block inventory and legacy PRELIM panels are inactive overrides, rather than removed prefab contents. Original joystick, look, jump, Interact and Settings transforms and sprites remain in use. CameraPivot's legacy controller is disabled because the copied Cinemachine rig controls the spawned player. Original pause/resume/menu callbacks are wired to the copied menu manager.

## Prefabs, board and Inspector setup

`Assets/Prefabs/Trees/TreeNode.prefab` contains a large collider, simple temporary cylinder face using the existing URP rendering pipeline, and a separate IMPACT TMP label. Nodes face the player on a vertical board. Replace the temporary face with the WIP node artwork when ready, retaining `TreeNode`, its label and collider references.

`TreeConnection.prefab` contains a local-space LineRenderer, two endpoints, 0.055-unit width, four cap vertices and a shared unlit blue material. It needs no Rigidbody or per-edge physics. A connection is selected through its two endpoint nodes. World edges and UI minimap edges are generated from the same data; the diagram is editable UI, not baked artwork.

The scene's `Trees Stage` object already references:

- `TreeChallenges.asset`, the Tree Board controller and both prefabs.
- Trees Book Statue and Trees Spawn.
- Trees UI host (`Canvas 1/SafeArea`) and original Gameplay_Interface.
- Existing `GraphStageClock`, `GraphCompletion` (stage number 10) and `GameOverManager` components.
- Existing correct, incorrect and game-over audio clips.

Change countdown duration (default 300 seconds) or feedback/stun duration (1.1 seconds) on TreeStageManager. Change challenge data in `Assets/Prefabs/Trees/TreeChallenges.asset`; each entry exposes type, objective, lesson, node positions, initial/required edges, root, target and identification rule. Node positions are normalized board coordinates. Root depth and level are 0. Depth/height answers are calculated, not serialized constants. The default number choices cover 0–5.

The garden currently reuses existing Wood3, SimpleNaturePack trees, Book_Statue, original settings-panel wood and existing shared UI assets. Floor boundaries keep the player within the temporary platform. The new assets in progress are not required.

## Five configured challenges

1. **Terminology:** A is root; A-B, A-C, B-D, B-E, C-F, C-G. Select A and confirm. Parent/child/leaf identification is also supported by configurable data.
2. **Build:** five disconnected nodes. Required directed links are A-B, A-C, B-D, B-E. Exact matching, one parent per non-root node, connectedness and absence of cycles are required.
3. **Repair:** initial links A-B, A-C, B-D, C-E. Remove C-E and create B-E. The complete required structure is checked; adding an extra parent is prevented.
4. **Depth:** A-B, A-C, B-D, B-E, D-F. Select the depth of F: three edges. The validator also supports height and other target nodes.
5. **Spanning tree:** undirected A-B, A-C, B-C, B-D, C-D. Remove links until all four vertices stay connected with three edges and no cycle. All eight valid spanning trees are accepted. Re-adding an original edge supports undo; foreign graph edges are refused.

## Book, timer, feedback and results

Approach the Book Statue and use the original Interact control or the contextual Book action. First interaction activates the waiting challenge and resumes the single countdown; subsequent interactions confirm. Select nodes directly in the world or on the minimap. For editable challenges, select the parent and then child and tap CONNECT; REMOVE accepts either endpoint order. CLEAR PICK clears selection only, never the structure.

Validation consumes repeated confirmations immediately. Correct submissions play the existing correct clip, flash nodes green, pause the timer and freeze the player during feedback. The next challenge replaces the board under the existing black teleport fade, returns to the spawn and waits for activation. No new timer is created and remaining time is not reset.

Incorrect confirmation plays the existing damage/incorrect clip, flashes nodes red, increments mistakes once and deducts exactly ten seconds. The existing Player.ToggleControl pattern provides a brief stun. Connections and picks are preserved. If the penalty exhausts the countdown, Game Over wins immediately; no later coroutine restores play.

The already-existing GraphStageClock is reused unchanged because it supports an independent countdown and active-play stopwatch without PRELIM's quiz/static-session coupling. Active time excludes correct transitions, waiting, pause and lesson reading; penalties do not inflate it. The already-existing GraphCompletion is reused unchanged with `stageNumber=10`, preserving atomic account reward/progression behavior. GameOverManager supplies the existing loss audio; Trees supplies its own result panel.

Victory requires all five correct submissions and shows TREES STAGE COMPLETE, Time Played, Correct and Mistakes. Game Over stops the timer and blocks gameplay. Both screens offer Play Again and Main Menu through covered scene transitions. Trees is the final configured topic, so there is no new next-stage destination.

## UI/art scope

Original reference images and host prefabs/code were inspected before edits. WoodPanel and WoodButtonTeal/Green/Red are reused unchanged from the current shared theme, with editable TMP labels. The original settings background is reused on the world board. No raster artwork was generated or recolored, and no GraphHUD sprites or Graph-local geometry were applied to Trees.

Content uses the documented calm dark-teal surface, gold headings/edges and pale text. The field guide uses the existing wood cover, two cream ReadingPaper pages, dark text and central brown spine. IMPACT and Liberation Sans are assigned explicitly. Mathematical conventions use readable text and `V - 1`; no unsupported arrow or special-symbol fallback is required. Original simple circular touch controls are preserved. The minimap's circles reuse an existing joystick-circle sprite with separate labels. Decorative elements do not intercept pointer events.

## Actual validation (2026-10-11)

- Unity 6000.3.6f1 compiled the new scripts without compile errors.
- **61 validator assertions passed**, including all 32 edge subsets of the final graph and all eight valid spanning trees; checks cover root/parent/child/leaf rules, exact construction/repair, direction, self/duplicate/missing-node edges, cycles, disconnectedness, depth and height.
- **20 Play Mode assertions passed twice**, including the final implementation: five rounds in order, initial/transition timer pauses, exact ten-second penalty, duplicate confirmation protection, retained wrong work, edit guards, book reactivation, lesson pause/resume, computed depth, spanning-tree completion and victory/Game Over exclusivity. Test reward writes were suppressed.
- Original Interact Button callbacks were invoked through their copied UI events: activation, minimap node A selection and confirmation produced one correct submission. Runtime board contained exactly seven nodes and six edges after preview cleanup.
- A five-second test clock exhausted by a wrong confirmation produced GAME_OVER, zero time, stopped clock, one mistake, zero correct and blocked input; duplicate confirmation had no effect.
- Play Again's result button callback reloaded Trees to waiting, zero correct, 300 seconds and one local player. Settings pause/resume blocked/unblocked gameplay and restored time scale.
- Trees -> Main Menu -> existing stage 10 Trees entry -> Trees succeeded. Re-entry had one player and one Canvas 1.
- Connected prefab paths were checked in Unity after authoring. Shared prefab files and PRELIM were not modified.
- Game View was inspected at **1920 x 1080 landscape** for gameplay, field guide, selected answers, feedback, victory and timeout. Trees panels were moved clear of Settings and copied touch controls. The simulator was never opened or changed; no device matrix was run.
- Screenshots are local under `Temp/TreesQA/`: `trees-landscape-final.png`, `trees-book-final.png`, `trees-victory.png` and `trees-game-over.png`.

## Re-run and remaining checks

Use **Tools > Logic Legends > Validate Trees Logic** outside Play Mode. For the five-round flow, enter Play Mode in Trees and add `TreesPlayChecks` to Trees Stage; its Result property/log reports success. The test component must remain editor-only and is not saved into the scene.

- [x] Core validators and alternate spanning trees.
- [x] Five-round state flow and no early victory.
- [x] Wrong work retention, exact penalties, duplicate confirmations and stun recovery.
- [x] Timer pause/resume, transition fades and no reset between rounds.
- [x] Original Interact callbacks, minimap selection and preview cleanup.
- [x] Timeout/victory exclusivity, result stats and retry.
- [x] Pause/resume, return-to-menu and menu re-entry.
- [x] Explicit fonts, legible text, original artwork and single landscape visual check.
- [ ] Physical Android taps, simultaneous joystick/look multitouch, safe-area cutouts and on-device performance.
- [ ] Signed-in male/female database spawning and real account reward/progression writes. Existing LevelSpawner/Firebase paths are reused, but no production reward writes were used for validation.
- [ ] Final WIP node/branch/pedestal/environment artwork replacement and device audio listening.

Temporary world models are intentionally basic. The connected interaction system and catalog are ready for replacement artwork. No Android APK build, physical device run, broad cross-device validation or production persistence test is claimed. The project also has pre-existing unused-field warnings and can report an InputActions disposal warning on editor shutdown; those unrelated systems were not rewritten.
