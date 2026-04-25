using System;
using UnityEngine;

public static class Player1v1MatchExtensions
{
    public static string ResolvePhotonRoomName(this Player1v1Match m)
    {
        if (m == null) return null;
        if (!string.IsNullOrEmpty(m.photonRoomName)) return m.photonRoomName;
        if (!string.IsNullOrEmpty(m.photonRoom)) return m.photonRoom;
        if (!string.IsNullOrEmpty(m.roomName) && m.roomName.Length > 2) return m.roomName;
        if (!string.IsNullOrEmpty(m._id)) return OneVsOneMatchSession.BuildDefaultPhotonRoomName(m._id);
        return null;
    }

    public static bool IsPrivate(this Player1v1Match m)
    {
        if (m == null) return false;
        if (!string.IsNullOrEmpty(m.type))
        {
            var t = m.type.ToLowerInvariant();
            if (t == "private") return true;
            if (t == "public") return false;
        }
        if (!string.IsNullOrEmpty(m.access))
        {
            string a = m.access.ToLowerInvariant();
            if (a == "public" || a == "0") return false;
            if (a == "private" || a == "1") return true;
        }
        return !m.isPublic;
    }

    public static string GetTypeLabel(this Player1v1Match m) =>
        m != null && m.IsPrivate() ? TrucoTextosClient.Privada : TrucoTextosClient.Publica;

    public static int GetEntryStake(this Player1v1Match m)
    {
        if (m == null) return 0;
        if (m.cost > 0) return m.cost;
        if (m.entryFee > 0) return m.entryFee;
        return 0;
    }

    public static int GetTrucoPlayerCount(this Player1v1Match m)
    {
        if (m == null) return 0;
        if (m.players != null && m.players.Length > 0) return Mathf.Min(m.players.Length, 2);
        if (m.currentPlayers > 0) return Mathf.Min(m.currentPlayers, 2);
        if (m.playerCount > 0) return Mathf.Min(m.playerCount, 2);
        return 0;
    }

    public static bool IsCurrentUserHostOfRoom(this Player1v1Match m)
    {
        var uid = ApiController.GetSessionUser?.Data?._id;
        if (m == null || string.IsNullOrEmpty(uid)) return false;
        if (OneVsOneMatchSession.IsHost && OneVsOneMatchSession.CurrentMatchId == m._id) return true;
        if (!string.IsNullOrEmpty(m.createdBy) && m.createdBy == uid) return true;
        if (m.players != null && m.players.Length >= 1 && m.players[0] != null && m.players[0]._id == uid)
        {
            if (m.players.Length == 1) return true;
        }
        return false;
    }

    public static bool CanClickJoinOnRoom(this Player1v1Match m)
    {
        if (m == null) return false;
        if (m.GetTrucoPlayerCount() >= 2) return false;
        if (m.IsCurrentUserHostOfRoom()) return false;
        return true;
    }

    public static bool IsLobbyLikeStatus(this Player1v1Match m)
    {
        if (m == null) return false;
        if (string.IsNullOrEmpty(m.status)) return true;
        string s = m.status.ToLowerInvariant();
        if (s == "active" && m.GetTrucoPlayerCount() >= 2) return false;
        return s == "lobby" || s == "open" || s == "waiting" || s == "pending" || s == "recruiting" || s == "active";
    }
}
