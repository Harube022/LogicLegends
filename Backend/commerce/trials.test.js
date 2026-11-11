const { test } = require('node:test');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const { rewardForRetries, startAttempt, retryAttempt, markAdmin, finishAttempt, presentAttempt } = require('./trials');
const id = () => crypto.randomBytes(16).toString('hex');
const profile = () => ({ coins: 37, gems: 19, unlockedStage: 8, inventory: { Common_Skin: true }, equipped: { clothes: 'Common_Skin' } });

test('reward reductions use the original base and cap at the two-retry policy', () => {
  assert.deepEqual(Array.from({ length: 7 }, (_, n) => rewardForRetries(n)), [1000, 750, 500, 500, 500, 500, 500]);
  assert.equal(rewardForRetries(10000), 500);
  for (const bad of [-1, 1.5, NaN, Infinity, '2']) assert.throws(() => rewardForRetries(bad));
});

for (let trial = 1; trial <= 10; trial++) {
  test(`trial ${trial}: completion receipt, retries, reload and repeated results preserve the existing account`, () => {
    for (let retries = 0; retries <= 2; retries++) {
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
