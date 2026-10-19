# Phase 3B: captured-player movement lock and development outcomes

Open `Testing/KhezuCinematic/OpenArchive_KhezuCinematic_Test.unity` and enter Play Mode. Use **Force 4 Mistakes**, then let the original Khezu catch the explorer. The existing two-second POV finishes before the crimson seal forms. The captured owner sees **MATHEMATICAL BARRIER**, **SUCCESS** (green), and **FAILED** (red). Mouse and the existing Input System UI module's touchscreen bindings use the same UGUI buttons. The buttons are 290 × 88 reference pixels inside the existing SafeArea canvas; there is one persistent click listener per button and no blocking label raycasts.

## Cause and enforcement

The cinematic used to disable `Player` temporarily and restore it at its Phase 3 handoff. There was no persistent movement restriction or authoritative outcome service after that handoff. A visual cylinder has no authority over `Player.Update`, `CharacterController.Move`, root motion, teleports or owner-authored avatar positions. The old local handoff panel had no connected outcome buttons.

The original `Player` is extended, rather than replaced. Its owner-keyed lock API rejects move, run, jump, interact and guided movement, clears velocity and buffered movement, suppresses root motion, freezes any existing Rigidbody and pins the root to the Master-confirmed capture anchor in Update/FixedUpdate/LateUpdate. The current scene avatar uses CharacterController and has no Rigidbody, dash or roll component. External direct CharacterController/transform movement is reconciled as well. Return-to-spawn and the shared teleporter reject movement during a lock. Water sinking pauses while movement is restricted; its existing 5/4/5-second timing is unchanged.

The encounter acquires its barrier token immediately at capture, before capture callbacks, and holds it throughout the cinematic and dissolve. The POV owns a separate lower-priority token. Its cleanup releases only that token. No frame between the cinematic and challenge reenables movement. Player, networking, CharacterController and EventSystem stay enabled, and camera rotation returns after the POV. Other restrictions, including the higher-priority health-depleted token, remain owned by their original services. An interrupted/reset/disconnected encounter releases only its barrier token.

`MultiplayerAvatarSync` retains the existing five-field owner stream. While captured, the owning avatar serializes the canonical anchor with stopped movement flags. The Master and observers consume owner packets but clamp position to the anchor in the existing synchronization component; LateUpdate also enforces the lock when a remote `Player` component is disabled. This project uses a **trusted PUN Master Client**, not a dedicated server. Master-confirmed capture/outcome snapshots are the authority in that architecture.

## Outcomes and development gates

The existing encounter handles event **173**, channel `ll.khezu.barrier.dev.v1`. Payload is exactly `(channel, captureSerial, success)`. Actor identity comes from Photon's actual event sender. The Master checks active-room membership, active captured actor, matching serial, completed cinematic, unprocessed challenge, development configuration and Editor/development compilation. Duplicate, stale, malformed, wrong-owner and disabled-configuration requests are ignored. An accepted result is checkpointed before its dissolve starts.

- **SUCCESS:** fade the existing crimson VFX, then release the barrier token without heart loss. The existing NavMeshAgent navigates back to the original `MonsterSpawnPoint`. A thirty-second absolute timer starts on arrival, followed by original `Full_Awaken`, `Idle`, and hunting. No return teleport is used; hunting history is preserved.
- **FAILED:** fade, then deduct one heart using the existing HealthManager's actor-aware, serial-deduplicated method. Release the barrier token, play original `Attack`, and resume the existing rotation. When alternatives are eligible, the failed actor cannot be selected immediately again. No new sleep timer is started. Zero health retains the health movement restriction and removes that actor from eligibility.

The HealthManager stores absolute per-actor hearts plus the last processed penalty serial in the existing monster checkpoint. Its existing `TakeDamage()` uses that same authoritative count in this configured scene, so prior damage cannot be overwritten by a barrier result. Legacy scenes retain their original health mode. Applying a checkpoint cannot repeat damage; restoring health removes only the health token. A host-owned future math-question service can call the existing hunting controller's `ResolveBarrierOutcome` after validating a real result; the temporary UI owns no state machine.

