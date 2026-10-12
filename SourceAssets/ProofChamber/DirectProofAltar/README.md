# Direct Proof Altar

Low-poly altar based on the blue **Direct Proof** station in `DirectAndIndirect.png`.
The asset includes a stone plinth and worktop, five proof-step bays, front numbers
1–5, blue trim, a central emblem, and three small crystal clusters. The challenge
information board, statement tablets, and book statue are separate game assets.

## Files

- `Assets/Art/ProofChamber/DirectProofAltar/Direct_Proof_Altar.fbx` — Unity-ready Y-up FBX.
- `Assets/Art/ProofChamber/DirectProofAltar/DirectProof_Palette.png` — simple shared palette; also embedded in the FBX.
- `SourceAssets/ProofChamber/DirectProofAltar/Direct_Proof_Altar.blend` — editable source with separate, clearly named parts.
- `SourceAssets/ProofChamber/DirectProofAltar/Direct_Proof_Altar_Preview.png` and `Direct_Proof_Altar_Top.png` — rendered previews.
- `SourceAssets/ProofChamber/DirectProofAltar/geometry-report.json` — measured topology and FBX round-trip result.
- `Tools/create_direct_proof_altar.py` — Blender 4.5 generation script.

## Game setup

The FBX contains one combined altar mesh under the `Direct_Proof_Altar` root,
plus empty `Proof_Step_01` through `Proof_Step_05` child anchors for tablet
placement. Each anchor is centered over its proof bay at local Unity Y 1.165;
their X positions are -1.30, -0.65, 0, 0.65, and 1.30. Pivot is at the floor,
centered in the altar. The blue details use a placeholder emissive material;
adjust its glow/bloom in Unity as desired.

The measured FBX contains **1,363 vertices, 1,361 faces, and 2,288 triangles**
with three shared materials and no modifiers. The FBX was reimported into
Blender and checked for matching geometry, zero loose vertices, and zero
zero-area faces. The source is component-based for editing, while the FBX uses
one mesh to reduce renderer count when duplicated.
