const { test } = require('node:test');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const { terminalReport, calculateReward, finishMatch, presentMatch } = require('./multiplayer');
const id = () => crypto.randomBytes(16).toString('hex');
const profile = () => ({ coins: 37, gems: 19, unlockedStage: 8, base_character: 'Male_Character',
  inventory: { Common_Skin: true }, equipped: { clothes: 'Common_Skin' } });
const report = (overrides = {}) => ({ version: 1, matchId: id(), actor: 1, online: true, adminUsed: false,
  outcome: 'victory', participants: [1, 2], completedTopics: Array.from({ length: 10 }, (_, i) => i),
  restoredTopics: Array.from({ length: 10 }, (_, i) => i),
  participationKeys: ['work:0:0:0', 'work:1:0:0', 'support:revive:2'], ...overrides });
const rankedReport = (overrides = {}) => report({ version: 2, outcome: 'defeat', participants: [1, 2, 3],
  completedTopics: [], restoredTopics: [], escapedActors: [], disconnectedActors: [], correctAnswers: [], ...overrides });
const fullAnswers = Array.from({ length: 10 }, (_, topic) =>
  Array.from({ length: topic === 1 ? 9 : topic === 6 ? 10 : 5 }, (_, round) =>
    topic === 2 ? [`1:${topic}:${round}:0`, `1:${topic}:${round}:1`] : [`1:${topic}:${round}:0`]).flat()).flat();

test('victory calculation is bounded at 600 team, 250 crystals and 150 participation coins', () => {
  for (let tier = 0; tier <= 3; tier++) {
    for (let crystals = 0; crystals <= 10; crystals++) {
      const request = report({ participationKeys: report().participationKeys.slice(0, tier),
        restoredTopics: Array.from({ length: crystals }, (_, i) => i), reward: 999999, coins: 999999, gems: 999999 });
      const result = calculateReward(request);
      assert.equal(result.reward, Math.floor(600 * tier / 3) + Math.floor(25 * crystals * tier / 3) + 50 * tier);
      assert.ok(result.reward >= 0 && result.reward <= 1000);
      assert.equal(result.exclusionReason, tier ? '' : 'no-participation');
    }
  }
  assert.deepEqual(calculateReward(report()), { baseCoins: 600, crystalCoins: 250, participationCoins: 150,
    consolationCoins: 0, reward: 1000, tier: 3, exclusionReason: '' });
});

test('defeat consolation follows genuinely completed topics and never exceeds 150 coins', () => {
  for (let topics = 0; topics <= 10; topics++) {
    for (let tier = 0; tier <= 3; tier++) {
      const result = calculateReward(report({ outcome: 'defeat', completedTopics: Array.from({ length: topics }, (_, i) => i),
        restoredTopics: [], participationKeys: report().participationKeys.slice(0, tier) }));
      assert.equal(result.reward, Math.floor(15 * topics * tier / 3));
      assert.equal(result.consolationCoins, result.reward);
      assert.equal(result.baseCoins + result.crystalCoins + result.participationCoins, 0);
      assert.ok(result.reward <= 150);
    }
  }
});

test('repeated work and support evidence cannot farm extra tiers', () => {
  assert.equal(calculateReward(report({ participationKeys: Array(128).fill('work:0:0:0') })).tier, 1);
  assert.equal(calculateReward(report({ participationKeys: Array(128).fill('support:give:2') })).tier, 1);
  assert.equal(calculateReward(report({ participationKeys: ['work:0:0:0', 'work:0:0:0', 'work:0:1:0'] })).tier, 2);
  assert.equal(calculateReward(report({ participationKeys: ['work:0:0:0', 'work:0:1:0', 'work:0:2:0', 'work:0:3:0'] })).tier, 3);
});

test('valid work covers ten topics and only Rules of Inference has a second subphase', () => {
  const counts = [5, 9, 5, 5, 5, 5, 10, 5, 5, 5];
  for (let topic = 0; topic < 10; topic++) {
    for (let round = 0; round < counts[topic]; round++) {
      for (let subphase = 0; subphase <= (topic === 2 ? 1 : 0); subphase++)
        assert.equal(calculateReward(report({ participationKeys: [`work:${topic}:${round}:${subphase}`] })).tier, 1);
    }
    assert.throws(() => terminalReport(report({ participationKeys: [`work:${topic}:${counts[topic]}:0`] })), /work evidence/);
    if (topic !== 2) assert.throws(() => terminalReport(report({ participationKeys: [`work:${topic}:0:1`] })), /work evidence/);
  }
  for (const kind of ['medkit', 'battery', 'revive', 'give'])
    assert.equal(calculateReward(report({ participationKeys: [`support:${kind}:2`] })).tier, 1);
});

