LOGIC LEGENDS — STANDALONE OPENING REVIEW

This setup renders the project's real Unity scene assets and existing character
animations. The storyboard images are composition references, never video frames.
No gameplay playback component, automatic trigger, Build Settings entry, account
write, reward event, or gameplay-scene save is added.

EDITABLE UNITY SETUP
Assets/Cinematics/OpeningReview/LogicLegends_Opening_Review.unity
Assets/Cinematics/OpeningReview/Editor/OpeningReview.cs
Assets/Cinematics/OpeningReview/OpeningBlend.shader
Assets/Cinematics/OpeningReview/OpeningPortal.shader

Source: the currently loaded LogicGarden scene, including its unsaved Sanctuary
setup. A save-as-copy snapshot is retained at:
Library/OpeningReview/Original_Unsaved_Backup.unity
The original scene had unsaved edits before this task. Do not reload or overwrite
it merely to open the review scene. Do not run Build again over the existing setup.

The isolated review scene contains cloned Sanctuary, garden and Study Library
visuals; all twelve prepared gameplay skin prefabs; the prepared Wizard prefab;
and the actual multiplayer Archive_Victory_Portal visual prefab. Gameplay scripts,
HUD, colliders, audio triggers and character controllers are removed from these
clones. The render camera uses layer 31 without changing the project's layer names
or the original scene objects. Lights, poses, cracks and portal formation are
evaluated deterministically in Edit Mode. Original clips/materials stay intact.

TIMELINE AND AUDIO
timeline.json: 11 storyboard scenes, including three inserts within Scene 8.
Duration: 134.133333 seconds; 4,024 frames at 30 fps; 1920 x 1080.
audio-edl.json: original audio source-to-sequence mappings, all at 1.0x.
caption-cues.json: phrase-level timing aligned by local ASR to existing narration.
source_transcripts*.json: original ASR output, source hashes and word timestamps.
fracture-control-points.json: reversible crack paths projected on Cylinder.106.
Narration_Edit.wav, Music_Ambience_SFX.wav: editable audio stems.
Review_Mix_PreMaster.wav, Review_Mix_Master.wav: mix and normalized mix.
Captions.ass, Fonts/: editable subtitle styling and font license.

All 20 supplied narration files are retained at their original playback speed.
Numbered filenames 5 and 9 are absent in the supplied folder; no spoken sentence
is missing from the storyboard coverage. Scenes 4, 7 and 10 have dramatic pauses.
Existing introduction music and project magic sounds are mixed below narration.
No new narration is generated. Direct audible audition is unavailable in this
tool session; uncertain ASR spans are recorded in edit-report.json for review.

RENDER / REVISE
1. Keep the user's LogicGarden scene open. Open the review scene additively.
2. Adjust timeline.json camera positions/times and review-only source if needed.
   prepare_edit.py rebuilds audio/timeline/captions from the supplied recordings.
3. In an Editor C# tool, invoke OpeningReview.Preview(seconds, name, 1280, 720)
   to write a PNG in this folder. This is an actual rendered frame.
4. Invoke OpeningReview.StartRender(1920,1080) in Edit Mode.
   OpeningReview.Status reports progress. OpeningReview.Finish() cancels safely.
   Do not edit C# or refresh scripts during a render; domain reload cancels it.
   Encoder threads and frame buffers are bounded to reduce memory use.
5. Once unity-render-status.json reports all 4,024 frames and an empty error,
   run finish_review.py with the Python recorded in tools.json. It burns in
   subtitles, masters audio, exports H.264/AAC and runs decode/integrity checks.
   Local media dependencies are under Library/CinematicTools/python.
6. Review the exported video and SRT. Subtitles are already burned in; avoid
   enabling the separate SRT simultaneously in a video player.

DELIVERY
../LogicLegends_Opening_Review.mp4
../LogicLegends_Opening_Review.srt
QA/: decoded full-resolution frames and phone-size contact sheets.
qa-report.json: output streams, frame count, decode, loudness and source hashes.

The original source assets, gameplay scenes, saved progress and rewards remain
outside this review setup. No Git commit, push or merge is performed.
