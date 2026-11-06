# Gem top-ups and shop purchases

This is the new standalone Node.js backend for hosting outside Firebase while the project stays on **Spark**. It replaces `paymongo-local/functions` for the upgraded shop. Both top-ups and clothing purchases use one account wallet: `users/<uid>/gems`. Owned clothes are saved at `users/<uid>/inventory/<itemId>`, so hosted live purchases are compatible with the existing game equipment loaders.

There is **one gem balance**, with no paid/earned split. Existing gems are preserved. Stage completion now awards **0 gems**, including scenes that still serialize an older nonzero reward. Gameplay never writes the gem field, even with a zero increment, so a stage save cannot overwrite a top-up. Coin rewards and stage unlocks continue through transactions on their own fields. Firebase Console or privileged Admin SDK edits can still change coins and gems for developer testing; no administrator credentials or gem-edit backdoor is included in the game.

The Unity settings point to **https://logiclegends-payment.onrender.com** with Sandbox disabled. This selects the hosted wallet; it does not enable payments. Keep the host's `COMMERCE_ENABLED=false` until the launch checks are complete, then use `CHECKOUT_ENABLED=false` to verify wallet access before accepting new top-ups. For local sandbox testing, set the asset URL back to `http://127.0.0.1:18080` and enable Sandbox.

In sandbox, accounts, gems, purchases and equipped clothes live in the database emulator. The shop's coin display continues to read the player's existing Firebase coins; test-wallet responses never replace that coin balance. A sandbox purchase updates the shop's gem counter and can buy/equip clothes in the menu; it never changes the live Firebase profile or real player equipment. Existing localPayments test wallets from the old prototype are not copied automatically. In sandbox, gameplay stages still use the existing Firebase progression code; their rewards are not mixed into this sandbox commerce wallet.

The Main Menu shows saved Firebase balances and owned clothes before contacting the commerce service. If that service is unavailable, the shop remains browseable with purchases blocked. Customization can still load and save already-owned equipment through Firebase. Sandbox inventory replaces that preview only after a successful sandbox response; the backend remains the authority for sandbox purchases and equipment. Firebase reads and login-token retrieval have bounded waits with retry feedback.

An intentionally disabled setup service returns `SHOP_SETUP`. The shop treats this as a paused-purchases state: clothes and balances stay visible without an error banner, the Top Up card says "Top-ups are not available yet," and purchase buttons remain disabled. Connection failures still offer retry feedback. Older setup-service responses are recognized until that host is redeployed.

The shop refreshes its wallet every eight seconds while open, after purchase notifications, and when the app regains focus. It adds no native Firebase database listener. After the Editor restart on October 19, signed-in checks confirmed that the shop shows the character's five outfits with the backend offline, disables purchases in the preview, and loads the saved character/equipment in Customization. The earlier crash was in the installed Firebase 13.9.0 native desktop networking library; removing this listener is a precaution, not proof that all SDK crashes are fixed. Firebase Unity 13.15.0 documents a desktop listener use-after-free fix; coordinate any SDK upgrade with the team, since it affects the whole project. See https://firebase.google.com/support/release-notes/unity#version_13150_-_august_06_2026.

## Current prices

- `gems_5`: **5 gems for PHP 1.00** (`100` centavos), QR Ph enabled by default.
- All ten existing male/female outfits support **Coins or Gems**. Ownership IDs and existing balances are unchanged.
- `economy.json` is the canonical reward/pricing policy. `catalog.json` maps existing item IDs to rarity and character. Run `python Tools/sync_solo_economy.py` from the repository root after tuning it; the Unity fallback is `Assets/Resources/SoloEconomy.json`.
- The authenticated wallet response supplies authoritative prices. Purchases send item ID, chosen currency and the displayed price/version. The server calculates the actual price and rejects stale quotes; amounts supplied by a client cannot override it.

| Rarity | Coins | Gems | Full 1,000-Coin completions | One retry, no bonus (750) | One retry + typical Truth Table bonus (850) | One retry + typical Inference bonus (840) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Common | 3,000 | 30 | 3 | 4 | 4 | 4 |
| Rare | 12,000 | 75 | 12 | 16 | 15 | 15 |
| Elite | 28,000 | 150 | 28 | 38 | 33 | 34 |
| Special | 50,000 | 250 | 50 | 67 | 59 | 60 |
| Epic | 80,000 | 400 | 80 | 107 | 95 | 96 |

