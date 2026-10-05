# Sets gameplay models

Three separate static model assets matching the previously created Sets Room/Arena. The Venn diagram uses blue A-only tiles, red B-only tiles and a purple lens-shaped intersection. Circle outlines blend into purple around the overlap. The separate universal-set frame has a blue light strip, U label and small brass corner details. The element is one reusable blue cube with luminous edges and a removable floating example number.

## Deliverables

- `Assets/Art/SetsGameplay/Venn_Diagram_Floor.fbx`
- `Assets/Art/SetsGameplay/Universal_Set_Boundary.fbx`
- `Assets/Art/SetsGameplay/Element_Object.fbx`
- `Assets/Art/SetsGameplay/SetsGameplay_Palette.png`
- `SourceAssets/SetsGameplay/Sets_Gameplay_Assets.blend` — all three assets, editable with packed palette.
- Three preview PNGs, geometry report and this guide.
- `Tools/create_sets_gameplay_assets.py` — reproducible Blender 4.5 generator. Run from the project location; it overwrites the generated files.

## Placement in the arena

Use the existing room's `Anchor_VennFloor` for both the Venn floor and universal boundary: parent each imported asset root there and reset local position and rotation to zero, with scale one. Both exports share the same floor-center origin. The arena anchor already lifts the overlays above its floor, avoiding z-fighting. The boundaries and colored regions are shallow surface overlays, not additional raised platforms.

The circles each have a radius of 2.35 units, with centers 1.55 units left and right of their shared origin. Overall circle footprint is approximately 7.8 x 4.7 units. The U frame is 9.6 x 7.0 units and fits inside the room's reserved 10 x 7.5 area. Space inside U but outside both circles remains unobstructed.

The element cube is approximately 0.505 units wide, including corner caps. Its origin is beneath the cube. A small gap gives it a hovering appearance without animation. The source layout places it in the outside-A/B region for inspection; its individual FBX exports at the origin.

## Reusable element labels

Duplicate `Element_Object.fbx` for each element. It contains one reusable body, luminous edges, corner caps, an `Element_LabelAnchor` and a separate `Element_Label_Example_1` mesh.

For a changing number or letter in Unity, disable the example mesh and attach a TextMeshPro/world-space label at `Element_LabelAnchor`. Update that label's text for each instance, and face it toward the gameplay camera if needed. The example is a static front-facing mesh; it does not billboard or change automatically. The body does not need to be remodeled for different values. The Blender labels are mesh geometry, so they do not require an external font to import.

## Geometry

| Asset | Source vertices | Triangles | Mesh objects |
|---|---:|---:|---:|
| Venn Diagram Floor | 3,694 | 1,998 | 12 |
| Universal Set Boundary | 72 | 62 | 5 |
| Element Object, including example 1 | 195 | 293 | 4 |

Two shared materials and one 64 x 64 palette cover the set. No subdivision, remaining modifiers or animation. The cube chamfers are modeled geometry. Region surfaces do not overlap each other; they use opaque colored tiles rather than stacked transparent disks. Flat floor overlays intentionally have open, upward-facing boundaries. Fine gaps in the tiled regions expose the arena floor beneath.

## Materials and Unity setup

`Sets_Surface` contains the muted floor colors and cube body. `Sets_Glow` is used for outlines and labels. Both sample the included palette, embedded in the FBXs and packed in the Blender source. UVs select palette swatches; they are not unique lightmap UVs.

The Blender source has emission on `Sets_Glow`. FBX does not deliver a Unity shader or post-processing setup. Assign an appropriate Unity material, use the palette as its base and emission map, and enable bloom to obtain the preview halo. Studio lights, preview paving, cameras and glow compositing are excluded from all model files.

These are visual assets. Pickup/placement behavior, colliders, triggers, membership detection and challenge scripts are not included. The region meshes are separately named `Region_A_Only`, `Region_Intersection` and `Region_B_Only` for later highlighting. For circular membership testing in local horizontal coordinates, use squared distance from centers (-1.55, 0) and (1.55, 0) against radius squared 5.5225. Test U bounds separately for complement/outside challenges. Region naming alone does not implement detection.

## Verification

Each FBX was independently re-imported in Blender. Source and imported vertex, face, triangle and mesh counts matched. No zero-area faces or loose vertices were found; scales are unit scale, and no modifiers, lights or cameras are exported. The preview images show the re-imported geometry with studio emission and bloom restored. Mobile performance has not been measured on a device.
