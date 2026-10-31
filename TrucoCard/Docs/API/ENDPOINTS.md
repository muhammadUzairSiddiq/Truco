# Complete Unity endpoint catalog

92 logical keys. `{0}` in the SO becomes the URL-escaped `{id}` shown here. Multiple legacy aliases can resolve to one versioned route. The new tournament controller uses the `Tournament*` keys; legacy creation/finalize helpers are not a fallback.

| SO key | Method | Path | Client usage |
|---|---|---|---|
| AcknowledgeAlert | POST | `/alerts/{id}` | reserved legacy |
| AddUserCoins | POST | `/admin/users/{id}/coins` | runtime |
| AdminAcknowledgeAlert | POST | `/admin/alerts/{id}/acknowledge` | reserved legacy |
| AdminAlertsSummary | GET | `/admin/alerts/stats/summary` | reserved legacy |
| AdminAudit | GET | `/admin/audit` | runtime |
| AdminBulkAcknowledgeAlerts | POST | `/admin/alerts/bulk/acknowledge` | reserved legacy |
| AdminDismissAlert | POST | `/admin/alerts/{id}/dismiss` | reserved legacy |
| AdminForceCloseMatch | POST | `/matches/{id}/force-close` | runtime |
| AdminForceCloseTournament | POST | `/admin/tournaments/{id}/force-close` | runtime |
| AdminGetAlert | GET | `/admin/alerts/{id}` | reserved legacy |
| AdminGetMatch | GET | `/admin/matches/{id}` | reserved legacy |
| AdminGetTournament | GET | `/admin/tournaments/{id}` | reserved legacy |
| AdminListAlerts | GET | `/admin/alerts` | reserved legacy |
| AdminListMatches | GET | `/admin/matches` | runtime |
| AdminListTournaments | GET | `/admin/tournaments` | runtime |
| AdminLogin | POST | `/admin/login` | reserved legacy |
| AdminLogout | POST | `/admin/logout` | reserved legacy |
| AdminResolveAlert | POST | `/admin/alerts/{id}/resolve` | reserved legacy |
| AdminSetChampion | POST | `/admin/tournaments/{id}/champion` | runtime |
| AdminSetMaintenance | POST | `/admin/system/maintenance` | runtime |
| AdminWalletRequestApprove | POST | `/admin/wallet/requests/{id}/approve` | runtime |
| AdminWalletRequestDetail | GET | `/admin/wallet/requests/{id}` | runtime |
| AdminWalletRequestList | GET | `/admin/wallet/requests` | runtime |
| AdminWalletRequestPay | POST | `/admin/wallet/requests/{id}/pay` | runtime |
| AdminWalletRequestReject | POST | `/admin/wallet/requests/{id}/reject` | runtime |
| AlertsSummary | GET | `/alerts/stats/summary` | reserved legacy |
| BulkAcknowledgeAlerts | POST | `/alerts/bulk/acknowledge` | reserved legacy |
| ChangePassword | POST | `/profile/password` | reserved legacy |
| CheckAdmin | GET | `/auth/check-admin` | runtime |
| ClientConfiguration | GET | `/client/config` | runtime |
| CreateAlert | POST | `/alerts/create` | reserved legacy |
| CreateMatchGlobal | POST | `/matches` | reserved legacy |
| CreateTournament | POST | `/tournaments` | runtime |
| CreateTournamentMatch | POST | `/tournaments/{id}/create-match` | runtime |
| DashboardActiveTournaments | GET | `/dashboard/realtime/active-tournaments` | reserved legacy |
| DashboardOnlinePlayers | GET | `/dashboard/realtime/online-players` | runtime |
| DashboardRealtimeActiveMatches | GET | `/dashboard/realtime/active-matches` | runtime |
| DashboardRealtimeFraudAlerts | GET | `/dashboard/realtime/fraud-alerts` | runtime |
| DashboardReconnectAlerts | GET | `/dashboard/realtime/reconnect-alerts` | runtime |
| DismissAlert | POST | `/alerts/{id}/dismiss` | reserved legacy |
| EnterTournament | POST | `/tournaments/{id}/join` | runtime |
| FinalizeMatch | POST | `/tournaments/{id}/finalize-match` | runtime |
| FinalizeTournament | POST | `/tournaments/{id}/finalize` | reserved legacy |
| ForgotPassword | POST | `/auth/forgot-password` | runtime |
| GetAlert | GET | `/alerts/{id}` | reserved legacy |
| GetAlertsDashboard | GET | `/admin/alerts/dashboard` | reserved legacy |
| GetCurrentUserProfile | GET | `/auth/me` | runtime |
| GetMatch | GET | `/matches/{id}` | runtime |
| GetMyMatches | GET | `/matches/player/my-matches` | reserved legacy |
| GetSystemStatus | GET | `/system/status` | runtime |
| GetTournament | GET | `/tournaments/{id}` | reserved legacy |
| GetTournamentPlayers | GET | `/tournaments/{id}/players` | runtime |
| GetUserProfile | GET | `/profile/profile` | reserved legacy |
| GetUserStats | GET | `/admin/users/{id}/stats` | reserved legacy |
| ListAlerts | GET | `/alerts` | reserved legacy |
| ListTournaments | GET | `/tournaments` | runtime |
| ListTransactions | GET | `/admin/transactions` | reserved legacy |
| ListUsers | GET | `/admin/users` | reserved legacy |
| Login | POST | `/auth/login` | runtime |
| Logout | POST | `/auth/logout` | reserved legacy |
| MatchCancelLobbyRefund | POST | `/matches/{id}/cancel-lobby-refund` | runtime |
| MatchEnd | POST | `/matches/{id}/end` | runtime |
| MatchList | GET | `/matches` | runtime |
| MatchPlayerLeave | POST | `/matches/{id}/leave` | runtime |
| MatchRegisterPhotonRoom | POST | `/matches/{id}/photon-room` | runtime |
| MatchRequestToken | POST | `/matches/{id}/match-token` | runtime |
| MatchStartGame | POST | `/matches/{id}/start-game` | runtime |
| MatchSubmitResult | POST | `/matches/{id}/result` | runtime |
| MatchWalkover | POST | `/matches/{id}/walkover` | runtime |
| PhotonSession | POST | `/auth/photon-session` | runtime |
| PlayerCreateMatch | POST | `/matches/player-create` | runtime |
| PlayerJoinMatch | POST | `/matches/{id}/join` | runtime |
| RefreshToken | POST | `/auth/refresh` | runtime |
| Register | POST | `/auth/register` | runtime |
| ResendPasswordReset | POST | `/auth/resend-password-reset` | reserved legacy |
| ResendVerification | POST | `/auth/resend-verification` | runtime |
| ResetPassword | POST | `/auth/reset-password` | reserved legacy |
| ResolveAlert | POST | `/alerts/{id}/resolve` | reserved legacy |
| TournamentFinalize | POST | `/tournaments/{id}/finalize` | runtime |
| TournamentFinalizeMatch | POST | `/tournaments/{id}/finalize-match` | runtime |
| TournamentJoin | POST | `/tournaments/{id}/join` | runtime |
| TournamentLeave | POST | `/tournaments/{id}/leave` | runtime |
| TournamentReconcile | POST | `/tournaments/{id}/reconcile` | runtime |
| TournamentSnapshot | GET | `/tournaments/{id}` | runtime |
| TournamentStart | POST | `/tournaments/{id}/start` | runtime |
| UpdateAvatar | POST | `/profile/avatar` | reserved legacy |
| UpdateAwardPercentage | POST | `/tournaments/{id}/update-award-percentage` | reserved legacy |
| ValidatePrivateTournament | POST | `/tournaments/{id}/validate-password` | runtime |
| VerifyEmail | POST | `/auth/verify-email` | runtime |
| WalletCreateRequest | POST | `/wallet/requests` | runtime |
| WalletRequestStatus | GET | `/wallet/requests/{id}` | runtime |
| WalletSnapshot | GET | `/wallet/snapshot` | runtime |
