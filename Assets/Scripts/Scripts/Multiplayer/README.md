# Private multiplayer lobbies

Open **Play > Multiplayer > Host Game / Join Game** in `Assets/Scenes/Main Menu.unity`.

The saved scene already contains and references the UI. No Photon dashboard changes, additional backend, paid service, or artwork import is required. PUN 2.52 and the existing fixed `asia` region are reused.

## Open Archive lobby destination — 2026-10-21

The saved Mode Selection > Multiplayer > Host / Join > Ready > Start flow now opens the completed `Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Topics/Testing/MultiplayerGameplay.unity`. The host stores that full path in the room, resolves it to its enabled build index, and calls the existing PUN synchronized loader. An index avoids both ambiguous duplicate scene names and repeated guest loads when PUN compares the synchronized scene with the active scene.

The existing spawner creates one owned avatar per client in four separate, grounded slots on the Archive spawn plaza. Online exploration binds that avatar without resetting its initial position to the shared spawn center; direct offline testing retains its explorer and Return to Spawn behavior. The existing ten topics, shared progress, monster encounter, learning interface and environment are reused. The startup fade now waits for the network avatar before revealing the scene.

The Archive has the existing party-chat panel, with a compact CHAT button beside Settings and clear of the test tools. It retains lobby history and restores local controls on close. `MultiplayerStage` remains a legacy test scene. Use **Logic Legends > Multiplayer > Connect Lobby To Open Archive** to restore the route, build registration and scene-owned chat without rebuilding the environment. The existing UI rebuild commands now target the Archive chat.

Protocol **4** requires all players to use the updated project/build; earlier builds do not contain this destination or the completed encounter content. Rebuild and install the same version on each device before multiplayer testing.

Verification: 57 live start/chat checks passed with one rendered PUN client and three independent Realtime clients; 11 additional checks exercised the actual Join submission and automatic scene load as a rendered guest, including checkpoint restoration and no reload after room-property updates. Nine direct offline regression checks passed. The saved Main Menu changes only its multiplayer destination; the saved Archive adds only party-chat UI. No Unity errors occurred. One PUN serialization warning occurred during guest-room teardown. Physical Android, the native keyboard, and simultaneous rendering on separate devices remain unverified. The repeatable checks are **Run Start And Chat Checks (Play Mode)** and **Run Open Archive Guest Checks (Play Mode)** under the Multiplayer editor menu.

## Behavior

- Hosts create unlisted, open rooms with six-character cryptographic invite codes and four total slots. The alphabet excludes I, O, 0 and 1. Room-name collisions retry up to five creation attempts. Codes are shown only after joining succeeds; Copy uses the system clipboard.
- Join trims and uppercases the code, validates its alphabet/length, and calls `JoinRoom` rather than creating a room. Submissions are disabled during a request. Full, closed, nonexistent, incompatible protocol and connection failures have readable messages.
- `LL-private-lobby-4` partitions the Photon application version, with PUN's version suffix, so older clients cannot join rooms whose Open Archive content they do not have. A room protocol property additionally verifies compatibility. Photon cannot distinguish a nonexistent code from a code in another application-version partition; that error explicitly mentions code, version and region.
- A signed-in player chooses a 3–20-text-element username in the game after selecting a base character. Letters from any language, decimal digits, and `_` are accepted; spaces and other symbols are not. The prompt keeps these rules visible and validates as the player types. It blocks common abusive English and Filipino terms, including simple number and accent substitutions. Returning Google or email accounts with a character but no valid username see the same prompt before entering the menu. The name is saved at `users/{uid}/username`; the Google account name is never used as the Photon nickname. The lobby, chat, and MultiplayerStage avatar nameplates use that username, with an Adventurer fallback if a profile is unavailable. Actor numbers, not names, identify roster/readiness/chat senders. Duplicate usernames are currently allowed. The client filter cannot guarantee moderation of every inappropriate name or direct database writes; production enforcement belongs in trusted server-side account validation.
- All players initially join Not Ready. Ready is a Photon player property. Only the current Master Client can start, with 2–4 present players all ready. Starting closes the room, uses conditional waiting/locking/loading properties, waits for server state and rechecks readiness/membership before `PhotonNetwork.LoadLevel`. Membership changes before loading cancel the start. Full rooms never start automatically.
- The destination is configurable on the main-menu NetworkManager's `MultiplayerMenuManager`; the completed **Open Archive MultiplayerGameplay** scene is the default. Lobby routing reuses its existing puzzle, encounter and completion systems.
- Room chat uses reliable Photon room event 74. The shared English/Filipino filter masks common abusive words before sending and again before adding received messages to history; both the waiting lobby and MultiplayerStage chat use this service. Empty and over-200-character messages are rejected, sending has a one-second cooldown, receiving applies a per-actor rate limit, and visible history is capped at 60 entries. TextMeshPro rich text is disabled for history, names and input. Join, leave and host-change messages are local system entries. No chat is written to Firebase; late joiners start with fresh local history. This client-side filter is not comprehensive moderation of messages sent by modified clients.
- Leaving clears room/chat state. Waiting hosts can leave and Photon selects a new Master Client. Unexpected online-session disconnection gives a retry path and returns from gameplay to the main menu. Gameplay recovery/migration is outside this feature. Idle menu connections are disconnected on solo entry and cannot redirect a solo stage later.

