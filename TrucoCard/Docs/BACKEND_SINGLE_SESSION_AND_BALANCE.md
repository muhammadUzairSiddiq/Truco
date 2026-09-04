# Backend requirements: single active match + Trucoin lock

Client hardening for Version 1.0.7+ reduces dual-device create races, but **atomic protection must live on the server**.

## Required API behavior

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
