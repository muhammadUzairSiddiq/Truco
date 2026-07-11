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
    /// <summary>User created a host room and is waiting before cards are dealt.</summary>
    public static bool IsHostWaitingForGuest()
    {
        if (OneVsOneMatchSession.GameStarted) return false;
        if (!OneVsOneMatchSession.IsHost) return false;
        return !string.IsNullOrEmpty(OneVsOneMatchSession.CurrentMatchId);
    }

    /// <summary>
    /// Guest paid / registered on backend for a lobby match and is joining or already
    /// seated in Photon before cards. Do not require InRoom — POST /join succeeds first,
    /// then Photon connect; sanitizing in between wiped the session and broke JOIN.
    /// </summary>
    public static bool IsGuestWaitingInLobby()
    {
        if (OneVsOneMatchSession.GameStarted) return false;
        if (OneVsOneMatchSession.IsHost) return false;
        return !string.IsNullOrEmpty(OneVsOneMatchSession.CurrentMatchId);
    }

    /// <summary>User is in a paid lobby before cards are dealt (leaving should confirm + refund).</summary>
    public static bool IsWaitingInPreGameLobby() =>
        IsHostWaitingForGuest() || IsGuestWaitingInLobby();

    /// <summary>Only the host who created a room should see the delete-room confirm when leaving.</summary>
    public static bool ShouldConfirmDeleteRoomOnLeave() => IsHostWaitingForGuest();

    /// <summary>Clear stale host memory when opening the room browser without an active table.</summary>
    public static void SanitizeSessionForRoomBrowser()
    {
        if (IsHostWaitingForGuest() || IsGuestWaitingInLobby()) return;
        if (OneVsOnePhotonFlow.IsMatchmakingBusyGlobally) return;
        TrucoActiveHostMatchStore.Clear();
        if (!OneVsOneMatchSession.GameStarted)
        {
            OneVsOneMatchSession.Clear();
            TrucoRoomPersistence.Clear();
        }
    }

    public static async Task CancelLobbyMatchAsync(string matchId)
  {
    if (string.IsNullOrEmpty(matchId)) return;
    await ApiController.CancelPreGameMatch1v1(matchId);
    TrucoActiveHostMatchStore.Clear();
    TrucoRoomPersistence.Clear();
    OneVsOneMatchSession.ClearSavedRoomPersistence();
  }

  /// <summary>POST /leave on every active lobby row for the logged-in user (refunds entry).</summary>
  public static async Task<int> PurgeAllMyActiveLobbyMatchesAsync(string keepMatchId = null)
  {
    var result = await PurgeAllMyActiveLobbyMatchesDetailedAsync(keepMatchId);
    return result.succeeded;
  }

  public static async Task<ApiController.LobbyPurgeResult> PurgeAllMyActiveLobbyMatchesDetailedAsync(string keepMatchId = null)
  {
    // Never kick the host/guest out of their waiting Photon room — that used to
    // fire OnLeftRoom → cancel the match they just created.
    string except = keepMatchId;
    if (string.IsNullOrEmpty(except) && IsWaitingInPreGameLobby())
        except = OneVsOneMatchSession.CurrentMatchId;

    bool keepLiveLobby = !string.IsNullOrEmpty(except) && IsWaitingInPreGameLobby()
        && except == OneVsOneMatchSession.CurrentMatchId;

    if (PhotonNetwork.InRoom && !OneVsOneMatchSession.GameStarted && !keepLiveLobby)
        PhotonNetwork.LeaveRoom(false);

    var result = await ApiController.PurgeAllMyLobbyMatchesAsync(except);
    if (result.succeeded > 0 && !keepLiveLobby)
    {
      TrucoActiveHostMatchStore.Clear();
      if (string.IsNullOrEmpty(except) || OneVsOneMatchSession.CurrentMatchId != except)
      {
        OneVsOneMatchSession.Clear();
        TrucoRoomPersistence.Clear();
      }
      if (OneVsOnePhotonFlow.Instance != null)
        OneVsOnePhotonFlow.Instance.ResetPurpose();
    }
    return result;
  }

  public static async Task<int> AdminPurgeAllLobbyMatchesAsync(System.Action<string> onError = null)
      => await ApiController.AdminForceCloseAllLobbyMatchesAsync(onError);

  public static async Task ForfeitActiveMatchAsync(string matchId)
  {
    if (string.IsNullOrEmpty(matchId)) return;
    string winner = OneVsOneMatchSession.CachedOpponentUserId
                    ?? PhotonPlayerHelper.GetOtherTrucoPlayerUserId();
    if (!string.IsNullOrEmpty(winner))
      await ApiController.Finalize1v1MatchSettlement(matchId, winner, submitResult: true);
    else
    {
      TrucoRulesScenarioLog.BackendFail("Forfeit settle skipped",
          "no opponent userId for match=" + matchId);
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

    /// <summary>Clears PlayerPrefs / in-memory lobby state when the backend no longer has an open match.</summary>
    public static void ReconcilePersistedLobbyState(System.Collections.Generic.List<Player1v1Match> activeList)
    {
        ClearStaleHostMemory(activeList);

        if (OneVsOneLobbyFlowRules.ShouldPreserveActivePreGameSession(
                !string.IsNullOrEmpty(OneVsOneMatchSession.CurrentMatchId),
                OneVsOneMatchSession.GameStarted))
            return;

        string savedMatchId = TrucoRoomPersistence.LastMatchId();
        if (!string.IsNullOrEmpty(savedMatchId) && !IsMatchActiveInList(savedMatchId, activeList))
            TrucoRoomPersistence.Clear();

        string current = OneVsOneMatchSession.CurrentMatchId;
        if (string.IsNullOrEmpty(current)) return;
        if (OneVsOneMatchSession.GameStarted) return;
        if (IsMatchActiveInList(current, activeList)) return;

        OneVsOneMatchSession.Clear();
        TrucoActiveHostMatchStore.Clear();
        TrucoRoomPersistence.Clear();
        if (OneVsOnePhotonFlow.Instance != null) OneVsOnePhotonFlow.Instance.ResetPurpose();
    }

    static bool IsMatchActiveInList(string matchId, System.Collections.Generic.List<Player1v1Match> activeList)
    {
        if (string.IsNullOrEmpty(matchId) || activeList == null) return false;
        for (int i = 0; i < activeList.Count; i++)
        {
            var m = activeList[i];
            if (m == null || m._id != matchId) continue;
            if (!m.IsLobbyLikeStatus()) return false;
            if (!m.IsCurrentUserParticipant() && !m.IsCurrentUserHostOfRoom()) return false;
            return true;
        }
        return false;
    }
}
