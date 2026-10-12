# Graph stage

The configured scene is `Assets/Scenes/Graph.unity`. It is registered in Build Settings and the Main Menu's Graphs entry (stage 9). Open the scene and press Play; no setup command is needed.

The catalog contains ten challenges: five with letter labels and five with numbered labels. Every new run shuffles the entire pool and selects exactly five distinct entries in random order. The selected order stays fixed through wrong answers, retries, lessons, and challenge transitions. PLAY AGAIN creates a fresh selection. The HUD counts rounds 1–5, and victory occurs after completing the fifth selected challenge.

## Play

1. Approach the existing Book Statue at the starting vertex (A or 1) and use the unchanged hand button (or keyboard interaction) to activate a challenge. The Graph INTERACT button calls the same method and also requires book proximity.
2. Walk across the bridges with the existing joystick or keyboard controls. The route and player position appear in the HUD/minimap.
3. At the destination, use CONFIRM without backtracking. The Book Statue also calls the same validator. For degree, choose a number before confirming.
4. A wrong confirmation costs exactly 10 seconds, counts one mistake, briefly disables the existing player controller, and preserves the answer/route. RETRY ROUTE returns to the configured start through the shared covered teleport. CLEAR SELECTION resets only the selected number.
5. A correct confirmation pauses the clock, shows feedback, and prepares the next graph. Interact with the book again to resume. Five correct challenges show the result; zero seconds shows Game Over.

The book has mesh-fitted compound BoxColliders on its static pedestal, steps and covers; its turning page remains collision-free. Collision setup is idempotent at runtime and in the scene setup tool, and the original proximity-based hand/INTERACT detection remains unchanged. Real activation displays one centered "Start!" for 1.5 seconds. Each non-final correct answer replaces it with "Challenge X completed! Interact with the book statue to start Challenge X+1." until the next book activation. The final answer uses the existing stage result UI. Notices use the central safe-area gap between the objective and map columns; degree choices briefly yield that space during Start and then restore. Tutorial book/answer practice never triggers gameplay notices.

Graph shares PRELIM's `WorldStageAudio` singleton and `Resources/Audio/WorldStageAudioClips`: LOFI BG / LOFI2 start at player spawn, including before book activation and during Help. Tracks use the same random selection with no immediate repeats, Music mixer routing, and separate SFX sources. Finishing the whole stage stops music and plays SOFTWIN once when the result appears; individual challenge completions do not play it. PRELIM and Graph use the same `StageResultCard` builder, with identical wood frames, IMPACT headings, LiberationSans summary and green/red buttons. Graph's **Play Next Stage** uses the existing covered transition to **Trees** (stage 10); **Go to Menu** opens **Main Menu**. Both Graph and Trees are enabled in Build Settings. Repeated navigation taps are consumed, and scene changes stop old audio without creating another playlist manager.

HELP opens the paged How to Play book using PRELIM's tutorial artwork and controls. It opens automatically after the initial spawn fade and remains available beside Settings during play. PREVIOUS and NEXT browse the guide. The first LET'S BEGIN starts interactive joystick, jump, and hand-button practice without activating a challenge. Actual joystick movement, a successful player jump, and interaction with the nearby "Interact with me" object complete these steps. Then the camera frames the real book for a practice interaction through the hand or INTERACT button. The minimap, objective, recorded route and configured starting countdown are explained individually. An isolated X/Y/Z/W graph demonstrates all five task types; degree choices, CONFIRM and CLEAR SELECTION use practice-only answer data, and RETRY ROUTE resets a demonstration route. No real catalog answers are exposed in the walkthrough. Arrows follow the actual controls and HUD targets. One shared Skip Tutorial button cancels any step and its camera transition. Completing or skipping restores the real graph and gameplay camera and opens "Let's start?". NO replays the full tutorial; YES prepares normal play, still requiring a fresh Book Statue interaction. Reopened Help does not restart onboarding.

