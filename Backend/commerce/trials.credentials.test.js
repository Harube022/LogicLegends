const { test } = require('node:test');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');

test('hosted Auth authorization and Database transactions share the configured private service identity', async () => {
  const { createApp } = require('./app');
  const { getApp, deleteApp } = require('firebase-admin/app');
  const privateKey = crypto.generateKeyPairSync('rsa', { modulusLength: 2048 }).privateKey.export({ type: 'pkcs8', format: 'pem' });
  createApp({ STORAGE_MODE: 'firebase', PAYMENT_MODE: 'live', LIVE_WALLET_READY: 'true',
    FIREBASE_PROJECT_ID: 'demo-trial-credentials', FIREBASE_DATABASE_URL: 'https://demo-trial-credentials.firebaseio.com',
    FIREBASE_SERVICE_ACCOUNT_JSON: JSON.stringify({ project_id: 'demo-trial-credentials', client_email: 'fixture@demo-trial-credentials.iam.gserviceaccount.com', private_key: privateKey }) });
  try {
    assert.ok(getApp('commerce-auth').options.credential);
    assert.equal(getApp('commerce-auth').options.credential, getApp('commerce-data').options.credential);
  } finally { await deleteApp(getApp('commerce-auth')); await deleteApp(getApp('commerce-data')); }
});
