# 17-page specification cross-check — 31 October 2026

Source: `C:/Users/User/Downloads/unity%20AI.pdf`, all 17 pages extracted and visually inspected, including the nine reference-screen pages. The tables below account for all **50 numbered feature requirements** in sections 3A, gameplay rules, tournament startup, tournament rounds, 3C, 3D and 4C. The document's process/release gates are recorded afterward. Later user instructions authorize the improved theme, image assets, Editor AI and proposed API definitions; screenshots and fixtures remain visual references rather than production evidence.

“Client covered” means implementation exists and relevant automated/source/UI evidence is identified. It does **not** mean an absent backend has passed acceptance. Server-owned financial/permission/lifecycle behavior remains a deployment dependency even when the parser has passing fixtures. See `ReleaseFinalTests.xml` for the final full Unity run, `ReleaseAuditTests.xml` for the earlier baseline, and `README.md` for the release decision. The intermediate failing assertion run is retained as `ReleaseFinalTests-attempt1.xml`; it compared InputActionReference wrapper identity instead of the actual configured action and bindings.

## 3A — 1v1 rooms, cancellation and refunds (6)

| ID | Required behavior | Client evidence | Remaining acceptance |
|---|---|---|---|
| A1 | Exact selected name, Flor, public/private/password, 15/30, entry and prize; canonical response display | `PlayerCreateMatchRequest`, `Player1v1MatchExtensions`, create/join flow, `TrucoLobbyFlowEditModeTests` | Live server round-trip for every setting combination |
| A2 | One room card per server ID despite repeated list/socket events | ID-keyed room reconciliation; lobby tests | Repeated live events with two devices |
| A3 | Supported delete/cancel acknowledgement and retry without force-close | `OneVsOneMatchLifecycle`, strict server acknowledgement, lobby/confirmation tests | Deployed cancel endpoint including timeout/retry |
| A4 | Unstarted empty-room 100% refund once after leave/disconnect/cleanup | Durable `TrucoPendingRefundStore`; dedicated cancel-lobby-refund contract | Atomic ledger/tombstones and server cleanup worker |
| A5 | Accurate pending refund; refresh balance/room on login/reconnect | Main-menu recovery drain, no unrelated balance-delta proof; confirmation tests | Lost response/app-kill recovery against real ledger |
| A6 | Investigate device-specific creation using network/API/environment evidence | Safe bounded HTTP errors and authenticated transport preflight | **Not device-verified:** actual signed APK, affected devices and backend request logs needed |

## 3B — challenge phases and preserved rules (8)

| ID | Required behavior | Client evidence | Remaining acceptance |
|---|---|---|---|
| R1 | Pending Flor/Envido blocks cards and second actions | GameManager pending/dictionary lock, TurnManager gating; release gameplay tests | Two authenticated clients with real challenge exchange |
| R2 | No stale Truco/Mazo/Quiero/Envido/Flor controls during transitions or ended hands | Central challenge/hand guards; `TrucoRulesEditModeTests`, gameplay scene wiring | Network timing and physical input acceptance |
| R3 | Sin Flor enforced in model, score, voice/text and transitions for both players | Match rule configuration and Sin Flor regression coverage; stale NoFlor RPC correction | Server validation of 15/30 Sin Flor matches |
| R4 | Reveal all three Flor cards before hand/match close; voice/text once | Existing reveal flow retained, stable dealt-hand ownership, rule tests | Two-client animation/audio observation at resolution boundaries |
| R5 | Falta Envido winning cards and final card movement finish before transition | Existing deferred reveal/hand close retained; rules tests | Real rendered two-client end-of-match timing |
| R6 | Accepted Truco=2/Retruco=3/Vale4=4 Mazo; independent Flor/Envido; first-trick tie | Deterministic rule regressions, winner-relative Envido cap and even-round Mano correction | Live backend settlement consistent with accepted scores |
| R7 | Preserve reported first-action Mazo and immediate winning Contra Flor variant | Existing behavior preserved; no unsupported rule variant invented | **Exact backend rule variant still requires confirmation**, as the PDF expressly requires |
| R8 | Son buenas text/audio once to both, respecting pending phase | Existing Son buenas flow and guarded phase preserved; rule regressions | Two-client voice/text trace; no production audio claim from pure tests |

## 3B — tournament startup (7)