The guide covers each task type, route tracking, validation, retries, penalties, and completion. Narrow layouts use a single reading page; longer text can be swiped to scroll. Practice never changes the selected graphs, route, degree answer, correct/mistake totals, or gameplay time. Graph Stage's controls-tutorial component exposes the joystick dead zone, movement distance, interaction range, camera transition time, book view distance and graph view height in the Inspector. The temporary high-priority camera owns its lens and pose; enabled camera input scripts, player renderer states, and existing arena visibility are restored on completion, Skip, replay and scene exit. Shared prefab assets are untouched.

Help pauses the Graph clock, gameplay input, and camera input, and restores their prior state when closed. A previously paused clock stays paused. The original pause/settings panel remains wired. Countdown starts at 300 seconds (configurable), and actual active play time is tracked separately from penalties.

## Configuration and scene ownership

`Graph Stage` owns `GraphStageManager`, `GraphStageClock`, `GraphUIController`, `GameOverManager`, and `GraphCompletion`. Inspector references are already assigned:

| Field | Assignment |
| --- | --- |
| Catalog | `Assets/Prefabs/Graph/GraphChallenges.asset` |
| Vertex / edge | `GraphVertex.prefab` / `GraphEdge.prefab` in the same folder |
| Arena | `Graph Arena`, rebuilt from catalog data per challenge |
| Book / spawn | `Graph Book Statue` / `Graph Spawn A` |
| UI host | `Canvas 1/SafeArea` |
| Gameplay controls | Existing `Gameplay_Interface` |
| Wood / paper | Original `settings_background.png` / `customization_scroll.png` |
| Sounds | Existing `WIN.mp3`, `DAMAGETAKEN1.mp3`, `GAMEOVER1.mp3` |
| Completion stage | 9 |

Copied PRELIM roots include Main Camera, Directional Light, CameraPivot, GameInput, EventSystem, LevelSpawner, LevelMenuManager, MobileInputUI, Canvas 1, CM_ThirdPersonCam, Inventory_Managers, and TeleportManager. The obsolete CameraPivot controller and unused truth-block inventory are inactive. PRELIM's spawner character assignments, Cinemachine rig, mobile callbacks, and pause/settings wiring are reused. Graph does not contain or call the existing `LevelManager` or `StageCompleteManager`. GraphCompletion owns one-time completion and account progression; GraphUIController owns the result panel.

The original Gameplay_Interface, LookArea, JumpButton, InteractButton, SettingsButton, Joystick, Background and Handle RectTransforms, including scale, anchors, pivot, position and size, match PRELIM. Its CanvasScaler also matches. Only Graph-owned actions move to the left column on narrower landscape/tablet layouts.

## Graph data

All coordinates are local to Graph Arena, with Y = 0. IDs and display labels are separate editable fields. Vertex labels and bridge arrows are separate from temporary geometry so finished artwork can replace the meshes later.

| Challenge | Vertices: ID (X, Z) | Edges |
| --- | --- | --- |
| Vertices and edges | A (0,0), B (0,12) | A-B |
| Degree of C | A (0,0), B (-10,12), C (0,12), D (10,12), E (0,24) | B-C, A-C, C-D, C-E |
| Simple path A to F | A (0,0), B (-10,8), C (10,8), D (-10,18), E (10,18), F (0,26) | A-B, B-D, D-F, A-C, C-E, E-F |
| Directed A to D | A (0,0), B (-10,12), C (10,12), D (0,24) | A->B, B->D, A->C, C->D |
| Cycle from A | A (0,0), B (-10,12), C (0,24), D (10,12) | A-B, B-C, C-D, D-A |
| Numbered vertices | 1 (0,0), 2 (0,12) | 1-2 |
| Degree of 3 | 1 (0,0), 2 (-10,12), 3 (0,12), 4 (10,12) | 1-3, 2-3, 3-4 |
| Numbered simple path | 1 (0,0), 2 (-10,8), 3 (-10,18), 4 (10,12), 5 (0,26) | 1-2, 2-3, 3-5, 1-4, 4-5 |
| Numbered directions | 1 (0,0), 2 (-10,8), 3 (10,12), 4 (-10,18), 5 (0,26) | 1->2, 2->4, 4->5, 1->3, 3->5 |
| Numbered triangular cycle | 1 (0,0), 2 (-10,16), 3 (10,16) | 1-2, 2-3, 3-1 |

