const { test } = require('node:test');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const { isAdminEmail, rewardForRetries, startAttempt, retryAttempt, markAdmin, finishAttempt, presentAttempt } = require('./trials');
const id = () => crypto.randomBytes(16).toString('hex');
const profile = () => ({ coins: 37, gems: 19, unlockedStage: 8, inventory: { Common_Skin: true }, equipped: { clothes: 'Common_Skin' } });

test('reward compounds by 25%, floors only the final value and handles further retries', () => {
  assert.deepEqual(Array.from({ length: 7 }, (_, n) => rewardForRetries(n)), [1000, 750, 562, 421, 316, 237, 177]);
  assert.equal(rewardForRetries(10000), 0);
  for (const bad of [-1, 1.5, NaN, Infinity, '2']) assert.throws(() => rewardForRetries(bad));
});
test('all three authenticated Admin account emails normalize exactly; lookalikes are denied', () => {
  for (const email of ['sngbdsa137@gmail.com', 'russelbarrameda593@gmail.com', 'famatigabarrameda@gmail.com']) {
    assert.equal(isAdminEmail(email), true); assert.equal(isAdminEmail('  ' + email.toUpperCase() + ' '), true);
    assert.equal(isAdminEmail(email + '.attacker.test'), false);
  }
  for (const email of ['', null, 'ordinary@gmail.com', 'sngbdsa137+admin@gmail.com', 'famatigabrrameda@gmail.com']) assert.equal(isAdminEmail(email), false);
});
for (let trial = 1; trial <= 10; trial++) {
  test(`trial ${trial}: completion receipt, retries, reload and repeated results preserve the existing account`, () => {
    for (let retries = 0; retries < 7; retries++) {
      const account = profile(), request = { trial, attemptId: id(), newAttempt: true };
      startAttempt(account, request);
      for (let n = 0; n < retries; n++) {
        const retry = { ...request, operationId: id() };
        retryAttempt(account, retry); retryAttempt(account, retry); // Same network request retried after a timeout.
      }
      finishAttempt(account, request);
      const after = structuredClone(account);
      for (let duplicate = 0; duplicate < 5; duplicate++) { startAttempt(account, request); finishAttempt(account, request); }
      assert.deepEqual(account, after);
      assert.equal(account.coins, 37 + rewardForRetries(retries));
      assert.equal(account.gems, 19); assert.equal(account.unlockedStage, 8);
      assert.deepEqual(account.inventory, { Common_Skin: true });
      assert.equal(presentAttempt(account, trial).retries, retries);
      const reloaded = { trial, attemptId: id(), newAttempt: false };
      startAttempt(account, reloaded);
      assert.equal(presentAttempt(account, trial).attemptId, request.attemptId);
      assert.throws(() => retryAttempt(account, { ...request, operationId: id() }), /Completed/);
    }
  });
  test(`trial ${trial}: any Admin clear makes the entire attempt ineligible, including after retries/reload`, () => {
    const account = profile(), request = { trial, attemptId: id(), newAttempt: true };
    startAttempt(account, request); markAdmin(account, request);
    retryAttempt(account, { ...request, operationId: id() });
    startAttempt(account, { trial, attemptId: id(), newAttempt: false });
    finishAttempt(account, request); finishAttempt(account, request);
    assert.equal(account.coins, 37); assert.equal(presentAttempt(account, trial).reward, 0);
    assert.equal(presentAttempt(account, trial).adminUsed, true);
    const fresh = { trial, attemptId: id(), newAttempt: true };
    startAttempt(account, fresh); finishAttempt(account, fresh);
    assert.equal(account.coins, 1037);
    assert.throws(() => finishAttempt(account, request), /changed/);
  });
}
test('invalid requests and wallet overflow cannot produce a partial receipt', () => {
  for (const trial of [0, 11, 1.1, '1']) assert.throws(() => startAttempt(profile(), { trial, attemptId: id() }));
  assert.throws(() => startAttempt(profile(), { trial: 1, attemptId: '../fake' }));
  const account = profile(), request = { trial: 1, attemptId: id(), newAttempt: true };
  startAttempt(account, request); account.coins = 2147483647;
  assert.throws(() => finishAttempt(account, request), /balance/);
  assert.equal(presentAttempt(account, 1).status, 'active');
});
