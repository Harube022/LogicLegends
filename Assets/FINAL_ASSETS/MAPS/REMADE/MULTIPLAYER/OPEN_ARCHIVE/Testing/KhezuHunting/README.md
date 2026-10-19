# Khezu / Proof Devourer — Phase 2 hunting test

Open **OpenArchive_KhezuHunting_Test.unity** in this folder. The Phase 1 scene remains saved separately. Both use the original ProofDevourer prefab, rig, materials, Animator and clips. This scene preserves the current **2.5 environment scale** and **2.875 monster world scale**. The player keeps its original size and existing controls.

## Play locally

1. Press Play and click the Game/Simulator view. Use WASD / joystick, Space / jump button, and right-side drag to look. R returns the player to spawn.
2. Press **Force 4 Mistakes** or the existing **Awaken All** button. You may instead press **+1 Mistake** four times. Khezu completes Wake_01, Wake_02, Wake_03 and Full_Awaken, then holds Idle briefly before choosing a target and moving.
3. Run from Khezu. **Target** and **Hunted IDs** appear in the hunting panel. The disconnected local explorer uses test ID **-1**; real rooms use Photon ActorNumbers.
4. At catch, Khezu stops, displays **Caught**, records the actor and emits `PlayerCaught(int actorId)` once. No damage, jumpscare, barrier or player lock is applied.
5. **Reset Hunt** stops the current pursuit and preserves history. **Start / Resume** starts another hunt once awake. If sleeping, it requests all four mistakes and waits for awakening to finish.
6. **Return to Sleep** sleeps at the current location and preserves history. It does not teleport back to the nest. **Clear Rotation** explicitly clears history and stops the hunt. Reset then move away before resuming if you were caught at close range.

The same commands are available in Khezu's existing **Awakening Test Controller** Inspector, or on **KhezuHuntTestControls** at `ExplorationTestRig/Canvas 1/SafeArea/Khezu_Hunting_Test_Panel`. Production AI and Photon code are under `OPEN_ARCHIVE/Hunting/`; test UI, session adapter and verification are under this `Testing/KhezuHunting/` folder.

## Existing multiplayer integration

The project uses **Photon PUN**, not Unity Multiplayer Services. `PlayerSpawner` creates the existing `Resources/Multiplayer_Male` or `Multiplayer_Female` avatar. The test session disables the disconnected exploration character when entering from a room, then reconnects the same follow camera and mobile controls to the spawned local player. Shared player, health, puzzle, lobby and avatar scripts/prefabs are not changed.

There is **one placed scene Khezu on each client**. Do not call PhotonNetwork.Instantiate for the monster. Only the Master Client selects targets, updates the NavMeshAgent, detects catches and mutates the hunting rotation. Other clients disable navigation and use position/rotation interpolation plus named Animator snapshots. PUN events **171/172** use channel `ll.khezu.phase2.v1`. Commands are sequenced and rate-limited; snapshots accept only the current Master as sender. Checkpoints are stored in the room property `ll.khezu.phase2.v1.snapshot` for late joins and Master handover. Capture serials prevent a restored checkpoint from replaying a capture event.

To test with the **existing lobby** in the Editor:

1. Include this scene in each client's build settings (it is enabled in this project).
2. Open Main Menu in Play Mode and open its normal Multiplayer panel.
3. Before hosting, choose **Tools → Logic Legends → Khezu Phase 2 → Use Existing Lobby For Next Hosted Room**. This sets the existing service's target scene for the next hosted room; it does not change the normal menu's default destination.
4. Host normally. Invite the other clients with the room code, ready up, and press the normal lobby Start button. The host-selected scene is shared by the existing lobby system. For a device-only host, a test launcher can set `MultiplayerLobbyService.EnsureExists().TargetScene = "OpenArchive_KhezuHunting_Test"` after opening Multiplayer and before calling the existing HostGame flow.
5. Check that all peers display the same target ID, state, hunted IDs and catch. Disconnect the selected player, reconnect another eligible avatar, and transfer/leave the Master to test recovery. Keep clients on identical builds and the project's existing Photon game version/region.

The host trusts the Photon room's Master Client as authority, consistent with this project's architecture. This is not a dedicated server implementation.

## Player eligibility and rotation

Only active Player avatars with a valid PhotonView owner who is connected and not inactive can be targeted. Remote Player scripts being disabled by `MultiplayerAvatarSync` is expected and does not disqualify those avatars.

