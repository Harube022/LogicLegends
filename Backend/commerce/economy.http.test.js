const { test } = require('node:test');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');

test('authenticated HTTP: quiz bonus receipts and both purchase currencies remain atomic across retries, reloads and duplicate input',
  { skip: process.env.RUN_EMULATOR_TESTS !== '1', timeout: 120000 }, async () => {
    process.env.FIREBASE_DATABASE_EMULATOR_HOST = '127.0.0.1:9000';
    const { createApp } = require('./app');
    const { getApp, deleteApp } = require('firebase-admin/app');
    const { getDatabase } = require('firebase-admin/database');
    const { catalog } = require('./wallet'), { pricesFor } = require('./economy'), bank = require('./quiz-questions.json');
    const prefix = 'solo-economy-http-' + crypto.randomBytes(6).toString('hex'), ids = [];
    const app = createApp({ SOLO_ECONOMY_V2_ENABLED:'true',STORAGE_MODE:'emulator',PAYMENT_MODE:'test',FIREBASE_DATABASE_EMULATOR_HOST:'127.0.0.1:9000',FIREBASE_PROJECT_ID:'demo-logic-legends' }, {
      verifyIdToken: async uid => { if (!uid.startsWith(prefix)) throw Error('Invalid test identity'); return {uid}; },
      getUser: async uid => ({uid,email:'ordinary@ncst.edu.ph',emailVerified:true,disabled:false})
    });
    const server=app.listen(0,'127.0.0.1');await new Promise(r=>server.once('listening',r));
    const base='http://127.0.0.1:'+server.address().port, db=getDatabase(getApp('commerce-data'));
    const id=()=>crypto.randomBytes(16).toString('hex');
    const call=(uid,path,body)=>fetch(base+path,{method:body?'POST':'GET',headers:{Authorization:'Bearer '+uid,'Content-Type':'application/json'},...(body?{body:JSON.stringify(body)}:{})});
    const ok=async r=>{const data=await r.json();assert.equal(r.status,200,JSON.stringify(data));return data;};
    async function account(name,character='Male_Character') {
      const uid=prefix+'-'+name;ids.push(uid);
      await db.ref('users/'+uid).set({base_character:character,coins:200000,gems:2000,unlockedStage:8,inventory:{legacy_outfit:true}});return uid;
    }
    try {
      const connectionUid=await account('connection-check');
      const beforeConnection=(await db.ref('users/'+connectionUid).get()).val();
      const connection=await ok(await call(connectionUid,'/economy'));
      assert.equal(connection.version,2);assert.equal(connection.catalog.length,10);
      assert.ok(connection.capabilities.includes('pending-quiz-bonuses'));
      assert.deepEqual((await db.ref('users/'+connectionUid).get()).val(),beforeConnection,'connection checks are strictly read-only');
      for(const trial of [2,3]) {
        const uid=await account('bonus-'+trial), request={trial,attemptId:id(),newAttempt:true};
        await ok(await call(uid,'/trials/start',request));
        const q=(trial===2?bank.truthTable:bank.inference)[0];
        const answer={...request,eventKey:trial===2?'tt-c1-col1-house0':'roi-c1-MP',questionId:q.id,choice:q.correctChoice,bonus:999999};
        const answers=await Promise.all(Array.from({length:8},()=>call(uid,'/trials/quiz-answer',answer).then(ok)));
        const bonus=trial===2?10:30;
        assert.ok(answers.every(a=>a.pendingBonus===bonus&&a.coins===200000));
        for(let n=0;n<2;n++) {const retry={...request,operationId:id()};await Promise.all([call(uid,'/trials/retry',retry).then(ok),call(uid,'/trials/retry',retry).then(ok)]);}
        assert.equal((await call(uid,'/trials/retry',{...request,operationId:id()})).status,409);
        const reload=await ok(await call(uid,'/trials/start',{trial,attemptId:id(),newAttempt:false}));
        assert.equal(reload.attemptId,request.attemptId);assert.equal(reload.quizAnswers.length,1);
        await ok(await call(uid,'/trials/quiz-answer',answer));
        const receipts=await Promise.all(Array.from({length:8},()=>call(uid,'/trials/complete',{...request,reward:999999}).then(ok)));
        assert.ok(receipts.every(a=>a.reward===500+bonus&&a.quizBonus===bonus&&a.reduction===500));
        const saved=(await db.ref('users/'+uid).get()).val();
        assert.equal(saved.coins,200500+bonus);assert.equal(saved.gems,2000);assert.equal(saved.inventory.legacy_outfit,true);assert.equal(saved.unlockedStage,8);
      }
      for(const item of catalog.items) {
        const uid=await account(item.id,item.character), price=pricesFor(item);
        const purchase=currency=>({itemId:item.id,currency,expectedPrice:price[currency],economyVersion:2});
        assert.equal((await call(uid,'/purchase',{...purchase('coins'),expectedPrice:1})).status,409);
        assert.equal((await call(uid,'/purchase',{...purchase('gems'),currency:'usd'})).status,409);
        const receipts=await Promise.all([call(uid,'/purchase',purchase('coins')).then(ok),call(uid,'/purchase',purchase('gems')).then(ok),call(uid,'/purchase',purchase('coins')).then(ok)]);
        const saved=await ok(await call(uid,'/wallet'));
        assert.ok(saved.inventory.includes(item.id)&&saved.inventory.includes('legacy_outfit'));
        assert.ok((saved.coins===200000-price.coins&&saved.gems===2000)||(saved.coins===200000&&saved.gems===2000-price.gems));
        assert.ok(receipts.every(r=>r.inventory.includes(item.id)));
        assert.equal((await ok(await call(uid,'/purchase',purchase('gems')))).coins,saved.coins);
      }
      const limited=await account('limited-coins');await db.ref('users/'+limited+'/coins').set(14000);
      const competing=await Promise.all(['Common_Skin','Rare_Skin'].map(itemId=>call(limited,'/purchase',{
        itemId,currency:'coins',expectedPrice:pricesFor(catalog.items.find(i=>i.id===itemId)).coins,economyVersion:2
      })));
      assert.deepEqual(competing.map(r=>r.status).sort(),[200,409]);
      const limitedWallet=await ok(await call(limited,'/wallet'));
      assert.ok([11000,2000].includes(limitedWallet.coins));assert.equal(limitedWallet.gems,2000);
      assert.equal(limitedWallet.inventory.length,2); // One purchase plus the preserved legacy outfit.
    } finally {
      await new Promise(r=>server.close(r));await Promise.all(ids.map(uid=>db.ref('users/'+uid).remove()));
      await deleteApp(getApp('commerce-data'));await deleteApp(getApp('commerce-auth'));
    }
  });
