const { test } = require('node:test');
const assert = require('node:assert/strict');
const { lifecycle, CHECKOUT_LIFETIME_MS } = require('./order-lifecycle');
const { catalog } = require('./wallet');
const clone = value => value == null ? value : structuredClone(value);
function database(seed) {
  const values = clone(seed);
  const snapshot = value => ({ val: () => clone(value), exists: () => value != null,
    child: path => snapshot(path.split('/').reduce((v, k) => v?.[k], value)) });
  function ref(path) {
    const keys = path.split('/');
    const get = () => keys.reduce((v, k) => v?.[k], values);
    const set = value => { let parent = values; for (const k of keys.slice(0, -1)) parent = parent[k] ||= {}; parent[keys.at(-1)] = clone(value); };
    return { child: part => ref(path + '/' + part), get: async () => snapshot(get()),
      update: async patch => set({ ...get(), ...patch }),
      transaction: async fn => {
        // Reproduce the SDK's empty local-cache callback before server retry.
        if (fn(null) === undefined) return { committed: false, snapshot: snapshot(get()) };
        const value = fn(clone(get())); if (value !== undefined) set(value);
        return { committed: value !== undefined, snapshot: snapshot(get()) };
      },
      orderByChild: key => { let minimum, maximum, count; const query = {
        startAt: value => (minimum = value, query), endAt: value => (maximum = value, query), limitToFirst: value => (count = value, query),
        get: async () => snapshot(Object.fromEntries(Object.entries(get() || {}).filter(([, v]) => v[key] >= minimum && v[key] <= maximum).slice(0, count)))
      }; return query; }
    };
  }
  return { ref, values };
}
function fixture() {
  let time = 10000000, behavior = 'expire', expireCalls = 0;
  const order = { id: 'order', uid: 'owner', sessionId: 'cs_fixture', productId: 'gems_25', amount: 500, gems: 25,
    currency: 'PHP', live: false, createdAt: time, expiresAt: time + CHECKOUT_LIFETIME_MS, expiryCheckAt: time + CHECKOUT_LIFETIME_MS, status: 'pending' };
  const db = database({ users: { owner: { gems: 7, coins: 31 } }, orders: { order } });
  const session = { id: order.sessionId, attributes: { livemode: false, reference_number: order.id, status: 'active',
    line_items: [{ amount: order.amount, currency: 'PHP', quantity: 1 }], payments: [] } };
  const paid = () => session.attributes.payments = [{ id: 'pay_fixture', attributes: { amount: 500, currency: 'PHP', livemode: false, status: 'paid' } }];
  const api = lifecycle({ orders: db.ref('orders'), users: db.ref('users'), live: false, now: () => time,
    paymongo: async (path, body) => {
      if (body) {
        expireCalls++;
        assert.equal(path, 'v1/checkout_sessions/cs_fixture/expire');
        if (behavior === 'network') throw Error('unavailable');
        if (behavior === 'paid-race') paid();
        if (behavior === 'already-expired') session.attributes.status = 'expired';
        if (behavior !== 'expire') throw Object.assign(Error('cannot expire'), { providerStatus: 400 });
        session.attributes.status = 'expired';
      }
      return clone(session);
    } });
  return { api, db, order, session, paid, age: () => time += CHECKOUT_LIFETIME_MS,
    setBehavior: value => behavior = value, calls: () => expireCalls };
}
test('QR expiry policy closes only provider-confirmed sessions and retains Firebase history', async () => {
  const f = fixture();
  assert.equal((await f.api.reconcile(f.order)).status, 'pending'); assert.equal(f.calls(), 0);
  f.age(); await f.api.sweep();
  assert.equal(f.db.values.orders.order.status, 'expired');
  assert.equal(f.db.values.orders.order.expiryReason, 'checkout_timeout');
  assert.equal(f.db.values.users.owner.gems, 7);
  await f.api.sweep(); assert.equal(f.calls(), 1);
});
test('Cancel closes an unpaid checkout and records the player cancellation', async () => {
  const f = fixture();
  const result = await f.api.reconcile(f.order, true);
  assert.equal(result.status, 'expired'); assert.equal(result.expiryReason, 'player_cancelled');
  assert.equal(result.amount, 500); assert.equal(result.gems, 25); assert.equal(f.db.values.users.owner.coins, 31);
});
test('payment wins a cancellation race and grants the original pack exactly once', async () => {
  const f = fixture(); f.setBehavior('paid-race');
  assert.equal((await f.api.reconcile(f.order, true)).status, 'paid');
  await f.api.reconcile(f.order, true); // stale request/webhook replay
  assert.equal(f.db.values.users.owner.gems, 32);
  assert.equal(Object.keys(f.db.values.users.owner.paymentGrants).length, 1);
});
test('provider 400 while processing retains the pending order for verification', async () => {
  const f = fixture(); f.setBehavior('processing'); f.age();
  const result = await f.api.reconcile(f.order, true);
  assert.equal(result.status, 'processing'); assert.ok(result.expiryCheckAt > result.expiresAt);
  assert.equal(f.db.values.users.owner.gems, 7);
  f.paid(); await f.api.reconcile(result); assert.equal(f.db.values.users.owner.gems, 32);
});
test('network failures never pretend a checkout was cancelled', async () => {
  const f = fixture(); f.setBehavior('network');
  await assert.rejects(f.api.reconcile(f.order, true), /unavailable/);
  assert.equal(f.db.values.orders.order.status, 'pending'); assert.equal(f.db.values.users.owner.gems, 7);
});
test('already-expired 400 is retrieved and confirmed; old orders use their creation deadline', async () => {
  const f = fixture(); f.setBehavior('already-expired');
  delete f.order.expiresAt; delete f.order.expiryCheckAt; f.age();
  const result = await f.api.reconcile(f.order);
  assert.equal(result.status, 'expired'); assert.equal(result.expiryReason, 'checkout_timeout');
});
test('mismatched provider sessions cannot expire or credit an order', async () => {
  const f = fixture(); f.session.attributes.reference_number = 'another-order';
  await assert.rejects(f.api.reconcile(f.order, true), /verify/); assert.equal(f.calls(), 0);
  assert.equal(f.db.values.orders.order.status, 'pending');
});
test('six server-priced packs use PHP centavos and unique stable product IDs', () => {
  assert.equal(catalog.packs.length, 6); assert.equal(new Set(catalog.packs.map(p => p.id)).size, 6);
  assert.deepEqual(catalog.packs.map(p => [p.gems, p.amount]), [[5,100],[25,500],[50,1000],[100,2000],[250,5000],[500,10000]]);
  for (const pack of catalog.packs) { assert.equal(pack.currency, 'PHP'); assert.ok(Number.isSafeInteger(pack.amount)); }
});
