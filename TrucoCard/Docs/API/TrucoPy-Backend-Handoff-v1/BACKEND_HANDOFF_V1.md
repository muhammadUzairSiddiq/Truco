# TrucoPy backend handoff — client contract v1

This is the **proposed contract requested by the project owner**, implemented by the Unity client. It does not claim these services are deployed. The supplied 17-page Unity specification is the feature checklist; the owner's later request expressly authorizes defining the missing APIs. Backend deployment and real settlement acceptance are still required before production release.

Give the backend developer these files together:

- [OpenAPI 3.0.3 contract](trucopy-v1.openapi.json): request/response schemas and logical Unity keys.
- [Complete endpoint table](ENDPOINTS.md) and [machine-readable catalog](endpoint-catalog.json).
- [Wallet and admin semantics](WALLET_AND_ADMIN_V1.md).
- [Tournament and Photon semantics](TOURNAMENT_AND_PHOTON_V1.md).
- [Public runtime configuration example](client-config.example.json).

The base URL already includes `/api`; the paths in the contract are relative to it. `{id}` in OpenAPI is `{0}` in the Unity SO. IDs are URL-escaped. Names and JSON fields are case-sensitive. Existing list/auth/1v1 DTOs are included as `Legacy*` schemas from `ApiModels.cs`; the new versioned tournament/wallet/admin/transport schemas take precedence for their routes. Reserved legacy catalog entries do not imply a current UI consumer. In particular, client-created tournament matches and legacy tournament finalize payloads are not a production fallback.

## Configuration and updates without rebuilding

The central asset is **`Assets/Resources/TrucoApiEndpoints.asset`**, type **`TrucoApiEndpointsSO`**. Its searchable inspector exposes base URL, fixed public bootstrap URL, trusted additional HTTPS origins, configuration revision, and every endpoint key/method/path. The export button writes only the public versioned configuration JSON.

The normal production Init scene fetches the fixed bootstrap before showing login. Current default: `GET https://srv983121.hstgr.cloud/api/client/config`. Host the supplied example at that URL. It is public and must contain no bearer tokens, cookies, Photon tickets, private keys, banking credentials or server secrets. Return HTTP 200 JSON directly; redirects are disabled. Use an appropriate short cache lifetime and increment `data.revision` whenever publishing a change.

The loader accepts schema version 1 and a compatible `minimumClientContractVersion` (currently 1), preserves the last valid cache/bundled routes during network failures, and rejects revision rollback, duplicate/unsafe keys, untrusted origins, HTTP method changes and changed ID-placeholder requirements. Only HTTPS origins bundled in the SO are trusted. Same-origin path/base-prefix changes and pre-authorized origin changes can be delivered after release without rebuilding. A new untrusted hostname, HTTP method, JSON protocol or new client behavior requires a compatible build. Adding a catalog row alone cannot implement new application behavior.

**Before the first production build:** set the actual base/bootstrap URL and any planned alternate trusted API origins in the SO. **After release:** edit the SO, increment revision, export JSON and ask the backend developer to serve it at the existing fixed bootstrap URL. Editing an SO in the Unity project alone does not change an installed APK. Keep the original bootstrap reachable for installed clients. Direct Editor entry into Login/MainMenu uses bundled/cached routes; start from Init to test a fresh bootstrap fetch.

## Shared server requirements

- All authenticated operations derive the account and role from the bearer session. Ignore client-supplied role, balance, winner authority and mutable Photon custom `userId` properties.
- All Trucoin amounts are whole integers within the client range `0..2147483647`; purchase/sale validation also enforces the current configured minimum/maximum. No floating-point ledger arithmetic. Available and held balances are separate server values.
- Commit balance changes, holds, releases, status changes, operation receipts and audit records in one database transaction. Enforce unique ledger effects for each entry, purchase credit, sale payout, refund and tournament prize.
- Idempotent mutation requests carry `idempotencyKey` in the JSON body. Check the stored result for an existing key **before** rejecting an old revision. Same key/same command returns the original committed receipt; same key/different command returns 409. A timeout is an unknown outcome, not failure or permission to repeat a debit under a new key.
- Enforce one active paid commitment per account across devices. Client disabling of buttons is not protection against two phones, reconnects or concurrent requests. Existing 1v1 creation has no new body idempotency field: use the authenticated account's active-entry constraint and recoverable match row to prevent a second charge after a lost response.
- Use UTC ISO8601 timestamps; preserve exact integer revisions. Reject malformed, partial and contradictory success payloads. Return non-2xx errors with safe `code`, Spanish `message`, and `requestId`; do not echo credentials, tickets, bank details or private payloads in errors/logs.
- Maintenance enforcement belongs in backend mutation/entry handlers as well as client UI. Check status before debiting. Cancellation/refund/reconciliation and authenticated history must remain available during maintenance so existing obligations can be resolved.
- Reconnect deadlines, expired registration cleanup, disconnect walkovers and full cancellation refunds must run server-side even when every app is closed. A disconnected client cannot guarantee a cleanup request.

