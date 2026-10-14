FULL RUINS MAP BORDER — ALL FOUR SIDES AND CORNERS

Fitted to the actual floor from the supplied screenshot:
Assets/FINAL_ASSETS/MAPS/REMADE/REMAKE TUTS/2nd/FLOOR.fbx.
The continuous structural skirt encloses the exposed floor edges from bottom
to top. A raised curb and capstone cover the complete perimeter; solid corner
piers join every corner. Decorative masonry joints do not cut through the
unbroken backing. The map's center and existing columns remain open.

FILES / PLACEMENT
Ruins_Map_FullBorder_NativeAligned.fbx: use with the original map FBXs in their
native imported coordinates. It is positioned to match FLOOR.fbx directly.
Ruins_Map_FullBorder.fbx: centered version with bottom-center pivot at zero.
Use this if you have moved the whole map to the origin or need custom placement.
Native centered-version offset in Unity: (329.679565, 9.909319, 13.554943).
Native centered-version offset in Blender: (-329.679565, -13.554943, 9.909319).
The original floor's native footprint is approximately 125.315 x 117.555 units.
If your scene scales the original map down, apply the same scale to the border.
If the map has been moved/rotated, parent both under the same layout root.

BLENDER
Open SourceAssets/RuinsMapBorder/Ruins_Map_Border.blend.
The border and a recentered reference copy of the original map are visible,
so you can inspect every edge and corner. REFERENCE_Map_NotExported contains
the map reference; none of its geometry is included in either border FBX.
Original project map files are unchanged. The border has four named editable
mesh parts: Stone_Foundation, Continuous_Rim, Masonry_Facing,
Border_Piers_And_Corners. Native mesh rotations/scales are applied.

UNITY / MATERIALS
Import RuinsMapBorder.unitypackage into Unity 6 with URP, then use Prefabs/.
Materials explicitly reference Textures/Ruins_Stone_BaseColor.png and the
baked darker Ruins_StoneShadow_BaseColor.png. Both are embedded in both FBXs.
For raw file import retain the .meta files and
Textures folder. Recreate mappings with Tools > Logic Legends > Build Full
Ruins Map Border. Manual material: URP Lit, supplied Base Map, metallic 0,
smoothness 0.12 and white tint for both materials; the darker shade is baked.
No procedural Blender shader is required by the exported appearance.

VERIFICATION
fit-and-geometry-report.json: measured map bounds, closed manifold structural
rings, no zero-area faces and FBX roundtrip counts.
unity-validation.txt: texture remapping and direct bounding-box fit against
the original FLOOR FBX. Border_Fitted_To_Map.png shows the complete fit.
The FBXs contain only the new border, without terrain, map or preview lights.