The project currently has a scene-level HealthManager, not a per-player network elimination registry. This phase does not change health. Availability is bridged through any of:

- Host-side `KhezuHuntingController.SetPlayerEligibility(actorId, false/true)`.
- Optional `KhezuHuntEligibility` on the player's root, set on the host.
- Photon player property **ll.eliminated = true**, or **ll.khezu.available = false**, consumed by the host. Future elimination systems should publish these flags or call the host API.

`HuntedPlayerIds` records each selected ActorNumber. Selection is random among eligible IDs not yet seen. Only when all currently eligible IDs have been seen is a new random rotation started. Sleep and resetting one hunt retain history; temporary unavailability retains the ID. Disconnected IDs are removed safely. If the target becomes unavailable, the host selects the next eligible target; with none available it waits in Idle without clearing history. ActorNumbers are stable within their Photon room, not permanent account IDs.

## Navigation, catch and tuning

The previous NavMesh belonged to the unscaled source map. This phase adds a separate **Khezu_WorldNavigation.asset**, baked from the existing environment's current world-space colliders. Water, ponds, waterfalls and cliffs are Not Walkable. Bridges, the four wide entrances, ramps and main ring are included. Existing scene navigation remains unchanged and uses a different agent type.

**AI Navigation 2.0.16** is installed for navigation tooling; version verified against the [Unity package registry](https://packages.unity.com/com.unity.ai.navigation). The NavMesh data loader registers only the owned Khezu dataset and removes only its own instance. No runtime baking, navigation links, terrain changes or geometry rebuilds are used.

The dedicated **Khezu Proof Devourer** agent type uses a conservative **9.7 m radius**, **10.2 m height**, **1.6 m step**, **40° slope**, and **0.3 m voxels**. The radius covers the current scaled Idle/Chase mesh through turns. The trigger BoxCollider surrounds the sampled mesh bounds, approximately **12.22 × 9.86 × 13.28 m** at current scale. Catch compares this collider's closest surface with the target's CharacterController capsule and requires an unobstructed line through the scene. The default surface gap is **0.65 m**, configurable in the hunting controller. The collider is a query volume; it applies no damage or physical push.

Default player run speed is **19.5 m/s** from the existing explorer's 13 m/s move speed × 1.5 run multiplier. Close pursuit is now **20.28 m/s** (4% above the runner) within an 8 m gap between the actual monster body and player capsule. Speed smoothly increases to the existing **26.68575 m/s** catch-up maximum at a 35 m surface gap. Acceleration stays 16 m/s² and angular speed 150°/s. These values are exposed in the Inspector.

Moving-target arrival braking is disabled. It previously slowed Khezu down toward the player's last sampled position, which let a runner open a large gap. Destination updates now take 0.1 seconds nearby, increasing smoothly to the existing 0.25 seconds at a large gap. Cached rosters still update every 0.5 s and catch checks every 0.1 s. The existing Chase clip uses speed matching; root motion stays disabled. Idle plays while stopped or without a complete dry route. Catch reach, wall/line-of-sight checks, player movement and hunting rotation are unchanged. No normal chase teleport or Warp call is used.

`Reports/PursuitBaseline.json` and `Reports/PursuitMovingTarget.json` compare the original and corrected six-second close chase against a 19.5 m/s runner on the existing north route. The Editor-only test repositions only the test player and moves its real CharacterController; the monster navigates from its saved nest normally. These are offline pursuit tests, not a live network or Android-device test.

When the selected player enters a pond or goes underneath a bridge, `KhezuWaterPursuit` chooses a complete dry route beside the existing water mesh boundary. Khezu approaches the reachable bank, follows changes in the player's position and paces along that bank while the player stays in water. It uses the existing bridges to reach other banks. Inspector settings **Shoreline Pace Distance** (18 m) and **Shoreline Turn Distance** (6 m) keep destinations ahead of arrival. Returning to reachable land immediately resumes normal direct pursuit. Catch distance and obstruction checks still apply; water is not added to the monster's NavMesh.

`KhezuWaterPursuitPlayChecks.Begin()` is an explicit Editor-only offline check, never attached to saved scenes. `Reports/WaterPursuitPlayMode.json` records actual agent movement through all four ponds, an extended stationary water target, movement using the original controls, a target underneath a bridge, the original jump back to land and a normal catch. It temporarily disables sinking for the extended probes, restores it and checks the saved 5 s grace / 4 s sinking / 5 s dry recovery settings during a real pursuit and forced return. Test player placement never moves the monster. Live multiplayer remains unverified.

The nest's four broken pillars and four torch pedestals now sit on the stone rim at source radii 13.8 m and 14.2 m. Previously they stood outside it at 17.4 m and 17.8 m, creating small player pockets outside the large monster's clearance. The same eight transforms are updated in all five saved scenes, both reusable nest assemblies and the builder defaults. The shared hunting/cinematic NavMesh is rebaked at the original 9.7 m radius. Meshes, obstacle collision, entrance widths, bridges, monster size and spawn points are retained. `Reports/NestClearance.json` verifies all other environment transforms are preserved.

`KhezuLandApproach` searches complete routes to nearby points on the reachable side of a dry obstacle when the first projection fails or sits across a wall. It favors clear approaches and the controller smoothly faces the actual player at arrival. It never warps the monster or expands catch distance. Solid obstacles still block capture. `KhezuLandApproachPlayChecks.Begin()` exercises the four former nest pockets with actual navigation and requires normal close-range captures, then checks two other off-mesh dry targets keep a complete approach route. Its Editor-only report is `Reports/LandApproachPlayMode.json`. This does not give Khezu permission to pass through walls, squeeze into physically smaller spaces or catch a player through cover.

After the clearance repair, **16/16 land-pursuit checks**, **40/40 navigation/rotation checks**, and **35/35 water-pursuit checks** passed in the cinematic scene. `Reports/LandNavigationSurvey.json` records 1,059 capsule-clear nest ground positions with zero failed direct projections, compared with 56 failures among 1,031 positions before repair. Across the broader 5,922-position ground survey, all 171 positions outside the original direct projection radius obtained a complete fallback approach route. This survey verifies reachable approach destinations; it does not assert a physical capture inside every smaller obstacle gap. The four former nest pockets were tested through actual close-range capture.

Network snapshots run at 10 Hz while hunting, 5 Hz during awakening and 1 Hz at rest; room checkpoints update on significant state changes and every 2 s. Full skeleton/bone transforms are not streamed.

If you resize the map or monster again, stop Play Mode, save, tune the agent radius/height and trigger bounds, then use **Tools → Logic Legends → Khezu Phase 2 → Rebake Current Hunting Navigation**. Run route checks again. Scaling a baked NavMesh's parent does not resize its baked world-space data.

## Verification

Editor commands under **Tools → Logic Legends → Khezu Phase 2**:

- **Verify Navigation And Rotation** checks four independent entrances, four bridge decks, main-ring connectivity, excluded water/outside bounds, randomized no-repeat rotations for simulated 2/3/4-player rosters, eligibility changes and checkpoint serialization. Results: `Reports/NavigationAndRotation.json`.
- **Verify Hunting In Play Mode** drives the actual on-screen controls and real local player target through sleep, awakening, target selection, chase, catch and reset; checks crossfade checkpoints and no repeated capture on restoration. Results: `Reports/HuntingPlayMode.json` and `Caught_PlayMode.png`.
- `KhezuPhotonOfflineChecks` is an explicitly created Editor-only runner for a one-actor Photon OfflineMode smoke test. It reloads the scene, uses the real network avatar spawner, verifies ActorNumber targeting, catch, room checkpoint state and eligibility flags, then leaves the offline room and reloads back to local exploration. Results: `Reports/PhotonOfflineSmoke.json`. It is not attached to saved scenes or included in player builds.

Completed in Unity 6000.3.6f1 on 16 October 2026: **40/40 navigation, rotation and serialization checks**, **21/21 runtime hunting/UI checks**, and **12/12 Photon offline-room checks passed**. Runtime checks confirm Khezu follows the same selected player after that player's location changes, transitions only after full awakening, stops on catch, and preserves history through sleep. `ProtectedFilesValidation.json` checks all 718 protected original monster, environment-scene and production-script files. `EnvironmentPreservation.json` compares the original environment geometry and transforms. `SavedSceneValidation.json` records final scene references, components, cameras, UI events and scales.

**Not yet verified:** live 2–4-client transport, peer position/rotation/animation consistency, real late joins, Master migration, network latency/loss, and Android build/device performance. The simulated roster and one-actor offline-room checks do not substitute for those tests. Original clips remain present; no replacement animations were created. No later encounter systems are implemented.
