# Backend implementation status and exact work request

Give this file to the backend developer with `TrucoPy-Backend-Contract-v1.zip`.

## Important status

The Unity client is implemented against the contract. This project has no backend repository, Swagger URL or staging service, so we cannot truthfully say which routes already exist on the server. The backend developer must mark each route as **implemented and tested**, **implemented with a different contract**, or **missing**.

“Implemented in Unity” means the client has the request, response validation, screen and error/retry behavior. It does not mean the server route exists.

## What the Unity client already calls

These are existing client integrations that must remain compatible:

| Area | Routes / keys | Required server result |
|---|---|---|
| Login/profile | `POST /auth/login`, `POST /auth/register`, `GET /auth/me`, `GET /auth/check-admin`, `POST /auth/refresh`, password/email verification routes | Bearer session, refresh cookies, explicit user ID and integer wallet balance. Never return a fake default balance. |
| Tournament list/admin list | `GET /tournaments`, `GET /admin/tournaments`, `POST /tournaments` | Canonical tournament settings: 4/8 players, Flor mode, public/private, target score 15/30, entry, prize and participants. |
| 1v1 lobby | `GET /matches`, `POST /matches/player-create`, `POST /matches/{id}/join`, `GET /matches/{id}`, `POST /matches/{id}/photon-room` | Preserve exact room settings, real match ID, exact two participant IDs and stable Photon room name. Debit only once. |
| 1v1 gameplay | `POST /matches/{id}/start-game`, `POST /matches/{id}/match-token`, `POST /matches/{id}/result`, `POST /matches/{id}/walkover`, `POST /matches/{id}/leave`, `POST /matches/{id}/end` | Server validates participant, legal result, token, presence/deadline and settlement. Client winner fields are claims, not authority. |
| 1v1 unused-entry refund | `POST /matches/{id}/cancel-lobby-refund`, body `{}` | Atomic full refund receipt with matching `matchId`, `refundId`, amount and wallet state. A generic success or missing room is not proof. |
| Admin/dashboard reads | `GET /admin/matches`, dashboard active matches/players/tournaments/reconnect/fraud routes | Real server data, role checks, safe errors. No sample data in production. |
| Maintenance | `GET /system/status`, `POST /admin/system/maintenance` | Canonical state under `data.maintenance`; server must enforce blocks for every account. |

The client still contains older compatibility methods such as `CreateTournamentMatch`, old tournament finalize helpers and unused alert/profile aliases. They are listed in `ENDPOINTS.md` as reserved or legacy. Do not build new production logic around them unless the backend team confirms they are intentionally retained.

## New routes the backend must implement first

These are the release blockers introduced by the contract:

| Priority | Route | Purpose |
|---|---|---|
| P0 | `GET /tournaments/{id}` | Versioned authoritative tournament snapshot before the client joins. |
| P0 | `POST /tournaments/{id}/join` | Register/debit once; return registration and snapshot. |
| P0 | `POST /tournaments/{id}/start` | When full, freeze positions and create every opening match atomically. |
| P0 | `POST /tournaments/{id}/leave` | Server decides refund/abandonment; idempotent. |
| P0 | `POST /tournaments/{id}/reconcile` | Server decides reconnect, walkover or no-winner cancellation. |
| P0 | `POST /tournaments/{id}/finalize-match` | Verify result, advance fixed bracket seat once, create next match when qualified. |
| P0 | `POST /tournaments/{id}/finalize` | Final champion only; return actual payout receipt and wallet snapshot. |
| P0 | `POST /auth/photon-session` | Issue short-lived Photon CustomAuth ticket; never debit or join. |
| P0 | Photon CustomAuth + `PathCreate` + `PathBeforeJoin` | Reject forged identity or wrong-room players before Photon creates an actor. |
| P0 | `GET /wallet/snapshot` | Available/held balance, limits, presets and complete request history. |
| P0 | `POST /wallet/requests` | Buy/sell request with server validation and idempotency. |
| P0 | `GET /wallet/requests/{id}` | Owner status/history and canonical request data. |
| P0 | `GET/POST /admin/wallet/requests...` | Admin list/detail/approve/reject/pay with role checks and applied receipts. |
| P0 | `GET /admin/audit` | Durable server audit feed, newest first, paged by opaque cursor. |
| P1 | `POST /admin/tournaments/{id}/champion` | Exceptional admin champion command, separate from player finalization. |
| P1 | `POST /admin/tournaments/{id}/force-close` | Close tournament and confirm all required refunds. |
| P1 | `POST /admin/users/{id}/coins` | Exceptional credit with matching ledger transaction receipt. |

## Rules every POST must follow

1. The bearer token determines the player/admin identity. Never trust a submitted user ID, role, wallet balance, winner, champion or Photon custom property.
2. Every financial or tournament mutation is atomic and idempotent. Store `idempotencyKey`, canonical request hash, actor and full result. Same key and same request returns the same receipt; same key with different data returns HTTP 409.
3. Validate the expected revision, but check an existing idempotency result before rejecting an old revision. A timeout is an unknown result; never perform a second debit or credit with a new key automatically.
4. Use integer Trucoins only. Keep available and held balances separate. Refunds and payouts must have durable IDs and exact amounts.
5. Return UTC ISO8601 timestamps and safe errors: `{success:false,code,error,message,requestId}`. Do not return stack traces, credentials, cookies, Photon tickets or banking data in logs/errors.
6. Return success only after the database transaction and audit record commit. `success:true` or HTTP 200 alone is not a financial receipt.
7. Enforce permissions and maintenance on the server, including when the app is offline or closed. Cleanup deadlines and refunds need a server worker.

## Required response rules

For tournament routes use the exact envelope in `TOURNAMENT_AND_PHOTON_V1.md`:

```json
{"success":true,"data":{"schemaVersion":1,"idempotencyKey":"same-key","snapshot":{}}}
```

For wallet/admin routes use the exact records and applied operation receipts in `WALLET_AND_ADMIN_V1.md`. For Photon use the exact ticket fields in `TOURNAMENT_AND_PHOTON_V1.md`; the token is memory-only and must never appear in runtime configuration or room properties.

## What the backend developer must send back

Please return:

1. A deployed staging base URL and the public `GET /client/config` JSON.
2. Swagger/OpenAPI for every route, including request and response examples.
3. Test accounts for player, admin, 4-player tournament and 8-player tournament scenarios. Do not send passwords in this project chat; use your secure channel.
4. Photon AppId/environment details, CustomAuth provider status and confirmation that `PathBeforeJoin` is enabled.
5. A test report proving duplicate requests, lost responses, two-device login, disconnects, full refunds, payouts, wallet holds and admin authorization.
6. A list of any route or field that differs from this contract. Do not silently change names or response shapes.

After receiving this, Unity only needs the staging URL/configuration values for compatible route changes. New behavior, new HTTP methods or changed schemas require a client build.
