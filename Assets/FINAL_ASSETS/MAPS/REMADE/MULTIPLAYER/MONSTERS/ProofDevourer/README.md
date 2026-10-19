# The Proof Devourer

An original, wingless and tailless low-poly quadruped modeled and animated in Blender 5.0.1 from the supplied `The Proof Devourer Asset Sheet0.png` concept. The mesh has no visible eyes. Its defining features are a pale pink faceted hide, raised back, muscular forelimbs, red claws, glowing mathematical inscriptions and a hollow circular mouth with two inward-facing tooth rings.

All deliverables are under `Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/MONSTERS/ProofDevourer/`.

## Unity use

Drag **`Prefabs/ProofDevourer.prefab`** into a scene. The root has an Animator using the model's valid **Generic** avatar, `Animations/ProofDevourer.controller`, and **Apply Root Motion disabled**. Both the stored scene-view pose and the default Animator state are **Sleep**. The character faces **+Z**, uses meters and unit assembly scales, and has its origin on the floor. Its awake bounds are approximately **4.05 m wide × 3.38 m high × 4.05 m long**. Scale the prefab root uniformly to suit the level.

There is one skinned renderer with two opaque URP/Lit materials. Emissive mathematical symbols are actual surface geometry with interpolated skin weights. They remain colored without bloom. The prefab has no cameras, lights, particles, colliders, player objects, custom runtime scripts or animation events.

The controller contains all thirteen named states and a **Trigger** parameter with the same name as each clip. A trigger requests that state through a 0.14 second transition. Future gameplay can also use Animator.Play or CrossFade with the state name.

- Sleep, Idle and Chase loop. Chase is entirely in place; gameplay owns movement.
- Wake_01, Wake_02 and Wake_03 hold their final poses until the next requested state. Request them in order, then Full_Awaken, for the staged awakening. Their adjacent boundary poses match.
- Full_Awaken, Capture, Jumpscare, Barrier_Cast, Attack and Stunned automatically return to Idle when finished.
- Return_To_Sleep returns to Sleep and ends in Sleep's original pose.
- Capture provides the reaching and claw-closing body motion only. Jumpscare extends the neck and gapes toward a future encounter camera. Barrier_Cast raises the forelimbs into a channeling pose. No attachment, camera, barrier or other gameplay system is implemented.

## Mesh, textures and rig

| Property | Delivered |
| --- | --- |
| Triangles | **6,280** |
| Authored vertices | 3,880 |
| Skeleton bones | **33** |
| Authored bone influences | At most **3** per vertex, normalized; none unweighted |
| Skinned renderers / material slots | **1 / 2** |
| Materials | `Devourer_PaleSkin`, `Devourer_RedRunes` |
| Shader | Universal Render Pipeline/Lit, opaque |
| Textures | 1,024 × 1,024 base color; 512 × 512 emission |
| Android texture overrides | ASTC 6 × 6, mipmaps enabled |
| Animations | **13** standalone .anim files and the same thirteen FBX takes |
| Sampling | 30 fps with FBX keyframe reduction |
| Root motion | Disabled; Root remains stationary |

The torso, neck and face opening form a continuous surface. The mouth is a three-dimensional lip ring and inward-facing throat funnel, with 36 individual teeth. Four radial controls (`Mouth_Upper`, `Mouth_Lower`, `Mouth_Left`, `Mouth_Right`) move the lips and teeth for breathing, awakening and roaring. There is no conventional eye-bearing skull or humanoid jaw.

The skeleton consists of Root, Pelvis, Spine_01, Spine_02, Chest, Neck_01, Neck_02, Head, four mouth controls and BackRidge; each side has Shoulder, UpperArm, Forearm, Hand, two digit groups, Thigh, Shin, Foot and Toe. There are no eye, wing or tail bones. Front digits have solid hooked claws, and both hind feet have articulated toes. The arch and small dorsal ridges maintain a distinct side and rear silhouette.

