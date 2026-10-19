# The Open Archive

A colorful low-poly fantasy island for Logic Legends, authored at **1 Unity unit = 1 metre**, using Unity **6000.3.6f1** and URP **17.3.0**. Intended platform: Android, 2–4 players.

The current environment was **improved in place**, retaining all **1,101 original GameObjects**, original mesh asset GUIDs, island layout, bridges, cliffs, waterfalls, spawn plaza, and integration anchors. The original scene rename to `MultiplayerGameplay.unity` is preserved through its GUID.

## Outer cliff fall prevention — 19 October 2026

A continuous invisible collision fence now closes the island's outer cliff gaps in all five saved scenes. The reusable `Prefabs/Terrain/OpenArchive_OuterEdgeSafety.prefab` sits under `OpenArchive/Environment/Cliffs/OuterEdgeSafety`. Ninety-six overlapping static BoxColliders follow the rim just inside the drop, with inner radius 98 local meters; in the 2.5x exploration/test scenes this is 245 world meters. The fence extends from -12 to 32 local meters, covering the cliff foundation and jumps from adjacent scenery.

The prefab contains no renderers, lights, particles, Rigidbody, navigation obstacle or runtime script. Its existing Ignore Raycast layer collides with the Player layer while the current camera obstacle mask ignores it. Original geometry and colliders are retained; no collision-layer settings, navigation assets, player controls, water logic, monster behavior, health or networking code changed. Interior rivers/ponds, spawn and the ten reserved spaces remain clear.

Use **Tools > Logic Legends > Open Archive > Safety > Apply outer cliff collision boundary** to restore the setup after regenerating the environment; stop Play Mode and save scene edits first. The prefab's individual BoxCollider dimensions remain editable in the Inspector. `Testing/EdgeSafety/Reports/SceneEdgeSafety.json` records zero missed capsule sweeps in 2,880 perimeter/height probes per scene (14,400 total), along with original-object preservation and interior-clearance checks. `EdgeSafetyPlayMode.json` passes **17/17** checks using the enabled original player's outward walks and full-speed jump attempts in eight directions. `EdgeSafetySourceProtection.json` verifies that only the five existing scene assets changed and all 552 other protected assets/scripts remain unchanged. No Unity Console errors were present. Tests are explicit Editor-only helpers and are not attached to saved scenes. Android hardware performance and live multiplayer were not tested.

## Pillar pursuit follow-up — 19 October 2026

A wider sweep reproduced nine remaining missed captures among 34 standable nest rock, wall-plinth, ruin and torch positions. Khezu could see the player but sometimes chose a reachable ground point outside his effective capture reach. The original four-footing regression did not cover these other surfaces.

The existing land-approach search now scores each destination using the same raised-target reach and actual projected upper-body sight lines used for capture. Khezu also finishes turning toward the selected player after the NavMesh path is consumed. Standing targets on raised scenery use the body's turning footprint, capped by the existing agent radius, plus the existing .65-local-meter arm allowance. A short support query keeps ordinary airborne jumps on the previous oriented-body reach. Flat-ground catch distance, running/chase speeds, collision meshes, NavMesh data, body/agent dimensions and encounter authority remain unchanged. Walls, overhead platforms and heights above the body still block capture.

`Testing/KhezuHunting/Fixtures/KhezuNestStandingSpots.json` stores the measured positions. Run `LogicLegends.Tests.Khezu.KhezuPillarPursuitPlayChecks.Begin()` explicitly in offline Editor Play Mode; it is never attached to a saved scene. `Reports/PillarPursuitBaseline.json` records the original 25/34 captures. `Reports/PillarPursuitPlayMode.json` now passes **34/34**, with continuous navigation, stable player positions, preserved hearts and restored controls. The separate `KhezuElevatedReachPlayChecks` passes **23/23** checks for wall/platform/range/height rejection, supported versus airborne reach and normal ground captures. `PillarPursuitComparison.json` combines those results; `PillarPursuitSourceProtection.json` confirms 552 existing scripts/assets remain unchanged and only the intended three hunting files plus the explicit reach regression helper changed. Reports are under `Testing/KhezuHunting/Reports`. Android device performance and live multiplayer transport remain unverified.

`PillarCaptureEncounter.json` verifies the real production cinematic/barrier at the formerly missed wall-plinth position (-19.75, 5.45, -31): anchored player lock, local challenge buttons, SUCCESS release with three hearts preserved, return to the nest, a completed 30-second sleep and subsequent resumed hunting. `Art/Previews/PillarReach_Fixed.png` is an actual Play Mode render of that capture.

