# Android optimization — current implementation and verification

Updated: 10 November 2026. Target: stable **30 FPS / 33.3 ms**, including frame pacing. This is a target, **not an achieved Android result**.

## Device and build status

- Requested phone: Infinix Note 12, advertised 8 GB RAM / 256 GB storage.
- The earlier lighting/geometry pass had no ADB devices. During this input audit, ADB detects **an Android emulator**, reporting ASUSAI2501B / Android 15 with x86_64 support. It is **not the requested physical Infinix Note 12**. The phone's exact model, SoC, GPU, Android release/API, usable RAM and storage remain **unverified**; no chipset is inferred from its name.
- Unity: 6000.3.6f1; active build target Android. Editor rendering tests use Windows / D3D12, GTX 1070 Ti. They cannot establish Android CPU/GPU performance or thermal stability.
- No physical Infinix profile, controlled installed-build comparison, phone GPU frame time, phone memory/GC capture or sustained thermal test is available. An isolated emulator APK attempt failed before installation because Unity rejects the deprecated x86-64 / Magic Leap target. The finally block restored ARM64, the original app ID and debug-signing selection; Main Menu remains clean. No new APK or Android touch result is claimed. The opt-in QA builder now uses ARM64 for future explicitly requested builds; it was not rerun.
- No Git commit, push, branch switch or production-account modification was performed.

## Input and Help/Tutorial audit — current update

### Actual causes and shared fixes — implemented

`GameInput` persists across scenes. Previously, `SoloStageLoader` acquired the same modal input block used by Help. Help/tutorial code captures `GameplayInputBlocked` as its previous state. Capturing it while the loader was active could restore the temporary loading block as a permanent legacy `true` when Help closed. Some initial guides also wait for input to become available, so stale state can prevent their opening. The legacy block itself was not cleared on single-scene entry.

- Added **owned loading blockers**, separate from the Help/tutorial modal snapshot. Loading still blocks movement, jump, hand and camera input, but cannot be captured as the previous Help state.
- The loader acquires/releases only its own loading owner. Closing it cannot release another modal's ownership.
- Single-scene entry clears outgoing legacy input state, movement and stale camera touches before destination Start. Destroyed modal owners are pruned; valid modal owners remain intact.
- `GameplayTouchRouting` includes loading in its camera restriction. Existing stun action-only restrictions, movement speeds, physics, quiz timers and progression are unchanged.
- No extra persistent input manager or scene-specific joystick workaround was added.

Files: `Assets/Scripts/Scripts/GameInput.cs`, `Assets/Scripts/Settings/SoloStageLoader.cs`, `Assets/Scripts/Scripts/Camera/GameplayTouchRouting.cs`.

### Touch/UI inspection and Editor regression results

All six saved scenes already have **one active EventSystem** with **InputSystemUIInputModule** and its multitouch pointer policy. No module replacement or duplicate EventSystem was needed. Tests check actual UI raycasts instead of indiscriminately disabling panel raycasts.

`MobileInputFlowChecks.Begin()` passed:

| Stage | Normal shared-loader entry | Direct entry | Layouts |
| --- | --- | --- | --- |
| Truth Table | Pass | Pass | 800×480, 1920×1080 |
| Rules of Inference | Pass | Pass | 800×480, 1920×1080 |
| Sets | Pass | Pass | 800×480, 1920×1080 |
| Formal vs Informal | Pass | Pass | 800×480, 1920×1080 |
| Direct and Indirect | Pass | Pass | 800×480, 1920×1080 |
| Trees | Pass | Pass | 800×480, 1920×1080 |

These are **Editor tests with queued Unity Input System finger events**, not physical Android touchscreen results. Each of the 24 entry/layout cases checks joystick dragging, a second finger tapping Jump exactly once while the first remains held, independent release, reopening Help, and exiting via Let's Begin without stuck input/time scale. Initial-guide checks include tutorial Skip → No/repeat → Skip → Yes, required practice-input handling, and Trees' challenge introduction. Rules of Inference also checks Book Statue framing during the tutorial transition. The harness waits for the intended startup fade before tapping; an early test failure was a premature test tap during that fade, corrected in the harness.