| ID | Required behavior | Client evidence | Remaining acceptance |
|---|---|---|---|
| S1 | At 4/4 or 8/8 retrieve real server match IDs and player assignments | Versioned strict snapshot; atomic Start request; no actor-order/local match fallback | Backend creates every opening match atomically |
| S2 | Same real gameplay scene/controller as 1v1, exact room/match context | `MultiplayerController`, shared GameManager, scoped Photon tickets and published immutable IDs | Deployed CustomAuth/admission hooks and live simultaneous tables |
| S3 | No game/reconnect clocks or walkovers before both valid hands synchronize | `TrucoTournamentHandReady`, dealt-round/actor metadata; startup/release tests | Delayed load, reconnect and host migration on deployed transport |
| S4 | Own three cards, opponent, board, score, turn, target and Flor mode | Stable hand seats, card sender/dealt-card validation, scene wiring/release tests | Both real authenticated clients observe correct hands/settings |
| S5 | Precise recoverable Spanish failure for incomplete opening matches | Startup validator, explicit waiting/failure state, snapshot polling/retry | Inject backend partial-start response and recover |
| S6 | Irrecoverable failure shows failed/cancelled, permits exit, requests reconciliation | Explicit server leave/reconcile, durable per-account resume; offline exit retains pending obligation | Actual server cancellation and confirmed entry returns |
| S7 | Explicit verified lifecycle states | Server snapshot status/localStatus → client lifecycle; no local settlement authority | Live state transitions and server recovery workers |

## 3B — rounds, reconnect and settlement (8)

| ID | Required behavior | Client evidence | Remaining acceptance |
|---|---|---|---|
| T1 | Immutable 4-player semis/final and 8-player quarters/semis/final | `TournamentStartupValidation`, immutable ancestry/pair validation; bracket/contract tests | Backend position immutability across all rounds |
| T2 | Early winner waits; next table only once both qualifiers exist | Server-owned localAssignment and waiting bracket; Editor 4/8 simulations | Full real events with deliberately delayed opposite match |
| T3 | Finalized match ID/result advances once under duplicates/retry | Stable mutation keys/body, revision and receipt checks; contract tests | Concurrent/replayed requests with server ledger assertions |
| T4 | One-player timeout uses backend walkover result | Disconnect reconciles server state; removed premature local win/end claim | Server presence/deadline authority and actual walkover |
| T5 | Both absent → cancelled/no winner/full each-entry refund/no commission | Strict cancelled snapshot/refund validation, reconcile API; contract fixtures | Server all-offline scheduler and exactly-once full refunds |
| T6 | Eliminated bracket observation optional; back always returns | Explicit leave/navigation callback, same-account resume suppression and durable pending state | Device offline/back/reopen with real session |
| T7 | Champion text and actual confirmed prize; no local credit | Champion separates advertised estimate from paid receipt; payout presentation/contract tests | Real payout ID/amount and wallet reconciliation |
| T8 | Delayed/duplicate prize/results render idempotently and reconcile | Revision/subject validation, polling and stable server operation IDs | Server-delayed response/replay and other-device balance check |

## 3C — admin integration (5)

| ID | Required behavior | Client evidence | Remaining acceptance |
|---|---|---|---|
| C1 | Real players/matches/tournaments/reconnect/fraud/audit | Existing dashboard API consumers plus new role-checked paginated server audit; no local audit feed as truth | Actual authorized backend feeds |
| C2 | Admin tournament creation transmits 4/8, Flor, visibility/password, 15/30, entry/prize | Existing exact creation DTO/form and preset/config tests | Canonical server create/list/snapshot response |
| C3 | Confirmed authorized close/champion/prize/maintenance with confirmation | Separate admin champion route, strict applied operation receipts/idempotency, close/refund acknowledgement; admin contract tests | Server roles, unique financial effects, audit and lost-response tests |
| C4 | Maintenance global and blocks selected actions | Public status + canonical admin response, polling/banner and entry/transaction guards; wallet/server-confirmation tests | Another live account is blocked by backend enforcement |
| C5 | No UI/local flag grants admin rights | Removed built-in-email bypass; server role checks before protected reads/mutations; backend role still mandatory | Unauthorized endpoint tests on deployed server |

## 3D — gameplay/tournament UI (8)

