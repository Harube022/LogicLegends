const express = require('express');
const crypto = require('node:crypto');
const { initializeApp, getApps, cert } = require('firebase-admin/app');
const { getAuth } = require('firebase-admin/auth');
const { getDatabase } = require('firebase-admin/database');
const { catalog, fail, signature, validatePayment, grant, buy, present, mutate } = require('./wallet');

function createApp(env = process.env, integrations = {}) {
  // Let a new hosting service obtain its HTTPS URL before private credentials
  // and the production wallet protections are configured. No payment or
  // account routes are available while this explicit switch is off.
  if (env.COMMERCE_ENABLED === 'false') {
    const setup = express();
    setup.disable('x-powered-by');
    setup.use((req, res, next) => { res.set('Cache-Control', 'no-store'); next(); });
    setup.get('/health', (req, res) => res.json({ mode: 'setup', paymentsEnabled: false }));
    setup.use((req, res) => res.status(503).json({ error: 'The shop is being configured. Please try again later.' }));
    return setup;
  }
  const sandbox = env.STORAGE_MODE === 'emulator';
  const live = env.PAYMENT_MODE === 'live';
  if (!['emulator', 'firebase'].includes(env.STORAGE_MODE) || !['test', 'live'].includes(env.PAYMENT_MODE)) throw new Error('Configure storage and payment modes explicitly.');
  if ((sandbox && (live || env.FIREBASE_DATABASE_EMULATOR_HOST !== '127.0.0.1:9000')) ||
      (!sandbox && (!live || env.FIREBASE_DATABASE_EMULATOR_HOST)) || env.FIREBASE_AUTH_EMULATOR_HOST) {
    throw new Error('Use emulator storage with test payments, or real Firebase storage with live payments.');
  }
  const projectId = env.FIREBASE_PROJECT_ID;
  if (!projectId) throw new Error('FIREBASE_PROJECT_ID is required.');
  if (!sandbox && env.LIVE_WALLET_READY !== 'true') throw new Error('Configure protected wallet rules and trusted gameplay rewards before enabling LIVE_WALLET_READY.');
  const authApp = getApps().find(a => a.name === 'commerce-auth') || initializeApp({ projectId }, 'commerce-auth');
  const options = sandbox ? { projectId: 'demo-logic-legends', databaseURL: 'http://127.0.0.1:9000?ns=demo-logic-legends-default-rtdb' }
    : { projectId, databaseURL: env.FIREBASE_DATABASE_URL,
        ...(env.FIREBASE_SERVICE_ACCOUNT_JSON ? { credential: cert(JSON.parse(env.FIREBASE_SERVICE_ACCOUNT_JSON)) } : {}) };
  if (!sandbox && !env.FIREBASE_DATABASE_URL?.startsWith('https://')) throw new Error('Real Firebase database URL is required.');
  const dataApp = getApps().find(a => a.name === 'commerce-data') || initializeApp(options, 'commerce-data');
  const db = getDatabase(dataApp);
  const auth = getAuth(authApp);
  const users = db.ref('users');
  const orders = db.ref('commerceOrders');
  const app = express();
  app.disable('x-powered-by');
  app.use((req, res, next) => { res.set('Cache-Control', 'no-store'); next(); });
  const asyncRoute = fn => (req, res, next) => Promise.resolve(fn(req, res)).catch(next);

  function configuration() {
    const key = env.PAYMONGO_SECRET_KEY || '';
    const base = (env.PUBLIC_BASE_URL || '').replace(/\/$/, '');
    if (!key.startsWith(live ? 'sk_live_' : 'sk_test_') || key.includes('REPLACE') ||
        !base.startsWith('https://') || base.includes('YOUR-TUNNEL')) throw fail('Payment setup is incomplete. Please try again later.', 503);
    return { key, base };
  }
  async function paymongo(path, body) {
    if (integrations.paymongo) return integrations.paymongo(path, body);
    const { key } = configuration();
    const response = await fetch('https://api.paymongo.com/' + path, {
      method: body ? 'POST' : 'GET', signal: AbortSignal.timeout(20000),
      headers: { Authorization: 'Basic ' + Buffer.from(key + ':').toString('base64'), 'Content-Type': 'application/json' },
      ...(body ? { body: JSON.stringify({ data: { attributes: body } }) } : {})
    });
    if (!response.ok) throw fail('Payment provider is unavailable. Please retry.', 502);
    const result = (await response.json()).data;
    if (result?.attributes?.livemode !== live) throw fail('Payment mode mismatch.', 502);
    return result;
  }
  async function reconcile(order) {
    if (!order?.sessionId || order.status === 'paid') return;
    const session = await paymongo('v1/checkout_sessions/' + encodeURIComponent(order.sessionId));
    const payment = validatePayment(session, order, live);
    if (!payment) {
      if (session.attributes.status === 'expired') await orders.child(order.id).update({ status: 'expired' });
      return;
    }
    // Grant marker and gems are in ONE account transaction. Replaying after a
    // crash between this commit and the order update never credits twice.
    const ref = users.child(order.uid);
    const result = await mutate(ref, account => grant(account, order, payment));
    if (!result.snapshot.child('paymentGrants/' + order.id).exists()) throw fail('Account is not ready. Please retry.', 409);
    await orders.child(order.id).update({ status: 'paid', paymentId: payment.id });
  }
  app.get('/health', (req, res) => res.json({ sandbox, live, mode: sandbox ? 'sandbox' : 'live' }));
  app.get(['/success', '/cancel'], (req, res) => res.type('html').send('<!doctype html><meta name="viewport" content="width=device-width"><title>Logic Legends</title><h1>Return to Logic Legends</h1><p>Your game will verify the payment and refresh your gems.</p>'));
  app.post('/webhook', express.raw({ type: 'application/json', limit: '256kb' }), asyncRoute(async (req, res) => {
    if (!Buffer.isBuffer(req.body) || !signature(req.body, req.get('Paymongo-Signature'), env.PAYMONGO_WEBHOOK_SECRET, live)) throw fail('Invalid signature.', 401);
    let event;
    try { event = JSON.parse(req.body.toString()).data?.attributes; } catch { throw fail('Invalid event.'); }
    if (event?.type !== 'checkout_session.payment.paid') return res.sendStatus(200);
    const id = event.data?.attributes?.reference_number;
    if (!/^[a-f0-9]{64}$/.test(id || '') || event.data?.attributes?.livemode !== live) throw fail('Invalid payment event.');
    const order = (await orders.child(id).get()).val();
    if (!order?.sessionId) throw fail('Order is not ready.', 409);
    await reconcile(order);
    return res.sendStatus(200);
  }));
  app.use(express.json({ limit: '4kb' }));
  // Bound per-player load on a single Node process. Scale-out hosts should also
  // enforce a shared/API-gateway rate limit.
  const buckets = new Map();
  app.use(asyncRoute(async (req, res) => {
    const token = /^Bearer (.+)$/.exec(req.get('Authorization') || '')?.[1];
    let uid;
    try { uid = (await (integrations.verifyIdToken || (t => auth.verifyIdToken(t)))(token || '')).uid; } catch { throw fail('Please sign in again.', 401); }
    const now = Date.now();
    for (const [key, value] of buckets) if (value.until < now) buckets.delete(key);
    const bucket = buckets.get(uid) || { until: now + 60000, count: 0 };
    if (++bucket.count > 90) throw fail('Please wait before trying again.', 429);
    buckets.set(uid, bucket);
    const userRef = users.child(uid);
    // Only the local sandbox may bootstrap an empty test wallet from the
    // character selected by the already authenticated client. No coins or gems
    // are accepted from the client, and no real profile is copied or changed.
    if (req.method === 'POST' && req.path === '/account') {
      if (sandbox) {
        const character = req.body?.character;
        if (!['Male_Character', 'Female_Character'].includes(character)) throw fail('Choose your character first.');
        await userRef.transaction(old => old || { base_character: character, gems: 0, coins: 0 });
      }
      const account = (await userRef.get()).val();
      if (!account?.base_character) throw fail('Choose your character first.', 409);
      return res.json(present(account, sandbox, live));
    }
    if (req.method === 'GET' && req.path === '/wallet') return res.json(present((await userRef.get()).val(), sandbox, live));
    if (req.method === 'POST' && req.path === '/equip') {
      const itemId = req.body?.itemId;
      if (typeof itemId !== 'string' || !/^[A-Za-z0-9_]+$/.test(itemId)) throw fail('Invalid outfit.');
      const result = await mutate(userRef, account => {
        if (!account) return account;
        const defaultId = account.base_character === 'Female_Character' ? 'female_default' : 'male_default';
        const item = catalog.items.find(i => i.id === itemId);
        if (itemId !== defaultId && (!item || item.character !== account.base_character || account.inventory?.[itemId] !== true)) throw fail('You do not own this outfit.', 409);
        account.equipped ||= {};
        account.equipped.clothes = itemId;
        return account;
      });
      return res.json(present(result.snapshot.val(), sandbox, live));
    }
    if (req.method === 'POST' && req.path === '/purchase') {
      const itemId = req.body?.itemId;
      if (typeof itemId !== 'string' || !catalog.items.some(i => i.id === itemId)) throw fail('Unknown item.');
      const result = await mutate(userRef, account => account ? buy(account, itemId) : account);
      if (result.snapshot.child('inventory/' + itemId).val() !== true) throw fail('Could not confirm your purchase.', 409);
      return res.json({ ...present(result.snapshot.val(), sandbox, live), itemId });
    }
    if (req.method === 'POST' && req.path === '/checkout') {
      const pack = catalog.packs.find(p => p.id === req.body?.productId);
      if (!pack || !/^[a-f0-9]{32}$/.test(req.body?.requestId || '')) throw fail('Invalid checkout request.');
      if (!(await userRef.child('base_character').get()).exists()) throw fail('Open the shop again before buying.', 409);
      const { base } = configuration();
      const id = crypto.createHash('sha256').update((live ? 'live:' : 'test:') + uid + ':' + req.body.requestId).digest('hex');
      const ref = orders.child(id);
      const lock = await ref.transaction(old => old ? undefined : {
        id, uid, productId: pack.id, gems: pack.gems, amount: pack.amount, currency: pack.currency,
        live, status: 'creating', createdAt: Date.now()
      });
      if (!lock.committed) {
        const old = lock.snapshot.val();
        if (old?.checkoutUrl) return res.json({ orderId: id, checkoutUrl: old.checkoutUrl, sandbox, live });
        throw fail('Checkout is still being created. Retry to check the same order.', 409);
      }
      try {
        const session = await paymongo('v2/checkout_sessions', {
          line_items: [{ name: pack.name + (live ? '' : ' (Test)'), amount: pack.amount, currency: pack.currency, quantity: 1 }],
          payment_method_types: (env.PAYMENT_METHODS || 'qrph').split(',').map(s => s.trim()), reference_number: id,
          success_url: base + '/success', cancel_url: base + '/cancel'
        });
        const url = new URL(session.attributes.checkout_url);
        if (url.protocol !== 'https:' || url.hostname !== 'checkout.paymongo.com') throw fail('Invalid payment URL.', 502);
        await ref.update({ sessionId: session.id, checkoutUrl: url.href, status: 'pending' });
        return res.json({ orderId: id, checkoutUrl: url.href, sandbox, live });
      } catch (error) {
        // Keep the request reserved after an uncertain timeout. A new automatic
        // session could charge twice. Resolve the provider request before retry.
        await ref.update({ status: 'needs_review' });
        throw error;
      }
    }
    if (req.method === 'GET' && req.path.startsWith('/orders/')) {
      const id = req.path.slice(8);
      if (!/^[a-f0-9]{64}$/.test(id)) throw fail('Invalid order ID.');
      const order = (await orders.child(id).get()).val();
      if (!order || order.uid !== uid) throw fail('Order not found.', 404);
      await reconcile(order);
      return res.json({ orderId: id, status: (await orders.child(id + '/status').get()).val(), sandbox, live });
    }
    throw fail('Unknown endpoint.', 404);
  }));
  app.use((error, req, res, next) => {
    console.error('Commerce request failed:', error.status || error.code || error.name);
    res.status(error.status || 500).json({ error: error.status ? error.message : 'Could not reach the shop. Please retry.' });
  });
  return app;
}
module.exports = { createApp };
