# Truth Table column holograms

Scene: `Assets/Scenes/TruthTable.unity`.

The existing P_Barrier, Q_Barrier and PQ_Barrier objects remain the puzzle's masking barriers. Their BoxColliders, transforms and snap references are unchanged. Physical locking is separate from answer feedback. `DynamicLogicPuzzle` owns visibility and the phase reports a confirmed incorrect submission; validation and penalties are unchanged.

The veil and energy materials are separate clones of the multiplayer CrimsonBarrier materials. They reuse its URP shaders and static Noto Sans Math SDF atlas; multiplayer assets are not edited. Unanswered/future columns show no red hologram. Red appears only for a submitted incorrect column, then clears on the next placement or after the feedback duration. Green marks a submitted correct column. All feedback hides at stage completion.

Each column has one pointed five-sided veil, a soft two-width outline and three baked symbols. All of ∧, ∨, ¬, →, ↔, T and F appear across the three columns. Symbols face the front reading direction, pulse through the multiplayer shader, and drift by less than 0.09 metres. The veil's base opacity is 0.045 from the front and 0.03 from behind. No bloom or extra lights are required.

`TruthTableColumnHologram` only updates renderer property blocks and a small visual-child offset. It never reads input, changes physics, edits questions or triggers validation. Cleanup does not modify the scene outside Play Mode.

Mobile budget: 35 submitted triangles and three renderers per column, up to three feedback columns (105 triangles / nine renderers), with no particles, lights, runtime text generation or per-frame mesh/material creation. Physical-device profiling has not been performed.

Verification: `Library/TruthTableHologramChecks.txt` records all nine columns, normal placement/locking, original Submit callbacks, exact existing −20s wrong-column penalty, red/green transitions, completion cleanup, animation and transparency comparisons at 1920×1080, 2960×1440 and 800×480. Saved-scene collider snapshots were preserved by the setup helper. The run used an offline account/reward-suppressed Editor session.

Known pre-existing UI limitation: Gameplay_Interface/LookArea intercepted Submit taps; excluding those graphics still did not expose a raycast target in the tested board state. The visual regression therefore invokes the unchanged Submit button callback. No shipped UI/input behavior is changed by this art update.

Safety copy: `Library/TruthHologramBefore.unity`. Authoring helper: `Assets/Editor/TruthTableHologramSetup.cs`; it refuses duplicate installation. Runtime artwork/animation: `Assets/Scripts/Scripts/PRELIM/Truth_Table_Script/TruthTableColumnHologram.cs`.
