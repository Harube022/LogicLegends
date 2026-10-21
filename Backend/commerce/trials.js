const { fail, balance } = require('./wallet');

const adminEmails = new Set(['sngbdsa137@gmail.com', 'russelbarrameda593@gmail.com', 'famatigabrrameda@gmail.com']);
const isAdminEmail = email => typeof email === 'string' && adminEmails.has(email.trim().toLowerCase());
const trialNumber = value => { if (!Number.isInteger(value) || value < 1 || value > 10) throw fail('Invalid trial.'); return String(value); };
const operationId = value => { if (!/^[a-f0-9]{32}$/.test(value || '')) throw fail('Invalid attempt request.'); return value; };
function rewardForRetries(retries) {
  if (!Number.isSafeInteger(retries) || retries < 0) throw fail('Invalid retry count.');
  // Round the final compounded reward, not each intermediate percentage.
  return Math.floor(1000 * Math.pow(.75, retries));
}
function state(account, trial) {
  if (!account) throw fail('Please load your saved account first.', 409);
  account.trialRuns ||= {};
  return account.trialRuns[trialNumber(trial)] ||= { attempts: {} };
}
function active(account, trial, id) {
  const group = state(account, trial); operationId(id);
  if (group.active !== id || !group.attempts?.[id]) throw fail('This trial attempt has changed. Reopen the trial.', 409);
  return group.attempts[id];
}
function startAttempt(account, request, now = Date.now()) {
  const group = state(account, request.trial), id = operationId(request.attemptId);
  if (group.attempts[id]) return account; // Repeated new-run requests cannot erase their receipt.
  if (request.newAttempt !== true && group.active && group.attempts[group.active]) return account;
  group.attempts[id] = { retries: 0, status: 'active', adminUsed: false, startedAt: now };
  group.active = id;
  return account;
}
function retryAttempt(account, request) {
  const run = active(account, request.trial, request.attemptId), id = operationId(request.operationId);
  if (run.status !== 'active') throw fail('Completed attempts cannot be retried.', 409);
  run.retryIds ||= {};
  if (!run.retryIds[id]) { run.retryIds[id] = true; run.retries++; }
  return account;
}
function markAdmin(account, request) {
  const run = active(account, request.trial, request.attemptId);
  if (run.status !== 'active') throw fail('This attempt has already ended.', 409);
  run.adminUsed = true; return account;
}
function finishAttempt(account, request, now = Date.now()) {
  const run = active(account, request.trial, request.attemptId);
  if (run.status === 'completed') return account;
  if (run.status !== 'active') throw fail('This attempt cannot be completed.', 409);
  const reward = run.adminUsed ? 0 : rewardForRetries(run.retries);
  // The receipt and the existing wallet balance commit in ONE account transaction.
  account.coins = balance(balance(account.coins) + reward);
  run.reward = reward; run.status = 'completed'; run.completedAt = now;
  return account;
}
function presentAttempt(account, trial, requestedId) {
  const group = state(account, trial);
  const id = requestedId && group.attempts[requestedId] ? requestedId : group.active;
  const run = group.attempts[id];
  if (!run) throw fail('No trial attempt is available.', 409);
  return { attemptId: id, trial, retries: run.retries, adminUsed: run.adminUsed === true,
    status: run.status, reward: run.reward || 0, coins: balance(account.coins) };
}
module.exports = { isAdminEmail, rewardForRetries, startAttempt, retryAttempt, markAdmin, finishAttempt, presentAttempt };
