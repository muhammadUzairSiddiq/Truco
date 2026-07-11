using System;
using System.Collections.Generic;

/// <summary>Pure 1v1 lobby rules (create/join/reconcile) — covered by EditMode tests.</summary>
public static class OneVsOneLobbyFlowRules
{
    /// <summary>Do not cancel an in-progress pre-game table because GET /matches hid a full row.</summary>
    public static bool ShouldPreserveActivePreGameSession(bool hasCurrentMatchId, bool gameStarted) =>
        hasCurrentMatchId && !gameStarted;

    /// <summary>Photon join should retry when the host has not finished CreateRoom yet.</summary>
    public static bool ShouldRetryPhotonJoin(short returnCode, string message, int attempt, int maxAttempts)
    {
        if (attempt >= maxAttempts) return false;
        if (returnCode == 32758) return true; // GameDoesNotExist
        if (string.IsNullOrEmpty(message)) return false;
        return message.IndexOf("not exist", StringComparison.OrdinalIgnoreCase) >= 0
               || message.IndexOf("does not exist", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static string ResolveUserId(User u)
    {
        if (u == null) return null;
        if (!string.IsNullOrEmpty(u._id)) return u._id;
        if (!string.IsNullOrEmpty(u.id)) return u.id;
        return null;
    }

    /// <summary>Every lobby row this user hosts (by createdBy, hostId, or sole player).</summary>
    public static List<string> GetAllHostedMatchIds(List<Player1v1Match> list, string currentUserId)
    {
        var ids = new List<string>();
        if (list == null || string.IsNullOrEmpty(currentUserId)) return ids;
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null || string.IsNullOrEmpty(m._id) || !m.IsLobbyLikeStatus()) continue;
            if (IsHostOfRoom(m, currentUserId)) ids.Add(m._id);
        }
        return ids;
    }

    /// <summary>User is registered in players[], or is the recorded host/creator.</summary>
    public static bool MatchBelongsToUser(Player1v1Match m, string userId)
    {
        if (m == null || string.IsNullOrEmpty(userId)) return false;
        if (m.players != null)
        {
            for (int i = 0; i < m.players.Length; i++)
            {
                var p = m.players[i];
                if (p != null && ResolveUserId(p) == userId) return true;
            }
        }
        if (!string.IsNullOrEmpty(m.createdBy) && m.createdBy == userId) return true;
        if (!string.IsNullOrEmpty(m.hostId) && m.hostId == userId) return true;
        return false;
    }

    /// <summary>When user hosts 2+ lobby rooms, return ids to cancel (keep session match or first).</summary>
    public static List<string> GetExtraHostedMatchIds(
        List<Player1v1Match> list,
        string sessionMatchId,
        string currentUserId)
    {
        var extras = new List<string>();
        if (list == null || string.IsNullOrEmpty(currentUserId)) return extras;

        var hosted = new List<Player1v1Match>();
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null || !m.IsLobbyLikeStatus()) continue;
            if (!IsHostOfRoom(m, currentUserId)) continue;
            hosted.Add(m);
        }
        if (hosted.Count <= 1) return extras;

        Player1v1Match keep = null;
        if (!string.IsNullOrEmpty(sessionMatchId))
        {
            for (int i = 0; i < hosted.Count; i++)
                if (hosted[i]._id == sessionMatchId) { keep = hosted[i]; break; }
        }
        if (keep == null) keep = hosted[0];

        for (int i = 0; i < hosted.Count; i++)
        {
            if (hosted[i]._id == keep._id) continue;
            extras.Add(hosted[i]._id);
        }
        return extras;
    }

    static bool IsHostOfRoom(Player1v1Match m, string userId)
    {
        if (m == null || string.IsNullOrEmpty(userId)) return false;
        string host = m.GetHostUserId();
        if (!string.IsNullOrEmpty(host) && host == userId) return true;
        if (m.players != null && m.players.Length == 1 && ResolveUserId(m.players[0]) == userId)
            return true;
        return false;
    }

    /// <summary>Both players seated in Photon — ready to load Gameplay.</summary>
    public static bool IsReadyToLaunchGameplay(int playerCount, int requiredPlayers) =>
        playerCount >= requiredPlayers && requiredPlayers > 0;

    /// <summary>Guest already charged on backend — must not POST /join again.</summary>
    public static bool IsAlreadyRegisteredGuest(bool isParticipant, bool isHost) =>
        isParticipant && !isHost;

    public static bool IsMatchFullApiError(string message)
    {
        if (string.IsNullOrEmpty(message)) return false;
        return message.IndexOf("full", StringComparison.OrdinalIgnoreCase) >= 0
               || message.IndexOf("llena", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool IsWrongPasswordApiError(string message)
    {
        if (string.IsNullOrEmpty(message)) return false;
        return message.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0
               || message.IndexOf("contrase", StringComparison.OrdinalIgnoreCase) >= 0
               || message.IndexOf("codigo", StringComparison.OrdinalIgnoreCase) >= 0
               || message.IndexOf("código", StringComparison.OrdinalIgnoreCase) >= 0
               || message.IndexOf("invalid", StringComparison.OrdinalIgnoreCase) >= 0
               || message.IndexOf("incorrect", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>Guest paid on backend but Photon not connected yet — show resume row.</summary>
    public static bool ShouldShowResumeInLobbyList(bool isLobbyLike, bool isParticipant, bool isHost, bool alreadyInPhotonRoom) =>
        isLobbyLike && isParticipant && !isHost && !alreadyInPhotonRoom;
}
