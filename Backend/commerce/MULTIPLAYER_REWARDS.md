# Open Archive multiplayer rewards

The multiplayer build uses the existing authenticated commerce service and the
existing `users/<uid>/coins` and `gems` wallet. It does not invoke solo trial
endpoints, alter solo retries, change shop prices, or create a second balance.

Deploy this version of **Backend/commerce** together with the matching Unity
build. Keep the existing CommerceSettings backend URL and Firebase service
identity. Repository edits do not redeploy the live Render service. Old servers
return 404 for the new endpoint; the client retains its pending report for retry
instead of inventing a local wallet credit.

## Request and receipt

`POST /multiplayer/complete` requires the player's Firebase ID token in the
existing Bearer authorization header. The authenticated UID selects the wallet;
request-body UIDs, coin totals and gem totals never select an account or amount.
Disabled accounts cannot claim. The JSON body is:

```json
{
  "version": 1,
  "matchId": "32lowercasehexcharactersfrommatch",
  "actor": 1,
  "online": true,
  "adminUsed": false,
  "outcome": "victory",
  "participants": [1, 2],
  "completedTopics": [0, 1, 2, 3, 4, 5, 6, 7, 8, 9],
  "restoredTopics": [0, 1],
  "participationKeys": ["work:0:0:0", "work:1:0:0", "support:revive:2"]
}
```

The match ID is the unchanged host-authored GUID in N format. The example above
uses a descriptive placeholder rather than a valid match ID. An online roster
contains two to four distinct positive actor IDs, including the claiming actor.
Topic IDs are zero-based, unique and between zero and nine. Restored topics must
be a subset of completed topics. An eligible online victory requires all ten
completed topics; excluded admin/offline previews may have skipped those
receipts and still record their zero-value exclusion. Defeat is the only other
accepted terminal outcome.

Only canonical accepted-work keys and teammate-support keys count. Work keys are
`work:TOPIC:ROUND:SUBPHASE`, using the actual topic's round count and subphase
(Rules of Inference permits subphases zero and one; other topics permit zero).
Support keys are `support:medkit:TARGET`, `support:battery:TARGET`,
`support:revive:TARGET`, or `support:give:TARGET`, where TARGET is another roster
member. Duplicate keys count once; the report is limited to 128 keys. Movement,
button presses, wrong answers, invalid/self support and arbitrary strings do not
create credit. The shipped game constructs these keys from host-accepted work and
successful support, rather than UI click counters.

Let `tier = min(3, unique valid participation keys)`. A victory pays:

- Team victory: `floor(600 * tier / 3)` coins.
- Restored crystals: `floor(25 * restoredTopicCount * tier / 3)` coins.
- Participation: `50 * tier` coins.

Thus three meaningful contributions and ten fully restored crystals earn the
1,000-coin maximum. Having no meaningful contribution earns zero. Being downed
does not delete earlier accepted contributions. Defeat pays only
`floor(min(150, 15 * completedTopicCount) * tier / 3)` coins. Admin-assisted and
offline reports persist zero-value excluded receipts and earn no milestone gems.

Full-tier eligible players receive one gem for their first genuine multiplayer
victory, and one further gem for their first victory with all ten restored
crystals. These are independent, once-per-account milestones, not recurring
match rewards. Both may be earned in one match; subsequent matches give no
additional gems for those milestones.

The response contains `matchId`, `status` (`completed`), `coins`, `gems`, `reward`,
`gemReward`, `baseCoins`, `crystalCoins`, `participationCoins`, `consolationCoins`,
`tier`, and `exclusionReason`. The two balance fields are the current wallet;
the reward fields are this receipt's immutable deltas. Exclusion reasons are
`admin-assisted`, `offline-preview`, or `no-participation`; eligible receipts use
an empty string.

The account transaction stores `multiplayerRewards/<matchId>` together with the
coin and gem increments and any `multiplayerMilestones` markers. A duplicate,
concurrent, retried, or edited report for the same authenticated UID and match ID
returns the original receipt without another payout. The same match ID can
legitimately award each teammate's own authenticated account. Gems, inventory,
equipment, solo trial ledgers and progression remain intact. Existing default
database rules deny direct client writes to the new receipt/milestone fields;
do not add a broad user-parent write rule.

## Trust and verification boundary

Gameplay remains Photon Master Client authoritative. This endpoint validates
the terminal report's schema, canonical evidence, roster relationships, reward
bounds, account identity, milestone uniqueness and wallet transaction. It does
not independently run the Unity puzzles or prove that an actor belonged to a
particular Photon room. A modified host/client can still fabricate a consistent
report; this is the same reported-outcome boundary documented for solo trials.
Existing legacy coin-write permissions are also unchanged. This integration is
not a claim of dedicated-server anti-cheat. A trusted Photon/server result
attestation would be a separate deployment and gameplay-authority project.

Run `node --test multiplayer.test.js` for pure policy and replay checks. With the
local database emulator on `127.0.0.1:9000`, set `RUN_EMULATOR_TESTS=1` and run
`node --test multiplayer.http.test.js` for authenticated HTTP, concurrent account
transactions, isolated teammate wallets, repeat milestones, invalid reports and
disabled-account checks. Tests use generated fixture accounts and mocked token
verification, never production credentials or real player balances.
