const { test } = require('node:test');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');

test('all ten trials: authenticated Admin policy and atomic reward receipts use the existing Firebase wallet',
  { skip: process.env.RUN_EMULATOR_TESTS !== '1', timeout: 120000 }, async () => {
    process.env.FIREBASE_DATABASE_EMULATOR_HOST = '127.0.0.1:9000';
    const { createApp } = require('./app');
    const { getApp, deleteApp } = require('firebase-admin/app');
    const { getDatabase } = require('firebase-admin/database');
    const prefix = 'trial-account-check-' + crypto.randomBytes(6).toString('hex');
    const records = new Map(), uids = new Set();
    const id = () => crypto.randomBytes(16).toString('hex');
    const app = createApp({ STORAGE_MODE: 'emulator', PAYMENT_MODE: 'test', FIREBASE_DATABASE_EMULATOR_HOST: '127.0.0.1:9000',
      FIREBASE_PROJECT_ID: 'logic-legends-ac66c' }, {
      verifyIdToken: async token => {
        if (!records.has(token)) throw Error('Invalid token');
        // Spoofed token email must never override the authoritative user record.
        return { uid: records.get(token).uid, email: 'sngbdsa137@gmail.com' };
      },
      getUser: async uid => [...records.values()].find(user => user.uid === uid)
    });
    const server = app.listen(0, '127.0.0.1'); await new Promise(resolve => server.once('listening', resolve));
    const base = 'http://127.0.0.1:' + server.address().port;
    const db = getDatabase(getApp('commerce-data'));
    const call = (token, path, body) => fetch(base + '/trials/' + path, { method: body ? 'POST' : 'GET',
      headers: { Authorization: 'Bearer ' + token, 'Content-Type': 'application/json' }, ...(body ? { body: JSON.stringify(body) } : {}) });
    const ok = async response => { const body = await response.json(); assert.equal(response.status, 200, JSON.stringify(body)); return body; };
    async function account(name, email) {
      const uid = prefix + '-' + name; uids.add(uid); records.set(name, { uid, email, disabled: false });
      await db.ref('users/' + uid).set({ coins: 37, gems: 19, unlockedStage: 8, base_character: 'Male_Character', inventory: { Common_Skin: true } });
      return db.ref('users/' + uid);
    }
    try {
      assert.equal((await call('missing', 'access')).status, 401);
      await account('ordinary', 'ordinary@example.test');
      assert.equal((await ok(await call('ordinary', 'access'))).admin, false);
      for (const [i, email] of ['sngbdsa137@gmail.com', 'russelbarrameda593@gmail.com', 'famatigabarrameda@gmail.com'].entries()) {
        const token = 'admin-' + i; await account(token, '  ' + email.toUpperCase() + '  ');
        assert.equal((await ok(await call(token, 'access'))).admin, true);
        for (let trial = 1; trial <= 10; trial++) {
          const request = { trial, attemptId: id(), newAttempt: true };
          await ok(await call(token, 'start', request)); await ok(await call(token, 'admin-used', request));
          assert.equal((await ok(await call(token, 'complete', request))).reward, 0);
        }
        assert.equal((await db.ref('users/' + records.get(token).uid + '/coins').get()).val(), 37);
      }
      for (let trial = 1; trial <= 10; trial++) {
        for (let retries = 0; retries <= 2; retries++) {
          const token = 'player-' + trial + '-' + retries, ref = await account(token, 'ordinary@example.test');
          const request = { trial, attemptId: id(), newAttempt: true, email: 'sngbdsa137@gmail.com', reward: 999999 };
          await ok(await call(token, 'start', request));
          assert.equal((await call(token, 'admin-used', request)).status, 403);
          for (let n = 0; n < retries; n++) {
            const retry = { ...request, operationId: id() };
            await Promise.all([call(token, 'retry', retry).then(ok), call(token, 'retry', retry).then(ok)]);
          }
          const replies = await Promise.all(Array.from({ length: 6 }, () => call(token, 'complete', request).then(ok)));
          assert.ok(replies.every(reply => reply.reward === [1000, 750, 562][retries]));
          const reloaded = await ok(await call(token, 'start', { trial, attemptId: id(), newAttempt: false }));
          assert.equal(reloaded.attemptId, request.attemptId);
          const saved = (await ref.get()).val();
          assert.equal(saved.coins, 37 + [1000, 750, 562][retries]);
          assert.equal(saved.gems, 19); assert.equal(saved.unlockedStage, 8); assert.equal(saved.inventory.Common_Skin, true);
        }
      }
      const token = 'concurrent', ref = await account(token, 'ordinary@example.test');
      const request = { trial: 9, attemptId: id(), newAttempt: true };
      await ok(await call(token, 'start', request));
      await Promise.all([ref.child('coins').transaction(value => value + 12),
        ...Array.from({ length: 12 }, () => call(token, 'complete', request).then(ok))]);
      assert.equal((await ref.child('coins').get()).val(), 1049);
      // Logout/account switch, an ordinary account, and a disabled allowed account cannot inherit Admin.
      assert.equal((await ok(await call('ordinary', 'access'))).admin, false);
      records.get('admin-0').disabled = true;
      assert.equal((await ok(await call('admin-0', 'access'))).admin, false);
      records.get('admin-1').email = 'ordinary@example.test';
      assert.equal((await ok(await call('admin-1', 'access'))).admin, false);
    } finally {
      await new Promise(resolve => server.close(resolve));
      await Promise.all([...uids].map(uid => db.ref('users/' + uid).remove()));
      await deleteApp(getApp('commerce-data')); await deleteApp(getApp('commerce-auth'));
    }
  });
