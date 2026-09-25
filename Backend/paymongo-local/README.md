# Local PayMongo test for Logic Legends

This setup keeps Firebase on Spark. It runs Functions and Realtime Database **on your computer**, verifies existing Logic Legends Firebase login tokens, and stores all test orders and gems locally. It does not change production database rules, balances, or inventory. Test gems are displayed separately in the Top Up panel and cannot buy real skins.

Product: `gems_5`, **5 gems for PHP 1.00** (`100` centavos). Checkout currently offers **QR Ph**; enable that method in your PayMongo test account. No Unity IAP package or Firebase service-account private key is required for this local setup.

## 1. Install dependencies (once)

Requires Node.js 22 and Java 21. In PowerShell:

```powershell
cd 'C:\Users\ADMIN\OneDrive\Documents\GitHub\LogicLegends\Backend\paymongo-local'
npm.cmd install
npm.cmd --prefix functions install
Copy-Item functions/.env.local.example functions/.env.local
```

Open `functions/.env.local` in a local editor and replace `PAYMONGO_SECRET_KEY` with your **secret test key**. Do not put it in Unity, screenshots, chat, or Git. `.env.local` is ignored by Git. The backend refuses live keys.

## 2. Start the emulators

From the same directory:

```powershell
npm.cmd start
```

Keep this terminal running. On the first run, the missing `.emulator-data` import may produce an informational warning. Stop with Ctrl+C to export local orders and test wallets for next time. A forced shutdown may lose local data.

- Emulator UI: http://127.0.0.1:4000
- Health endpoint: http://127.0.0.1:5001/demo-logic-legends/us-central1/payments/health

The demo project is intentional: no Functions deployment, billing upgrade, or production database access. The Auth emulator is intentionally **not** started; use your normal game login. Only public Firebase signing certificates are used to verify those login tokens.

## 3. Expose only the Functions port with an HTTPS tunnel

Install Cloudflare Tunnel (`cloudflared`) following https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/downloads/ . Then run in a second terminal:

```powershell
cloudflared tunnel --url http://127.0.0.1:5001
```

Copy the generated `https://....trycloudflare.com` address. Do not expose the database port (9000) or Emulator UI (4000).

Set this in `functions/.env.local`, using your generated hostname:

```text
PUBLIC_BASE_URL=https://YOUR-HOST.trycloudflare.com/demo-logic-legends/us-central1/payments
```

## 4. Register a TEST PayMongo webhook

In PayMongo **Test mode → Developers → Webhooks**, add an endpoint:

```text
https://YOUR-HOST.trycloudflare.com/demo-logic-legends/us-central1/payments/webhook
```

Subscribe to `checkout_session.payment.paid`. Copy that webhook's **signing secret** (different from the API secret key) into `PAYMONGO_WEBHOOK_SECRET` in `.env.local`. Restart the Firebase emulators after changing configuration. Keep the tunnel running so its address does not change.

If the tunnel URL changes, update both the PayMongo webhook and `PUBLIC_BASE_URL`, then restart the emulators. For an APK, update the Unity backend URL as well.

## 5. Test in Unity

In `Main Menu`, select `Shop_Menu/TopUpScroll`. Its `PayMongoTestTopUp` component contains the backend URL, buy button, and status label.

- **Editor:** the default `http://127.0.0.1:5001/demo-logic-legends/us-central1/payments` URL works.
- **Android phone:** set Backend URL to the full public HTTPS `PUBLIC_BASE_URL`, then build with **Development Build** enabled. Phone localhost refers to the phone, not your PC. Keep your PC, emulators, and tunnel running.
- **Release builds:** this component disables test purchases.

Sign in normally, open Shop → Top Up, and click **Test Buy** for **5 Gems / PHP 1.00**. Use the PayMongo test checkout simulation; do not send real funds to a test QR. Return to the game after simulating payment. The status displays `Local test gems: 5` after verification. The main game's gem balance remains unchanged.

The client polls while the panel is open, checks again on focus, and remembers a pending order across restarts. Repeated clicks reopen the same pending checkout. The webhook and order-status endpoint both verify payment with PayMongo; if the webhook is delayed, a status check can reconcile the order. Fulfillment updates the order and test wallet in one transaction, so duplicates cannot grant twice.

For a failed or expired checkout that cannot be reopened, stop Play mode and clear only this test client's `PayMongoTestOrder_<uid>` and `PayMongoTestOrder_<uid>_request` PlayerPrefs entries before retrying. Do not clear all game PlayerPrefs. Uncertain checkout creation failures are deliberately not automatically retried with a new order.

## Verification

```powershell
npm.cmd test
```

Tests cover duplicate fulfillment, tampered signatures, stale webhook timestamps, live-mode rejection, incorrect amount/currency/session/product quantity, pending payments, and emulator-only storage. These are local automated tests; a real PayMongo sandbox checkout must still be completed after entering your private key and webhook secret.

Before a public release, replace this local-only backend with hosted infrastructure, move real wallet spending/reward grants to trusted backend operations, and review the real database rules. This test component never grants spendable production gems.

References: https://docs.paymongo.com/docs/payment-channels-hosted-checkout-quick-start and https://firebase.google.com/docs/functions/local-emulator
