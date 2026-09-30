# Logic Garden flower bush

Low-poly flower bush modeled against the supplied front, side and top reference: 18 green stems, 61 main leaves, 16 small bud calyx leaves, seven white five-petal flowers with yellow centers, and eight pink closed buds. Flower and bud heights are staggered around a three-dimensional footprint.

## Deliverables

- `LogicGarden_FlowerBush.fbx` is in `Assets/Art/LogicGardenFlowerBush` in the project, or alongside this README in the downloadable ZIP.
- `LogicGarden_FlowerBush.blend` is the editable source in this folder. No modifiers or subdivision are required.
- `LogicGarden_FlowerBush_Preview.png`, `Front.png`, `Side.png` and `Top.png` (each with the full asset-name prefix) show the exported and re-imported model.
- `geometry-report.json` contains measured counts and export validation.
- The generator is `Tools/create_logic_garden_flower_bush.py` in the project, or alongside this README in the ZIP. Run it from its project location using Blender 4.5 background mode.

## Geometry and hierarchy

1,679 mesh vertices; 1,382 polygon faces: 1,368 quads and 14 pentagons. Rendered triangle equivalent: 2,778. All leaf, petal, stem and bud faces are quads; only flower-center end caps are pentagons. The leaf surfaces use two folded quads per side, with a thin closed edge. Petals also use two quads per side. No triangle planes, dense spheres, alpha cards, textures or subdivision are used.

The root `LogicGarden_FlowerBush` has five child meshes: `Stems`, `Leaves`, `Petals`, `Centers`, and `Buds`. Each has one simple replaceable material. The five meshes combine repeated components to avoid hundreds of renderer objects. Named vertex groups in the Blender source allow individual stems, leaves and petals to be selected and edited. Components intentionally overlap at their connections.

Units are metres. The root has a ground-level pivot and unit scale; the FBX is Y-up. Blender uses Z-up. The studio ground, camera and lights are excluded from both asset files. UVs are basic overlapping projections for placeholder/replacement materials, not unique lightmap UVs.

## Validation and Unity use

The FBX was re-imported into Blender and checked for the five-child hierarchy, retained quad and triangle counts, unit object scales and absence of modifiers. Mesh checks found zero boundary edges, nonmanifold edges or zero-area faces. Front, side, top and perspective renders were visually inspected.

Drag the FBX from Unity's Project window into a scene and save it as a prefab if desired. All geometry is included; no reconstruction is required. The asset is intended for repeated mobile-game placement. It has five renderer/material groups for convenient recoloring, so it is not a single-draw-call asset. Share materials across copies. Actual performance depends on copy count, shaders and shadows; no Unity device benchmark was performed. Unity may split source vertices for flat normals and UV attributes, and triangulates polygons for rendering.

For editing, open the `.blend` or use Blender File > Import > FBX. Select a child mesh, enter Edit Mode, and select linked geometry or a named vertex group. The native Blender source is stored outside Unity's Assets folder to avoid an extra automatic model import.
