public static class ApiConfig
{
    public static string BaseUrl = "https://srv983121.hstgr.cloud/api";

    // ───────────────────────── Central SO overrides ─────────────────────────
    // All backend routes can be changed/added from Resources/TrucoApiEndpoints (TrucoApiEndpointsSO)
    // WITHOUT touching this file. When the backend dev provides the match-end route, just add a row
    // key=MatchEnd, path=/matches/{0}/end in that asset.

    static System.Collections.Generic.Dictionary<string, string> _overrides;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoLoadEndpointsFromResources()
    {
        var so = UnityEngine.Resources.Load<TrucoApiEndpointsSO>("TrucoApiEndpoints");
        if (so != null) { ApplyEndpoints(so); TrucoRemoteApiConfiguration.Initialize(so); }
    }

    /// <summary>Apply base URL + endpoint overrides from the central ScriptableObject.</summary>
    public static void ApplyEndpoints(TrucoApiEndpointsSO so)
    {
        if (so == null) return;
        if (!string.IsNullOrEmpty(so.baseUrl)) BaseUrl = so.baseUrl.TrimEnd('/');
        _overrides = new System.Collections.Generic.Dictionary<string, string>();
        if (so.endpoints != null)
            foreach (var e in so.endpoints)
                if (e != null && !string.IsNullOrEmpty(e.key) && !string.IsNullOrEmpty(e.pathTemplate))
                    _overrides[e.key] = e.pathTemplate;
        TrucoDebugLog.Log(TrucoDebugLog.Category.Settings,
            "Endpoints loaded from SO. Base=" + BaseUrl + ", overrides=" + _overrides.Count);
    }

    public static void ApplyRemoteEndpoints(string baseUrl, System.Collections.Generic.IEnumerable<TrucoApiEndpointsSO.Endpoint> endpoints)
    {
        var next = _overrides == null ? new System.Collections.Generic.Dictionary<string, string>()
            : new System.Collections.Generic.Dictionary<string, string>(_overrides);
        foreach (var endpoint in endpoints) next[endpoint.key] = endpoint.pathTemplate;
        BaseUrl = baseUrl.TrimEnd('/');
        _overrides = next;
    }

    static string PathOf(string key, string defaultPath)
        => _overrides != null && _overrides.TryGetValue(key, out var t) && !string.IsNullOrEmpty(t) ? t : defaultPath;

    static string Url(string key, string defaultPath) => BaseUrl + PathOf(key, defaultPath);
    static string Url(string key, string defaultPath, string id) => BaseUrl + string.Format(PathOf(key, defaultPath), System.Uri.EscapeDataString(id ?? ""));

    /// <summary>Resolve a brand-new endpoint defined only in the SO (no code member). Returns null if missing.</summary>
    public static string Custom(string key, params object[] args)
    {
        if (_overrides != null && _overrides.TryGetValue(key, out var t) && !string.IsNullOrEmpty(t))
            return BaseUrl + (args != null && args.Length > 0 ? string.Format(t, args) : t);
        return null;
    }

    #region Auth

    public static string Login => Url("Login", "/auth/login");
    public static string Register => Url("Register", "/auth/register");
    public static string Logout => Url("Logout", "/auth/logout");
    public static string GetCurrentUserProfile => Url("GetCurrentUserProfile", "/auth/me");
    public static string CheckAdmin => Url("CheckAdmin", "/auth/check-admin");
    public static string RefreshToken => Url("RefreshToken", "/auth/refresh");
    public static string ForgotPassword => Url("ForgotPassword", "/auth/forgot-password");
    public static string ResendVerification => Url("ResendVerification", "/auth/resend-verification");
    public static string ResendPasswordReset => Url("ResendPasswordReset", "/auth/resend-password-reset");
    public static string ResetPassword => Url("ResetPassword", "/auth/reset-password");
    public static string VerifyEmail => Url("VerifyEmail", "/auth/verify-email");

    #endregion

    #region Profile

    public static string GetUserProfile => Url("GetUserProfile", "/profile/profile");
    public static string UpdateAvatar => Url("UpdateAvatar", "/profile/avatar");
    public static string ChangePassword => Url("ChangePassword", "/profile/password");