## Close reach onto rocks and clearer stone — 19 October 2026

Khezu's existing host-owned hunting controller now checks actual head/chest/upper-arm sight lines when the selected player's feet are raised above his ground. This allows close captures onto the nest's low rubble and footings while solid walls, overhead platforms, excessive distance and heights above his body remain excluded. His normal ground catch distance (.65 world meters), movement balance, body scale and NavMesh remain unchanged. Reach is adjustable under **Reach onto raised rocks and ledges**: `elevatedReachDistance` is .65 monster-local meters, used only above an .85-world-meter feet-height threshold. The existing capture/cinematic/barrier/outcome pipeline still owns the encounter.

Reachable-side route sampling also checks Khezu's lower ground level and scores an upper-body view when the target is on a raised surface. It does not move him through scenery or teleport him onto the player. Explicit offline tests live separately in `Testing/KhezuHunting/KhezuElevatedReachPlayChecks.cs`; the initial 21-check suite is now extended to the 23-check regression described above. Four raised nest footings and two normal ground pockets produce real captures without monster teleports or navigation freezes. Wall, overhead-platform, excessive-distance and excessive-height fixtures reject captures. `ElevatedCaptureEncounter.json` additionally verifies an actual raised-footing cinematic/barrier handoff, player lock, challenge buttons, SUCCESS release, unchanged hearts and return to sleeping in the nest. No Unity Console errors remain. Android device performance and live multiplayer transport were not retested.

Shared stone materials also have much clearer rock grain, crevices and beveled paving joints. See [Art/README.md](Art/README.md) and [Art/Previews/Stone_After.png](Art/Previews/Stone_After.png). The map geometry and collision are preserved.

## Textured surface and afternoon lighting upgrade — 19 October 2026

The follow-up adds a stronger painted grass albedo, animated flames and embers on all eight torches, and rotating welcome rings/motes at the existing player spawn. It also fixes the coplanar nest-floor/entrance overlap using separate visual meshes while retaining the original collision meshes and navigation. These changes are saved in all five scenes. See [Art/README.md](Art/README.md) for the dedicated polish command, effect prefabs and verification.

All five saved environment/test scenes now use world-scale painted grass and earth detail, warm stone paving, weathered ruins and cliffs, darker nest soil, wood grain, turquoise shoreline foam and moving waterfall streaks. Existing tree LODs remain, with sparse flowers, mushrooms, ferns, roots and leaves away from paths and reserved spaces. Materials are assigned to the actual scene objects.

The original transforms, colliders, mesh references, ten topic slots, four entrances and gameplay components pass preservation checks. An offline Play Mode check confirms fourteen monster routes, player movement, hunting/capture, POV jumpscare, crimson barrier and SUCCESS return to sleep. Native Android performance and live multiplayer transport still need device verification.

See [Art/README.md](Art/README.md) for tuning, mobile budgets, limitations and evidence. Use **Tools > Logic Legends > Open Archive > Art > Apply surface and daylight upgrade** for this pass; the older geometry generators can overwrite its material assignments.

## Beast den visual redesign

The earlier den geometry pass retained all **1,126 objects** from the previous saved map and the exact vertex/index geometry of **277 protected meshes**. The island, pathway network, original water sections, four bridges, ten building anchors, and southern spawn retain their layout.

The nest now has a **17 m diameter** level sleeping hollow at **Y0.25**, recessed **0.55 m** below the Y0.8 perimeter. Its shallow shoulder rises over four metres, permitting wide bodies to cross without jumping. `MonsterSpawnPoint` is now **(0,0.25,0)**. An irregular rock rim, fractured pillars, damaged gray gateways, buried scratched slabs, compacted soil, rubble, moss, three broken restraint chains, and sparse gold rune fragments replace the clean plaza appearance. The sleeping center has no raised obstacles; the embedded slabs and scars are nearly flush with the ground.

The island additionally has broken arch pockets, two static ancient scribe statues, clustered boulders, small grassy hummocks, buried foundation corners outside the reserved squares, mossy bridge piers, and four small bank cascades. These are environment art only. No monster models or gameplay systems were added.

