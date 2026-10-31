# Release audit — 31 October 2026

**Client contract/integration work is delivered. Production sign-off remains pending backend deployment, authenticated staging acceptance and a signed Android device run.** This report does not certify 100% functionality or an absence of all possible defects.

All 17 pages of `C:/Users/User/Downloads/unity%20AI.pdf` were read, including the reference screenshots. [The requirement matrix](requirements.md) accounts for all 50 numbered feature requirements and records the external acceptance still needed for each. The owner's later request authorizes defining the missing APIs and the improved theme; the PDF is a checklist, not evidence of a working server.

## Delivered

- Exact proposed wallet, admin, tournament and transport contracts integrated into the Unity client; [backend handoff](../../API/BACKEND_HANDOFF_V1.md), [OpenAPI](../../API/trucopy-v1.openapi.json) and [all 92 route keys](../../API/ENDPOINTS.md).
- One searchable `TrucoApiEndpointsSO` at `Assets/Resources/TrucoApiEndpoints.asset`. Compatible routes can update after release through versioned trusted HTTPS bootstrap JSON. Editing an SO alone does not update an installed APK; new protocols, HTTP methods or untrusted origins need a compatible build.
- Authoritative tournament snapshots/assignments, immutable bracket progression, result/finalization receipts, actual paid-prize display, durable resume, recoverable leave and refund reconciliation. Production no longer invents tournament matches from Photon actor order.
- Scoped Photon CustomAuth support, immutable published account IDs and exact assigned-pair checks; verified connection readiness precedes a new paid entry. Deployment of the provider/admission hooks remains the backend team's work.
- Player wallet requests/history/held balance, admin purchase/sale review and payment recording, server audit pagination, strict financial payloads, subject-bound applied receipts and retry-safe admin operation keys. No real transfer or coin mutation was performed in this audit.
- Gameplay fixes for pending challenge locks, winner-relative Envido caps, spectator filtering, stable dealt-hand actor ownership through host migration, stale/duplicate deal rejection and card sender/ownership validation. Existing rule/reveal behavior was retained.
- Canonical server-confirmed maintenance state; removed built-in-email admin bypass, raw response/cookie/token logs and saved passwords. Removed the fabricated default profile balance. HTTP cookie merging, logout/native cookie clearing and concurrent refresh guards repaired.
- Correct Input System fallback for admin screens; explicit admin close confirmation; Unity 6 fallback font correction. Reporter pointer handling works with the project's Input System setting.
- Original home artwork/layout retained; only two orphan missing-script components were removed from MainMenu. Existing brown/gold/green theme, solid brown navigation, selected-icon feedback and tournament screens were preserved. [Earlier screen gallery](../TournamentRepair-2026-10-31/gallery.html); [four additional actual admin UI captures](admin-screens/contact-sheet.jpg), explicitly labelled Editor fixtures.

## Verification

| Check | Observed result | Evidence / limit |
|---|---|---|
| Final full Unity EditMode suite, including tests that enter Play Mode | **337 cases: 334 passed, 0 failed, 3 ignored** | [Raw NUnit report](ReleaseFinalTests.xml). Ignored cases are opt-in live Photon 2/4/8-client tests; no separate staging AppId was supplied. |
| Earlier baseline suite | 318 cases: 315 passed, 0 failed, 3 ignored | [Unmodified earlier report](ReleaseAuditTests.xml) |
| Intermediate Input System test assertion | One wrapper-identity assertion failed, then was corrected and rerun | [Original failing report](ReleaseFinalTests-attempt1.xml). Unity's SDK creates another InputActionReference wrapper for the same action. Final test verifies actual action instance, GUID, binding, enabled state and configuration. Helper behavior was not weakened. |
| Editor-runtime and Android-defined non-Editor C# compilation | Both compiler exit 0 | [Compiler summary](verification.json). Existing obsolete API warnings remain; this is not an IL2CPP/Gradle/signing build. |
| AI exclusion | All eight inspected AI/practice types absent from Android-defined assembly | [Reflection result](player-ai-exclusion.json); build scene dependencies and Resources checked separately. |
| Production static preflight | **25 checks passed** | [Preflight JSON](preflight.json): AI off, no missing scene scripts, no Editor practice dependency, routes configured/valid, Android support and SDK/NDK/JDK present. Presence checks do not prove an APK. |
| OpenAPI 3.0.3 validation | Passed with openapi-spec-validator 0.7.2 | 85 paths, 88 operations, 89 schemas, 92 logical keys; internal references resolve. [Validation summary](verification.json). Reserved legacy entries are identified separately from new integration contracts. |
| Actual UI rendering | Four new admin list/detail/empty captures inspected; no clipping or overlap observed at 480×800 | [Contact sheet](admin-screens/contact-sheet.jpg). Fixture data does not verify a backend. |
| Source snapshot | Relative paths, sizes and SHA256 hashes | [Source manifest](source-manifest.json); generated after final source changes. |

The passing tests cover rule scenarios, lobby settings/lifecycle helpers, scene wiring, menu/navigation presentation, real layered-canvas pointer dispatch, bracket/feedback presentation, Editor 4/8-player simulations, wallet/profile/admin payloads and guards, authoritative tournament/ticket validation, HTTP session merging/concurrency, and Input System fallback. Pure fixtures do not establish a deployed ledger or a live authenticated tournament. Historical low-level Photon smoke tests are not proof of the new CustomAuth deployment.

## AI switches

**AI is OFF in the source project and the verified QA copy.**

| Asset / type | Current value | How to use during development |
|---|---|---|
| `Assets/Editor/TrucoTournamentPractice/TournamentPracticeSettings.asset` / `TrucoTournamentPracticeSettings` | `enabledInEditor=false`, `autoPlayYourSeat=false` | `Truco → Editor Tournament Practice → Settings`; enable `enabledInEditor` for the local tournament preview. `Open AI Tournament` deliberately enables it. Disable afterward or run preflight before handoff. |
| `Assets/Editor/TrucoDevelopmentSettings.asset` / `TrucoDevelopmentSettings` | `mode=Multiplayer` | `Truco → Development → Settings`; legacy synthetic networking additionally requires a **different staging** `stagingPhotonAppId`. |

Both settings are under `Assets/Editor`; runtime bot/settings/hooks use `UNITY_EDITOR` guards. They are excluded from Android players, including development players. Tests restore the original practice flags instead of leaving AI enabled. The preflight menu is `Truco → Release → Run production preflight`.

## Build identity and outstanding acceptance

Audited Unity version: **6000.0.80f1**. Existing source bundle version: **1.0.4**, Android version code **1**. User version/project settings and `Assets/StreamingAssets/build_info` were preserved. **No new signed production APK was generated**, so there is no production artifact hash/signing/device certification in this report.

The separate backend developer must implement/deploy the exact handoff contract and complete its ledger/authorization/concurrency acceptance. Photon CustomAuth and pre-join room authorization must be configured and exercised on staging. Normal 4/8-player completion, actual champion payment, double-disconnect full refunds, receipt verification, sale holds/payments and maintenance on another account still need real service evidence. The specific backend rule variant mentioned in requirement R7 must also be confirmed.

Run the signed Android artifact on the supported devices for startup, touch input, safe areas, networking/TLS, background/reopen and reconnect. A static toolchain check, C# compilation or local AI simulation cannot replace that release gate. The [backend acceptance sequence](../../API/BACKEND_HANDOFF_V1.md#backend-acceptance-before-production) is ready for the backend developer; no additional API-name clarification is needed to begin that work.
