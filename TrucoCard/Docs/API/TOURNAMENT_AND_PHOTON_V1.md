# Authoritative tournament v1 - implementation notes

The normative DTO/path fragment is trucopy-v1.openapi.json. The Unity implementation is TrucoTournamentService.cs and MultiplayerController.cs. No external server implementation or deployment is claimed.

## Common response and activation

Every GET/POST succeeds only with HTTP200 and `{success:true,data:{schemaVersion:1,idempotencyKey?:"...",snapshot:{...}}}`. POST responses echo the exact body idempotencyKey. GET /tournaments/{id} must serve this versioned contract before the client sends Join. A legacy success flag, missing balances/revision, malformed assignment, or mismatched receipt fails closed. The listing GET /tournaments remains the existing list contract; this change governs authenticated detail/mutations only.

All requests require the existing bearer session. Player identity comes from that session, never a submitted account ID. The snapshot localStatus/localAssignment and wallet belong to the authenticated caller.

## Fields and bracket identity

- revision is a nonnegative monotonically increasing int64. The same revision means identical semantic snapshot content; array order is immaterial. wallet.revision also cannot move backward.
- maxPlayers is4 or8, targetScore is15 or30, and withFlor is explicit boolean. entryFee and advertisedPrize are nonnegative integers in Trucoins. No omitted number may silently become0.
- participants have unique playerId, registrationId and position0..capacity-1. They retain positions after start, including eliminated/withdrawn participants. entryPaid means the entry was atomically confirmed, including free entries whose debit is0.
- Once starting/playing, return exactly capacity-1 match rows with immutable (round,index) coordinates. Opening seats are position2*index and2*index+1. Later seats come only from the respective child winner, never arrival/actor order.
- A future waiting row may have null matchId/roomName and unknown player IDs. A ready/playing/finished row requires a real unique backend matchId, both assigned player IDs, and roomName `TMatch_{tournamentId}_R{round}_M{index}`. IDs, existing seats and finished winners cannot change in later revisions.
- localAssignment is null until an authenticated player has a ready/playing table. When present it exactly matches that match row, contains that player's ID, and lists both distinct expected players. The client never creates a backend match from Photon actor order.
- status is registration,starting,playing,completed,cancelled,reconciliation_required. localStatus is not_registered,registered,waiting,playing,eliminated,withdrawn,champion. localStatus champion requires a server-finished final won by that caller.

## Operations

- POST join: atomically authorize/register/debit once. Return paid registration receipt and current snapshot. If already registered, return its current registration without charging again. Insufficient balance/closed registration must have no partial debit.
- POST start: any paid registered caller may request it when full. Server atomically freezes all positions and creates every opening backend match exactly once. Simultaneous requests with different client keys still converge to one resource-owned start. Already started returns the current snapshot, not duplicate matches. Partial external creation failures enter reconciliation_required or cancel with authoritative refunds; never return ready tables with invented IDs.
- POST leave: server decides pre-start refund eligibility or post-start abandonment, records it idempotently and returns current state. A client may navigate away offline, but retains an account-bound resume ID and never credits a refund locally.
- POST reconcile: decide reconnect/walkover/no-winner outcomes from independently verified server presence/deadlines. The client reason is a hint, never proof. Two disconnected players must not both be declared winners. A cancelled match has null winnerId. If this prevents legal bracket progress, return reconciliation_required/cancelled according to the documented tournament policy; do not invent a local winner/bye.
- POST finalize-match: body matchId/winnerId/scores/handRound is a claim. Verify assigned caller, legal game result and peer/server evidence independently. Update that match and assign its winner to the fixed parent slot atomically. If both parent seats are known, create/return its backend match identity exactly once. Never charge an entry again per bracket table. Repeating the same result returns current state; conflicting results reject without modifying the recorded winner.
- POST finalize: only a validated final champion can request settlement. Semifinals never pay a championship prize. Credit exactly once with durable payoutId; return actual amount/currency/champion/status and current authenticated wallet snapshot. A transport success or advertisedPrize is not proof of paid amount.

## Idempotency, revisions and failure recovery

POST bodies carry a persistent random idempotencyKey and expectedRevision. The client persists keys per account/tournament/operation/match across timeout/restart. Keys are reused after uncertain outcomes. A repeated key+same logical payload returns the same operation record plus the current authoritative snapshot. Conflicting logical payload rejects. expectedRevision is a concurrency hint: unrelated joins or sibling results advancing the global revision must not make otherwise valid commutative operations impossible. Re-evaluate resource preconditions atomically, reject genuinely conflicting identity/outcome changes, and return the current revision/state through the error/reconciliation contract.

