const { onRequest } = require('firebase-functions/v2/https');
const { initializeApp, getApps } = require('firebase-admin/app');
const { getAuth } = require('firebase-admin/auth');
const { getDatabase } = require('firebase-admin/database');
const crypto = require('node:crypto');
const { PRODUCT, verifySignature, fulfill, assertLocal } = require('./payment-core');

function resources() {
  assertLocal(process.env);
  const authApp = getApps().find(a => a.name === 'player-auth') ||
    initializeApp({ projectId: 'logic-legends-ac66c' }, 'player-auth');
  const dataApp = getApps().find(a => a.name === 'test-data') || initializeApp({
    projectId: 'demo-logic-legends',
    databaseURL: 'http://127.0.0.1:9000?ns=demo-logic-legends-default-rtdb'
  }, 'test-data');
  return { auth: getAuth(authApp), root: getDatabase(dataApp).ref('localPayments') };
}

function configured() {
  const key = process.env.PAYMONGO_SECRET_KEY || '';
  const base = process.env.PUBLIC_BASE_URL || '';
  if (!key.startsWith('sk_test_') || key.includes('REPLACE') ||
      !base.startsWith('https://') || base.includes('YOUR-TUNNEL')) {
    throw Object.assign(new Error('Configure the test key and public HTTPS URL in functions/.env.local, then restart.'), { status: 503 });
  }
  return { key, base: base.replace(/\/$/, '') };
}

async function paymongo(path, body) {
  const { key } = configured();
  const response = await fetch('https://api.paymongo.com/' + path, {
    method: body ? 'POST' : 'GET',
    headers: { Authorization: 'Basic ' + Buffer.from(key + ':').toString('base64'), 'Content-Type': 'application/json' },
    ...(body ? { body: JSON.stringify({ data: { attributes: body } }) } : {}),
    signal: AbortSignal.timeout(20000)
  });
  if (!response.ok) throw Object.assign(new Error('PayMongo request failed. Check test-mode payment methods and configuration.'), { status: 502 });
  const result = await response.json();
  if (result.data?.attributes?.livemode !== false) throw new Error('Rejected non-test PayMongo response.');
  return result.data;
}

async function reconcile(root, id, order) {
  if (order.status !== 'paid' && order.sessionId) {
    const session = await paymongo('v1/checkout_sessions/' + encodeURIComponent(order.sessionId));
    // A transaction's first callback can receive a cold-cache null value.
    // Return a value so Firebase fetches/retries against the server state.
    await root.transaction(state => fulfill(state, id, session) || state || {});
  }
}

exports.payments = onRequest({ region: 'us-central1', timeoutSeconds: 60 }, async (req, res) => {
  res.set('Cache-Control', 'no-store');
  try {
    const { auth, root } = resources();
    const path = req.path.replace(/\/$/, '') || '/';
    if (req.method === 'GET' && path === '/health') return res.json({ mode: 'test', storage: 'local-emulator' });
    if (req.method === 'GET' && ['/success', '/cancel'].includes(path)) {
      return res.type('html').send('<!doctype html><meta name="viewport" content="width=device-width"><title>Logic Legends test checkout</title><h1>Return to Logic Legends</h1><p>The game will check your payment status. Test gems are stored locally only.</p>');
    }
    if (req.method === 'POST' && path === '/webhook') {
      if (!verifySignature(req.rawBody, req.get('Paymongo-Signature'), process.env.PAYMONGO_WEBHOOK_SECRET)) {
        return res.status(401).json({ error: 'Invalid webhook signature.' });
      }
      const event = req.body?.data?.attributes;
      if (event?.type !== 'checkout_session.payment.paid') return res.sendStatus(200);
      const session = event.data;
      const id = session?.attributes?.reference_number;
      if (session?.attributes?.livemode !== false || !/^[a-f0-9]{64}$/.test(id || '')) return res.sendStatus(400);
      const order = (await root.child('orders/' + id).get()).val();
      if (!order || !order.sessionId) return res.sendStatus(409);
      // Retrieve directly from PayMongo before fulfilling even a signed event.
      await reconcile(root, id, order);
      return res.sendStatus(200);
    }
    const token = /^Bearer (.+)$/.exec(req.get('Authorization') || '')?.[1];
    let uid;
    try { uid = (await auth.verifyIdToken(token || '')).uid; }
    catch { return res.status(401).json({ error: 'Sign in to Logic Legends again.' }); }

    if (req.method === 'POST' && path === '/checkout') {
      const { base } = configured();
      if (req.body?.productId !== PRODUCT.id || !/^[a-f0-9]{32}$/.test(req.body?.requestId || '')) {
        return res.status(400).json({ error: 'Invalid product or request ID.' });
      }
      const id = crypto.createHash('sha256').update(uid + ':' + req.body.requestId).digest('hex');
      const orderRef = root.child('orders/' + id);
      const lock = await orderRef.transaction(old => old ? undefined : {
        id, uid, productId: PRODUCT.id, gems: PRODUCT.gems, amount: PRODUCT.amount,
        currency: PRODUCT.currency, status: 'creating', createdAt: Date.now()
      });
      if (!lock.committed) {
        const existing = lock.snapshot.val();
        if (existing?.checkoutUrl) return res.json({ orderId: id, checkoutUrl: existing.checkoutUrl });
        return res.status(409).json({ error: 'This checkout is pending or failed. Check its status before starting another.' });
      }
      try {
        const session = await paymongo('v2/checkout_sessions', {
          line_items: [{ name: '5 Test Gems', amount: PRODUCT.amount, currency: 'PHP', quantity: 1 }],
          payment_method_types: ['qrph'], reference_number: id,
          success_url: base + '/success', cancel_url: base + '/cancel'
        });
        const url = new URL(session.attributes.checkout_url);
        if (url.protocol !== 'https:' || url.hostname !== 'checkout.paymongo.com') throw new Error('Invalid checkout URL.');
        await orderRef.update({ sessionId: session.id, checkoutUrl: url.href, status: 'pending' });
        return res.json({ orderId: id, checkoutUrl: url.href });
      } catch (error) {
        await orderRef.update({ status: 'creation_failed' });
        throw error;
      }
    }
    if (req.method === 'GET' && path === '/wallet') {
      const key = crypto.createHash('sha256').update(uid).digest('hex');
      return res.json({ gems: (await root.child('wallets/' + key + '/gems').get()).val() || 0 });
    }
    if (req.method === 'GET' && path.startsWith('/orders/')) {
      const id = path.slice('/orders/'.length);
      if (!/^[a-f0-9]{64}$/.test(id)) return res.sendStatus(400);
      const order = (await root.child('orders/' + id).get()).val();
      if (!order || order.uid !== uid) return res.status(404).json({ error: 'Order not found.' });
      await reconcile(root, id, order);
      const updated = (await root.child('orders/' + id).get()).val();
      return res.json({ orderId: id, status: updated.status, gems: updated.status === 'paid' ? PRODUCT.gems : 0 });
    }
    return res.status(404).json({ error: 'Unknown endpoint.' });
  } catch (error) {
    // Do not print tokens, request bodies, keys, or payment customer details.
    console.error('Local payment request failed:', error.code || error.name);
    return res.status(error.status || 500).json({ error: error.status ? error.message : 'Local payment service failed. Check the emulator terminal.' });
  }
});
