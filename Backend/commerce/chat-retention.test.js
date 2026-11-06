const { test } = require('node:test');
const assert = require('node:assert/strict');
const { once } = require('node:events');
const { sweepMessages, RETENTION_MS, attachChatCleanup } = require('./chat-retention');
const NOW = 1800000000000;

function fixture() {
  const cutoff = NOW - RETENTION_MS;
  const data = { alice: { bob: {
    old: { sentAt: cutoff - 1, text: 'old' }, boundary: { sentAt: cutoff, inviteCode: 'AB3D7K' },
    recent: { sentAt: cutoff + 1, text: 'keep' }, future: { sentAt: NOW + 100, text: 'legacy clock skew' },
    malformed: { text: 'missing date' }
  } }, carol: { dave: { removedFriends: { sentAt: cutoff - 10 } } } };
  const writes = [];
  const store = {
    keys: async parts => Object.keys(parts.reduce((node, key) => node[key] || {}, data)),
    page: async (first, second, max, limit, after) => Object.entries(data[first][second])
      .filter(([id, value]) => typeof value.sentAt === 'number' && value.sentAt >= 1 && value.sentAt <= max &&
        (!after || value.sentAt > after.sentAt || value.sentAt === after.sentAt && id > after.id))
      .sort(([a, av], [b, bv]) => av.sentAt - bv.sentAt || a.localeCompare(b)).slice(0, limit).map(([id, value]) => ({ id, value })),
    remove: async updates => {
      writes.push(updates);
      // A new send races with the deletion; it must survive.
      data.alice.bob.concurrent = { sentAt: NOW, text: 'new' };
      for (const path of Object.keys(updates)) {
        assert.equal(updates[path], null);
        const [first, second, id] = path.split('/'); delete data[first][second][id];
      }
    }
  };
  return { store, data, writes };
}

test('cleanup deletes expired text and invites in pages, including removed friendships, preserving new sends', async () => {
  const { store, data, writes } = fixture();
  assert.deepEqual(await sweepMessages(store, { now: () => NOW, pageSize: 1 }),
    { dryRun: false, cutoff: NOW - RETENTION_MS, conversations: 2, deleted: 3 });
  assert.deepEqual(Object.keys(data.alice.bob).sort(), ['concurrent', 'future', 'malformed', 'recent']);
  assert.deepEqual(data.carol.dave, {});
  assert.equal(writes.length, 3);
  assert.equal((await sweepMessages(store, { now: () => NOW })).deleted, 0, 'rerunning is idempotent');
});

test('dry run counts exactly the same expired records without deleting anything', async () => {
  const { store, data, writes } = fixture(); const before = structuredClone(data);
  assert.equal((await sweepMessages(store, { now: () => NOW, dryRun: true, pageSize: 1 })).wouldDelete, 3);
  assert.equal(writes.length, 0); assert.deepEqual(data, before);
});

test('a failed delete aborts without advancing the page; the next sweep retries it', async () => {
  const { store, writes } = fixture(); const remove = store.remove;
  store.remove = async () => { throw Error('offline'); };
  await assert.rejects(sweepMessages(store, { now: () => NOW }), /offline/);
  assert.equal(writes.length, 0);
  store.remove = remove;
  assert.equal((await sweepMessages(store, { now: () => NOW })).deleted, 3);
});

test('cleanup endpoint needs its private token and runs with payments disabled', async () => {
  const { createApp } = require('./app'); const { store } = fixture();
  const env = { COMMERCE_ENABLED: 'false', CHAT_CLEANUP_ENABLED: 'true', CHAT_CLEANUP_TOKEN: 'test-secret-that-is-longer-than-32-characters' };
  const app = createApp(env, { chatCleanup: { createStore: () => store, now: () => NOW } });
  const server = app.listen(0, '127.0.0.1'); await once(server, 'listening');
  const base = 'http://127.0.0.1:' + server.address().port;
  const post = (suffix, token) => fetch(base + '/internal/chat-cleanup' + suffix,
    { method: 'POST', headers: token ? { Authorization: 'Bearer ' + token } : {} });
  try {
    assert.equal((await post('', null)).status, 401);
    assert.equal((await post('', 'wrong')).status, 401);
    assert.equal((await (await post('?dryRun=true', env.CHAT_CLEANUP_TOKEN)).json()).wouldDelete, 3);
    assert.equal((await (await post('', env.CHAT_CLEANUP_TOKEN)).json()).deleted, 3);
    const health = await (await fetch(base + '/health')).json();
    assert.equal(health.paymentsEnabled, false);
    assert.equal(health.chatCleanup.lastSucceededAt, NOW);
    assert.equal((await fetch(base + '/checkout', { method: 'POST' })).status, 503);
  } finally { await new Promise(resolve => server.close(resolve)); }
});

test('startup sweeps once, overlapping sweeps are rejected and close stops the timer', async () => {
  const app = require('express')(); const { store } = fixture();
  let release; const blocked = new Promise(resolve => { release = resolve; });
  const keys = store.keys;
  store.keys = async parts => { await blocked; return keys(parts); };
  const controller = attachChatCleanup(app, { CHAT_CLEANUP_ENABLED: 'true' }, { createStore: () => store, now: () => NOW });
  controller.start();
  await assert.rejects(controller.run(), error => error.code === 'CHAT_CLEANUP_BUSY');
  controller.stop(); release();
  while (controller.status.running) await new Promise(resolve => setImmediate(resolve));
  assert.equal(controller.status.lastDeleted, 3);
});
