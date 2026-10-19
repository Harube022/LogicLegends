const { test } = require('node:test');
const assert = require('node:assert/strict');
test('protected rules allow owner reads and owned equips but deny currency, inventory and order writes',
  { skip: process.env.RUN_EMULATOR_TESTS !== '1' }, async () => {
    const ns = 'demo-commerce-rules';
    const base = 'http://127.0.0.1:9000';
    const rules = require('./database.rules.json');
    const owner = { Authorization: 'Bearer owner', 'Content-Type': 'application/json' };
    assert.equal((await fetch(`${base}/.settings/rules.json?ns=${ns}`, { method: 'PUT', headers: owner, body: JSON.stringify(rules) })).status, 200);
    const now = Math.floor(Date.now() / 1000);
    const token = uid => Buffer.from(JSON.stringify({ alg: 'none', typ: 'JWT' })).toString('base64url') + '.' +
      Buffer.from(JSON.stringify({ aud: ns, iss: 'https://securetoken.google.com/' + ns, sub: uid, user_id: uid,
        auth_time: now, iat: now, exp: now + 3600, firebase: { sign_in_provider: 'custom', identities: {} } })).toString('base64url') + '.';
    const call = (path, method = 'GET', value, uid = 'alice') => fetch(`${base}/${path}.json?ns=${ns}&auth=${token(uid)}`, {
      method, headers: { 'Content-Type': 'application/json' }, ...(value === undefined ? {} : { body: JSON.stringify(value) }) });
    try {
      await fetch(`${base}/users/alice.json?ns=${ns}`, { method: 'PUT', headers: owner,
        body: JSON.stringify({ gems: 10, coins: 20, base_character: 'Male_Character', inventory: { Common_Skin: true } }) });
      assert.equal((await call('users/alice/gems')).status, 200);
      assert.equal((await call('users/alice/gems', 'GET', undefined, 'bob')).status, 401);
      for (const [path, value] of [ ['users/alice/gems', 999], ['users/alice/coins', 999],
        ['users/alice/inventory/Elite_Skin', true], ['users/alice/paymentGrants/fake', { gems: 500 }],
        ['commerceOrders/fake', { status: 'paid' }], ['users/alice', { gems: 999 }] ])
        assert.equal((await call(path, 'PUT', value)).status, 401, path);
      assert.equal((await call('users/alice/equipped/clothes', 'PUT', 'Common_Skin')).status, 200);
      assert.equal((await call('users/alice/equipped/clothes', 'PUT', 'Elite_Skin')).status, 401);
    } finally { await fetch(`${base}/users/alice.json?ns=${ns}`, { method: 'DELETE', headers: owner }); }
  });
