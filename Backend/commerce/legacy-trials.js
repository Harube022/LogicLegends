// Compatibility for the existing live build until SOLO_ECONOMY_V2_ENABLED=true.
// Uses the SAME account/wallet/attempt receipts; no migration or separate balance.
const { fail, balance } = require('./wallet');

const adminEmails = new Set(['sngbdsa137@gmail.com', 'russelbarrameda593@gmail.com', 'famatigabarrameda@gmail.com', 'gravata.joshuaharvey@ncst.edu.ph', 'cayangyang.jhayfel@ncst.edu.ph']);
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
  account.currentTrial = request.trial;
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
function recordProgressAnswer(account, request) {
  const run = active(account, request.trial, request.attemptId);
  if (run.status !== 'active') throw fail('This attempt has already ended.', 409);
  if (typeof request.eventKey !== 'string' || !/^[A-Za-z0-9_-]{1,80}$/.test(request.eventKey) ||
      typeof request.questionId !== 'string' || request.questionId.length > 100 ||
      typeof request.question !== 'string' || request.question.length > 1200 ||
      typeof request.correctAnswer !== 'string' || request.correctAnswer.length > 1200 ||
      typeof request.selectedAnswer !== 'string' || request.selectedAnswer.length > 1200 ||
      typeof request.correct !== 'boolean') throw fail('Invalid trial answer.');
  run.quizAnswers ||= {};
  if (!run.quizAnswers[request.eventKey]) run.quizAnswers[request.eventKey] = {
    eventKey: request.eventKey, kind: request.answerType || 'answer', questionId: request.questionId, question: request.question,
    correctAnswer: request.correctAnswer, selectedAnswer: request.selectedAnswer, correct: request.correct
  };
  return account;
}
function finishAttempt(account, request, now = Date.now()) {
  const run = active(account, request.trial, request.attemptId);
  if (run.status === 'completed') return account;
  if (run.status !== 'active') throw fail('This attempt cannot be completed.', 409);
  const allAnswers = Object.values(run.quizAnswers || {});
  const answers = request.trial === 2 ? allAnswers.filter(answer => answer.kind === 'table') : allAnswers;
  run.correctAnswers = answers.filter(answer => answer.correct === true).length;
  run.incorrectAnswers = answers.length - run.correctAnswers;
  run.accuracyPercent = answers.length ? Math.floor(run.correctAnswers * 100 / answers.length) : 0;
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
    quizAnswers: Object.values(run.quizAnswers || {}), status: run.status, reward: run.reward || 0, coins: balance(account.coins) };
}
module.exports = { isAdminEmail, rewardForRetries, startAttempt, retryAttempt, recordProgressAnswer, markAdmin, finishAttempt, presentAttempt };
