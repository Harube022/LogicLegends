# Logic Legends: game and website integration

## What changed

- **Access and moderation:** Unity checks the Render account endpoint before opening the signed-in menu, including for saved sessions and Google sign-in. The endpoint uses a verified Firebase ID token for the UID, reads the current Firebase Auth user and the website moderation record, and allows only verified NCST players or verified users with the trusted `admin` or `super_admin` claim. Render applies the same decision before wallet, trial and multiplayer reward requests. The existing trial Admin panel uses this decision. The main menu Admin button opens the existing website Profile admin controls after another server check.
- **Already signed-in players:** The game rechecks on protected entry, after returning to the app when the last check is old, and about every two minutes in the foreground. Confirmed denials return the player to login and leave the Photon lobby. A temporary connection failure blocks new protected entry without signing the player out. An active match may continue locally through an outage; protected backend operations still check eligibility.
- **Existing restrictions:** The website supports `feedback` and `dashboard`, with optional expiration. A feedback restriction blocks both game feedback and reports. Dashboard restrictions remain scoped to the website features that use them. No existing restriction type blocks multiplayer only. Bans block protected access and the existing website ban action disables the Firebase account and revokes its sessions.
- **Social and Feedback:** Friends, Chats and player search have a Report Player action with the seven specified reasons. Others requires an explanation. The lower left Feedback button opens four categories, with subject and description. The form confirms success only after the website returns a saved record ID. Failed submissions keep the draft. The existing rate limit allows one submission per account per 30 seconds across both clients.
- **Review:** The existing Netlify education function writes to `website/feedback`. Records use server UID, time and status; reports add the target UID and reason. Existing website submissions are still readable. Platform admins can mark a game submission reviewed or go directly to the reported account in the existing moderation controls. A report never applies a penalty automatically.

The game and website use the same Firebase Auth UID and the same Realtime Database. The old game admin email list was a separate authorization mechanism, not an account provisioning flow. Before rollout, a Super Admin should confirm which legacy game admins should retain access and use the existing website Profile role controls to grant their **existing accounts** the Admin claim. The website reuses the Firebase identity for an existing email. This preserves the UID and saved progress. Users need to sign in again after the role update revokes older sessions. Accounts are not automatically elevated from the old list.

## Deploy in this order

1. **Confirm administrator claims** for existing game admins before changing the Render backend. Use the existing trusted role controls. Never add a public role assignment route or put Admin SDK credentials in Unity.
2. **Deploy the website repository changes to Netlify.** The current site URL and education/moderation functions remain in place. This change does not introduce a Netlify environment variable. The existing server-only Firebase Admin credentials must point to the same Auth project and RTDB as the game.
3. **Prepare shared Firebase rules.** Export the current production Realtime Database rules and keep an untouched backup. If the website's additive rules migration has not been applied, review that migration too. On the current export (or the result of that migration), run:

   ```powershell
   node Backend/commerce/patch-game-rules.js exported.rules.json reviewed.rules.json
   ```

   Review the output against the export before publishing it. The helper wraps existing access grants under `users` and `social`, adds the eligibility/ban/revoked-session checks, protects the server-owned website data, and preserves existing wallet validation and indexes. It refuses broad root grants and direct website grants for manual review. If the live database has other game paths, review and protect them separately. Test the actual candidate with a staging copy before applying it to production. Do not replace the live rules with the repository template. The game does not require Identity Platform or Firebase blocking functions; a disallowed user may still obtain a Firebase credential on a free project, but the protected game APIs and deployed rules reject it.
4. **Deploy Render's existing game backend** (`Backend/commerce`, currently used by `logiclegends-payment.onrender.com`). **No new Render environment variable.** Keep the existing Firebase project/database/service account and payment/economy settings. The service must already be running in its normal mode; if `COMMERCE_ENABLED=false`, its setup mode does not serve account routes. Verify the expected commit reaches the service's linked branch first. If auto deploy is on, wait for that commit to become live. If it is off, use **Manual Deploy > Deploy latest commit** on the service's Deploys page. A manual deploy cannot pick up local files that have not been committed and pushed. Confirm the live deploy SHA and test `GET /game/access` with a test account; an allowed request returns `policyVersion: 1`. Do not change payment flags to roll out this feature.
5. **Smoke test before distributing the new game build.** Use test accounts for NCST player, outside-domain player, outside-domain Admin, demoted Admin, restricted player and banned player. Test email/password, Google and saved sign-in, keeping a session open during a role or ban change. Submit a report and a bug from an Android device; review them in the Netlify admin page. Confirm a timeout keeps the draft and direct RTDB access follows the rules. The new Unity build requires the new account endpoint.

No production settings, data, role claims or deployments were changed by this work.

## Multiplayer scoring and leaderboard findings

The current Open Archive mode is cooperative. The Photon Master Client advances the match state, accepts puzzle work, and records successful teammate support. Completing all ten topics opens the exit. A victory requires at least one survivor and all survivors to escape; a team wipe or expired match clock is a defeat. The local results display is calculated from that match state. Wrong answers and repeat actions do not award participation credit; there is no deduction from the multiplayer coin payout or a cumulative ranking score. Gameplay time/health penalties are separate.

