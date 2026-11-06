# Latest skins — local integration handoff

Updated: 2026-11-06. These are local working-tree changes. Nothing was committed, pushed, merged, deployed, or written to production account data.

## Implemented locally

### Source-motion restoration (2026-11-06; supersedes earlier stride fixes)

The widened gait and foot jitter were caused by the procedural PlayerStrideWarp
leg solver and pelvis/floor corrections added during playback tuning. Those
changes are rejected. All twelve Gameplay prefabs now have no stride component.
The legacy PlayerStrideWarp type is inert serialization compatibility only: it
has no update callback and cannot write transforms. Do not reattach a solver.

PlayerAnimator now changes playback clocks only. Animator global speed stays 1;
Walk/Run keep the actual assigned clips and use bounded per-state cadence with
.18-second smoothing. Cadence follows actual permitted movement, including the
existing .95/.88/.80/.75 carrying penalties. Idle, Carry, Jump and the one-shot
Stun timeline retain their previous behavior. No new locomotion clip, curve edit,
limb offset, pelvis lowering, foot IK, root motion or character rescaling is used.
LatestSkinPlaybackSetup.Apply() now removes legacy stride components and sets
playback values only. It never searches for a wider stride or changes a pose.

Keep SharedSkinAnimationRetarget only for the explicitly requested donor clips.
These Generic rigs have different bind bases, so their static bind-space rotation
conversion and hip translation/proportion conversion remain necessary. Hip origins
now come from mesh bind matrices, not an animated Idle pose that displaced
borrowed gaits vertically. Its
phase-dependent ground curves and per-frame sole/floor correction are removed.
The source path is preserved rather than forced to follow a procedural floor.
The existing upper-body carry support is retained and does not write leg bones.
Execution order: animation driver 100, donor bind conversion 120, carry support
150, item alignment 250. There is no stride stage at 130.

Current verification: LatestSkinSourceMotionChecks.Static(), followed by
LatestSkinPlayChecks.BeginSourceMotion(). Read Integration/SourceMotionChecks.txt.
It compares final lower-body poses against the same assigned source clip at the
same phase, after required donor bind conversion, and exercises all twelve skins
using real GameInput/CharacterController movement, turning, transitions, carrying,
four movement penalties, run delay and jumping. Accounts and rewards are excluded.
Earlier PlaybackChecks/FootJitterChecks results describe the removed solver and
must not be treated as current locomotion acceptance results.

Verified locally: all twelve source-motion Play Mode cases passed, including
continuous clocks, turns/blends, carrying, jumping and the four weight levels.
Both legs retain original offsets; final source-pose differences stay below
.44 degree. All 48 inherited visuals passed, and all 300 source hashes match.
See Docs/QA/SourceMotionRestore-2026-11-06.txt for the detailed results.

Source stride and unchanged gameplay travel speed are independent. At the base
prefabs' 13-unit walk / 19.5-unit run, slowing a short in-place source gait can
leave source-to-ground sliding. Do not compensate with wider procedural strides,
bone edits, a hidden movement-speed change or excessively fast playback. Physical
Android and live multiplayer peer checks remain separate acceptance work.

### Reused animations (2026-11-06)

These nine state assignments reuse the existing prepared Gameplay animation
assets. No new `.anim` files, source-clip edits, Humanoid conversion, or Avatar
replacement is required.

| Destination | State | Existing donor |
| --- | --- | --- |
| Male Uniform | Walk, Run | Male Elite |
| Male Special | Walk | Male Elite |
| Male Rare | Walk | Male Elite |
| Male Epic | Jump | Male Common |
| Male Common | Walk, Run | Male Elite |
| Female Uniform | Run | Female Special |
| Female Epic | Walk | Female Rare |

The rigs are Generic Mixamo rigs with no Humanoid Avatar. Their bind bases and
limb proportions differ. `SharedSkinAnimationRetarget` corrects the donor's
animated rotations using both skeletons' mesh bind matrices. Destination limb
offsets remain intact; only the required hips translation is scaled/rebased for
the destination rig. There are no dynamic grounding curves or sole corrections.
Run/Jump retain their authored airborne paths. CharacterController owns movement
and gravity. The source-motion restoration above supersedes older ground-fit tests.

The adapter runs after animation and before PlayerCarryRig. It weights its
correction through the existing transitions and excludes the destination Carry
layer's contribution on masked bones. Own Idle, CarryIdle, Carry, Stun, and other
unlisted states keep their existing clips. Each replacement has a recalibrated
source foot speed or jump duration; its own playback range permits the existing
movement speed without changing Player speeds or run permissions.

Include the new runtime adapter, the seven affected Gameplay prefabs/controllers,
updated Mapping.json and their `.meta` files together. Player/resource/menu visual
instances inherit these prefab changes; do not copy whole teammate scenes.
`LatestSkinAnimationReuse.Requests` centralizes the mappings. Its `Apply()` helper
only updates these states and prefab calibration. `LatestSkinIntegration.BuildVisuals()`
reapplies them at the end, but importing the prepared assets is preferable to
rebuilding the preparation pipeline.

