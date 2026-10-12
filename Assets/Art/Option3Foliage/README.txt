OPTION 3 — BUSHES AND FLOWERS ONLY

Import Option3Foliage.unitypackage into Unity 6 with Universal Render Pipeline.
Use Prefabs/Option3_BushesAndFlowers for the assembled foliage perimeter.
Only clipped bushes and pink/white/lavender flowers are included: no stone curb,
soil/grass base, tree, arches, lanterns or existing garden model.

FBX files:
Option3_BushesAndFlowers.fbx: 22 low trimmed hedge pieces and 12 mixed clusters.
Hedge_Segment.fbx: reusable approximately 2 m long hedge segment.
Mixed_Flower_Cluster.fbx: reusable pink/white blooms and lavender-like spikes.
Each part has its own clearly named mesh object. All rotations/scales are
applied. The assembled pivot is at ground level in the center of the opening.
The opening is approximately 12 m x 9.4 m, based on the existing garden asset
dimensions. Adjust positions of individual sections or use the modular prefabs
to fit your exact garden. Each modular model has a ground-level pivot.

Texture: Textures/Option3_FoliagePalette.png, shared by one opaque URP material.
UVs deliberately sample leaf/petal colors from this texture. No alpha cutouts
or procedural Blender shading is needed. Folded leaf front/back geometry is
included, and flowers have geometric petals, centers, stems and purple florets.

Raw FBX setup: keep Textures/ with the FBXs. The palette is also embedded.
Use Tools > Logic Legends > Build Option 3 Foliage to create and remap the URP
material and generate the three ready-to-use prefabs. For manual setup, use
URP Lit, assign the palette to Base Map, set Base Color white, metallic 0,
smoothness 0.16, point filtering and disable mipmaps for this small color atlas.
The included .meta files retain explicit FBX material remaps.

Editable Blender source and foliage-only previews are provided in SourceAssets.
Preview floors, lights and cameras are excluded from all FBX files.
This is a standalone reconstruction of the foliage from the chosen concept;
the existing garden model and gameplay scenes have not been modified.
