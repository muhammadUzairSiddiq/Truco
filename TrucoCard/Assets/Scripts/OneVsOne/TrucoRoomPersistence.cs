using Photon.Pun;
using UnityEngine;

/// <summary>Guards last Photon room + match id for reconnection fallback (JoinRoom after ConnectUsingSettings).</summary>
public static class TrucoRoomPersistence
{
    const string KeyRoom = "TrucoLastPhotonRoom";
    const string KeyMatch = "TrucoLastMatchId";

    public static void SaveCurrentRoom()
    {
        if (PhotonNetwork.CurrentRoom == null) return;
        var n = PhotonNetwork.CurrentRoom.Name;
        if (string.IsNullOrEmpty(n)) return;
        PlayerPrefs.SetString(KeyRoom, n);
        if (!string.IsNullOrEmpty(OneVsOneMatchSession.CurrentMatchId))
            PlayerPrefs.SetString(KeyMatch, OneVsOneMatchSession.CurrentMatchId);
        PlayerPrefs.Save();
    }

    public static string LastRoomName() => PlayerPrefs.GetString(KeyRoom, "");

    public static string LastMatchId() => PlayerPrefs.GetString(KeyMatch, "");

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(KeyRoom);
        PlayerPrefs.DeleteKey(KeyMatch);
        PlayerPrefs.Save();
    }
}