After merging into a review copy: compile and run
LatestSkinSourceMotionChecks.Static(), then LatestSkinPlayChecks.BeginSourceMotion().
Read Integration/SourceMotionChecks.txt. The earlier AnimationReuseChecks ground-fit
results are historical; they describe corrections that have now been removed.
Retest physical Android and two multiplayer peers separately. Keep this follow-up
with the character integration group, without production account changes or Git
operations. All 300 original animation/FBX/importer hashes remain unchanged and
there are still 156 animation assets.

### Earlier integration

- Twelve gender/category replacements, behind the **existing equipment identity GameObjects**. Existing item IDs, ownership keys, rarity entries, default IDs, and menu model references are retained.
- Base player assets: `Assets/Prefabs/PREFABS/CHARACTERS/Player.prefab` and `Player_Female.prefab`. Their `Assets/Resources/Multiplayer_Male.prefab` / `Multiplayer_Female.prefab` variants inherit the replacement visuals.
- `Assets/Scenes/Main Menu.unity`: the shared shop, customization, and profile mannequins use the same new skins. Their manager arrays still reference the same identity objects and IDs.
- The 10 existing transparent shop portrait PNGs in `Assets/Art/ShopPortraits` now show the corresponding new models. Dimensions and sprite GUIDs are retained, so the existing card layout/references remain valid.
- New `_Gameplay.prefab` variants in each skin's `Prefabs` folder. The earlier `_Skin.prefab` preparation outputs are retained separately.
- Matching Idle/Walk/Run/Jump/Stun state machines; a Generic transform mask blends Carry onto the torso/arms without replacing locomotion in the legs. The existing `Player` still owns movement, jump physics, three-second run activation, running permissions, and speeds.
- Matching Stun is driven by `DynamicDoorTrigger` through `PlayerAnimator.BeginStun`. The derived clip falls once, holds a grounded pose for the remaining penalty, and recovers once. It does not loop. The supplied Stun FBXs contain a fall but no authored get-up; the derived recovery follows that same skin's pose trajectory in reverse. A bespoke authored get-up can replace this later without changing gameplay timing.
- PRELIM hammer timing: Stun now starts in the same impact step as knockback, after the existing hammer wind-up. BeginStun commits the Stun state and first pose immediately, clears locomotion flags and the carry layer, and ignores repeated signals while that hit is active. It no longer waits until displacement ends. The animation interval includes the existing knockback interval; the original total control-lock deadline, displacement, penalties and grounded recovery are preserved. This fix needs the shared PlayerAnimator and DynamicDoorTrigger changes together; no controller or source-clip replacement is required.
- PRELIM stun camera: the trap now acquires `GameInput.AcquireGameplayActionBlock(this)` and releases only that owner. `GameplayActionsBlocked` guards movement/jump/hand/run, while the original `GameplayInputBlocked` remains the modal flag that menus/tutorials snapshot and the camera checks. Player stays enabled so CameraTargetController and Cinemachine keep rotating/following; Player.Update skips locomotion while an action owner exists. Do not change camera routing to use GameplayActionsBlocked. Settings/Help/tutorial locks retain their existing global input/Player/camera behavior, and trap cleanup never overwrites them. Include GameInput, Player, MobileInputUI, DynamicDoorTrigger and the Admin/hint action-gate changes together.
- A stable player-space carry socket uses a reference calibrated from each skin's Carry pose, rather than chasing animated hand positions every frame. Hand-span fitting uses the same stable reference. Visible-mesh pivot compensation uses mesh-space bounds, independent of the item's original world rotation. Inventory, pickups, answer data, drop physics, and water recovery remain owned by their existing stage scripts.
- Carried items retain their original mesh and pickup world scale. CarriedObjectAlignment compensates for scaled parents and never creates a smaller substitute mesh. Full-size items clear the torso through upward/forward positioning; stable two-bone arm support places both hands symmetrically around the bottom item. Original physics and item identity stay on the same object.
- Proof scroll carry visuals use the shared socket. The shared GrabbableObject integration covers Truth Table blocks, inference crystals, Sets grabbables, and Induction tablets; `SetElement` also supports the parented carry path.
- Shop/profile camera fitting uses actual skinned vertices for the new outfits rather than their conservative all-animation culling bounds.
- `PlayerEquipmentLoader.SameEquipmentID` accepts the old prefab spelling `male default` as an alias for `male_default`. It does not rewrite account data.
- Admin's **Change Character** cycles all 12 skins locally, including the other gender, while retaining the same Player and capsule. **Restore Equipped** restores the account-selected visual. Backend equipment responses are still remembered during the preview; no inventory, ownership, equipped ID, or remote-avatar ID is changed. The public action requires the existing authenticated `AdminAuthorization` decision. Revoking access or leaving the scene removes the preview.
- Graph no longer overrides the shared PRELIM Jump transform or places Admin above the hand. Existing mode-specific hiding remains in place; tutorial targets still reference the actual controls.
- All ten solo trials explicitly enable Admin during `TrialSessionServices.SceneLoaded`; eligibility comes from the shared TrialCatalog. Each saved scene has one enabled controller and a connected gameplay Canvas. The button remains restricted to verified Admin accounts and stays hidden while Help/tutorial/reading modals own input. Garden, multiplayer and menus are unchanged.
- Admin rebinds the shared input when it initializes late, so a missing input reference at startup cannot permanently hide the entry point. All ten saved trial scenes passed authorized Admin opening and Show Hint through simulated touch at three mobile layouts, with ordinary-account actions rejected; see `Integration/AdminAvailabilityChecks.txt`. Editor tests use a temporary, unsaved InputSettings copy so simulated touch works in the background without changing the game's input settings.