Use [Prefabs/Architecture/UniversalMonsterNest_Den.prefab](Prefabs/Architecture/UniversalMonsterNest_Den.prefab) for the redesigned reusable nest. The original assembly remains available as authoring history. New modular den meshes and prefabs share the existing vertex-color material and add no textures.

[Previews/DenRedesignValidation.json](Previews/DenRedesignValidation.json) records object/GUID preservation, unchanged protected geometry and spawn transforms, 901 sleeping-floor samples, clear animation volume, and the corrected monster spawn. The player-eye review is [Previews/OpenArchive_Nest_PlayerEye.png](Previews/OpenArchive_Nest_PlayerEye.png); [OpenArchive_Nest_Detail.png](Previews/OpenArchive_Nest_Detail.png) shows the whole den more closely.

## Explore with the existing controls

Open [Scenes/OpenArchive_Explore.unity](Scenes/OpenArchive_Explore.unity), press **Play**, and click the Game/Simulator view. The visible character starts at the southern spawn. The existing exploration copy retains its 2.5× map scale; the character and controls keep their original settings. Spawn placement follows the actual plaza surface at this scale.

- **Move:** the existing left joystick or WASD, using the project's original `GameInput` and `Player` settings.
- **Look:** drag the existing right-side look area; the shared `CM_ThirdPersonCam` supplies camera collision and two-finger pinch zoom.
- **Jump:** the original jump-arrow button or Space. The hand interaction and gear/settings buttons retain their original callbacks.
- **Run:** the existing Player behaviour runs after three seconds of continuous movement.
- **Pause/resume:** the existing settings panel and `LevelMenu` behaviour.
- **Return to spawn:** R or the exploration utility button. Falling below the island also returns the character to spawn.

The controls, sprites, positions, safe-area behaviour, and pause panel come from `Assets/Prefabs/PREFABS/WORLD_UI/Canvas 1.prefab`, as used by `MultiplayerStage` and the lesson scenes. The original shared prefabs and input scripts are unchanged. The local explorer shows the default uniform without cloud equipment or a multiplayer room.

The source [Scenes/MultiplayerGameplay.unity](Scenes/MultiplayerGameplay.unity) remains **environment only**, with a preview camera and empty anchors. No monsters, topic buildings, AI, puzzles, or multiplayer spawning logic were added.

## Maintain the improved map

Commands are under **Tools → Logic Legends → Open Archive**. Save all open edits and stop Play mode first.

1. Open `Scenes/MultiplayerGameplay.unity` to inspect the environment.
2. **Redesign Existing Nest And Island Art** maintains the latest den art pass in place. **Verify Den Redesign And Preserved Layout** checks it against the saved pre-redesign baseline. The earlier **Improve Existing Environment In Place** pass is retained as authoring history; running it can restore earlier nest surfaces and shadow choices.
3. **Rebake Existing Navigation And Validate** updates only navigation and its report after collision changes.
4. **Measure Improved Environment** writes actual coordinates, ground samples, geometry/clearance checks, and an inventory.
5. **Render Review Views** opens the saved source scene in Single mode and writes seven real-geometry renders, including player-eye and detail views of the nest. **Render Footprint Debug View** produces the annotated overhead image.
6. **Show Reserved Footprints And Entrances** toggles editor overlays. Enable the Scene view Gizmos button too. The anchors have no added runtime components.
7. **Sync Improvements To Existing Exploration Scene** updates the existing test environment and added scenery while retaining its player, camera, UI, and controls. Open `OpenArchive_Explore.unity` and Play.

**Create Exploration Test Scene** can create a fresh test copy when explicitly wanted. The original `OpenArchiveBuilder` and Blender kit authoring script remain available as source history. **Build Complete Environment** and **Verify Repeatable Build** regenerate the original environment and can overwrite refinements; do not use them to maintain this improved scene.

## Layout and anchors

X points east; Z points north. The island is **200 m across**, ground is predominantly Y 0, and the outer grass bypass remains clear.

| Element | Dimensions/location |
| --- | --- |
| Universal monster nest | Origin, 30 m footprint; sleeping floor Y0.25, perimeter Y0.8 |
| Primary nest entrances | Exactly four: North, South, East, West |
| Entry ramps | 10 m wide, smooth 0.8 m rise over 8 m run; no jump needed |
| Entrance anchors | Radius 15, Y 0.8, facing outward |
| Arch collision opening | About 10.8 m wide × 7.3 m high above its base; footing clearance about 10.3 m |
| Main ring | Radius 54, 10 m wide, radii 49–59 |
| Main radial paths | 10 m wide |
| Secondary paths | Approximately 6 m wide, including the nest bypass |
| Spawn plaza | (0,0,−83), 18 m diameter, floor Y 0.15 |
| Four spawn roots | Plaza-local (±2,1.23,±2), facing north; minimum separation 4 m |
| Future structure anchors | Exactly ten, radius 74 |

