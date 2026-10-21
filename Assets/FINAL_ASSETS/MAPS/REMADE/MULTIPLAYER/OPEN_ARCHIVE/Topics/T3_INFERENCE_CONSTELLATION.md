# T3 - Inference Constellation

Open `Topics/Testing/MultiplayerGameplay.unity`, enter Play Mode and visit T3. Finish the team's active topic before beginning another one. Use Hand / E at the existing animated Book to begin. Its closed, floating/open and page-turning presentation is retained.

Five challenges use the original Rules of Inference banks and examples. Each has two stages:

1. Read the numbered premise blanks. Take the physical word tablets and place them in the matching sockets. Fill every blank and choose **Check words**. Identical words are interchangeable.
2. The completed premises remain available in the reader. Read the ten curriculum Rule Crystals, carry one to the central altar and choose **Confirm rule**. Only a correct rule reveals the original conclusion. The next challenge begins at the Book.

The right-side objective shows the current stage, placement count/altar, interaction cue and shared clock. **Read** opens a larger scrollable premise and rule-reference panel. **Return item** appears there while carrying. A placed tablet or crystal can be picked up again. Replacing an occupied socket returns its former item to its stand. Incorrect submissions keep the physical objects, arrangement and question, and record one mistake for the authenticated submitting player through the existing monster awakening system. Focus colors never reveal correctness before submission.

One six-minute team clock starts at the first Book activation. It continues through both stages and all five challenges. Reading, incorrect answers and subsequent Book activations cannot extend it. Timeout adds one mistake for the current Book activator, releases carried items, and keeps completed challenges, the current stage and its placed items. A Book retry starts a fresh six-minute attempt at that unfinished stage. Finishing all five challenges freezes the remaining time and unlocks another topic. Captured/stunned players cannot manipulate the puzzle; their teammates remain able to help.

`ArchiveTopicSession` remains the sole gameplay and timer owner. T3 uses the existing Photon commands, authenticated sender, station revision, replay protection, room checkpoint, departure cleanup and master handoff. `ArchiveInferencePresentation` and `ArchiveInferenceUI` only display that state. There are no changes to solo question banks/controllers, player movement, health, barrier outcomes or the network protocol.

The existing `Archive_T03_Inference_Constellation.prefab` references five reusable props: `Archive_T3_WordTablet`, `Archive_T3_ConstellationStand`, `Archive_T3_PremiseSocket`, `Archive_T3_RuleCrystal` and `Archive_T3_RuleAltar`. Opaque URP materials show lit crystal facets, brass, stone and parchment. Carry/placement/outcome sounds, socket focus and bounded one-shot sparks provide feedback. Each feedback system has at most 24 particles; there are no continuous particle emitters or new transparent walls. The original Book collider/NavMeshObstacle is preserved. Props add no solid collision or navigation fences, and new inlays are vertically separated from the existing ground.

Opt-in Editor validation:

- `ArchiveInferenceChecks.Begin()` checks all five native two-stage challenges, UI, correction, movement restrictions and retries in both stages.
- `ArchiveTopicNetworkChecks.BeginInference()` uses an invisible private Photon room with an Editor host and two independent Realtime peers to check shared progress, late join, ownership, attributed mistakes, replay/forged snapshot rejection, live master handoff, departure, capture restrictions and completion.
- `ArchiveBookTimerChecks.Begin()`, `ArchiveTruthChecks.Begin()` and `ArchiveTopicSceneChecks.Validate()` check T1/T2 regressions and both monsters' existing navigation routes.

See `Testing/Reports/T3PolishPlayMode.json`, `T3SharedProgressPhoton.json` and `T3PolishCompletion.json` for results. Timeout checks age the authoritative deadline; they do not wait six real minutes. Headless remote peers verify transport and authority, not rendered remote Unity UI. Physical Android input/performance remains a device test.
