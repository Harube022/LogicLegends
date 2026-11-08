const { fail, balance } = require('./wallet');
const { config, trialPolicy, baseAfterRetries } = require('./economy');
const quizBank = require('./quiz-questions.json');

const adminEmails = new Set(['sngbdsa137@gmail.com', 'russelbarrameda593@gmail.com', 'famatigabarrameda@gmail.com', 'gravata.joshuaharvey@ncst.edu.ph', 'cayangyang.jhayfel@ncst.edu.ph']);
const isAdminEmail = email => typeof email === 'string' && adminEmails.has(email.trim().toLowerCase());
const trialNumber = value => { if (!Number.isInteger(value) || value < 1 || value > 10) throw fail('Invalid trial.'); return String(value); };
const operationId = value => { if (!/^[a-f0-9]{32}$/.test(value || '')) throw fail('Invalid attempt request.'); return value; };
function rewardForRetries(retries, trial = 1) {
  if (!Number.isSafeInteger(retries) || retries < 0) throw fail('Invalid retry count.');
  return baseAfterRetries(trialPolicy(trial).baseReward, retries);
}
function policy(run, trial) {
  // Freeze this run's policy. Later tuning cannot change a pending payout.
  if (!run.policy) run.policy = { ...trialPolicy(trial), maxRetries: config.maxRetries,
    retryReductionPercent: config.retryReductionPercent, version: config.version };
  return run.policy;
}
function adjustedBase(run, trial) {
  const p = policy(run, trial);
  return Math.floor(p.baseReward * Math.max(0, 100 - Math.min(run.retries, p.maxRetries) * p.retryReductionPercent) / 100);
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
  policy(group.attempts[id], request.trial);
  group.active = id;
  return account;
}
function retryAttempt(account, request) {
  const run = active(account, request.trial, request.attemptId), id = operationId(request.operationId);
  if (run.status !== 'active') throw fail('Completed attempts cannot be retried.', 409);
  run.retryIds ||= {};
  if (!run.retryIds[id]) {
    if (run.retries >= policy(run, request.trial).maxRetries) throw fail('No Try Again attempts remain. Start Over or study in the Library.', 409);
    run.retryIds[id] = true; run.retries++;
  }
  return account;
}
function answerQuiz(account, request) {
  const run = active(account, request.trial, request.attemptId);
  if (run.status !== 'active') throw fail('This attempt has already ended.', 409);
  const event = request.eventKey;
  const valid = request.trial === 2 ? /^tt-c[1-3]-col[1-3]-house[0-5]$/ :
    request.trial === 3 ? /^roi-c[1-5]-(MP|MT|HS|DS|CD|SIMP|CONJ|ADD|DN)$/ : null;
  if (!valid || typeof event !== 'string' || !valid.test(event)) throw fail('Invalid quiz event.');
  const bank = request.trial === 2 ? quizBank.truthTable : quizBank.inference;
  const question = bank.find(q => q.id === request.questionId);
  if (!question || !Number.isInteger(request.choice) || request.choice < 0 || request.choice >= question.choices.length) throw fail('Invalid quiz answer.');
  run.quizAnswers ||= {};
  if (run.quizAnswers[event]) return account; // First answer locks the event, even when wrong.
  const p = policy(run, request.trial), correct = request.choice === question.correctChoice;
  const bonus = !run.adminUsed && correct ? Math.max(0, Math.min(p.quizReward, p.quizBonusLimit - (run.pendingBonus || 0))) : 0;
  run.quizAnswers[event] = { eventKey: event, questionId: question.id, choice: request.choice, correct, bonus };
  run.pendingBonus = (run.pendingBonus || 0) + bonus;
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
  const base = adjustedBase(run, request.trial);
  const reward = run.adminUsed ? 0 : base + (run.pendingBonus || 0);
  // The receipt and the existing wallet balance commit in ONE account transaction.
  account.coins = balance(balance(account.coins) + reward);
  run.reward = reward; run.status = 'completed'; run.completedAt = now;
  run.baseReward = policy(run, request.trial).baseReward;
  run.reduction = run.baseReward - base;
  run.quizBonus = run.adminUsed ? 0 : (run.pendingBonus || 0);
  return account;
}
function presentAttempt(account, trial, requestedId) {
  const group = state(account, trial);
  const id = requestedId && group.attempts[requestedId] ? requestedId : group.active;
  const run = group.attempts[id];
  if (!run) throw fail('No trial attempt is available.', 409);
  const p = run.policy || { ...trialPolicy(trial), maxRetries: config.maxRetries, retryReductionPercent: config.retryReductionPercent };
  const baseReward = run.baseReward ?? p.baseReward;
  const reduction = run.reduction ?? baseReward - Math.floor(baseReward * Math.max(0, 100 - Math.min(run.retries,p.maxRetries) * p.retryReductionPercent) / 100);
  return { economyVersion: config.version, attemptId: id, trial, retries: run.retries, adminUsed: run.adminUsed === true,
    baseReward, reduction, pendingBonus: run.pendingBonus || 0, quizBonus: run.quizBonus || 0,
    maxRetries: p.maxRetries, retryReductionPercent: p.retryReductionPercent,
    quizReward: p.quizReward, quizBonusLimit: p.quizBonusLimit,
    quizAnswers: Object.values(run.quizAnswers || {}),
    status: run.status, reward: run.reward || 0, coins: balance(account.coins) };
}
module.exports = { isAdminEmail, rewardForRetries, startAttempt, retryAttempt, answerQuiz, markAdmin, finishAttempt, presentAttempt };
