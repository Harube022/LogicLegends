# Logic Legends — The Open Archive

57-second, 1920 × 1080, 30 fps multiplayer story cinematic. English story captions, an original instrumental score and Open Archive game sound effects. No voice-over.

## Approved story

The Pillar of Logic has been restored, but the damage severed its connection to the Open Archive. Lefyahj sends four chosen minds to recover the knowledge stored there. Solving ten topic challenges restores the knowledge crystals; contributions from different teammates make the restoration whole. The Archive's guardians, The Primal Axiom and The Proof Devourer, became hostile after its corruption. The team must restore the Archive and bring every surviving teammate home through the return portal.

This continuation was approved in this chat. It is the multiplayer story; it does not add a Solo-completion prerequisite to entering multiplayer.

## Delivery and integration

- Review film: `LogicLegends_OpenArchive_Story_1080p.mp4` beside this file.
- Production copy: `Assets/Assets/Video_Cutscene/LogicLegends_OpenArchive_Story.mp4`.
- Existing Main Menu `MultiplayerMenuManager.multiplayerStory` references the production clip. Main Menu scene change is one serialized field.
- First tap on Multiplayer plays the film before opening the existing multiplayer menu. End or Skip saves a local, account-specific watched flag. Returning entry opens the menu immediately.
- Friend-invite continuation waits until that same entry flow finishes, preserving the room code.
- A missing clip, decoder error or 15-second prepare timeout opens the existing menu without marking the film watched. Repeated taps cannot stack films or invoke duplicate callbacks.
- Background menu music pauses and resumes. Film audio uses the existing Music mixer and volume setting.
- Watched status is stored in PlayerPrefs (`LogicLegends.OpenArchive.IntroSeen.v1.<uid>`), scoped to this installation and account. It is not a cloud profile write.

## Production safety

The isolated authoring scene clones the current environment and existing characters/monster clips. Runtime gameplay, input, physics and networking components are removed from those clones. The production MultiplayerGameplay scene, topic state, hunting, rewards and networking were not edited for the film. No APK was built and no physical-device or live 2–4-player test was performed.

## Reproduction

1. Use the current Unity Editor in Edit Mode with the production MultiplayerGameplay scene saved.
2. `ArchiveTeaser.Build()` creates the separate `Assets/Cinematics/OpenArchiveTeaser/OpenArchive_Teaser.unity` authoring scene.
3. `ArchiveTeaser.Preview(seconds, name, width, height)` produces a still for framing checks.
4. `ArchiveTeaser.StartRender(1920,1080,"Archive_Unity_Footage.mp4")` renders the 12-shot, 57-second timeline to `EditSource`. Progress is in `unity-render-status.json`. Do not recompile scripts during rendering.
5. Run `EditSource/finish_teaser.py --encode` with the bundled Python runtime. It composes the original score, layers project SFX, creates ASS/SRT captions and encodes the final MP4 using the configured FFmpeg.
6. Copy the approved final MP4 to the production clip path, keeping its `.meta` GUID.
7. `ArchiveIntroPlaybackChecks.Run()` executes isolated native Play Mode checks with temporary synthetic identities, then restores the original scene. It does not sign in or join a Photon room.

The frame/SFX timings, score generation, provenance and validation evidence are retained in `EditSource`. The final captioned film contains all title cards; the `.srt` is an optional separate caption transcript.
