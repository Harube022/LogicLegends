# Khezu cinematic first-person jumpscare

Open `OpenArchive_KhezuCinematic_Test.unity` in this folder. This is a saved copy of the hunting test, with the existing environment, player controls, NavMesh, Khezu prefab, and monster scale preserved. The original Phase 1/2 scenes remain available.

## Try it

1. Press **Play** and select **PREVIEW POV (OFFLINE)** at the bottom of the screen for an immediate 2-second preview. This preview does not confirm a capture, activate a barrier, change health, or alter hunting rotation.
2. Toggle **REDUCED MOTION** to remove camera shake and the rapid camera lunge. The same original animations and timing remain visible.
3. For the actual catch flow, use the existing **Force 4 Mistakes** button. Wait for awakening and let Khezu catch the local explorer. Only the captured actor receives the POV.
4. The **BARRIER HANDOFF** test panel appears after the cinematic. **RESET TEST ENCOUNTER** releases this test. **Reset Hunt** or **Return to Sleep** also cancels an in-progress encounter and restores controls.

Normal controls stay the same: WASD/joystick, right-side drag to look, Space/jump button, and R/return to spawn. Input and camera look are suspended only during the local cinematic. Resetting the test does not report a puzzle success or failure.

For a future live multiplayer check, open Main Menu in Play Mode, open Multiplayer, then select **Tools > Logic Legends > Khezu Cinematic > Use Existing Lobby For Next Hosted Room** before hosting. Join/ready/start through the existing lobby. This only changes the next lobby's target scene for this session; the production menu default is preserved. The immediate preview button is disabled in a Photon room; use a real host-confirmed catch there.

The user confirmed Phase 3 is not implemented yet. There is no mathematical challenge, physical barrier, damage, or challenge outcome implementation here. The clearly labeled handoff panel verifies sequencing only.

## Runtime integration

Reusable scripts are in `../../Cinematics/`, separate from test UI and verification scripts:

- `KhezuCinematicJumpscareController`: a local-only existing-prefab visual copy, manual PlayableGraph with the three original clips, authored Cinemachine 3.1.5 POV, bounded audio, cleanup and accessibility settings.
- `KhezuCaptureEncounterController`: consumes the existing Phase 2 host-confirmed `Caught` state and capture serial. The host drives world animation and the encounter phase. Local presentation cannot confirm a capture or determine an outcome.

The Phase 2 snapshot now includes `captureStartedAt` (Photon room clock) and `encounterPhase`. Catch serial and actor ID remain the idempotency key. Existing Photon events and host validation are reused; no new networking framework or network prefab was introduced. Caught snapshots update at 5 Hz, with reliable phase transitions/checkpoints.

`Hunting -> Capture -> CinematicJumpscare -> BarrierCast -> BarrierChallenge`

The host starts world **Capture** at capture time, **Jumpscare** at 0.06 seconds and **Barrier_Cast** at 1.6 seconds. At 2 seconds, the host confirms the handoff. World clips and local presentation share a nonuniform beat mapping: the preparation is compressed to 60 ms and the mouth lunge takes the next 100 ms. Camera, roar marker and handoff follow that mapping. Other clients continue receiving the original world monster animation through the existing snapshot sync. The placed monster and player are never moved for cinematic framing.

The caught client's presentation uses these timeline markers:

| Time | Presentation |
| --- | --- |
| 0–0.06 s | Input lock, 35 ms dissolve to POV, rapid Capture, capture impact and brief jolt; short breath/growl accents at 0.025/0.045 s |
| 0.06–0.16 s | Fast forward thrust of the local visual model, original Jumpscare clip rapidly opens the mouth |
| 0.16–0.5 s | Wide-mouth hit, synchronized roar/stinger and recoil; preparation sound layers fade out over 25 ms |
| 0.5–1.6 s | Impact accent, settling reaction, fading shake and transparent-center edge vignette |
| 1.6–2.0 s | Original Barrier_Cast, pullback, magic cue, cross-dissolve back to gameplay |

The local model thrusts 0.42 m toward the POV during the 100 ms attack. Its existing rig adds the head's animated forward motion. The camera keeps its depth anchored during the attack so following the head does not cancel the apparent lunge. A short recoil follows the hit. This replaces the former slow preparation before the 0.789-second hit; overall duration remains 2 seconds. Reduced Motion removes the added model translation and camera shake and keeps stable face-relative framing.

Movement tuning (2026-10-18): Khezu's close pursuit now holds **20.28 m/s**, only 4% above the player's 19.5 m/s run speed. It smoothly increases toward the existing **26.68575 m/s** maximum when the body-to-capsule gap grows from 8 to 35 m. Arrival auto-braking is disabled, and nearby paths refresh at 0.1 s rather than 0.25 s, so he does not stall at the player's previous position. Both saved hunting and cinematic tests use these settings. Acceleration, turning, player speed, catch reach, wall checks and NavMesh remain unchanged. Chase animation speed matching follows the actual movement. See the hunting test's `Reports/PursuitBaseline.json` and `Reports/PursuitMovingTarget.json` for the moving-runner comparison.

