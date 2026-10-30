## Chamber lighting and collision — October 30, 2026

- Preserved the current Stage, ceiling, border, bookshelves, tables, chairs, five original LAMP placements and gameplay references, including the unsaved chamber arrangement present at the start of this change. A safety copy is in `Library/TreesChamberBefore.unity`.
- New art is grouped under `ChamberDecorations`: four wall banners, two chained reading-aisle chandeliers and two small perimeter crystal niches. Existing assets are reused. The main book/board approaches and side aisles remain open.
- Each of the five original lamps and both chandeliers has one warm, shadow-free light and a scene-owned emissive interior. Ambient/daylight fill is reduced; nearby floors have distinct warm pools while unlit walls remain dim. Source-pack materials and global render-pipeline/quality assets are unchanged. Emission flags survive URP import.
- Disabled the four obsolete garden fences at x=±14 and z=-10/18, which blocked empty space within the larger chamber. The original Stage retains floor/wall collision. Added a 76-triangle collider to the visible ChamberBorder to close seams in the inner shell. The book uses one combined static-geometry collider independent of animated pages; the board uses its visible meshes instead of enclosing boxes across empty gaps.
- Play Mode passed a continuous 17-waypoint walk, both side aisles, access around/behind the board, book approaches, physical perimeter containment and 216 boundary ray samples. Each of seven lamps measurably brightens a nearby floor patch. All five practice tutorials and ten real questions passed at three Editor screen sizes. Reports: `Library/TreesChamberPlayChecks.txt` and `Library/TreesRevisionChecks.txt`.
- Mobile cost is bounded to seven shadow-free local lights, 14 new decorative renderers, combined chain meshes and no particles or decoration behaviours. Physical-device performance profiling has not been performed.

## Action-guided Trees onboarding — October 30, 2026

- Help opens automatically on arrival. Let's Begin releases its input/timer snapshot, then starts the first practice introduction. Reopening Help later returns to the previous game state without replaying practice or activating the book.
- Each challenge's separate X/Y/Z/W example now waits for every required action. The instruction and spotlight advance immediately after the expected tap succeeds. Next is only available for explanatory steps; one shared Skip remains available throughout.
- Walkthroughs cover root selection and Clear Pick; building links and undoing/reconnecting a branch; removing a wrong parent and assigning the requested parent; correcting a numerical answer; and removing cycle edges before spanning-tree submission. Practice submission uses the normal validator without real penalties or progress changes.
- Challenge 3's authored banks start as connected, acyclic rooted trees with incorrect requested parent assignments. Repair requires matching every required directed link with no extra edges. The practice does not claim those starting puzzles contain cycles or disconnected nodes.
- Play Mode checks completed every action in all five walkthroughs at 1920×1080, 2960×1440 and 800×480 using Input System touch events. They also verified arrival Help, Help reopening, early/mid-practice Skip, unchanged real data/time, both questions in all challenges, and camera/input restoration. See `Library/TreesRevisionChecks.txt`. These are Editor touch simulations, not physical-device tests.

## Restored Trees workspace — October 28, 2026

- Restored the earlier screen-space instruction sidebar and right-hand board workspace from the previous implementation. The InformationBoard model remains; Submit, Connect, Remove, Clear Pick, Help and Back still use the existing gameplay logic.
- Questions, counters, buttons and world node labels now use Liberation Sans, with bold labels/headings. The diagram uses more of the parchment, preserves authored node/edge data, separates close rows and stops branches at node borders.
- The Trees Book Statue matches PRELIM's measured visible height (about 4.27 m). Its static collision bounds follow its new size. It is offset clear of spawn; the InformationBoard has separate solid base/frame colliders. Existing prefab page animation and indicators remain attached.
- Each challenge has one separate X/Y/Z/W practice introduction using the existing board-facing camera. Next and the shared Skip button restore the book-controlled start. Practice cannot alter the real tree, selected questions, timer, penalties, retries or rewards. Question 2 does not repeat the introduction.
- Play Mode verified all ten questions, all five introductions, touch input at three landscape sizes, skipping during a blend, Help/Back restoration, and live CharacterController collision from three book approaches and the board front. All fifty bank diagrams have separated nodes and valid solutions. Help clips were refreshed for this restored UI.

