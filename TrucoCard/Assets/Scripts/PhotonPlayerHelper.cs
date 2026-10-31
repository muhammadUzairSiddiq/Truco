using System.Collections.Generic;
using System.Linq;
using Photon.Realtime;
using UnityEngine;

/// <summary>Excludes admin spectators (custom prop isSpectator) from 2P Truco turn logic.</summary>
public static class PhotonPlayerHelper
{
    public const string SpectatorKey = "isSpectator";

    public static bool IsSpectatorPlayer(Player p)
    {
        if (p == null || p.CustomProperties == null) return false;
        if (!p.CustomProperties.TryGetValue(SpectatorKey, out object o) || o == null) return false;
        if (o is bool b) return b;
        if (o is int i) return i == 1;
        var s = o.ToString();
        return s == "1" || s.ToLower() == "true";
    }

    public static List<Player> GetTrucoPlayers()
    {
        if (Photon.Pun.PhotonNetwork.CurrentRoom == null) return new List<Player>();
        return Photon.Pun.PhotonNetwork.PlayerList
            .Where(p => p != null && !IsSpectatorPlayer(p))
            .OrderBy(p => p.ActorNumber)
            .ToList();
    }

    public static string TryGetBackendUserId(Player p)
    {
        // Published UserId comes from Photon authentication. Custom properties are
        // writable by peers and must never identify a participant or a payout recipient.
        return string.IsNullOrWhiteSpace(p?.UserId) ? null : p.UserId;
    }

    public static bool HasMatchingAuthenticatedPair(IEnumerable<Player> players, IEnumerable<string> expectedUserIds,
        string localUserId)
    {
        if (players == null || expectedUserIds == null || string.IsNullOrWhiteSpace(localUserId)) return false;
        var seats = players.Where(p => p != null && !IsSpectatorPlayer(p)).ToArray();
        var expected = expectedUserIds.ToArray();
        if (seats.Length != 2 || expected.Length != 2 || expected.Any(string.IsNullOrWhiteSpace)
            || expected.Distinct(System.StringComparer.Ordinal).Count() != 2) return false;
        var actual = seats.Select(TryGetBackendUserId).ToArray();
        return actual.All(id => !string.IsNullOrWhiteSpace(id))
            && actual.Distinct(System.StringComparer.Ordinal).Count() == 2
            && new HashSet<string>(expected, System.StringComparer.Ordinal).SetEquals(actual)
            && seats.Any(p => p.IsLocal && TryGetBackendUserId(p) == localUserId);
    }

    /// <summary>Local authenticated Photon backend id. Session state cannot substitute for an unverified network peer.</summary>
    public static string GetLocalTrucoPlayerUserId()
    {
        var local = Photon.Pun.PhotonNetwork.LocalPlayer;
        return TryGetBackendUserId(local);
    }

    /// <summary>Other seated player's immutable, published authenticated backend id.</summary>
    public static string GetOtherTrucoPlayerUserId()
    {
        foreach (var p in GetTrucoPlayers())
        {
            if (p == null || p.IsLocal) continue;
            var id = TryGetBackendUserId(p);
            if (!string.IsNullOrEmpty(id)) return id;
        }
        return null;
    }

    public static bool TryGetAvatarIndex(Player p, out int index)
    {
        index = 0;
        if (p?.CustomProperties == null) return false;
        if (!p.CustomProperties.TryGetValue(TrucoPunPlayerAvatarUtil.AvatarKey, out object o) || o == null) return false;
        if (o is int i) { index = UnityEngine.Mathf.Clamp(i, 0, PlayerAvatarData.Count - 1); return true; }
        if (int.TryParse(o.ToString(), out int j)) { index = UnityEngine.Mathf.Clamp(j, 0, PlayerAvatarData.Count - 1); return true; }
        return false;
    }

    /// <summary>When Photon has no avatar property, use a stable per-player fallback (not random every frame).</summary>
    public static int ResolveAvatarIndexForDisplay(Player p)
    {
        if (p == null) return 0;
        if (TryGetAvatarIndex(p, out int i)) return i;
        int salt = p.ActorNumber * 1103515245;
        if (!string.IsNullOrEmpty(p.UserId)) salt ^= p.UserId.GetHashCode();
        return UnityEngine.Mathf.Abs(salt) % PlayerAvatarData.Count;
    }
}