## Files and ownership

| File | Role |
| --- | --- |
| `Assets/Scenes/Main Menu.unity` | Saved lobby hierarchy and component references; existing Play/Multiplayer callback reused. |
| `MultiplayerMenuManager.cs` | Thin existing menu entry point and configurable scene destination; legacy solo callback retained. |
| `MultiplayerLobbyService.cs` | One persistent room/network owner, profile lookup, readiness, lifecycle and bounded chat. |
| `LobbyRules.cs` | Shared invite-code rules, room protocol, readiness checks and error text. |
| `MultiplayerLobbyUI.cs` | Host/Join/code/waiting panels and editor-buildable layout. |
| `UsernameSetupUI.cs` and `UsernameSetupBuilder.cs` | Artstyle-matched GraphHUD prompt saved in Main Menu; connected to AuthManager and the existing website email registration path. |
| `PlayerUsername.cs` and `MultiplayerAvatarNameplate.cs` | Username validation/session cache and multiplayer-only overhead names. |
| `InappropriateTextFilter.cs` | Shared blocked terms, normalization, and in-place chat censorship. |
| `RoomChatPanel.cs` | Reusable chat view with input isolation and subscription cleanup. |
| `Assets/Editor/MultiplayerLobbySetup.cs` | Rebuilds only the lobby in the open Main Menu scene. |
| `Assets/Editor/MultiplayerLobbyChecks.cs` | Opt-in live multi-client lobby verification. |
| `Assets/Editor/MultiplayerStartChecks.cs` | Opt-in live chat-history, host authority and scene-start verification. |

The service persists after scene loading, retaining bounded plain-text chat. `ChatMessages` provides actor IDs and sender names for the card view; `Messages` remains a plain transcript. Both views use the same PUN room transport, length limit and cooldown.

Open Archive has a scene-owned `MultiplayerChatHUD` directly under its existing Canvas 1, with only the edge button under SafeArea. CHAT sits beside Settings on the left, clear of the right-side look/jump/interact controls and test drawer. Tapping it opens a conversation panel and clears the unread badge; Close, Escape or the dimmed world closes it. Movement and camera input are suspended locally while chat is open, without pausing the multiplayer simulation. The touch HUD is hidden while expanded and restored on close, keeping narrow tablet layouts clear. Settings closes chat before taking its own input block. Leaving removes the HUD and clears chat; solo scenes never create it. The composer accounts for the native keyboard area where the device reports it.

Rebuild the lobby and stage chat with **Logic Legends > Multiplayer > Build Lobby And In-Game Chat UI**. This updates only those UI hierarchies and preserves the environment and prefab connections.

## Artwork

The lobby retains its original outer wood frame and uses the shared `Assets/Art/GraphHUD/GraphMapFrame.png` for Host/Join choices, the invite-code form, party list and chat. `GraphStatusFrame.png` supplies the input fields. These are clipped wooden frames with cream interiors and dark ink, without rolled scrolls or a dark teal chat pane. Existing cropped sprites and nine-slice borders are reused; the panel multiplier is the cropped short side times .01. Full-color artwork stays white-tinted. Lobby actions use fixed 280x88 dimensions (220x88 for supporting Back/Copy/Send actions), matching Host/Join geometry and button Image multiplier 4.5. WaitingLobby uses the former back-button space to make its roster/chat about 30% taller, with a wider chat and more visible conversation space. IMPACT headings and Liberation Sans messages/input are assigned explicitly, and message cards are editable UI elements. The existing safe areas and connected gameplay controls remain intact.