The cinematic remains 2 seconds. Shake combines bounded translation with pitch/yaw/roll recoil and a decaying settling wobble at capture, lunge and impact; it ends before the barrier transition. The reduced-motion option disables both angular and positional shake.

The local visual rig has no PhotonView or active colliders. It uses transient layer 31 at a distant local staging position, a private Cinemachine channel (15), and a capped 1280×720 render target. The gameplay camera remains in its original world position. Its original Cinemachine channel mask is restored after completion/cancellation. No extra AudioListener, post-processing volume, blur, flashing, or custom full-screen shader is added. Render target size and framing adapt to screen aspect/orientation. One temporary skinned mesh with the original two materials is rendered during the sequence.

The original skeleton and Capture/Jumpscare/Barrier_Cast assets are unchanged. Local retiming reaches the original Jumpscare clip's wide-mouth section at the explicit roar marker; it does not generate or replace animation assets.

## Future Phase 3 connection

On `KhezuCaptureEncounterController`:

- **Host Barrier Activated(actorId, captureSerial)**: host-only notification after 2 seconds. Connect the authoritative barrier service here, keyed by both values. A checkpoint that already confirms BarrierChallenge does not create a second barrier.
- **Local Challenge Requested(actorId, captureSerial)**: caught client only, after its cinematic has restored controls/camera. Replace the test-panel listener with the real mathematical challenge UI. Phase 3 should acquire its own input lock if required.
- **Local Encounter Ended**: close/cancel the local challenge UI on host reset or disconnect.
- **ReleaseAfterHostChallenge(actorId, captureSerial)**: callable by a host-owned outcome service. It rejects a mismatched actor/serial and releases the encounter without deciding an outcome, applying damage, or clearing hunting history.

The adapter prevents Start Hunting from bypassing an active captured encounter. Explicit test resets still work. Late checkpoints resume the remaining presentation, skip expired sound cues, and never replay a completed scare. Disconnect callbacks fence off a stale encounter and dispose the local presentation. The host releases a captured actor who disconnects. Future persistent barrier services should keep the actor/serial key idempotent across reconnects and host migration.

## Audio and Inspector tuning

Six dedicated effects are now assigned from `../../Cinematics/Audio/`: `Khezu_Capture_Impact`, `Khezu_Low_Breathing`, `Khezu_Tense_Growl`, `Khezu_Maw_Roar_Stinger`, `Khezu_Impact_Accent` and `Khezu_Barrier_Pulse`. No audio assignment is missing. The roar combines a low vocal rasp, an inharmonic falling stinger and electrical/alien texture; the quiet breathing and rising growl create contrast before the lunge.

The external audio generator was unconfigured. These effects were rendered offline from the existing Small Sound Kit plus original vocal/noise/sub-impact synthesis. Source files remain unchanged. `Authoring/build_khezu_audio.py` (NumPy) reproduces the WAVs and records source provenance, peaks and RMS in `Reports/AudioAssetValidation.json`. `Reports/Khezu_TwoSecond_AudioPreview.wav` is a two-second audition reference; actual game scheduling is frame-based. The effects are preloaded mono 44.1 kHz PCM16, use about 190 KB of sample data combined, and retain their authored levels without import normalization. All six files and the reference mix have zero clipped samples.

Adjust `Shake Intensity` (default 0.85), `Shake Translation` (0.09 m maximum), `Shake Rotation Degrees` (2.4° maximum), `Reduced Motion`, `Framing Distance`, `Master Volume` (0.65), individual cue levels and the optional AudioMixerGroup on the cinematic component. These shake maxima are scaled by intensity. Voices use an 8 ms gain attack, a combined gain budget limited by Master Volume and an 80 ms exit fade. With no override mixer, transient voices route through the existing SoundFX channel, respecting its volume/mute preference. Device speaker/headphone mixing remains unverified.

## Verification artifacts

`Reports/CinematicPlayMode.json` records the actual offline awakening/hunt/catch sequence, preview button, increased measured chase speed, 2-second duration, actual forward movement of the model/face during the 100 ms attack, anchored camera depth, roar playback and SFX routing, combined audio gain, bounded positional/angular shake, actor gate, duplicate rejection, world animations, handoff timing, unchanged authoritative transforms and restored controls. `Reports/CinematicLifecycle.json` records offline snapshot fixtures for late checkpoints, observer presentation gate, resets, disconnect callbacks and preconfirmed handoff deduplication. `Reports/ScreenFraming.txt` records 507 samples across the complete shake-enabled timeline at 16:9, 20:9 and 9:16, including the nearest baked mesh vertex depth and mouth edge margin. `Fast_Windup_End.png` and `Fast_Lunge_Peak.png` show the attack's start and peak. `Maw_*.png` are rendered framing previews, and `Phase3_Handoff.png` shows the test handoff.

The editor-only check components are never attached to the saved scene and are excluded from builds. Original assets and production player/puzzle/health scripts are checked against the existing 718-file protection manifest.

Live multi-client Photon play, an actual host switch/disconnect, Android build/device performance, speaker/headphone mixing and the future Phase 3 barrier/challenge remain unverified. Actor-gate, checkpoint and callback fixtures do not replace those tests.
