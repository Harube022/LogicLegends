const { test } = require('node:test');
const assert = require('node:assert/strict');
const { once } = require('node:events');
const { startServer } = require('./server');

test('Render setup binds all interfaces and serves health without Firebase credentials', async () => {
  const server = startServer({ RENDER: 'true', PORT: '0', STORAGE_MODE: 'emulator', COMMERCE_ENABLED: 'false' });
  try {
    await once(server, 'listening');
    assert.equal(server.address().address, '0.0.0.0');
    const base = 'http://127.0.0.1:' + server.address().port;
    const health = await fetch(base + '/health');
    assert.equal(health.status, 200);
    assert.deepEqual(await health.json(), { mode: 'setup', paymentsEnabled: false });
    const checkout = await fetch(base + '/checkout', { method: 'POST' });
    assert.equal(checkout.status, 503);
    assert.equal((await checkout.json()).code, 'SHOP_SETUP');
  } finally { await new Promise(resolve => server.close(resolve)); }
});

test('local emulator setup remains loopback-only', async () => {
  const server = startServer({ PORT: '0', STORAGE_MODE: 'emulator', COMMERCE_ENABLED: 'false' });
  try {
    await once(server, 'listening');
    assert.equal(server.address().address, '127.0.0.1');
  } finally { await new Promise(resolve => server.close(resolve)); }
});

test('invalid port fails before starting the server', () => {
  assert.throws(() => startServer({ PORT: 'not-a-port' }), /PORT must be an integer/);
});
