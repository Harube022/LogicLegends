# The Primal Axiom

An original low-poly, skinned stone guardian built in Blender 5.0.1 from the supplied `SirRajjang.png` concept. Includes its editable source, FBX, thirteen skeletal animation clips, URP materials, Animator Controller, and reusable Unity prefab.

Everything delivered is under `Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/MONSTERS/PrimalAxiom/`.

## Use in Unity

Drag **`Prefabs/PrimalAxiom.prefab`** into a scene. The prefab starts in **Sleep**, including its stored scene-view pose. Its Animator is on the prefab root, uses a **Generic** avatar, and has **Apply Root Motion disabled**. Unity units are meters, the origin is at ground level, all assembly scales are one, and the character faces **+Z**. The awake character is approximately 4.86 m tall, 4.98 m across the fists, and 2.52 m deep. Scale the prefab root uniformly if a smaller guardian is needed.

The two materials use **Universal Render Pipeline/Lit**. Runes, horn lines and eyes use opaque emissive geometry. Their color remains visible without bloom. The prefab has one SkinnedMeshRenderer, two submeshes, no particles, no lights, no transparency, and no custom runtime components.

The `Animations/PrimalAxiom.controller` contains thirteen named states and one **Trigger** parameter with the same name as each clip. Set a trigger to request that animation, or use Animator.Play/CrossFade with a state name. Any State transitions take 0.14 seconds. This controller provides animation routing only.

- `Wake_01`, `Wake_02` and `Wake_03` hold their final poses until the next requested state. Their starting poses continue the preceding awakening stage.
- `Full_Awaken`, `Capture`, `Jumpscare`, `Barrier_Cast`, `Attack` and `Stunned` automatically return to `Idle` after finishing.
- `Return_To_Sleep` automatically returns to `Sleep`. Its final pose matches the beginning of `Sleep`.
- `Sleep`, `Idle` and `Chase` repeat. **Chase is in place**; future gameplay owns movement and navigation.
- For a normal staged awakening, request `Wake_01`, then `Wake_02`, `Wake_03`, and `Full_Awaken` as appropriate. No wake counting, targeting or other gameplay behavior is implemented.

## Mesh and materials

| Property | Delivered asset |
| --- | --- |
| Triangles | **6,240** |
| Authored mesh vertices | 3,771 |
| Imported Unity vertices | 15,734, including flat-normal and UV splits |
| Skeleton bones | **34** |
| Maximum authored influences per vertex | **2**, normalized; no unweighted vertices |
| Skinned renderers / material slots | **1 / 2** |
| Materials | `Axiom_StoneGold`, `Axiom_RuneEmissive` |
| Base color atlas | 1,024 × 1,024 PNG |
| Emission atlas | 512 × 512 PNG |
| Texture Android overrides | ASTC 6 × 6, mipmaps enabled |
| Shader | Unity URP/Lit, opaque |
| Animation sampling | 30 fps; keyframe reduction on FBX import |
| Runtime root motion | Disabled; skeleton Root remains stationary |

The rig contains Root, Pelvis, Spine_01, Chest, Neck, Head, Jaw, Mane, Beard and Tablets; per side it has Shoulder, UpperArm, Forearm, Hand, two finger groups, Thumb, Thigh, Shin, Foot, Toe and Brow. The extra tablet bone folds the hanging mathematical tablets forward when the monster lies down. The mane and beard have secondary motion. The jaw opens independently for roars; the eyelid/eye bones close the eyes in Sleep. Finger groups close around a capture pose. Stone armor uses intentionally firm weights over a weighted torso and limb core to preserve its faceted shape.

The palette atlases contain padded, per-facet color islands with subtle mineral variation. No external textures, custom shaders or runtime material scripts are required. The Android texture overrides are configured; device support and actual performance still need to be checked on the game's target hardware.

## Animation clips

Every standalone `.anim` is under `Animations/`. The same thirteen takes are embedded in `Models/PrimalAxiom.fbx`; the Model Importer has their names, frame ranges, loop flags, and root-lock settings configured. The standalone files are the motions assigned in the controller.

