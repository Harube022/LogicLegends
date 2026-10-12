INDUCTION CHAMBER — INFORMATION BOARD AND FORMULA SCROLLS STAND

QUICK START (Unity 6000.3.6f1, URP 17.3)
1. Import InductionBoards.unitypackage. URP and TextMeshPro must be installed.
   The package includes the font and atlas assets used by the editable labels.
2. Drag Prefabs/InformationBoard.prefab or Prefabs/FormulaScrollsStand.prefab
   into your scene. Materials already reference the supplied baked textures.
3. Edit the TextMeshPro objects under the exported ...Anchor transforms.
   Information Board: ChallengeTitleLabel, ChallengeFormulaLabel,
   ChallengeInstructionsLabel. Formula rack: ScrollLabel_01 through _04.
   No label is baked into a mesh or texture.

The two models match the altar's blue-gray stone, warm bronze/gold trim,
parchment and blue crystals. Both use meters and bottom-center root pivots.
The information board is approximately 2.18 m wide x 3.30 m high.
The smaller freestanding rack is approximately 1.51 m wide x 2.30 m high.
The rack slopes back by 14 degrees and has four independent scroll objects.
All four scrolls reference one mesh. ReusableFormulaScroll.prefab provides a
single reusable scroll with its own editable label; its pivot is at its center.
ScrollSlot_01 through _04 are placement parents. Move the slot to move its
scroll and label together. The full stand's root pivot remains bottom-center.
The parchment object on the Information Board is named Info_ParchmentSurface.

FILES
InformationBoard.fbx: only the upright board and its support/base.
FormulaScrollsStand.fbx: only the angled rack and four separate scrolls.
Materials/: four explicitly configured Unity URP Lit materials.
Textures/: 16 baked PNG images, grouped by material name.
Editor/InductionBoardsSetup.cs: asset setup, validation and packaging.
Prefabs/: the two ready-to-use props and a convenience reusable scroll.
Blender source and previews are included under SourceAssets/InductionBoards.
The source contains only the two models, without presentation lights or cameras.

TEXTURES / BAKING
For Stone, Parchment, Metal and Crystal:
  *_BaseColor.png — sRGB, baked color without lighting/shadows.
  *_Normal.png — tangent-space normal, import as Normal Map.
  *_Roughness.png — linear roughness, supplied as source data.
  *_MetallicSmoothness.png — linear URP packed data, R=metallic,
                             A=smoothness (1 - roughness), G/B unused.
Stone/parchment are 1024x1024; metal/crystal are 512x512.
Procedural color/roughness and bump detail were baked using Blender Cycles.
The final exported materials contain image texture nodes, not procedural nodes.
Texture maps are repeatable surface swatches applied with explicit mesh UVs.
Parchment on the large board uses continuous rectangular UVs. Other mesh
components use a smart unwrap, including the shared reusable scroll geometry.
Material settings such as metallic and crystal emission are recreated in Unity.
Blender shader nodes are not assumed to survive FBX import.

FBX EMBEDDING / PORTABLE PATHS
Both binary FBXs embed their connected color, normal and roughness textures.
Relative texture references use the standard <ModelName>.fbm extraction folder.
The authoritative portable maps, including packed Unity maps, are all supplied
in Textures/. No texture requires a path on the creator's machine in Unity.
Packed metallic/smoothness textures are assigned through the Unity setup;
they are not a Blender FBX shader feature.

IF IMPORTING RAW FILES INSTEAD OF THE UNITY PACKAGE
Copy Assets/Art/InductionBoards into your project's Assets/Art directory.
Include .meta files to preserve FBX-to-material mappings and prefab references.
Import TextMeshPro Essential Resources if your project does not contain them.
Run Tools > Logic Legends > Build Induction Information and Formula Boards.
This explicitly configures texture color spaces, creates URP materials, and
remaps the FBX's Chamber_Stone/Parchment/Metal/Crystal material slots.
It creates prefabs; it does not populate or save a gameplay scene.

OPTIONAL EMBEDDED TEXTURE EXTRACTION
Select either FBX in Unity's Project window > Materials tab > Extract Textures.
Choose a folder such as Textures/Extracted. The supplied Textures/ PNGs already
provide these maps, so extraction is unnecessary when using this pack's setup.
Do not rely on automatically extracted Blender materials to recreate shading.
Use the supplied URP .mat files, the setup menu, or the explicit manual recipe.

MANUAL URP MATERIAL RECIPE
Shader: Universal Render Pipeline/Lit, Surface Type Opaque.
Base Map: *_BaseColor.png; Base Color white.
Normal Map: *_Normal.png; normal strength 0.35.
Metallic Map: *_MetallicSmoothness.png; Smoothness 1; source Metallic Alpha.
Enable normal-map and metallic-map shader variants (Unity inspector handles this).
Crystal only: enable Emission, use Crystal_BaseColor.png as emission map,
             emission color white at intensity 0.8.
Other materials: emission disabled.
Use sRGB only for BaseColor. Packed data and roughness must be linear.
Visible bloom requires the scene's own post-processing configuration.

VERIFICATION
geometry-report.json: FBX export/reimport counts, UV coverage, relative paths,
zero-area checks and pivots. bake-report.json: baked maps and bake method.
unity-validation.txt: imported texture settings, every renderer's URP material
and texture references, labels, shared scroll mesh, bounds and render checks.
Unity_Assets_Preview.png: render of the actual imported FBXs with mapped URP
materials and editable text. Studio Blender renders are clearly named separately.

No altar, chamber, character, timer, statue, scene or gameplay system is included.