## Truth Table session and visible-stack follow-up

Current inventory restoration (2026-11-06) supersedes the earlier always-visible
stack requirement. Tap a selected inventory slot to hide/unequip its value; tap
again to equip it. Only the selected value is visible. All stored identities
still count toward the four-block capacity. Counts and ownership are not consumed
by hiding. Weight and carry pose depend on equipped blocks; no equipped stack
means normal speed and the ordinary empty-handed run rules.


- Each new Truth Table scene owns a fresh six-house question/answered state. Temporarily disabling the same controller retains its live state and restores the proper door angle/collision on re-enable. Input subscriptions are removed on exit, scene-owned block membership prevents stale pickup targeting, and spawners destroy their remaining identities on scene teardown. Quiz penalties, bonus event keys and answer validation are unchanged.
- Inventory holds at most four real True/False identities. Collection equips the matching value. UI taps use ToggleSelectedSlot; repeated taps hide and re-equip that stack. SetSelectedSlotDirectly remains an idempotent equip operation for pickup/placement code. Slot counts derive from identity lists, and duplicate pickups cannot increment counts. Rejected columns return the same identities to inventory.
- `TruthTableCarryStack` creates one socket per identity below the existing hold point. Items retain their original visible and physical world size (the supplied True/False blocks measure about 2.64 by 2.25 by 2.25 units). Vertical spacing uses each full mesh height plus .025 spacing, with .14-second compaction. Newly collected items start in their own stack position. All carried colliders are disabled and rigidbody interpolation is off while held. Drops restore world physics outside the capsule; released items return to their original scene so water recovery remains active.
- Default weight settings are on the scene's InventoryManager: one block .95, two .88, three .80, four .75. Multipliers apply to the existing player walk speed and the number of equipped blocks. Unequipping removes the modifier and run suppression without deleting inventory. Values are configurable and validated to decrease strictly with weight. The manager owns a movement modifier and run suppression, removing both when empty or destroyed. Empty-handed running still uses the existing three-second activation. Jump force, gravity and countdown rules are unchanged.
- All 12 controllers now have a derived `CarryIdle` pose: neutral lower body, torso and shoulders from that skin's Idle first pose, with matching Carry arm/finger rotations. The upper carry layer uses this stable pose while the legs retain ordinary walking/jumping locomotion. This avoids Male Uniform's moving idle torso conflict and Female Epic's changing socket/scale. The original six source clips remain intact; each Gameplay folder now also contains `*_CarryIdle_Gameplay.anim`.
- Each skin's minimum forward clearance is measured against its own posed torso/armor in carry idle, walk and jump. Include the updated Gameplay prefabs as well as controllers and derived clips; copying only the runtime scripts loses those calibrated references.
- Full-size follow-up: the anchor adds upward/forward clearance from the actual item bounds and a shoulder-height reference. PlayerCarryRig counter-rotates the carrying spine against locomotion hip twist, then solves both arms toward symmetric rear-face grips. Bone lengths, original object proportions and lower-body locomotion remain unchanged. The stack follows this stable centreline rather than animated hand sway. The shared scale-preserving follower also replaces the previous shrink presentation for other carried items; Sets parenting now preserves world scale.
- Shared changes to review with the main programmer: Player (owner-scoped run suppression), PlayerAnimator, PlayerCarryRig, IntegratedSkinProfile, CarriedObjectAlignment, GrabbableObject (held interpolation/collider state), and GameInput (input-action disposal and stale-instance guard). These are shared character dependencies; do not overwrite the teammate's networking or menu files to import them.
- Truth Table files: InventoryManager, InventorySlot, MobileInventoryUI, TruthTableCarryStack, TruthBlock, TruthBlockSpawner, TruthTableDoorInteraction and its Presentation partial, Help/onboarding content, and the scene's four weight/stack settings. Menu and multiplayer scene assets are not replaced by this follow-up.
- The earlier full carry/re-entry reports below predate the restored inventory toggle. Current toggle coverage: LatestSkinPlayChecks.BeginInventory(), Library/InventoryToggleChecks.txt. It passed both values at sizes 1–4, repeated Input System touch toggles, mixed-value switching, original identity/scale, placement, return, drop and reset. Older carry fixtures that assert every stored block stays visible represent the superseded policy. Concise current Admin answers in Formal, Direct/Indirect and Induction use their actual answer flags, accepted orders and formula mappings; bank navigation and authorization remain unchanged. InventoryAndAdminPresentationChecks.Answers() checks all 150 bank variants against validation.
- Reproduce the offline regression using `StageHelpGameplayRecorder.CheckTruthCarry()` outside Play Mode with clean saved scenes. It uses the real TruthTable scene and a temporary copy of the actual menu presentation with application/account scripts removed before Awake. This exercises scene teardown/re-entry without touching real account data. The temporary fixture is removed afterward. Physical Android and the real authenticated menu/lobby flow remain acceptance checks for the integration copy.
- Completed Editor checks: three TruthTable entries with two intervening menu-scene visits, all 18 house quizzes, concealed block prompts behind closed doors, all 12 skins at all four stack sizes, repeated inventory selection, turning, sustained run attempts, jumping/landing, dropping without capsule lift, compaction, incorrect-column return and correct resubmission. See `TruthTableCarryReentryChecks.txt` and `TruthTableCarryChecks.txt` beside this handoff. These checks simulate mobile touch and isolate backend writes; they are not a physical Android or live-account certification.
- The shared repeated-pickup/clone/non-player-socket regression passed all 12 again after the neutral-pose changes (`SocketChecks.txt`). Actual TruthTable drops, exact column placement and two water recoveries each for True and False also passed without duplicates or capsule lift (`Library/TrialInteractionRepairsChecks.txt`). The Editor was restored to clean, stopped TruthTable with temporary QA scenes removed. The historical snapshot-comparison checker could not be rerun because its original Temp backups were absent; no replacement baseline was created.
- Latest full-size verification: all 12 skins passed all four stack sizes at .95/.88/.80/.75 speed, symmetric hand support while walking/turning/jumping, stable idle, no body intersection, original visible/physical sizes, compaction and correct/incorrect submission flows (`TruthTableCarryChecks.txt`). All 900 sampled Walk/Run/Jump poses passed (`TruthCarryPoseReach.txt`; reproduce with `TruthTableCarryChecks.PreviewArmReach()`). Three nonuniform anchor scales and release retained the original True block size (`TruthCarryScaleChecks.txt`). The updated socket test also passed all 12 without creating replacement render meshes. These are Editor results; physical Android remains an integration check.