The previous revision notes below describe the superseded full-board UI.

## Current Trees revision — October 28, 2026

- The existing InformationBoard model holds the live world-space question, diagram, selection and editing controls. The old visual board and separate feedback panels are removed. Submit validates; Back keeps work and restores exploration.
- Five challenges now require two distinct bank questions each. Each bank retains ten entries: five foundation questions and five harder structures. The second question has more nodes, deeper paths, additional repair links or a larger spanning task. Both picks remain fixed for the attempt.
- The compact Trial 10 HUD includes the shared timer and both challenge/question counters. Help uses the shared scrollable guide and preserves the previous timer/input state.
- Admin can clear a question or the remainder of a challenge. Its answer viewer lists all fifty entries, identifies both selected questions and derives complete solutions from validation data. Existing authentication and reward exclusion remain in place.
- Help has two actual, isolated gameplay clips: book activation and using the board. They share one question, use real touch input, purpose-built close-ups, short captions and encoded two-second fades. Recording effects live only in Editor scripts.
- Verified fifty solvable entries, 5,000 ordered random selections, normal completion of all ten questions, correction/penalty behavior, Help/Back and mobile layouts at 1920×1080, 2960×1440 and 800×480. Evidence is in Library/TreesRevisionChecks.txt and Library/TreesHelpPlaylistChecks.txt.

The earlier implementation notes below are historical and describe the previous five-question board/sidebar.

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
| `TreeBoardFocus.cs` | Activates the dedicated scene camera `CM_TreesBoardCam` through the existing Cinemachine brain, freezes look input and returns to the exploration camera on exit. |
| `TreeUIController.cs` | Exploration HUD with read-only minimap, focused board with projected screen-space node targets, actions, number choices, two-page field guide and results. |
| `TreesPlayChecks.cs` | Editor-only Play Mode flow checks; absent from Android builds. Verifies camera switching and reward-free completion without persistence suppression. |
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
- Trees UI host (`Canvas 1`) and original Gameplay_Interface. Shared touch controls keep their existing SafeArea parent; Trees content does not inherit it.
- Trees-owned TreeBoardFocus, dedicated `CM_TreesBoardCam`, output camera and board references. The shared exploration camera prefab remains connected.
- Existing `GraphStageClock` and `GameOverManager` components. Trees has no `GraphCompletion` component or completion persistence call.
- Existing correct, incorrect and game-over audio clips.

Change countdown duration (default 600 seconds) or feedback/stun duration (1.1 seconds) on TreeStageManager. Change challenge data in `Assets/Prefabs/Trees/TreeChallenges.asset`; each entry exposes type, objective, lesson, node positions, initial/required edges, root, target and identification rule. Node positions are normalized board coordinates. Root depth and level are 0. Depth/height answers are calculated, not serialized constants. The default number choices cover 0–5.

The garden currently reuses existing Wood3, Book_Statue, original settings-panel wood and existing shared UI assets. The two decorative SimpleNaturePack trees beside the board were removed at the user's request. Floor boundaries keep the player within the temporary platform. The new assets in progress are not required.

Trees Book Statue has a scene-local solid BoxCollider fitted to its model and an Animator using `Assets/FINAL_ASSETS/MAPS/REMADE/BookFlip.controller`, matching the truth-table statue's looping page animation. Root motion is disabled; the shared prefab stays connected and unchanged. On 2026-10-12, Play Mode checks confirmed character collision, animated page rotation, board entry and exit with restored controls. No physical Android validation was performed.

## Five configured challenges

