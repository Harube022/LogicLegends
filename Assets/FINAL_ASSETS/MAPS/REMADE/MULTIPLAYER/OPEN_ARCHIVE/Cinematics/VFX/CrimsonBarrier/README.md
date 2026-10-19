# Crimson Discrete Mathematics barrier VFX

Reusable asset: **Crimson_Mathematical_Prison.prefab**. The prefab contains the transparent crimson cylinder, rotating upper/lower energy rings, mathematical wall glyphs and diagrams, concentric ground seal, rising streaks, sparks and glowing fragments.

Select its `CrimsonMathBarrierVfx` component to edit the shared **Crimson_Barrier_Style** inline. Exposed settings include all four requested hexadecimal colors, glow intensity, radius, height, animation speed, wall opacity, inside opacity, formation/dissolve durations and rune rotation speed. Inspector buttons preview the formed barrier, rising energy and breaking runes. Inspector style changes affect all instances sharing the same style asset.

## Rendering and notation

The existing `Assets/TextMesh Pro/Fonts/NotoSansMath-Regular.ttf` supplies a dedicated static 1024×1024 SDF atlas. Every requested symbol is present: **∀ ∃ ∈ ∉ ∪ ∩ ⊆ ∧ ∨ ¬ → ↔ ∑**. Baked meshes use its glyph UVs, avoiding runtime text layout, atlas growth or fallback-font requests. Separate inside/outside glyph faces preserve readable notation from either side. Interior faces are hidden from outside cameras so opposing formulas do not overlap the player.

The slowly rotating wall panels contain overlapping Venn sets, a graph with five nodes and seven edges, the four-row conjunction truth table, `A={1,2,3}`, `A ⊆ B`, `∀x∈A` and a three-element function mapping. Ground runes rotate independently around the central Venn seal. Energy rises during formation. Dissolution separates glyphs and line fragments, emits a short shard burst and fades the complete effect before destroying the instance.

Two dedicated URP unlit shaders use transparent blending and no depth writes. The inside wall opacity defaults to 0.018, outside opacity to 0.055. No full-screen effect, post-processing volume, runtime light, collider or UI canvas is added. The prefab has 11 renderers and at most **24 sparks + 8 fragments**. Baked triangle and shader validation counts are recorded in `Testing/CrimsonBarrier/Reports/`. Actual Android GPU performance remains unverified.

## Existing encounter integration

`KhezuBarrierVfxPresentation` is attached to the existing Khezu in **OpenArchive_KhezuCinematic_Test.unity**. It only reads `KhezuCaptureEncounterController`, the confirmed capture actor/serial and the existing hunting snapshot. It creates one visual after the two-second cinematic has completed and the encounter confirms `BarrierChallenge`. Late checkpoints show the already formed visual. Reset/release dissolves it; disconnect and component disposal clean it up. Other peers can render the prison around the corresponding network avatar without receiving a challenge UI from this component.

The visual prefab owns no movement lock, challenge outcome, health, target selection or network validation. `BeginFormation(elapsed)` and `Dissolve(elapsed)` are cosmetic APIs; neither releases a player or resolves a challenge. The presentation now seeks the existing dissolve using the host's accepted-outcome timestamp.

The subsequent **Phase 3B bug fix** connects persistent player movement tokens, development SUCCESS/FAILED buttons, authoritative outcomes, the existing HealthManager and return-to-nest states through the existing encounter and hunting controllers. See `Testing/KhezuBarrier/README.md` for setup, exact source changes and verification. The original handoff panel is replaced in the saved cinematic test scene. Movement remains locked from capture until the host-confirmed dissolve completes.

## Authoring and verification

`Tools → Logic Legends → Crimson Barrier → Build Reusable VFX Assets` updates the owned baked meshes, glyph atlas references, materials and prefab. It does not rebuild the environment or change gameplay. `CrimsonMathBarrierReview.Verify()` validates palette, glyph coverage, geometry/particle budgets and shader compilation. `RenderPreviews()` renders temporary views using the original project player model and removes the staging objects afterward.

`LogicLegends.Tests.Khezu.CrimsonBarrierPlayChecks.Begin()` runs explicitly in offline Editor Play Mode. It exercises the real two-second cinematic and existing encounter with snapshot fixtures, checks delayed creation, rise/rotation, live particles, actor/serial deduplication, fade/disposal, late checkpoints and disconnect cleanup. Its test component is not saved in scenes and is excluded from player builds. Reports and outside/inside/formation/destruction/seal previews are in `OPEN_ARCHIVE/Testing/CrimsonBarrier/Reports/`.

The historical VFX source-protection manifest records the 268 gameplay scripts that were unchanged by the visual update. Current movement/outcome verification belongs to `Testing/KhezuBarrier/Reports/`; VFX checks alone do not certify multiplayer gameplay or native Android performance.