## Old-to-new mapping

| Existing saved item ID | Existing identity/model | New gender/category |
|---|---|---|
| `male_default` (legacy prefab: `male default`) | `UNIFORM_MALE` | Male Uniform |
| `Common_Skin` | `eka_costume1` | Male Common |
| `Elite_Skin` | `eka_costume2` | Male Elite |
| `Rare_Skin` | `eka_costume3` | Male Rare |
| `Epic_Skin` | `eka_costume4` | Male Epic |
| `Special_Skin` | `eka_costume5` | Male Special |
| `female_default` | `UNIFORM_FEMALE` | Female Uniform |
| `Common_Costume` | `COSTUME2_FEMALE` | Female Common |
| `Elite_Costume` | `COSTUME1_FEMALE` | Female Elite |
| `Rare_Costume` | `COSTUME5_FEMALE` | Female Rare |
| `Epic_Costume` | `COSTUME3_FEMALE` | Female Epic |
| `Special_Costume` | `COSTUME4_FEMALE(1)` | Female Special |

Category is determined from the **serialized item ID**, not the old costume number. Original `Elte` folders are retained and contain correctly named Elite assets. `male_tuxedo` and `female_tuxedo` remain intact because no corresponding new Tuxedo was supplied; do not remove those owned inventory entries.

## Files and animation policy

Runtime scripts:

- `Assets/Scripts/Scripts/IntegratedSkinProfile.cs`
- `Assets/Scripts/Scripts/PlayerCarryRig.cs`
- `Assets/Scripts/Scripts/CarriedObjectAlignment.cs`
- `Assets/Scripts/Scripts/PlayerAnimator.cs`
- `Assets/Scripts/Scripts/Player.cs` (jump-flight-duration property, owner-scoped carrying run suppression, and Truth Table drop routing)
- `Assets/Scripts/Scripts/GrabbableObject.cs`
- `Assets/Scripts/Scripts/GameInput.cs`
- `Assets/Scripts/Scripts/MobileInputUI.cs` (joystick action-lock guard)
- `Assets/Scripts/Scripts/GameManager/TrialContextHints.cs` (pause hint timing during action-only restrictions)
- `Assets/Scripts/Scripts/PRELIM/Truth_Table_Script/TruthTableCarryStack.cs`, `InventorySlot.cs`, `MobileInventoryUI.cs`, `TruthBlock.cs`, `TruthBlockSpawner.cs`, `TruthTableDoorInteraction.cs` and its Presentation partial.
- `Assets/Scripts/Scripts/PlayerItemRelease.cs`
- `Assets/Scripts/Scripts/Debug/AdminCharacterCatalog.cs`, `AdminCharacterOverride.cs`, `AdminDebugController.cs`
- `Assets/Scripts/Scripts/GameManager/TrialSessionServices.cs` and the Admin Canvas references in PRELIM, TruthTable, RulesOfInference and Setssss scenes.
- `Assets/Resources/AdminCharacterCatalog.asset` (12 prefab references; include its `.meta`)
- `Assets/Scripts/Scripts/Graph/GraphUIController.Layout.cs`
- `Assets/Scripts/Scripts/PRELIM/PropositionalLogic_Script/DynamicDoorTrigger.cs`
- `Assets/Scripts/Scripts/PRELIM/Truth_Table_Script/InventoryManager.cs`, `DynamicLogicPuzzle.cs`, `LogicPuzzle.cs`
- `Assets/Scripts/Scripts/Sets/SetElement.cs`
- `Assets/Scripts/Scripts/FormalInformalProofs/ProofsLibraryStageController.cs`
- `Assets/Scripts/Scripts/FormalInformalProofs/ProofsLibraryStageController.Scrolls.cs`
- `Assets/Scripts/Scripts/Firebase/PlayerEquipmentLoader.cs`
- `Assets/Scripts/Scripts/Shop_Customization_Manager/CharacterInspectView.cs`

