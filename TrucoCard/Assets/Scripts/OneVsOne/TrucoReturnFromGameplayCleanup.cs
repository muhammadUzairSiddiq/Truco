using Photon.Pun;
using UnityEngine;

/// <summary>
/// Leaving Gameplay → Photon leave + correct backend call (refund only in pre-game lobby).
/// </summary>
public static class TrucoReturnFromGameplayCleanup
{
    static bool _pending;
    static bool _matchSettledOnExit;

    public static void MarkLeavingGameplay(bool matchAlreadySettled = false)
    {
        if (SpectatorContext.IsSpectator) return;
        _pending = true;
        if (matchAlreadySettled) _matchSettledOnExit = true;
    }

    public static void ConsumeIfNeeded()
    {
        if (!_pending) return;
        _pending = false;
        bool settled = _matchSettledOnExit;
        _matchSettledOnExit = false;

        string matchId = OneVsOneMatchSession.CurrentMatchId;
        bool gameStarted = OneVsOneMatchSession.GameStarted;

        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom(false);

        if (!string.IsNullOrEmpty(matchId))
        {
            if (settled)
                TrucoActiveHostMatchStore.Clear();
            else if (gameStarted)
                _ = OneVsOneMatchLifecycle.ForfeitActiveMatchAsync(matchId);
            else
                _ = OneVsOneMatchLifecycle.CancelLobbyMatchAsync(matchId);
        }

        OneVsOneMatchSession.Clear();
        TrucoMatchProgress.ClearAllMatchMemory();
        if (OneVsOnePhotonFlow.Instance != null) OneVsOnePhotonFlow.Instance.ResetPurpose();
        TrucoLobbyMatchmakingUi.HideWaitingOverlay();

        if (PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InRoom && !PhotonNetwork.InLobby)
            PhotonNetwork.JoinLobby();

        UsernameMainMenuBinder.ApplyToScene();
    }
}
