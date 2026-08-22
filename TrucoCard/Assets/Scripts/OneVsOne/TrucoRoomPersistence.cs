using Photon.Pun;
using UnityEngine;

/// <summary>Guards last Photon room + match id for reconnection fallback (JoinRoom after ConnectUsingSettings).</summary>
public static class TrucoRoomPersistence
{
    const string KeyRoom = "TrucoLastPhotonRoom";
    const string KeyMatch = "TrucoLastMatchId";
    const string KeyEntry = "TrucoLastEntryFee";
    const string KeyFlor = "TrucoLastWithFlor";
    const string KeyHost = "TrucoLastIsHost";
    const string KeyTarget = "TrucoLastTargetScore";

    public static void SaveCurrentRoom()
    {
        if (PhotonNetwork.CurrentRoom == null) return;
        var n = PhotonNetwork.CurrentRoom.Name;
        if (string.IsNullOrEmpty(n)) return;
        SaveLobbyKeys(n, OneVsOneMatchSession.CurrentMatchId, OneVsOneMatchSession.EntryFee,
            OneVsOneMatchSession.WithFlor, OneVsOneMatchSession.IsHost, OneVsOneMatchSession.TargetScore);
    }

    /// <summary>After API join/create succeeds but before Photon OnJoinedRoom — enables resume if Photon is slow.</summary>
    public static void SavePendingLobby(string photonRoom, string matchId, int entryFee, bool withFlor, bool isHost,
        int targetScore = TrucoMatchRules.DefaultTargetScore)
    {
        if (string.IsNullOrEmpty(photonRoom) || string.IsNullOrEmpty(matchId)) return;
        SaveLobbyKeys(photonRoom, matchId, entryFee, withFlor, isHost, targetScore);
    }

    static void SaveLobbyKeys(string photonRoom, string matchId, int entryFee, bool withFlor, bool isHost,
        int targetScore)
    {
        PlayerPrefs.SetString(KeyRoom, photonRoom);
        if (!string.IsNullOrEmpty(matchId))
            PlayerPrefs.SetString(KeyMatch, matchId);
        PlayerPrefs.SetInt(KeyEntry, entryFee);
        PlayerPrefs.SetInt(KeyFlor, withFlor ? 1 : 0);
        PlayerPrefs.SetInt(KeyHost, isHost ? 1 : 0);
        PlayerPrefs.SetInt(KeyTarget, TrucoMatchRules.NormalizeTargetScore(targetScore));
        PlayerPrefs.Save();
    }

    public static string LastRoomName() => PlayerPrefs.GetString(KeyRoom, "");

    public static string LastMatchId() => PlayerPrefs.GetString(KeyMatch, "");

    public static int LastEntryFee() => PlayerPrefs.GetInt(KeyEntry, 0);

    public static void RestoreSessionFromSaved(Player1v1Match match)
    {
        if (match == null) return;
        string room = match.ResolvePhotonRoomName();
        if (string.IsNullOrEmpty(room)) room = LastRoomName();
        int fee = match.GetEntryStake() > 0 ? match.GetEntryStake() : PlayerPrefs.GetInt(KeyEntry, 0);
        bool flor = match.withFlor;
        int target = match.targetScore == 0
            ? PlayerPrefs.GetInt(KeyTarget, TrucoMatchRules.DefaultTargetScore)
            : match.targetScore;
        if (match.IsCurrentUserHostOfRoom())
            OneVsOneMatchSession.SetHostContext(match._id, room, fee, flor, target);
        else
            OneVsOneMatchSession.SetGuestContext(match._id, room, fee, flor, target);
    }

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(KeyRoom);
        PlayerPrefs.DeleteKey(KeyMatch);
        PlayerPrefs.DeleteKey(KeyEntry);
        PlayerPrefs.DeleteKey(KeyFlor);
        PlayerPrefs.DeleteKey(KeyHost);
        PlayerPrefs.DeleteKey(KeyTarget);
        PlayerPrefs.Save();
    }
}
