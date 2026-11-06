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
