# Client implementation and verification handoff

Updated 31 October 2026. This report supersedes the earlier home replacement and development-player AI instructions. It does not mark every PDF requirement production-ready.

## Current player interface

The authored home, wood header, avatar, balance, logo and two action illustrations remain intact. Bottom navigation uses a solid brown background, equal slots, original icons, a larger selected icon and a cream selection state. Profile/avatar choices, region/language controls, notifications, room list/form, wallet and authentication/recovery screens use brown, cream, gold and green surfaces with button feedback.

Tournament selection now has a readable banner with separate trophy artwork, rounded cards, flat green actions and clear rules/entry/prize details. The bracket uses cream participant cards, green local-player markers and gold winners, fits settled viewport dimensions and retains scrolling. Waiting/loading/failure/cancellation and champion views share the theme. Live empty lists display an empty state; request failure clears loading and returns to the menu.

The invisible-overlay defect was caused by sorting orders above Unity's signed 16-bit range (for example practice 35000). `TrucoCanvasOrder` keeps all overlays below 32767, above navigation and below transitions. The same correction covers wallet, confirmation, bracket, champion, maintenance and reconnection screens.

## Editor tournament practice

Open **Truco > Editor Tournament Practice > Open AI Tournament**. This enters Play Mode and opens practice without a backend registration or balance. The home and bottom tournament actions also open practice when enabled.

Settings: `Assets/Editor/TrucoTournamentPractice/TournamentPracticeSettings.asset`.

- `enabledInEditor`: toggle the bypass. Disable it to use the normal live tournament route.
- `autoPlayYourSeat`: enable AI on every seat; otherwise play your cards against AI opponents.
- Choose Flor, 15/30 target, seed and action delay in the Inspector.

Practice plays four- and eight-player brackets through real local card actions, results, advancement and champion. It shares the rule helpers and bracket state with production; it is a local development harness, not a replacement for Photon/backend integration testing. It does not move Trucoins or prove purchase, prize or refund settlement.

All practice code and its ScriptableObject asset live under `Assets/Editor`. The older development settings asset was moved from Resources to `Assets/Editor/TrucoDevelopmentSettings.asset`; its bot, settings and hook are now guarded by `UNITY_EDITOR`, including development builds. The old AI player-build helper was removed. Non-Editor compilation and assembly inspection confirm that these AI types are absent.

## Evidence and remaining dependencies

See `Docs/QA/TournamentRepair-2026-10-31/README.md` for the current results and actual Unity screen gallery. Fixture balances, tournament listings and lifecycle examples are labelled Editor examples; they do not demonstrate live server transactions.

The earlier 144-test result and Photon 2/4/8-client transport/rejoin checks remain in `Docs/QA/UnityTests-2026-10-31.xml`. They are separate historical evidence, not full paid tournament acceptance. The current AI runs complete local four/eight-player tournaments. No production Android package or real-device acceptance is claimed.

The backend handoff remains `Docs/BACKEND_API_REQUEST_2026_10_31.md`: confirm wallet schemas/routes, admin review, authoritative tournament assignments, cancellation/refund reconciliation and prize payments. No mock production money was added. Existing user edits to build_info and ProjectSettings remain preserved.

## Generated artwork provenance

Mode: built-in image generation. Originals remain under `C:/Users/User/.codex/generated_images/01a1910a-7417-7de2-9d48-8e269ddda32c/`.

Asset: `Assets/Resources/TrucoUI/Club/MenuBackground.png`

Prompt: Use case: stylized-concept. Asset type: production background texture for the portrait Unity mobile card game TrucoPy. Create a refined full-bleed 2:3 portrait dark emerald felt playing-table background with dark walnut edges, extremely subtle warm brushed brass corner inlays, soft studio illumination and gentle vignette. The central 80 percent must be quiet, uniformly deep green, low contrast, clean negative space so live UI text and panels are readable. Elegant authentic South American card club craftsmanship, restrained realistic material detail, not bright casino neon. No words, letters, logos, cards, chips, objects, frames inside the center, interface elements or watermark. This is the reusable background used behind all menu screens, not a screenshot or concept mockup.

Asset: `Assets/Resources/TrucoUI/Club/TournamentHero.png`

Prompt: Use case: stylized-concept. Asset type: wide hero illustration for tournament menus in Unity mobile TrucoPy. Create a polished 3:2 landscape illustration of a beautifully crafted brushed gold trophy with subtle laurel engraving, three traditional Spanish playing cards fanned at its base, on rich emerald felt with a restrained dark walnut table rim. Trophy placed in the RIGHT half of the composition, LEFT half has dark clean negative space for live Spanish headings. Warm soft spotlight from above, exquisite realistic game-art materials, refined card club style, deep forest green shadows, champagne brass highlights, no gaudy neon or excessive sparkles. Edges fade into near-black emerald to blend with a dark green interface. No text, lettering, numbers, logos or watermark. This is a reusable actual game UI asset, not a UI screenshot.
