const { test } = require('node:test');
const assert = require('node:assert/strict');
const { grant, buy, mutate } = require('./wallet');
test('database transactions prevent duplicate top-ups and overspending under concurrency',
  { skip: process.env.RUN_EMULATOR_TESTS !== '1' }, async () => {
    process.env.FIREBASE_DATABASE_EMULATOR_HOST = '127.0.0.1:9000';
    const { initializeApp, deleteApp } = require('firebase-admin/app');
    const { getDatabase } = require('firebase-admin/database');
    const app = initializeApp({ projectId: 'demo-logic-legends', databaseURL: 'http://127.0.0.1:9000?ns=demo-logic-legends-default-rtdb' }, 'commerce-test');
    const ref = getDatabase(app).ref('automatedTests/commerce/' + Date.now());
    try {
      await ref.set({ base_character: 'Male_Character', gems: 0 });
      await ref.get();
      const order = { id: 'one', gems: 5 };
      await Promise.all(Array.from({ length: 5 }, () => ref.transaction(a => a ? grant(a, order, { id: 'pay_one' }) : a)));
      assert.equal((await ref.child('gems').get()).val(), 5);
      await ref.transaction(a => a ? grant(a, { id: 'two', gems: 5 }, { id: 'pay_two' }) : a);
      const results = await Promise.allSettled(['Common_Skin', 'Elite_Skin'].map(id => mutate(ref, a => a ? buy(a, id) : a)));
      const value = (await ref.get()).val();
      assert.equal(value.gems, 0);
      assert.equal(Object.keys(value.inventory).length, 1);
      assert.equal(results.filter(r => r.status === 'fulfilled').length, 1);
      await ref.transaction(a => buy(a, Object.keys(value.inventory)[0]));
      assert.equal((await ref.child('gems').get()).val(), 0);
    } finally { await ref.remove(); await deleteApp(app); }
  });
