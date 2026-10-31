# Unity AI specification implementation status

For the later UI/AI implementation and 144-test Unity run, see `CLUB_UI_AND_AUTOMATION_2026_10_31.md`. This file preserves the earlier first-increment findings.

Date: 2026-10-31. Source: all 17 pages of the supplied `unity%20AI.pdf`, including the nine screenshot references. This is the first implementation increment, not release acceptance.

## Dependency map

| Area | Existing implementation and dependencies |
| --- | --- |
| Boot/navigation | Enabled scenes: InitScene, LoginScreen, MainMenu, AdminScene, Gameplay, TempScene. TournamentManager owns selection, matchmaking and waiting overlays. |
| Tournament registration/start | TournamentMatchmakingOverlay -> MultiplayerController -> ApiController.EnterTournament/CreateTournamentMatch -> HttpApiClient. MultiplayerCallbacks receives Photon room/player properties. Opening assignments are published in `openingTables`. |
| Shared gameplay | Both modes load Gameplay. GameManager, TurnManager and UIMANAGER own dealing, turns and challenges. TournamentGameplayVariantBridge applies Flor/score settings. TrucoMatchPlayGate guards table entry and several timer consumers. |
| Brackets/results | TournamentBracketState is a client projection; MultiplayerController.PlayNextBracket calls finalize-match/finalize. TournamentFinalizeGuard and TrucoServerAcknowledgement gate acknowledgements. |
| Reconnect/refunds | TrucoPunReconnectionManager; 1v1 lifecycle, pending refund store and ApiController cancellation helpers. Tournament reconciliation requires a verified backend contract. |
| Authentication/configuration | HttpApiClient uses Bearer authorization and refresh-token flow. ApiConfig has endpoint overrides in Resources/TrucoApiEndpoints. Photon region and client settings are Resources assets. No credentials changed. |
| Existing UI assets | TournamentCardPF/TournamentCardAdminPF, MainMenu overlays, TrucoUiTheme and runtime builders. Screenshot names, balances and banking/contact details are reference data only. |
| Wallet/admin | Existing wallet refresh and admin controls do not establish the purchase/sale contract requested in section 4C. ApiConfig does not expose that complete workflow. |

## First increment

- `Assets/Scripts/TournamentStartupValidation.cs`: deterministic validation of complete 4/8-player opening plans. Reject missing/duplicate match IDs, repeated/missing players, missing local assignment, wrong round/index and wrong room identity. Never return a partial assignment.
- `Assets/__TrucoCard/Code/Core/Multiplayer/MultiplayerController.cs`: validate capacity before registration; check cached failure on room join; process failure before match IDs/plans; latch a local startup failure before the Photon echo; exit failed matchmaking and label reconciliation pending. Handle malformed opening JSON without proceeding to a table.
- `Assets/Scripts/TrucoMatchPlayGate.cs`: require matching tournament ID and backend match ID in addition to the existing room checks. Preserve the existing 1v1 branch.
- `Assets/Editor/TrucoTournamentStartupTests.cs`: four deterministic test methods covering valid assignments and invalid/failure scenarios. Unity metadata included for new scripts.

Code inspection found that the old join path ignored a cached `tournamentFailure`, while a combined properties update could process `backendMatchId` before failure. The old opening-plan consumer accepted the first local seat without checking the rest of the plan. These are code-level findings; the reported production startup failure has not been reproduced with four devices.

## Verification

- Compiled runtime C# sources successfully with the installed Unity 6000.0.80f1 Roslyn compiler and cached Unity/package references. A temporary response file corrected the old installation path, included current first-party sources and added the installed Newtonsoft reference. No Library assemblies were overwritten.
- Compiled and executed all four new NUnit test methods under Unity's bundled Mono runtime: all passed. This is a standalone deterministic test run, not an Editor/PlayMode or network test.
- `git diff --check`: passed.
- No APK generated. Current project settings report bundle version `1.0.4`, Android version code `1`; the PDF's v1.1.3 release claim is not a verified build identifier for this checkout.
- Existing edits in `Assets/StreamingAssets/build_info` and `ProjectSettings/ProjectSettings.asset` were preserved.

To run in Unity: open Window > General > Test Runner, choose EditMode, and run `TrucoTournamentStartupTests`, then the existing rule/settlement suites.

## Remaining implementation and release gates

| Requirement | Status / next evidence needed |
| --- | --- |
| Real 4/8-player startup | First defensive patch implemented. Need authenticated test users, client/server logs and a full event on the intended backend. |
| Server-owned immutable pairings | Still open: current code seeds from Photon actor order and requests server match creation. Validate authoritative assignments/round positions from backend before replacing this flow. Photon properties alone are not server authorization. |
| Both hands ready before clocks | Still open: room existence is not proof that both clients loaded/dealt. Implement per-hand readiness acknowledgement without deadlocking scene loading or master rotation. |
| Partial-create recovery/master migration | Still open: the local latch prevents same-session callback retries only. Backend idempotency/reconciliation is needed across disconnects, fresh sessions and master migration. |
| Cancellation/refunds/walkovers | No player-facing tournament reconciliation contract verified. Do not reuse admin force-close or claim a refund. Need authoritative single/double-disconnect outcomes and exactly-once credits. |
| Brackets/final/prize | Existing implementation requires full 4/8-player completion, duplicate finalize and server-confirmed prize amount tests. |
| v1.1.3 room/UI/challenge fixes | Existing code/tests found; device retest still required for exact room settings, card size, deletion, Envido lock and Son buenas. |
| Truco/Flor/Envido regressions | Existing suites retained; full Unity and two-client regression run outstanding, including Sin Flor 15/30 and reveal timing. |
| Admin and maintenance | Existing client integration; authorization and global effects need backend/device evidence. |
| Wallet buy/sell/history | Deferred until tournament core is verified and backend supplies limits, request/status schemas, receipt flow, held balance and bank/contact configuration. No mock transactions added. |
| Android release | Build/version reconciliation, APK build and representative device/network testing outstanding. |

No feature in this increment is marked end-to-end verified. No live transactions, registrations, refunds or administrative actions were submitted during this work.