## Verification — 2026-10-12

Performed in Unity 6000.3.6f1 with the real PUN cloud settings, one actual game client and up to four independent Realtime clients in temporary private rooms:

- Two- and four-player lobbies, distinct actor tracking with duplicate names, initial Not Ready, all-ready gating and explicit starting.
- Invalid/ambiguous codes, normalized code joining through the actual service, nonexistent room, fifth-player rejection, closed room and incompatible room protocol.
- Host-only controls, guest start rejection, host-role changes, guest departure, host departure and migration to the local guest.
- Chat delivery in both directions, whitespace/length rejection, cooldown, plain-text markup, input blocking and 62 live messages enforcing the 60-entry history cap.
- Readiness changes and guest departure during the start transition cancel and reopen the lobby. Duplicate Start presses produce one session.
- Host loads Stage 1 Multiplayer; the independent remote client receives the matching PUN `curScn` property. The room remains closed and marked loading. Chat survives the load and gameplay input is restored.
- Leave/reopen clears players, readiness, history and callbacks; one service remains. Disconnection cleanup and retry path were exercised.
- Clipboard code copy and leaving while chat is focused restore input correctly.
- Existing Solo button opens stage selection and enters PRELIM outside a Photon room. After its existing tutorial closes, the local player is enabled and input is unblocked. An idle online connection disconnects without redirecting the solo stage.
- Play Mode visual inspection at 1920×1080, 3040×1440 and 800×480 landscape. Existing wood/parchment/button artwork, label fit, disabled controls and usable content margins inspected. No original Main Menu objects/components were removed; its existing changes are limited to the NetworkManager and canvas child list.

Re-run from **Logic Legends > Multiplayer**. Live checks require Play Mode in Main Menu; the build command requires Edit Mode in Main Menu. Logs/screenshots are under ignored `Temp/MultiplayerQA`.

The start checks leave their temporary room after success and return to Main Menu. Stopping Play Mode directly inside the multiplayer stage exposed a PUN `PhotonView` cleanup exception during editor teardown; normal room exit is checked separately. Photon package source was not changed.

Remaining device checks: two separately rendered editors/devices completing a synchronized load, Android touch keyboard/clipboard/safe-area behavior on physical hardware, and actual network loss on a device. The independent clients prove live transport/room state but do not render a second game. A random-code collision was not forced. Full multiplayer stage playthroughs, gameplay scoring/rewards and recovery remain outside this task.


## MultiplayerStage and inherited characters - 2026-10-12

`Assets/Resources/Multiplayer_Male.prefab` is a variant of `Assets/Prefabs/PREFABS/CHARACTERS/Player.prefab`; `Multiplayer_Female.prefab` is a variant of `Player_Female.prefab`. Their existing asset GUIDs are preserved. Movement settings, controller geometry, CameraTarget, outfits, meshes, animations and future child/component edits inherit from the base. The variants add only a root PhotonView and MultiplayerAvatarSync; they do not copy or override the character's content. Edit the base prefabs for shared changes. Multiplayer-specific overrides still take precedence under Unity's normal variant rules.

PlayerSpawner now waits at most three seconds for character profile selection, uses a visible male fallback without an initialized Firebase session, creates one owned Photon avatar and places actors in four grounded slots. It binds the inherited local CameraTarget and starts with a downward view of the clearing. The inherited PlayerEquipmentLoader shows its fallback immediately, reads only the owner's profile and sends validated outfit selections to other clients. MultiplayerAvatarSync synchronizes position, rotation and movement animation while disabling remote input/character physics.

The new scene contains connected instances of Canvas 1, NetworkSpawner, GameInput, MobileInputUI, CM_ThirdPersonCam, TeleportManager and LevelMenuManager. Joystick, Jump/Interact, settings/pause/return buttons and the startup fade are connected. Unused stage-specific UI is disabled with scene overrides. Nothing is unpacked. The platform is 100 by 100 units with boundary colliders, a clear central spawn area, a stone loop/trail, a forest border, bushes/flowers/rocks, benches, lamps and a book landmark, using existing project assets. Decorations have no multiplayer puzzle logic.

