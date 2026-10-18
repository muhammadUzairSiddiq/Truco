# Version 1.1.2 feedback — next-build checklist

Client changes prepared on 2026-10-18. Completion below distinguishes implementation from live acceptance.

- [x] Tournament first: serialize opening-table creation, accept explicitly successful wrapped match responses, and include the failed table and server error in startup failures.
- [ ] Verify the reported tournament failure against the backend error/source and a real full tournament. The Unity workspace does not contain the backend. Serializing requests avoids competing writes but does not establish the cause of the reported server failure.
- [ ] Live tournament acceptance: opening matches, quarterfinals, semifinals, final, champion, one prize payment, and walkover. Existing bracket tests cover deterministic advancement only.
- [x] Room settings: read actual Flor/target settings from the host session and Photon lobby properties; retain selected settings in the create response model.
- [x] Compact room cards: reduce height from 380 to 280 UI units and reduce internal text spacing while retaining the password/action lane.
- [x] Delete Room: explicitly close the backend room and verify it is no longer an open lobby. Keep unconfirmed refunds in the recovery ledger; report refund-pending separately from deletion failure. Accept refund receipts from the legacy leave endpoint. Ignore repeated delete clicks during a request.
- [x] Envido: lock local card play immediately on Envido, Real Envido and Falta Envido; reject stale turn updates while a challenge remains pending.
- [x] Son buenas: both seats derive the same second declaration from the resolved winner, using the shared text/Android speech path.
- [x] Unity script compilation completed successfully in the isolated QA copy (existing warnings, no compiler errors).
- [ ] Unity automated regression suite passes for these changes. Batch runs stalled during editor initialization before producing test results, including a retry after preserving/rebuilding the Bee cache. Added regression cases remain unverified.
- [ ] Two-device acceptance: room listing/layout, deletion/refund, all Envido responses and Android speech.

No release APK, backend deployment, or live financial test is included in these checks.

Compilation log: `D:/TrucoQA/20260923-ClientA/Build112All.log`.
Final test-start attempt log: `D:/TrucoQA/20260923-ClientA/Build112Verified.log`.
