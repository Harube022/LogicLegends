// Kept separate so wallet and economy policy can share validation without a require cycle.
const fail = (message, status = 400) => Object.assign(new Error(message), { status });
module.exports = { fail };