Both `UNITY_EDITOR || DEVELOPMENT_BUILD` **and** `KhezuCaptureEncounterController.developmentTestOutcomes` must permit test outcomes. The switch is enabled only in this dedicated saved test scene. A non-development build strips `Khezu_Barrier_Development_UI` through `KhezuBarrierReleaseUiStripper`, and the host's compiled gate rejects test events even if a client sends one. No production math-question UI is implemented by this temporary selector.

## Connected scene and source changes

`Editor/KhezuBarrierSetup.cs` connects and saves the existing scene through **Tools → Logic Legends → Khezu Barrier → Connect Movement Lock And Development Outcomes**. It adds the outcome panel under `Canvas 1/SafeArea/Khezu_Barrier_Development_UI`, references the existing encounter, disables other test controls during capture, and connects the original HealthManager with three heart images. It references the existing nest transform. It does not rebuild the environment, NavMesh, model, animations, font, shaders, VFX materials, profile or meshes.

Modified existing source files (paths below are relative to `Assets/`):

- `Scripts/Scripts/Player.cs`, `GameInput.cs`: reject locked input through the actual player motor.
- `Scripts/Scripts/Multiplayer/MultiplayerAvatarSync.cs`: enforce the capture anchor in owner/receiver synchronization.
- `Scripts/Scripts/HealthManager.cs`: preserve legacy health behavior while supporting initialized actor-aware health.
- `Scripts/Scripts/GameManager/TeleportManager.cs`: reject teleport while movement-locked.
- `FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Exploration/OpenArchiveExplorationSession.cs`: reject return-to-spawn during capture.
- `FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Water/OpenArchiveWaterSinking.cs`: pause sinking while captured.
- `FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Cinematics/KhezuCinematicJumpscareController.cs`: transfer movement protection using the cinematic token.
- `FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Cinematics/KhezuCaptureEncounterController.cs`: acquire/release barrier protection and retain existing handoff callbacks.
- `FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Hunting/KhezuHuntingController.cs`, `KhezuHuntRotation.cs`, `KhezuPhotonSync.cs`: extend existing outcome states, rotation and checkpoint validation.
- `FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Cinematics/VFX/CrimsonBarrier/CrimsonMathBarrierVfx.cs`, `KhezuBarrierVfxPresentation.cs`: align the existing fade to the accepted outcome timestamp.
- Existing cinematic/VFX Editor check sources are updated to expect persistent barrier protection and the connected outcome panel.

New implementation files: `Player.MovementLocks.cs`, `HealthManager.Multiplayer.cs`, `KhezuHuntingController.Barrier.cs`, `KhezuCaptureEncounterController.Barrier.cs` extend their existing partial classes. `KhezuBarrierDevelopmentUI.cs` and `Editor/KhezuBarrierSetup.cs` are isolated in this testing folder. Editor check/build helpers are excluded from player compilation.

## Verification

Current evidence is in `Reports/`. The full Play Mode test uses real awakening, navigation/capture, original cinematic, virtual Input System keyboard/mouse and the existing mobile input path. It holds movement/jump/run input, attempts direct CharacterController movement and transform teleport, checks root motion, tests both real mouse-click outcomes, waits the full thirty-second nest sleep, replays health checkpoints, and checks external restriction, observer and death fixtures. A separate PUN OfflineMode test uses the actual spawned network avatar, owner movement stream, authenticated event dispatch, room checkpoint and disconnect cleanup.

Final results: **40/40 Editor gameplay checks** and **18/18 PUN OfflineMode checks**, with zero runtime errors. The network run applies the existing `TakeDamage()` before a barrier failure and confirms a further single-heart loss. The gameplay run also confirms that restoring health releases its health token while preserving an unrelated restriction. Release-panel stripping and static mouse/touchscreen action bindings passed. The 499-file source-protection audit allows the 16 documented script changes and confirms the other 483 authored files are unchanged.

Fixture checks and a one-actor offline room do not replace live 2–4-client testing. Native Android touch/device performance, latency, ownership changes, live disconnects, late joins and Master migration must be reported separately from Editor results. See the JSON reports for actual pass counts and build results; do not infer a device test from a successful APK build.

Android verification reached native IL2CPP compilation, then its packaging worker was intentionally stopped because the first full build was taking too long and no Android device was connected. No APK was produced; final Android compilation/runtime testing remains unverified. The build-failure dialog was dismissed, release signing restored and the active platform returned to Windows 64-bit. No further APK build is queued.
