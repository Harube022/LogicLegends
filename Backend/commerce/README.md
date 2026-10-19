# Gem top-ups and shop purchases

This is the new standalone Node.js backend for hosting outside Firebase while the project stays on **Spark**. It replaces `paymongo-local/functions` for the upgraded shop. Both top-ups and clothing purchases use one account wallet: `users/<uid>/gems`. Owned clothes are saved at `users/<uid>/inventory/<itemId>`, so hosted live purchases are compatible with the existing game equipment loaders.

The checked-in Unity settings use the **local sandbox**. Sandbox accounts, gems, purchases and equipped clothes live in the database emulator. The shop's coin display continues to read the player's existing Firebase coins; test-wallet responses never replace that coin balance. A sandbox purchase updates the shop's gem counter and can buy/equip clothes in the menu; it never changes the live Firebase profile or real player equipment. Existing localPayments test wallets from the old prototype are not copied automatically. In sandbox, gameplay stages still use the existing Firebase progression code; their rewards are not mixed into this sandbox commerce wallet.

## Current prices

- `gems_5`: **5 gems for PHP 1.00** (`100` centavos), QR Ph enabled by default.
- The ten active male/female clothing items: **10 gems each**.
- Server prices are in `catalog.json`. Unity sends product/item IDs; client amounts and price overrides are ignored. Update both catalog and scene labels if prices change.

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

5. In Unity, `Assets/Resources/CommerceSettings.asset` has the backend URL and Sandbox setting. The Editor uses `http://127.0.0.1:18080`. A Development APK must use the public HTTPS tunnel URL. Sandbox purchases are disabled in release builds.

6. Sign in normally. Open Shop → Top Up → Buy 5 Gems. Complete PayMongo's **simulated payment** and return to the game. The shop balance updates within a few seconds. After two successful top-ups, preview a 10-gem outfit, press Buy and Confirm. Balance becomes zero, the outfit becomes owned, and it appears in Customization for sandbox equip/preview.

Keep all three processes running during tests. Stop the database emulator with **Ctrl+C once**, wait for export completion, then stop the Node server and tunnel. Reopening the emulator imports `.emulator-data`. A new quick tunnel gets a new URL; update the backend `.env` and PayMongo webhook. The Editor URL stays localhost. Do not expose ports 9000 or 4000.

## External hosting preparation

Use a host that supports Node.js 22 or Docker. Build/start this **Backend/commerce** directory, with `npm ci` and `node server.js`, or use the included Dockerfile. The host assigns the public HTTPS URL **after you create and deploy the service**. Configure a persistent service suitable for incoming webhooks. The Docker context excludes local environment and credential files.

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

`database.rules.json` is a **reviewable commerce protection baseline**, not automatically deployed. It permits owner reads and selected profile/equipment writes; it blocks direct client writes to gems, coins, inventory, payment-grant markers and orders. Review/merge it with the actual database rules and other app features. Broad root or user-level `.write` grants in other rules would bypass narrower protection, so they must be removed. Database rules must be deployed separately; this backend never changes them by itself.

The existing StageCompleteManager, Coin and GraphCompletion scripts still grant gameplay currency from the client. **These legacy grants are denied under the protected rules.** They need trusted server validation and reward endpoints before public release. Do not enable permissive currency rules to keep those scripts working: that would let clients change paid balances. This task prepares top-up, purchase and equip endpoints; validating stage completion requires a separate game-specific reward implementation. Stage progression is independently allowed in the baseline, so it is not an anti-cheat system.

Once the rules and trusted gameplay reward path are ready, set `LIVE_WALLET_READY=true` on the host. The server intentionally refuses real Firebase mode without this acknowledgment. Perform a controlled live checkout, verify exactly one credit, buy an outfit, equip it, and restart the game to verify persistence. Refund/dispute handling and customer-support reconciliation should be configured before opening a public store; no refund revocation is automated here.

No live backend, Firebase rules or live PayMongo webhook has been deployed by this change.

## Validation

```powershell
npm.cmd test
# With the database emulator running:
$env:RUN_EMULATOR_TESTS='1'
npm.cmd test
```

The suite checks webhook signatures and modes, exact payment amounts, ID-based server pricing, insufficient balances, character eligibility, duplicate grants, ownership/equipment, cross-player order access and simultaneous spending. HTTP integration tests mock only PayMongo and token verification; they use real database emulator transactions. Completing a PayMongo sandbox checkout from Unity is still the final manual integration check for this updated UI.
