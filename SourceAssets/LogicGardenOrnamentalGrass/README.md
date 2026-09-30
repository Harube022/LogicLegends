# Logic Garden ornamental grass

Reference-guided low-poly clump with 20 pointed blades, outward-arching lower leaves, taller central leaves, and three slender stalks carrying pale seed heads. Footprint approximately 0.90 x 0.90 metres; height 0.60 metres (Unity units at scale 1).

## Files

- `Assets/Art/LogicGardenOrnamentalGrass/LogicGarden_OrnamentalGrass.fbx`: complete game model in the Unity project.
- `SourceAssets/LogicGardenOrnamentalGrass/LogicGarden_OrnamentalGrass.blend`: editable source, stored outside Assets to prevent an extra model import.
- Perspective, front, side and top PNG previews and `geometry-report.json` are alongside the source.
- `Tools/create_logic_garden_ornamental_grass.py`: reproducible Blender 4.5 generator, to run from its project location.
- The download ZIP contains the FBX, source, previews, report, README and generator together.

## Geometry

401 source vertices; 216 polygon faces, comprising 138 quads and 78 triangles; 354 rendered triangles total. All leaf surfaces are quads. Triangles are used only for the tiny seed florets and stalk end caps.

Each blade uses three quads on its front and three reverse quads on its back. Back vertices have independent normals and a very small offset to avoid coincident surfaces. The additional bend improves the curved silhouette without subdivision. Leaves are intentionally open surfaces without side walls, not watertight solids. Seed stalks have a triangular cross-section with two sections; each head contains three simple eight-triangle florets.

No modifiers, subdivision, dense internal geometry, textures or alpha cards. Two mesh objects and two simple placeholder materials: `Foliage` (leaves and stalks) and `SeedHeads`. Both are children of `LogicGarden_OrnamentalGrass`. Named vertex groups in the Blender source make individual leaves, stalks and florets easy to select.

## Export and use

The FBX uses Y-up, metre units, unit object scales and a central ground-level root pivot. All geometry is baked into the meshes. Drag the FBX into a Unity scene, assign your materials, and optionally save the hierarchy as a prefab. Use ordinary opaque materials with backface culling; reverse leaf faces provide visibility from either side. Basic projection UVs are included, not a unique lightmap unwrap.

The export was re-imported into Blender and checked for preserved hierarchy, polygon counts, quad count, triangle count and unit scale. There are no loose vertices or zero-area faces. Previews show the re-imported FBX; studio lighting, camera and floor are excluded from both model files.

The mesh is designed for repeated mobile-game placement, with two renderer/material groups. No Unity device benchmark was performed. Frame rate still depends on visible copy count, shadows and shader cost. Unity may split source vertices at flat normals or other attributes and triangulates quads for rendering. Shared meshes and materials should be reused between copies.