Each skin owns `Animations/Gameplay/`, including its controller, six gameplay clip copies, a derived centred `CarryIdle` pose, and a `CarryUpperBody.mask`. Original extracted `.anim` files, FBXs, materials, native rigs, and proportions remain separate. Gameplay copies loop Idle/Walk/Run/Carry; Jump and Stun are one-shots. CarryIdle is a stable neutral standing pose; the masked upper layer uses it during movement. PlayerCarryRig applies symmetric, shoulder-width arm support after animation, using the full-size item bounds. It preserves bone lengths and leaves leg locomotion intact. Duplicate horizontal hip travel is removed. Jump's hip height is held at the idle baseline so animation cannot add a second physical jump. Original source curves are retained in the preparation assets.

The source-motion restoration above supersedes all procedural stride fitting. Prepared skins use playback-only cadence limits, .18-second rate smoothing and proportional carrying slowdown. Global Animator speed stays one, root motion stays disabled, and the prepared uniform scale remains unchanged.

Jump copies trim the source's anticipation/idle tail before fitting the airborne portion to `Player.JumpFlightDuration`. Launch force, gravity, buffering and landing remain unchanged. StunTime is a normalized animation-time parameter controlled by the driver; it advances monotonically through fall/hold/recovery. Ground height is baked from actual skinned bounds. The carry layer is disabled immediately during stun. Hammer knockback retains capsule collision, and a small downward grounding move keeps the stunned capsule on the floor. Total penalty defaults to five seconds and is configurable on DynamicDoorTrigger (up to 30 seconds); a shorter setting cannot cut off recovery.

Drops now move the original physical bounds outside the player capsule before re-enabling physics. `PlayerItemRelease` ignores only item/carrier collision pairs until they separate, then restores those pairs. World collisions remain enabled. Truth Table placement atomically clears the hand and carry follower before assigning the exact answer slot, keeping slot collisions disabled as before. Formal scroll carry size is measured from the mesh (1.4-unit longest side before hand fitting), independent of FBX/parent scale; original altar layout sizing remains unchanged.

Editor-only tools and reports:

- `.../LATEST_SKINS/Editor/LatestSkinIntegration.cs`: builds local gameplay variants, applies player/menu replacements. Do not run indiscriminately on unrelated scenes.
- `.../LATEST_SKINS/Editor/LatestSkinPlayChecks.cs`: isolated offline fixture; disables cloud-loading entry points and restores the original scene afterward.
- `.../LATEST_SKINS/Editor/LatestSkinDebugChecks.cs`: temporary Admin visual cycling without a backend session, repeated no-lift releases, actual Truth Table placement, and actual proof-scroll mesh checks for each skin.
- `.../LATEST_SKINS/Editor/LatestSkinPortraitSetup.cs`: reproducible transparent shop portrait renders, retaining the existing PNG paths/GUIDs.
- `.../LATEST_SKINS/Integration/Mapping.json`: exact asset/controller paths and calibration values.
- `.../LATEST_SKINS/Integration/PlayChecks.txt`: actual Play Mode results. A result is a pass only when its final line says all 12 passed.
- `.../LATEST_SKINS/Integration/StaticChecks.txt`: preserved player IDs/physics, Resource variants, unchanged shop metadata, and all 36 menu preview mappings/framing checks.
- `.../LATEST_SKINS/Integration/SocketChecks.txt`: all 12 skins, repeated pickup, synchronous Sets-style replacement cloning, source appearance/scale restoration, and transfer to a non-player altar socket. Run with `LatestSkinPlayChecks.Begin(true)`.
- `.../LATEST_SKINS/Integration/StunGeometryChecks.txt`: all 12 derived stun clips sampled through fall and recovery, with grounded mesh bounds and an upright final pose.
- `.../LATEST_SKINS/Integration/ImpactChecks.txt`: all 12 skins, same-call state entry from Idle/Walk/Run/Jump/CarryIdle, Stun on the first physically displaced frame, duplicate-signal protection, unchanged recovery lock/displacement, grounded mesh/capsule and clean return to Idle. Reproduce with `LatestSkinPlayChecks.BeginImpact()` outside Play Mode with saved scenes. It reads the actual PRELIM door settings into the isolated fixture (currently .32-second wind-up, 18-unit knockback, 5-second total recovery) and does not write account data.
- `.../LATEST_SKINS/Integration/HammerCameraChecks.txt`: saved PRELIM scene and all 12 skins with simulated mobile swipes before, during knockback, during recovery and afterward. Checks rendered-camera follow, blocked joystick/jump/hand/run, Settings-over-stun pause/resume, intentional tutorial camera locks and trap-abort cleanup. Run `StageHelpGameplayRecorder.CheckHammerCamera()` outside Play Mode with clean saved scenes; it uses the offline backend seam and does not replace Help videos. Tests passed in the Editor; physical Android remains unverified.
- `.../LATEST_SKINS/Integration/LoopChecks.txt`: all 12 Idle/Walk/Run/Carry endpoint checks. The two largest supplied Walk seams (Female Epic and Male Special, about 9–10 degrees) are corrected across the cycle in gameplay copies, without an inserted hold or extra frame.
- `.../LATEST_SKINS/Integration/Previews/`: actual running/carrying screenshots.
- Local safety copies of the replaced player/menu files are in `Temp/LatestSkinsIntegrationBackup`; Temp is not a merge artifact.

