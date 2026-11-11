// Run against a fresh export of the shared database rules, not a local template.
const fs = require('node:fs');
const path = require('node:path');
const { NCST } = require('./game-access');

const guard = "auth != null && auth.token.email_verified === true && (auth.token.admin === true || auth.token.super_admin === true || (auth.token.email != null && auth.token.email.matches(" + NCST + "))) && root.child('website/moderation').child(auth.uid).child('ban/active').val() !== true && (!root.child('website/moderation').child(auth.uid).child('sessionsRevokedAt').exists() || auth.token.auth_time > root.child('website/moderation').child(auth.uid).child('sessionsRevokedAt').val())";

function patchGameRules(document) {
  const rules = document.rules;
  if (!rules?.users || !rules?.social) throw new Error('Expected the shared users and social rules.');
  const closed = value => value == null || value === false || value === 'false';
  if (!closed(rules['.read']) || !closed(rules['.write']))
    throw new Error('A database-wide grant must be reviewed first.');
  // A dynamic top-level rule could also grant access to these protected paths.
  if (Object.keys(rules).some(key => key.startsWith('$')))
    throw new Error('A dynamic database-wide rule must be reviewed first.');
  function walk(node, handle) {
    for (const [key, value] of Object.entries(node)) {
      if (key === '.read' || key === '.write') handle(node, key, value);
      else if (!key.startsWith('.') && value && typeof value === 'object') walk(value, handle);
    }
  }
  for (const branch of ['users', 'social']) walk(rules[branch], (node, key, value) => {
    if (!closed(value) && (typeof value !== 'string' || !value.startsWith('(' + guard + ') && ')))
      node[key] = '(' + guard + ') && (' + value + ')';
  });
  const website = rules.website ||= {};
  walk(website, (_node, _key, value) => {
    if (!closed(value)) throw new Error('Review existing website client grants before applying this migration.');
  });
  website['.read'] = false;
  website['.write'] = false;
  return document;
}

if (require.main === module) {
  const [input, output] = process.argv.slice(2);
  if (!input || !output || path.resolve(input) === path.resolve(output))
    throw new Error('Usage: node patch-game-rules.js exported.rules.json reviewed.rules.json');
  const document = patchGameRules(JSON.parse(fs.readFileSync(input, 'utf8')));
  fs.writeFileSync(output, JSON.stringify(document, null, 2) + '\n', { flag: 'wx' });
}
module.exports = { patchGameRules };
