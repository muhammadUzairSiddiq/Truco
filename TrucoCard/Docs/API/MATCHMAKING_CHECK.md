# TrucoPy matchmaking check

Base URL `https://srv983121.hstgr.cloud/api`. The paths below are relative to this base. [OpenAPI contract](trucopy-v1.openapi.json) · [Tournament and Photon details](TOURNAMENT_AND_PHOTON_V1.md)

## Headers Unity sends

| Request | Headers |
| --- | --- |
| Authenticated API requests | `Authorization: Bearer <accessToken>`. Unity also sends `x-auth-token: <accessToken>` for compatibility. |
| JSON POST requests | `Content-Type: application/json`, `x-nonce: <fresh UUID in 32 hex characters>`, `x-timestamp: <Unix time in milliseconds UTC>`. |
| Requests with a session cookie or CSRF token | Unity also forwards `Cookie` and `X-XSRF-TOKEN` when available. |
| 1v1 `/result` and `/walkover` | `x-match-token` from `/match-token` and the agreed legacy `x-game-secret` header, in addition to the above. The server still validates caller and result. |

`POST /auth/login` is exempt from replay validation. `GET /system/status` and `GET /client/config` are public. A retried write sends a fresh nonce. Tournament retries retain the original `idempotencyKey` and body.

## 1v1 flow

| Step | API |
| --- | --- |
| List and read | `GET /matches`, `GET /matches/{matchId}`, `GET /matches/player/my-matches` |
| Check multiplayer first | `POST /auth/photon-session` with `mode: "one_vs_one"` and null `tournamentId`, `matchId`, `roomName`. Connect to Photon Master before creating or joining a paid room. |
| Host creates | `POST /matches/player-create` with `name`, `type` (`public` or `private`), `cost`, `prize`, `maxPlayers` (`2`), `password`, `withFlor`, `targetScore` (`15` or `30`). |
| Register room name | `POST /matches/{matchId}/photon-room` with `photonRoomName` and `roomName` set to the same exact name. |
| Guest joins | `POST /matches/{matchId}/join` with `{}` for a public room or `{ "password": "…" }` for a private room. |
| Open assigned room | Both players obtain a new `POST /auth/photon-session` ticket with the same `matchId` and exact `roomName`. Its `allowedRooms` includes that room. |
| Start and get result token | `POST /matches/{matchId}/start-game`, `POST /matches/{matchId}/match-token` |
| Settle and close | `POST /matches/{matchId}/result`, `POST /matches/{matchId}/walkover`, `POST /matches/{matchId}/leave`, `POST /matches/{matchId}/end`, `POST /matches/{matchId}/cancel-lobby-refund` |

Return the real `_id` and canonical `photonRoomName` on create, join and detail. Before gameplay, `GET /matches/{matchId}` must identify both assigned API accounts in `players`. Their published Photon `UserId` values must match. For an already settled unused lobby refund, repeat `/cancel-lobby-refund` returns the receipt for the same match and account. The recent Editor run saw HTTP `409` from that endpoint for two older rooms.

## Tournament flow

| Step | API |
| --- | --- |
| Create and list | `POST /tournaments`, `GET /tournaments` |
| Read before charging | `GET /tournaments/{tournamentId}`. This is where `Prod Smoke 701` failed validation. |
| Private tournament | `POST /tournaments/{tournamentId}/validate-password` with `{ "password": "…" }`. Unity reads `valid` at the top level. The server must enforce the validated access for the same account and tournament at join. |
| Check multiplayer first | `POST /auth/photon-session` with `mode: "tournament"`, `tournamentId`, and null `matchId` and `roomName`. |
| Register and start | `POST /tournaments/{tournamentId}/join`, `POST /tournaments/{tournamentId}/start`. Writes send `idempotencyKey` and `expectedRevision` in the JSON body. |
| Open assigned table | Read the authenticated player's `localAssignment`. Request a tournament ticket with that exact `tournamentId`, `matchId` and `roomName`. Both players use the same room. |
| Advance and settle | `POST /tournaments/{tournamentId}/finalize-match`, `POST /tournaments/{tournamentId}/finalize`, `POST /tournaments/{tournamentId}/reconcile`, `POST /tournaments/{tournamentId}/leave` |

Successful detail and write responses use `{ "success": true, "data": { "schemaVersion": 1, "snapshot": { … } } }`. Writes also return `data.idempotencyKey`. The snapshot includes the tournament ID, revision, status, local status, rules, participants, matches, refunds, payout, and the caller's wallet with integer `available`, `held`, and `revision`. A ready assignment must match its `matches[]` row and contain the two assigned API IDs. The v1 room name is `TMatch_{tournamentId}_R{round}_M{index}` using zero-based round and index. Send one redacted actual response for `GET /tournaments/6aee08b3938a1dd70caaf9e0` so we can compare it field by field.

## Photon setup

The app's ticket issuer is `POST /auth/photon-session`. It returns `{success:true,data:{schemaVersion:1,ticket:{provider,userId,mode,tournamentId,matchId,authToken,expiresAt,appVersion,ticketId,allowedRooms}}}`. The separate Custom Authentication provider configured in Photon validates the ticket. Unity sends it `{token,mode,tournamentId,matchId}`. Return `ResultCode: 1` and the API account ID as `UserId` after successful validation. The 1v1 Editor log shows `Authentication type not supported (none configured)` on the bundled Photon Realtime App ID starting `7667f450`, region `sa`. Enable and test the Custom Authentication provider for that exact app. Keep room creation and pre-join authorization in place for paid tables. [Photon Custom Authentication setup](https://doc.photonengine.com/pun/current/connection-and-authentication/authentication/custom-authentication)

Please run one real 1v1 and one four-player tournament with test accounts and test Trucoins. Confirm both assigned players enter gameplay in each room. If a step fails, send its path, HTTP status, redacted response body and Photon error code if present.