1. **Terminology:** A is root; A-B, A-C, B-D, B-E, C-F, C-G. Select A and confirm. Parent/child/leaf identification is also supported by configurable data.
2. **Build:** five disconnected nodes. Required directed links are A-B, A-C, B-D, B-E. Exact matching, one parent per non-root node, connectedness and absence of cycles are required.
3. **Repair:** initial links A-B, A-C, B-D, C-E. Remove C-E and create B-E. The complete required structure is checked; adding an extra parent is prevented.
4. **Depth:** A-B, A-C, B-D, B-E, D-F. Select the depth of F: three edges. The validator also supports height and other target nodes.
5. **Spanning tree:** undirected A-B, A-C, B-C, B-D, C-D. Remove links until all four vertices stay connected with three edges and no cycle. All eight valid spanning trees are accepted. Re-adding an original edge supports undo; foreign graph edges are refused.

## Book, timer, feedback and results

Approach the 3D Book Statue and use the original Interact control or OPEN TREE BOARD action. This activates the separate, initially inactive `CM_TreesBoardCam`, matching Rules of Inference's camera activation pattern. Its fixed orthographic framing is beyond the player/statue and faces the existing tree straight on. Cinemachine blends to it while the main camera remains the normal rendering output; Trees no longer disables the brain or manually changes the main camera's position, rotation or lens. The brain's scene-local lens-mode override permits the board's orthographic lens and defaults back to perspective for exploration. The output viewport reserves the left quarter for the sidebar and is restored on exit.

Movement, look, jump, Interact and Settings controls are hidden, and player/global gameplay input is blocked. Board editing waits for the camera blend to finish. Trees-owned screen-space buttons track the projected world nodes, creating large 2D touch targets without duplicate node labels or a second editable tree. The exploration minimap is read-only.

First entry activates the waiting challenge and resumes the single countdown. Select a node or number and tap CONFIRM in the focused view. For editable challenges, select the parent and then child and tap CONNECT; REMOVE accepts either endpoint order. CLEAR SELECTION clears picks only, never the structure. EXIT BOARD restores the camera, look controllers and original exploration controls. Connections, selections and number answers are retained when exiting and reopening; the active countdown keeps running outside the board. Editing and confirmation require board mode.

The focused sidebar contains a compact objective card with separate progress, challenge title and left-aligned instructions. Its height follows the text; feedback, timer and Field Guide stack beneath it. A calm backdrop prevents scenery from competing with the sidebar. The sidebar and camera viewport both use the full canvas's 25% boundary, preventing the former SafeArea/full-screen coordinate mismatch and exposed strip. Exit Board has its own safe-inset container; bottom actions and the edge-adjacent exploration Field Guide receive only their required inset. Centered content, lessons and results use the full canvas. Closing the lesson returns to the board with movement still locked. Correct round transitions close the board under the existing teleport flow and restore exploration for the next activation. Victory and timeout restore the exploration camera but keep gameplay controls blocked behind the result screen.

Validation consumes repeated confirmations immediately. Correct submissions play the existing correct clip, flash nodes green, pause the timer and freeze the player during feedback. The next challenge replaces the board under the existing black teleport fade, returns to the spawn and waits for activation. No new timer is created and remaining time is not reset.

Incorrect confirmation plays the existing damage/incorrect clip, flashes nodes red, increments mistakes once and deducts exactly ten seconds. The existing Player.ToggleControl pattern provides a brief stun. Connections and picks are preserved. If the penalty exhausts the countdown, Game Over wins immediately; no later coroutine restores play.

The already-existing GraphStageClock is reused unchanged because it supports an independent countdown and active-play stopwatch without PRELIM's quiz/static-session coupling. Active time excludes correct transitions, waiting, pause and lesson reading; penalties do not inflate it. Trees completion now only shows its result panel: coin/gem rewards and completion account/progression writes are deferred at the user's request. The Trees-owned `GraphCompletion` component and call are removed; the shared Graph implementation is unchanged. GameOverManager supplies the existing loss audio.