`UniversalMonsterNest` retains `MonsterSpawnPoint` and `Entrance_North/South/East/West`. Irregular quarter-wall plinths and boulders close the rim between the four gateways. The central floor is a broad shallow earth hollow; the existing footprint and ten-metre smooth entrance ramp collision remain.

The existing character capsule is height 5.5, radius 0.7, centreY 1.75, skin 0.08. Its bottom is `playerRootY − 1`. `PlayerSpawn_01` through `_04` retain rootY 1.23, leaving 0.08 m above the Y 0.15 plaza. Re-evaluate this offset for a different collider or scale.

Each empty `FutureStructureSlots/StructureSlot_XX` Transform reserves **14 × 14 m** inside a **26 × 26 m** scenery-protected region, leaving 6 m around the minimum footprint. The old round paved pads now blend into irregular meadow clearings. No runtime footprint lines or signs are present.

| Slot | Angle clockwise from north | CentreX | CentreZ |
| --- | ---: | ---: | ---: |
| 01 | 0° | 0.00 | 74.00 |
| 02 | 37.5° | 45.05 | 58.71 |
| 03 | 75° | 71.48 | 19.15 |
| 04 | 112.5° | 68.37 | −28.32 |
| 05 | 150° | 37.00 | −64.09 |
| 06 | 210° | −37.00 | −64.09 |
| 07 | 240° | −64.09 | −37.00 |
| 08 | 270° | −74.00 | 0.00 |
| 09 | 300° | −64.09 | 37.00 |
| 10 | 330° | −37.00 | 64.09 |

Exact unrounded bounds and 225 ground samples per footprint are in [Previews/ImprovementMeasurements.json](Previews/ImprovementMeasurements.json). All ten are flat at Y 0, do not overlap, and have no obstructing decoration triangles or physics obstacles. Large-body capsule queries also check circulation around each reserve.

## Water and scenery refinements

The four river/pond sections retain genuine holes in the walkable mesh and have no hidden walkable floor. The square-stepped banks were locally retopologized into curved, tapered shores while retaining outer island vertices and existing connections. The four original bridges and broad dry cardinal routes remain. Navigation also works with bridge sources excluded.

Patchy bank rocks, small bank plants, and lily pads replace the regular shoreline edging. Existing tree objects retain their identities with varied scale, rotation, and safe placement jitter. Eight small ruin fragments and spatially combined vegetation add detail outside protected routes and reserves. The main ring is still 10 m wide; its decorative paving is weathered and secondary path ends blend into the meadow. Paving adds no collision steps. The eight original cliff waterfalls remain.

Original kit sources are [Meshes/OpenArchive_Kit.blend](Meshes/OpenArchive_Kit.blend), [Meshes/OpenArchive_Kit.fbx](Meshes/OpenArchive_Kit.fbx), and [Meshes/OpenArchive_KitManifest.json](Meshes/OpenArchive_KitManifest.json). Metric meshes and reusable prefabs are under `Meshes/` and `Prefabs/{Terrain,Bridges,Nature,Architecture,Decorations}`. The latest procedural low-poly den modules are authored by `Editor/OpenArchiveDenRedesign.cs`; no Blender regeneration is required.

Archive trees reference the existing `Assets/CHEATCODE/SimpleNaturePack/Prefabs/Tree_01.prefab` through `Tree_05.prefab` meshes and atlas. Keep that vendor folder available. Vendor assets are referenced without modification. Blender regeneration is unnecessary for using or refining this scene.

## Android rendering preparation

The environment uses **five shared geometry materials**:

- `Archive_Atlas`: URP/Lit, a 512 × 512 color atlas and matching emission mask for the kit.
- `Archive_NatureAtlas`: URP/Lit, existing 1024 × 1024 tree atlas.
- `Archive_Meadow`: a local URP diffuse shader using vertex colors for ground and weathered stone, with shadow/depth passes and no added textures.
- `Archive_TurquoiseWater`: a local opaque URP shader using vertex depth tint, inexpensive sine ripples, and 1.8 cm motion. No scene-color, scene-depth, reflection, normal, or refraction textures are sampled.
- `Archive_Sea`: URP/Unlit; the separate sky remains Unity's procedural daylight skybox.

