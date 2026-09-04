using System.Collections.Generic;
using UnityEngine;

/// <summary>Estado mínimo de la partida 1v1 frente a backend y Photon (para panel admin / correlación id).</summary>
public static class OneVsOneMatchSession
{
    public static string CurrentMatchId { get; private set; }
    public static string PhotonRoomName { get; private set; }
    public static bool IsHost { get; private set; }
    public static int EntryFee { get; private set; }
    /// <summary>Game variant for this table: true = Con Flor, false = Sin Flor. Backend stores it; gameplay reads it.</summary>
    public static bool WithFlor { get; private set; } = true;
    public static int TargetScore { get; private set; } = TrucoMatchRules.DefaultTargetScore;
    /// <summary>Opponent’s backend user id (from Photon custom properties), cached while in-room for forfeit on reconnect failure.</summary>
    public static string CachedOpponentUserId { get; private set; }
    /// <summary>True after POST /start-game (cards dealt). Lobby cancel must not refund after this.</summary>
    public static bool GameStarted { get; private set; }
    /// <summary>
    /// Cached wallet balance when the cards were first dealt (entry fee already charged), or -1 when unknown.
    /// Prize verification compares against this instead of a balance that may already include the prize.
    /// </summary>
    public static int WalletBalanceAtGameStart { get; private set; } = -1;
    public const int MaxPlayersGameplay = 2;
    /// <summary>1v1 real: dos jugadores (sin hueco “reservado” extra).</summary>
    public const int MaxPlayersPhoton = 2;

    public static void SetHostContext(string matchId, string photonName, int entryFee, bool withFlor = true,
        int targetScore = TrucoMatchRules.DefaultTargetScore)
    {
        CurrentMatchId = matchId;
        PhotonRoomName = photonName;
        IsHost = true;
        EntryFee = entryFee;
        WithFlor = withFlor;
        TargetScore = TrucoMatchRules.NormalizeTargetScore(targetScore);
        TrucoDebugLog.Log(TrucoDebugLog.Category.OneVsOne,
            "Host context: match=" + matchId + " room=" + photonName + " fee=" + entryFee + " flor=" + withFlor);
    }

    public static void SetGuestContext(string matchId, string photonName, int entryFee, bool withFlor = true,
        int targetScore = TrucoMatchRules.DefaultTargetScore)
    {
        CurrentMatchId = matchId;
        PhotonRoomName = photonName;
        IsHost = false;
        EntryFee = entryFee;
        WithFlor = withFlor;
        TargetScore = TrucoMatchRules.NormalizeTargetScore(targetScore);
        TrucoDebugLog.Log(TrucoDebugLog.Category.OneVsOne,
            "Guest context: match=" + matchId + " room=" + photonName + " flor=" + withFlor);
    }

    public static void SetWithFlor(bool withFlor) => WithFlor = withFlor;
    public static void SetTargetScore(int targetScore) =>
        TargetScore = TrucoMatchRules.NormalizeTargetScore(targetScore);

    public static void MarkGameStarted()
    {
        if (!GameStarted)
            WalletBalanceAtGameStart = ApiController.GetSessionUser?.Data?.wallet?.balance ?? -1;
        GameStarted = true;
        // Cards are dealt: the entry fee is consumed and must never be reclaimed as an abandoned room.
        TrucoPendingRefundStore.Forget(CurrentMatchId);
    }

    public static void Clear()
    {
        CurrentMatchId = null;
        PhotonRoomName = null;
        IsHost = false;
        EntryFee = 0;
        WithFlor = true;
        TargetScore = TrucoMatchRules.DefaultTargetScore;
        GameStarted = false;
        WalletBalanceAtGameStart = -1;
        CachedOpponentUserId = null;
    }

    public static void ClearSavedRoomPersistence() => TrucoRoomPersistence.Clear();

    public static void SetCachedOpponentUserId(string userId)
    {
        if (!string.IsNullOrEmpty(userId)) CachedOpponentUserId = userId;
    }

    public static string BuildDefaultPhotonRoomName(string matchId)
    {
        if (string.IsNullOrEmpty(matchId)) return "truco1v1_" + Random.Range(10000, 99999);
        return "tr1_" + matchId;
    }
}

/// <summary>Private room password (API field <c>password</c>): server requires min length (6+). Host cache for list display.</summary>
public static class OneVsOnePrivateRoomCode
{
    public const int MinPasswordLength = 4;
    public const int MaxPasswordLength = 48;

    static readonly Dictionary<string, string> MatchIdToCode = new Dictionary<string, string>();

    public static string GenerateNewCode() => UnityEngine.Random.Range(100000, 1000000).ToString();

    public static bool IsValidFormat(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        s = s.Trim();
        if (s.Length < MinPasswordLength || s.Length > MaxPasswordLength) return false;
        return true;
    }

    public static void RememberForMatch(string matchId, string code)
    {
        if (string.IsNullOrEmpty(matchId) || !IsValidFormat(code)) return;
        MatchIdToCode[matchId] = code.Trim();
    }

    public static string TryGetRememberedForMatch(string matchId)
    {
        if (string.IsNullOrEmpty(matchId)) return null;
        return MatchIdToCode.TryGetValue(matchId, out var c) ? c : null;
    }
}