The lobby and enabled build destination use MultiplayerStage. The editor setup also registers that scene in each ParrelSync editor's separate build settings on script reload. Protocol version 3 requires both clients to use the updated project before joining a room.

Rebuild from **Logic Legends > Multiplayer > Build MultiplayerStage And Character Variants**. This command preserves prefab links and recreates the variants from their bases; keep network-specific additions in the setup if rebuilding is needed. Live stage checks are in the existing Start And Chat check command. Their results and rendered screenshot are in `Temp/MultiplayerQA`.


Stage verification: 27 live checks passed with the new scene and character variants, including visible male/female defaults, local female ownership and camera retargeting, remote position/walking updates, inherited equipment RPC selection, grounded movement, one active stage AudioListener and startup fade completion. Setup validation confirms both variant bases, shared prefab links, build registration and all 95 grounded decoration instances. Both character base assets remain untouched. Existing Main Menu preview cameras produce an unrelated multiple-AudioListener warning; MultiplayerStage itself has exactly one active listener. Physical Android controls and two separately rendered game clients still need device testing.

Latest UI/chat verification: 42 live checks passed with zero failures after the GraphHUD redesign. This includes retained lobby history, guest-to-host and host-to-guest in-game messages, actor-based sender attribution, literal plain-text markup, unread counts, local input/look isolation, touch HUD restoration, Settings handoff, leaving cleanup and perspective/skybox preservation. Button containment and visible text fit were checked at 1920x1080, 2400x1080 and 1440x1080; captures are in `Temp/MultiplayerQA/chat-open-*.png`. Native mobile keyboard behavior and a second rendered device remain untested.

Lobby layout update: HostOrJoin, JoinByCode and the resized WaitingLobby passed nine Play Mode layout checks at 1920x1080, 2400x1080 and 1440x1080. WaitingLobby was checked with the game host and three independent Photon clients, including a long display name. Checks cover text overflow, button/frame containment, fixed action dimensions, button multiplier 4.5, roster population and a usable chat viewport. Screenshots are `Temp/MultiplayerQA/lobby-choice-*.png`, `lobby-join-*.png` and `lobby-waiting-*.png`; the report is `lobby-layout-checks.txt`. This is an editor aspect-ratio check, not a physical-device keyboard test. To rebuild only these saved menu screens, use **Logic Legends > Multiplayer > Build Lobby UI Only**; it preserves the stage and connected prefab instances. The opt-in layout runner uses `Temp/MultiplayerLobbyLayoutChecks.request` containing `prepare`, exits its temporary room and restores the original editor scene after completion.

MultiplayerStage chat styling update: the centered GraphHUD conversation frame now has a separate IMPACT title, Liberation Sans player-count/privacy line, a subtle divider and 32-unit content padding. Its wider message area and framed input use the existing artwork. CHAT, CLOSE and SEND share compact 220x88 geometry, readable labels and background multiplier 4.5; unread counts use a matching compact cream/wood plaque. Keyboard-aware placement, scene-only availability, unread tracking, transport and input restoration retain their existing behavior. Rebuild with **Logic Legends > Multiplayer > Build In-Game Chat UI Only**, which updates the scene-owned chat child while preserving the linked canvas prefab and environment. After this saved scene update, all 45 live start/chat checks passed with zero failures, including button dimensions/artwork and text containment at 1920x1080, 2400x1080 and 1440x1080, bidirectional chat, unread counts, local movement/look restoration, Settings handoff and room-exit cleanup. A second rendered device and native mobile keyboard were not exercised.

MultiplayerStage SafeArea structure: `Canvas 1/MultiplayerChatHUD` owns the centered conversation and full-screen backdrop directly under the canvas. `Canvas 1/SafeArea/MultiplayerChatEdgeControls` owns only the edge-positioned CHAT button and its unread badge. The existing edge touch HUD remains under SafeArea; centered pause/dialogue panels already belong directly to Canvas. Builders preserve this split without unpacking or changing the shared canvas prefab. The chat's keyboard offset now uses full-screen coordinates, and disabling its controller also hides the separately parented edge button. After saving this hierarchy, all 48 live start/chat checks passed with zero failures at 1920x1080, 2400x1080 and 1440x1080, including simulated iPhone cutouts on wide-phone/tablet captures, a full-canvas modal root, edge-button hierarchy, bidirectional chat and input restoration. Physical device/keyboard testing remains outstanding.
