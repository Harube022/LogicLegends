const { fail, validatePayment, grant, mutate } = require('./wallet');
const CHECKOUT_LIFETIME_MS = 30 * 60 * 1000;
const terminal = order => ['paid', 'expired'].includes(order?.status);

// QR expiry alone does not close Hosted Checkout. Only a provider-confirmed
// expiry is terminal; a timer or a Cancel click is never proof of cancellation.
function lifecycle({ orders, users, paymongo, live, now = Date.now }) {
  function deadline(order) { return order.expiresAt || order.createdAt + CHECKOUT_LIFETIME_MS; }
  function validSession(session, order) {
    const a = session?.attributes;
    if (session?.id !== order.sessionId || a?.livemode !== live || order.live !== live || a.reference_number !== order.id)
      throw fail('Could not verify the checkout. Please retry.', 502);
    return a;
  }
  async function recordExpiry(order, reason) {
    await mutate(orders.child(order.id), current => !current ? current : current.status === 'paid' ? undefined :
      { ...current, status: 'expired', expiredAt: current.expiredAt || now(), expiryReason: current.expiryReason || reason, expiryCheckAt: null });
  }
  async function inspect(order, session, reason) {
    const a = validSession(session, order);
    const payment = validatePayment(session, order, live);
    if (payment) {
      const result = await mutate(users.child(order.uid), account => grant(account, order, payment));
      if (!result.snapshot.child('paymentGrants/' + order.id).exists()) throw fail('Account is not ready. Please retry.', 409);
      await orders.child(order.id).update({ status: 'paid', paymentId: payment.id, expiryCheckAt: null });
      return true;
    }
    // A paid payment with an unexpected amount must be investigated, never expired.
    if ((a.payments || []).some(p => p.attributes?.status === 'paid')) throw fail('Payment needs review. Please contact support.', 409);
    if (a.status === 'expired') { await recordExpiry(order, reason || 'provider_expired'); return true; }
    return false;
  }
  async function reconcile(order, cancel = false) {
    if (!order?.sessionId || order.status === 'paid') return order;
    const path = 'v1/checkout_sessions/' + encodeURIComponent(order.sessionId);
    let session = await paymongo(path);
    const reason = cancel ? 'player_cancelled' : 'checkout_timeout';
    if (!await inspect(order, session, reason) && (cancel || now() >= deadline(order))) {
      try {
        session = await paymongo(path + '/expire', {});
        // Read back even after a successful response: never clear local history
        // until PayMongo confirms expired (or a racing payment confirms paid).
        if (!session?.attributes) session = await paymongo(path);
        await inspect(order, session, reason);
      } catch (error) {
        if (error.providerStatus !== 400) throw error;
        // Already expired/paid and an in-flight payment all return 400.
        // Retrieve instead of assuming cancellation succeeded.
        session = await paymongo(path);
        if (!await inspect(order, session, reason)) {
          await mutate(orders.child(order.id), current => !current ? current : terminal(current) ? undefined :
            { ...current, status: 'processing', expiryCheckAt: now() + 60000 });
        }
      }
    }
    const current = (await orders.child(order.id).get()).val();
    // Upgrade legacy pending orders when they are next checked, without a
    // destructive migration or deleting successful/cancelled order history.
    if (current && !terminal(current) && !current.expiryCheckAt) {
      await mutate(orders.child(order.id), value => !value ? value : terminal(value) ? undefined :
        { ...value, expiresAt: deadline(value), expiryCheckAt: Math.max(now() + 60000, deadline(value)) });
    }
    return (await orders.child(order.id).get()).val();
  }
  function presentOrder(order) {
    return { orderId: order.id, productId: order.productId, gems: order.gems, amount: order.amount,
      currency: order.currency, status: order.status, checkoutUrl: order.checkoutUrl,
      expiresAt: deadline(order), expiryReason: order.expiryReason, sandbox: !live, live };
  }
  let sweeping = false;
  async function sweep() {
    if (sweeping) return;
    sweeping = true;
    try {
      const due = await orders.orderByChild('expiryCheckAt').startAt(1).endAt(now()).limitToFirst(50).get();
      const values = Object.values(due.val() || {});
      for (const order of values) {
        try { await reconcile(order); }
        catch (error) { console.error('Checkout expiry check failed:', error.status || error.code || error.name); }
      }
    } finally { sweeping = false; }
  }
  return { reconcile, presentOrder, sweep };
}
module.exports = { lifecycle, CHECKOUT_LIFETIME_MS, terminal };