Recommended error classes: 400 validation, 401 expired/invalid session, 403 role/ownership denied, 404 missing resource, 409 stale revision/conflicting commitment/idempotency mismatch, 422 invalid game transition, 429 rate limit, 503 maintenance/unavailable. The client keeps unknown financial outcomes pending and refreshes canonical server state.

## Authentication, profile and public maintenance

Existing login uses `POST /auth/login` with `{email,password}` and returns a nonempty root `accessToken` (legacy `token` is accepted). Follow with `GET /auth/me`:

```json
{"success":true,"user":{"_id":"player-id","username":"Jugador","role":"player","wallet":{"balance":176}}}
```

The wallet balance must be explicitly present, integer and nonnegative; missing balance no longer creates a default. Profile `id` is accepted as a legacy alternative to `_id`. Passwords are not persisted in PlayerPrefs. Login must issue a `refresh_token` cookie if automatic renewal is required; refresh uses that cookie and returns a renewed `access_token` cookie (plus a rotated refresh cookie when appropriate). Partial cookie updates merge, concurrent renewals share one operation, and logs omit tokens, cookies and response bodies. Logout clears manual and native Unity cookies plus in-memory HTTP/Photon/tournament state; pending per-account recovery IDs remain for authenticated reconciliation. Native cookie cleanup follows the [Unity 6 API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Networking.UnityWebRequest.ClearCookieCache.html).

Participant lists need only public `_id`, `username` and optional avatar. Do not expose another player's email, wallet or banking data merely because the legacy Unity DTO has those optional fields. Wallet requests and receipt URLs are visible only to their authenticated owner or an authorized admin.

`GET /auth/check-admin` returns `{ok:true,role:"admin"}` only for a server-authorized admin. Every protected route must independently enforce its permission. Knowing a special email or showing an admin button grants no authority.

`GET /system/status` is public so maintenance can be checked before login. `POST /admin/system/maintenance` is admin-only and accepts the `active`, `blockGame`, `blockTables`, `blockTournaments`, `blockTransactions` flags and Spanish `message`. Server-generated `updatedAt`/`updatedBy` override any submitted values. Both return the complete canonical state:

```json
{"success":true,"data":{"maintenance":{"active":false,"blockGame":true,"blockTables":true,"blockTournaments":true,"blockTransactions":true,"message":"El juego está en mantenimiento. Volvé más tarde.","updatedAt":"2026-10-31T12:00:00Z","updatedBy":"admin-id"}}}
```

Unity does not apply an admin's edited state until this canonical response arrives. Server enforcement must cover all players; a local cached flag is only UI recovery.

## 1v1 compatibility and refund contract

Keep the exact selected room name, `type`, password policy, `withFlor`, `targetScore` (15/30), entry/cost and prize in the server record. List/join/create responses must return canonical settings, real match ID and exactly assigned player IDs. Return stable `photonRoomName`; accept its registration through `/matches/{id}/photon-room` only from its authorized host and validate the expected room name. A player does not gain room permission by submitting an arbitrary name.

`GET /matches/{id}` must expose `match.players` with the two authenticated API IDs before the client starts gameplay; Photon published `Player.UserId` must match that pair. A real paid create/join now requires an authenticated Photon Master connection before the fee request, followed by a room-scoped ticket once the server assignment exists. A later transport failure remains recoverable server state, never a local refund.

For an unstarted paid room, `POST /matches/{id}/cancel-lobby-refund` with `{}` atomically closes the unused entry and returns:

```json
{"success":true,"data":{"matchId":"match-id","refunded":true,"refundId":"ledger-refund-id","amount":50,"walletBalance":176}}
```

Repeat calls for the same authenticated account/entry return the same effect, including after the active lobby row was cleaned up. Keep a settlement tombstone. A generic `{success:true}`, absent match ID, different match ID, removed lobby row, or unrelated balance increase is not proof of this refund. `alreadyRefunded:true` with the same match ID is accepted for an already committed effect. Pending debt is retained until an operation-bound confirmation arrives.

