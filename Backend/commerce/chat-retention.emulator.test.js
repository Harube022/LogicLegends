const { test } = require('node:test');
const assert = require('node:assert/strict');
const { createFirebaseStore, sweepMessages, RETENTION_MS } = require('./chat-retention');

test('real RTDB shallow traversal, indexed deletion and server timestamp rules preserve other data',
  { skip: process.env.RUN_EMULATOR_TESTS !== '1' }, async () => {
    process.env.FIREBASE_DATABASE_EMULATOR_HOST = '127.0.0.1:9000';
    const { getApp, deleteApp } = require('firebase-admin/app');
    const { getDatabase } = require('firebase-admin/database');
    // A disposable namespace, never the normal emulator database or a live account.
    const project = 'demo-chat-retention-' + Date.now();
    const ns = project + '-default-rtdb', base = 'http://127.0.0.1:9000';
    const headers = { Authorization: 'Bearer owner', 'Content-Type': 'application/json' };
    const rules = require('./database.rules.json');
    const store = createFirebaseStore({ STORAGE_MODE: 'emulator', FIREBASE_PROJECT_ID: project,
      FIREBASE_DATABASE_EMULATOR_HOST: '127.0.0.1:9000' });
    const db = getDatabase(getApp('social-retention'));
    const now = Date.now();
    const message = (sentAt, inviteCode = '') => ({ sender: 'alice', text: 'fixture', inviteCode, sentAt });
    const fixture = { users: { alice: { username: 'Alice', coins: 123, gems: 9 }, bob: { username: 'Bob' } },
      social: { friends: { alice: { bob: true }, bob: { alice: true } }, requests: { alice: { carol: true } },
        messages: { alice: { bob: {
          expired: message(now - RETENTION_MS - 1), invite: message(now - RETENTION_MS, 'AB3D7K'),
          recent: message(now - 1000), malformed: { text: 'no date' }
        } }, carol: { dave: { formerFriends: message(now - RETENTION_MS - 20) } } } } };
    try {
      assert.equal((await fetch(`${base}/.settings/rules.json?ns=${ns}`, { method: 'PUT', headers, body: JSON.stringify(rules) })).status, 200);
      await db.ref().set(fixture);
      assert.deepEqual((await store.keys([])).sort(), ['alice', 'carol']);
      const dry = await sweepMessages(store, { now: () => now, dryRun: true, pageSize: 1 });
      assert.equal(dry.wouldDelete, 3);
      const result = await sweepMessages(store, { now: () => now, pageSize: 1 });
      assert.equal(result.deleted, 3);
      const saved = (await db.ref().get()).val();
      assert.deepEqual(saved.users, fixture.users);
      assert.deepEqual(saved.social.friends, fixture.social.friends);
      assert.deepEqual(saved.social.requests, fixture.social.requests);
      assert.deepEqual(Object.keys(saved.social.messages.alice.bob).sort(), ['malformed', 'recent']);
      assert.equal(saved.social.messages.carol, undefined);
      const seconds = Math.floor(Date.now() / 1000);
      const token = Buffer.from(JSON.stringify({ alg: 'none', typ: 'JWT' })).toString('base64url') + '.' +
        Buffer.from(JSON.stringify({ aud: project, iss: 'https://securetoken.google.com/' + project, sub: 'alice', user_id: 'alice',
          auth_time: seconds, iat: seconds, exp: seconds + 3600, firebase: { sign_in_provider: 'custom', identities: {} } })).toString('base64url') + '.';
      const send = (key, value, method = 'PUT') => fetch(`${base}/social/messages/alice/bob/${key}.json?ns=${ns}&auth=${token}`,
        { method, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(value) });
      assert.equal((await send('serverTimestamp', message({ '.sv': 'timestamp' }))).status, 200);
      const stamp = (await db.ref('social/messages/alice/bob/serverTimestamp/sentAt').get()).val();
      assert.equal(typeof stamp, 'number'); assert.ok(Math.abs(stamp - Date.now()) < 10000);
      assert.equal((await send('forgedFuture', message(now + RETENTION_MS))).status, 401);
      assert.equal((await send('forgedPast', message(now - RETENTION_MS))).status, 401);
      assert.equal((await send('serverTimestamp', null)).status, 401, 'clients cannot delete or rewrite messages');
      assert.equal((await send('notFriends', { '.sv': 'timestamp' })).status, 401);
    } finally { await db.ref().remove(); await deleteApp(getApp('social-retention')); }
  });