One directional daylight source uses soft shadows. The shared `Assets/Settings/UniversalRP.asset` now enables **Low** soft-shadow filtering; its existing **25 m** distance, **1024 px** main-light shadow map, and **single cascade** remain. This shared pipeline capability also applies to soft-shadow lights in other scenes. Lower tree LODs, cliffs, small vegetation, paths, and water avoid shadow casting; the major den rocks, pillars, and gateways cast shadows. No realtime lights or textures were added.

The Editor currently uses **Very Low** quality, which disables shadows. Review rendering temporarily enables shadows and restores that preference afterward. Use a shadow-enabled quality level to see soft shadows during local Play. Input, build settings, and quality levels were not changed.

Kit flames and glyphs are emissive geometry. Appropriate non-LOD geometry has static batching flags; shared materials enable instancing; trees retain LODGroups. Small plants are spatially combined and have no unnecessary colliders.

The current inventory is recorded in `ImprovementMeasurements.json`. Its **all-LOD triangle upper bound** sums every tree LOD simultaneously and is not a rendered-frame triangle count. Android frame time, memory, thermal behaviour, physical multitouch, and networked play remain unprofiled; no hardware performance guarantee is claimed.

## Validation and limits

[Previews/NavigationValidation.json](Previews/NavigationValidation.json) records **275 passing layout/navigation checks**, including independent four-gate approaches, cross-nest routes, full-width dry paths, a complete ring, ten destinations, all bridges, and alternative routes with bridges excluded.

| Local bake | Radius | Height | Climb | Slope | Voxel |
| --- | ---: | ---: | ---: | ---: | ---: |
| Player | 0.8 m | 5.5 m | 0.7 m | 40° | 0.15 m |
| Large body | 2.55 m | 5.5 m | 0.7 m | 40° | 0.15 m |

[Meshes/OpenArchive_PlayerNavMesh.asset](Meshes/OpenArchive_PlayerNavMesh.asset) loads through the saved scene's legacy navigation reference. [Meshes/OpenArchive_LargeMonsterNavMesh.asset](Meshes/OpenArchive_LargeMonsterNavMesh.asset) is retained for explicit future integration. Both currently use agentTypeID 0: Unity does not automatically choose a bake by a character's radius. Future AI integration must choose/register data or establish distinct agent types and rebake. No AI behaviour is supplied.

The validator queries temporary data away from other scenes and removes only registrations it owns. `navigationTriangles = −1` means global triangulation is intentionally omitted after a Unity 6 native failure during earlier authoring; independent samples and bounded complete routes verify the data instead. These bakes and coordinate measurements describe the source scene at unit scale. The existing 2.5× exploration copy uses the original CharacterController directly; rebake for that world scale if future navigation agents are integrated there.

- [ImprovementMeasurements.json](Previews/ImprovementMeasurements.json): actual footprint bounds, flatness, geometry, circulation, across-width gate clearance, spawn spacing, supported URP shaders, and current inventory.
- [InPlacePreservation.json](Previews/InPlacePreservation.json): retained original objects and asset GUIDs. Earlier build/repeatability reports are historical.
- [ExplorationValidation.json](Previews/ExplorationValidation.json): 11 Editor Play-mode checks for visible grounded character, shared joystick movement/release, jumping, look, pause/resume, recovery, and camera setup.
- [OpenArchive_Layout.png](Previews/OpenArchive_Layout.png): updated top-down aerial.
- [OpenArchive_Footprints_Debug.png](Previews/OpenArchive_Footprints_Debug.png): ten reserved squares and four entry ramps.
- [OpenArchive_Aerial.png](Previews/OpenArchive_Aerial.png), [from spawn](Previews/OpenArchive_FromSpawn.png), [nest](Previews/OpenArchive_Nest.png), and [bridge](Previews/OpenArchive_Bridge.png): actual saved-scene geometry renders.

These geometry checks use capsules 5.5 m tall, with radii 0.8 m and 2.55 m. Future animated monster silhouettes, Android hardware performance/multitouch, and networked play still require integration testing. No topic buildings, monster models, AI, puzzles, networking, or combat logic were added by this refinement.

