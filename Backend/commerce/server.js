function startServer(env = process.env) {
  const port = Number(env.PORT || 18080);
  if (!Number.isInteger(port) || port < 0 || port > 65535) throw new Error('PORT must be an integer from 0 to 65535.');
  // Render health checks reach the container through its network interface.
  // Keep local emulator use loopback-only, but always bind hosted services publicly.
  const host = env.RENDER === 'true' || env.STORAGE_MODE !== 'emulator' ? '0.0.0.0' : '127.0.0.1';
  const setup = env.COMMERCE_ENABLED === 'false';
  console.log(`Commerce starting; host=${host}, port=${port}, setup=${setup}`);
  const { createApp } = require('./app');
  const app = createApp(env);
  console.log('Commerce routes ready; opening HTTP listener');
  const server = app.listen(port, host, () => console.log(setup
    ? `Commerce setup service listening on ${host}:${server.address().port}; payments disabled`
    : `Commerce listening on ${host}:${server.address().port}; storage=${env.STORAGE_MODE}, payments=${env.PAYMENT_MODE}`));
  server.on('error', error => {
    console.error(`Commerce listener failed: ${error.code || 'UNKNOWN'}`);
    process.exitCode = 1;
  });
  return server;
}

if (require.main === module) startServer();
module.exports = { startServer };
