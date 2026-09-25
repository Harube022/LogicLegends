const crypto = require('node:crypto');

const PRODUCT = Object.freeze({ id: 'gems_5', gems: 5, amount: 100, currency: 'PHP' });

function verifySignature(raw, header, secret, now = Date.now()) {
  if (!secret || secret.startsWith('REPLACE') || !Buffer.isBuffer(raw)) return false;
  const parts = Object.fromEntries(String(header || '').split(',').map(s => s.trim().split('=')));
  if (!/^\d+$/.test(parts.t || '') || !/^[a-f0-9]{64}$/i.test(parts.te || '')) return false;
  if (Math.abs(now / 1000 - Number(parts.t)) > 300) return false;
  const expected = crypto.createHmac('sha256', secret).update(parts.t + '.').update(raw).digest();
  return crypto.timingSafeEqual(expected, Buffer.from(parts.te, 'hex'));
}

function paidPayment(session, order) {
  const a = session?.attributes;
  if (session?.id !== order.sessionId || a?.livemode !== false || a.reference_number !== order.id) return null;
  const items = a.line_items;
  if (!Array.isArray(items) || items.length !== 1 || items[0].amount !== PRODUCT.amount ||
      items[0].currency !== PRODUCT.currency || items[0].quantity !== 1) return null;
  return (a.payments || []).find(p => p.id && p.attributes?.status === 'paid' &&
    p.attributes.livemode === false && p.attributes.amount === PRODUCT.amount &&
    p.attributes.currency === PRODUCT.currency) || null;
}

// Run this inside a single database transaction: order and wallet commit together.
function fulfill(state, orderId, session) {
  const order = state?.orders?.[orderId];
  if (!order || order.productId !== PRODUCT.id || order.amount !== PRODUCT.amount ||
      order.gems !== PRODUCT.gems || order.currency !== PRODUCT.currency) return;
  if (order.status === 'paid') return;
  const payment = paidPayment(session, order);
  if (!payment) return;
  const key = crypto.createHash('sha256').update(order.uid).digest('hex');
  state.wallets ||= {};
  state.wallets[key] ||= { gems: 0 };
  state.wallets[key].gems += PRODUCT.gems;
  order.status = 'paid';
  order.paymentId = payment.id;
  order.paidAt = Date.now();
  return state;
}

function assertLocal(env) {
  if (env.FUNCTIONS_EMULATOR !== 'true' || env.FIREBASE_DATABASE_EMULATOR_HOST !== '127.0.0.1:9000') {
    throw new Error('This backend requires the local Functions and Database emulators.');
  }
  if (env.FIREBASE_AUTH_EMULATOR_HOST) throw new Error('Use real Firebase login tokens, not the Auth emulator.');
}

module.exports = { PRODUCT, verifySignature, paidPayment, fulfill, assertLocal };