Victory requires all five correct submissions and shows TREES STAGE COMPLETE, Time Played, Correct and Mistakes. Game Over stops the timer and blocks gameplay. Both screens offer Play Again and Main Menu through covered scene transitions. Trees is the final configured topic, so there is no new next-stage destination.

## UI/art scope

Original reference images and host prefabs/code were inspected before edits. Following artstyle.md's 2026-10-12 shared-use policy, objectives use GraphObjectiveFrame, feedback/timers use GraphStatusFrame, and the minimap/results use GraphMapFrame. Their existing cropped sub-sprites, nine-slice borders, white tint, cream paper and dark ink are preserved. WoodPanel remains the lesson's book cover; WoodButtonTeal/Green/Red keep the shared brown-rimmed artwork and editable TMP labels. Trees text actions are 280 x 88, number choices 96 x 72, and all button background Images store multiplier 4.5. The original settings background remains the world board. No raster artwork was generated or recolored and no shared sprite, theme, prefab or artstyle.md convention was changed.

The world puzzle keeps its calm dark-teal surface, colored branches and pale node labels; the cream HUD uses dark editable text. The field guide uses the existing wood cover, two cream ReadingPaper pages, dark text and central brown spine. IMPACT and Liberation Sans are assigned explicitly. Mathematical conventions use readable text and `V - 1`; no unsupported arrow or special-symbol fallback is required. Original simple circular touch controls are preserved. The minimap's circles reuse an existing joystick-circle sprite with separate labels. Decorative elements do not intercept pointer events.

## Historical validation (2026-10-11)

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

The checks above describe the original stage implementation. The focused-board follow-up replaces world/minimap editing and repeat Book confirmation with screen-space node taps and an explicit CONFIRM button. Its additional Play Mode and visual validation is recorded below.

- **37 Play Mode assertions passed** for the focused-board flow: projected touch-target alignment and actual UI raycast priority, camera framing, movement/input locking, camera restoration, retained selections and edited branches, no editing outside board mode, exact penalties, all five rounds, lesson return and victory cleanup. Reward persistence was suppressed.
- Invoking the original active `InteractButton` opened the focused board. The explicit Exit, Close Lesson and Play Again callbacks were exercised. Exiting during incorrect feedback restored the camera immediately and restored movement after the stun.
- A five-second clock exhausted by an incorrect submission while focused produced GAME_OVER, zero remaining time, stopped clock, closed board, restored full perspective camera viewport and blocked gameplay controls/input.
- The revised compact objective cards were inspected in landscape Game View for terminology and build challenges, including text overflow checks. Board, timer, feedback and edit buttons remained visible; the player and statue no longer obscure the diagram. The two-page lesson retained its paper/book treatment and BACK TO BOARD action.
- Saved Trees has its focus camera/board references, no missing scripts and no saved test component. Connected scene prefabs remain connected. The simulator was not operated; no physical Android validation was performed.
- Follow-up captures: `Temp/TreesQA/trees-focused-objective-final.png`, `trees-build-objective-final.png` and `trees-focus-field-guide.png`.

## Dedicated camera and deferred rewards validation (2026-10-12)

- **42 Play Mode assertions passed** across all five rounds, including the separate camera's initial inactive state, activation through the enabled Cinemachine brain, perspective restoration on exit, retained edits, lesson return, input locking and reward-free victory.
- The original active Interact button callback opened `CM_TreesBoardCam`. Its orthographic view, projected touch targets, compact objective cards and hidden exploration controls were inspected in landscape Game View. Capture: `Temp/TreesQA/trees-dedicated-board-camera.png`.
- A wrong confirmation against a five-second clock produced GAME_OVER and deactivated the board camera. After the blend, `CM_ThirdPersonCam` was live, the output viewport was full and perspective, and gameplay controls remained blocked behind the result screen.
- Trees has no `GraphCompletion` component or completion call. Validation required no persistence suppression. Shared Graph rewards are unchanged.
- The simulator was not operated. Validation was in the Unity Editor; physical Android taps, safe areas and performance remain unverified.

