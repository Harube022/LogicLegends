const { createApp } = require('./app');
const port = Number(process.env.PORT || 18080);
const host = process.env.STORAGE_MODE === 'emulator' ? '127.0.0.1' : '0.0.0.0';
createApp().listen(port, host, () => console.log(process.env.COMMERCE_ENABLED === 'false'
  ? `Commerce setup service listening on ${port}; payments disabled`
  : `Commerce listening on ${port}; storage=${process.env.STORAGE_MODE}, payments=${process.env.PAYMENT_MODE}`));