These are ceiling(price / payout) estimates from a zero Coin balance, not measured player telemetry. “Typical” models one checkpoint retry, ten correct eligible house quizzes (+100), or three correct eligible crystal quizzes (+90). Failed-run time is additional. Trials normally share 6–18-minute clocks, with different pause/reset rules; no reliable average completion-duration data exists. The former catalog charged 10 Gems for every rarity and offered no Coin purchase path. The initial Editor audit was signed out. A later read-only check of the cached signed-in wallet confirmed 0 Coins, 0 Gems and no owned cosmetics; no identity was exported and no balance or ownership was changed. This single wallet is a starting-balance check, not population telemetry. Common is an early goal; higher tiers require sustained successful play. Tune after collecting actual completion/retry data.

## Local test

Dependencies are already installed on this machine. For a fresh checkout, run `npm.cmd install` here and in `../paymongo-local`.

1. Start **only the database emulator** in terminal 1:

```powershell
cd 'C:\Users\ADMIN\OneDrive\Documents\GitHub\LogicLegends\Backend\paymongo-local'
node node_modules/firebase-tools/lib/bin/firebase.js emulators:start --project demo-logic-legends --only database --import .emulator-data --export-on-exit .emulator-data
```

2. Start the HTTPS tunnel in terminal 2. The new server uses **18080**, not the old Functions port 5001:

```powershell
cloudflared tunnel --url http://127.0.0.1:18080
```

3. Copy `.env.example` to `.env` if missing. Put your **test** secret key and webhook signing secret in `.env`; keep it private. Set `PUBLIC_BASE_URL` to the generated HTTPS tunnel URL with **no additional path**. In PayMongo Test mode, point the existing webhook to `<PUBLIC_BASE_URL>/webhook`, subscribed to `checkout_session.payment.paid`. Restart the Node server whenever `.env` changes.

4. Start the Node server in terminal 3:

```powershell
cd 'C:\Users\ADMIN\OneDrive\Documents\GitHub\LogicLegends\Backend\commerce'
npm.cmd start
```

5. For this local test, set `Assets/Resources/CommerceSettings.asset` to `http://127.0.0.1:18080` and enable Sandbox. A Development APK must use the public HTTPS tunnel URL. Sandbox purchases are disabled in release builds. Restore the hosted URL and disable Sandbox when returning to the hosted wallet.

6. Sign in normally. Open Shop → Top Up → Buy 5 Gems. Complete PayMongo's **simulated payment** and return to the game. The shop balance updates within a few seconds. After two successful top-ups, preview a 10-gem outfit, press Buy and Confirm. Balance becomes zero, the outfit becomes owned, and it appears in Customization for sandbox equip/preview.

Keep all three processes running during tests. Stop the database emulator with **Ctrl+C once**, wait for export completion, then stop the Node server and tunnel. Reopening the emulator imports `.emulator-data`. A new quick tunnel gets a new URL; update the backend `.env` and PayMongo webhook. The Editor URL stays localhost. Do not expose ports 9000 or 4000.

## External hosting preparation

Use a host that supports Node.js 22 or Docker. Build/start this **Backend/commerce** directory, with `npm ci` and `node server.js`, or use the included Dockerfile. The host assigns the public HTTPS URL **after you create and deploy the service**. Configure a persistent service suitable for incoming webhooks. The Docker context excludes local environment and credential files.

For the **first deployment**, set only `NODE_VERSION=22` and `COMMERCE_ENABLED=false`, and use `/health` as the health-check path. This starts a setup service without Firebase or PayMongo credentials. Its health response is `{"mode":"setup","paymentsEnabled":false}`; all account/payment endpoints return 503. This allows you to obtain the permanent HTTPS URL and configure secrets before accepting payments. Keep the switch false until the launch requirements below are complete. This mode needs no database emulator.