test('admin and offline reports record zero payouts and cannot earn milestone gems', () => {
  for (const changes of [{ adminUsed: true }, { adminUsed: true, completedTopics: [], restoredTopics: [] },
    { online: false }, { online: false, completedTopics: [], restoredTopics: [] }, { adminUsed: true, online: false }, { participationKeys: [] }]) {
    const account = profile(), request = report(changes);
    finishMatch(account, request);
    assert.equal(account.coins, 37); assert.equal(account.gems, 19);
    const receipt = presentMatch(account, request.matchId);
    assert.equal(receipt.reward, 0); assert.equal(receipt.gemReward, 0); assert.notEqual(receipt.exclusionReason, '');
    assert.equal(account.multiplayerMilestones, undefined);
    finishMatch(account, report({ matchId: request.matchId }));
    assert.equal(account.coins, 37); assert.equal(account.gems, 19);
  }
});

test('one atomic account receipt survives duplicate and edited completion requests', () => {
  const account = profile(), request = report();
  finishMatch(account, request, 1000);
  const saved = structuredClone(account);
  for (let n = 0; n < 20; n++) {
    finishMatch(account, request, 9999);
    finishMatch(account, { matchId: request.matchId, reward: 999999, gems: 999999, actor: 99 }, 9999);
  }
  assert.deepEqual(account, saved);
  assert.equal(account.coins, 1037); assert.equal(account.gems, 21);
  assert.equal(account.unlockedStage, 8); assert.deepEqual(account.inventory, { Common_Skin: true });
  assert.deepEqual(account.equipped, { clothes: 'Common_Skin' });
  assert.deepEqual(presentMatch(account, request.matchId), { matchId: request.matchId, status: 'completed',
    coins: 1037, gems: 21, reward: 1000, gemReward: 2, baseCoins: 600, crystalCoins: 250,
    participationCoins: 150, consolationCoins: 0, tier: 3, exclusionReason: '' });
  // Later account spending/awards change balances, never the original receipt delta.
  account.coins -= 5; account.gems -= 1;
  assert.equal(presentMatch(account, request.matchId).coins, 1032);
  assert.equal(presentMatch(account, request.matchId).gemReward, 2);
});

test('milestone gems are once per account, independently earned, and require full participation', () => {
  const account = profile();
  const weak = report({ participationKeys: ['work:0:0:0'] }); finishMatch(account, weak);
  assert.equal(presentMatch(account, weak.matchId).gemReward, 0);
  const first = report({ restoredTopics: [] }); finishMatch(account, first);
  assert.equal(presentMatch(account, first.matchId).gemReward, 1);
  const second = report(); finishMatch(account, second);
  assert.equal(presentMatch(account, second.matchId).gemReward, 1);
  const third = report(); finishMatch(account, third);
  assert.equal(presentMatch(account, third.matchId).gemReward, 0);
  assert.deepEqual(account.multiplayerMilestones, { firstVictory: first.matchId, allCrystals: second.matchId });
  assert.equal(account.gems, 21);
});

test('a separate authenticated account can receive its own share of the same match', () => {
  const first = profile(), second = profile(), request = report();
  finishMatch(first, request);
  finishMatch(second, { ...request, actor: 2, participationKeys: ['work:0:1:0', 'work:0:2:0', 'support:medkit:1'] });
  assert.equal(first.coins, 1037); assert.equal(second.coins, 1037);
  assert.equal(Object.keys(first.multiplayerRewards).length, 1); assert.equal(Object.keys(second.multiplayerRewards).length, 1);
});

test('invalid envelopes, topics, rosters and support targets cannot manufacture a receipt', () => {
  const malformed = [
    { matchId: '../invalid' }, { version: 2 }, { actor: 0 }, { actor: 3 }, { online: 'true' }, { adminUsed: null }, { outcome: 'active' },
    { participants: [1] }, { participants: [1, 1] }, { participants: [1, -2] }, { participants: [1, 2, 3, 4, 5] },
    { completedTopics: [0, 0] }, { completedTopics: ['0'] }, { completedTopics: [10] },
    { completedTopics: [0], restoredTopics: [] }, { restoredTopics: [0, 0] },
    { outcome: 'defeat', completedTopics: [0], restoredTopics: [1] },
    { participationKeys: ['support:give:1'] }, { participationKeys: ['support:revive:3'] },
    { participationKeys: ['support:medkit:02'] }, { participationKeys: ['work:10:0:0'] }, { participationKeys: ['work:0:00:0'] },
    { participationKeys: ['button:spam'] }, { participationKeys: [null] }, { participationKeys: Array(129).fill('work:0:0:0') }
  ];
  for (const changes of malformed) {
    const account = profile(), before = structuredClone(account);
    assert.throws(() => finishMatch(account, report(changes)));
    assert.deepEqual(account, before);
  }
  assert.throws(() => finishMatch(null, report()), /saved account/);
  assert.throws(() => presentMatch(profile(), id()), /No multiplayer/);
});

