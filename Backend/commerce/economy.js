const config = require('./economy.json');
const { fail } = require('./wallet-errors');
const whole = value => Number.isSafeInteger(value) && value >= 0 && value <= 2147483647;
if (config.version < 2 || config.maxRetries !== 2 || !whole(config.retryReductionPercent) || config.retryReductionPercent > 50 ||
    config.trials.length !== 10 || new Set(config.trials.map(p => p.trial)).size !== 10 ||
    config.trials.some(p => !whole(p.baseReward) || !whole(p.quizReward) || !whole(p.quizBonusLimit) || p.trial < 1 || p.trial > 10) ||
    config.rarities.some(p => !whole(p.coins) || !whole(p.gems) || (p.coins === 0 && p.gems === 0))) throw new Error('Invalid solo economy configuration.');
function trialPolicy(trial) {
  const policy = config.trials.find(t => t.trial === trial);
  if (!policy) throw fail('Invalid trial.');
  return policy;
}
function baseAfterRetries(base, retries) {
  if (!Number.isSafeInteger(base) || base < 0 || !Number.isSafeInteger(retries) || retries < 0) throw fail('Invalid reward settings.');
  return Math.floor(base * Math.max(0, 100 - Math.min(config.maxRetries, retries) * config.retryReductionPercent) / 100);
}
function pricesFor(item) {
  const tier = config.rarities.find(r => r.name === item.rarity);
  if (!tier) throw fail('Unknown cosmetic rarity.');
  return { coins: tier.coins, gems: tier.gems };
}
module.exports = { config, trialPolicy, baseAfterRetries, pricesFor };
