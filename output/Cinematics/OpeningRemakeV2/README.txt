LOGIC LEGENDS - MOVIE_002 STORY REMAKE

Visual reference: Assets/Assets/Video_Cutscene/Movie_002.mp4.
The previous Beta_Introo reference has been superseded by the user's correction.

Delivery:
  ../LogicLegends_Opening_Story_Remake_v4.mp4
  ../LogicLegends_Opening_Story_Remake_v4.srt

V4 - PILLAR SYMBOL RINGS
All ten original circular math-symbol meshes now rotate in their own planes.
Their imported origins are offset, so each ring rotates around its geometric
center. Its center and the Pillar body stay stationary. Angular speed eases
down during the 0.9 seconds preceding the first crack and reaches zero exactly
at the firstCrack narration cue (54.681333 seconds). Every later Pillar shot
retains that same stopped pose. Absolute film time supports repeatable seeks
and two-camera crossfades without accumulated rotation or restarting.

Pillar-symbol-ring-rest.json holds canonical original transforms. Native
verification in pillar-orbit-verification.json checks rotation before damage,
unchanged angles after damage through 105 seconds, stable centers, reproducible
seeking, and a stationary Pillar body. Rebuild this section with:
  OpeningRemakeV2.StartRender(1920,1080,"UnityPillarOrbitFootage.mp4",3345,952)
Then run complete_pillar.py. V4 combines the retained 3D opening (frames 0-951),
the new Pillar footage (frames 952-3344, including the outgoing crossfade), and
the retained arrival/ending footage (global frames 3345-4023). Original voice,
subtitle timings, failed papers and student summoning design are preserved.
Inspect EditSource/QA-v4 and Verification-v4.json for delivery evidence.

V3 - REAL THREE-DIMENSIONAL LOGIC REALM
The flat LOGICREALM3 map sprite has been retired in this review scene. Its
Gemini mark cannot appear in the new opening because the original map texture
is no longer rendered. The original source image is preserved.

The new map uses raised terrain, coastline cliffs, mountain ridges, forests,
animated ocean geometry, and a miniature of the actual Pillar crystal mesh.
The camera approaches at an oblique angle, giving real perspective and parallax.
Logic Garden and Pillar labels identify the same story locations. Meshes and
URP shaders are reusable review assets under Assets/Cinematics/OpeningRemakeV2.
The terrain uses a shared grid to eliminate mismatched mountain edge heights.

Render the opening with:
  OpeningRemakeV2.StartRender(1920,1080,"UnityRealm3DFootage.mp4",1020,0)
Then run complete_realm.py. It waits for a complete error-free render, preserves
the render status, and assembles V3 from these clean, subtitle-free sources:
  UnityRealm3DFootage.mp4: global frames 0-1019
  UnityFootage.mp4: global frames 1020-2165
  UnityPolishedFootage.mp4: global frames 2166-4023
The original narration mix and the same 36 timed captions are applied once.
Delivery is 1920x1080, constant 30 fps, 4024 frames, 134.133 seconds.

The new terrain is confined to the review scene. Production gameplay scenes,
source character/animation assets, the original movie, and VideoPlayer routing
are preserved. Inspect EditSource/QA-v3, Verification-v3.json and
realm-geometry-verification.json for current evidence. Previous V1/V2 exports
remain available. The following notes describe the retained V2 improvements.

Latest revision: grounded Wizard walking, original failed-exam papers and richer
student summoning circles. The previous Story_Remake export remains available.
The Wizard now uses his own distance-matched stride without the student speed
bias, eased acceleration, gradual turns and a blended walk-to-greeting handoff.
His neutral standing pose uses the existing Wizard_Idle1 clip. Existing walk,
casting and greeting clips are reused; their source assets are unchanged.
Three papers are cloned from the original CINEMATIC/Tests.fbx asset. Two close
shots clearly show the red F grade. Books are staged beside the hero paper.
Six turquoise/gold floor seals have layered contours, vector math inscriptions,
counter-rotating geometric cores, rising sparks and soft local illumination.

For this revision, render frames 2166 through 4023 using:
  OpeningRemakeV2.StartRender(1920,1080,"UnityPolishedFootage.mp4",4024,2166)
