using System;

/// <summary>Maps raw API/Photon errors to short player-facing copy (no stack/server jargon).</summary>
public static class TrucoUserFacingErrors
{
    public static string ForApiOrPhoton(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return TrucoTextosClient.ErrorGenerico;
        string m = raw.ToLowerInvariant();

        if (m.Contains("participant") || m.Contains("unauthorized") || m.Contains("forbidden")
            || m.Contains("game secret") || m.Contains("x-game-secret") || m.Contains("cheat"))
            return TrucoTextosClient.ErrorPremioNoConfirmado;

        if (OneVsOneLobbyFlowRules.IsWrongPasswordApiError(raw))
            return TrucoTextosClient.ContrasenaIncorrecta;
        if (OneVsOneLobbyFlowRules.IsMatchFullApiError(raw))
            return TrucoTextosClient.SalaLlena;

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

    public static string ForPrizeSettlementFailure(string rawServerDetail)
    {
        TrucoDebugLog.Warn(TrucoDebugLog.Category.Api,
            "Prize settlement failed (hidden from UI): " + (rawServerDetail ?? "?"));
        return TrucoTextosClient.ErrorPremioNoConfirmado;
    }
}
