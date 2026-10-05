# Sets Room / Arena

Static fantasy chamber architecture based on the supplied Sets Stage reference. This is asset 1 from the requested asset list. The room has a tiled stone floor, staggered masonry courses, banded columns, an empty pointed statue alcove, a shallow rear dais, burgundy-and-gold banners, eight sconces with stylized flame meshes, closed double entry doors and a separate coffered ceiling.

## Files

- `Assets/Art/SetsArena/Sets_Room_Arena.fbx`: complete room and named placement anchors.
- `Assets/Art/SetsArena/SetsArena_Palette.png`: 64 x 64 shared palette; also embedded in the FBX.
- `SourceAssets/SetsArena/Sets_Room_Arena.blend`: editable Blender 4.5 source with packed palette.
- Interior, cutaway and top previews: rendered from the exported and re-imported FBX.
- `geometry-report.json`: measured geometry counts and export validation.
- `Tools/create_sets_arena.py`: reproducible generator. Running it from its project location overwrites the generated room files.

The ZIP preserves the project-relative folders for all these files.

## Layout

Interior floor footprint is 14 x 12 metres. Wall/ceiling clearance is approximately 5.6 metres. The central floor reserves a 10 x 7.5 metre rectangle for later gameplay pieces. Its thin stone inlay is architectural decoration; it is not a functional Universal Set Boundary. The rear dais is 0.3 metres high and sized for the existing Book Statue. The statue is not duplicated into this export.

The root pivot is the center of the floor at walking height. The FBX is exported Y-up at metre scale with unit object scales. Floor sections, walls, piers, banners, doors, sconces and ceiling remain separate named meshes. Their shared assembly origin makes replacement and alignment straightforward. Hide or remove `Ceiling_Removable` for an overhead gameplay camera. Doors are separate static meshes that can be configured later.

Six empty anchors identify suggested locations: BookStatue, VennFloor, SetInformationBoard, ChallengeUI, ElementPedestal and PlayerEntrance. They are organizational transforms, not visible models or working gameplay components.

## Counts

- 16,027 source mesh vertices.
- 12,975 polygon faces.
- 26,517 triangles.
- 68 mesh objects plus root and placement anchors.
- Three shared materials, one tiny palette image.
- No subdivision surfaces, unapplied modifiers or animation.

Most surfaces are simple quads with small modeled chamfers. The room has no LODs. Actual runtime vertex counts may increase because of split normals and UV seams. No mobile-device performance benchmark has been run.

## Import and use

Keep the FBX and palette together. The three source materials are `Arena_StoneCloth`, `Arena_Metal` and `Arena_Flame`. Remap these to suitable shaders for your Unity render pipeline as needed. Palette UVs provide flat color variation; generate or create a separate lightmap UV channel if baking lighting.

This delivery contains room geometry only. Add Unity colliders, lights, door behavior and gameplay scripts in the project. The Venn diagram, universal-set boundary, statue, elements, spawn pedestal, boards, UI, trigger zones and feedback systems are separate stages of the asset list and are not generated here. Flame meshes are static; emission does not replace a Unity Light component.

## Validation and previews

The FBX was re-imported in Blender and matched the source vertex, polygon, triangle and mesh counts. No zero-area faces or loose vertices were found. Imported scales were checked as 1 and modifiers as absent. Exports and source contain only meshes and empty transforms; preview cameras, lights and compositor glow are excluded.

The interior preview shows the enclosed room. The cutaway preview temporarily hides the roof and front/east architecture for visibility. The top preview hides only the ceiling. These visibility changes are preview-only; the delivered FBX and Blender source contain the complete enclosure.
