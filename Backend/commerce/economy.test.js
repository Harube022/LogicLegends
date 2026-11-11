const { test } = require('node:test');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const trials = require('./trials');
const { config, pricesFor } = require('./economy');
const { buy, catalog, present } = require('./wallet');
const bank = require('./quiz-questions.json');
const id = () => crypto.randomBytes(16).toString('hex');
function attempt(trial) {
  const account = { coins: 37, gems: 19, inventory: { already_owned: true }, unlockedStage: 8 };
  const request = { trial, attemptId: id(), newAttempt: true };
  trials.startAttempt(account, request);
  return { account, request };
}
for (let trial = 1; trial <= 10; trial++) {
  test(`trial ${trial}: at most two checkpoint retries; fresh run resets only its attempt`, () => {
    const { account, request } = attempt(trial);
    for (let n = 1; n <= 2; n++) {
      const retry = { ...request, operationId: id() };
      trials.retryAttempt(account, retry); trials.retryAttempt(account, retry);
      assert.equal(trials.presentAttempt(account, trial).retries, n);
    }
    assert.throws(() => trials.retryAttempt(account, { ...request, operationId: id() }), /No Try Again/);
    const fresh = { trial, attemptId: id(), newAttempt: true };
    trials.startAttempt(account, fresh);
    assert.equal(trials.presentAttempt(account, trial).retries, 0);
    assert.equal(trials.presentAttempt(account, trial).pendingBonus, 0);
    assert.equal(account.coins, 37); assert.equal(account.gems, 19);
    assert.deepEqual(account.inventory, { already_owned: true }); assert.equal(account.unlockedStage, 8);
    trials.finishAttempt(account, fresh); trials.finishAttempt(account, fresh);
    assert.equal(account.coins, 1037);
  });
}
for (const trial of [2,3]) {
  test(`trial ${trial}: only first eligible quiz answers earn capped pending bonuses, paid once at completion`, () => {
    const { account, request } = attempt(trial), questions = trial === 2 ? bank.truthTable : bank.inference;
    const question = questions[0];
    const events = trial === 2 ? Array.from({length:54},(_,i)=>`tt-c${1+Math.floor(i/18)}-col${1+Math.floor(i%18/6)}-house${i%6}`) :
      Array.from({length:45},(_,i)=>`roi-c${1+Math.floor(i/9)}-${['MP','MT','HS','DS','CD','SIMP','CONJ','ADD','DN'][i%9]}`);
    const wrong = { ...request, eventKey: events[0], questionId: question.id, choice: (question.correctChoice+1)%4 };
    trials.answerQuiz(account, wrong);
    trials.answerQuiz(account, {...wrong, choice: question.correctChoice});
    assert.equal(trials.presentAttempt(account, trial).pendingBonus, 0, 'changing the answer does not overwrite a wrong first answer');
    for (const eventKey of events.slice(1)) {
      const answer = {...request, eventKey, questionId: question.id, choice: question.correctChoice, bonus:999999, correct:true};
      trials.answerQuiz(account, answer); trials.answerQuiz(account, answer);
    }
    const cap = config.trials.find(t=>t.trial===trial).quizBonusLimit;
    assert.equal(trials.presentAttempt(account, trial).pendingBonus, cap);
    assert.equal(account.coins, 37, 'pending bonuses are not spendable');
    for (let i=0;i<2;i++) trials.retryAttempt(account,{...request,operationId:id()});
    const reloaded = JSON.parse(JSON.stringify(account));
    for(const eventKey of events) trials.answerQuiz(reloaded,{...request,eventKey,questionId:question.id,choice:question.correctChoice});
    trials.finishAttempt(reloaded,request); trials.finishAttempt(reloaded,request);
    const receipt=trials.presentAttempt(reloaded,trial);
    assert.equal(receipt.baseReward,1000); assert.equal(receipt.reduction,500); assert.equal(receipt.quizBonus,cap);
    assert.equal(receipt.reward,500+cap); assert.equal(reloaded.coins,537+cap);
    assert.throws(()=>trials.answerQuiz(reloaded,wrong),/already ended/);
    const fresh={...request,attemptId:id()};trials.startAttempt(reloaded,fresh);
    assert.equal(trials.presentAttempt(reloaded,trial).pendingBonus,0);
    assert.equal(trials.presentAttempt(reloaded,trial).quizAnswers.length,0);
  });
  test(`trial ${trial}: admin use excludes base and bonus; malformed and cross-trial quiz events fail`,()=>{
    const {account,request}=attempt(trial), q=(trial===2?bank.truthTable:bank.inference)[0];
    assert.throws(()=>trials.answerQuiz(account,{...request,eventKey:'arbitrary-farm',questionId:q.id,choice:q.correctChoice}),/Invalid quiz event/);
    const answer={...request,eventKey:trial===2?'tt-c1-col1-house0':'roi-c1-MP',questionId:q.id,choice:q.correctChoice};
    assert.throws(()=>trials.answerQuiz(account,{...answer,questionId:'fake'}),/Invalid quiz answer/);
    assert.throws(()=>trials.answerQuiz(account,{...answer,choice:4}),/Invalid quiz answer/);
    trials.answerQuiz(account,answer);trials.markAdmin(account,request);trials.finishAttempt(account,request);
    assert.equal(account.coins,37);assert.equal(trials.presentAttempt(account,trial).reward,0);assert.equal(trials.presentAttempt(account,trial).quizBonus,0);
  });
}
test('each existing outfit uses its configured currency; ownership is never charged twice',()=>{
  for(const item of catalog.items) {
    const prices=pricesFor(item),currency=prices.coins>0?'coins':'gems',other=currency==='coins'?'gems':'coins',price=prices[currency];
    assert.ok(price>0);assert.equal(prices[other],0);
    const account={base_character:item.character,coins:200000,gems:2000,inventory:{legacy_outfit:true}};
    const old=structuredClone(account);
    buy(account,item.id,currency);const after=structuredClone(account);
    buy(account,item.id,other);
    assert.deepEqual(account,after);assert.equal(account[currency],old[currency]-price);
    assert.equal(account[other],old[other]);
    assert.equal(account.inventory.legacy_outfit,true);assert.equal(account.inventory[item.id],true);
    assert.equal(present(account,false,true).catalog.find(p=>p.id===item.id)[currency],price);
    assert.throws(()=>buy({base_character:item.character,coins:0,gems:0},item.id,currency),/Not enough/);
    assert.throws(()=>buy({base_character:item.character,coins:200000,gems:2000},item.id,other),/cannot be purchased/);
  }
});
test('rarities increase meaningfully; no wallet migration or default balance grant',()=>{
  assert.deepEqual(config.rarities.map(r=>r.name),['Common','Rare','Elite','Special','Epic']);
  assert.deepEqual(config.rarities.map(r=>r.coins),[10000,40000,0,0,0]);
  assert.deepEqual(config.rarities.map(r=>r.gems),[0,0,150,250,400]);
  assert.equal(present(null,false,true).coins,0);assert.equal(present(null,false,true).gems,0);
  assert.throws(()=>buy({base_character:'Male_Character'},'Common_Skin','usd'),/Coins or Gems/);
});
test('an active run freezes its configured reward policy while historical receipts stay untouched',()=>{
  const {account,request}=attempt(1),p=config.trials[0],old=p.baseReward;
  try {p.baseReward=9999;trials.retryAttempt(account,{...request,operationId:id()});trials.finishAttempt(account,request);assert.equal(trials.presentAttempt(account,1).reward,750);}
  finally {p.baseReward=old;}
});

