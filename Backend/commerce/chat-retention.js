const crypto = require('node:crypto');

const RETENTION_MS = 24 * 60 * 60 * 1000;
const DEFAULT_INTERVAL_MS = 15 * 60 * 1000;
const validKey = key => typeof key === 'string' && key.length > 0 && key.length <= 128 &&
  !/[.#$\[\]/\u0000-\u001f\u007f]/.test(key);
const expired = (message, cutoff) => Number.isSafeInteger(message?.sentAt) && message.sentAt > 0 && message.sentAt <= cutoff;

// Discover keys with REST shallow=true so a sweep never downloads the entire
// message tree. Only the indexed, expired pages below each conversation are read.
function createFirebaseStore(env) {
  const emulator = env.STORAGE_MODE === 'emulator';
  if (!['emulator', 'firebase'].includes(env.STORAGE_MODE)) throw new Error('Configure STORAGE_MODE for chat cleanup.');
  if (emulator && (env.RENDER === 'true' || env.FIREBASE_DATABASE_EMULATOR_HOST !== '127.0.0.1:9000' ||
      process.env.FIREBASE_DATABASE_EMULATOR_HOST !== '127.0.0.1:9000')) throw new Error('Chat emulator must use local port 9000.');
  if (!emulator && (env.FIREBASE_DATABASE_EMULATOR_HOST || process.env.FIREBASE_DATABASE_EMULATOR_HOST))
    throw new Error('Remove the database emulator setting from hosted chat cleanup.');
  const { initializeApp, getApps, cert, applicationDefault } = require('firebase-admin/app');
  const { getDatabase } = require('firebase-admin/database');
  const projectId = emulator ? (env.FIREBASE_PROJECT_ID?.startsWith('demo-') ? env.FIREBASE_PROJECT_ID : 'demo-logic-legends') : env.FIREBASE_PROJECT_ID;
  const databaseURL = emulator ? `http://127.0.0.1:9000?ns=${projectId}-default-rtdb` : env.FIREBASE_DATABASE_URL;
  if (!emulator && (!env.FIREBASE_PROJECT_ID || !databaseURL?.startsWith('https://')))
    throw new Error('Firebase project and HTTPS database URL are required for chat cleanup.');
  const credential = emulator ? undefined : env.FIREBASE_SERVICE_ACCOUNT_JSON
    ? cert(JSON.parse(env.FIREBASE_SERVICE_ACCOUNT_JSON)) : applicationDefault();
  const app = getApps().find(app => app.name === 'social-retention') || initializeApp({
    projectId, databaseURL,
    ...(credential ? { credential } : {})
  }, 'social-retention');
  const messages = getDatabase(app).ref('social/messages');
  return {
    async keys(parts) {
      if (!parts.every(validKey)) throw new Error('Invalid chat path.');
      const url = new URL(databaseURL);
      url.pathname = '/social/messages' + (parts.length ? '/' + parts.map(encodeURIComponent).join('/') : '') + '.json';
      url.searchParams.set('shallow', 'true');
      const token = emulator ? 'owner' : (await credential.getAccessToken()).access_token;
      const response = await fetch(url, { headers: { Authorization: 'Bearer ' + token }, signal: AbortSignal.timeout(30000) });
      if (!response.ok) throw Object.assign(new Error('Chat key lookup failed.'), { code: 'CHAT_KEYS_' + response.status });
      return Object.keys(await response.json() || {});
    },
    async page(first, second, cutoff, limit, after) {
      let query = messages.child(first).child(second).orderByChild('sentAt').endAt(cutoff).limitToFirst(limit);
      query = after ? query.startAfter(after.sentAt, after.id) : query.startAt(1);
      const snapshot = await query.get();
      const rows = [];
      snapshot.forEach(child => { rows.push({ id: child.key, value: child.val() }); });
      return rows;
    },
    remove: updates => messages.update(updates)
  };
}

async function sweepMessages(store, { now = Date.now, dryRun = false, pageSize = 250 } = {}) {
  const cutoff = now() - RETENTION_MS;
  let conversations = 0, deleted = 0;
  for (const first of await store.keys([])) {
    if (!validKey(first)) continue;
    for (const second of await store.keys([first])) {
      if (!validKey(second) || first === second) continue;
      conversations++;
      let after;
      while (true) {
        const page = await store.page(first, second, cutoff, pageSize, after);
        if (!page.length) break;
        const updates = {};
        for (const { id, value } of page) {
          if (validKey(id) && expired(value, cutoff)) updates[`${first}/${second}/${id}`] = null;
        }
        const count = Object.keys(updates).length;
        // Delete only individual expired message IDs, never a whole conversation:
        // a concurrent fresh send, friendship, profile or wallet cannot be lost.
        if (count && !dryRun) await store.remove(updates);
        deleted += count;
        const last = page.at(-1);
        after = { id: last.id, sentAt: last.value.sentAt };
        if (page.length < pageSize) break;
      }
    }
  }
  return { dryRun, cutoff, conversations, ...(dryRun ? { wouldDelete: deleted } : { deleted }) };
}

function attachChatCleanup(app, env, integrations = {}) {
  if (env.CHAT_CLEANUP_ENABLED !== 'true') return null;
  const minutes = Number(env.CHAT_CLEANUP_INTERVAL_MINUTES || 15);
  if (!Number.isInteger(minutes) || minutes < 1 || minutes > 1440) throw new Error('Chat cleanup interval must be 1–1440 minutes.');
  const now = integrations.now || Date.now;
  const status = { enabled: true, retentionHours: 24, intervalMinutes: minutes, running: false,
    lastSucceededAt: null, lastDeleted: 0, lastError: null };
  let store, timer;
  async function run(options = {}) {
    if (status.running) throw Object.assign(new Error('Chat cleanup is already running.'), { code: 'CHAT_CLEANUP_BUSY' });
    status.running = true;
    try {
      // Lazy initialization lets Render open its health-check port first and
      // allows cleanup with COMMERCE_ENABLED=false without enabling payments.
      store ||= (integrations.createStore || createFirebaseStore)(env);
      const result = await sweepMessages(store, { now, dryRun: options.dryRun === true });
      if (!result.dryRun) { status.lastSucceededAt = now(); status.lastDeleted = result.deleted; }
      status.lastError = null;
      return result;
    } catch (error) {
      status.lastError = error.code || 'CHAT_CLEANUP_FAILED';
      throw error;
    } finally { status.running = false; }
  }
  const scheduledRun = () => run().then(result => console.log('Social chat cleanup:', JSON.stringify(result)))
    .catch(error => { if (error.code !== 'CHAT_CLEANUP_BUSY') console.error('Social chat cleanup failed:', error.code || error.name); });
  const controller = {
    status, run,
    start() {
      if (timer) return;
      timer = setInterval(scheduledRun, minutes * 60000); timer.unref();
      void scheduledRun();
    },
    stop() { clearInterval(timer); timer = null; }
  };
  app.locals.chatCleanup = controller;
  // Optional external scheduler: a POST can wake a Render Free web service.
  // This secret is server-only and is never distributed in the Android app.
  app.post('/internal/chat-cleanup', async (req, res) => {
    const secret = env.CHAT_CLEANUP_TOKEN;
    if (!secret || secret.length < 32) return res.status(503).json({ code: 'CHAT_CLEANUP_TOKEN_REQUIRED' });
    const supplied = /^Bearer (.+)$/.exec(req.get('Authorization') || '')?.[1] || '';
    if (!crypto.timingSafeEqual(crypto.createHash('sha256').update(supplied).digest(), crypto.createHash('sha256').update(secret).digest()))
      return res.status(401).json({ code: 'UNAUTHORIZED' });
    try { return res.json(await run({ dryRun: req.query.dryRun === 'true' })); }
    catch (error) { return res.status(error.code === 'CHAT_CLEANUP_BUSY' ? 409 : 503).json({ code: error.code || 'CHAT_CLEANUP_FAILED' }); }
  });
  return controller;
}

module.exports = { RETENTION_MS, DEFAULT_INTERVAL_MS, expired, sweepMessages, attachChatCleanup, createFirebaseStore };
