using System;

/// <summary>Maps raw API/Photon errors to short player-facing copy (no stack/server jargon).</summary>
public static class TrucoUserFacingErrors
{
    /// <summary>Create-room failures — never shows "Contraseña incorrecta".</summary>
    public static string ForCreateRoom(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return TrucoTextosClient.ErrorCrearSala;
        string m = raw.ToLowerInvariant();

        if (m.Contains("participant") || m.Contains("unauthorized") || m.Contains("forbidden")
            || m.Contains("game secret") || m.Contains("x-game-secret") || m.Contains("cheat"))
            return TrucoTextosClient.ErrorCrearSala;

        if (OneVsOneLobbyFlowRules.IsSessionExpiredApiError(raw))
            return TrucoTextosClient.SesionExpirada;

        if (OneVsOneLobbyFlowRules.IsAlreadyHasRoomApiError(raw)
            || m.Contains("ya tienes") || m.Contains("ya tenés"))
            return TrucoTextosClient.YaTienesSala;

        if (OneVsOneLobbyFlowRules.IsRoomAlreadyExistsApiError(raw))
            return TrucoTextosClient.SalaYaExiste;

        if (m.Contains("network") || m.Contains("timeout") || m.Contains("timed out")
            || m.Contains("connection") || m.Contains("unreachable") || m.Contains("dns"))
            return TrucoTextosClient.ErrorConexion;

        if (m.Contains("saldo") || m.Contains("balance") || m.Contains("insufficient"))
            return TrucoTextosClient.SaldoInsuficiente;

        if (m.Contains("http") || m.Contains("status") || m.Contains("exception")
            || m.Contains("nullreference") || m.Contains("stack") || m.Contains("at ")
            || m.Contains("400") || m.Contains("401") || m.Contains("403") || m.Contains("500")
            || m.Contains("invalid") || m.Contains("incorrect"))
            return TrucoTextosClient.ErrorCrearSala;

        if (raw.Length <= 80 && raw.IndexOf(':') < 0 && raw.IndexOf('{') < 0)
            return raw;

        return TrucoTextosClient.ErrorCrearSala;
    }

    /// <summary>Join-room failures — password mapping only when the API actually refers to a room password.</summary>
    public static string ForJoinRoom(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return TrucoTextosClient.ErrorUnirse;
        string m = raw.ToLowerInvariant();

        if (m.Contains("participant") || m.Contains("unauthorized") || m.Contains("forbidden")
            || m.Contains("game secret") || m.Contains("x-game-secret") || m.Contains("cheat"))
            return TrucoTextosClient.ErrorGenerico;

        if (OneVsOneLobbyFlowRules.IsWrongPasswordApiError(raw))
            return TrucoTextosClient.ContrasenaIncorrecta;
        if (OneVsOneLobbyFlowRules.IsMatchFullApiError(raw))
            return TrucoTextosClient.SalaLlena;
        if (OneVsOneLobbyFlowRules.IsSessionExpiredApiError(raw))
            return TrucoTextosClient.SesionExpirada;

        if (m.Contains("network") || m.Contains("timeout") || m.Contains("timed out")
            || m.Contains("connection") || m.Contains("unreachable") || m.Contains("dns"))
            return TrucoTextosClient.ErrorConexion;

        if (m.Contains("saldo") || m.Contains("balance") || m.Contains("insufficient"))
            return TrucoTextosClient.SaldoInsuficiente;

        if (m.Contains("http") || m.Contains("status") || m.Contains("exception")
            || m.Contains("nullreference") || m.Contains("stack") || m.Contains("at ")
            || m.Contains("400") || m.Contains("401") || m.Contains("403") || m.Contains("500"))
            return TrucoTextosClient.ErrorUnirse;

        if (raw.Length <= 80 && raw.IndexOf(':') < 0 && raw.IndexOf('{') < 0)
            return raw;

        return TrucoTextosClient.ErrorUnirse;
    }

    public static string ForApiOrPhoton(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return TrucoTextosClient.ErrorGenerico;
        string m = raw.ToLowerInvariant();

        // Auth / participant failures are not "missing prize" — never imply the player won.
        if (m.Contains("participant") || m.Contains("unauthorized") || m.Contains("forbidden")
            || m.Contains("game secret") || m.Contains("x-game-secret") || m.Contains("cheat"))
            return TrucoTextosClient.ErrorGenerico;

        if (OneVsOneLobbyFlowRules.IsSessionExpiredApiError(raw))
            return TrucoTextosClient.SesionExpirada;

        // Password only when the message clearly refers to a room password/code — never bare "invalid".
        if (OneVsOneLobbyFlowRules.IsWrongPasswordApiError(raw))
            return TrucoTextosClient.ContrasenaIncorrecta;
        if (OneVsOneLobbyFlowRules.IsMatchFullApiError(raw))
            return TrucoTextosClient.SalaLlena;
        if (OneVsOneLobbyFlowRules.IsAlreadyHasRoomApiError(raw))
            return TrucoTextosClient.YaTienesSala;
        if (OneVsOneLobbyFlowRules.IsRoomAlreadyExistsApiError(raw))
            return TrucoTextosClient.SalaYaExiste;

        if (m.Contains("network") || m.Contains("timeout") || m.Contains("timed out")
            || m.Contains("connection") || m.Contains("unreachable") || m.Contains("dns"))
            return TrucoTextosClient.ErrorConexion;

        if (m.Contains("saldo") || m.Contains("balance") || m.Contains("insufficient"))
            return TrucoTextosClient.SaldoInsuficiente;

        // Never surface raw HTTP / Unauthorized / stack traces to players.
        if (m.Contains("http") || m.Contains("status") || m.Contains("exception")
            || m.Contains("nullreference") || m.Contains("stack") || m.Contains("at ")
            || m.Contains("400") || m.Contains("401") || m.Contains("403") || m.Contains("500"))
            return TrucoTextosClient.ErrorGenerico;

        // Short clean messages already localized can pass through.
        if (raw.Length <= 80 && raw.IndexOf(':') < 0 && raw.IndexOf('{') < 0)
            return raw;

        return TrucoTextosClient.ErrorGenerico;
    }

    /// <summary>Logs settlement failures only — never returns player-facing prize/support copy.</summary>
    public static string ForPrizeSettlementFailure(string rawServerDetail)
    {
        TrucoDebugLog.Warn(TrucoDebugLog.Category.Api,
            "Prize settlement failed (no player toast): " + (rawServerDetail ?? "?"));
        return null;
    }
}
