# Shader investigation — 2026-11-11

Scope: the ten solo trials. Shader fixes deferred at the user's request. No player/asset-bundle build or lighting bake was run.

## Findings supported by source and saved-scene inspection

1. Sets: `LogicLegends/OpenArchive/TreeTimber` is assigned to 168 renderers (10 unique meshes). Its forward pass samples the base palette and up to three projected canopy/bark textures, computes derivatives and relief normals, then evaluates additional lights per pixel. There is no mobile or Low-quality shader branch. `SoloTrialVisuals` does not reduce those material features when Graphics Low is selected. Both source and catalog Low meshes retain the required TEXCOORD2 data; missing detail UV data was not found in this inspection.
2. `LogicLegends/OpenArchive/DetailedSurface` occupies 42 material slots in Sets and 51 in Rules of Inference. It samples an atlas, macro variation and the base map, optionally more stone/grass/emission textures, and computes procedural details before per-pixel lighting. There is no Low-quality or mobile shading branch. These are material-slot counts, not measured draw calls or GPU time.
3. Truth Table and Rules of Inference have 1,756 and 1,626 enabled saved foliage renderers using the legacy vegetation material. The existing trial runtime converts these to `LogicLegends/Solo/FoliageCutout`, applies density/distance limits and uses LODs. That shader draws both sides, samples/clips alpha, animates wind and evaluates additional lights per surviving pixel. Overlapping grass can repeat that work. Saved renderer totals include LOD levels, and are not the number drawn in a gameplay frame.
4. The two custom ground shaders already have a mobile branch that avoids several desktop texture samples. They also retain per-pixel additional-light evaluation. Simplifying these alone may not address the heavier foliage and TreeTimber work.

## What was verified

- Low lighting rendered four independent light pools on one large mesh in the Editor using URP Lit, Simple Lit, Truth Table ground and Inference ground shaders.
- All ten saved scenes had enabled realtime fixtures selectable nearby under the corrected four-light Low budget. These are individual-nearby-fixture checks, not a claim that every scene light is simultaneously enabled.
- The active light cap now matches the Forward per-object cap: 4/6/8. Slot changes fade without exceeding that cap. Stationary-player light selection remained stable through full camera rotation in the regression fixture.
- The Editor frame-timing query reported GPU frame time 0 ms while outside Play mode. That is not usable GPU performance evidence and was not treated as an actual rendering cost.

## What is not established

No working Android GPU capture or before/after Android frame-time measurement was collected. The shader bottleneck ranking, rainbow-artifact cause and absence of Android lag remain unverified. A source review identifies work and candidates, not the exact dominant device bottleneck.

## Next shader work, deferred

- Capture the same gameplay view on the Android device and compare temporary material overrides one shader family at a time. Retain geometry, resolution, lights and camera pose so the comparison separates shader cost from mesh/draw overhead.
- Prioritize TreeTimber in Sets: test a mobile/Low variant using a simpler projection and no derivative relief while keeping the current palette and light response.
- Test DetailedSurface without distant procedural detail, using shader variants that actually skip texture reads rather than only setting blend strengths to zero.
- Test grass overdraw and per-pixel lighting cost separately from density and draw-call cost. A foliage-specific simpler lighting path should retain visible local torch lighting.
- Restrict runtime material/variant choices to the ten trial scenes; do not globally replace shared multiplayer artwork.

Inventory: `ShaderInventory.json`. Lighting regression results: `TrialLightingChecks.txt` in this directory.
