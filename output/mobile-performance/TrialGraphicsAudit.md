# Solo trial graphics baseline

## Pre-reset audit

Active counts include components on active scene objects. Local light totals include disabled fixtures and fixtures on inactive objects. Shadow counts refer to active lights with shadows enabled. The audit inspected all ten saved scenes before editing them.

| Trial | Lights and shadows | Shaders, effects, and overrides |
| --- | --- | --- |
| 1 · Propositional Logic | 1 sun, 27 point lights, 2 shadowed points | URP Lit and TMP, legacy foliage and additive/alpha particle materials. 80 automatic particle systems, including gameplay markers. Chamber script wrote ambient and sun settings around camera rendering. |
| 2 · Truth Table | 1 shadowed sun, 25 point lights | URP and TMP with custom grass/path and hologram shaders, legacy foliage and particles. Three cloud emitters and three state-driven holograms. Dusk script adjusted ambient light, sky and lamps. |
| 3 · Rules of Inference | 1 shadowed sun, 7 spots, 9 points | Custom grass/path and stone shaders, legacy foliage, URP Lit and TMP. One triggered success emitter. |
| 4 · Formal vs. Informal Proofs | 1 shadowed sun, 14 of 53 points active, 2 spots, 1 shadowed point | Stock URP and TMP. No scene volume or particle system. Authored accents also subject to the shared light budget. |
| 5 · Direct & Indirect Proofs | 1 shadowed sun, 12 of 50 points active, 4 spots | Stock URP and TMP. No volume or particles. Flicker script and shared light budget both acted on local fixtures. |
| 6 · Mathematical Induction | 1 shadowed sun, 12 of 53 points active, 4 spots | Stock URP and TMP. No volume or particles. Flicker script and shared light budget both acted on local fixtures. |
| 7 · Sets | 1 shadowed sun, 10 spots | URP and TMP with custom stone and timber shaders. No volume or particles. |
| 8 · Functions and Relations | 1 shadowed sun, 8 spots, 2 points | Stock URP and TMP with legacy particles for clouds and book motes. Fog and graphics-dependent scenery and light effects. No volume. |
| 9 · Graphs | 1 shadow-free sun, 11 of 17 points active, 8 spots | URP and TMP with legacy particle materials on eight triggered answer markers. Active crystal Bloom volume and runtime pulse and flicker. |
| 10 · Trees | 1 shadow-free sun, 7 of 23 points active | Stock URP and TMP. No volume or particles. |

There were no exact duplicate active lights by type, position, orientation, mask and color, or same-name particle emitters at the same location. Fixtures in different locations were counted separately. No saved camera post-processing flag was enabled. Graphs could enable it at runtime. No trial had a separate baked-lighting data asset assigned in the audit.

The project’s default Android quality level was Medium. The common URP asset uses Forward+, a render scale of 1 and 2× MSAA. The solo graphics runtime previously created pipeline copies and applied saved presets to render scale, local lights, shadows, LOD and other settings. Additional shared and scene-specific components managed lights, visual effects, materials, foliage and cameras. Several components wrote graphics settings for the same scenes. The audit found multiple writers and did not establish duplicate persistent managers.

Local lights, shadows, overdraw, multi-texture grass and repeated scene scans are plausible Android costs. The audit did not measure their individual cost or establish the cause of the reported flickering.

## Applied reset

- Kept one shadow-free directional light and flat ambient fill in each trial. Disabled all 302 local light entries in the trial scenes, including inactive entries. Turned off fog and ambient reflections. Kept existing skyboxes.
- Disabled the Graphs Bloom volume, scene lighting and effects controllers, and six decorative particle emitters. Kept puzzle and door markers, and retained the Truth Table red and green feedback with a simpler translucent material.
- Assigned 16 scene-specific stock URP material copies where custom or legacy shaders needed replacing. Preserved the original materials and shaders. The Truth Table and Rules of Inference ground now uses a simplified grass surface, so the prior shader-blended dirt paths and detail are absent from the baseline.
- Disconnected solo runtime graphics application and temporarily made Graphics Options selections apply no graphics changes. The menu and controls still appear and accept selections. Audio, controls and accessibility settings retain their existing Apply path.
- Kept shared project render-pipeline assets, existing Android build-time optimizations, scene objects, camera components, transforms, colliders and gameplay references. Compared all ten saved scenes against pre-reset copies and checked the changed serialized fields.

## Verification

All ten trials passed an offline Unity Editor Play Mode smoke test. The first opened directly and the other nine loaded through the existing solo loader. Each exited Help and activated its starting Book Statue via the hand control. Each loaded with one shadow-free light, fog off, no active Volume and no runtime solo graphics override. In the first trial, selecting each of the six Graphics Options and tapping Apply left saved graphics settings and the renderer state unchanged.

This was a startup and interaction test. It did not solve every puzzle. The Editor log recorded teardown errors in the PRELIM completion notice and Trees board cleanup during scene exit. Those gameplay paths were outside this reset and were left for separate investigation.

ADB listed no connected Android device. Android frame time, flickering, and any before-and-after performance change are unverified. The reset removes several costs but also removes the former Low preset’s runtime render-scale and foliage reductions, so a performance gain cannot be assumed.

[Raw pre-reset audit](TrialGraphicsBefore.json) · [Saved reset log](TrialGraphicsReset.txt) · [Editor Play Mode results](TrialGraphicsPlayChecks.txt)
