# Tournament visibility in Android players — 1 November 2026

The Editor AI path and the live player path entered the tournament UI differently. This caused a real presentation defect when the live API was unavailable. Backend activation alone does not update an installed APK.

## Confirmed causes

1. `MainMenuViewCoordinator` used an Editor-only practice shortcut when AI was enabled. The player route called `TournamentManager.DisplayTournamentSelectionUI`, which awaited a successful list response before displaying the tournament screen. A failed response navigated back to Home. The screen therefore disappeared precisely when the backend was unavailable.
2. `TournamentBracketOverlay` drew at order 32400 while `TrucoTournamentStatusScreen` drew at 32500. Opening the live bracket above the waiting/status screen left it covered by that screen.
3. The exact received APK labelled 1.1.4 is not present here. The available local release artifact at `Library/Bee/Android/Prj/IL2CPP/Gradle/launcher/build/outputs/apk/release/launcher-release.apk` declares versionName **1.0.4**, versionCode **1**. Its IL2CPP metadata includes `TournamentBracketOverlay` and `TournamentBracketGraphic`, and excludes the inspected practice/bot types. This proves live rendering code exists in that artifact; it does not certify another APK's contents.

## Client changes

- Open the themed tournament browser immediately, before resume/list requests complete.
- Keep the browser open on API failure with a Spanish error state and retry action. Distinguish failure from a successful empty list.
- Keep 4-player and 8-player format buttons available during loading, failure and empty states. Both open the real production bracket renderer with empty, unassigned slots. No fabricated participants, registration, results, fees or wallet changes occur.
- Closing a format returns to the browser without leaving a real tournament, disconnecting Photon or changing scenes.
- Raise bracket order to 32525, above tournament status and below champion/confirmation/transition layers.
- Ignore late list responses after the browser is closed. Clear format state when presenting the live server-driven bracket.
- Keep AI settings disabled and the practice/bot code excluded from player builds. The new browser/format path is ordinary runtime code and has no Editor-only dependency.

## What backend activation does

A compatible deployed API supplies tournament listings, authenticated registration, server-created assignments, participants, bracket progression and actual settlement. The existing live controller renders those verified states. Format viewing works before activation; entering and playing a real tournament still requires the server and Photon authentication setup.

The visibility fixes require a new APK from the updated source. Enabling APIs cannot retroactively apply a canvas/navigation fix to an installed build. Confirm the distributed APK's embedded version and hash before calling it 1.1.4 or a replacement release. Source version/signing choices were preserved.

## Verification

The new tests exercise the production browser with AI off, loading/failure/empty states, both bracket formats, no mutation of live tournament state, and return navigation. A rendered Play Mode pointer regression checks that the bracket can receive taps above the waiting/status canvas.

- [Focused test report](focused-tests.xml): **8 passed, 0 failed**, covering the player presentation and rendered canvas/pointer checks.
- [Final full regression report](final-tests.xml): **343 cases, 340 passed, 0 failed, 3 skipped**. The skipped cases are opt-in live Photon smoke tests requiring a separate staging app. This includes the corrected tests and the rendered overlap regression.
- [Source verification](source-verification.json): all five modified production files match the final tested QA source by SHA256. AI assets remain disabled (`enabledInEditor=0`, `autoPlayYourSeat=0`, legacy `mode=0`).
- [Android-defined compilation/type verification](player-code-verification.json): C# compiler exit 0; production browser and bracket types are present in PE TypeDefinitions, inspected practice/bot types are absent. This verifies compilation, not a newly signed Android build.
- [Unavailable-service screen](screens/player-tournaments-unavailable.png), [loading](screens/player-tournaments-loading.png) and [empty list](screens/player-tournaments-empty.png): actual production components, rendered at 480×800.
- [4-player bracket format](screens/player-bracket-format-4.png) and [8-player bracket format](screens/player-bracket-format-8.png): production renderer, empty unassigned slots, no backend fixture records or AI. Visually inspected with no clipping/overlap observed.

Intermediate reports are retained for traceability. Attempt 1 exposed an ambiguous test lookup for `Status` (both header and match cards use that name); the assertion now selects the header explicitly. Attempt 2 showed the test's direct empty-scene Play Mode entry retained the normal transition cover; the test now completes the transition before exercising waiting-screen/bracket layering. The production transition implementation was not changed to make the test pass.
