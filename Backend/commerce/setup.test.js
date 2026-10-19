const { test } = require('node:test');
const assert = require('node:assert/strict');
const { createApp } = require('./app');

test('hosting setup works without credentials and disables all commerce routes', async () => {
  const server = createApp({ COMMERCE_ENABLED: 'false' }).listen(0, '127.0.0.1');
  await new Promise(resolve => server.once('listening', resolve));
  const base = 'http://127.0.0.1:' + server.address().port;
  try {
    assert.deepEqual(await (await fetch(base + '/health')).json(), { mode: 'setup', paymentsEnabled: false });
    const unavailable = await fetch(base + '/wallet');
    assert.equal((await unavailable.json()).code, 'SHOP_SETUP');
    for (const [path, method] of [['/wallet', 'GET'], ['/account', 'POST'], ['/checkout', 'POST'],
      ['/purchase', 'POST'], ['/equip', 'POST'], ['/orders/any', 'GET'], ['/webhook', 'POST']]) {
      assert.equal((await fetch(base + path, { method })).status, 503, path);
    }
  } finally { await new Promise(resolve => server.close(resolve)); }
});
