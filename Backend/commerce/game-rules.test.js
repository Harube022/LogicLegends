const { test } = require('node:test');
const assert = require('node:assert/strict');
const { patchGameRules } = require('./patch-game-rules');
test('game rules patch is repeatable and preserves indexes and validations', () => {
  const rules = require('./database.rules.json');
  const patched = patchGameRules(structuredClone(rules));
  assert.deepEqual(patchGameRules(structuredClone(patched)), patched);
  assert.deepEqual(patched.rules.commerceOrders, rules.rules.commerceOrders);
  assert.equal(patched.rules.users.$uid.coins['.validate'], rules.rules.users.$uid.coins['.validate']);
  assert.throws(() => patchGameRules({ rules: { '.read': true, users: {}, social: {} } }), /database-wide grant/);
  assert.throws(() => patchGameRules({ rules: { website: { feedback: { '.read': 'auth != null' } }, users: {}, social: {} } }), /website client grants/);
});

test('Firebase enforces eligible tokens and moderation on users and social; website records stay private',
  { skip: process.env.RUN_EMULATOR_TESTS !== '1' }, async () => {
    const ns = 'demo-game-account-rules';
    const base = 'http://127.0.0.1:9000';
    const rules = patchGameRules(structuredClone(require('./database.rules.json')));
    const owner = { Authorization: 'Bearer owner', 'Content-Type': 'application/json' };
    const install = await fetch(`${base}/.settings/rules.json?ns=${ns}`, { method: 'PUT', headers: owner, body: JSON.stringify(rules) });
    assert.equal(install.status, 200, await install.text());
    const now = Math.floor(Date.now() / 1000);
    const token = (extra = {}) => Buffer.from(JSON.stringify({ alg: 'none', typ: 'JWT' })).toString('base64url') + '.' +
      Buffer.from(JSON.stringify({ aud: ns, iss: 'https://securetoken.google.com/' + ns, sub: 'alice', user_id: 'alice',
        auth_time: now, iat: now, exp: now + 3600, email: 'alice@ncst.edu.ph', email_verified: true,
        firebase: { sign_in_provider: 'password', identities: {} }, ...extra })).toString('base64url') + '.';
    const call = (path, { method = 'GET', body, claims } = {}) =>
      fetch(`${base}/${path}.json?ns=${ns}&auth=${token(claims)}`, { method, headers: { 'Content-Type': 'application/json' }, body: body == null ? undefined : JSON.stringify(body) });
    const set = (path, body) => fetch(`${base}/${path}.json?ns=${ns}`, { method: 'PUT', headers: owner, body: JSON.stringify(body) });
    try {
      assert.equal((await set('users/alice', { username: 'Alice', coins: 20, gems: 10 })).status, 200);
      assert.equal((await call('users/alice')).status, 200);
      assert.equal((await call('users/alice/coins', { method: 'PUT', body: 25 })).status, 200);
      for (const claims of [{ email: 'alice@gmail.com' }, { email_verified: false }, { email: 'alice@ncst.edu.ph.fake' }])
        assert.equal((await call('users/alice', { claims })).status, 401);
      const admin = { email: 'admin@gmail.com', admin: true };
      assert.equal((await call('users/alice', { claims: admin })).status, 200);
      assert.equal((await call('website/feedback', { claims: admin })).status, 401);
      assert.equal((await call('website/moderation/alice/ban', { method: 'PUT', body: true })).status, 401);
      assert.equal((await call('users/alice/admin', { method: 'PUT', body: true })).status, 401);
      await set('website/moderation/alice', { ban: { active: true } });
      assert.equal((await call('users/alice/coins', { method: 'PUT', body: 26 })).status, 401);
      assert.equal((await call('social/friends/alice')).status, 401);
      await set('website/moderation/alice', { sessionsRevokedAt: now });
      assert.equal((await call('users/alice')).status, 401);
    } finally { await set('website/moderation/alice', null); await set('users/alice', null); }
  });
