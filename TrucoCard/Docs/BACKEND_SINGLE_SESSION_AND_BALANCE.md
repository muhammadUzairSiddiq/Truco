# Backend requirements: single active match + Trucoin lock

Client hardening for Version 1.0.7+ reduces dual-device create races, but **atomic protection must live on the server**.

## Required API behavior

### Mandatory: idempotent pre-game refund (Milestone 3A)

This is a backend financial operation, not a Photon or Unity cleanup operation.  A player who
created a paid 1v1 room that never reached `started` must receive the full entry fee exactly once,
even if the app is killed, the Photon room expires, or the match row is later removed by a janitor.

Implement a single transactional operation, for example `POST /matches/:id/cancel-lobby-refund`.
The authenticated creator may call it repeatedly.  It must lock the match/settlement record and:

1. reject the request when the match ever reached `started` or has a winner;
2. atomically change an open pre-game match to `cancelled` and credit the original entry fee;
3. insert a unique immutable ledger row such as `match-refund:<matchId>:<userId>` in the same
   transaction; and
4. return the settled state and credited amount for every retry, without creating another credit.

Do not hard-delete a pre-game match until its refund settlement record exists.  If normal cleanup
must remove the match row, preserve a tombstone containing `matchId`, participant ids, entry fee,
`neverStarted`, refund status, and refund ledger id so the operation remains replay-safe.  The
existing `POST /leave` route may delegate to this operation for a pre-game lobby, but it must not
be the only place this settlement information lives.

Required integration tests:

- create paid room → no join → creator force-kills app → returns after room cleanup → one full refund;
- repeat cancel/refund request concurrently and after success → balance credited once;
- creator disconnects/reconnects repeatedly before and after cleanup → one refund;
- a joined or started match → no pre-game refund;
- failed database transaction at every write point → no partially credited/refunded state.

1. **One active lobby/match per user**
   - `POST /matches/player-create` and `POST /matches/:id/join` must reject when the authenticated user already has an open lobby or in-progress 1v1 match.
   - Prefer HTTP 409 with a stable message such as `Player already has an active room` (client maps this to “Ya tenés una sala activa”).

2. **Atomic Trucoin reservation**
   - Deduct/reserve entry fee in the same DB transaction as creating/joining the match.
   - Concurrent create/join from two devices with balance 20 / stake 20 must allow **at most one** success; the other must fail with insufficient balance or already-has-room.
   - Balance must never go negative.

3. **Optional but recommended: single active session**
   - Issuing a new login/token may invalidate older device sessions so the same account cannot play two live Photon matches as itself.

## Client already does

- Refresh profile balance before create/join.
- Purge leftover lobby rows before create.
- Clear sticky local host prefs when the match is gone from `GET /matches`.
- Map “already has active room” API errors to player-facing copy.