Then run finish_story_remake.py polish after rendering is false with no error.
The first 2166 frames use the previously verified smoke/map/Pillar footage;
the new section replaces the classroom, summoning and arrival scenes. The final
encode checks that the new segment contains exactly 1858 frames and that the
assembled film contains exactly 4024 frames. Review evidence is in QA-v2 and
Verification-v2.json. Timeline staging is kept in EditSource/timeline.json.

The MP4 includes subtitles already. The separate SRT is for future editing;
loading it over this MP4 would display captions twice.

This version restores the smoke and floating-symbol opening, the original
Logic Realm map, the approach to the Pillar, the real-world classroom cutaways,
the summoning and the Logic Garden arrival. It uses current Unity environments,
student models and the current Lefyajh design. It keeps the user's 20 original
voice recordings at 1.0x, retaining the full 2:14 narration edit. The reference
Movie_002 supplies the visual sequence, rather than the new narration duration.

The Guardian now walks to his casting mark, animates his hands/arms during the
summoning, opens an animated portal and welcomes the students. Four students
walk out in a staggered arrival, then step into place. Classroom classmates
use current rigs and idle clips; the summoning seals appear below their feet.

Rebuild sources:
  Assets/Cinematics/OpeningRemakeV2/LogicLegends_Opening_Remake.unity
  Assets/Cinematics/OpeningRemakeV2/Editor/OpeningRemakeV2.cs
  Assets/Cinematics/OpeningRemakeV2/Editor/OpeningRemakeV2.Story.cs
  EditSource/timeline.json (final camera staging; preserve this file)
  EditSource/story-rest-state.json (canonical smoke and symbol transforms)

The preparation Python bootstraps missing source files and copies the original
audio; it preserves the final renderer, finishing script and timeline when
they already exist. The Story.cs
partial file contains the finished staging. DirectX 11 is recommended for the
offline Unity render. No production graphics or gameplay settings changed.

Render with OpeningRemakeV2.StartRender(1920,1080) in Edit Mode. Wait for frame
4024, rendering false and no error, then run finish_story_remake.py all.
Review the QA images and original-voice verification before distribution.

The delivered version includes a refined smoke reveal. The live cloud particles
fade as the camera approaches the map, so the map remains readable in sequential
renders. Its first 1,020 frames were rendered to UnityOpeningRefined.mp4; the
remaining frames use the completed 4,024-frame UnityFootage.mp4. The finishing
script joins these at exactly 34 seconds before applying subtitles and audio.
The full baseline render status is preserved in unity-full-render-status.json.
For another partial refinement, use StartRender(1920,1080,"UnityOpeningRefined.mp4",1020).

The casting shot also has an unobstructed camera refinement. Frames 3096 through
3272 (103.2 to 109.1 seconds) use UnityCastingRefined.mp4, then the original
arrival shot resumes. StartRender(1920,1080,"UnityCastingRefined.mp4",3273,3096)
rebuilds this section. Preserve unity-opening-render-status.json before starting
it; the finishing script validates the opening, full baseline and casting status.
All joined frames are given exact 30 fps timestamps before subtitle/audio encode.

NEW-USER GAME INTEGRATION
The verified V4 file is now bundled as:
  Assets/Assets/Video_Cutscene/LogicLegends_Opening_V4.mp4
The existing AuthManager intro VideoPlayer in Assets/Scenes/Main Menu.unity
references that imported clip. Only its clip reference changed; the original
Beta_Introo.mp4 and Movie_002.mp4 are preserved.

The existing SelectBaseCharacter -> SubmitUsername -> PlayIntroCutscene flow
remains authoritative. Returning users with a base character and username keep
their existing direct route to mode selection. Skip, finish, menu audio and
loading handlers are unchanged. The new file includes the original narration
and burned-in subtitles, so no subtitle overlay is required in the game.

Integration evidence, the exact one-line scene diff, source-copy hash and
isolated native VideoPlayer playback evidence are in EditSource/NewUserIntegration.
No real account was created or changed for this verification. New builds will
include the updated video; previously installed APKs are not modified by this
project change. Earlier review exports remain available.