The checks use saved gameplay scenes and the actual loading/Help/tutorial implementations, an offline avatar and inert backend. They do not answer questions or generate rewards. The original Editor scene, input settings and question-history preferences are restored. See `InputFlowChecks.txt`, `output/AndroidUIAudit/Flows.txt`, and `Assets/Editor/MobileInputFlowChecks.cs`.

The previous loading checks established arrival/readiness only; they did **not** establish Help exit or joystick recovery. These new checks cover that gap. Exhaustive quiz, inventory and puzzle-button/device testing remains separate.

### Camera culling: what was already present

- Unity's **frustum culling** already rejects renderers outside each camera's viewing volume, using renderer bounds. It does not require baked occlusion data. No off-screen gameplay logic or GameObjects were disabled.
- All six inspected Main Cameras already have `useOcclusionCulling = true`. This permits **baked occlusion culling**; it does not bake data. Inspected production scene references have no assigned baked occlusion asset.
- Truth Table and Rules of Inference also render a minimap with another camera. An object outside the gameplay view may still render for the minimap. `Renderer.isVisible` includes other cameras, including the Editor Scene view, and is not proof that the gameplay frustum test failed.
- Formal/Informal, Direct/Indirect and Trees reuse one large combined chamber renderer (`Induction/Stage.fbx`: 683,335 source triangles, 15 native mesh LODs). Its bounds span the chamber. Culling cannot independently discard individual walls inside that renderer. See `CombinedGeometryAudit.txt`. No arbitrary bounds shrinking or gameplay-mesh hiding was applied.
- The retained enclosed-chamber bake pilot below is used to measure benefit first. No outdoor bake or production rollout is justified without useful measured reductions and dynamic-object/doorway checks. Existing geometry/quality budgets remain in place.

### Android build/testing limitation

`Assets/Editor/MobileAndroidQaBuild.cs` and `output/mobile-performance/qa/MobileAndroidSmoke.cs` retain a reproducible, **opt-in** Development QA setup. It uses a separate application ID (`com.logiclegends.mobileqa`), an offline avatar, no real account, and an extra per-build define. Ordinary builds do not run the harness. The source is staged only under the ignored test-fixture directory. Build settings are restored in a finally block. No production application was installed over or account data changed. The failed x86-64 attempt produced no Android measurements; ARM64 acceptance on the actual phone remains required.

## Earlier lighting, geometry and loading work — preserved

### Lighting correctness and build safety — implemented; Editor verified

1. **Fixed an enabled-state latch in `SoloSceneGraphics`.** A fixture discovered while disabled was permanently excluded, even if its chamber controller subsequently enabled it. The budget now detects changes made by the light's owner and re-ranks it. An isolated regression check confirms later activation works.
2. Baked-only lights no longer compete for the real-time light budget. The budget avoids repeatedly writing unchanged light/shadow and foliage-renderer properties. Foliage shadows are disabled once, with the original state restored when the scope ends.
3. Added `AndroidSoloBuildChecks`: Android player builds fail if any of the three runtime solo pipelines is missing from URP's build inclusion list, is not per-pixel, or lacks its minimum light allowance. It also checks the shared loading-screen reference.
4. **Important finding:** the current checkout already labels all three solo pipelines `RuntimeSoloPipeline` and enables `IncludeAdditionalRPAssets`. All three appeared in Android's actual `TryGetRenderPipelineAssets` list. The general project renderer is Forward+, while solo presets use Forward. A Forward+-only build can strip the Forward additional-light variants, but that mismatch is **not present in the current inclusion configuration**. It cannot be asserted as the cause of the installed phone build without that APK/logs and a device reproduction. Rebuild from this checkout; do not conclude that changing an Editor preset repairs an already installed APK.
5. The current solo pipelines retain per-pixel additional lighting at every preset. Their existing bounds remain:

| Setting | Low | Medium | High |
| --- | ---: | ---: | ---: |
| Active nearby point/spot budget | 6 | 12 | 24 |
| Additional lights per object | 4 | 6 | 8 |
| Android 3D render scale | 0.70 | 0.90 | 1.00 |
| MSAA | Off | Off | 2× |
| Optional post-processing | Off | Allowed | Allowed |
| Main-light shadow ceiling | Off | Limited | User selection |
| Grass retained by stable density selection | 50% | 75% | 100% |
| Grass draw distance, world units | 65 | 100 | 150 |

