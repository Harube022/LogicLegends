# Logic Garden low-poly bush

Created from the supplied bush reference, emphasizing rounded layered foliage and mobile-friendly geometry.

## Files

- `../../Assets/Art/LogicGardenBush/LogicGarden_Bush.fbx`: Unity-ready mesh; also importable into Blender.
- `LogicGarden_Bush.blend`: editable source mesh, with a vertex group for each leaf and the inner core.
- `LogicGarden_Bush_Preview.png`: studio preview. The ground, lights and camera are preview-only and are not included in either model file.
- `geometry-report.json`: measured geometry and FBX round-trip checks.
- `../../Tools/create_logic_garden_bush.py`: reproducible Blender generation/export script.

## Geometry

1,394 mesh vertices; 2,124 triangular faces; 165 individual closed leaves; one closed low-resolution core. One mesh object, one placeholder material, no textures, no modifiers and no subdivision. All components are closed and outward-facing; intentional leaf overlaps create the dense silhouette. No open edges, nonmanifold edges or degenerate faces were found in the re-imported FBX.

Dimensions approximately 1.63 x 1.68 x 1.56 metres (Blender XYZ). Origin is at ground level, centred horizontally; object scale is (1,1,1). FBX is exported Y-up. A basic reusable leaf UV layout is included, with overlapping UVs; it is not a unique lightmap unwrap.

## Use

Drag the FBX from the Unity Project window into a scene, assign your own material, and duplicate it or save it as a prefab. The mesh is designed for repeated placement, with a single material slot and no transparency requirements. Keep copies on a shared material. Final frame rate depends on visible copy count, shadows, shader and target device; no Unity device performance benchmark was performed. Unity's imported vertex count can differ because of attribute splitting and import settings.

For editing, open the `.blend` directly or use Blender File > Import > FBX. Leaves are disconnected closed components in a single mesh. In Edit Mode, hover over a leaf and select linked to edit it, or select its named vertex group. The source `.blend` is outside Assets so Unity does not import a duplicate model.

Regenerate with Blender 4.5: `blender --background --python Tools/create_logic_garden_bush.py` from the repository root. This overwrites the generated asset files.