    #endregion

    #region Tournaments
    // Torneos: creación reservada al panel de administración (no hay flujo de jugador que cree torneos en esta app).

    public static string ListTournaments => Url("ListTournaments", "/tournaments");
    public static string CreateTournament => Url("CreateTournament", "/tournaments");
    public static string GetTournament(string id) => Url("GetTournament", "/tournaments/{0}", id);
    public static string EnterTournament(string id) => Url("EnterTournament", "/tournaments/{0}/join", id);
    public static string ValidatePrivateTournament(string id) => Url("ValidatePrivateTournament", "/tournaments/{0}/validate-password", id);
    public static string GetTournamentPlayers(string id) => Url("GetTournamentPlayers", "/tournaments/{0}/players", id);
    public static string FinalizeTournament(string id) => Url("FinalizeTournament", "/tournaments/{0}/finalize", id);
    public static string FinalizeMatch(string id) => Url("FinalizeMatch", "/tournaments/{0}/finalize-match", id);
    public static string CreateTournamentMatch(string id) => Url("CreateTournamentMatch", "/tournaments/{0}/create-match", id);
    public static string UpdateAwardPercentage(string id) => Url("UpdateAwardPercentage", "/tournaments/{0}/update-award-percentage", id);

    #endregion

    #region Matches
    // 1v1: resultados y saldos normales = autoridad del backend; el cliente solo consume API (join, códigos, etc.).

    public static string ListMatches => Url("MatchList", "/matches");
    public static string CreateMatchGlobal => Url("CreateMatchGlobal", "/matches"); // same endpoint but global
    public static string GetMatch(string id) => Url("GetMatch", "/matches/{0}", id);
    public static string GetMyMatches => Url("GetMyMatches", "/matches/player/my-matches");

    /// <summary>Player creates a 1v1 room: balance check + entry fee + match row (see Swagger).</summary>
    public static string PlayerCreateMatch => Url("PlayerCreateMatch", "/matches/player-create");

    public static string PlayerJoinMatch(string id) => Url("PlayerJoinMatch", "/matches/{0}/join", id);

    /// <summary>After Photon room is created, register name so admin panel can see it.</summary>
    public static string MatchRegisterPhotonRoom(string id) => Url("MatchRegisterPhotonRoom", "/matches/{0}/photon-room", id);

    /// <summary>POST body typically { "winnerId": "…" } — requires match token + replay headers.</summary>
    public static string MatchSubmitResult(string id) => Url("MatchSubmitResult", "/matches/{0}/result", id);

    /// <summary>Short-lived token required before POST /result or /walkover (anti-cheat).</summary>
    public static string MatchRequestToken(string id) => Url("MatchRequestToken", "/matches/{0}/match-token", id);

    /// <summary>Claim win when opponent abandons: body { "claimerId": "…" }. Requires x-game-secret.</summary>
    public static string MatchWalkover(string id) => Url("MatchWalkover", "/matches/{0}/walkover", id);

    /// <summary>
    /// Player leaves match lobby / unregisters from active <c>players</c> (implement on server).
    /// Called when exiting gameplay so stale "full" rows clear; safe if match already completed (no-op).
    /// </summary>
    public static string MatchPlayerLeave(string id) => Url("MatchPlayerLeave", "/matches/{0}/leave", id);

    /// <summary>
    /// Idempotent financial settlement for a paid room that never started.  Unlike <see cref="MatchPlayerLeave"/>,
    /// this route must work from a retained settlement tombstone after lobby cleanup and must return success only
    /// after the original entry is credited exactly once.
    /// </summary>
    public static string MatchCancelLobbyRefund(string id) =>
        Url("MatchCancelLobbyRefund", "/matches/{0}/cancel-lobby-refund", id);

    /// <summary>
    /// End-of-match route. Default guesses <c>/matches/{id}/end</c>; override the exact path in the
    /// TrucoApiEndpoints asset (key=MatchEnd) when the backend dev provides it — no code change needed.
    /// </summary>
    public static string MatchEnd(string id) => Url("MatchEnd", "/matches/{0}/end", id);