Additional-light shadows remain off. Flames/emissive fixture surfaces, puzzle text and glowing guidance arrows are not disabled by this budget. Quality remains in the existing `SoloGameSettings` preference; no second graphics-settings system was introduced.

**Editor pixel test:** a point light on the existing solo Simple Lit surface changes mean image brightness from 0.0000 to 0.1806 at Low, Medium and High. This proves the Editor pipelines produce a light pool; it does **not** prove flames/pools on an Android GPU. See `LightingPixelChecks.txt` and `BuildCopyChecks.txt`. Android player-script compilation and a Lit/Simple Lit/flame material validation bundle also passed for the configured Vulkan/GLES3 target (`AndroidLightingBuild.txt`). This was not an APK, native IL2CPP build, installation or device rendering test. The runtime baked-light check uses `Light.bakingOutput`, which is available in the Player, rather than the Editor-only `lightmapBakeType` property.

**Still unresolved on device:** why the particular installed build loses all torch/candle illumination. Current configuration, disabled-state fix and build guards are verified project-side; exact phone rendering remains awaiting device verification. Do not describe the device bug as conclusively reproduced or fixed.

### Heavy environment geometry and physics — implemented, Android scoped

The saved scenes contain 25 streetlamps in Truth Table and 15 in Rules of Inference. Each source lamp has **295,616 triangles** and also uses that detailed render mesh for its MeshCollider. The upper tree meshes contain 96,300 triangles each. These are asset inventories, not visible frame counts.

`AndroidEnvironmentPreparation` reuses the models' existing LODs, preserving positions, normals, tangents, UVs, material-slot order and source bounds. It repacks referenced vertices into separate mobile assets; original FBXs remain intact.

| Model | Source triangles | Low | Medium | High |
| --- | ---: | ---: | ---: | ---: |
| Streetlamp | 295,616 | 7,615 | 15,231 | 30,463 |
| Upper tree geometry | 96,300 | 4,500 | 6,106 | 9,983 |

- `SoloMobileMeshes.asset` centralizes these choices. `SoloTrialVisuals` selects them on Android and updates them when quality changes. Desktop keeps the source models.
- `AndroidEnvironmentBuildProcessor` changes **Android build copies only**, before scene activation/collider cooking: selected decorative render meshes start at the mobile High mesh, and matching decorative MeshColliders use the source's Low LOD. They are not replaced by oversized bounding boxes.
- Static batching is removed only from those targeted build-copy objects so runtime mesh selection can work. Unrelated objects retain their flags.
- Existing floor, path, stairs, water, triggers, board, puzzle, player and interaction colliders are not changed. Materials and transforms are retained.

Read-only preview-scene checks of the build-copy processor:

| Scene | Render filters changed | MeshColliders retained | Collision triangle inventory before → after |
| --- | ---: | ---: | ---: |
| TruthTable | 33 | 81 | 7,727,786 → 527,761 |
| RulesOfInference | 23 | 89 | 4,668,060 → 348,045 |

Counts include inactive assets and are not per-frame physics timings. Shape/contact checks on the phone, including NPC navigation around lamps, remain required. See `PreparedEnvironment.txt`, `CurrentAssetAudit.txt`, `BuildCopyChecks.txt`.

### Android textures — implemented

Six wood, grass and rock/background textures used by the two outdoor scenes now have **Android-only 1024 maximum size / ETC2 RGBA8 overrides**. ETC2 is the conservative GLES3/Vulkan baseline while the phone's exact GPU is unknown. Desktop import limits, source texture files, font atlases, equations, interface art and skins were not resized.

A full 2048→1024 texture at the same format uses one quarter of its texel storage; this is a theoretical reduction, not measured phone memory. Low also retains the existing mip limit. Actual installed memory, close-up sharpness and ETC2 appearance require device checks. The exact paths are in `PreparedEnvironment.txt`. ASTC/device-specific compression targeting remains a benchmark decision.

### Scripts, UI and initialization — implemented

