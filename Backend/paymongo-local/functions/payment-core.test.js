const { test } = require('node:test');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const { verifySignature, fulfill, assertLocal } = require('./payment-core');

const fixture = () => ({
  state: { orders: { order1: { id: 'order1', uid: 'player1', sessionId: 'cs_1', productId: 'gems_5', amount: 100, gems: 5, currency: 'PHP', status: 'pending' } } },
  session: { id: 'cs_1', attributes: { livemode: false, reference_number: 'order1',
    line_items: [{ amount: 100, currency: 'PHP', quantity: 1 }],
    payments: [{ id: 'pay_1', attributes: { status: 'paid', livemode: false, amount: 100, currency: 'PHP' } }] } }
});
test('valid payment grants five gems once, including duplicate deliveries', () => {
  const { state, session } = fixture();
  assert.equal(fulfill(state, 'order1', session), state);
  assert.equal(Object.values(state.wallets)[0].gems, 5);
  assert.equal(state.orders.order1.status, 'paid');
  assert.equal(fulfill(state, 'order1', session), undefined);
  assert.equal(Object.values(state.wallets)[0].gems, 5);
});
for (const [name, mutate] of [
  ['live session', s => s.attributes.livemode = true],
  ['wrong session', s => s.id = 'cs_other'],
  ['wrong reference', s => s.attributes.reference_number = 'other'],
  ['wrong amount', s => s.attributes.payments[0].attributes.amount = 1],
  ['wrong currency', s => s.attributes.payments[0].attributes.currency = 'USD'],
  ['unpaid', s => s.attributes.payments[0].attributes.status = 'pending'],
  ['live payment', s => s.attributes.payments[0].attributes.livemode = true],
  ['no payment', s => s.attributes.payments = []],
  ['wrong quantity', s => s.attributes.line_items[0].quantity = 2]
]) test('does not grant for ' + name, () => {
  const { state, session } = fixture(); mutate(session);
  assert.equal(fulfill(state, 'order1', session), undefined);
  assert.equal(state.wallets, undefined);
});
test('signature checks exact bytes, test signature and timestamp', () => {
  const raw = Buffer.from('{"data":1}');
  const t = '1700000000'; const secret = 'unit-test-secret';
  const sig = crypto.createHmac('sha256', secret).update(t + '.').update(raw).digest('hex');
  const header = `t=${t},te=${sig},li=`;
  assert.equal(verifySignature(raw, header, secret, Number(t) * 1000), true);
  assert.equal(verifySignature(Buffer.from('{"data":2}'), header, secret, Number(t) * 1000), false);
  assert.equal(verifySignature(raw, header, secret, Number(t) * 1000 + 301000), false);
  assert.equal(verifySignature(raw, `t=${t},te=,li=${sig}`, secret, Number(t) * 1000), false);
});
test('production storage and emulator authentication are refused', () => {
  const env = { FUNCTIONS_EMULATOR: 'true', FIREBASE_DATABASE_EMULATOR_HOST: '127.0.0.1:9000' };
  assert.doesNotThrow(() => assertLocal(env));
  assert.throws(() => assertLocal({}));
  assert.throws(() => assertLocal({ ...env, FIREBASE_DATABASE_EMULATOR_HOST: 'remote:9000' }));
  assert.throws(() => assertLocal({ ...env, FIREBASE_AUTH_EMULATOR_HOST: '127.0.0.1:9099' }));
});