    /// <summary>Initialize backend game state when both players enter gameplay (master client).</summary>
    public static string MatchStartGame(string id) => Url("MatchStartGame", "/matches/{0}/start-game", id);

    #endregion

    #region Alerts

    public static string CreateAlert => Url("CreateAlert", "/alerts/create");
    public static string ListAlerts => Url("ListAlerts", "/alerts");
    public static string GetAlert(string id) => Url("GetAlert", "/alerts/{0}", id);
    public static string AcknowledgeAlert(string id) => Url("AcknowledgeAlert", "/alerts/{0}", id);
    public static string ResolveAlert(string id) => Url("ResolveAlert", "/alerts/{0}/resolve", id);
    public static string DismissAlert(string id) => Url("DismissAlert", "/alerts/{0}/dismiss", id);
    public static string AlertsSummary => Url("AlertsSummary", "/alerts/stats/summary");
    public static string BulkAcknowledgeAlerts => Url("BulkAcknowledgeAlerts", "/alerts/bulk/acknowledge");

    #endregion

    #region Dashboard (admin / live)

    public static string DashboardRealtimeActiveMatches => Url("DashboardRealtimeActiveMatches", "/dashboard/realtime/active-matches");
    public static string DashboardRealtimeFraudAlerts => Url("DashboardRealtimeFraudAlerts", "/dashboard/realtime/fraud-alerts");
    public static string DashboardRealtimeOnlinePlayers => Url("DashboardOnlinePlayers", "/dashboard/realtime/online-players");
    public static string DashboardRealtimeActiveTournaments => Url("DashboardActiveTournaments", "/dashboard/realtime/active-tournaments");
    public static string DashboardRealtimeReconnectAlerts => Url("DashboardReconnectAlerts", "/dashboard/realtime/reconnect-alerts");

    #endregion

    #region Admin

    public static string AdminLogin => Url("AdminLogin", "/admin/login");
    public static string AdminLogout => Url("AdminLogout", "/admin/logout");
    public static string ListUsers => Url("ListUsers", "/admin/users");
    public static string GetUserStats(string id) => Url("GetUserStats", "/admin/users/{0}/stats", id);
    public static string AddUserCoins(string id) => Url("AddUserCoins", "/admin/users/{0}/coins", id);
    public static string AdminListTournaments => Url("AdminListTournaments", "/admin/tournaments");
    public static string AdminGetTournament(string id) => Url("AdminGetTournament", "/admin/tournaments/{0}", id);
    public static string AdminListMatches => Url("AdminListMatches", "/admin/matches");
    public static string AdminGetMatch(string id) => Url("AdminGetMatch", "/admin/matches/{0}", id);

    public static string AdminForceCloseMatch(string id) => Url("AdminForceCloseMatch", "/matches/{0}/force-close", id);
    public static string AdminForceCloseTournament(string id) => Url("AdminForceCloseTournament", "/admin/tournaments/{0}/force-close", id);
    public static string AdminSetMaintenance => Url("AdminSetMaintenance", "/admin/system/maintenance");
    public static string ListTransactions => Url("ListTransactions", "/admin/transactions");
    public static string GetSystemStatus => Url("GetSystemStatus", "/admin/system/status");
    public static string GetAlertsDashboard => Url("GetAlertsDashboard", "/admin/alerts/dashboard");
    public static string AdminListAlerts => Url("AdminListAlerts", "/admin/alerts");
    public static string AdminGetAlert(string id) => Url("AdminGetAlert", "/admin/alerts/{0}", id);
    public static string AdminAcknowledgeAlert(string id) => Url("AdminAcknowledgeAlert", "/admin/alerts/{0}/acknowledge", id);
    public static string AdminResolveAlert(string id) => Url("AdminResolveAlert", "/admin/alerts/{0}/resolve", id);
    public static string AdminDismissAlert(string id) => Url("AdminDismissAlert", "/admin/alerts/{0}/dismiss", id);
    public static string AdminBulkAcknowledgeAlerts => Url("AdminBulkAcknowledgeAlerts", "/admin/alerts/bulk/acknowledge");
    public static string AdminAlertsSummary => Url("AdminAlertsSummary", "/admin/alerts/stats/summary");

    #endregion
}
