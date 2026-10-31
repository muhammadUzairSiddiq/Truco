# Tournament UI repair and Editor practice verification

31 October 2026 · Unity 6000.0.80f1 · isolated QA project `D:/TrucoQA/20260923-ClientA`.

## What was fixed

Tournament practice used canvas order 35000, outside Unity's signed 16-bit range. It could render behind the menu. Shared `TrucoCanvasOrder` now assigns valid layers to practice, live tournament status/bracket/champion, wallet, confirmations, maintenance, reconnection and transitions. A second rendered test caught the transition cover remaining active during direct Editor entry. Opening practice now releases that cover before exposing its controls.

Editor practice opens before backend/lobby cleanup, and hooks rebind with domain reload enabled or disabled. Live list requests support empty results, clear stale list contents, clear loading on failures and preserve menu recovery. Profile preferences deactivate when the production list opens.

Tournament banner/cards, waiting/details, table, results, bracket and champion views use the brown/gold/cream/green theme. Brackets refit after canvas dimensions settle. Shared surface styling resets legacy sprite scaling that had stretched button corners.

## How to test without a build

1. Select **Truco > Editor Tournament Practice > Open AI Tournament**. It starts Play Mode if needed.
2. Choose the four- or eight-player tournament, enter practice, view the bracket, then play your table.
3. Open **Truco > Editor Tournament Practice > Settings** to toggle `enabledInEditor`. The asset is `Assets/Editor/TrucoTournamentPractice/TournamentPracticeSettings.asset`.
4. Enable `autoPlayYourSeat` to let AI play every seat. Disable it to play against AI. Flor, target score, seed and delay are editable.
5. Disable `enabledInEditor` to route tournament entry back to the live backend.

No real registration, Trucoins debit, prize or refund occurs in practice. It uses shared rule helpers and bracket state, with a local development match driver. It does not prove the full production Photon/API path or every advanced challenge variant.

## Verification

- Rendered pointer regression: **2 passed, 0 failed** after the transition-cover fix. It checks actual `EventSystem.RaycastAll` targets above a menu canvas, pointer-clicks tournament details and wallet purchase, and verifies production bracket/champion/status layers. Report: `pointer-after-cover-fix.xml`.
- Full focused run before that last cover fix: 48 of 49 passed. Its single failed rendered-pointer case found the cover blocker and is superseded by the passing report above. Original evidence is retained unmodified as `full-regression-before-cover-fix.xml`.
- That full run completed both interactive AI tournament paths: **4 players / 3 matches / 228 actions**, and **8 players / 7 matches / 595 actions**. Seed 1731, target 15, Flor enabled. It also passed deterministic four/eight-player model cases at 15/30 with Flor on/off, unique deals, pending challenge locks, idempotent advancement and all 40 card faces.
- Runtime and non-Editor C# compilations passed after the final runtime changes.
- Player assembly inspection found none of: `TrucoDevelopmentBot`, `TrucoDevelopmentSettings`, `TrucoEditorTournamentHooks`, `TrucoPracticeMatch`, `TrucoPracticeTournament`, `TrucoTournamentPracticeUI`, `TrucoTournamentPracticeSettings`.
- Practice sources and SO assets live under `Assets/Editor`; runtime entry points are guarded by `UNITY_EDITOR`. Practice assets are not build-scene dependencies. No Android APK or device validation is claimed.

## Actual Unity captures

Open `gallery.html` for 33 full-size 480x800 PNGs. Five contact sheets group the menu/profile/notifications/rooms, tournament list/practice flow, wallet, account forms, and production tournament lifecycle states. These are actual source UI renders, not image-generated screen mockups.

Wallet data, tournament list entries and lifecycle states use Editor fixtures. A displayed approved sale or pending prize in a screenshot is sample presentation data, not evidence of a server payment. No user notification history was changed for previews.

Backend integration and settlement acceptance still require the API contract in `Docs/BACKEND_API_REQUEST_2026_10_31.md`. The earlier Photon transport/rejoin report remains separate evidence. The original home scene was not edited by this UI pass; existing user build/settings changes remain preserved.
