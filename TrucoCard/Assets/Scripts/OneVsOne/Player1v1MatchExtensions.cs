using System;
using Photon.Pun;
using UnityEngine;

public static class Player1v1MatchExtensions
{
    public static string ResolvePhotonRoomName(this Player1v1Match m)
    {
        if (m == null) return null;
        if (!string.IsNullOrEmpty(m.photonRoomName)) return m.photonRoomName.Trim();
        if (!string.IsNullOrEmpty(m.photonRoom)) return m.photonRoom.Trim();
        // Never use display name (name/roomName) — Photon room is always tr1_{matchId} or API photonRoomName.
        if (!string.IsNullOrEmpty(m._id)) return OneVsOneMatchSession.BuildDefaultPhotonRoomName(m._id);
        return null;
    }

    /// <summary>Resolves Photon room name for join/create; always falls back to tr1_{matchId}.</summary>
    public static string ResolvePhotonRoomNameOrDefault(this Player1v1Match m, string matchIdFallback = null)
    {
        string room = m != null ? m.ResolvePhotonRoomName() : null;
        if (!string.IsNullOrEmpty(room)) return room;
        string id = !string.IsNullOrEmpty(matchIdFallback) ? matchIdFallback : m?._id;
        if (!string.IsNullOrEmpty(id)) return OneVsOneMatchSession.BuildDefaultPhotonRoomName(id);
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

    /// <summary>
    /// Prize for a 1v1 = total pot (entry × 2) minus the 10% house fee.
    /// e.g. entry 10 → 18, entry 100 → 180, entry 500 → 900. Backend is the final authority.
    /// </summary>
    public static int ComputeOneVsOnePrize(int entryStake)
    {
        if (entryStake <= 0) return 0;
        return Mathf.FloorToInt(entryStake * 2 * 0.9f);
    }

    public static string FormatEntryPrizeLabel(int entryStake)
    {
        int prize = ComputeOneVsOnePrize(entryStake);
        return string.Format(TrucoTextosClient.EntryPrizeFormat, entryStake, ComputeOneVsOnePrize(entryStake));
    }

    /// <summary>Guest already registered on backend — resume Photon, do not POST /join again.</summary>
    public static bool IsAlreadyRegisteredGuest(this Player1v1Match m) =>
        OneVsOneLobbyFlowRules.IsAlreadyRegisteredGuest(m.IsCurrentUserParticipant(), m.IsCurrentUserHostOfRoom());

    /// <summary>True if the logged-in user is already listed in this match's players array.</summary>
    public static bool IsCurrentUserParticipant(this Player1v1Match m)
    {
        var uid = ApiController.GetSessionUser?.Data?._id;
        if (m == null || string.IsNullOrEmpty(uid) || m.players == null) return false;
        for (int i = 0; i < m.players.Length; i++)
        {
            var p = m.players[i];
            if (p != null && OneVsOneLobbyFlowRules.ResolveUserId(p) == uid) return true;
        }
        return false;
    }

    /// <summary>Should this row appear in the joinable lobby list?</summary>
    public static bool ShouldShowInLobbyList(this Player1v1Match m)
    {
        if (m == null) return false;
        if (!m.IsLobbyLikeStatus()) return false;
        if (m.GetTrucoPlayerCount() >= 2) return false;
        if (m.IsStaleFullVersusPhoton()) return false;
        // Guest already registered — don't offer ENTRAR again (use CONTINUAR via ShouldShowResumeInLobbyList).
        if (m.IsCurrentUserParticipant() && !m.IsCurrentUserHostOfRoom()) return false;
        return true;
    }

    /// <summary>Guest already joined on backend — show CONTINUAR to connect Photon (not a new POST /join).</summary>
    public static bool ShouldShowResumeInLobbyList(this Player1v1Match m) =>
        OneVsOneLobbyFlowRules.ShouldShowResumeInLobbyList(
            m != null && m.IsLobbyLikeStatus(),
            m != null && m.IsCurrentUserParticipant(),
            m != null && m.IsCurrentUserHostOfRoom(),
            PhotonNetwork.InRoom && OneVsOneMatchSession.CurrentMatchId == m._id);

    /// <summary>Prize to show on a room row: trust the API value if present, else compute from entry.</summary>
    public static int GetPrizeForDisplay(this Player1v1Match m)
    {
        if (m == null) return 0;
        if (m.prize > 0) return m.prize;
        return ComputeOneVsOnePrize(m.GetEntryStake());
    }

    /// <summary>Players as reported by GET /matches payload (not merged with Photon).</summary>
    public static int GetApiReportedPlayerCount(this Player1v1Match m)
    {
        if (m == null) return 0;
        if (m.players != null && m.players.Length > 0)
            return Mathf.Clamp(m.players.Length, 0, 2);
        if (m.currentPlayers > 0)
            return Mathf.Clamp(m.currentPlayers, 0, 2);
        if (m.playerCount > 0)
            return Mathf.Clamp(m.playerCount, 0, 2);
        return 0;
    }

    /// <summary>API says full (2) but Photon lobby has synced and shows 0 in the room — ghost / expired lobby row.</summary>
    public static bool IsStaleFullVersusPhoton(this Player1v1Match m)
    {
        if (m == null) return false;
        if (m.GetApiReportedPlayerCount() < 2) return false;
        string room = m.ResolvePhotonRoomName();
        if (string.IsNullOrEmpty(room)) return false;
        if (!OneVsOnePhotonFlow.TryGetLiveLobbyPlayerCountForMatch(room, out int live)) return false;
        return live == 0;
    }

    /// <summary>
    /// Host created lobby (API still 1/2) but Photon room is gone (host killed app / left).
    /// Guests must not JOIN — room should be removed.
    /// </summary>
    public static bool IsHostAbandonedVersusPhoton(this Player1v1Match m)
    {
        if (m == null || !m.IsLobbyLikeStatus()) return false;
        if (m.IsCurrentUserHostOfRoom()) return false;
        if (m.GetApiReportedPlayerCount() >= 2) return false;
        string room = m.ResolvePhotonRoomName();
        if (string.IsNullOrEmpty(room)) return false;
        if (!OneVsOnePhotonFlow.TryGetLiveLobbyPlayerCountForMatch(room, out int live)) return false;
        return live <= 0;
    }

    /// <summary>Player count shown in list: for stale/abandoned rows use live Photon count so UI shows 0/2.</summary>
    public static int GetTrucoPlayerCountForUi(this Player1v1Match m)
    {
        if (m == null) return 0;
        if (m.IsStaleFullVersusPhoton() || m.IsHostAbandonedVersusPhoton())
        {
            string room = m.ResolvePhotonRoomName();
            if (!string.IsNullOrEmpty(room) && OneVsOnePhotonFlow.TryGetLiveLobbyPlayerCountForMatch(room, out int live))
                return Mathf.Clamp(live, 0, 2);
            return 0;
        }
        return m.GetTrucoPlayerCount();
    }

    public static int GetTrucoPlayerCount(this Player1v1Match m)
    {
        if (m == null) return 0;
        // Join is enforced by the API; Photon lobby can lag or show 0 while DB already has 2 players → "Match is full".
        // Use the higher of API-reported count and Photon lobby count so ENTRAR / 0/2 matches reality.
        int apiCount = 0;
        if (m.players != null && m.players.Length > 0)
            apiCount = Mathf.Clamp(m.players.Length, 0, 2);
        else if (m.currentPlayers > 0)
            apiCount = Mathf.Clamp(m.currentPlayers, 0, 2);
        else if (m.playerCount > 0)
            apiCount = Mathf.Clamp(m.playerCount, 0, 2);

        int photonCount = -1;
        string room = m.ResolvePhotonRoomName();
        if (!string.IsNullOrEmpty(room) && OneVsOnePhotonFlow.TryGetLiveLobbyPlayerCountForMatch(room, out int live))
            photonCount = Mathf.Clamp(live, 0, 2);

        if (photonCount < 0)
            return apiCount;
        return Mathf.Clamp(Mathf.Max(photonCount, apiCount), 0, 2);
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
        if (!string.IsNullOrEmpty(host) && host == uid) return true;
        // API list often omits createdBy; sole registered player is the host waiting for a rival.
        if (m.players != null && m.players.Length == 1 && OneVsOneLobbyFlowRules.ResolveUserId(m.players[0]) == uid)
            return true;
        return false;
    }

    public static bool CanClickJoinOnRoom(this Player1v1Match m)
    {
        if (m == null) return false;
        if (m.IsStaleFullVersusPhoton()) return false;
        if (m.IsHostAbandonedVersusPhoton()) return false;
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
        if (s == "finished" || s == "cancelled" || s == "abandoned" || s == "completed" || s == "closed")
            return false;
        if (s == "in_progress" || s == "playing" || s == "live") return false;
        return s == "lobby" || s == "open" || s == "waiting" || s == "pending" || s == "recruiting" || s == "active";
    }
}