`POST /matches/{id}/leave` and `/end` are lifecycle cleanup, not unconditional financial proof. `/start-game` must only mark a valid assigned two-player match started. `/match-token` issues a short-lived token bound to the authenticated participant/match. `/result` and `/walkover` use this token, a replay nonce and millisecond timestamp; preserve the current request fields/headers shown in OpenAPI. The legacy `x-game-secret` shipped with a client is a compatibility identifier, **not a private server credential or sufficient payout authority**. Validate result legality using authoritative evidence/opponent agreement, match state and authenticated presence; never settle solely because one client submitted `winnerId` or says a rival timed out.

Admin `POST /matches/{id}/force-close` with `{}` is a naturally idempotent audited cancellation. Return matching `{success:true,data:{matchId,status:"cancelled",refundsApplied:true}}` only after all required returns are committed. `closed` is also accepted. The Unity admin list no longer treats a bare HTTP 200 as confirmation.

## Tournament and wallet activation

Implement the complete versioned schemas in the linked tournament and wallet notes. The tournament controller checks the versioned GET snapshot before paid Join. Backend creates/seeds all opening matches atomically, preserves bracket positions, publishes exact room assignments, finalizes once and computes actual payouts/refunds. No local Photon actor order or local AI result can advance the production bracket or alter a wallet.

The local Editor tournament uses the same visible navigation theme but a separate deterministic practice engine. It is intentionally a development fixture. It does not test a deployed ledger, backend authorization, Cloud CustomAuth, Android lifecycle or bank transfers.

Exceptional admin champion selection is **`POST /admin/tournaments/{id}/champion`** (`AdminSetChampion`), separate from normal server-earned tournament finalization. Exceptional champion, tournament force-close and coin-credit actions now require the exact subject-bound applied operation receipts in the admin notes. A completed tournament prize must have a unique ledger effect even if an admin retries under another request key. General audit is `GET /admin/audit`; the UI no longer presents local button logs as authoritative server audit.

## Photon deployment requirements

Implement `POST /auth/photon-session` exactly as described in the transport notes. It issues identity/context tickets without charging or joining. Pre-fee null scope is valid; exact known rooms require an exact allowlist. Configure the Photon CustomAuth provider, disable anonymous production fallback, and return the account's real immutable `UserId`. Use `PathCreate` and **`PathBeforeJoin`** authorization for server-assigned rooms and admin spectator roles. `PathJoin` notification occurs too late to be the admission gate. If the existing dashboard lacks `PathBeforeJoin`, obtain support for it or an equivalent server plugin before release.

The client supports this now; the backend/Photon setup is still external work. See the [Photon CustomAuth reference](https://doc.photonengine.com/pun/current/connection-and-authentication/authentication/custom-authentication) and [room admission webhook reference](https://doc.photonengine.com/realtime/v5/gameplay/web-extensions/webhooks). Webhook/provider credentials are configured only on servers/Photon, never in public runtime JSON or the APK. Synthetic multiplayer QA requires a separate staging Photon AppId.

## Backend acceptance before production

Run against the actual deployed contract, with real test accounts and server ledger assertions:

1. Contract bootstrap: valid update, offline cache, malformed/rollback/untrusted origin, unavailable endpoint, and intentional minimum-client-version upgrade.
2. 1v1 create/join/delete for all settings; two phones on the same account; duplicate list events; timeout after debit; app kill; exactly one unused-entry refund.
3. Full 4- and 8-player simultaneous opening tables, real complete matches, early winner waiting, automatic next rounds, final/champion, actual paid prize and repeated finalization.
4. One disconnect/rejoin/walkover, both disconnected cancellation with no winner and **100% of each entry refunded with no commission**, app background/reopen, eliminated-player navigation, and expired registration cleanup when all apps are closed.
5. Purchase transfer verification through requested/receipt/verification/credited; sale hold/approve/pay/finalize; rejection releases the original hold once. Replay every financial action, lose the first response, refresh from another device and assert the same ledger result.
6. Unauthorized admin actions rejected server-side; authorized maintenance affects another account; exceptional credit/champion/force-close have matching applied receipts and durable audit. Test lost response, restart, concurrent retry and stale revision.
7. Deployed Photon CustomAuth rejects forged identity and expired/wrong-room tickets; `PathBeforeJoin` rejects unassigned accounts; spectator access requires the real admin role. Test reconnect and host migration with immutable IDs.
8. Build the signed Android artifact, record artifact hash/version/signing identity, and run startup, input, safe areas, TLS/network, foreground/background and reconnect on the supported device matrix. AI-only Editor tests do not establish Android release acceptance.

This handoff deliberately contains no real credentials, transfer accounts, phone numbers or live payment instructions.
