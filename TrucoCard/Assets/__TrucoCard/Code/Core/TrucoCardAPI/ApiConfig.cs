public static class ApiConfig
{
    public static string BaseUrl = "https://srv983121.hstgr.cloud/api";

    #region Auth

    public static string Login => $"{BaseUrl}/auth/login";
    public static string Register => $"{BaseUrl}/auth/register";
    public static string Logout => $"{BaseUrl}/auth/logout";
    public static string GetCurrentUserProfile => $"{BaseUrl}/auth/me";
    public static string CheckAdmin => $"{BaseUrl}/auth/check-admin";
    public static string RefreshToken => $"{BaseUrl}/auth/refresh";
    public static string ForgotPassword => $"{BaseUrl}/auth/forgot-password";
    public static string ResendVerification => $"{BaseUrl}/auth/resend-verification";
    public static string ResendPasswordReset => $"{BaseUrl}/auth/resend-password-reset";
    public static string ResetPassword => $"{BaseUrl}/auth/reset-password";
    public static string VerifyEmail => $"{BaseUrl}/auth/verify-email";

    #endregion

    #region Profile

    public static string GetUserProfile => $"{BaseUrl}/profile/profile";
    public static string UpdateAvatar => $"{BaseUrl}/profile/avatar";
    public static string ChangePassword => $"{BaseUrl}/profile/password";

    #endregion

    #region Tournaments
    // Torneos: creación reservada al panel de administración (no hay flujo de jugador que cree torneos en esta app).

    public static string ListTournaments => BaseUrl + "/tournaments";
    public static string GetTournament(string id) => $"{BaseUrl}/tournaments/{id}";
    public static string EnterTournament(string id) => $"{BaseUrl}/tournaments/{id}/join";
    public static string ValidatePrivateTournament(string id) => $"{BaseUrl}/tournaments/{id}/validate-password";
    public static string GetTournamentPlayers(string id) => $"{BaseUrl}/tournaments/{id}/players";
    public static string FinalizeTournament(string id) => $"{BaseUrl}/tournaments/{id}/finalize";
    public static string FinalizeMatch(string id) => $"{BaseUrl}/tournaments/{id}/finalize-match";
    public static string CreateTournamentMatch(string id) => $"{BaseUrl}/tournaments/{id}/create-match";
    public static string UpdateAwardPercentage(string id) => $"{BaseUrl}/tournaments/{id}/update-award-percentage";

    #endregion

    #region Matches
    // 1v1: resultados y saldos normales = autoridad del backend; el cliente solo consume API (join, códigos, etc.).

    public static string ListMatches => BaseUrl + "/matches";
    public static string CreateMatchGlobal => BaseUrl + "/matches"; // same endpoint but global
    public static string GetMatch(string id) => $"{BaseUrl}/matches/{id}";
    public static string GetMyMatches => BaseUrl + "/matches/player/my-matches";

    /// <summary>Player creates a 1v1 room: balance check + entry fee + match row (see Swagger).</summary>
    public static string PlayerCreateMatch => BaseUrl + "/matches/player-create";

    public static string PlayerJoinMatch(string id) => $"{BaseUrl}/matches/{id}/join";

    /// <summary>After Photon room is created, register name so admin panel can see it.</summary>
    public static string MatchRegisterPhotonRoom(string id) => $"{BaseUrl}/matches/{id}/photon-room";

    /// <summary>POST body typically { "winnerId": "…" } — confirm in Swagger; may require admin or player role.</summary>
    public static string MatchSubmitResult(string id) => $"{BaseUrl}/matches/{id}/result";

    /// <summary>
    /// Player leaves match lobby / unregisters from active <c>players</c> (implement on server).
    /// Called when exiting gameplay so stale "full" rows clear; safe if match already completed (no-op).
    /// </summary>
    public static string MatchPlayerLeave(string id) => $"{BaseUrl}/matches/{id}/leave";

    #endregion

    #region Alerts

    public static string CreateAlert => BaseUrl + "/alerts/create";
    public static string ListAlerts => BaseUrl + "/alerts";
    public static string GetAlert(string id) => $"{BaseUrl}/alerts/{id}";
    public static string AcknowledgeAlert(string id) => $"{BaseUrl}/alerts/{id}";
    public static string ResolveAlert(string id) => $"{BaseUrl}/alerts/{id}/resolve";
    public static string DismissAlert(string id) => $"{BaseUrl}/alerts/{id}/dismiss";
    public static string AlertsSummary => BaseUrl + "/alerts/stats/summary";
    public static string BulkAcknowledgeAlerts => BaseUrl + "/alerts/bulk/acknowledge";

    #endregion

    #region Dashboard (admin / live)

    public static string DashboardRealtimeActiveMatches => $"{BaseUrl}/dashboard/realtime/active-matches";
    public static string DashboardRealtimeFraudAlerts => $"{BaseUrl}/dashboard/realtime/fraud-alerts";

    #endregion

    #region Admin

    public static string AdminLogin => $"{BaseUrl}/admin/login";
    public static string AdminLogout => $"{BaseUrl}/admin/logout";
    public static string ListUsers => $"{BaseUrl}/admin/users";
    public static string GetUserStats(string id) => $"{BaseUrl}/admin/users/{id}/stats";
    public static string AddUserCoins(string id) => $"{BaseUrl}/admin/users/{id}/coins";
    public static string AdminListTournaments => $"{BaseUrl}/admin/tournaments";
    public static string AdminGetTournament(string id) => $"{BaseUrl}/admin/tournaments/{id}";
    public static string AdminListMatches => $"{BaseUrl}/admin/matches";
    public static string AdminGetMatch(string id) => $"{BaseUrl}/admin/matches/{id}";

    public static string AdminForceCloseMatch(string id) => $"{BaseUrl}/matches/{id}/force-close";
    public static string ListTransactions => $"{BaseUrl}/admin/transactions";
    public static string GetSystemStatus => $"{BaseUrl}/admin/system/status";
    public static string GetAlertsDashboard => $"{BaseUrl}/admin/alerts/dashboard";
    public static string AdminListAlerts => $"{BaseUrl}/admin/alerts";
    public static string AdminGetAlert(string id) => $"{BaseUrl}/admin/alerts/{id}";
    public static string AdminAcknowledgeAlert(string id) => $"{BaseUrl}/admin/alerts/{id}/acknowledge";
    public static string AdminResolveAlert(string id) => $"{BaseUrl}/admin/alerts/{id}/resolve";
    public static string AdminDismissAlert(string id) => $"{BaseUrl}/admin/alerts/{id}/dismiss";
    public static string AdminBulkAcknowledgeAlerts => $"{BaseUrl}/admin/alerts/bulk/acknowledge";
    public static string AdminAlertsSummary => $"{BaseUrl}/admin/alerts/stats/summary";

    #endregion
}