- The actual scene-entry run exposed repeated `CountOfPlayersProperty.Update` exceptions from a legacy Photon demo counter with a missing Text reference. Broken counters are now disabled in solo trial scene setup and late player spawns. Valid counters and multiplayer scenes are preserved. This stops recurring exception/log work; its phone frame-time cost has not been measured.
- Student NPC puzzle snapshots previously rebuilt state strings/arrays every rendered frame. They now refresh at **10 Hz**, while immediate modal/movement restrictions still pause roaming. NPC counts, routes, hints and animations are preserved.
- Student skin/catalog dependencies now use `Resources.LoadAsync` after destination activation, rather than synchronously loading in the scene-loaded callback. The loading readiness check waits for catalog completion; it does not force the whole optional roaming map to finish before play.
- Preparing thousands of environment materials previously happened in one `SoloTrialVisuals.Awake`. Surface preparation now yields between roughly **2 ms work slices**. A single Unity API operation can still exceed that target; no hard frame-time guarantee is claimed.
- Shared Truth Table/Rules of Inference timer formatting reuses the unchanged second's string. The loading percentage label only changes when the displayed integer changes. The loading status no longer allocates the same string every frame.
- Existing prior optimizations remain: event-based accessibility/touch discovery, cached minimap rendering, stable foliage density/distance budgets, cheap cutout foliage shader, bounded incremental NPC navigation and optional post/shadow ceilings. These were already in the implementation, but were missing from the old version of this document.

## Shared loading across stages — implemented; Editor route checks

`SoloStageLoader` reuses `Assets/Prefabs/UI/SharedLoadingScreen.prefab` through a Resources reference. There is no replacement design or second loading prefab.

- One guarded request/screen survives scene activation. Duplicate taps are rejected.
- Destination scenes load asynchronously. Asset-loading progress occupies the measurable part of the bar. Activation/setup use the existing responsive indeterminate bar, without inventing percentages for unknown work.
- The artwork stays visible while Awake/Start, player spawning, shared graphics/material preparation, Graph/Trees/Induction readiness and NPC catalog loading finish. It then shows completion and fades out.
- Android uses Low background-loading priority and restores the previous priority afterward. Surface work is spread across frames; expensive native scene/asset integration still requires profiling.
- Fixed an Induction Help teardown exception that accessed a destroyed HUD during scene unload. Leaving Induction no longer restores old reading controls/time into the incoming scene. Navigation normalizes the outgoing paused time scale before the next stage owns its pause.
- Input now uses its own **owned loading blocker**, separate from Help/tutorial modal snapshots; releasing the loader does not clear Help/tutorial ownership. Countdown updates ignore loading without changing their book/start or penalty rules.
- Solo trial entries, retries, stage navigation, legacy health navigation and wizard stage entry route into this shared path. Networked Photon loading retains its existing authority/synchronization path.
- No forced full GC, preloading all destination scenes, or extra persistent gameplay managers was added.

`AndroidLoadingChecks` exercised all ten actual saved trial scenes, followed by TruthTable and RulesOfInference re-entry, using an inert backend and a test avatar. The first run passed all 12 loads, activation coverage, material/settings readiness and duplicate-request rejection. Ready/reveal times were **4.20–12.10 seconds in the Editor**. Unity allocated memory at ready was roughly **2.05–2.25 GiB**, including Editor content; this is not Android process memory or a measured peak. The final warm rerun passed all 12 routes in approximately **2.57–4.11 seconds**, with **1,968.5–2,172.0 MiB** allocated at ready. Warm-cache and earlier timings are not a controlled before/after loading benchmark. See `LoadingEditorResults.txt` for each route.

These tests open the initial stage state; they do not solve every puzzle, verify authenticated network startup, prove device touch behavior or measure release-build loading. Account rewards/progression were not advanced. The test harness restores its temporary settings and question-history keys.

## Baked lighting pilot — prepared and tested separately, not promoted

The saved chamber inspection found:

- Formal/Informal: 140 active mesh filters, 27 with UV2, 13 marked Contribute GI.
- Trees: 84 active mesh filters, 13 with UV2, none marked Contribute GI.
- The main Induction-based chamber shell and ceiling have no lightmap UVs. No usable baked lightmap set was assigned to the inspected gameplay scenes.

