const { fail, balance } = require('./wallet');

const rounds = [5, 9, 5, 5, 5, 5, 10, 5, 5, 5];
const matchId = value => {
  if (typeof value !== 'string' || !/^[a-f0-9]{32}$/.test(value)) throw fail('Invalid multiplayer match.');
  return value;
};
const actorId = value => Number.isSafeInteger(value) && value > 0 && value <= 2147483647;
function uniqueNumbers(value, min, max, label) {
  if (!Array.isArray(value) || value.length < min || value.length > max ||
      value.some(v => !Number.isSafeInteger(v)) || new Set(value).size !== value.length) throw fail('Invalid ' + label + '.');
  return value;
}
function terminalReport(request) {
  matchId(request.matchId);
  if (![1, 2].includes(request.version) || !actorId(request.actor) || typeof request.online !== 'boolean' ||
      typeof request.adminUsed !== 'boolean' || !['victory', 'defeat'].includes(request.outcome)) throw fail('Invalid terminal multiplayer report.');
  const participants = uniqueNumbers(request.participants, 2, 4, 'multiplayer roster');
  if (!participants.every(actorId) || !participants.includes(request.actor)) throw fail('Invalid multiplayer participant.');
  const completed = uniqueNumbers(request.completedTopics, 0, 10, 'completed topics');
  if (completed.some(t => t < 0 || t > 9)) throw fail('Invalid completed topic.');
  const restored = uniqueNumbers(request.restoredTopics, 0, 10, 'restored crystals');
  if (restored.some(t => !completed.includes(t))) throw fail('A restored crystal requires its completed topic.');
  if (request.online && !request.adminUsed && request.outcome === 'victory' && completed.length !== 10)
    throw fail('A multiplayer victory requires all ten topics.', 409);
  if (request.version === 2) {
    const escaped = uniqueNumbers(request.escapedActors, 0, participants.length, 'escape evidence');
    const disconnected = uniqueNumbers(request.disconnectedActors, 0, participants.length, 'disconnect evidence');
    if ([...escaped, ...disconnected].some(actor => !participants.includes(actor))) throw fail('Invalid actor in match evidence.');
    const ranked = request.online && !request.adminUsed;
    if (ranked && ((escaped.length && completed.length !== 10) || (request.outcome === 'victory' && !escaped.length)))
      throw fail('Escape evidence does not match the result.', 409);
    const counts = new Map(participants.map(actor => [actor, 0]));
    const solved = new Set();
    const solvedPerTopic = rounds.map(() => 0);
    if (!Array.isArray(request.correctAnswers) || request.correctAnswers.length > 64) throw fail('Invalid correct-answer evidence.');
    for (const key of request.correctAnswers) {
      const match = typeof key === 'string' && /^([1-9][0-9]{0,9}):([0-9]):(0|[1-9][0-9]?):([01])$/.exec(key);
      if (!match || match[0] !== key) throw fail('Invalid correct-answer key.');
      const [, actorText, topicText, roundText, phaseText] = match;
      const actor = Number(actorText), topic = Number(topicText);
      const puzzle = [topicText, roundText, phaseText].join(':');
      if (!counts.has(actor) || Number(roundText) >= rounds[topic] || Number(phaseText) > (topic === 2 ? 1 : 0) || solved.has(puzzle))
        throw fail('Invalid or duplicate correct-answer credit.');
      counts.set(actor, counts.get(actor) + 1);
      solved.add(puzzle);
      solvedPerTopic[topic]++;
    }
    if (ranked && completed.some(topic => solvedPerTopic[topic] !== rounds[topic] * (topic === 2 ? 2 : 1)))
      throw fail('A completed topic requires all its correct answers.', 409);
    return { participants, escaped, disconnected, counts };
  }
  if (!Array.isArray(request.participationKeys) || request.participationKeys.length > 128) throw fail('Invalid participation evidence.');
  for (const key of request.participationKeys) {
    if (typeof key !== 'string' || key.length > 64) throw fail('Invalid participation key.');
    const work = /^work:([0-9]):(0|[1-9][0-9]?):([01])$/.exec(key);
    if (work) {
      const topic = Number(work[1]), round = Number(work[2]), subphase = Number(work[3]);
      if (round >= rounds[topic] || subphase > (topic === 2 ? 1 : 0)) throw fail('Invalid puzzle-work evidence.');
      continue;
    }
    const support = /^support:(medkit|battery|revive|give):([1-9][0-9]{0,9})$/.exec(key);
    const target = support ? Number(support[2]) : 0;
    if (!support || !actorId(target) || target === request.actor || !participants.includes(target)) throw fail('Invalid teammate-support evidence.');
  }
  return { completed, restored, tier: Math.min(3, new Set(request.participationKeys).size) };
}
function calculateReward(request) {
  const report = terminalReport(request);
  if (request.version === 2) {
    const { participants, escaped, disconnected, counts } = report;
    const correctAnswers = counts.get(request.actor);
    const didEscape = escaped.includes(request.actor);
    const exclusionReason = request.adminUsed ? 'admin-assisted' : !request.online ? 'offline-preview' :
      disconnected.includes(request.actor) ? 'disconnected' : '';
    if (exclusionReason) return { reward: 0, leaderboardReward: 0, correctAnswers, escaped: didEscape, exclusionReason };
    const scores = participants.filter(actor => !disconnected.includes(actor)).map(actor => counts.get(actor));
    const low = Math.min(...scores), gap = Math.max(...scores) - low;
    const leaderboardReward = gap <= 1 ? 1000 : 500 + Math.round((correctAnswers - low) * 500 / gap);
    return { reward: didEscape ? 2000 : 1000, leaderboardReward, correctAnswers, escaped: didEscape, exclusionReason };
  }
  const { completed, restored, tier } = report;
  const exclusionReason = request.adminUsed ? 'admin-assisted' : !request.online ? 'offline-preview' : !tier ? 'no-participation' : '';
  const result = { baseCoins: 0, crystalCoins: 0, participationCoins: 0, consolationCoins: 0, reward: 0, tier, exclusionReason };
  if (!exclusionReason) {
    if (request.outcome === 'victory') {
      result.baseCoins = Math.floor(600 * tier / 3);
      result.crystalCoins = Math.floor(25 * restored.length * tier / 3);
      result.participationCoins = 50 * tier;
    } else result.consolationCoins = Math.floor(Math.min(150, 15 * completed.length) * tier / 3);
    result.reward = result.baseCoins + result.crystalCoins + result.participationCoins + result.consolationCoins;
  }
  return result;
}
function finishMatch(account, request, now = Date.now()) {
  if (!account || typeof account !== 'object' || Array.isArray(account)) throw fail('Please load your saved account first.', 409);
  const id = matchId(request.matchId);
  // A repeated or edited report can only read the original immutable receipt.
  if (account.multiplayerRewards?.[id]) return account;
  const reward = calculateReward(request);
  const milestones = account.multiplayerMilestones || {};
  const legacyVictory = request.version === 1 && !reward.exclusionReason && request.outcome === 'victory' && reward.tier === 3;
  const firstVictory = legacyVictory && !milestones.firstVictory;
  const allCrystals = legacyVictory && request.restoredTopics.length === 10 && !milestones.allCrystals;
  const gemReward = Number(firstVictory) + Number(allCrystals);
  // Validate every balance before changing any field, including the receipt.
  const coins = balance(balance(account.coins) + reward.reward);
  const gems = balance(balance(account.gems) + gemReward);
  const points = request.version === 2 ? balance(balance(account.points) + reward.leaderboardReward) : null;
  account.coins = coins; account.gems = gems;
  if (request.version === 2 && reward.leaderboardReward > 0) account.points = points;
  if (firstVictory || allCrystals) {
    account.multiplayerMilestones ||= {};
    if (firstVictory) account.multiplayerMilestones.firstVictory = id;
    if (allCrystals) account.multiplayerMilestones.allCrystals = id;
  }
  account.multiplayerRewards ||= {};
  account.multiplayerRewards[id] = { status: 'completed', version: request.version, ...reward, gemReward, outcome: request.outcome,
    actor: request.actor, completedTopics: request.completedTopics.length, restoredTopics: request.restoredTopics.length, completedAt: now };
  return account;
}
function presentMatch(account, id) {
  const receipt = account?.multiplayerRewards?.[matchId(id)];
  if (!receipt) throw fail('No multiplayer reward receipt is available.', 409);
  if (receipt.version === 2)
    return { matchId: id, status: receipt.status, version: 2, coins: balance(account.coins), gems: balance(account.gems),
      points: balance(account.points), reward: receipt.reward, gemReward: 0, leaderboardReward: receipt.leaderboardReward,
      correctAnswers: receipt.correctAnswers, escaped: receipt.escaped, exclusionReason: receipt.exclusionReason || '' };
  return { matchId: id, status: receipt.status, coins: balance(account.coins), gems: balance(account.gems),
    reward: receipt.reward, gemReward: receipt.gemReward, baseCoins: receipt.baseCoins, crystalCoins: receipt.crystalCoins,
    participationCoins: receipt.participationCoins, consolationCoins: receipt.consolationCoins,
    tier: receipt.tier, exclusionReason: receipt.exclusionReason || '' };
}
module.exports = { terminalReport, calculateReward, finishMatch, presentMatch };
