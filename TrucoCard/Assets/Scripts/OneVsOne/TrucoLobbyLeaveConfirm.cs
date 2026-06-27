using System;
using Photon.Pun;

/// <summary>Confirms leaving a pre-game 1v1 lobby before navigation cancels the room.</summary>
public static class TrucoLobbyLeaveConfirm
{
    public static void RunIfNeeded(Action onProceed)
    {
        if (!OneVsOneMatchLifecycle.IsWaitingInPreGameLobby())
        {
            onProceed?.Invoke();
            return;
        }

        TrucoConfirmDialog.Show(
            TrucoTextosClient.ConfirmLeaveLobbyTitle,
            TrucoTextosClient.ConfirmLeaveLobbyBody,
            TrucoTextosClient.ConfirmNoQuedarme,
            TrucoTextosClient.ConfirmSiSalir,
            onYes: () => AbandonLobbyAndProceed(onProceed));
    }

    public static async void AbandonLobbyAndProceed(Action onProceed)
    {
        string matchId = OneVsOneMatchSession.CurrentMatchId;
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom(false);
        if (!string.IsNullOrEmpty(matchId))
            await OneVsOneMatchLifecycle.CancelLobbyMatchAsync(matchId);
        OneVsOneMatchSession.Clear();
        if (OneVsOnePhotonFlow.Instance != null) OneVsOnePhotonFlow.Instance.ResetPurpose();
        TrucoLobbyMatchmakingUi.HideWaitingOverlay();
        TrucoWalletHudRefresh.Apply();
        TrucoNotificationLog.Success(TrucoTextosClient.SalaCanceladaReembolso);
        onProceed?.Invoke();
    }
}
