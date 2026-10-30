# TruthTable sunset

Only `Assets/Scenes/TruthTable.unity` uses this setup. It reuses the existing SkySeries MegaSun cubemap through a dedicated material copy, with a warm tint, a low 14-degree sun and readable ambient fill. Original sky and lamp prefabs are unchanged.

Adjust the `TruthTableDuskLighting` component on `Truth_Table` for sunlight, ambient colors, sky exposure/tint, lamp brightness, emission and flicker. All 25 lantern lights remain at their existing fixture positions and ranges. Lamp intensity is 22, emission 1.2; six fixtures retain their gentle 88–100% irregular flicker and the other 19 remain steady.

The former nighttime visibility component is removed from the scene. Normal camera rendering presents the entire view directly, without a cone, mask, blur, player-facing fill light or extra presentation camera. UI, formulas, blocks, indicators and gameplay are unchanged.

Setup: `Assets/Editor/TruthTableSunsetSetup.cs`. Safety copy: `Library/TruthSunsetBefore.unity`. Offline Play Mode verification: `StageHelpGameplayRecorder.CheckTruthSunset()`, report `Library/TruthTableSunsetChecks.txt`.
