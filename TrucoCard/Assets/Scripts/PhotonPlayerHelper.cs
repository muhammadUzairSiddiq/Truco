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
        if (p?.CustomProperties == null) return null;
        if (!p.CustomProperties.TryGetValue("userId", out object o) || o == null) return null;
        var s = o.ToString();
        return string.IsNullOrEmpty(s) ? null : s;
    }

    /// <summary>Other Truco (non-spectator) player’s backend _id, if they set custom property userId.</summary>
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
