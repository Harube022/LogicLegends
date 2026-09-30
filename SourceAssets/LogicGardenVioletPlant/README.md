# Logic Garden violet flowering plant

Reference-guided plant with five staggered upright violet flower spikes, five open five-petal blossoms with small yellow centers, five pointed pink-violet tip buds, 45 purple spike bracts and 28 green leaves. Broad leaves spread from a central base, with smaller leaves along the stems.

## Deliverables

- `Assets/Art/LogicGardenVioletPlant/LogicGarden_VioletPlant.fbx`: assembled Unity asset.
- `Assets/Art/LogicGardenVioletPlant/LogicGarden_Violet_ModularKit.fbx`: five reusable, individually named components.
- `SourceAssets/LogicGardenVioletPlant/LogicGarden_VioletPlant.blend`: editable assembled source with per-element vertex groups.
- `SourceAssets/LogicGardenVioletPlant/LogicGarden_Violet_ModularKit.blend`: editable component library, arranged in a grid.
- Perspective, front, side and top previews plus `geometry-report.json`.

The ZIP includes both FBXs and Blender files, all previews, the report and generation scripts. The generator at `Tools/create_logic_garden_violet_plant.py` reads pure geometry helper definitions from `Tools/create_logic_garden_flowering_ivy.py` without running its ivy creation workflow. Run the violet generator from its project location using Blender 4.5 background mode.

## Geometry

1,620 source vertices; 740 quad faces; 1,480 rendered triangles. All source polygons are quads, including leaves, petals, stems and buds. No subdivision or modifiers. One assembled mesh and four plain placeholder material slots: green foliage, violet petals, yellow centers and pink-violet bud tips. No textures, alpha cards, hidden core or studio objects are included in the exported plant.

Leaf and petal surfaces use simple shaped quads. Slightly offset reverse faces with independent normals make them visible from either side with ordinary opaque backface-culling materials. These are intentionally open surfaces, with no unnecessary edge walls. Stems use square cross-sections. UVs are basic overlapping projections rather than a unique lightmap or detailed texture unwrap.

## Components and dimensions

`Open_Flower`, `Flower_Stalk`, `Flower_Bud`, `Large_Leaf`, `Small_Leaf` are provided as separate kit meshes. The flower-bud component includes its short stalk and side buds; the assembled plant uses the same closed tip-bud form on its flower spikes. Module dimensions approximate the reference and are scaled/rotated during assembly. Kit objects have unit scale and intentional grid-placement offsets.

The assembled plant measures 0.8 x 0.7 x 0.8 metres in Unity XYZ. Blender uses Z-up. The FBX is Y-up, has unit object scale, and its origin is at the bottom center for ground placement. All final size adjustments are baked into the vertices.

## Validation and use

Both FBXs were re-imported into Blender. The assembly preserves its quad and triangle counts, unit scale and single mesh; it has no zero-area faces or loose vertices. The kit retains all five named meshes and quad faces. Perspective/front/side/top previews show the re-imported assembly. The preview floor, camera and lights are excluded from the model files.

Drag the assembled FBX into Unity and replace its material slots as desired. To edit, open the native Blender source or import the FBX. Named vertex groups let you select individual components within the combined mesh; the separate kit supports new arrangements. The native sources are outside Assets to avoid duplicate Unity model imports.

Designed for repeated mobile-game placement; no device benchmark was performed. Four material slots require multiple draw calls. Share meshes and materials across copies, and account for shadow and shader costs. Unity triangulates source quads and may split source vertices for imported attributes.
