const { test } = require('node:test');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');

test('merge rollout defaults off: old clients keep their wallet/shop/rewards; v2 writes are blocked until the programmer connects it',
  { skip: process.env.RUN_EMULATOR_TESTS !== '1', timeout:120000 }, async()=>{
    process.env.FIREBASE_DATABASE_EMULATOR_HOST='127.0.0.1:9000';
    const {createApp}=require('./app'), {getApp,deleteApp}=require('firebase-admin/app'), {getDatabase}=require('firebase-admin/database');
    const uid='economy-rollout-'+crypto.randomBytes(6).toString('hex'), id=()=>crypto.randomBytes(16).toString('hex');
    const env={STORAGE_MODE:'emulator',PAYMENT_MODE:'test',FIREBASE_DATABASE_EMULATOR_HOST:'127.0.0.1:9000',FIREBASE_PROJECT_ID:'demo-logic-legends'};
    const integrations={verifyIdToken:async()=>({uid}),getUser:async()=>({uid,email:'ordinary@example.test',disabled:false})};
    const server=createApp(env,integrations).listen(0,'127.0.0.1');await new Promise(r=>server.once('listening',r));
    const db=getDatabase(getApp('commerce-data')),ref=db.ref('users/'+uid),base='http://127.0.0.1:'+server.address().port;
    const call=(path,body)=>fetch(base+path,{method:body?'POST':'GET',headers:{Authorization:'Bearer fixture','Content-Type':'application/json'},...(body?{body:JSON.stringify(body)}:{})});
    const ok=async r=>{const b=await r.json();assert.equal(r.status,200,JSON.stringify(b));return b;};
    try{
      await ref.set({coins:37,gems:100,base_character:'Male_Character',inventory:{legacy_outfit:true},unlockedStage:8});
      const before=(await ref.get()).val();assert.equal((await call('/economy')).status,503);
      assert.equal((await call('/trials/start',{trial:1,attemptId:id(),newAttempt:true,economyVersion:2})).status,503);
      assert.equal((await call('/purchase',{itemId:'Common_Skin',currency:'coins',expectedPrice:3000,economyVersion:2})).status,503);
      assert.deepEqual((await ref.get()).val(),before,'a merge alone does not change new economy state or balances');
      const wallet=await ok(await call('/wallet'));assert.equal(wallet.economy,undefined);assert.equal(wallet.coins,37);assert.equal(wallet.gems,100);
      const bought=await ok(await call('/purchase',{itemId:'Common_Skin'}));assert.equal(bought.gems,90);assert.equal(bought.coins,37);assert.ok(bought.inventory.includes('legacy_outfit'));
      await ok(await call('/purchase',{itemId:'Common_Skin'}));assert.equal((await ref.child('gems').get()).val(),90);
      const request={trial:1,attemptId:id(),newAttempt:true};await ok(await call('/trials/start',request));
      for(let n=0;n<2;n++)await ok(await call('/trials/retry',{...request,operationId:id()}));
      const receipt=await ok(await call('/trials/complete',request));assert.equal(receipt.reward,562);assert.equal(receipt.coins,599);
      await ok(await call('/trials/complete',request));assert.equal((await ref.child('coins').get()).val(),599);
      assert.equal((await ref.child('unlockedStage').get()).val(),8);
    }finally{
      await new Promise(r=>server.close(r));await ref.remove();await deleteApp(getApp('commerce-data'));await deleteApp(getApp('commerce-auth'));
    }
  });
