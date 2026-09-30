# Logic Garden quad-leaf bush

Reference-guided rounded bush with 180 overlapping leaves, staggered around nine layers, overlapping crown leaves and a few upright shoots. This is a new asset; earlier bush and flower-bush files remain separate.

## Files

- Unity FBX: `Assets/Art/LogicGardenQuadBush/LogicGarden_QuadBush.fbx` in the project, or `LogicGarden_QuadBush.fbx` in the ZIP.
- Editable Blender source: `SourceAssets/LogicGardenQuadBush/LogicGarden_QuadBush.blend`.
- Perspective and top previews are in this source folder and in the ZIP.
- `geometry-report.json` contains measured counts and FBX validation.
- Generator: `Tools/create_logic_garden_quad_bush.py`. Run from its project location using Blender 4.5 background mode.

## Geometry

- 2,160 source mesh vertices.
- 720 faces, all quads; 1,440 rendered triangles including leaf backs.
- Each leaf front uses just two shaped quads with a pointed six-vertex outline and shallow central fold. Two reverse quads supply the back, offset inward 0.8 mm to avoid coincident-surface artifacts. Back vertices are separate so their opposite normals remain independent.
- One mesh, one replaceable opaque placeholder material; no texture dependency.
- No inner sphere, stems, subdivision, modifiers or leaf edge walls.
- Approximately 1.64 x 1.68 x 1.46 metres in Blender XYZ, with ground-level origin and unit scale. FBX is Y-up for Unity.

The leaves are intentionally open surfaces rather than watertight solids. Their boundary edges are expected, not broken topology. Overlapping leaf layers create volume without a hidden core. Standard opaque backface-culling materials can be used; the reverse geometry supplies the visible leaf undersides. No two-sided shader is required.

## Editing and Unity use

Open the `.blend`, or import the FBX into Blender. Named leaf vertex groups select each leaf together with its back. UVs overlap per leaf and are intended for a reusable leaf texture, not a unique lightmap. Blender source is outside Assets so Unity does not import an extra model.

Drag the FBX into a Unity scene and replace its placeholder material as desired. Shared mesh/material copies are suitable for repeated placement. Actual mobile performance depends on visible copy count, shader, shadows and device; no Unity device benchmark was performed. Unity may split vertices for imported mesh attributes and will triangulate quads for rendering.

The exported FBX was re-imported into Blender. Checks confirmed one complete mesh, one material, all 720 quads preserved, unit scale, no modifiers, no loose vertices and no zero-area faces. Perspective and top renders were visually inspected. Preview floor, camera and lights are excluded from both model files.
