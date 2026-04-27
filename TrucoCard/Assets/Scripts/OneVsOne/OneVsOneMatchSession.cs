using System.Collections.Generic;
using UnityEngine;

/// <summary>Estado mínimo de la partida 1v1 frente a backend y Photon (para panel admin / correlación id).</summary>
public static class OneVsOneMatchSession
{
    public static string CurrentMatchId { get; private set; }
    public static string PhotonRoomName { get; private set; }
    public static bool IsHost { get; private set; }
    public static int EntryFee { get; private set; }
    /// <summary>Opponent’s backend user id (from Photon custom properties), cached while in-room for forfeit on reconnect failure.</summary>
    public static string CachedOpponentUserId { get; private set; }
    public const int MaxPlayersGameplay = 2;
    /// <summary>1v1 real: dos jugadores (sin hueco “reservado” extra).</summary>
    public const int MaxPlayersPhoton = 2;

    public static void SetHostContext(string matchId, string photonName, int entryFee)
    {
        CurrentMatchId = matchId;
        PhotonRoomName = photonName;
        IsHost = true;
        EntryFee = entryFee;
        Debug.Log($"[OneVsOne] Contexto anfitrión: match={matchId} room={photonName} entrada={entryFee}");
    }

    public static void SetGuestContext(string matchId, string photonName, int entryFee)
    {
        CurrentMatchId = matchId;
        PhotonRoomName = photonName;
        IsHost = false;
        EntryFee = entryFee;
        Debug.Log($"[OneVsOne] Contexto invitado: match={matchId} room={photonName}");
    }

    public static void Clear()
    {
        CurrentMatchId = null;
        PhotonRoomName = null;
        IsHost = false;
        EntryFee = 0;
        CachedOpponentUserId = null;
        TrucoRoomPersistence.Clear();
    }

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
    public const int MinPasswordLength = 6;
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
