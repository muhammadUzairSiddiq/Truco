# TrucoPy backend handoff

Hi. This package contains the API contract needed by the Unity game client.

The Unity side is already wired to these route names and response shapes. Please implement the server routes with the same names and fields. If a route already exists with a different name or response, tell us the difference before changing it.

## Start here

1. Open `BACKEND_IMPLEMENTATION_STATUS.md`.
2. Open `trucopy-v1.openapi.json` in Swagger Editor or Postman.
3. Read `WALLET_AND_ADMIN_V1.md` for Trucoin purchases, sales, admin approvals and refunds.
4. Read `TOURNAMENT_AND_PHOTON_V1.md` for tournaments, brackets, reconnection and Photon login.
5. Use `client-config.example.json` as the response for `GET /client/config`.

## First routes to finish

Finish these before testing the Android build:

- `GET /auth/me`
- `GET /tournaments/{id}`
- `POST /tournaments/{id}/join`
- `POST /tournaments/{id}/start`
- `POST /tournaments/{id}/leave`
- `POST /tournaments/{id}/reconcile`
- `POST /tournaments/{id}/finalize-match`
- `POST /tournaments/{id}/finalize`
- `POST /auth/photon-session`
- `GET /wallet/snapshot`
- `POST /wallet/requests`
- `GET /wallet/requests/{id}`
- Admin wallet list/detail/approve/reject/pay routes
- `GET /admin/audit`

## Rules that must be followed

- The logged-in account comes from the Bearer token. Do not trust a user ID sent by the app.
- Money values are whole-number Trucoins. Keep available and held balances separately.
- Every money or tournament write must be atomic and idempotent.
- Store each `idempotencyKey`. Repeating the same request must return the original result, never charge twice.
- A timeout is an unknown result. Do not create a second transaction with a new key until the first operation is checked.
- Return a success receipt only after the database transaction and audit record are committed.
- Use UTC ISO timestamps.
- Do not put tokens, cookies, bank details or stack traces in logs or error responses.
- Enforce admin permissions and maintenance on the server, even if the Unity screen is bypassed.

## Required handback

Please send back:

- Staging API URL
- `GET /client/config` response
- Swagger/OpenAPI for the deployed routes
- Test accounts through a secure channel
- Photon AppId and CustomAuth status
- Confirmation that Photon `PathBeforeJoin` is enabled
- Test results for duplicate requests, lost responses, two devices, disconnects, refunds, payouts and unauthorized admin actions

The client configuration asset is included at `Unity/TrucoApiEndpoints.asset`. It shows the exact logical route keys used by Unity.

Compatible path changes can be supplied later through `GET /client/config`. New behavior, new HTTP methods or changed JSON contracts require a new Unity build.
