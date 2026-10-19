# Khezu / Proof Devourer — Phase 1 awakening test

Open `OpenArchive_KhezuAwakening_Test.unity` in this folder, or use **Tools → Logic Legends → Khezu Phase 1 → Open Saved Test Scene**. The saved scene preserves the user's current **2.5 environment-root scale** (roughly **500 m across**). The original environment/exploration scene files are unchanged. The controllable character retains its original size. Khezu's latest adjustment makes it **25% larger than its previous visible size**, increasing its world scale from 2.3 to **2.875**.

The original `MONSTERS/ProofDevourer/Prefabs/ProofDevourer.prefab` is a connected prefab instance at:

`OpenArchive/UniversalMonsterNest/MonsterSpawnPoint/Khezu_Awakening_Test`

It inherits the existing marker's position and rotation. The marker remains at local **(0, 0.25, 0)** within the nest, now world **(0, 0.625, 0)** after environment scaling. Khezu uses **1.15 local scale** under the **2.5 parent scale**, giving **2.875 world scale** while keeping its origin on the nest floor. The original Generic avatar, mesh, materials, thirteen clips and `ProofDevourer.controller` are reused. No asset rebuild, animation generation or model replacement was performed.

## Manual test

1. Enter Play Mode. **Sleep** starts automatically and loops. The test panel appears near the top center of the Game/Simulator view, below the movement help.
2. Press the on-screen **+1 Mistake** once per stage, or **Awaken All** to queue all four awakening stages. The counter clamps to four. Queued clips finish in order. The live status shows requested mistakes and the current Animator state.
3. At four mistakes, **Full_Awaken** plays once and the existing Animator transitions to looping **Idle**.
4. The on-screen **Reset to Sleep** clears pending stages and previews, blending back to looping Sleep. It also works during an unfinished awakening.
5. Inspector controls remain on **Khezu_Awakening_Test**. **Preview <clip name>** plays any of the thirteen original animations in place. Preview is separate from mistake progression. Press Reset to return to the test, or Increase Mistakes to restart at stage one.

The component's context menu also provides **Test/Increase Mistake Count** and **Test/Reset To Sleep**. Test buttons are enabled only in Play Mode. The Inspector shows requested mistakes, the active stage, Animator state and preview mode.

The scene includes the existing **Archive_Explorer**, **GameInput**, **third-person follow camera**, and shared **mobile UI** copied from the exploration scene. Click inside the **Game/Simulator** view to focus keyboard input. Use **WASD / left joystick** to move, **Space / jump button** to jump, **right-side drag** to turn the camera, and **R / Return to spawn** to reset your position. The settings gear pauses; Resume restores input. You start at the existing player plaza; walk straight ahead toward the central nest. Test buttons receive UI clicks inside their own panel without covering the joystick or look area outside the panel. No network connection is required.

| Mistakes | Existing clip | Result |
| --- | --- | --- |
| 0 | Sleep (4.4 s) | Loop |
| 1 | Wake_01 (1.8 s) | Hold its final pose |
| 2 | Wake_02 (2.2 s) | Hold its final pose |
| 3 | Wake_03 (2.2 s) | Hold its final pose |
| 4 | Full_Awaken (3.0 s), then Idle (3.4 s) | One-shot, then loop |

## Motion and isolation

Manual state requests use **0.25 s fixed-time crossfades**. The original Full_Awaken → Idle transition remains **0.14 s**. Adjacent authored awakening boundary poses match. Reset and preview requests also crossfade; Animator.Play is used only to initialize Sleep before the first rendered frame.

Apply Root Motion is disabled. The test locks the prefab assembly's local position, rotation and scale in LateUpdate, while leaving all animated bones free to use their original curves. Always Animate prevents offscreen culling from stopping the test. No Rigidbody, NavMeshAgent, movement, collider, chase, network, puzzle or health behavior is added to the monster. Chase preview is only the existing in-place limb animation. Player control uses the existing exploration harness and unchanged shared player scripts and prefabs.

All new code and the scene live in this Testing folder. Custom Inspector, setup and verification code live under Editor and are excluded from player assemblies. The test scene is not added to build settings. Original source scenes and monster assets are preserved. The copied environment preview camera and fixed review camera are disabled for normal play; the existing exploration follow camera is the sole active view. The fixed review camera is retained for automated monster screenshots only.

## Verification and evidence

**Tools → Logic Legends → Khezu Phase 1 → Verify In Play Mode** runs a repeatable coroutine against the real Animator. Start it after entering Play Mode without manually changing the initial sleeping state. It checks automatic Sleep, two Sleep cycles, all four stages and final-pose holds, Full_Awaken → Idle, Idle looping, rapid-click queuing, counter clamping, reset during awakening, all thirteen previews, missing-name rejection, original curve bindings and clip motion, boundary continuity and stationary assembly/rig roots. Let the check finish before using the manual buttons.

Evidence is saved in `Reports/`:

- `PlayModeValidation.json`: individual checks, actual original clip names/durations, missing clips and movement measurements.
- `01_Sleep_PlayMode.png` through `06_Idle_PlayMode.png`: renders of the live animated mesh in the nest.
- `EnvironmentPreservation.json`: comparison of all 1,150 existing environment transforms, 885 renderers/meshes and 545 colliders against the original scene, plus the saved monster prefab link.
- `ProtectedFilesBefore.json` and `ProtectedFilesValidation.json`: SHA-256 preservation check covering original monster assets, original environment/exploration scenes and production C# files.
- `Setup.json`: scene, prefab and spawn placement references.
- `EnvironmentResize.json`: enlarged test-map dimensions, compensated Khezu scale, and preserved monster/player sizes. Environment comparison allows this intentional root-scale override. Earlier source navigation measurements apply to the original map; navigation has not been rebaked for this stationary test.
- `MonsterSizeAndButtons.json`: current map scale, 15% Khezu enlargement, and the three on-screen test buttons. This supersedes the earlier 1.25-scale resize profile.
- `KhezuScaleAdjustment.json`: latest additional 25% enlargement, saved/reloaded scale, floor alignment and retained test buttons. This supersedes the monster size in earlier reports.
- `PlayerControlsValidation.json` and `PlayerControls_PlayMode.png`: player input, movement, jump, camera, pause and stationary-monster checks in this scene. Run **Tools → Logic Legends → Khezu Phase 1 → Verify Player Controls In Play Mode** separately from the awakening checks.

Required clips missing: **none**. All thirteen clips are available for animation preview: Sleep, Wake_01, Wake_02, Wake_03, Full_Awaken, Idle, Chase, Capture, Jumpscare, Barrier_Cast, Attack, Return_To_Sleep and Stunned. Extra clip previews verify animation playback only; they do not implement those gameplay systems.

Before the latest 25% scale adjustment, the setup passed **93/93 awakening and UI checks** and **14/14 player-control checks**. Verification clicks the on-screen buttons through UI pointer events, checks their raycast reachability, and validates the chosen monster size rather than requiring unit scale. Both assembly and rig-root drift were zero. The latest scale-only adjustment was checked by saving and reloading the scene, confirming a 1.25 size ratio, floor alignment and retained buttons; the full suite was not repeated for this adjustment.

Verification is limited to Unity 6000.3.6f1 Editor Play Mode and the current project's rendering quality. Android builds/devices, multiplayer sessions, navigation and production encounter integration are not verified in this phase. The existing renderer's Bone4 setting and the project's skinning quality are preserved.