Degree is calculated from enabled incident edges; a self-loop counts twice. Navigation accepts any valid simple path, not a fixed answer. A cycle must close at its configured start, contain at least three distinct vertices, and repeat no intermediate vertex. Directed bridges have visible arrows and a gate that blocks reverse entry without charging an automatic penalty.

The five new answers are: 1->2; degree(3) = 3; path 1->2->3->5 or 1->4->5; directed path 1->2->4->5 or 1->3->5; cycle 1->2->3->1 or 1->3->2->1. Catalog positions are not the displayed round numbers, because the run is randomized.

The traversal tracker samples horizontal player movement, requires a continuous connecting bridge crossing through its middle, and records arrival once. Its bounds match the 7.98-unit platform radius and 5.5-unit bridge half-width, with an additional support margin from the actual CharacterController radius and skin width. Standing on the rim or walking with the capsule partly over a bridge edge preserves continuity. The platform support margin alone cannot record arrival or allow route confirmation: the player centre must enter the actual platform disk. Continuous samples crossing the midpoint also count when a slower frame skips the middle. Trigger jitter, teleport jumps, off-graph movement and nonexistent connections cannot create a valid route. Directed gates wait for capsule clearance before closing behind an arriving player, remain closed against reverse travel, and cannot record a reverse edge. Invalid routes require explicit retry. Confirmation freezes recording during validation and transitions.

## Scripts

New runtime scripts in this folder: `GraphChallengeData` (including validator), `GraphChallengeCatalog`, `GraphVertex`, `GraphEdge`, `GraphTraversalTracker`, `GraphStageClock`, `GraphStageManager`, `GraphUIController`, and `GraphCompletion`. `GraphStagePlayChecks` is Editor-only validation and is not attached to the saved scene.

New Editor utilities: `GraphStageSetup`, `GraphChamberSetup`, `GraphStageChecks`, and `GraphVisualSetup` under `Assets/Editor`.

Scoped existing-script changes:

- `CameraTargetController.SetViewAngles`: Graph's initial viewing angle; existing defaults unchanged.
- `TeleportManager.UsesCoveredTransitions`: includes Graph in the existing covered teleport flow.
GraphCompletion uses the existing Firebase user schema for rewards and unlockedStage in a single transaction. Its Editor validation flag suppresses account writes. The shared StageCompleteManager source has no Graph changes.

Main Menu's Graphs scene target and Build Settings now include Graph. Unrelated local changes were preserved.

## Temporary artwork

The chamber reuses LowPolyDungeon floor, wall, pillar, torch and banner prefabs. Platforms use Assets/FINAL_ASSETS/Graph/Bilog.fbx; bridges use Assets/FINAL_ASSETS/Graph/Bridge.fbx. The source Bridge Highlights mesh uses a separate GraphPathLight material, orange throughout the crossing and blue only after the destination vertex arrival is recorded. Only the modular length scales with each connection; the requested bridge cross-section scale and source stone materials are preserved. Invisible walk colliders keep the existing forgiving movement widths. Vertex labels float 6.95 units above the vertex origin, remain attached to their vertex, and use the existing FaceCamera component to face the player camera every frame. Their text boxes autosize without clipping. A Graph-local TMP overlay material prevents the character or bridge cutting through the floating label. The Book Statue uses the existing prefab and directed arrows keep the existing teal material. No new raster artwork was generated. These are temporary assets pending the Graph artwork.

