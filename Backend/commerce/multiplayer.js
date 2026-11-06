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
  if (request.version !== 1 || !actorId(request.actor) || typeof request.online !== 'boolean' ||
      typeof request.adminUsed !== 'boolean' || !['victory', 'defeat'].includes(request.outcome)) throw fail('Invalid terminal multiplayer report.');
  const participants = uniqueNumbers(request.participants, 2, 4, 'multiplayer roster');
  if (!participants.every(actorId) || !participants.includes(request.actor)) throw fail('Invalid multiplayer participant.');
  const completed = uniqueNumbers(request.completedTopics, 0, 10, 'completed topics');
  if (completed.some(t => t < 0 || t > 9)) throw fail('Invalid completed topic.');
  const restored = uniqueNumbers(request.restoredTopics, 0, 10, 'restored crystals');
  if (restored.some(t => !completed.includes(t))) throw fail('A restored crystal requires its completed topic.');
  if (request.online && !request.adminUsed && request.outcome === 'victory' && completed.length !== 10)
    throw fail('A multiplayer victory requires all ten topics.', 409);
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
  const { completed, restored, tier } = terminalReport(request);
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
  const firstVictory = !reward.exclusionReason && request.outcome === 'victory' && reward.tier === 3 && !milestones.firstVictory;
  const allCrystals = !reward.exclusionReason && request.outcome === 'victory' && reward.tier === 3 && request.restoredTopics.length === 10 && !milestones.allCrystals;
  const gemReward = Number(firstVictory) + Number(allCrystals);
  // Validate both balances before changing any field, including the receipt.
  const coins = balance(balance(account.coins) + reward.reward);
  const gems = balance(balance(account.gems) + gemReward);
  account.coins = coins; account.gems = gems;
  if (firstVictory || allCrystals) {
    account.multiplayerMilestones ||= {};
    if (firstVictory) account.multiplayerMilestones.firstVictory = id;
    if (allCrystals) account.multiplayerMilestones.allCrystals = id;
  }
  account.multiplayerRewards ||= {};
  account.multiplayerRewards[id] = { status: 'completed', ...reward, gemReward, outcome: request.outcome,
    actor: request.actor, completedTopics: request.completedTopics.length, restoredTopics: request.restoredTopics.length, completedAt: now };
  return account;
}
function presentMatch(account, id) {
  const receipt = account?.multiplayerRewards?.[matchId(id)];
  if (!receipt) throw fail('No multiplayer reward receipt is available.', 409);
  return { matchId: id, status: receipt.status, coins: balance(account.coins), gems: balance(account.gems),
    reward: receipt.reward, gemReward: receipt.gemReward, baseCoins: receipt.baseCoins, crystalCoins: receipt.crystalCoins,
    participationCoins: receipt.participationCoins, consolationCoins: receipt.consolationCoins,
    tier: receipt.tier, exclusionReason: receipt.exclusionReason || '' };
}
module.exports = { terminalReport, calculateReward, finishMatch, presentMatch };