Faceted normals are imported from Blender. The palette texture uses padded per-facet UV islands, with mild skin color variation. Runes include summation marks, cubes, triangles, circles and universal-quantifier forms. They are projected onto the actual skin and inherit its weights. All runtime surfaces use the two materials; no external shaders or effects are needed.

## Animation clips

Every clip is stored at `Animations/<name>.anim` and assigned in `Animations/ProofDevourer.controller`. The Model Importer for `Models/ProofDevourer.fbx` also has all thirteen takes configured with these names, loop flags, frame ranges and locked root transforms.

| Clip | Seconds | Loop | Motion |
| --- | --- | --- | --- |
| Sleep | 4.4 | Yes | Folded ground posture, slow breathing, small mouth pulse |
| Wake_01 | 1.8 | No | Claw twitch and slight head movement |
| Wake_02 | 2.2 | No | Shoulder movement, neck and head rise |
| Wake_03 | 2.2 | No | Forelimb push into a partly raised posture |
| Full_Awaken | 3.0 | No | Rise from Wake_03, lift the neck and gape in a roar |
| Idle | 3.4 | Yes | Alert hunch, breathing, head sway and mouth pulse |
| Chase | 0.8 | Yes | Fast diagonal limb cycle and heavy quadrupedal crawl |
| Capture | 1.8 | No | Reach both forelimbs forward, close the claws, recover |
| Jumpscare | 1.8 | No | Extend the neck toward the camera, enlarge the circular maw |
| Barrier_Cast | 3.0 | No | Raise and spread the forelimbs, hold a channeling pose |
| Attack | 1.8 | No | Shift weight onto the left forepaw, sweep the right claws across the front, then recover smoothly |
| Return_To_Sleep | 3.2 | No | Lower the torso and fold the limbs into Sleep |
| Stunned | 1.8 | No | Recoil, sway off balance, step out with one forelimb, recover |

## Skinning quality

The renderer explicitly requests **Bone4** skinning for its maximum three influences. The project's currently active **Very Low** preset uses **OneBone**. In this Unity Editor, the active project limit affects both CPU mesh baking and live skinned previews, even with the renderer set to Bone4. At that preset Unity uses a coarser deformation; four-bone skinning preserves the authored neck, lips and joint blends.

The asset was checked at both limits. All thirteen clips move, their loops close, their root stays fixed, and ground contacts remain within the verification tolerance at the active OneBone setting as well as FourBones. The project's preset and settings file are preserved. When reviewing the full quality mouth deformation, use a quality configuration that permits four bone influences. No Android device performance claim is made.

The editor's numerical validation and frozen-pose render temporarily use FourBones and restore the original value and editor dirty state in a `finally` block. **Tools → Proof Devourer → Render live skin check at project quality** renders the actual skinned renderer using the current project settings; its `UnityLive_*.png` images show the active preset's result. This separates the authored full quality review from the project's lowest-quality approximation.

## Source and rebuilding

`Models/ProofDevourer.blend` contains the editable skinned mesh, armature, and all thirteen actions. The separate **STUDIO** collection contains the review camera, lighting and floor and is excluded from the FBX. The Blender modeling front is -Y; only the export assembly is rotated to face Unity +Z. Use the FBX as the game import and the `.blend` as the authoring source.

`Authoring/create_proof_devourer.py` reproduces the geometry, weights, textures, skeleton, actions, FBX, Blender source, validation data and hero render. Run in Blender 5.0 without add-ons:

```text
blender --background --factory-startup --python <absolute path to Authoring/create_proof_devourer.py>
```

Then run **Tools → Proof Devourer → Build asset package** in Unity. `Editor/ProofDevourerAssetSetup.cs` configures importers and materials, extracts the takes, creates the controller and prefab, and verifies them. Existing clip and material GUIDs are retained. Rebuilding replaces this package's controller configuration and prefab overrides; preserve intentional manual edits before rebuilding. The setup is editor-only and is excluded from player builds.