HUD frames reuse the original settings wood with local runtime slicing; buttons use the existing blank brown-rimmed WoodButtonTeal derivative with editable TMP labels. Lessons use parchment pages. IMPACT is explicitly assigned to short labels, LiberationSans to reading text, and a local font fallback supplies graph arrows from LogicFallback_SDF without modifying shared font atlases. The local font owns a GPU copy of its atlas and a separate material; Play Mode exit cleanup was verified. `artstyle.md` is unchanged.

## Editing and validation

1. Open Graph and edit its catalog for objectives, labels, positions, enabled edges, and directions.
2. Replace the vertex/edge prefab artwork while retaining its collider, label, bridge, arrow and gate references. Keep the current walkable widths and label visibility when fitting new models.
3. Assign replacements on Graph Stage's Inspector if using different prefabs. Keep the original mobile controls and their parent transforms intact.
4. Run **Tools > Logic Legends > Validate Graph Rules** for deterministic graph/traversal/clock checks. In a fresh Play session, **Test Graph Help (Play Mode)** checks automatic Help, page navigation, blocked activation and submissions, clock/control restoration, preserved run state, and compatibility with Settings pause.
5. In a fresh Graph Play session, run **Tools > Logic Legends > Test Graph Flow (Play Mode)**. It drives the actual player through the five randomly selected graphs, exercises mobile interaction callbacks, and suppresses account writes. **Test Numbered Graph Flow (Play Mode)** uses an Editor-only selection of the five new numbered graphs for complete new-content coverage. Exit Play Mode afterward; both tests finish by checking Game Over.
6. **Test Graph Book and Notifications (Play Mode)** walks the real player into the statue to check blocking and interaction range, then uses an Editor-only five-round degree fixture to exercise every numbered completion message, persistent paused waiting, hand/UI book activation, Start expiry, and the shared PRELIM result UI. It checks spawn-time playback, forced track-end rollover, no immediate repeats, singleton rejection, mixer routing, music through Help, and one SOFTWIN only for final completion. Account writes are suppressed; the production random selection is unchanged.
6. The one-time **Build Graph Stage** utility refuses to overwrite an already configured Graph scene. **Add Existing-Asset Graph Chamber** is also guarded against duplicate scenery. Neither is needed for normal use.
7. **Expand Graph Challenge Pool** adds any missing numbered default entries without replacing existing challenge data, scenery, or prefabs. The saved catalog is already expanded; rerunning the command does not add duplicates.

Validation performed in Unity 6000.3.6f1:

- Compilation: no errors.
- Rule checks: passed the ten letter/number challenges, alternate paths and cycles, invalid/repeated routes, both computed degrees, self-loop degree, jitter/teleport/off-graph protection, and clock semantics. Across 200 seeded selections, every run had five unique entries, all ten entries were eligible, order varied, and the shared catalog stayed unchanged.
- Play flow: passed all five newly numbered challenges. After changing lighting to require destination arrival, a production random run passed Find a path, Directed graph, Numbered directions, Vertices and edges, and Numbered vertices. Checks also require orange lighting before arrival and blue lighting afterward. Checks used the real player/colliders and covered duplicate activation/confirmation, exact penalty, stun recovery, retry, selection clearing, preserved sampled order, paused transitions, five-correct victory, and zero-time Game Over. Account writes were suppressed.
- Saved-scene comparison: original control transforms and CanvasScaler match PRELIM; no LevelManager component in Graph.
- Visual review: 1920x1080, 2400x1080 landscape and 1440x1080 tablet Game Views; lesson margins, world labels, timer, minimap, feedback, and result panel inspected. Arrow glyphs checked against the assigned font.
- Screenshots are under `Temp/GraphQA` (local, ignored by Git).

Limitations: no physical mobile device was connected; Unity Remote reports that adb has no device/emulator. Touch hardware, device performance, and a packaged mobile build were not validated. Firebase reward writes were intentionally suppressed during automated validation; authenticated end-to-end progression was not tested. Temporary project artwork is not the final concept-board art.

