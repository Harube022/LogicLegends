# Element Objects 2–9

Eight numbered versions of the existing blue Sets element cube. Each FBX contains a complete cube with luminous edges, corner caps, its modeled floating number, and a label anchor. Numbers are real mesh geometry with slight thickness and do not depend on Unity text components or external fonts.

## Files

`Element_Object_2.fbx` through `Element_Object_9.fbx` are separate reusable assets. The included `Element_Objects_2_to_9.blend` lays out all eight for editing. A shared palette PNG is also embedded in every FBX and packed in the Blender file. The preview is rendered from the exported FBXs.

Every individual FBX has a ground-level origin at (0,0,0), unit scale and Y-up export. Cube width is approximately 0.505 units; numeral height is 0.27 units. Each model has four meshes and two shared materials. No modifiers, subdivision, animation, lights or cameras are included. Geometry counts range from 340 to 672 triangles per complete numbered object; see geometry-report.json for individual measurements.

The number is a separate child mesh, allowing replacement or removal. It is static and does not billboard automatically. Pickup and placement behavior are not included. Set up suitable emission materials and bloom in Unity for the preview glow; FBX does not contain Unity shader/post-processing configuration.

All eight FBXs were individually re-imported. Vertex, face, triangle and mesh counts matched the source; object scales and root origins were checked. No zero-area faces or loose vertices were found.

The project generator is Tools/create_element_numbers_2_to_9.py. Rebuilding uses the existing SourceAssets/SetsGameplay/Sets_Gameplay_Assets.blend and Windows Arial Bold font. Neither dependency is required to use the delivered FBXs or edit the delivered Blender file.
