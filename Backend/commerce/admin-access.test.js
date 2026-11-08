const { test } = require('node:test');
const assert = require('node:assert/strict');

test('admin access requires a verified, enabled authoritative account for all five emails', async () => {
  const previousHost = process.env.FIREBASE_DATABASE_EMULATOR_HOST;
  process.env.FIREBASE_DATABASE_EMULATOR_HOST = '127.0.0.1:9000';
  const { createApp } = require('./app');
  const { getApps, deleteApp } = require('firebase-admin/app');
  const emails = ['sngbdsa137@gmail.com', 'russelbarrameda593@gmail.com', 'famatigabarrameda@gmail.com',
    'gravata.joshuaharvey@ncst.edu.ph', 'cayangyang.jhayfel@ncst.edu.ph'];
  assert.equal(emails.length, 5);
  const records = new Map();
  for (const [i, email] of emails.entries()) {
    records.set('admin-' + i, { uid: 'fixture-admin-' + i, email: ' ' + email.toUpperCase() + ' ', disabled: false, emailVerified: true });
    records.set('unverified-' + i, { uid: 'fixture-unverified-' + i, email, disabled: false, emailVerified: false });
  }
  records.set('disabled', { uid: 'fixture-disabled', email: emails[0], disabled: true, emailVerified: true });
  records.set('ordinary', { uid: 'fixture-player', email: 'ordinary@example.invalid', disabled: false, emailVerified: true });
  // Token emails deliberately spoof an admin. The fetched user record must win.
  const app = createApp({ STORAGE_MODE: 'emulator', PAYMENT_MODE: 'test', FIREBASE_PROJECT_ID: 'demo-admin-access',
    FIREBASE_DATABASE_EMULATOR_HOST: '127.0.0.1:9000', SOLO_ECONOMY_V2_ENABLED: 'true' }, {
    verifyIdToken: async token => { if (!records.has(token)) throw Error('Invalid fixture'); return { uid: records.get(token).uid, email: emails[0] }; },
    getUser: async uid => [...records.values()].find(user => user.uid === uid)
  });
  const server = app.listen(0, '127.0.0.1');
  await new Promise(resolve => server.once('listening', resolve));
  try {
    const url = 'http://127.0.0.1:' + server.address().port + '/trials/';
    const call = (token, action = 'access', body) => fetch(url + action, {
      method: body ? 'POST' : 'GET', headers: { Authorization: 'Bearer ' + token, 'Content-Type': 'application/json' },
      ...(body ? { body: JSON.stringify(body) } : {}), signal: AbortSignal.timeout(5000)
    });
    assert.equal((await call('invalid')).status, 401);
    for (const [token, user] of records) {
      const response = await call(token);
      assert.equal(response.status, 200);
      assert.deepEqual(await response.json(), { admin: token.startsWith('admin-'), uid: user.uid });
      if (!token.startsWith('admin-')) assert.equal((await call(token, 'admin-used', { trial: 1, attemptId: 'a'.repeat(32) })).status, user.disabled ? 401 : 403);
    }
  } finally {
    await new Promise(resolve => server.close(resolve));
    await Promise.all(getApps().map(deleteApp));
    if (previousHost === undefined) delete process.env.FIREBASE_DATABASE_EMULATOR_HOST;
    else process.env.FIREBASE_DATABASE_EMULATOR_HOST = previousHost;
  }
});