Setup mode does not load Firebase Admin. On Render, the server binds `0.0.0.0` and the platform's `PORT`. Startup logs show `Commerce starting`, then `Commerce routes ready`, then `Commerce setup service listening ...; payments disabled`. If Render reports no open port, check which startup message was reached, confirm the start command is `node server.js`, and keep `COMMERCE_ENABLED=false` while diagnosing it. Do not remove `/health` or enable payments to bypass a failed deployment.

Store configuration privately on that host:

```text
STORAGE_MODE=firebase
PAYMENT_MODE=live
FIREBASE_PROJECT_ID=logic-legends-ac66c
FIREBASE_DATABASE_URL=https://logic-legends-ac66c-default-rtdb.asia-southeast1.firebasedatabase.app
PUBLIC_BASE_URL=https://YOUR-HOST
PAYMONGO_SECRET_KEY=<live secret key>
PAYMONGO_WEBHOOK_SECRET=<live webhook signing secret>
PAYMENT_METHODS=qrph
```

**Remove** `FIREBASE_DATABASE_EMULATOR_HOST` from hosted configuration. Use the host's assigned `PORT`.

For Firebase credentials, open Firebase Console → Project settings → Service accounts → Firebase Admin SDK → Generate new private key. Upload the JSON as a **private secret file** and point `GOOGLE_APPLICATION_CREDENTIALS` at that file, or store its complete JSON in the host's `FIREBASE_SERVICE_ACCOUNT_JSON` secret. Never put it in Unity, `Assets`, a Docker image, Git or chat. Local credential files can be kept under this directory's ignored `.secrets/` folder. The service-account email alone is not a credential. Local emulator tests do not need this file.

Create/update the **live** PayMongo webhook to `https://YOUR-HOST/webhook`. Change Unity's CommerceSettings Backend URL to the host URL and disable Sandbox only when the live environment is ready.

## Required before accepting live payments

`database.rules.json` is a **reviewable commerce protection baseline**, not automatically deployed. It permits owner reads, integer coin rewards, stage progression, and selected profile/equipment writes. It blocks direct client writes to gems, inventory, payment-grant markers and orders. Review/merge it with the actual database rules and other app features. Broad root or user-level `.write` grants in other rules would bypass narrower protection, so they must be removed. Database rules must be deployed separately; this backend never changes them by itself.

Trial completion coins now use the authenticated trial endpoints described below. The existing `Coin` world pickup and stage progression paths remain compatible; they never grant gems. This is not a server-authoritative validation of puzzle answers or world pickups. The server catalog uses gems for the active clothing items, and there is no coin-to-gem conversion endpoint. Gem purchases, spending, ownership and payment-grant markers are server controlled.

Keep `COMMERCE_ENABLED=false` on Render until the updated reward scripts, backend and protected rules are deployed together and verified. Older game builds still attempt client gem writes or whole-account transactions and must be updated. No stage scene files need to be edited to apply the zero-gem reward policy.

Once the protected rules and the zero-gem gameplay update are deployed and verified, set `LIVE_WALLET_READY=true` on the host. The server intentionally refuses real Firebase mode without this acknowledgment. Live top-ups additionally require `CHECKOUT_ENABLED=true`; omitting it keeps new checkouts disabled. With commerce running, setting `CHECKOUT_ENABLED=false` pauses new top-ups while wallet reads, outfit purchases, and fulfillment of existing payments continue. Use `COMMERCE_ENABLED=false` only for initial setup or a full shutdown, because it also blocks webhook fulfillment. Render environment changes require a redeploy.

Perform a controlled live checkout, verify exactly one credit, buy an outfit, equip it, and restart the game to verify persistence. Refund/dispute handling and customer-support reconciliation should be configured before opening a public store; no refund revocation is automated here.

To leave setup mode, also change `COMMERCE_ENABLED` to `true` and redeploy after all live environment variables and credentials are configured. Enabling this switch does not bypass the live-wallet readiness check.

No live backend, Firebase rules or live PayMongo webhook has been deployed by this change.

## Validation

```powershell
npm.cmd test
# With the database emulator running:
$env:RUN_EMULATOR_TESTS='1'
npm.cmd test
```

