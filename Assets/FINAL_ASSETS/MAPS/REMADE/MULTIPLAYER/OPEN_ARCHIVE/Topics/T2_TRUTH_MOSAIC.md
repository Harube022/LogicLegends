# T2 - Truth Mosaic

Open `Topics/Testing/MultiplayerGameplay.unity`, enter Play Mode and visit T2. The whole team must finish any active topic before starting another.

The Book remains closed until its first Hand/E activation. During a table it opens, floats and turns its pages. Nine tables reuse the original solo truth-expression banks: three Easy, three Medium and three Hard. P and Q are fixed inputs in TT, TF, FT, FF order; players fill only the four OUTPUT sockets.

Take a green T/True tile or amber F/False tile from its reusable stand with Hand/E. Carry it to the intended row and use Hand/E again. Different players can contribute to the same table. Placing another tile replaces an output. Fill four rows and use **Check table** in the compact right-side card or Book reader. The card shows the expression, all four current outputs and shared remaining time; Read opens the larger reference table. Return tile is available in the reader while carrying.

Incorrect submissions preserve all outputs and their visual object identity, record one mistake for the authenticated submitting player, and use the existing monster awakening flow. Soft placement/carry sounds, outcome sounds, socket highlights and bounded one-shot energy sparks give feedback. Color never reveals whether a placed value is mathematically correct before submission.

One six-minute team deadline begins at the first Book activation and continues across all nine tables. Reading, wrong answers and later Book gates cannot extend it. Timeout records one mistake for the current Book activator and keeps all solved tables and the unfinished table's outputs. Read the Book to retry that unfinished expression with a fresh six-minute attempt. Completion freezes the remaining time and unlocks other topics. Capture/stun restrictions still reject puzzle actions without blocking teammates.

`ArchiveTopicSession` remains the only puzzle/timer owner. Existing Photon commands, sender validation, revisions and room checkpoints carry progress, answer receipts and the deadline. `ArchiveTruthPresentation` and `ArchiveTruthUI` only present that state. No solo controller, player controls, barrier logic or networking protocol is replaced.

Reusable assets are in `Art/Prefabs/Archive_T2_TruthTile.prefab`, `Archive_T2_RowSocket.prefab` and `Archive_T2_TileStand.prefab`. The existing `Archive_T02_Truth_Mosaic.prefab` references them. Its Book collision and NavMeshObstacle are preserved; the smaller T2 Book assembly is a visual override. New floor/table pieces are separated in height and add no colliders or navigation fences. T1 and the other eight topic kits retain their art.

Editor checks: `ArchiveTruthChecks.Begin()` runs the nine native T2 tables and timeout/retention checks; `ArchiveBookTimerChecks.Begin()` checks T1; `ArchiveTopicNetworkChecks.Begin()` uses a private Photon room and two independent Realtime peers. See `Testing/Reports/T2PolishPlayMode.json`, `T1BookTimer.json`, `SharedProgressPhoton.json` and `T2PolishCompletion.json` for the current results and scope. Timeout checks use aged deadlines; these do not imply a six-minute wall-clock wait, rendered remote Unity clients or physical Android testing.