## Backend and save-data compatibility

No backend deployment, schema migration, wallet change, ownership grant/reset, price change, or inventory reset is required for these visual mappings. Keep `Male_Character` / `Female_Character`, all existing item IDs, and `users/{uid}/equipped/clothes` unchanged. Existing catalog and security rules continue to authorize purchases and equipping. Do not rename database IDs to the new prefab filenames.

Authenticated login/equip/purchase/save-roundtrip tests must be done on test accounts or the existing sandbox by the programmer; this work does not claim production-account verification. Multiplayer locomotion still uses the existing equipment-ID and movement synchronization. Remote carried-item/Carry-pose replication was not expanded here; validate the project's existing item replication and add authoritative remote carry-state synchronization if required for network play. This is distinct from the implemented solo/local carry behavior.

## Completed local verification

- All 12 saved equipment selections passed real offline Play Mode movement, original run delay/speeds, matching jump/landing timing, carry idle/walk/run/jump, full-size/off-centre mesh alignment, drop/return cleanup, and the actual hammer knockback/full-stun coroutine.
- Follow-up: all 12 passed three consecutive natural releases without capsule lift, exact Truth Table slot placement, and two collections of the actual proof-scroll mesh at a readable world size. Stun progress stayed monotonic, the capsule stayed grounded, and the Carry layer stayed off throughout recovery.
- Graph's real UI passed raycast-driven touch checks at three mobile Game-view layouts: PRELIM Jump/Admin transforms, all 12 picker names, both cycling directions, Restore Equipped, nested Back/Close, and automatic visual/input cleanup on access revocation. The existing challenge/question clearing, answer viewer, numerical-mode restoration and reward-exclusion checks also passed. The authenticated decision used the existing Editor-only fake backend seam; no production account was changed. Report: `Library/GraphRevision/Admin.txt` (generated QA, not a merge dependency).
- Actual TruthTable and Setssss saved scenes also passed simulated-touch and physics checks using `StageHelpGameplayRecorder.CheckInteractionRepairs(scene)`. Both True/False inventory drops, actual column-slot placement, water recovery, and repeated numbered-element drops retained identity and did not lift the player. Report: `Library/TrialInteractionRepairsChecks.txt`. The original PRELIM Editor scene, Game-view size, safe-area setting and question-history preferences were restored; no Help recordings were replaced.
- All 12 passed the targeted repeat-pickup/clone/altar-socket regressions. The non-player socket check protects the inference crystal's existing `ConfigureInventoryState(true, socket, false)` placement flow.
- All 36 shop/customization/profile mappings and preview-camera framing checks passed. Serialized IDs/defaults, movement values, stored shop prices/currencies/gender restrictions/default metadata match their pre-integration snapshots.
- Unity scripts compile cleanly. These results do not replace authenticated account tests, full stage acceptance runs, multiplayer peer tests, or a physical Android build.

## Integrating with a teammate's newer menu or multiplayer work

Use the teammate's latest Main Menu and multiplayer scenes as the integration base. Do not replace their scenes with this working folder's copies. This checkout also contains unrelated lesson, lighting, economy and backend changes; preserve those locally and review them separately from the character handoff. The teammate's latest files have not been compared here, so a conflict-free integration is not yet verified.

1. Preserve both developers' working folders first. Perform the eventual integration in a separate review copy based on the teammate's latest project. Do not pull, switch branches, reset, or discard this large uncommitted working tree to obtain that base. No commit, push, merge or deployment is authorized by this document.
2. Import `Assets/Characters_NEW/LATEST_SKINS` with its existing `.meta` files, the new character runtime scripts, `Resources/AdminCharacterCatalog.asset`, and the matching portrait assets. Check for same-path assets already present in the integration copy before replacing them. Reuse the prepared assets; rebuilding clips is unnecessary for the handoff.
3. Compare and combine shared script changes. Start with the teammate's current versions of Player, PlayerAnimator, PlayerEquipmentLoader, CharacterInspectView, GrabbableObject and any affected stage/carry scripts. Port the character-related sections while retaining their current networking, menu, inventory and backend behavior. The files listed earlier are a review inventory, not permission to replace every file wholesale. Admin startup/picker changes can be reviewed as a separate group.
4. Update only the visual identity children and carry-rig references in the teammate's latest `Player.prefab` and `Player_Female.prefab`. Keep the Player root, CharacterController, PhotonView, network components, prefab GUIDs and equipment IDs. The `Resources/Multiplayer_Male.prefab` and `Multiplayer_Female.prefab` variants inherit these bases; this character integration did not require replacing those variant files. Retain the teammate's variant changes and inspect inherited visuals and overrides afterward.
5. In the teammate's latest Main Menu scene, update the existing character mannequin identity children and preview references for CustomizationManager, ShopPreviewUI and MainMenuProfileSection. Preserve the rest of their menu hierarchy, buttons, services, layout and serialized references. `LatestSkinIntegration.ApplyToMainMenu()` is a narrowly targeted helper, but review its Replace method before using it: it rebuilds the visual children inside each matched character identity. Use it only in the backed-up review copy, inspect its diff, and keep changes limited to character identities. Do not run the complete preparation/build pipeline on the teammate's project without review.
6. Keep the teammate's multiplayer scenes and spawner/lobby flow. They already spawn the shared `Multiplayer_Male` / `Multiplayer_Female` resources. Verify that their latest equipment and animation synchronization works with the updated visual hierarchy; do not replace network code with an older copy to force compilation. Preserve Photon prefab names, object identities and ownership handling.
7. Keep their current Build Settings / Build Profile configuration. Character replacement by itself does not require copying `ProjectSettings/EditorBuildSettings.asset`, adding legacy multiplayer scenes, changing currency prices, deploying Backend/commerce, or modifying account data.
8. Compile and review the combined diff before the eventual approved integration. Verify all 12 saved equipment IDs, menu previews, owned/unowned selection, solo carry/stun/release behavior, and two connected multiplayer clients seeing the correct skins and animations. Test mobile input in the final build. Existing Editor-only results do not establish compatibility with unseen teammate changes or live network peers.