The suite checks webhook signatures and modes, exact payment amounts, ID-based server pricing, insufficient balances, character eligibility, duplicate grants, ownership/equipment, cross-player order access and simultaneous spending. HTTP integration tests mock only PayMongo and token verification; they use real database emulator transactions. Completing a PayMongo sandbox checkout from Unity is still the final manual integration check for this updated UI.


## Trial completion coins and authenticated Admin

The same service now provides `/trials/access`, `/trials/start`, `/trials/retry`,
`/trials/admin-used`, `/trials/quiz-answer`, and `/trials/complete`. They use the existing Firebase ID token,
account UID, service credentials, and `users/<uid>/coins` balance. There is no second
wallet. Deploy the updated **Backend/commerce** service together with the new Unity
build. The existing `CommerceSettings` URL stays the same; no extra public service,
payment configuration, or gem migration is required. This repository change does
not redeploy the live host.

Each trial has a configurable base reward (currently 1,000 Coins). A run allows **two Try Again operations maximum**. Reductions use the original base: `floor(base * (100 - 25 * retries) / 100)`, giving **1,000 / 750 / 500**. Retry still invokes that stage’s existing checkpoint/reset flow: Propositional Logic retains its checkpoint and retry time limits; Sets repeats the timed-out challenge; stages that previously reloaded the run still do so. Ordinary answer corrections and Retry Route are not Game Over retries.

The first Game Over offers Start Over, Try Again (25% reward reduction), and Main Menu. The second offers Try Again at 50% total reduction. The third replaces Try Again with Study in Logic Garden Library. Study uses the existing Garden entrance and does not mark dialogue, objectives, or library unlocks complete. **Start Over** abandons the attempt and clears its progress, retries and pending bonuses. It does not alter any saved balance or inventory. Help, reading and tutorial replay/skip never count as retries.

Completion pays only after the whole trial: **adjusted base + pending quiz bonus**. Bonuses are not reduced by retries. They remain pending and non-spendable until completion; a failed or abandoned run pays nothing. Start Over clears them. Existing historical completion receipts are never recalculated or credited again.

The inspected pickup quizzes previously applied time penalties and unlocked collection but did not contain a completion-Coin bonus implementation. Their distinct interactions remain unchanged; the configurable pending rewards are now:

- **Truth Table:** +10 Coins for a correct first answer at a house/column/challenge event; cap **180** per run. A reset house, new quiz draw, rejected placement or replayed column keeps the same event identity, so it cannot farm additional Coins. Existing wrong-answer and column penalties remain unchanged.
- **Rules of Inference:** +30 Coins for a correct first pickup-quiz answer per tower rule/challenge; cap **150** per run. Either answer permits collection. Answering latches that crystal’s quiz, and its completed event survives dropping, water recovery, retries and outbox reload. The quiz’s 10-second wrong-answer penalty remains separate from altar penalties.

First answers, including wrong answers, lock the event. The server verifies the choice against exported gameplay question data (`quiz-questions.json`), validates event ranges and applies its own amounts/caps. Client-supplied `bonus`, `reward`, or `correct` fields cannot grant Coins. Export updated banks through **Logic Legends > Economy > Sync Policy and Export Quiz Answers** when authoring questions. Practice quizzes send no reward event. Admin-assisted attempts pay zero base and zero bonus.

`/trials/quiz-answer` stores pending event receipts. The completion receipt and existing wallet balance are updated in one Firebase account transaction. Client outboxes retain stable attempt/event IDs through reloads and uncertain responses. Failure/completion cards show retries, original base, reduction, bonus (where applicable), and final/potential payout. No individual challenge payout or separate wallet is introduced.

Each account's attempt ledger is stored below `users/<uid>/trialRuns/<trial>`.
The completion receipt and coin increment commit in a single account transaction.
Repeated completion requests, result-panel reopen, scene reload, and a lost response
after a successful commit return the same receipt without a second increment.
Unfinished Unity requests persist in an account-scoped local outbox and reconnect
after login, including from the main menu. No balance is saved locally. Old pending
completions finish before a new run replaces their active-attempt pointer. Existing
gems, inventory, equipment, and progression fields survive the transaction.

Admin access is granted only to authenticated Firebase account records with one of
these normalized emails:

