const { fail, balance } = require('./wallet');
const { config, trialPolicy, baseAfterRetries } = require('./economy');
const quizBank = require('./quiz-questions.json');

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
  account.currentTrial = request.trial;
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
  if (typeof event !== 'string' || !/^[A-Za-z0-9_-]{1,80}$/.test(event)) throw fail('Invalid quiz event.');
  let prompt, correctAnswer, selectedAnswer, isCorrect;
  const answerType = request.answerType || (request.trial === 2 || request.trial === 3 ? 'quiz' : 'answer');
  if (answerType === 'table') {
    if (request.trial !== 2 || !/^tt-table-[a-f0-9]{32}$/.test(event) ||
        typeof request.questionId !== 'string' || !/^tt-c[1-3]-col[1-3]$/.test(request.questionId) ||
        typeof request.question !== 'string' || request.question.length > 1200 ||
        typeof request.correctAnswer !== 'string' || request.correctAnswer.length > 1200 ||
        typeof request.selectedAnswer !== 'string' || request.selectedAnswer.length > 1200 ||
        typeof request.correct !== 'boolean') throw fail('Invalid truth-table answer.');
    prompt = request.question; correctAnswer = request.correctAnswer;
    selectedAnswer = request.selectedAnswer; isCorrect = request.correct;
  } else if (answerType === 'quiz') {
    if (request.trial !== 2 && request.trial !== 3) throw fail('Invalid quiz event.');
    const valid = request.trial === 2 ? /^tt-c[1-3]-col[1-3]-house[0-5]$/ : /^roi-c[1-5]-(MP|MT|HS|DS|CD|SIMP|CONJ|ADD|DN)$/;
    if (!valid.test(event)) throw fail('Invalid quiz event.');
    const bank = request.trial === 2 ? quizBank.truthTable : quizBank.inference;
    const question = bank.find(q => q.id === request.questionId);
    if (!question || !Number.isInteger(request.choice) || request.choice < 0 || request.choice >= question.choices.length) throw fail('Invalid quiz answer.');
    prompt = question.prompt; correctAnswer = question.choices[question.correctChoice];
    selectedAnswer = question.choices[request.choice]; isCorrect = request.choice === question.correctChoice;
  } else if (answerType === 'answer') {
    if (!Number.isSafeInteger(request.trial) || request.trial < 1 || request.trial > 10 ||
        typeof request.questionId !== 'string' || request.questionId.length > 100 ||
        typeof request.question !== 'string' || request.question.length > 1200 ||
        typeof request.correctAnswer !== 'string' || request.correctAnswer.length > 1200 ||
        typeof request.selectedAnswer !== 'string' || request.selectedAnswer.length > 1200 ||
        typeof request.correct !== 'boolean') throw fail('Invalid trial answer.');
    prompt = request.question; correctAnswer = request.correctAnswer;
    selectedAnswer = request.selectedAnswer; isCorrect = request.correct;
  } else {
    throw fail('Invalid trial answer.');
  }
  run.quizAnswers ||= {};
  if (run.quizAnswers[event]) return account; // First answer locks the event, even when wrong.
  const p = policy(run, request.trial), eligibleBonus = answerType === 'quiz' && (request.trial === 2 || request.trial === 3);
  const bonus = eligibleBonus && !run.adminUsed && isCorrect ? Math.max(0, Math.min(p.quizReward, p.quizBonusLimit - (run.pendingBonus || 0))) : 0;
  run.quizAnswers[event] = { eventKey: event, kind: answerType, questionId: request.questionId, question: prompt,
    correctAnswer, selectedAnswer, choice: request.choice, correct: isCorrect, bonus };
  if (answerType === 'quiz' && eligibleBonus) run.pendingBonus = (run.pendingBonus || 0) + bonus;
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
  const allAnswers = Object.values(run.quizAnswers || {});
  const answers = request.trial === 2 ? allAnswers.filter(answer => answer.kind === 'table') : allAnswers;
  run.correctAnswers = answers.filter(answer => answer.correct === true).length;
  run.incorrectAnswers = answers.length - run.correctAnswers;
  run.accuracyPercent = answers.length ? Math.floor(run.correctAnswers * 100 / answers.length) : 0;
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
module.exports = { rewardForRetries, startAttempt, retryAttempt, answerQuiz, markAdmin, finishAttempt, presentAttempt };
