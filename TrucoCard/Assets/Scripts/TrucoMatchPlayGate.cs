using MH.Multiplayer;
using Photon.Pun;

/// <summary>
/// Tournament tables must not show timeout / reconnection / gameplay banners until a real 1v1
/// match room with two seated players exists. Does not change Truco rules.
/// </summary>
public static class TrucoMatchPlayGate
{
    public static bool IsLiveTable()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return false;
        if (PhotonNetwork.CurrentRoom.PlayerCount < 2) return false;
        if (!MultiplayerController.IsTournamentActive) return true;
        var match = MultiplayerController.ActiveMatch;
        if (match == null || string.IsNullOrEmpty(match.roomName) || string.IsNullOrEmpty(match.matchId)) return false;
        return PhotonNetwork.CurrentRoom.Name == match.roomName
               && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("isTournamentMatch", out object value)
               && value is bool active && active;
    }
}
