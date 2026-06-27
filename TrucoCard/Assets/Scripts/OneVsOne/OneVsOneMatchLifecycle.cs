using System.Threading.Tasks;
using Photon.Pun;

/// <summary>
/// Client rules (backend is authority for coins):
/// 1) Create/join lobby → entry deducted on server.
/// 2) Cancel before cards (lobby only) → POST /leave → refund.
/// 3) Game started → leave/forfeit → POST /result (opponent wins), no refund.
/// 4) Normal end (15 pts) → POST /result + /leave, winner gets prize, room closes.
/// </summary>
public static class OneVsOneMatchLifecycle
{
    /// <summary>User is in a paid lobby before cards are dealt (leaving should confirm + refund).</summary>
    public static bool IsWaitingInPreGameLobby()
    {
        if (string.IsNullOrEmpty(OneVsOneMatchSession.CurrentMatchId)) return false;
        return !OneVsOneMatchSession.GameStarted;
    }

    public static async Task CancelLobbyMatchAsync(string matchId)
  {
    if (string.IsNullOrEmpty(matchId)) return;
    await ApiController.CancelPreGameMatch1v1(matchId);
    TrucoActiveHostMatchStore.Clear();
  }

  public static async Task ForfeitActiveMatchAsync(string matchId)
  {
    if (string.IsNullOrEmpty(matchId)) return;
    string winner = OneVsOneMatchSession.CachedOpponentUserId;
    if (!string.IsNullOrEmpty(winner))
    {
      if (PhotonNetwork.IsMasterClient)
        await ApiController.Finalize1v1MatchAsMaster(matchId, winner);
      else
        await ApiController.Finalize1v1MatchAsGuest(matchId);
    }
    else
    {
      await ApiController.TryNotifyPlayerLeftMatch1v1(matchId);
      await ApiController.GetCurrentUserProfile();
    }
    TrucoActiveHostMatchStore.Clear();
  }

  public static void ClearStaleHostMemory(System.Collections.Generic.List<Player1v1Match> activeList)
  {
    string remembered = TrucoActiveHostMatchStore.GetRememberedMatchId();
    if (string.IsNullOrEmpty(remembered)) return;
    if (activeList == null)
    {
      TrucoActiveHostMatchStore.Clear();
      return;
    }
    for (int i = 0; i < activeList.Count; i++)
    {
      var m = activeList[i];
      if (m != null && m._id == remembered && m.IsLobbyLikeStatus())
        return;
    }
    TrucoActiveHostMatchStore.Clear();
  }
}
