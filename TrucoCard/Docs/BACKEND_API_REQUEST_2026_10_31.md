# Current backend handoff — 31 October 2026

The owner requested exact proposed APIs because the separate backend developer has not supplied these contracts. The Unity client is now integrated with that proposal; this is not a claim of backend deployment.

Use [the current handoff](API/BACKEND_HANDOFF_V1.md), [OpenAPI contract](API/trucopy-v1.openapi.json), [complete endpoint catalog](API/ENDPOINTS.md), [wallet/admin rules](API/WALLET_AND_ADMIN_V1.md), and [tournament/Photon rules](API/TOURNAMENT_AND_PHOTON_V1.md).

All HTTP route keys are configured in `Assets/Resources/TrucoApiEndpoints.asset` (`TrucoApiEndpointsSO`). The runtime bootstrap supports compatible path changes within bundled trusted HTTPS origins without rebuilding. The exact public payload is [client-config.example.json](API/client-config.example.json); the searchable SO inspector can export it. Editing an SO alone does not update an installed APK.

See [the release audit](QA/ReleaseAudit-2026-10-31/README.md) and [all 50 feature requirements](QA/ReleaseAudit-2026-10-31/requirements.md). Backend financial/authorization/tournament deployment, Photon admission setup and signed Android acceptance remain release gates.
