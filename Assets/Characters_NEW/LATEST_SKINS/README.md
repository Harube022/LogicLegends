# Prepared LATEST_SKINS — 2026-11-05

This is the source-preparation snapshot. A subsequent local gameplay integration now uses separate `_Gameplay` variants; see [Integration/HANDOFF.md](Integration/HANDOFF.md). The original prepared `_Skin.prefab` files and extracted source clips below remain separate and intact.

## Contents
- 12 character prefabs: Female and Male × Common, Elite, Epic, Rare, Special, Uniform.
- 72 extracted animation clips: Carry, Jump, Idle, Run, Stun, Walk per skin.
- 12 external URP/Lit materials using the matching original basecolor texture and source material settings.
- 72 actual Unity-rendered clip previews, plus Preparation/AllSkins_Idle.png.
- Each skin has Animations/, Materials/, Prefabs/, and Previews/.
- The original folders spelled Elte were retained. Their assets are correctly named Female_Elite and Male_Elite.

## Use and naming
Use Prefabs/<Gender>_<Category>_Skin.prefab for placement. The renamed <Gender>_<Category>_Skin.fbx is the original Idle FBX containing the mesh. Other FBXs often contain only animation skeletons; dragging those files into a scene does not create a visible character.
Extracted idle clips are separately named Animations/<Gender>_<Category>_Idle.anim. Preview controllers are scoped to these prepared prefabs; their six states are for inspection only, with Idle as the default.

## Appearance and scale findings
All 12 original meshes, basecolor textures, and skeleton bone references are present. The original embedded materials already use URP/Lit. The new source models measure only about 0.015–0.017 world units tall, so they appear to disappear at normal gameplay scale. Prepared prefabs apply one equal X/Y/Z scale multiplier on an unanimated wrapper. Their original proportions and animation curves are preserved.
Reference heights use actual baked mesh vertices, not renderer culling bounds or copied transform values. Female reference: current Multiplayer_Female / UNIFORM_FEMALE. Male reference: current Multiplayer_Male / eka_costume1. Feet are grounded in the idle starting pose with an independent Feet Offset parent. Jumping and other authored motion remain unchanged.
Skinned renderer bounds were rebuilt across 25 samples of every matching clip, in root-bone space, to keep deformed meshes within their culling bounds.

| Skin | Original height | Uniform multiplier | Prepared height | Clips | Missing |
|---|---:|---:|---:|---:|---|
| Female Common | 0.016855 | 321.550 | 5.420 | 6 | None |
| Female Elite | 0.017103 | 316.890 | 5.420 | 6 | None |
| Female Epic | 0.015954 | 339.707 | 5.420 | 6 | None |
| Female Rare | 0.016786 | 322.872 | 5.420 | 6 | None |
| Female Special | 0.016253 | 333.457 | 5.420 | 6 | None |
| Female Uniform | 0.016718 | 324.185 | 5.420 | 6 | None |
| Male Common | 0.017077 | 313.242 | 5.349 | 6 | None |
| Male Elite | 0.014613 | 366.054 | 5.349 | 6 | None |
| Male Epic | 0.016589 | 322.464 | 5.349 | 6 | None |
| Male Rare | 0.015967 | 335.023 | 5.349 | 6 | None |
| Male Special | 0.016900 | 316.515 | 5.349 | 6 | None |
| Male Uniform | 0.016804 | 318.320 | 5.349 | 6 | None |

## Verification
- All 72 source/extracted curve fingerprints match, including timing, frame rate, key values/tangents/weights, object-reference curves, events, wrapping and clip settings.
- All animation paths resolve on their own skin. Generic rigs remain Generic; no cross-skin substitution or Humanoid retargeting was performed.
- All 72 clips drive the actual prepared prefab via isolated manual Animator PlayableGraphs; motion and sampled animated bounds pass.
- All 12 prefabs have valid meshes, bone references and textured materials, uniform scales, and grounded idle starting poses.
- Original source FBX and PNG bytes, and their GUIDs, remain unchanged through renaming.
- Existing scene/prefab/controller/script files checked: 2234. Changed outside LATEST_SKINS: 0.
- Missing meshes: 0. Missing textures: 0. Missing clips: 0. The newly supplied Male Elite Run.fbx is included and verified.
- Verification used isolated Editor preview scenes and actual Unity renders. It did not run player accounts, currency, saves, or gameplay progression. Runtime/mobile gameplay integration was intentionally not performed.

Detailed source paths, GUIDs, uniform multipliers, durations, frame rates and validation flags are in Preparation/PreparedSkins.json. Original live-file hashes are in Preparation/Baseline.json. The Editor-only preparation tool is Editor/LatestSkinPreparation.cs.