test('coin or gem overflow cannot partially alter a wallet, milestone or receipt', () => {
  for (const balances of [{ coins: 2147483647 }, { gems: 2147483647 }, { coins: -1 }, { gems: 1.5 }]) {
    const account = { ...profile(), ...balances }, before = structuredClone(account);
    assert.throws(() => finishMatch(account, report()), /balance/);
    assert.deepEqual(account, before);
  }
});

test('each player gets their own escape payout and disconnected escapees forfeit both rewards', () => {
  const request = rankedReport({ completedTopics: Array.from({ length: 10 }, (_, i) => i),
    escapedActors: [1, 3], disconnectedActors: [3], correctAnswers: fullAnswers });
  assert.deepEqual(calculateReward(request), {
    reward: 2000, leaderboardReward: 1000, correctAnswers: 64, escaped: true, exclusionReason: ''
  });
  assert.deepEqual(calculateReward({ ...request, actor: 2 }), {
    reward: 1000, leaderboardReward: 500, correctAnswers: 0, escaped: false, exclusionReason: ''
  });
  assert.deepEqual(calculateReward({ ...request, actor: 3 }), {
    reward: 0, leaderboardReward: 0, correctAnswers: 0, escaped: true, exclusionReason: 'disconnected'
  });
  assert.equal(calculateReward({ ...request, outcome: 'victory', actor: 2 }).reward, 1000);
});

test('rank uses accepted answers only and awards full points for a one-answer spread', () => {
  const answers = ['1:0:0:0', '1:0:1:0', '1:0:2:0', '2:1:0:0', '2:1:1:0', '3:2:0:0'];
  const points = request => [1, 2, 3].map(actor => calculateReward({ ...request, actor }).leaderboardReward);
  assert.deepEqual(points(rankedReport({ correctAnswers: answers })), [1000, 750, 500]);
  assert.deepEqual(points(rankedReport({ correctAnswers: answers.slice(1) })), [1000, 1000, 1000]);
  assert.deepEqual(points(rankedReport({ correctAnswers: answers, disconnectedActors: [3] })), [1000, 1000, 0]);
  assert.deepEqual(points(rankedReport()), [1000, 1000, 1000]);
  assert.deepEqual(points(rankedReport({ adminUsed: true, correctAnswers: answers })), [0, 0, 0]);
  assert.deepEqual(points(rankedReport({ online: false, correctAnswers: answers })), [0, 0, 0]);
  assert.deepEqual(points(rankedReport({ correctAnswers: ['1:0:0:0', '1:0:1:0', '2:1:0:0'] })), [1000, 750, 500]);
});

test('ranked evidence checks reject double credit, nonmembers, and impossible portal entries', () => {
  for (const overrides of [{ correctAnswers: ['1:0:0:0', '2:0:0:0'] }, { correctAnswers: ['4:0:0:0'] },
    { correctAnswers: ['1:0:0:1'] }, { correctAnswers: ['1:6:10:0'] }, { correctAnswers: ['1:0:0:0\n'] },
    { correctAnswers: null }, { disconnectedActors: [1, 1] }, { escapedActors: [4] }, { escapedActors: [1] },
    { completedTopics: [0] }, { outcome: 'victory', completedTopics: Array.from({ length: 10 }, (_, i) => i), correctAnswers: fullAnswers }])
    assert.throws(() => calculateReward(rankedReport(overrides)));
  assert.ok(Buffer.byteLength(JSON.stringify(rankedReport({ correctAnswers: fullAnswers }))) < 4096);
});

test('points and coins are written once without changing gems or accepting requested payout amounts', () => {
  const account = { ...profile(), points: 120 };
  const request = rankedReport({ reward: 1000000, leaderboardReward: 1000000, points: 1000000 });
  finishMatch(account, request);
  const saved = structuredClone(account);
  finishMatch(account, { ...request, actor: 2, disconnectedActors: [1] });
  assert.deepEqual(account, saved);
  assert.deepEqual(presentMatch(account, request.matchId), { matchId: request.matchId, status: 'completed',
    version: 2, coins: 1037, gems: 19, points: 1120, reward: 1000, gemReward: 0, leaderboardReward: 1000,
    correctAnswers: 0, escaped: false, exclusionReason: '' });
  const excluded = rankedReport({ disconnectedActors: [1] });
  finishMatch(account, excluded);
  assert.equal(account.points, 1120);
  assert.equal(account.coins, 1037);
  assert.equal(presentMatch(account, excluded.matchId).exclusionReason, 'disconnected');
  const overflow = { ...profile(), points: 2147483647 }, before = structuredClone(overflow);
  assert.throws(() => finishMatch(overflow, rankedReport()), /balance/);
  assert.deepEqual(overflow, before);
});
