# Logic Garden flowering ivy

Modular, reference-guided ivy with an upper leafy cluster, horizontal branches and five uneven hanging trails. The assembled asset contains 120 leaves in three shapes, 11 open/side flowers, seven pale pink buds and nine main vine branches, with short connecting flower/bud stems.

## Deliverables

- `Assets/Art/LogicGardenFloweringIvy/LogicGarden_FloweringIvy.fbx`: complete assembled game asset.
- `Assets/Art/LogicGardenFloweringIvy/LogicGarden_Ivy_ModularKit.fbx`: eight separate named construction pieces laid out in a grid.
- `SourceAssets/LogicGardenFloweringIvy/LogicGarden_FloweringIvy.blend`: editable assembly, with per-element vertex groups.
- `SourceAssets/LogicGardenFloweringIvy/LogicGarden_Ivy_ModularKit.blend`: editable modular library.
- Perspective, front, side and top previews, plus `geometry-report.json`.
- Generator: `Tools/create_logic_garden_flowering_ivy.py`, run from its project location using Blender 4.5 background mode.

The ZIP contains all of these files together. Preview stone support, floor, cameras and lights are NOT included in either asset or kit. They only illustrate placement.

## Geometry and scale

Assembly: 2,476 source vertices; 1,054 polygon faces, all quads; 2,108 rendered triangles. One combined mesh with four simple placeholder material slots: green foliage/stems, cream petals, yellow centers and pink buds. No subdivision, modifiers, textures, dense hidden core or alpha cards.

Leaves A/B use two/one quads on each side respectively. The ivy leaf uses four quads on each side to provide an angular lobed outline. Petal faces are also quads. Reverse surface copies are slightly offset and carry independent normals, allowing ordinary opaque backface-culling materials. Leaves and petals intentionally have open boundary edges; no unnecessary edge walls are modeled. Stems have square cross-sections; buds are simple capped quad forms.

Unity dimensions: X 1.2, Y 0.8, Z 0.6 metres. In Blender the vertical axis is Z. Root origin is near the upper central attachment point, useful for placing against a structure. Geometry scale is baked and object scales are 1. FBX is Y-up. As with other rigid modular plants, move/rotate/scale copies to fit a wall, pillar or rock; this asset does not automatically conform to a surface.

## Modular kit

Eight named objects: `Leaf_A`, `Leaf_B`, `Leaf_C_Ivy`, `Flower_Open`, `Flower_Side`, `Flower_Bud`, `Vine_Segment_A`, `Vine_Segment_B`. Pieces use approximate requested module sizes, with local roots at their attachment points. The shorter stem includes a fork. Grid positions in the library separate pieces for editing. Select and duplicate a module to extend the vine, or edit named vertex groups in the assembled source. The assembly consolidates copies into one renderer instead of keeping hundreds of scene objects.

## Validation and Unity use

Both FBX files were re-imported into Blender. The assembly preserves face and triangle counts, quads, unit scale and a single complete mesh; it has zero loose vertices or zero-area faces. The kit preserves all eight named meshes, quad topology, unit scale and absence of modifiers. Front, side, top and perspective previews were visually inspected.

Drag the assembled FBX into Unity and replace its materials. It is intended for repeated mobile-game placement; share its mesh and materials between copies. Four material slots mean multiple draw calls per instance. No mobile-device performance benchmark was performed; copy count, shadows and shaders still affect frame rate. Unity triangulates quads and may split source vertices for normals/UV attributes. UVs are basic overlapping projections, not a unique lightmap or detailed texture unwrap.

Native Blender files live outside Assets to avoid duplicate automatic Unity model imports.
