const { test } = require('node:test');
const assert = require('node:assert/strict');
const { fulfill } = require('./payment-core');

test('local HTTP endpoints reject unauthenticated purchases and forged webhooks',
  { skip: process.env.RUN_EMULATOR_TESTS !== '1' }, async () => {
    const base = 'http://127.0.0.1:5001/demo-logic-legends/us-central1/payments';
    assert.deepEqual(await (await fetch(base + '/health')).json(), { mode: 'test', storage: 'local-emulator' });
    for (const path of ['/checkout', '/webhook']) {
      const result = await fetch(base + path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{}' });
      assert.equal(result.status, 401, path);
    }
    assert.equal((await fetch(base + '/wallet')).status, 401);
  });

test('real emulator transaction grants exactly once under concurrent requests',
  { skip: process.env.RUN_EMULATOR_TESTS !== '1' }, async () => {
    process.env.FIREBASE_DATABASE_EMULATOR_HOST = '127.0.0.1:9000';
    const { initializeApp, deleteApp } = require('firebase-admin/app');
    const { getDatabase } = require('firebase-admin/database');
    const app = initializeApp({ projectId: 'demo-logic-legends',
      databaseURL: 'http://127.0.0.1:9000?ns=demo-logic-legends-default-rtdb' }, 'transaction-test');
    const ref = getDatabase(app).ref('automatedTests/' + Date.now());
    try {
      await ref.set({ orders: { test: { id: 'test', uid: 'test-user', sessionId: 'cs_test', productId: 'gems_5', amount: 100, gems: 5, currency: 'PHP', status: 'pending' } } });
      const session = { id: 'cs_test', attributes: { livemode: false, reference_number: 'test',
        line_items: [{ amount: 100, currency: 'PHP', quantity: 1 }],
        payments: [{ id: 'pay_test', attributes: { status: 'paid', livemode: false, amount: 100, currency: 'PHP' } }] } };
      await Promise.all(Array.from({ length: 5 }, () => ref.transaction(state => fulfill(state, 'test', session) || state || {})));
      const result = (await ref.get()).val();
      assert.equal(result.orders.test.status, 'paid');
      assert.equal(Object.values(result.wallets)[0].gems, 5);
    } finally { await ref.remove(); await deleteApp(app); }
  });
