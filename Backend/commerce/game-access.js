const { fail } = require('./wallet');

const BANNED = 'Your Logic Legends account has been banned. Please contact the administrators if you believe this is a mistake.';
const NCST = /^[a-z0-9.!#$%&'*+\/=?^_`{|}~-]+@ncst\.edu\.ph$/i;
const banned = () => Object.assign(fail(BANNED, 403), { code: 'ACCOUNT_BANNED' });
const denied = (message, code) => Object.assign(fail(message, 403), { code });

function decideAccess(user, moderation, now = Date.now()) {
  if (moderation?.ban?.active === true) throw banned();
  if (!user || user.disabled) throw denied('This account is disabled.', 'ACCOUNT_DISABLED');
  if (user.emailVerified !== true) throw denied('Please verify your email address before logging in.', 'EMAIL_UNVERIFIED');
  const claims = user.customClaims || {};
  const admin = claims.admin === true || claims.super_admin === true;
  const email = typeof user.email === 'string' ? user.email.trim() : '';
  if (!admin && (email.length > 254 || !NCST.test(email)))
    throw denied('Use a verified @ncst.edu.ph account to play Logic Legends.', 'DOMAIN_NOT_ALLOWED');
  const restriction = moderation?.restriction;
  const restrictions = restriction?.active === true && (!restriction.expiresAt || restriction.expiresAt > now)
    && Array.isArray(restriction.capabilities) ? restriction.capabilities : [];
  return { uid: user.uid, admin, feedbackRestricted: restrictions.includes('feedback'), policyVersion: 1 };
}

// One authoritative Auth record read. The signed token determines the UID;
// current roles, disabled state and revocation come from Firebase, not the token's claims.
async function authorizeGame({ token, verifyToken, getUser, getModeration, onIdentity, now }) {
  let identity;
  try { identity = await verifyToken(token); } catch { throw fail('Please sign in again.', 401); }
  if (typeof identity?.uid !== 'string' || !identity.uid) throw fail('Please sign in again.', 401);
  onIdentity?.(identity.uid);
  const [user, moderation] = await Promise.all([getUser(identity.uid), getModeration(identity.uid)]);
  if (moderation?.ban?.active === true) throw banned();
  if (user?.uid !== identity.uid) throw fail('Please sign in again.', 401);
  const revokedAt = Date.parse(user.tokensValidAfterTime || '');
  if (Number.isFinite(revokedAt) && (!Number.isFinite(identity.auth_time) || identity.auth_time * 1000 < revokedAt))
    throw fail('Your session has expired. Please sign in again.', 401);
  return decideAccess(user, moderation, now);
}

module.exports = { authorizeGame, decideAccess, NCST };
