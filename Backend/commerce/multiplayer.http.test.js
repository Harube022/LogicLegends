const { test } = require('node:test');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');

test('authenticated multiplayer HTTP grants use atomic existing-wallet receipts and independent account milestones',
  { skip: process.env.RUN_EMULATOR_TESTS !== '1', timeout: 120000 }, async () => {
    process.env.FIREBASE_DATABASE_EMULATOR_HOST = '127.0.0.1:9000';
    const { createApp } = require('./app');
    const { getApp, deleteApp } = require('firebase-admin/app');
    const { getDatabase } = require('firebase-admin/database');
    const prefix = 'multiplayer-reward-check-' + crypto.randomBytes(6).toString('hex');
    const records = new Map(), uids = new Set();
    const id = () => crypto.randomBytes(16).toString('hex');
    const app = createApp({ STORAGE_MODE: 'emulator', PAYMENT_MODE: 'test', FIREBASE_DATABASE_EMULATOR_HOST: '127.0.0.1:9000',
      FIREBASE_PROJECT_ID: 'demo-logic-legends' }, {
      verifyIdToken: async token => { if (!records.has(token)) throw Error('Invalid token'); return { uid: records.get(token).uid }; },
      getUser: async uid => [...records.values()].find(user => user.uid === uid)
    });
    const server = app.listen(0, '127.0.0.1'); await new Promise(resolve => server.once('listening', resolve));
    const base = 'http://127.0.0.1:' + server.address().port;
    const db = getDatabase(getApp('commerce-data'));
    const call = (token, body, suffix = 'complete', method = 'POST') => fetch(base + '/multiplayer/' + suffix, { method,
      headers: { Authorization: 'Bearer ' + token, 'Content-Type': 'application/json' }, ...(method === 'POST' ? { body: JSON.stringify(body) } : {}) });
    const ok = async response => { const body = await response.json(); assert.equal(response.status, 200, JSON.stringify(body)); return body; };
    const report = (overrides = {}) => ({ version: 1, matchId: id(), actor: 1, online: true, adminUsed: false, outcome: 'victory',
      participants: [1, 2], completedTopics: Array.from({ length: 10 }, (_, i) => i),
      restoredTopics: Array.from({ length: 10 }, (_, i) => i),
      participationKeys: ['work:0:0:0', 'work:1:0:0', 'support:revive:2'], ...overrides });
    async function account(token, create = true) {
      const uid = prefix + '-' + token; uids.add(uid); records.set(token, { uid, disabled: false, email: 'player@ncst.edu.ph', emailVerified: true });
      const ref = db.ref('users/' + uid);
      if (create) await ref.set({ coins: 37, gems: 19, unlockedStage: 8, base_character: 'Male_Character',
        inventory: { Common_Skin: true }, trialRuns: { fixture: { keep: true } } });
      return ref;
    }
    try {
      assert.equal((await call('invalid', report())).status, 401);
      const first = await account('first'), second = await account('second');
      const request = report({ uid: records.get('second').uid, coins: 999999, gems: 999999, reward: 999999 });
      const replies = await Promise.all(Array.from({ length: 8 }, () => call('first', request).then(ok)));
      assert.ok(replies.every(reply => reply.reward === 1000 && reply.gemReward === 2));
      let saved = (await first.get()).val();
      assert.equal(saved.coins, 1037); assert.equal(saved.gems, 21);
      assert.equal(Object.keys(saved.multiplayerRewards).length, 1);
      assert.equal(saved.inventory.Common_Skin, true); assert.equal(saved.trialRuns.fixture.keep, true); assert.equal(saved.unlockedStage, 8);
      assert.equal((await second.child('coins').get()).val(), 37);
      assert.equal((await ok(await call('first', { matchId: request.matchId, reward: 999999, adminUsed: false }))).reward, 1000);
      assert.equal((await first.child('coins').get()).val(), 1037);
      const teammate = await ok(await call('second', { ...request, actor: 2, participationKeys: ['work:0:0:0', 'work:0:1:0', 'support:medkit:1'] }));
      assert.equal(teammate.reward, 1000); assert.equal(teammate.gemReward, 2);
      for (let cycle = 1; cycle <= 3; cycle++) {
        const rematch = report();
        await Promise.all([first.child('coins').transaction(value => value + 12),
          first.child('gems').transaction(value => value + 1),
          ...Array.from({ length: 8 }, () => call('first', rematch).then(ok))]);
        saved = (await first.get()).val();
        assert.equal(saved.coins, 1037 + 1012 * cycle); assert.equal(saved.gems, 21 + cycle);
        assert.equal(Object.keys(saved.multiplayerRewards).length, 1 + cycle);
        assert.equal(saved.inventory.Common_Skin, true); assert.equal(saved.trialRuns.fixture.keep, true);
        assert.equal(saved.unlockedStage, 8);
        assert.equal((await ok(await call('first', rematch))).gemReward, 0);
      }
      const parallel = await account('parallel');
      const simultaneous = [report(), report()];
      await Promise.all(simultaneous.flatMap(request => Array.from({ length: 4 }, () => call('parallel', request).then(ok))));
      const concurrentSaved = (await parallel.get()).val();
      assert.equal(concurrentSaved.coins, 2037); assert.equal(concurrentSaved.gems, 21);
      assert.equal(Object.keys(concurrentSaved.multiplayerRewards).length, 2);
      assert.equal(Object.values(concurrentSaved.multiplayerRewards).reduce((sum, receipt) => sum + receipt.gemReward, 0), 2);
      const excluded = await account('excluded');
      for (const changes of [{ adminUsed: true }, { adminUsed: true, completedTopics: [], restoredTopics: [] },
        { online: false }, { participationKeys: [] }]) {
        const reply = await ok(await call('excluded', report(changes)));
        assert.equal(reply.reward, 0); assert.equal(reply.gemReward, 0); assert.notEqual(reply.exclusionReason, '');
      }
      assert.equal((await excluded.child('coins').get()).val(), 37); assert.equal((await excluded.child('gems').get()).val(), 19);
      const defeat = await ok(await call('excluded', report({ outcome: 'defeat', completedTopics: [0, 1], restoredTopics: [] })));
      assert.equal(defeat.consolationCoins, 30); assert.equal(defeat.reward, 30); assert.equal(defeat.gemReward, 0);
      for (const bad of [{ outcome: 'active' }, { participants: [1, 1] }, { completedTopics: [] },
        { participationKeys: ['support:revive:1'] }, { restoredTopics: [0, 0] }])
        assert.ok([400, 409].includes((await call('excluded', report(bad))).status));
      records.get('second').disabled = true;
      assert.equal((await call('second', report())).status, 403);
      await account('uninitialized', false);
      assert.equal((await call('uninitialized', report())).status, 409);
      assert.equal((await call('first', {}, 'unknown')).status, 404);
      assert.equal((await call('first', null, 'complete', 'GET')).status, 404);
    } finally {
      await new Promise(resolve => server.close(resolve));
      await Promise.all([...uids].map(uid => db.ref('users/' + uid).remove()));
      await deleteApp(getApp('commerce-data')); await deleteApp(getApp('commerce-auth'));
    }
  });