- `sngbdsa137@gmail.com`
- `russelbarrameda593@gmail.com`
- `famatigabarrameda@gmail.com`

The backend verifies the ID token, including revocation, then reads the Firebase
Auth user record. Request-body email, profile email, and Unity visibility/build
switches cannot grant access. Both Auth and Database apps use the configured service
identity, including when supplied through `FIREBASE_SERVICE_ACCOUNT_JSON`. Unity
keeps a short-lived backend decision, invalidates it on identity/session or scene
changes, and closes an Admin panel if access is lost. Account services must be
reachable to obtain or refresh Admin permission; there is no local-email fallback.

Every Admin mutation requires the shared authorization check. A clear/skip waits
for `/trials/admin-used` to persist the exclusion **before** invoking gameplay.
That exclusion survives retries/reloads and makes the entire attempt's normal coin
reward zero. Opening the read-only question viewer does not itself clear content,
change progression, count as a retry, or grant coins. A later deliberate new run is
eligible again. Existing Admin progression behavior remains unchanged.

The supplied database rules already deny client writes to the new `trialRuns`
ledger through their default-deny policy. Do not add a broad write permission at
`users/<uid>`, because that would also let clients forge receipts. The older
world-pickup coin/progression permissions are unchanged. Completion remains a
report from the shipped game; these endpoints do not independently solve or grade
the stage's proof, placement, or connection data.

### Trial verification

`node --test` covers the pure reward/authorization rules. With the local database
emulator on `127.0.0.1:9000`, set `RUN_EMULATOR_TESTS=1` and run `node --test` to also
exercise real Firebase transactions and the authenticated HTTP routes. The suite
uses generated fixture UIDs and mocked Auth records/token verification, never real
accounts. It checks all ten trials and all three approved identities, ordinary and
disabled accounts, spoofed email fields, duplicate and concurrent completions,
retries, receipt recovery, and preservation of existing wallet/profile fields.

In the Unity Editor, `TrialAccountPlayChecks.Begin()` runs isolated Play Mode client
checks using a transport compiled only into the Editor. It checks the durable
outbox, retries and lost responses, account switches, all Admin mutation entry
points, and portrait/landscape coin text. Reports are written to
`Library/TrialAccountChecks.txt`; fake-account preferences are removed afterward.


## Economy v2 rollout and verification

Deploy the updated **Backend/commerce** service before distributing the new Unity build, then explicitly set `SOLO_ECONOMY_V2_ENABLED=true` after the programmer has verified it. Existing wallet and inventory data need no migration; old paid receipts stay intact. The client refuses to send a new completion to an older reward service and blocks purchases when authoritative v2 prices are missing. No production deployment or real-money purchase is performed by the test tools.

- `node --test` runs policy, reward, purchase, authorization, and server tests.
- With the local database emulator bound to `127.0.0.1:9000`, set `RUN_EMULATOR_TESTS=1` to also exercise authenticated HTTP, transaction concurrency, duplicate payouts/bonuses, stale prices, both purchase currencies, and database rules.
- Unity: **Logic Legends > Economy > Check All Solo Scenes in Play Mode** checks actual failure callbacks, checkpoint destinations, touch raycasts, both retry limits, fresh starts, mobile result layouts, and both real quiz hooks using isolated accounts.
- Unity: **Logic Legends > Economy > Check Coins and Gems Shop in Play Mode** checks the saved Main Menu preview and explicit purchase confirmation with fake wallets. No signed-in account is required and no real funds/inventory are used.
- `python Tools/sync_solo_economy.py --check` verifies the canonical policy and Unity display fallback agree. A build also rejects an out-of-sync policy.


## Main programmer handoff: connect after the GitHub merge

This work stays local until the team commits it. No branch switch, commit, push, merge, or hosting deployment has been performed. At the read-only check, local `main` and GitHub `main` both pointed to `ed8f02f9a77e4c86204abb6cb1b87a7648e113c0`. Later changes can still introduce conflicts; the PR checks and Unity checks must pass on the actual merged revision.

### One connection, existing wallet