| Clip | Seconds | Loop | Motion |
| --- | --- | --- | --- |
| Sleep | 4.0 | Yes | Grounded folded posture, slow breathing, closed eyes |
| Wake_01 | 1.8 | No | Slight head lift and finger movement, still asleep |
| Wake_02 | 2.2 | No | Shoulder and arm movement, head rises |
| Wake_03 | 2.0 | No | Push upward, eyes opening, crouched alert pose |
| Full_Awaken | 3.0 | No | Rise from Wake_03, lift head and open jaw in a roar |
| Idle | 3.2 | Yes | Alert hunch, breathing, subtle sway and blink |
| Chase | 0.8 | Yes | Fast alternating arm/leg charge, in place |
| Capture | 1.8 | No | Reach forward, curl fingers, recover |
| Jumpscare | 1.8 | No | Lean the face toward the player and roar |
| Barrier_Cast | 3.0 | No | Raise both arms, hold a channeling pose, recover |
| Attack | 2.0 | No | Raise both fists, slam forward to ground, recover |
| Return_To_Sleep | 3.0 | No | Lower onto forearms and fold into the original sleeping pose |
| Stunned | 1.8 | No | Recoil, stagger sideways, regain balance |

Capture has no player attachment or constraints. Barrier_Cast animates only the guardian. Jumpscare moves the body and jaw; it does not create a camera or effects. There are no animation events that call gameplay code.

## Source and rebuilding

`Models/PrimalAxiom.blend` contains the editable skinned mesh, full armature and all thirteen actions. Its separate **STUDIO** collection contains review lighting, camera and floor; those objects are excluded from the exported FBX. Blender's modeling front is -Y; the FBX export assembly is rotated to face Unity +Z. The FBX is the game import; the `.blend` is the editing source.

`Authoring/create_primal_axiom.py` reproduces the mesh, palette atlases, explicit weights, analytic limb poses, actions, FBX, source file and Blender hero render without add-ons. Run with Blender 5.0:

```text
blender --background --factory-startup --python <absolute path to Authoring/create_primal_axiom.py>
```

After changing/rebuilding the FBX, run **Tools → Primal Axiom → Build asset package** in Unity. This configures importers, updates the existing materials and clips, rebuilds the animation controller, saves the prefab and validates it. The setup retains the GUIDs of existing standalone clips/materials. Rebuilding replaces this package's controller configuration and prefab overrides, so preserve intentional manual changes before using it.

`Editor/PrimalAxiomAssetSetup.cs` is editor-only authoring and verification code. It is excluded from player builds and is not attached to the prefab. No AI, networking, targeting, puzzles, health, collisions or gameplay scripts were added.

## Verification and review

Verified in the connected **Unity 6000.3.6f1** project on **16 October 2026**:

- FBX and prefab load; Generic avatar is valid; Animator states play through the actual Animator API.
- All thirteen clips deform this skeleton. All 350 transform bindings per clip resolve.
- The imported mesh has 6,240 triangles, 34 skin bones and two valid URP materials, with zero missing prefab references.
- Every clip was sampled at 30 fps and the resulting skinned mesh was checked for motion, root displacement, ground clearance and loop endpoints.
- Sleep, Idle and Chase have matching start/end meshes. Root displacement is zero. Import compression produces at most approximately 1.7 mm of ground contact error across sampled frames.
- Return_To_Sleep ends within 0.002 mm of Sleep's starting mesh after FBX import.
- Full_Awaken and Jumpscare move the jaw approximately 34° and 36° respectively.
- Actual imported URP poses were rendered in an isolated Unity preview scene. The existing gameplay scenes were not opened or saved by the asset setup.

Evidence is in `Previews/UnityValidation.json`, `Previews/AnimatorPlaybackValidation.json` and `Previews/BlenderValidation.json`. Re-run **Tools → Primal Axiom → Verify imported prefab and clips** after edits. Unity review rendering uses a frozen bake of each sampled skinned pose so that the default Sleep state does not overwrite the review pose during camera rendering.

View `Previews/PrimalAxiom_Hero.png`, `Previews/PrimalAxiom_UnityContactSheet.png`, `Previews/PrimalAxiom_AnimationReview.gif` and the individual `Unity_*.png` pose renders. The GIF contains all thirteen actual Unity clips at 12 fps. To recreate it, render **Tools → Primal Axiom → Render animation review frames**, then run `Authoring/package_reviews.py` with a Python installation containing Pillow. Its intermediate frames go to the project's Temp folder.

No Android device profiling or player build was performed. Existing unrelated project warnings may remain; asset-specific import and script errors were checked in the connected Unity Console.
