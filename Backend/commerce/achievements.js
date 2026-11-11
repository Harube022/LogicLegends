const { fail, balance } = require('./wallet');

// Reward values are server-owned. Trial completion receipts are also server-owned,
// so clients cannot submit progress or choose a payout.
const definitions = [
  { id: 'first_steps', title: 'FIRST STEPS', objective: 'Complete your first trial.', unit: 'TRIAL', trials: [1,2,3,4,5,6,7,8,9,10], target: 1, coins: 500, gems: 0 },
  { id: 'truth_seeker', title: 'TRUTH SEEKER', objective: 'Complete the Truth Table trial.', unit: 'TRIAL', trials: [2], target: 1, coins: 750, gems: 0 },
  { id: 'inference_adept', title: 'INFERENCE ADEPT', objective: 'Complete the Rules of Inference trial.', unit: 'TRIAL', trials: [3], target: 1, coins: 1000, gems: 0 },
  { id: 'the_awakening', title: 'THE AWAKENING', objective: 'Conquer Trials 1–3.', unit: 'TRIALS', trials: [1,2,3], target: 3, coins: 100, gems: 5 },
  { id: 'the_ascent', title: 'THE ASCENT', objective: 'Conquer Trials 4–5.', unit: 'TRIALS', trials: [4,5], target: 2, coins: 2000, gems: 5 },
  { id: 'the_reckoning', title: 'THE RECKONING', objective: 'Conquer Trials 6–8.', unit: 'TRIALS', trials: [6,7,8], target: 3, coins: 2000, gems: 10 },
  { id: 'the_legends_rise', title: "THE LEGEND'S RISE", objective: 'Conquer Trials 9–10.', unit: 'TRIALS', trials: [9,10], target: 2, coins: 2000, gems: 10 },
  { id: 'logic_legend', title: 'LOGIC LEGEND', objective: 'Conquer all ten trials.', unit: 'TRIALS', trials: [1,2,3,4,5,6,7,8,9,10], target: 10, coins: 3000, gems: 25 }
];

function completedTrials(account) {
  const completed = new Set();
  for (let trial = 1; trial <= 10; trial++) {
    const attempts = Object.values(account?.trialRuns?.[String(trial)]?.attempts || {});
    if (attempts.some(run => run?.status === 'completed' && run.adminUsed !== true)) completed.add(trial);
  }
  return completed;
}

function present(account) {
  const completed = completedTrials(account);
  const claimed = account?.achievements?.claimed || {};
  return {
    coins: balance(account?.coins),
    gems: balance(account?.gems),
    items: definitions.map(definition => {
      const progress = Math.min(definition.target, definition.trials.filter(trial => completed.has(trial)).length);
      const isClaimed = claimed[definition.id]?.claimed === true;
      return { id: definition.id, title: definition.title, objective: definition.objective,
        unit: definition.unit, progress, target: definition.target, coins: definition.coins, gems: definition.gems,
        eligible: progress >= definition.target && !isClaimed, claimed: isClaimed };
    })
  };
}

function claim(account, id, now = Date.now()) {
  if (!account) throw fail('Please load your saved account first.', 409);
  const definition = definitions.find(item => item.id === id);
  if (!definition) throw fail('This achievement is unavailable.');
  account.achievements ||= {};
  account.achievements.claimed ||= {};
  if (account.achievements.claimed[id]?.claimed === true) throw fail('This achievement reward was already claimed.', 409);
  const completed = completedTrials(account);
  const progress = definition.trials.filter(trial => completed.has(trial)).length;
  if (progress < definition.target) throw fail('Complete the achievement before claiming its reward.', 409);
  account.coins = balance(balance(account.coins) + definition.coins);
  account.gems = balance(balance(account.gems) + definition.gems);
  account.achievements.claimed[id] = { claimed: true, claimedAt: now, coins: definition.coins, gems: definition.gems };
  return account;
}

module.exports = { definitions, completedTrials, present, claim };
