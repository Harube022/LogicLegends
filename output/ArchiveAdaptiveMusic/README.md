# Open Archive adaptive score and distance-aware labels

The existing ArchiveAudioLandscape owns the score. No additional gameplay state, RPC, mixer or match timer was introduced.

- Daylight: calm, curious piano and warm pads (0-2 completed topics).
- Afternoon: warmer strings and a gentle pulse (3-4 topics).
- Evening: restrained, uneasy harmony (5-6 topics).
- Night: sparse low pulse and suspense (7-10 topics).
- Hunt: urgent strings and percussion for the locally targeted player. It returns to the current world mood after pursuit ends.

Five original 80-second stereo loops share a 96 BPM motif and harmony. Circular wrapped note/reverb tails keep the loop boundary continuous. Composition source and provenance are beside this file. Unity imports them as stereo Vorbis streaming at 24 kHz; at most two score voices overlap during a crossfade. Existing Music volume controls apply. Capture/cinematic instructions duck the score; reconnect recovery fades it to silence. Existing item and monster SFX are retained on the SFX mixer.

The existing ArchiveWorldLabelLayout now discovers fixed and runtime-created world captions. Captions stay fully readable through 55 world units, then fade out by 85 units. Night fog softens only the distant fade band, preserving a normal reading distance. Fully faded renderers stop drawing. Text components and challenge objects remain active, so updates continue while distant. Screen-space HUD, challenge readers and portal rune effects are excluded. Auto-discovered captions preserve their owners' moving transforms. Disabling the presenter restores the visual properties it owns.

## Verification

`playmode-checks.json`: 19 passing isolated native Play Mode checks cover all five music moods, returning from hunt, two-voice limit, Music mixer routing, cinematic ducking, recovery silence, near/far text, fog fade, HUD exclusion, dynamically generated captions, moving labels and restoration on disable.

`production-day-smoke.json`, `production-night-smoke.json`, `production-hunt-smoke.json`: fresh offline MultiplayerGameplay Play Mode observations, including the actual phase owner and monster target. Item cue assignments and the tranquilizer shot are also recorded.

`night-gameplay-check.png`: actual game-view capture confirming the clean distant scenery and retained HUD.

`World-Moods-Preview.mp3`: short preview in Daylight / Afternoon / Evening / Night / Hunt order.

No APK, physical Android device, live Photon room or 2-4-player session was tested. Production gameplay, hunting rules, rewards and network authority were not changed. The saved scene only assigns the score clips and presentation settings.
