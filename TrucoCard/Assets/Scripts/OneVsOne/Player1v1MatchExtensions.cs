using System;
using Photon.Pun;
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
        // Live PUN count for the match we are actually in (fixes stale /players on API).
        if (!string.IsNullOrEmpty(m._id)
            && m._id == OneVsOneMatchSession.CurrentMatchId
            && PhotonNetwork.InRoom
            && PhotonNetwork.CurrentRoom != null
            && !string.IsNullOrEmpty(OneVsOneMatchSession.PhotonRoomName)
            && string.Equals(OneVsOneMatchSession.PhotonRoomName, PhotonNetwork.CurrentRoom.Name, StringComparison.Ordinal))
        {
            return Mathf.Clamp(PhotonNetwork.CurrentRoom.PlayerCount, 0, 2);
        }
        int listCount = m.players == null ? 0 : Mathf.Min(m.players.Length, 2);
        int fromApi = 0;
        if (m.currentPlayers > 0) fromApi = m.currentPlayers;
        else if (m.playerCount > 0) fromApi = m.playerCount;
        if (fromApi > 0)
        {
            if (listCount > 0) return Mathf.Clamp(Mathf.Min(fromApi, listCount), 0, 2);
            return Mathf.Clamp(fromApi, 0, 2);
        }
        return listCount;
    }

    /// <summary>Backend host id. Do not use <c>players[0]</c> — list order is not the creator (e.g. joiner can appear first if creator left API ordering).</summary>
    public static string GetHostUserId(this Player1v1Match m)
    {
        if (m == null) return null;
        if (!string.IsNullOrEmpty(m.createdBy)) return m.createdBy;
        if (!string.IsNullOrEmpty(m.hostId)) return m.hostId;
        return null;
    }

    public static bool IsCurrentUserHostOfRoom(this Player1v1Match m)
    {
        var uid = ApiController.GetSessionUser?.Data?._id;
        if (m == null || string.IsNullOrEmpty(uid)) return false;
        if (OneVsOneMatchSession.IsHost && OneVsOneMatchSession.CurrentMatchId == m._id) return true;
        string host = m.GetHostUserId();
        return !string.IsNullOrEmpty(host) && host == uid;
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
        // API uses "active" for open lobbies; must still list when 2/2 (full) so rows show "Llena" / "Tu sala".
        if (s == "finished" || s == "cancelled" || s == "abandoned") return false;
        return s == "lobby" || s == "open" || s == "waiting" || s == "pending" || s == "recruiting" || s == "active";
    }
}
