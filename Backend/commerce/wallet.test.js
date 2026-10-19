const { test } = require('node:test');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const { signature, validatePayment, grant, buy, present } = require('./wallet');
const order = { id: 'order1', uid: 'player1', sessionId: 'cs_1', amount: 100, currency: 'PHP', gems: 5, live: false };
const session = () => ({ id: 'cs_1', attributes: { livemode: false, reference_number: 'order1',
  line_items: [{ amount: 100, currency: 'PHP', quantity: 1 }],
  payments: [{ id: 'pay_1', attributes: { amount: 100, currency: 'PHP', status: 'paid', livemode: false } }] } });
test('two verified top-ups can buy an outfit without duplicate credits or spending', () => {
  let account = { base_character: 'Male_Character', gems: 0 };
  const payment = validatePayment(session(), order, false);
  assert.ok(payment);
  grant(account, order, payment);
  grant(account, order, payment);
  assert.equal(account.gems, 5);
  assert.throws(() => buy(account, 'Common_Skin'), /Not enough/);
  grant(account, { ...order, id: 'order2' }, { id: 'pay_2' });
  buy(account, 'Common_Skin');
  buy(account, 'Common_Skin');
  assert.equal(account.gems, 0);
  assert.equal(account.inventory.Common_Skin, true);
  assert.deepEqual(present(account, true, false).inventory, ['Common_Skin']);
});
test('price and eligibility come from server catalog', () => {
  const account = { base_character: 'Male_Character', gems: 100 };
  assert.throws(() => buy(account, 'unknown'), /unavailable/);
  assert.throws(() => buy(account, 'Common_Costume'), /character/);
  buy(account, 'Rare_Skin');
  assert.equal(account.gems, 90);
});
test('invalid balances and overflow cannot be granted or spent', () => {
  for (const value of [-1, '100', 1.5, 2147483648]) {
    assert.throws(() => grant({ gems: value }, order, { id: 'pay_1' }), /balance/);
  }
  assert.throws(() => grant({ gems: 2147483647 }, order, { id: 'pay_1' }), /balance/);
});
for (const [name, mutate] of [
  ['live mode', s => s.attributes.livemode = true], ['session', s => s.id = 'other'],
  ['reference', s => s.attributes.reference_number = 'other'], ['price', s => s.attributes.line_items[0].amount = 1],
  ['quantity', s => s.attributes.line_items[0].quantity = 2], ['currency', s => s.attributes.payments[0].attributes.currency = 'USD'],
  ['unpaid', s => s.attributes.payments[0].attributes.status = 'pending'], ['payment mode', s => s.attributes.payments[0].attributes.livemode = true]
]) test('reject mismatched payment ' + name, () => { const value = session(); mutate(value); assert.equal(validatePayment(value, order, false), null); });
test('signature verifies raw bytes, mode, and timestamp', () => {
  const raw = Buffer.from('{"test":1}'); const secret = 'test-signing-secret'; const t = '1700000000';
  const sig = crypto.createHmac('sha256', secret).update(t + '.').update(raw).digest('hex');
  const header = `t=${t},te=${sig},li=`;
  assert.ok(signature(raw, header, secret, false, Number(t) * 1000));
  assert.equal(signature(raw, header, secret, true, Number(t) * 1000), false);
  assert.equal(signature(Buffer.from('{}'), header, secret, false, Number(t) * 1000), false);
  assert.equal(signature(raw, header, secret, false, Number(t) * 1000 + 301000), false);
});
test('live payment validates only against live order and live runtime', () => {
  const value = session(); value.attributes.livemode = true; value.attributes.payments[0].attributes.livemode = true;
  assert.ok(validatePayment(value, { ...order, live: true }, true));
  assert.equal(validatePayment(value, order, true), null);
  assert.equal(validatePayment(value, { ...order, live: true }, false), null);
});
