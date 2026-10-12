# Logic Garden Terrain Border

Standalone terrain-only asset modeled from the supplied square grassy plateau
reference. It includes the level grass placement pad, faceted rock cliff,
secondary stones, a front earthen approach, grass-and-soil apron, moss, leafy
fern clumps, hanging ivy, tiny wildflowers, grass tufts, and scattered pebbles.
It contains **no existing arena walls, tree, fountain, topiary, or gameplay
props**. Those stay in the user's separate Unity scene.

## Files

- `Assets/Art/LogicGardenTerrainBorder/Logic_Garden_Terrain_Border.fbx` —
  Unity-ready metre-scale, Y-up FBX.
- `Assets/Art/LogicGardenTerrainBorder/LogicGarden_TerrainSurface.png` —
  compact painted grass/soil texture.
- `Assets/Art/LogicGardenTerrainBorder/LogicGarden_TerrainPalette.png` —
  shared rock, plant and flower palette. Both textures are embedded in the FBX.
- `SourceAssets/LogicGardenTerrainBorder/Logic_Garden_Terrain_Border.blend` —
  editable Blender source with separate named parts.
- `SourceAssets/LogicGardenTerrainBorder/Logic_Garden_Terrain_Border_Preview.png`
  and `Logic_Garden_Terrain_Border_Top.png` — renders of the reimported FBX.
- `SourceAssets/LogicGardenTerrainBorder/geometry-report.json` — topology and
  FBX round-trip validation.
- `Tools/create_logic_garden_terrain_border.py` — Blender 4.5 build script.

## Placement and geometry

The object origin is centered at ground level. The completely level arena pad
measures 12 × 9.4 m at Unity Y = 0.92 m, with an `Arena_Placement_FlatCenter`
empty at its center. The entire terrain footprint is approximately 18.3 ×
14.6 m. The front approach is on negative Blender Y, which exports as Unity
negative Z. Adjust the overall scale to the existing arena's footprint before
placing it in the scene; the source scene was not modified.

The FBX has **35,758 vertices, 13,614 faces, and 28,692 triangles**, in 12
named mesh groups with two shared materials and no modifiers. It was reimported
into Blender and checked for matching triangle count, zero loose vertices, and
zero zero-area faces. Preview lights and camera are not part of the FBX.

This is a practical 3D reconstruction of one perspective picture. The image
cannot define hidden surfaces or every painted brush stroke, so the mesh and
textures reproduce its key shapes and visual details rather than pixel-match it.