Review commands:

- **Verify imported prefab and clips** samples every clip at 30 fps and writes `Previews/UnityValidation.json`.
- **Render Unity review** writes thirteen full quality pose images and four model views.
- **Render animation review frames** writes intermediate frames under `Temp/ProofDevourerReviewFrames`.
- `Authoring/package_reviews.py`, run with Python and Pillow, assembles the pose contact sheet, four-view model sheet and animation GIF.

## Verification

Verified in the connected **Unity 6000.3.6f1** project on **16 October 2026**:

- The FBX, Generic avatar and prefab load, with one skinned mesh and two URP/Lit materials.
- All thirteen animations use the intended 33-bone skeleton. Every transform binding resolves. Mesh weights have no unweighted vertices.
- Every clip was sampled at 30 fps for actual vertex movement, stationary root, ground clearance and loop endpoints. Sleep, Idle and Chase have matching endpoints. Return_To_Sleep returns to Sleep.
- Source meshes at all adjacent awakening boundaries match exactly. Imported versions were also checked for continuity.
- All named states play through the actual Animator API; the prefab defaults to Sleep.
- Front, side, rear, top and animated Unity views were inspected. The model has no eyes, wings or tail.
- Missing references and Unity Console import/script errors were checked. No unrelated gameplay scenes or systems were changed.

Evidence: `Previews/BlenderValidation.json`, `Previews/UnityValidation.json`, `Previews/UnityValidation_ActiveQuality.json`, `Previews/AnimatorPlaybackValidation.json`, and `Previews/LiveSkinComparison.json`.

Review images: `ProofDevourer_Hero.png`, `ProofDevourer_UnityContactSheet.png`, `ProofDevourer_ModelSheet.png`, `ProofDevourer_AnimationReview.gif`, plus the individual `Unity_*.png` and `UnityLive_*.png` renders in `Previews/`.

No Android device profiling or player build was performed. AI, networking, targeting, health, puzzle logic and barrier systems remain outside this asset package.

## Claw slash revision — 19 October 2026

Attack now uses a right-claw cross-body slash: 0.38 seconds of anticipation, a quick 0.34-second cut, and weighted recovery to the existing Idle pose. The root stays fixed, while the left forepaw and hind feet support the body. The clip retains its original GUID, state name and 1.8-second duration. No damage, capture, multiplayer or hunting logic changed.

To revise only Attack, run `Authoring/reanimate_attack.py` in background Blender. It updates the action in the existing `.blend`, checks that the mesh, rig, weights and twelve other actions are unchanged, and exports the original character FBX. In Unity, **Tools → Proof Devourer → Apply authored claw slash to Attack** extracts the precise sampled motion into the existing clip. Import compression is temporarily disabled during extraction, then restored; this avoids sliding IK contacts. The asset-package rebuild also applies this extraction. **Review claw slash** checks continuity, root movement and supporting feet, and renders the pose sequence.

Verified in Unity 6000.3.6f1: 340 valid transform curves, matching Idle entry and exit, stationary root, maximum support-bone drift 0.00136 m, and 2.34 m of claw travel at prefab unit scale. The original Animator trigger plays Attack and returns to Idle. A focused Play Mode check using an aged capture fixture confirmed the original FAILED handler plays the updated clip, deducts exactly one heart, releases movement and resumes hunting after 1.8 seconds. This check uses the existing cinematic test scene offline; live multiplayer and device playback were not rerun for this animation-only change.

Evidence: `Previews/Slash_SourceValidation.json`, `Slash_UnityValidation.json`, `Slash_EncounterValidation.json`. Visual previews: `Previews/Khezu_ClawSlash.gif`, `Khezu_ClawSlash_Sheet.png` and eight `Slash_*.png` poses. The other twelve standalone clips, original asset metadata, controller, prefab, materials and textures were unchanged.
