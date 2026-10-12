INDUCTION ALTAR — ONE REUSABLE MODEL

Import the supplied Unity package into a Unity project with Universal Render
Pipeline and TextMeshPro. Drag one of Prefabs/Altar_BaseCase,
Altar_InductiveHypothesis, or Altar_InductiveStep into your scene.
All three use the exact same mesh assets. Only accent materials and text differ.
No gameplay scene is modified by this asset pack.

FILES
InductionAltar_Shared.fbx: complete altar including separate blank boards.
InductionAltar_Boards.fbx: optional board-only export in matching altar coordinates.
Prefabs/Altar_*.prefab: complete stations with editable TextMeshPro labels.
Prefabs/Boards_*.prefab: interchangeable title + two parchment panels, with labels.
Materials/: shared stone, bronze, parchment and three accent/crystal color pairs.
Textures/Stone_BaseColor.png: small-scale blue-gray stone surface.
Editor/InductionAltarSetup.cs: rebuilds materials and prefabs in the editor.

GEOMETRY
One shared mesh assembly; 9 named mesh objects, 5,780 triangles total.
Metric units; approximately 2.20 m wide and 2.925 m tall.
Root pivot is the bottom center. Applied mesh transforms, UVs and normals.
StonePedestal, UprightFrame, BronzeHardware, StationAccents, CrystalAccents,
CircularEmblem, TitleBoard, ParchmentPanel_01 and ParchmentPanel_02 are separate.
The stone base is shared. The two scroll panels are independently selectable.
No chamber, statue, HUD, torches, character or environment is included.

LABEL EDITING
Expand the prefab hierarchy to TitleTextAnchor, Panel01TextAnchor or
Panel02TextAnchor. Edit their TextMeshPro component's Text field in the Inspector.
The labels are not part of the FBX, texture or altar mesh.
Default titles: BASE CASE / INDUCTIVE HYPOTHESIS / INDUCTIVE STEP.
Hypothesis and step panel wording is placeholder content, editable per challenge.
Panel text uses plain P(k) notation and >= so the included font covers it.

BOARD SWAPPING
The board-only prefabs use the same FBX submeshes as the complete altar prefabs.
To swap boards manually, disable TitleBoard and both ParchmentPanel objects
in the shared altar, then place a Boards_* prefab at the altar root with local
position and rotation zero and scale one. Disable the old text anchors too.
For a whole-station palette swap, use the corresponding Altar_* prefab or
replace Altar_Accent_* and Altar_Crystal_* material slots on all relevant objects.
Each variant retains the same shared geometry and pivot.

SETUP / REBUILD
Tools > Logic Legends > Build Induction Altar Prefabs.
Requires imported TextMeshPro Essential Resources if no default font exists.
If using a different render pipeline, change the supplied materials to that
pipeline's Lit shader and retain the base colors, textures and emission values.
Emission is enabled; visible bloom requires your scene's post-processing setup.
No lights or colliders are added. Add your desired collision/gameplay components
to a prefab variant rather than modifying the imported FBX.

SOURCE
Editable Blender source and three studio previews are supplied separately.
Preview labels, floor, cameras and lighting are excluded from the FBX.
This is a stylized reconstruction from the supplied reference, not an extraction
of the original reference's model.