## Shared-frame UI and focused framing validation (2026-10-12)

- Read the updated artstyle.md and inspected the actual GraphHUD frames, original Play button/settings panel and current shared brown-rimmed button artwork before implementing this reskin.
- **42 Play Mode assertions passed again after the final changes**, covering all five rounds, camera switching, projected UI touch targets, edits, lessons, results and absence of rewards.
- The sidebar's right boundary and camera viewport's left boundary matched with **zero pixel gap**. Final projected node A touch-target error was **zero pixels**. All Trees button background Images inspected had multiplier **4.5**.
- Inspected terminology, build controls, the two-page Field Guide and completion results in one **1920 x 1080 landscape Game View**. No text overflow was reported in the inspected objective/build/result states. Retry, node selection/confirmation, Field Guide and Back to Board callbacks worked.
- The focused camera center is 0.35 world units lower to place the board slightly higher on screen. Removed the two decorative Tree_01 prefab instances beside the board; shared prefab assets and the gameplay tree remain intact.
- Captures under `Temp/TreesQA/`: `trees-hud-board-raised-final-1.png`, `trees-hud-build-final.png`, `trees-hud-field-guide-final.png` and `trees-hud-result-final.png`.
- No device profiles or physical devices were tested, and the Simulator was not operated, as requested. This validation covers the Editor landscape view only.

## Exploration HUD placement follow-up (2026-10-12)

- Field Guide follows the copied Settings button's actual bounds, vertically centered with a 16-unit gap to its right. Its position is converted into full-canvas coordinates; it does not require moving the HUD under SafeArea.
- HUD minimap branches use dark reading ink `#291A0F` for contrast against cream paper. World branch colors remain unchanged.
- Open Tree Board is centered 125 reference units above the bottom, plus any bottom safe inset, with its hint below. It clears the player in the inspected exploration view.
- Checked the three changes in the existing 1920 x 1080 landscape Game View. Field Guide open/close, Open Tree Board and Exit Board callbacks passed; exiting restored player controls. Button artwork, dimensions and multiplier 4.5 remain intact.
- Capture: `Temp/TreesQA/trees-exploration-hud-placement.png`. No device checks or Simulator operation.

## Re-run and remaining checks

Use **Tools > Logic Legends > Validate Trees Logic** outside Play Mode. For the five-round flow, enter Play Mode in Trees and add `TreesPlayChecks` to Trees Stage; its Result property/log reports success. The test component must remain editor-only and is not saved into the scene.

- [x] Core validators and alternate spanning trees.
- [x] Five-round state flow and no early victory.
- [x] Wrong work retention, exact penalties, duplicate confirmations and stun recovery.
- [x] Timer pause/resume, transition fades and no reset between rounds.
- [x] Original Interact callbacks and preview cleanup; minimap selection was superseded by focused-board input.
- [x] Timeout/victory exclusivity, result stats and retry.
- [x] Pause/resume, return-to-menu and menu re-entry.
- [x] Explicit fonts, legible text, original artwork and single landscape visual check.
- [ ] Physical Android taps, simultaneous joystick/look multitouch, safe-area cutouts and on-device performance.
- [ ] Signed-in male/female database spawning. Existing LevelSpawner/Firebase paths are reused. Completion rewards and progression writes are deferred and absent from Trees.
- [ ] Final WIP node/branch/pedestal/environment artwork replacement and device audio listening.

Temporary world models are intentionally basic. The connected interaction system and catalog are ready for replacement artwork. No Android APK build, physical device run, broad cross-device validation or production persistence test is claimed. The project also has pre-existing unused-field warnings and can report an InputActions disposal warning on editor shutdown; those unrelated systems were not rewritten.
