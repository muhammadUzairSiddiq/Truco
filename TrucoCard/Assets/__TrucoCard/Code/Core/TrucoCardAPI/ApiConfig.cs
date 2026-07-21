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
        if (so != null) ApplyEndpoints(so);
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

    static string PathOf(string key, string defaultPath)
        => _overrides != null && _overrides.TryGetValue(key, out var t) && !string.IsNullOrEmpty(t) ? t : defaultPath;

    static string Url(string key, string defaultPath) => BaseUrl + PathOf(key, defaultPath);
    static string Url(string key, string defaultPath, string id) => BaseUrl + string.Format(PathOf(key, defaultPath), id);

    /// <summary>Resolve a brand-new endpoint defined only in the SO (no code member). Returns null if missing.</summary>
    public static string Custom(string key, params object[] args)
    {
        if (_overrides != null && _overrides.TryGetValue(key, out var t) && !string.IsNullOrEmpty(t))
            return BaseUrl + (args != null && args.Length > 0 ? string.Format(t, args) : t);
        return null;
    }

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

    public static string ListTournaments => Url("ListTournaments", "/tournaments");
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
    public static string CreateMatchGlobal => BaseUrl + "/matches"; // same endpoint but global
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
    /// End-of-match route. Default guesses <c>/matches/{id}/end</c>; override the exact path in the
    /// TrucoApiEndpoints asset (key=MatchEnd) when the backend dev provides it — no code change needed.
    /// </summary>
    public static string MatchEnd(string id) => Url("MatchEnd", "/matches/{0}/end", id);

    /// <summary>Initialize backend game state when both players enter gameplay (master client).</summary>
    public static string MatchStartGame(string id) => Url("MatchStartGame", "/matches/{0}/start-game", id);

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
