LOGIC LEGENDS - REMASTERED OPENING

Delivery:
  ../LogicLegends_Opening_Remastered.mp4 (1920 x 1080, 30 fps)
  ../LogicLegends_Opening_Remastered.srt (optional separate subtitle track)

The MP4 already includes subtitles. Do not enable the SRT on top of it, or
captions will appear twice. The SRT is provided for future edits/accessibility.

The user's 20 original voice recordings and their 1.0x playback timing are
preserved. The existing mastered narration, music and effects WAV is reused.
No replacement voice or generated actors were used. Source integrity checks,
caption timings, render logs and verification are in EditSource/.

Story sequence:
1. Invitation and continuous approach into the Logic Realm.
2. Truth, reasoning and structure around the Pillar of Logic.
3. The Pillar and the balance of knowledge.
4. Close approach followed by a fade to black.
5. First cracks and flickering light.
6. The Pillar falters and the realm dims.
7. Deeper fractures.
8. The sanctuary, Logic Garden and library begin to fade.
9. The Logic Guardian summons chosen minds through the portal.
10. Four current player characters arrive in the Logic Garden.
11. Guardian and companions; closing showcase of all 12 current skins.

Sources:
  Assets/Cinematics/OpeningRemake/LogicLegends_Opening_Remake.unity
  Assets/Cinematics/OpeningRemake/Editor/OpeningRemake.cs
  EditSource/timeline.json

Current LogicGarden scenery and the latest character prefabs are freshly
cloned into the isolated film scene. The existing sanctuary is reused from
the previous cinematic authoring scene. Gameplay source scenes are preserved.

Re-render (Edit Mode):
  Open the saved remake scene and run OpeningRemake.StartRender(1920,1080)
  through the Unity Editor tooling. Wait until OpeningRemake.Status reports
  frame 4024, rendering false, and an empty error field.
  Then run finish_remake.py prepare, encode, and inspect using Python.
  Review the QA contact sheets and boundary frames before approval.

DirectX 11 was used after a DirectX 12 device crash during rendering.
This is an Editor launch option; no production graphics/build settings changed.

The original LogicLegends_Opening_Review.mp4 remains intact.
The new export has not been assigned to the new-user VideoPlayer automatically.
