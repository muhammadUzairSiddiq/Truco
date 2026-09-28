# Milestones 3A–3D: release gate

Status: **NOT approved for production release.** Unity changes and isolated QA are not backend or device acceptance.

## September 26 UI review

- Existing gold/green/brown spritesheet is present and used by runtime UI; no replacement bitmap was generated in this pass.
- Replaced stacked bracket presentation with connected 4/8-player round columns, waiting slots, winner highlighting, final trophy and refresh/back controls.
- Enlarged champion panel/trophy and retained dynamic prize plus payment-confirmed versus pending copy. Private tournament cards now use the brown panel.
- These are presentation-only changes. No gameplay rule changes in this UI pass.
- Updated clone compiled and passed **116 tests, 0 failures, 0 skipped**, including five new bracket presentation checks and the three isolated Photon transport tests. Results: `D:/TrucoQA/20260923-ClientA/UIFinalTestResults.xml`.
- Headless checks do not establish visual reference parity. Actual screen rendering, portrait/aspect/safe-area testing, detailed room-table reference layout, player-avatar integration and Android review remain open.

## Scope and safety

- Preserve the existing Truco, Envido and Con Flor rule implementation. Sin Flor guards enforce the selected variant.
- Preserve the pre-existing dirty worktree. No backend implementation, production balance mutations, deployment or release signing was performed.
- QA clone: `D:/TrucoQA/20260923-ClientA`. Separate product/PlayerPrefs identity; API targets `http://127.0.0.1:18731/api`.
- Live Photon tests use independent clients and a unique application-version namespace for each test; no game API or wallet operations.
- The Photon tests are transport tests, **not** full authenticated matches through the Gameplay screen.

## Implemented client corrections

| Area | Corrections | Acceptance still required |
| --- | --- | --- |
| 3A unused-room refund | Keep paid-entry recovery pending until explicit refund receipt; no success from vanished row, unrelated balance increase, or unknown balance; pending notification on unsuccessful cancellation | Kill/relaunch app, cancel retries, account switching, ledger proof of exactly one 100% credit |
| 3B Sin Flor | Variant applied before dealing; guards cover detection, scoring, response UI and reconnect snapshots | Two-device hands with Flor in either seat; normal Envido/Truco/Mazo/card play; no audio/text/state/points from Flor |
| 3B startup | Separate 1v1/tournament callbacks; register with a real player-ID payload; require backend match IDs; publish opening pairings together | Real 4/8-account start, both three-card hands, all action/turn/timer synchronization |
| 3B progression | Merge independent results without erasing completed branches; preserve winner slots; reset next-match scores; leave/reload gameplay between tables; wait for result confirmation | Every round, winner waiting, delayed sibling, reconnect and walkover in each round |
| 3B economy | Bracket table requests send zero additional entry; final screen does not assert prize receipt without a payment receipt | Backend confirmation of per-tournament fee semantics; idempotent creation/result/finalization/cancellation |
| 3C Unity admin | Reject unconfirmed financial/admin responses; wrapped maintenance parsing; apply maintenance changes only after server confirmation; periodic dashboard/menu maintenance refresh; validate create-form amounts/password | Real admin API integration, global enforcement, server audit, role enforcement, force-close and exceptional actions |
| 3D presentation | Circular timer presentation, compact speaker/challenge messages, full-width room metadata/password lane, retained password during refresh, menu exit cleanup, champion payment-pending state | Android visual checks, spectator bracket updates, avatar/notification/audio checks, device memory/performance profiling |

## Tests and reproducibility

- `TrucoTournamentFeedbackTests`: 4/8-player bracket mapping, waiting, duplicate/foreign results, merge/JSON round trip, Sin Flor calculation/score guards.
- `TrucoServerConfirmationTests`: explicit refund/payment receipts; rejection, malformed responses and wrapped maintenance state.
- `TrucoGameplaySceneWiringTests`: both three-card slot sets, required controls, scene inclusion and UI assets.
- `TrucoPhotonCloneSmokeTests`: independent 2/4/8-client connections, paired rooms, configuration/property synchronization, disconnect and rejoin. Opt-in with `TRUCO_RUN_PHOTON_QA=1` in the QA process only.
- Existing rule, settings, lobby and settlement tests remain in the suite.
- Isolated scene-inclusive run: **111 passed, 0 failed, 0 skipped**. XML: `D:/TrucoQA/20260923-ClientA/FinalTestResults.xml`. Later runs must supersede this when sources change.
- Run Unity with `-batchmode -nographics -projectPath <QA clone> -runTests -testPlatform EditMode -testResults <XML> -logFile <log>`.
- `Tools/Prepare-MultiplayerQA.ps1` copies working source into a NEW directory without copying Library or deleting an existing checkout. Before launching, isolate product name, API URL and Photon version. Source configuration is initially copied, not automatically sanitized.
- `TrucoQaBuild.Android` only runs in a product named `Truco-QA-*`; uses debug signing and package `com.truco.qa.clienta`. This is build verification, not a release APK.

## Required server contracts (not implemented here)

1. Unused-room cancellation must return a transaction-specific receipt, e.g. `{ "ok": true, "data": { "matchId": "...", "refunded": true } }` or `alreadyRefunded: true`. HTTP 200, `ok: true` alone, room deletion and wallet deltas do not prove a refund.
2. Match/tournament/admin mutations must return explicit `ok: true` or `success: true`. A tournament result acknowledgement is not a payment acknowledgement; `paid: true` or `prizePaid: true` is required to display payment as received.
3. Failed tournament startup needs an authoritative cancellation/refund operation covering every paid participant and removing the tournament from active lists. No confirmed player-facing contract is available; do not substitute admin force-close or mint client coins.
4. Backend must enforce membership, bracket mapping, trusted results, once-only entry/prize/refund, and duplicate match-creation prevention across master changes and lost responses. Photon properties and PlayerPrefs are not financial authority.
5. Maintenance must be enforced server-side for all operations, with real admin identity/timestamps and double-confirmed UI. Local audit history is not a substitute for a server audit.

## Release-blocking evidence still missing

- Approved staging endpoint and 4–8 dedicated test accounts with test funds, plus admin access/authorization for exceptional operations.
- Full authenticated 4/8-player Gameplay runs, including interrupted creation, both seats dropping, host migration, waiting winners, elimination watching and final settlement.
- Ledger verification: one entry per participant, one refund for eligible cancellation, one champion payout, no duplicate economic mutation after retries or restart.
- Android device/emulator. ADB reported no attached devices during this session. Layout/audio/performance acceptance has not been performed.
- APK build result and device install/run evidence; production signing/deployment remain separate release steps.

Do not tell the client that Milestones 3A–3D are complete until these gates pass.