Use the existing **Assets/Resources/CommerceSettings.asset**. Its existing `Backend URL` remains the single endpoint used by accounts, Coins, Gems, inventory, purchases and trial rewards. No second account system, balance, Firebase project, or new secret in the Unity client is needed.

There are two coordinated opt-ins: backend `SOLO_ECONOMY_V2_ENABLED` defaults to **false**, and the Unity **Solo Economy Connected** checkbox (`soloEconomyConnected`) defaults to **off**. The backend compatibility adapter retains the currently deployed reward and 10-Gem purchase contract while its switch is off. This protects the existing live build even if a repository merge triggers hosting auto-deployment; it does not create another wallet. The new linear 1,000/750/500 policy and rarity prices apply only after the programmer activates v2. It is intentionally not enabled in this handoff. While off:

- Normal completion rewards and quiz bonuses stay in the account-scoped local outbox, with their original IDs. They do not call the new reward endpoints. They can sync after connection is enabled.
- New purchases are blocked before a request is sent. Existing wallet reads, owned outfit previews, and the existing equipment/account systems remain available.
- Existing Admin authorization and confirmed zero-reward exclusion use the current service. An older unpaid normal completion must be resolved before a new Admin attempt can replace its server pointer.
- Local Play Mode and Development builds are available. A release build is blocked until the connection switch is enabled, with an HTTPS host and Sandbox disabled.

### Programmer's integration order

1. Review and merge the local code through the team's normal process. Include new C# files **and their `.meta` files**, `Assets/Resources/SoloEconomy.json` and its `.meta`, backend files, and the two Python check/sync tools. This checkout also contains earlier scene/material/tutorial work: review those changes separately rather than overwriting or blindly accepting Unity YAML. The economy integration itself requires no new scene-object wiring and does not replace scene GUIDs.
2. Let **Solo economy merge checks** run on the PR. Its workflow has read-only repository permissions, no deployment step, no account credentials, and no commit/push commands. It checks source conflict markers, the economy mirror, quiz IDs, Unity metadata, and credential-free backend tests. Firebase-emulator integration tests and Unity Play Mode checks still run locally.
3. Deploy the **Backend/commerce** directory to the existing service using its existing private Firebase/hosting configuration. Leave `SOLO_ECONOMY_V2_ENABLED=false` for the initial deployment; this keeps the old live contract active. Set it to `true` and restart the service when the new client release is ready for connection. Preserve the same production Firebase database and all existing `users` data. No wallet/inventory reset or database migration is required. Coordinate the backend and client release: older clients cannot submit the new dual-currency price-confirmation contract; stale purchases are rejected rather than charged a changed price.
4. In Unity, sign in with a test account. Check `CommerceSettings` → `Backend URL` against the deployed service. For production use HTTPS and disable Sandbox; for a local Editor emulator use the existing sandbox configuration.
5. Run **Logic Legends > Economy > Test Backend Connection (Read Only)**. It calls authenticated `GET /economy` and checks version 2, all ten trial policies, the cosmetic catalog and the required capabilities. It creates no attempt, changes no balance/inventory, and does not enable the checkbox automatically.
6. After the backend opt-in is on and the read-only check succeeds, enable **Solo Economy Connected** in Unity. Pending outbox operations retain their IDs and sync safely. Run the ten-scene and shop Play Mode checks, build a Development APK, and verify the production-like test account before making a release build.

The new read-only endpoint returns `version`, `capabilities`, `economy`, `catalog`, `sandbox` and `live`. It never returns quiz answers. All writes continue to require the existing authenticated Firebase ID token. Backend configuration and quiz data must match the Unity build; the build precheck rejects stale mirrors/exports.

For local merge verification, run `python Tools/check_solo_economy_merge.py` and the backend tests. Never commit Library, Temp, emulator data, credentials, or generated recordings. The workflow does not create commits or publish a service.

The compatibility snapshots are `legacy-trials.js` and `legacy-catalog.json`. They are deliberately fixed to the pre-merge live contract; tune new rewards and prices only in `economy.json`. The rollout test verifies that leaving the server switch off preserves old purchases/rewards and rejects all new v2 writes.

Workflow action references: [checkout](https://github.com/actions/checkout) and [setup-node](https://github.com/actions/setup-node).
