# Social chat: 24-hour retention on Render

Social direct messages and room-invite cards expire **24 hours after each message
was sent**. This is a rolling deadline, not a midnight reset. The same stored
conversation serves both friends, so deletion removes that message for both.
Removed friendships are also covered. Accounts, friendships, pending friend
requests, wallet data and Photon party chat are outside this cleanup.

`chat-retention.js` uses the Firebase Admin SDK on the existing Node.js service.
Firebase can remain on Spark; no Cloud Functions deployment is needed. Cleanup
does not depend on payment configuration or enable purchases.

## Deploy to the existing Render web service

1. Deploy the updated `Backend/commerce` source to the existing Render service.
   Keep its current root directory, `npm ci` build and `node server.js` start
   command. Preserve its existing commerce/payment settings.
2. In that service's Environment settings, add:

   ```text
   CHAT_CLEANUP_ENABLED=true
   CHAT_CLEANUP_INTERVAL_MINUTES=15
   STORAGE_MODE=firebase
   FIREBASE_PROJECT_ID=logic-legends-ac66c
   FIREBASE_DATABASE_URL=https://logic-legends-ac66c-default-rtdb.asia-southeast1.firebasedatabase.app
   ```

   Reuse its existing private Firebase credential: `FIREBASE_SERVICE_ACCOUNT_JSON`
   or `GOOGLE_APPLICATION_CREDENTIALS` pointing to a Render secret file. If missing,
   configure one privately as described in README. Remove
   `FIREBASE_DATABASE_EMULATOR_HOST`. Do not copy a service account into Unity or Git.
   `COMMERCE_ENABLED=false` can stay false if the shop is still paused.
3. Rebuild/distribute the updated Android app, then publish the prepared Firebase
   Realtime Database rules from `database.rules.json`, merging with the current
   live rules if they have changed. The Social changes are `.indexOn: ["sentAt"]`
   at each conversation and `sentAt === now` validation. Existing access permissions
   are retained. Updated clients send Firebase's server timestamp; older builds
   that send device timestamps can be denied by the stricter timestamp rule.
4. Save the Render environment and redeploy. Startup immediately runs cleanup;
   **already-expired stored history is permanently deleted on the first sweep**.
5. Check `https://logiclegends-payment.onrender.com/health`. With cleanup enabled,
   `chatCleanup` reports `retentionHours: 24`, `intervalMinutes: 15`,
   `lastSucceededAt`, `lastDeleted`, `running` and `lastError`. A successful run has
   a non-null `lastSucceededAt` and null `lastError`. Logs contain aggregate counts,
   not usernames, message text, account IDs or secrets.

This repository change does not deploy Render, change its environment, publish
Firebase rules, distribute an APK or delete any live chat history by itself.

## Render Free: cleanup while the service sleeps

A timer only runs while Node is running. Render Free spins down idle web services
and wakes them for incoming HTTP requests. Startup catches up, but a sleeping or
unavailable service can leave expired records physically stored for longer than
24 hours. Unity hides expired messages/invites and removes their unread counts
locally, using Firebase's server-time offset and a one-second expiry tick.

For regular physical cleanup on Free, configure an external scheduler you control:

1. Generate a random secret (for example `node -e "console.log(require('node:crypto').randomBytes(32).toString('hex'))"`).
   Store it only in Render and the scheduler as `CHAT_CLEANUP_TOKEN`.
2. Every 15 minutes, make an HTTPS **POST** to
   `https://logiclegends-payment.onrender.com/internal/chat-cleanup` with the header
   `Authorization: Bearer <CHAT_CLEANUP_TOKEN>`. Give the request at least two
   minutes to allow for a cold start, and retry failed requests. Never put the
   token in the URL, Unity, Git or a public workflow file.
3. A successful response reports `deleted`, `cutoff` and `conversations`. A 409
   means another sweep is running; retry later. A 503 means configuration or
   database access needs attention. Unauthenticated requests cannot run cleanup.
   Add `?dryRun=true` for a read-only count of messages currently due for deletion.
   This does not pause the service's ordinary startup/timer sweeps.

Without a scheduler or an always-running service, this is best-effort physical
cleanup on wake. Even with a scheduler, deletion occurs on the next successful
sweep, normally within 15 minutes after the 24-hour deadline, not at the exact
millisecond. This external schedule has not been created by the repository change.

## Data handling and verification

The backend discovers conversation keys with REST `shallow=true`, then queries
only expired records using the `sentAt` index in pages of 250. It never downloads
the complete message tree or deletes conversation parents. Concurrent new sends
survive. Repeated sweeps are safe; failed sweeps can retry. Missing/invalid legacy
timestamps are left for review, and existing client-dated records use their saved
timestamp. New writes require the server timestamp to prevent future-dated sends.
Only message records are removed; database backups, if configured separately,
have their own retention policy.

Run `npm.cmd test` in `Backend/commerce` for fixture, endpoint and existing backend
regressions. With the local database emulator on `127.0.0.1:9000`, run:

```powershell
$env:RUN_EMULATOR_TESTS='1'
node --test chat-retention.emulator.test.js
```

The integration test creates a disposable `demo-chat-retention-*` namespace and
checks actual shallow discovery, pagination, expiry, invite removal, server
timestamps, rejected forged dates and preservation of profiles/friendships. It
never connects to a live database. Unity's local retention check uses
`Temp/SocialMenuPresentationChecks.request` with the value `retention`; it checks
24-hour boundaries, unread counts and the existing delivery regressions.

References: [Render Free lifecycle](https://render.com/docs/free),
[Firebase Admin writes/deletion](https://firebase.google.com/docs/database/admin/save-data).
