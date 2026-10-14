PALE STONE RUINS PROP KIT

Ruins_Props_Perimeter.fbx is a suggested arrangement around an open center.
It contains only new ruin props, without the screenshot's map, platform or
existing circular columns. All pieces can be repositioned independently.
The approximate clear center is 8 m x 8 m; adjust the layout to fit your map.

Nine individual FBXs are also supplied:
BrokenWall_A, BrokenWall_B, BrokenColumn_Stump, FallenColumn,
CollapsedArch_Fragment, WornSteps, CrackedPaving_Slab,
RubbleCluster_A and RubbleCluster_B.
Individual prop pivots are at ground level and centered on their footprint.
Metric scale, applied mesh rotations/scales, UV-mapped textured surfaces.
Weathered pale stone matches the supplied screenshot; moss is understated.

Blender: open SourceAssets/RuinsProps/Ruins_Props.blend. The perimeter is
visible and selected. The original modular pieces are hidden in the viewport;
unhide them in the Outliner to inspect their 3x3 arrangement. Separate objects
and material slots remain editable. No source map model was changed.

Unity: import RuinsProps.unitypackage into a URP project and use Prefabs/.
Materials/ contains URP Lit stone, moss and dark crevice materials, explicitly
linked to the PNG files in Textures/. Embedded FBX textures are also included.
For raw file import keep the Textures folder and .meta files. Run
Tools > Logic Legends > Build Ruins Prop Kit to recreate material mappings.
Manual materials: URP Lit, corresponding BaseColor image, metallic 0,
smoothness 0.13; white tint except Ruins_Crevice uses a dark gray tint.
The texture appearance uses image maps rather than relying on Blender nodes.

Blender source, geometry report, Unity validation report and previews are
included. Presentation floor, lights and cameras are excluded from FBX/source.
The assembled layout is a starting arrangement, with each prop independently
editable, rather than a fitted modification of the screenshot's map.