| ID | Required behavior | Client evidence | Remaining acceptance |
|---|---|---|---|
| D1 | Compact scrolling rooms with complete settings/private password | Runtime room rows/theme/layout; menu/lobby tests; existing screen gallery | Supported-device safe areas and long values |
| D2 | Compact complete tournament cards and register/enter | Themed tournament list, 4/8 labels/settings and action routing; feedback tests/gallery | Real API registration/open flow |
| D3 | Clear 4/8 brackets, own user, waiting and winners | `TournamentBracketGraphic`, viewport fitting/highlights; bracket tests/gallery | Physical-device readability and real progress |
| D4 | Compact readable waiting/reconnect timer away from cards | Tournament status/reconnect presentation retained; feedback/scene checks | Two-device timing/layout observation |
| D5 | Readable own/opponent challenge prompts without invalid actions | Existing challenge presentation plus phase guards; rule/scene tests | Real voice/text/input timing |
| D6 | Three-card table reveal and final movement; no false deal/white flash | Existing reveal flow, stable hands, cached/stale deal rejection; rule/release tests | Rendered network stress/animation acceptance |
| D7 | Spanish result/navigation and server-confirmed prize | INICIO/original navigation, Spanish status messages; estimate-vs-paid tests and gallery | Final localization/audio/device sweep |
| D8 | Preserve working avatar/profile/notifications/mute/icons; readable theme | Original home preserved; solid brown nav, selected scale/color, common feedback; menu/club tests/gallery | Physical-device visual/input accessibility |

## 4C — Trucoins screens (8)

| ID | Required behavior | Client evidence | Remaining acceptance |
|---|---|---|---|
| W1 | Original home avatar/name/balance and 1v1/tournament/bottom nav | Original home menu tests, profile binder, strict explicit server balance; 100-coin default removed | Authenticated profile/HUD update on device |
| W2 | Available/held, Buy/Sell/History, recent real status/time | Wallet hub/recent three rows, full scrolling history and canonical snapshot; wallet UI/payload tests | Actual request history/holds from backend |
| W3 | Buy presets 50/60/75/100/150/200 + other/minimum/config/total | Server-configured presets/limits with strict integer validation; wallet tests | Backend current limits and amount validation |
| W4 | Request ID/quantity/pending/instructions/copy/send receipt; no sample bank data | Server-bound request page and authorized HTTPS receipt page; no local receipt-upload success | Real receipt hosting/storage and bank instructions |
| W5 | Purchase progress only after server confirmation | Request status/history parser, page-generation guards, snapshot/profile refresh; tests | Real transfer verification and exactly-once credit |
| W6 | Sale minimum/config/available, bank/alias/holder/ID/WhatsApp; server hold | Sale validation plus strict request/snapshot contract | Atomic hold and backend validation/ownership |
| W7 | Sale status/history and rejection returns held amount after confirmation | Server statuses, no local release/payment; admin approve/reject/pay with applied receipts | Actual bank payment, hold conversion/release and replay tests |
| W8 | Spanish loading/empty/rejected/offline/retry; prevent repeated submits | Guarded async page state, stable request intent keys, explicit errors; wallet UI/contract tests | Timeout/app-restart and actual service errors |

## Process and release gates from pages 1–8

Repository/scenes/Photon/API configuration were inspected before changes. All screenshot pages 9–17 were visually reviewed. Existing home art/layout was preserved in this audit; two orphan missing-script components were removed without moving its UI. The prior screenshot gallery remains under `Docs/QA/TournamentRepair-2026-10-31/`.

The client dependency map is: Init → public compatible route config → HTTP login/profile → MainMenu navigation → versioned tournament service → server assignment → scoped Photon CustomAuth → shared gameplay/hand readiness → proposed result → backend finalization → authoritative bracket/payout/wallet refresh. Wallet/admin screens use separate strict services; server role/ledger authority never comes from local AI or button state.

The original historical tournament non-opening defect was reproduced and repaired at the UI canvas/transition-cover layer in the previous QA report. This audit also replaces the unsafe production actor-order startup/finalization path with the proposed authoritative contract. Full real 4/8 backend tournaments are **not accepted solely from the Editor simulation or previous low-level Photon transport smoke tests**.

Automated rule, scene, challenge, lobby, presentation, wallet/admin, profile, tournament and ticket contract tests are recorded in this folder. Android build support/toolchain presence is checked by `preflight.json`. No newly signed production APK, bank transfer, deployed refund, Cloud authentication or supported-device certification was produced by this audit. The exact source build version is recorded in preflight; existing user version/signing choices were preserved.

The release decision remains **backend/staging/Android acceptance pending**, rather than an unsupported 100% claim. The [backend handoff](../../API/BACKEND_HANDOFF_V1.md) defines the exact remaining service contract and acceptance sequence.
