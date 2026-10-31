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
#if UNITY_EDITOR
        if (TrucoDevelopmentSettings.IsIsolatedNetworkRoom(PhotonNetwork.CurrentRoom.Name)) return true;
#endif
        if (!MultiplayerController.IsTournamentActive)
            return PhotonNetwork.CurrentRoom.Name == OneVsOneMatchSession.PhotonRoomName
                && TrucoPhotonAuthentication.IsAuthorizedForRoom("one_vs_one", null,
                    OneVsOneMatchSession.CurrentMatchId, OneVsOneMatchSession.PhotonRoomName)
                && PhotonPlayerHelper.HasMatchingAuthenticatedPair(PhotonNetwork.PlayerList,
                    OneVsOneMatchSession.AuthenticatedParticipants, ApiController.GetSessionUser?.Data?._id);
        return TournamentStartupValidation.HasMatchingTableIdentity(MultiplayerController.ActiveMatch,
            MultiplayerController.CurrentTournamentRequest.TournamentId,
            PhotonNetwork.CurrentRoom.Name, PhotonNetwork.CurrentRoom.CustomProperties);
    }
}