Changing lights to Baked alone would therefore lose illumination. Custom outdoor ground shaders also need baked-GI sampling support before moving their direct light pools to lightmaps. Moving players need suitable light probes. Flickering and moving sources must retain an appropriate dynamic contribution.

`AndroidChamberExperiment` generated secondary UVs on **separate mesh copies** of stable architecture, then baked an isolated copy of the actual Formal/Informal chamber. Gameplay/account scripts were disabled in the copy. Result: **one non-directional lightmap**, 134,712 bytes reported for its color texture at runtime; the EXR file is 282,591 bytes. The warm pools and chamber visibility were inspected in the rendered preview.

Editable pilot: `Assets/Tests/AndroidOptimization/ChamberExperiment.unity`. It is **not in the build scene list**, not referenced by gameplay, and must not be shipped as another trial. Original gameplay scenes/source meshes were not saved or overwritten by the experiment.

**Pending:** production UV/light-probe authoring, stage-specific lightmap integration, moving-character checks and Android lightmap-memory/visual comparisons. The pilot is not a claim that all trial lighting is baked.

## Occlusion measurement — no production rollout

Baked occlusion was tested on the same enclosed chamber copy first. The baked asset is **106,003 bytes on disk**. The reported native asset wrapper is not a measurement of the full runtime culling working set.

- Manual Editor camera submission timings were noisy and `umbraDataSize` reported zero; those readings are inconclusive and must not justify rollout.
- A separate Play Mode comparison uses fixed camera positions, actual chamber art, baked data, warm-up frames and render/CPU counters. See `OcclusionPlayResults.txt` and the captures under `Library/ChamberOcclusion_*.png`.
- The initial run did **not demonstrate a consistent draw/triangle reduction**. Whole-frame CPU varied and includes Editor/wait/shader warm-up. No reliable Android CPU/GPU benefit or full culling-memory cost has been established.
- No occlusion data was assigned to production chambers, libraries or outdoor scenes. No automatic outdoor rollout was made. Doors, dynamic puzzle pieces and moving-object visibility need targeted validation before adoption.

### Current fixed-view retest

Retested the retained bake with 60 warm-up and 60 measured frames per case, 800×480, D3D12 / GTX 1070 Ti / Ryzen 7 3700X. These are **Editor counters**, not Infinix performance:

| View | Draws off → on | Triangles off → on | Scriptable cull ms off → on |
| --- | ---: | ---: | ---: |
| Front | 259.6 → 280.7 | 2,107,038 → 2,112,887 | 0.236 → 0.306 |
| Side | 229.1 → 242.0 | 1,903,768 → 1,936,782 | 0.226 → 0.315 |
| Rear | 198.6 → 198.6 | 1,772,866 → 1,772,866 | 0.282 → 0.312 |

No consistent submitted-work saving was demonstrated; culling cost increased in these samples. Shared Editor rendering and other Editor work make small draw changes/noisy whole-frame CPU unsuitable as a phone comparison. Main-thread readings were 5.76–6.78 ms except one 28.44 ms outlier; they do not establish stable frame pacing. The recorder named `GPU Frame Time` reported roughly 0.10–0.11 ms, but was not validated against GPU profiling, so it is **not accepted as GPU frame time**.

Unity total allocated memory stayed around 2,134–2,135 MiB including the Editor and retained assets. That is not the native occlusion working set or a phone memory comparison. The 106,003-byte baked file and 616-byte wrapper also do not establish total runtime overhead. Front-view on/off captures were inspected: the book, altars, board and architecture remain visible, with no observed static pop-out in that view. Moving-door and dynamic puzzle visibility remain unverified.

**Decision:** retain the isolated bake for further profiling; do not assign it to production or bake open outdoor maps solely because the camera checkbox is enabled. Spatially separating the combined chamber shell is a possible later optimization, requiring an art/visibility benchmark; this update does not split source meshes or change collisions. See `OcclusionPlayResults.txt`.

## Rendering comparison and limitations

`output/AndroidGraphicsAudit/EditorProfile.txt` contains a fixed-camera **800×480 Editor** comparison using actual TruthTable art, with gameplay/NPC/account controllers excluded. After cases explicitly select the Android mesh LODs in the fixture. The legacy baseline and after cases share the camera and build target. This isolates rendering policies, not full gameplay.

