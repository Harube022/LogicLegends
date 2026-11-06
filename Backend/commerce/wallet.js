const crypto = require('node:crypto');
const catalog = require('./catalog.json');
const { fail } = require('./wallet-errors');
const economy = require('./economy');
function balance(value) {
  if (value == null) return 0;
  if (!Number.isSafeInteger(value) || value < 0 || value > 2147483647) throw fail('Invalid wallet balance.', 409);
  return value;
}
function signature(raw, header, secret, live, now = Date.now()) {
  if (!secret || secret.includes('REPLACE')) return false;
  const parts = Object.fromEntries(String(header || '').split(',').map(s => s.trim().split('=')));
  const sig = parts[live ? 'li' : 'te'];
  if (!/^\d+$/.test(parts.t || '') || !/^[a-f0-9]{64}$/i.test(sig || '') || Math.abs(now / 1000 - Number(parts.t)) > 300) return false;
  const expected = crypto.createHmac('sha256', secret).update(parts.t + '.').update(raw).digest();
  return crypto.timingSafeEqual(expected, Buffer.from(sig, 'hex'));
}
function validatePayment(session, order, live) {
  const a = session?.attributes;
  if (session?.id !== order.sessionId || a?.livemode !== live || order.live !== live || a.reference_number !== order.id) return null;
  const items = a.line_items;
  if (!Array.isArray(items) || items.length !== 1 || items[0].amount !== order.amount ||
      items[0].currency !== order.currency || items[0].quantity !== 1) return null;
  return (a.payments || []).find(p => p.id && p.attributes?.status === 'paid' &&
    p.attributes.livemode === live && p.attributes.amount === order.amount && p.attributes.currency === order.currency) || null;
}
function grant(account, order, payment) {
  if (!account) return account;
  account.paymentGrants ||= {};
  if (account.paymentGrants[order.id]) return account;
  account.gems = balance(balance(account.gems) + order.gems);
  account.paymentGrants[order.id] = { paymentId: payment.id, gems: order.gems, at: Date.now() };
  return account;
}
function buy(account, itemId, currency = 'gems') {
  const item = catalog.items.find(i => i.id === itemId);
  if (!item) throw fail('This item is unavailable.');
  if (!account || account.base_character !== item.character) throw fail('This outfit does not match your character.', 409);
  account.inventory ||= {};
  if (account.inventory[itemId] === true) return account;
  if (!['coins', 'gems'].includes(currency)) throw fail('Choose Coins or Gems.');
  const price = economy.pricesFor(item)[currency], current = balance(account[currency]);
  if (current < price) throw fail('Not enough ' + currency + '.', 409);
  account[currency] = current - price;
  account.inventory[itemId] = true;
  return account;
}
function present(account, sandbox, live) {
  return { gems: balance(account?.gems), coins: balance(account?.coins),
    character: account?.base_character || '',
    inventory: Object.keys(account?.inventory || {}).filter(k => account.inventory[k] === true),
    equippedClothes: account?.equipped?.clothes || (account?.base_character === 'Female_Character' ? 'female_default' : 'male_default'),
    equippedPet: account?.equipped?.pets || '', sandbox, live,
    economy: economy.config,
    catalog: catalog.items.map(item => ({ id: item.id, rarity: item.rarity, ...economy.pricesFor(item) })) };
}
async function mutate(ref, update) {
  await ref.get();
  let rejection;
  const result = await ref.transaction(value => {
    rejection = null;
    try { return update(value); }
    catch (error) { rejection = error; return; }
  });
  if (rejection) throw rejection;
  return result;
}
module.exports = { catalog, balance, fail, signature, validatePayment, grant, buy, present, mutate };
