# Element Spawn Pedestal

Low-poly stone pedestal matching the Sets Arena and blue element cubes: stepped oval foundation, recessed body, blue light band, brass trim, three docking sockets and small decorative inlays. This delivery is the static pedestal model with spawn-point transforms.

## Files

- `Element_Spawn_Pedestal.fbx`: complete pedestal, combined mesh, root and three spawn anchors.
- `Element_Spawn_Pedestal.blend`: editable source with 20 named mesh components and packed palette.
- `SpawnPedestal_Palette.png`: 64 x 64 palette, also embedded in the FBX.
- Empty, top and occupied preview images; geometry-report.json; this guide.
- Project generator: `Tools/create_element_spawn_pedestal.py`. Model generation is self-contained; the optional occupied preview loads existing element FBXs from this project.

The occupied preview uses the previously created cubes 1, 2 and 3 to demonstrate fit. Those cubes remain separate assets and are not duplicated into the pedestal FBX or Blender source.

## Dimensions and geometry

Approximately 2.32 wide x 0.54 high x 1.20 deep Unity units. The root is centered at ground level. Exports use metre scale, Y-up and unit object scales.

1,102 mesh vertices, 650 polygon faces, 1,576 triangles. The FBX has one mesh and three material slots. No subdivision, unapplied modifiers or animation. Thin decorative trim and surface rings intentionally use open faces.

## Placement and spawn anchors

Place the pedestal root at the arena's `Anchor_ElementPedestal` and rotate it 90 degrees around Unity Y so its long axis follows the side aisle. Keep scale at one. The occupied width across that aisle is then 1.20 units, leaving the central Venn area clear.

Three transforms are included: `Spawn_Point_01`, `Spawn_Point_02` and `Spawn_Point_03`. Their local Unity positions are (-0.70, 0.542, 0), (0, 0.542, 0) and (0.70, 0.542, 0). Instantiate the existing element cubes at these transforms; their ground-level origins retain a slight hovering gap over the sockets. Orient the labels toward the player or camera separately as required.

The model does not implement spawning, pickup or challenge logic. Add those scripts and appropriate Unity colliders in the project.

## Materials

`Pedestal_Stone`, `Pedestal_Brass` and `Pedestal_Glow` share the small included palette. The Blender source has emission on the glow material. Configure an appropriate emission shader and bloom in Unity for the preview halo. FBX does not supply Unity render-pipeline or post-processing settings. Preview lights, cameras and floor are excluded from the model files.

## Validation

The final FBX was independently re-imported in Blender and checked against source geometry counts, unit scales, no modifiers and all three spawn anchors. No loose vertices or zero-area faces were found. The preview images render the re-imported FBX. Runtime mobile-device performance has not been benchmarked.