Reconcile is a repeatable check: after a confirmed response the client may issue a new key for a later check. Ledger uniqueness and transaction rules prevent duplicate monetary effects independently of client keys. Preserve confirmed payout/refund IDs and amounts forever; completed/cancelled tournaments cannot silently reopen under the same ID.

On disconnect, the client stops local actions and submits reconciliation. Reconnection retains Photon stable actor/deal ownership separately; server settlements cannot trust that UI alone. Photon custom authentication/webhooks or equivalent server-side authenticated presence and result validation are required. Server reconnect deadline is60 seconds from verified loss of connection, aligned with the agreed Photon TTL policy; local countdown timing is only presentation.

## Settlement receipts

Payout status pending carries no confirmed amount. Paid requires nonnegative integer amount, currency TRUCOINS, nonempty payoutId, and championId matching the finished final. amount is the actual paid value and may differ from advertisedPrize. not_due means no payment is owed, not pending.

Every cancelled tournament snapshot must include a refund row for each paid positive-fee entry, pending or refunded, for the FULL entryFee without commission. Confirmed refunded status needs a durable refundId. A smaller or missing refund cannot be treated as settlement. Voluntary forfeiture while the tournament still plays may return not_due according to the agreed rules.

Refund status pending cannot raise available balance locally. refunded requires refundId and the actual nonnegative integer amount. not_due has amount0. The server wallet available/held/revision fields are required and authoritative. Neither a request acceptance nor a timeout is a refund receipt.

## Client integration entry points

`TryResumeTournamentAsync(bool userInitiated=false)` reads a durable account-bound tournament ID after login. It never re-registers/debits. Deliberate local exit suppresses automatic re-entry for the same session while retaining unresolved state for the next login/manual resume.

`LeaveTournamentByPlayerAsync(Action onCompleteAction=null)` returns whether navigation can complete, not whether a refund was confirmed. Its callback runs on confirmed or recoverable-offline exit. LastLeaveConfirmed distinguishes server acceptance; unresolved registration/payment/refunds remain explicitly pending. `LeaveTournamentMatchmaking` is local cleanup only and must not be presented as cancellation/refund.

`ReconcileTournamentAfterDisconnectAsync(reason)` applies only server-assigned outcomes. `RefreshAuthoritativeStateAsync()` polls the immutable snapshot, never merges Photon result JSON into authority. The local Editor AI practice stays separate and is never packaged in Android players.

## Photon ticket and deployment contract

`PhotonSession` is `POST /auth/photon-session`, authenticated by the API session. Body: `{mode,tournamentId,matchId,roomName}`; mode is exactly `one_vs_one`, `tournament`, or `spectator`. The other fields are strings or null. This endpoint neither registers a player nor debits an entry. Pre-fee identity preflight uses null match/room and may return an empty room allowlist. Only authenticated Photon Master readiness permits a subsequent NEW paid registration request. Exact known assignment tickets then require the assigned room in `allowedRooms`.

Response: `{success:true,data:{schemaVersion:1,ticket:{provider:"photon",userId,mode,tournamentId,matchId,authToken,expiresAt,appVersion,ticketId,allowedRooms:[]}}}`. `userId` is the immutable API account ID; the provider returns it as Photon `UserId`. Context echoes exactly. Expiry is an ISO UTC string (recommend 2-5 minutes); ticket and appVersion are nonempty; allowedRooms contains unique exact room names, no wildcards. The client's POST to the Photon CustomAuth provider sends `{token,mode,tournamentId,matchId}`. Token remains private/memory-only, is never part of remote config, room properties, saved state, or logs, and clears on logout/account generation changes.

Backend deployment must implement the Photon CustomAuth provider, disable anonymous fallback, validate signed/opaque tickets and server-owned identity/scope/expiry, and return authenticated UserId/opaque AuthCookie context. Configure `PathCreate` to verify the creator/room/assigned mode and `PathBeforeJoin` to reject unauthorized accounts BEFORE an actor joins. `PathJoin` is an after-join notification and is insufficient. If PathBeforeJoin is unavailable in the Photon dashboard, enable it through Photon Support or use an equivalent server plugin before release. Verify webhooks authenticate Photon server requests; no embedded client API secret is proof. PublishUserId is enabled so participants compare the exact API-assigned pair using immutable `Player.UserId`. Presence/reconnect deadlines/results/refunds remain server-owned; a mutable custom userId property is display-only.

Photon documentation: https://doc.photonengine.com/pun/current/connection-and-authentication/authentication/custom-authentication and https://doc.photonengine.com/realtime/v5/gameplay/web-extensions/webhooks . Implement these deployment gates before production acceptance; a client compilation or synthetic ticket fixture does not prove a deployed service is ready.