Prior reference was retained in `PreviousRenderingBaseline.txt`. Current results and source inventories must not be presented as Android FPS. Current fixed-camera Low: **370.5 → 277.1 draw calls** and **998,985 → 371,579 triangles** (about 25.2% and 62.8% reductions). Main-thread samples were 16.47 → 16.48 ms, including waits/Editor work, so no CPU speedup is claimed. GPU time is unavailable. Geometry and draw reductions establish less submitted work, not a demonstrated stable-30-FPS phone result.

Existing UI microbenchmarks (`before-ui.json`, `after-ui.json`) remain separate: previous accessibility/touch discovery scans totalled approximately 4.77 ms when coincident; unchanged updated component samples were approximately 0.0032 ms combined. Those numbers are not whole-frame or current-device measurements.

## Reproducing the project-side checks

Run the opt-in Editor helpers only outside Play Mode: `AndroidLightingChecks.CheckBuildCopies()`, `AndroidLightingChecks.CheckPixels()`, `AndroidLightingChecks.CompileAndroidValidation()`, and `AndroidLoadingChecks.Begin()`. For the six-stage input regression, use `MobileInputFlowChecks.Begin()` (the shared tutorial QA dependencies are required). The rendering comparison is `SoloGraphicsPlayChecks.Begin()`. For a fresh chamber pilot use `AndroidChamberExperiment.Begin()`; an existing pilot is retained rather than duplicated. `ChamberOcclusionPlayChecks.Begin()` compares its views.

Generated bake/UV fixtures (including the large copied shell mesh) and validation bundles are locally ignored by Git; the Editor scripts regenerate them. Runtime mobile mesh resources and text reports are not ignored. Do not add the experimental scene to the build list. No gameplay scene was changed by these experiments.

## Required Android verification and remaining work

**Implemented** means code/assets exist locally. **Editor verified** means the listed checks ran here. **Verified on Android: none in this session.** All device conclusions below remain awaiting verification.

When the phone is connected, record:

```text
adb shell getprop ro.product.model
adb shell getprop ro.product.device
adb shell getprop ro.soc.model
adb shell getprop ro.board.platform
adb shell getprop ro.build.version.release
adb shell getprop ro.build.version.sdk
adb shell dumpsys SurfaceFlinger
adb shell cat /proc/meminfo
```

Use the GLES/GPU identification from SurfaceFlinger (and the Unity device log), not a guessed Note 12 specification. Do not publish device serials/account tokens.

For before/after captures, retain the same APK configuration, graphics/lighting/shadow preset, 30-FPS selection, resolution/orientation, selected skin, NPC population, question seed and route. Capture after warm-up and again after 15–20 minutes. Use a Development Build/Profiler for diagnosis, then a release build for acceptance.

Record median/p95/p99 CPU and GPU frame times, missed-33.3-ms frames/pacing, render and main thread, GC allocation/collections, memory peak/steady, draw/SetPass/triangles, physics/navigation/UI costs and each load/activation stage. Compare an archived baseline APK to the rebuilt optimized APK; desktop counters cannot replace this.

Acceptance checks:

- Low/Medium/High: visible flames and local torch/candle pools; no black chambers, overexposure or unreadable labels. Test board/tutorial cameras as well as gameplay.
- TruthTable: six house approaches, quiz, four-block carry, table placement/submission, lamps and water recovery; include NPC roaming/navigation.
- RulesOfInference: altar/board, tower quiz and collection, carried/dropped crystal, conclusion and repeated entry; include NPCs.
- All ten trial transitions/retries: one loading screen, no exposed partial stage, no duplicate managers, correct settings restoration and working Help/Back/input afterward.
- Inspect simplified decorative collisions from multiple angles. Paths, stairs, floors and puzzle collisions must remain intact.
- Baked lighting/occlusion: compare on/off on the same enclosed route, including doorways and dynamic pieces; collect culling CPU/memory costs. Enable more areas only after a demonstrated benefit.
- Benchmark remaining large geometry, UI/progress-string allocations, NPC ground/path queries, particles/transparency, asset integration and GPU skinning on the actual device before broader changes.

No stable 30 FPS, Android illumination repair, production-wide bake or successful device build is claimed without these checks.