A distinct accepted puzzle contribution counts once per topic, round and subphase. The support keys count useful medkits, batteries, revives and used gifts once per action type and teammate. Let `tier = min(3, distinct valid credits)`. There is no fixed coin grant per answer:

| Outcome | Saved multiplayer payout |
| --- | --- |
| Victory | `floor(600 * tier / 3)` team coins + `floor(25 * fullyRestoredCrystals * tier / 3)` crystal coins + `50 * tier` participation coins; up to 1,000 total. |
| Defeat | `floor(min(150, 15 * completedTopics) * tier / 3)` consolation coins. |
| No credit, offline practice or admin-assisted | Zero permanent payout. |
| First full-tier victory / first full-tier victory with all 10 crystals | One Gem for each milestone, once per account. Both can qualify in the same match. |

The game binds a local Firebase UID to a Photon actor number for its pending reward. The Photon transport currently uses an anonymous session ID; the actor number is scoped to the room. The host creates the match GUID. Render selects the wallet by Firebase token UID, checks the result format, roster, topics and contribution keys, and saves `users/{uid}/multiplayerRewards/{matchId}`, milestone markers and wallet increments in one transaction. The receipt prevents duplicate payouts for the same UID and match GUID. The match data and tiers are temporary; wallet currency and receipts persist. No writer for separate cumulative leaderboard points was found.

The website calls the read-only `education?action=leaderboard` endpoint. It reads up to 100 user records ordered by the numeric child configured in `LEADERBOARD_POINTS_FIELD`, maps the account key to Firebase UID and displays profile name or username. It sorts descending by points, then by name; the UI assigns sequential places. This is a real data query when configured. If there is no configuration or data, it shows the corresponding empty state. Production configuration was not inspected. The UI does not infer points from coins.

**Future plan:** Agree on a separate ranking points policy first. Store server-issued match IDs with an authenticated Firebase roster and terminal outcome from a trusted match authority. Recompute points from validated evidence, then atomically record one result per UID and match ID and increment a protected total. Keep that total separate from the spendable wallet and the client-reported receipts. Reject impossible results and conflicting replays, and reconcile abandoned matches without awarding points. Current Photon host and client evidence is insufficient for a competitive ranking: the Render endpoint validates structure and duplicate receipt IDs, but cannot prove a match or its roster occurred. Cross-client agreement alone would still permit collusion. A trusted match authority or verified result source is a prerequisite, subject to cost/hosting review. Keep website reads to a small indexed top-N query on demand and avoid per-action database writes or subscriptions.

**Future notifications:** If needed, store a small set of server-authored notices and fetch a bounded page when the user opens the relevant screen. Measure read volume before considering push or background delivery. No notification or leaderboard behavior was changed.

## Verification and limitations

- Game backend: all 91 tests passed with the local Firebase Realtime Database Emulator, including current-role decisions, bans, domain eligibility, protected direct access, and regression coverage for trial, wallet, checkout and multiplayer rewards. Account fixtures use fake Firebase token verification and isolated emulator data.
- Website: both new integration tests passed. The full suite passed 31 of 41. The same 10 failures reproduce on the unchanged website baseline; they concern older role/invitation expectations. Existing application behavior was preserved.
- Website browser smoke: the local fixture site rendered the report and bug category in the existing review list at 1,440 and 390 pixels. Opening the report resolved the exact Firebase UID in the moderation panel, and marking it reviewed updated the review card.
- Unity Editor: compiled without errors. Isolated checks passed for cached admin decisions across role/account changes; report target selection and self-report blocking; seven report reasons and four feedback categories; required Others details; retry after failed submission; and action sizing at 4:3, 16:9 and 20:9. Android keyboard and touch behavior still need a physical device smoke test.
- The new rules were exercised against the repository template in the emulator. The live rules export, real Firebase sign-in and Android device end-to-end tests are rollout checks.
- Standard Unity clients check account access before opening or starting a lobby, and leave on a confirmed ban. Photon does not currently authenticate Firebase identities or independently revoke an active room connection. Blocking a modified client at the Photon transport would require configuring trusted Photon custom authentication and enforcing it on the service. Local code cannot secure access to bundled assets.

## Main files

| Project | Files | Responsibility |
| --- | --- | --- |
| Unity | `AuthManager.cs`, `TrialSessionServices.cs`, `TrialBackendClient.cs`, `AdminAuthorization.cs` | Sign-in gating, rechecks, authenticated transport and verified admin visibility |
| Unity | `GameCommunityUI.cs`, `CommunityBackendClient.cs`, `FriendsUI.cs` | Main Menu and Social actions, validation, submission and draft handling |
| Render | `app.js`, `game-access.js`, `patch-game-rules.js` | Fresh account checks across routes and the additive RTDB rules migration |
| Website | `netlify/lib/education.mjs`, `netlify/lib/moderation.mjs`, `netlify/lib/dashboard-accounts.mjs`, `Logic_Legends_Web/JS/admin-tools.js` | Shared submission, review, exact account lookup and role revocation |
