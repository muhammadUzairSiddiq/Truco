# Unity Project Context

<!-- unity-onboarding:generated:start -->
Last analyzed: 2026-07-26, commit `1e7f372`.

- Unity project root: `D:\UNITY PROJECTS\Truco\TrucoCard`
- Editor: Unity 6000.0.80f1.
- Rendering: Universal Render Pipeline 17.0.4.
- Input: Unity Input System 1.19.0; legacy input usage not fully audited.
- Multiplayer/backend: Photon PUN 2 room/RPC/event flow plus PlayFab and a custom Truco API.
- Game architecture: MonoBehaviour-centric, with static deterministic rule helpers and Photon master-authoritative scoring.
- Startup: `Assets/__TrucoCard/Scenes/InitScene.unity`; gameplay is handled by the Gameplay scene and `GameManager`.
- Tests: Unity Test Framework 1.6.0; first-party EditMode suites live under `Assets/Editor`.
- Tooling: local Unity Editor is available; no Unity MCP connector was detected.
- Important constraint: multiplayer correctness requires two peers and backend access; a single Editor test run cannot prove settlement or replication.
- Existing working-tree changes in generated/settings files predated this analysis and must be preserved.

Primary sources: `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`,
`ProjectSettings/EditorBuildSettings.asset`, `Assets/Scripts/GameManager.cs`,
`Assets/Scripts/UIMANAGER.cs`, `Assets/Scripts/OneVsOne`, and `Assets/Editor`.
<!-- unity-onboarding:generated:end -->
