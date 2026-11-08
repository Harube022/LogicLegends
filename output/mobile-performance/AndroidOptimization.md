# Android performance notes

The changes provide a cheaper starting point for Main Menu and the ten solo trials. They do not establish a measured Android frame rate. The available measurements are Editor CPU samples and an inventory of the saved PRELIM scene. A phone build is still needed to identify the largest remaining cost.

## Changes already made

- New defaults: Low graphics, Low lighting, 30 FPS, real-time shadows Off and motion blur Off.
- Low graphics renders the 3D scene at 80% of the display width and height. That is about 64% of the full-resolution pixel count; it is not a promise of a 36% frame-time improvement. Overlay menu text and buttons retain their normal resolution.
- The solo renderer uses Forward rendering and automatic intermediate textures. The added screen-space ambient occlusion passes have been removed. The presets do not request HDR, an opaque texture, a depth texture or additional-light shadows. A camera effect that explicitly needs depth can still request it.
- The lamp-correction follow-up uses bounded per-pixel lighting: up to four additional lights per object on Low, six on Medium and eight on High. This replaces the earlier vertex-only Low profile, which could miss small light pools on large low-poly surfaces. Additional-light shadows remain disabled. Main directional lighting, authored ambient colors, glowing fixture surfaces and puzzle labels retain the current art style.
- Optional shadows have limited ranges: Low 20, Medium 35 and High 55 world units. Low and Medium use hard shadows; High allows soft shadows. The default Off profile avoids the moving boundary of a camera-centered shadow range. The Truth Table night-visibility mask is a separate gameplay effect and remains present.
- Changing only audio or controls reuses the render pipeline. The settings layout avoids rewriting its transforms when the screen and safe area have not changed.
- Accessibility text now updates when text or settings change. Touch controls are discovered once and new joystick/look controls register themselves. The recurring half-second scene scans are gone.
- Older saved settings from the initial implementation migrate once to the cheaper graphics defaults. Audio, control and reading preferences are preserved. Later choices made through Apply are saved normally.

The Main Menu confirmation now uses the compact parchment card, and both graphics dropdowns use native anchored option lists. These UI changes apply to the shared settings used by all ten solo trials.

## What was measured

The Editor sample uses the saved PRELIM scene with 105 text components. Each sample invokes the component update directly; it does not measure a complete rendered game frame.

| Component | Previous recurring scene scan | Updated behavior |
| --- | ---: | --- |
| Accessibility | 1.85 ms per scan | One initial discovery, then text/settings events |
| Touch settings | 2.92 ms per scan | One initial discovery, then registrations |

The old scans could together add about 4.77 ms when they occurred on the same frame. After the change, the unchanged-frame sample for both components combined was approximately 0.0032 ms. Timing varies by hardware and Editor state. See `before-ui.json` and `after-ui.json` for the raw samples.

The saved PRELIM inventory contains 2,265 renderers, of which 292 are enabled on active objects, 106 distinct materials, 542 LOD groups, 28 enabled real-time lights (27 point lights and one directional light), 12 canvases and 105 text components. These counts include authored content for multiple areas and do not represent visible draw calls. Gameplay activation and generators can change them. See `saved-scene-inventory.json`.

## Recommended next work, in order

1. **Measure an installed Android build.** Choose the weakest phone you intend to support and a typical midrange phone. Capture CPU time, GPU time, allocations and memory while moving through the busiest area, rotating the camera, opening settings and completing a stage. Use a Development Build for diagnosis, then check a release build for the final result. At 30 FPS a frame lasts 33.3 ms; aim for around 22 ms of active work to leave room for heat and device variation. Run a separate 15–20 minute play session to check whether performance drops as the device warms. These are proposed acceptance targets, not results already achieved. [Unity profiling guidance](https://unity.com/how-to/best-practices-for-profiling-game-performance)

2. **Bake room lighting before adding more live effects.** Review the 28 saved lights in PRELIM first. Bake stationary walls, floors and decorative lights where the trial's changing lighting permits it, and use light probes for the moving character. Keep genuinely changing lights dynamic. Inspect lightmap memory and UVs; baking still has texture and memory costs. For runtime-generated rooms, build reusable baked modules or bake appropriate shading into their textures instead of assuming the current generated scene can simply be lightmapped. [Unity lighting and graphics guidance](https://unity.com/blog/games/optimize-your-mobile-game-performance-expert-tips-on-graphics-and-assets)

3. **Reduce the work the camera can see.** Inspect actual draw calls in the Frame Debugger. Reuse materials, compare batching or instancing for repeated props, tune the existing LOD groups, and disable distant decorations. Consider occlusion culling for stable room geometry. Evaluate simpler shaders on environment props and keep expensive transparency, water and particles small on screen. Change one category at a time and compare CPU/GPU captures. [Unity URP performance settings](https://docs.unity.com/en-us/engine/6000.3/manual/render-pipelines/universal-render-pipeline/introduction/configure-for-better-performance)

4. **Set texture budgets for Android.** Review large background, outfit and environment textures at their actual on-screen size. Test ASTC on supported devices, with ETC2 support or compression targeting for devices that need it. Check installed memory rather than APK size alone: unsupported formats may decompress into larger textures. Keep UI text and important icons readable when reducing texture sizes. [Unity texture format guidance](https://docs.unity.com/en-us/engine/6000.3/manual/materials-and-shaders/textures/textures-getting-started/texture-compression-formats/texture-choose-format-by-platform)

5. **Profile UI and allocation spikes.** Keep static interface art separate from frequently changing counters where profiling shows canvas rebuilds. Update countdown labels only when their displayed value changes. Pool repeatedly spawned effects and avoid creating lists, materials or searching the scene in per-frame paths. The static shop portraits already avoid running a live 3D camera for every card. Validate these changes against the Profiler before expanding the refactor. [Unity UI optimization](https://unity.com/how-to/unity-ui-optimization-tips)

6. **Keep frame delivery consistent.** Android frame pacing, multithreaded rendering, IL2CPP, ARM64 and incremental garbage collection are already enabled in this project. GPU skinning is currently disabled: benchmark enabling it for the character on the target phones instead of assuming the benefit. Compare graphics APIs on those phones, and lower optional quality if a sustained thermal test cannot hold the frame target. [Android performance guidance](https://developer.android.com/games/optimize)

Start with the device capture and lighting audit. Those measurements should decide whether the next change is rendering, scripts, physics, memory or loading; an Editor lag report alone cannot establish which one dominates on Android.
