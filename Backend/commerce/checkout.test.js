const { test } = require('node:test');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
test('HTTP checkout, signed fulfillment, wallet, purchase and equip work together',
  { skip: process.env.RUN_EMULATOR_TESTS !== '1' }, async () => {
    process.env.FIREBASE_DATABASE_EMULATOR_HOST = '127.0.0.1:9000';
    const { createApp } = require('./app');
    const { getApp, deleteApp } = require('firebase-admin/app');
    const { getDatabase } = require('firebase-admin/database');
    const uid = 'automated-commerce-' + Date.now();
    const secret = 'fixture-signing-secret';
    const sessions = new Map();
    const env = { STORAGE_MODE: 'emulator', PAYMENT_MODE: 'test', FIREBASE_DATABASE_EMULATOR_HOST: '127.0.0.1:9000',
      FIREBASE_PROJECT_ID: 'logic-legends-ac66c', PUBLIC_BASE_URL: 'https://fixture.example',
      PAYMONGO_SECRET_KEY: 'sk_test_fixture', PAYMONGO_WEBHOOK_SECRET: secret };
    const app = createApp(env, {
      verifyIdToken: async token => { if (!token) throw Error(); return { uid: token === 'fixture' ? uid : uid + '-other' }; },
      paymongo: async (path, body) => {
        if (!body) return sessions.get(path.split('/').pop());
        const id = 'cs_' + sessions.size;
        const session = { id, attributes: { ...body, livemode: false, checkout_url: 'https://checkout.paymongo.com/' + id,
          payments: [{ id: 'pay_' + id, attributes: { amount: 100, currency: 'PHP', livemode: false, status: 'paid' } }] } };
        sessions.set(id, session); return session;
      }
    });
    const server = app.listen(0, '127.0.0.1');
    await new Promise(resolve => server.once('listening', resolve));
    const base = 'http://127.0.0.1:' + server.address().port;
    const orders = [];
    const request = (path, body, token = 'fixture') => fetch(base + path, { method: body ? 'POST' : 'GET',
      headers: { Authorization: 'Bearer ' + token, 'Content-Type': 'application/json' }, ...(body ? { body: JSON.stringify(body) } : {}) });
    try {
      assert.equal((await fetch(base + '/wallet')).status, 401);
      const initial = await (await request('/account', { character: 'Male_Character', gems: 9999 })).json();
      assert.equal(initial.gems, 0);
      env.CHECKOUT_ENABLED = 'false';
      assert.equal((await (await request('/wallet')).json()).paymentsEnabled, false);
      assert.equal((await request('/checkout', { productId: 'gems_5', requestId: crypto.randomBytes(16).toString('hex') })).status, 503);
      assert.equal(sessions.size, 0);
      for (let n = 0; n < 2; n++) {
        env.CHECKOUT_ENABLED = 'true';
        const input = { productId: 'gems_5', requestId: crypto.randomBytes(16).toString('hex'), amount: 1, gems: 9999 };
        const checkout = await (await request('/checkout', input)).json(); orders.push(checkout.orderId);
        const duplicate = await (await request('/checkout', input)).json();
        assert.equal(duplicate.orderId, checkout.orderId);
        assert.equal(sessions.size, n + 1);
        env.CHECKOUT_ENABLED = 'false'; // Existing payments must still fulfill while checkout is paused.
        assert.equal((await request('/orders/' + checkout.orderId, null, 'someone-else')).status, 404);
        const session = sessions.get('cs_' + n);
        const body = JSON.stringify({ data: { attributes: { type: 'checkout_session.payment.paid', data: session } } });
        const t = Math.floor(Date.now() / 1000).toString();
        const sig = crypto.createHmac('sha256', secret).update(t + '.' + body).digest('hex');
        assert.equal((await fetch(base + '/webhook', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body })).status, 401);
        for (let retry = 0; retry < 2; retry++) assert.equal((await fetch(base + '/webhook', { method: 'POST',
          headers: { 'Content-Type': 'application/json', 'Paymongo-Signature': `t=${t},te=${sig},li=` }, body })).status, 200);
      }
      assert.equal((await (await request('/wallet')).json()).gems, 10);
      assert.equal((await request('/purchase', { itemId: 'Common_Costume' })).status, 409);
      const owned = await (await request('/purchase', { itemId: 'Common_Skin', price: 0 })).json();
      assert.equal(owned.gems, 0); assert.deepEqual(owned.inventory, ['Common_Skin']);
      assert.equal((await (await request('/purchase', { itemId: 'Common_Skin' })).json()).gems, 0);
      assert.equal((await request('/purchase', { itemId: 'Elite_Skin' })).status, 409);
      assert.equal((await request('/equip', { itemId: 'Elite_Skin' })).status, 409);
      assert.equal((await (await request('/equip', { itemId: 'Common_Skin' })).json()).equippedClothes, 'Common_Skin');
    } finally {
      await new Promise(resolve => server.close(resolve));
      const db = getDatabase(getApp('commerce-data'));
      await db.ref('users/' + uid).remove();
      for (const id of orders) await db.ref('commerceOrders/' + id).remove();
      await deleteApp(getApp('commerce-data')); await deleteApp(getApp('commerce-auth'));
    }
  });
