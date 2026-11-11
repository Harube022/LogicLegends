const { test } = require('node:test');
const assert = require('node:assert/strict');

test('live account policy uses trusted roles and moderation on every protected HTTP request', async () => {
  const previousHost = process.env.FIREBASE_DATABASE_EMULATOR_HOST;
  process.env.FIREBASE_DATABASE_EMULATOR_HOST = '127.0.0.1:9000';
  const { createApp } = require('./app');
  const { getApps, deleteApp } = require('firebase-admin/app');
  const make = (uid, email, extras = {}) => ({ uid, email, disabled: false, emailVerified: true, ...extras });
  const records = {
    player: make('player', 'player@NCST.EDU.PH'),
    outsider: make('outsider', 'player@ncst.edu.ph.example'),
    instructor: make('instructor', 'teacher@example.test', { customClaims: { instructor: true } }),
    website: make('website', 'web@example.test', { customClaims: { super_admin: true } }),
    game: make('game', 'legacy@example.test', { customClaims: { admin: true } }),
    unverified: make('unverified', 'unverified@example.test', { emailVerified: false, customClaims: { admin: true } }),
    disabled: make('disabled', 'disabled@ncst.edu.ph', { disabled: true })
  };
  const moderation = {};
  const env = { STORAGE_MODE: 'emulator', PAYMENT_MODE: 'test', FIREBASE_PROJECT_ID: 'demo-game-access',
    FIREBASE_DATABASE_EMULATOR_HOST: '127.0.0.1:9000' };
  const app = createApp(env, {
    verifyIdToken: async uid => { if (!records[uid]) throw Error('Invalid'); return { uid, email: 'fake@ncst.edu.ph', admin: true, auth_time: 100 }; },
    getUser: async uid => records[uid],
    getModeration: async uid => moderation[uid],
    now: () => 5000
  });
  const server = app.listen(0, '127.0.0.1');
  await new Promise(resolve => server.once('listening', resolve));
  const url = 'http://127.0.0.1:' + server.address().port;
  const call = async (uid, path = '/game/access', body) => {
    const res = await fetch(url + path, {
      method: body ? 'POST' : 'GET', headers: { Authorization: 'Bearer ' + uid, 'Content-Type': 'application/json' },
      ...(body ? { body: JSON.stringify(body) } : {}), signal: AbortSignal.timeout(5000)
    });
    return [res.status, await res.json()];
  };
  try {
    assert.equal((await call('invalid'))[0], 401);
    assert.deepEqual(await call('player'), [200, { uid: 'player', admin: false, feedbackRestricted: false, policyVersion: 1 }]);
    assert.equal((await call('outsider'))[1].code, 'DOMAIN_NOT_ALLOWED');
    assert.equal((await call('instructor'))[1].code, 'DOMAIN_NOT_ALLOWED');
    for (const uid of ['website', 'game']) assert.equal((await call(uid))[1].admin, true);
    assert.equal((await call('unverified'))[1].code, 'EMAIL_UNVERIFIED');
    assert.equal((await call('disabled'))[1].code, 'ACCOUNT_DISABLED');
    assert.equal((await call('player', '/trials/admin-used', { uid: 'website', admin: true }))[0], 403);
    moderation.player = { restriction: { active: true, expiresAt: 9000, capabilities: ['feedback', 'dashboard'] } };
    assert.equal((await call('player'))[1].feedbackRestricted, true);
    moderation.player.restriction.expiresAt = 4999;
    assert.equal((await call('player'))[1].feedbackRestricted, false);
    moderation.player.ban = { active: true };
    records.player.disabled = true;
    for (const [path, body] of [['/game/access'], ['/wallet'], ['/trials/start', {}], ['/multiplayer/complete', {}]])
      assert.equal((await call('player', path, body))[1].code, 'ACCOUNT_BANNED');
    records.website.customClaims = {};
    assert.equal((await call('website'))[1].code, 'DOMAIN_NOT_ALLOWED');
    records.game.tokensValidAfterTime = new Date(101000).toISOString();
    assert.equal((await call('game'))[0], 401);
  } finally {
    await new Promise(resolve => server.close(resolve));
    await Promise.all(getApps().map(deleteApp));
    if (previousHost === undefined) delete process.env.FIREBASE_DATABASE_EMULATOR_HOST;
    else process.env.FIREBASE_DATABASE_EMULATOR_HOST = previousHost;
  }
});