The main programmer should integrate this one character-focused group first. Keep the remaining local trial, environment, library and economy changes intact for their own review groups. Resolve a shared-file conflict by preserving both required behaviors, not by selecting one developer's entire file.

## Merge and setup checklist

1. Follow the teammate-integration order above. Review the character scripts, player-prefab visual changes, Main Menu character references, new gameplay assets, and **their `.meta` files together**. Apply the character edits to the latest shared files instead of replacing the teammate's entire scenes or prefabs. Keep existing player/resource prefab GUIDs and identity references.
2. Open the project in its recorded Unity version with URP. Refresh/import; wait for scripts to compile. Do not regenerate or discard GUIDs.
3. Inspect both base player prefabs and Resource variants: six new category visuals plus retained Tuxedo entries, one Player/CharacterController, one carry rig, and only the selected outfit active.
4. Inspect Main Menu's CustomizationManager, ShopPreviewUI and MainMenuProfileSection arrays. Each of the 12 IDs must resolve to the corresponding gender/category. Verify shop/profile camera framing at mobile aspect ratios.
5. Run the opt-in offline checks with `LatestSkinPlayChecks.Begin()` from the Unity MCP execute-code tool, outside Play Mode and with clean saved scenes. Read the complete report; confirm original scenes are restored afterward.
   Run the targeted socket/replenishment checks with `LatestSkinPlayChecks.Begin(true)`. `LatestSkinStaticChecks.Run()` also requires the original pre-integration snapshots in `Temp/LatestSkinsIntegrationBackup`; Unity may remove Temp, so do not manufacture a new baseline from the modified assets if those snapshots are missing. Compare against the preserved original project instead. `LatestSkinPlayChecks.RestorePending()` explicitly completes any deferred Editor scene restoration when running through MCP with the Editor in the background.
6. In a test account, select every owned outfit, leave/re-enter a trial, restart the app, and confirm the same ID is loaded. Test Uniform fallbacks and the legacy male-default alias without a data migration. Confirm unowned outfits remain blocked.
7. Truth Table: run `StageHelpGameplayRecorder.CheckTruthCarry()` with saved scenes. Test all six doors after repeated Main Menu returns, four visible identities, value selection without unequipping, .95/.88/.80/.75 weight levels, no running while carrying, automatic compaction and empty-handed speed restoration. Verify real menu navigation and touch on Android separately from the isolated Editor fixture. RulesOfInference retains its existing running rules; verify carry, dropped-item recollection and water recovery there without resetting crystal quiz-completion flags.
8. Sets: pick up repeated spawner elements; drop/place/return and resubmit. Formal Proofs: read/get/place/take-back/return scrolls in all five challenges. Induction: collect/replace/return tablets. Confirm each item stays centred and no duplicate object is created.
9. Propositional Logic: trigger an incorrect door and hammer knockback. Check one fall, grounded hold, one recovery, and movement restored after recovery. Check consecutive hits and Game Over interruption. Never re-enable loopTime on Stun.
   Verify that the reaction is already visible on the first knockback frame. Keep BeginStun before the knockback loop, and include knockback time in the animation duration without extending the original recovery deadline. Run `LatestSkinPlayChecks.BeginImpact()` for all 12; the .32-second hammer wind-up is intentional and is not an animation delay after impact.
   Run `StageHelpGameplayRecorder.CheckHammerCamera()` and verify normal swipe rotation/follow during the stun on Android. The trap must not disable Player or set the global modal flag. Movement and interactions stay blocked by the owner-scoped action lock, including after closing Settings mid-stun. Ending/aborting a trap releases only its own action owner and never clears a menu/tutorial camera lock.
10. Authorized test account: open Admin > Change Character; cycle all 12 in both directions while empty-handed and carrying. Close Admin to continue testing. Restore Equipped, switch/logout accounts, and change scenes. Confirm previews are local and saved equipment/ownership remains unchanged. Ordinary accounts must have no button and must fail the action guard. Include AdminCharacterCatalog.asset and all 12 referenced Gameplay prefabs in the merge.
11. Graph: compare Jump/Admin with PRELIM at multiple landscape safe areas, then enter/exit numerical mode and tutorials. Confirm intentional control hiding still restores the same positions.
12. Test the final Android build on a physical device. Editor checks do not establish device performance, touch behavior, or authenticated backend correctness.