## Graph HUD asset revision

Canvas 1 is a connected instance of Assets/Prefabs/PREFABS/WORLD_UI/Canvas 1.prefab. It is never unpacked. Legacy puzzle children remain in the prefab hierarchy as inactive scene overrides. The shared prefab file is unchanged. Mobile button callbacks still target the prefab joystick component; pause buttons target the scene LevelMenu. CanvasScaler and all original control transforms are preserved.

Graph's extra solid teal panel fills are removed, and the gameplay backing is transparent. The original wood panel artwork remains. Minimap is 270 x 270; timer is 270 x 100, with readable 32-unit text. Field Guide sits 14 units to the right of Settings. Compact 360-unit-wide objective, route and feedback panels occupy the left column; the center stays open. Only Graph actions and degree choices adapt on tablet layouts. Degree buttons are 110 x 64, use the source sprite proportions with 18-22 unit labels, and form a four-column grid on wide displays or a two-column grid on tablets. Incorrect answers show -10 beside the timer for 2.5 seconds. The minimap uses the original Pointer_0, tinted green and rotated with player heading; its orange/blue edges match the world lighting.

Apply Graph Asset Setup updates only Graph's canvas connection, preview geometry, vertex/edge prefabs and completion component. It does not modify the shared Canvas 1 asset, source FBX assets, other scenes, or the ten-challenge catalog.

The Graph camera uses the scene skybox instead of a flat teal clear color. Unity was left in Android Simulator as requested; subsequent device-size checks were stopped. Physical Android hardware and Firebase writes remain untested.

For the normal-walking regression, run **Tools > Logic Legends > Test Graph Walking Boundaries (Play Mode)** in a fresh Graph session. It walks the actual player along offset A -> B -> D bridges, around B's platform rim, and through the turn; it checks continuous recording, arrival-only lighting and gate closure. Validation uses the existing Simulator without changing the device or touch controls.

Normal-walking fix validation (2026-10-11): deterministic checks passed platform rims and junction turns, both sides of every enabled catalog edge, a continuous low-frame-rate midpoint crossing, gate clearance, and retained teleport/off-path/reverse protections. The Simulator boundary test passed the real player's offset A -> B -> D crossing and B rim turn with arrival-only blue lighting. The full five-numbered-challenge Play flow also passed, including blocked reverse travel, retry, penalty/stun recovery and terminal states. Account writes were suppressed; no physical device or Android build was tested. Shared Canvas 1 and touch-control transforms were unchanged.

## Graph object size revision

Vertex roots now use the requested (2.28, 2.28, 2.28) scale and edge roots use (2.5, 2, 4). The platform walk radius is 7.98 units and the bridge deck width is 11 units. Runtime layout expands catalog positions by approximately 3.1687; a reference 12-unit connection now spans 38.024 units between vertices and retains the requested 24.8-unit bridge deck, overlapping both platforms. Other connections fit their endpoint spacing. The shared catalog, challenge answers and five-of-ten random selection are unchanged.

Arrival checks, capsule support margins and one-way gate placement match the enlarged geometry. The minimap normalizes the expanded coordinates into its existing layout. The Graph scene's book statue is uniformly 2.28 times larger (4.332 units tall), with a scaled placement offset and interaction range. Chamber dimensions/light ranges and the Graph camera's 18-unit follow distance fit the larger objects. Floating labels stay camera-facing at 6.95 units above the platform origin, with a restrained 1.5 world scale. Source models, materials, orange/blue lighting states, UI dimensions and original touch controls are preserved.

Use **Tools > Logic Legends > Apply Graph Object Sizes** to reapply the Graph-only prefab dimensions and saved scene preview. This operation is idempotent and preserves the connected Canvas 1 instance. Resized geometry passed deterministic traversal checks on both sides of every enabled edge in all ten challenges, plus the real-player A -> B -> D bridge-side/rim/turn check in the existing Android Simulator. No device changes or hardware/build testing were performed.