The shared `StageHelpGameplayRecorder` now selects the existing male-default equipment identity through the loader, instead of toggling nested PlayerAnimator GameObjects by their old names. Other legacy development-only helpers may need the same change; normal runtime equipment selection is integrated.

## Ready-to-copy prompt

```text
Review and finish the local LATEST_SKINS integration described in
Assets/Characters_NEW/LATEST_SKINS/Integration/HANDOFF.md.

Use your latest Main Menu and multiplayer scenes as the integration base in a
separate review copy. Preserve both developers' working folders. Do not overwrite
your scenes with our complete scene files, and do not pull/reset/discard our
uncommitted working tree. Compare your latest shared scripts and player prefabs,
then port the character-related changes while retaining your other features.
Keep unrelated lighting, lesson, economy and backend changes in separate review
groups. This document does not authorize a commit, push, merge or deployment.

Preserve all existing item IDs, rarity/prices, owned inventory, account balances,
base-character IDs, prefab GUIDs and equipped/clothes save paths. Do not migrate
database IDs to prefab names. The twelve category mappings are already integrated
locally in our base player prefabs and Main Menu mannequins. Tuxedo entries remain
legacy because no replacements were supplied.

Import the gameplay prefabs/controllers/clips/masks and all .meta files together.
Compile, inspect Resource variants, and run LatestSkinPlayChecks.BeginSourceMotion()
in Unity MCP outside Play Mode with saved scenes. Read SourceMotionChecks.txt. Review any older
Editor recording tools that activate nested PlayerAnimator GameObjects directly.

Include the restored inventory toggle as well: InventoryManager, MobileInventoryUI, TruthTableCarryStack, Help wording and their existing .meta files. UI calls ToggleSelectedSlot; gameplay callers keep the idempotent SetSelectedSlotDirectly API. Keep total inventory capacity separate from equipped-count weight. Run LatestSkinPlayChecks.BeginInventory() and InventoryAndAdminPresentationChecks.Answers(); the latter verifies concise Admin answers for all 150 proof-stage questions. Preserve existing Admin authorization and bank navigation.

Include the source-motion restoration near the top: PlayerAnimator, the inert
PlayerStrideWarp compatibility type, SharedSkinAnimationRetarget, all twelve
Gameplay prefab updates and playback-only Editor setup/check helpers. No stride
component belongs on a prefab. Preserve only the requested donor assignments:
Male Elite Walk/Run for Male Uniform/Common, Male Elite Walk for Male Special/Rare,
Male Common Jump for Male Epic, Female Special Run for Female Uniform, and Female
Rare Walk for Female Epic. Keep donor assets unchanged and the required static
Generic bind conversion. Do not restore grounding curves, sole guards, procedural
leg IK or pelvis corrections. Existing carry support remains separate. Run
LatestSkinSourceMotionChecks.Static(), then BeginSourceMotion() on
LatestSkinPlayChecks. Read SourceMotionChecks.txt. Do not use the superseded
stride/reach reports as current locomotion proof.

Use sandbox/test accounts to verify login, owned/unowned equipment selection,
shop previews, profile framing, saved equipment after restart, and unchanged
backend purchase/ownership validation. No production reset or reward grant.

Perform the stage and physical Android acceptance checks listed in the handoff,
including five carry systems, drop/return/water recovery, three-second run timing,
and full hammer stun cleanup. Validate networked carry-state/item replication
separately; this change does not expand that protocol. Keep all gameplay movement,
timers, quizzes, penalties, rewards and progression unchanged. Clearly report
verified behavior and any remaining work. Do not commit, push, merge or deploy
without our explicit authorization.

The follow-up fixes add an authenticated, local-only Admin character picker.
Import Resources/AdminCharacterCatalog.asset and its twelve prefab references.
Do not connect previews to ownership grants, equipment saves or remote RPC IDs.
Check picker cycling, Restore Equipped, and access revocation on test accounts.
Run StageHelpGameplayRecorder.CheckGraphAdmin() for the Editor-only backend
fixture, mobile raycasts and three safe-area layouts. Read Library/GraphRevision/Admin.txt.
Stun is now a non-looping fall/hold/recovery; do not restore its old loop flag.
The provided source clips have no authored get-up, so the derived recovery uses
the same skin's pose trajectory in reverse. Bespoke get-up art is optional polish.
Check PlayerItemRelease collision-pair restoration, exact Truth Table placement,
and actual proof-scroll world size across all skins on a physical Android build.

Include the Truth Table visible-stack follow-up: TruthTableCarryStack and inventory,
door/session and shared carry/input changes, the scene's four weight multipliers,
and each skin's updated Gameplay prefab/controller/CarryIdle clip. Preserve the
teammate's other shared-file changes. Run CheckTruthCarry() and review both saved
TruthTableCarry reports. Test real Main Menu re-entry and physical Android touch;
the automated menu fixture strips application/account code and does not certify
that live flow. Do not restore inventory deselection or running while carrying.
```

