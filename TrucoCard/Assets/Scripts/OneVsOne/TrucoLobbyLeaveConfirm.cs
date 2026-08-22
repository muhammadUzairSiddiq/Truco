using System;
using Photon.Pun;

/// <summary>Confirms deleting a host room before leaving the pre-game lobby.</summary>
public static class TrucoLobbyLeaveConfirm
{
    public static void RunIfNeeded(Action onProceed)
    {
        if (onProceed == null) return;
        if (OneVsOneMatchLifecycle.IsGuestWaitingInLobby())
        {
            AbandonLobbyAndProceed(onProceed);
            return;
        }
        if (!OneVsOneMatchLifecycle.ShouldConfirmDeleteRoomOnLeave())
        {
            onProceed.Invoke();
            return;
        }

        TrucoConfirmDialog.Show(
            TrucoTextosClient.ConfirmLeaveLobbyTitle,
            TrucoTextosClient.ConfirmLeaveLobbyBody,
            TrucoTextosClient.ConfirmNoQuedarme,
            TrucoTextosClient.ConfirmSiSalir,
            onYes: () => AbandonLobbyAndProceed(onProceed),
            onNo: () => { });

        if (!TrucoConfirmDialog.IsVisible())
        {
            AppManager.Instance?.DisplayNotification(
                TrucoTextosClient.ConfirmLeaveLobbyBody,
                () => AbandonLobbyAndProceed(onProceed));
        }
    }

    public static async void AbandonLobbyAndProceed(Action onProceed)
    {
        string matchId = OneVsOneMatchSession.CurrentMatchId;
        if (string.IsNullOrEmpty(matchId))
            matchId = TrucoActiveHostMatchStore.GetRememberedMatchId();
        // Refund BEFORE Photon leave — leaving first can webhook-close without /leave refund.
        if (!string.IsNullOrEmpty(matchId))
            await OneVsOneMatchLifecycle.CancelLobbyMatchAsync(matchId);
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom(false);
        OneVsOneMatchSession.Clear();
        TrucoMatchProgress.ClearAllMatchMemory();
        TrucoActiveHostMatchStore.Clear();
        if (OneVsOnePhotonFlow.Instance != null) OneVsOnePhotonFlow.Instance.ResetPurpose();
        TrucoLobbyMatchmakingUi.HideWaitingOverlay();
        TrucoWalletHudRefresh.Apply();
        TrucoNotificationLog.Success(TrucoTextosClient.SalaCanceladaReembolso);
        var list = UnityEngine.Object.FindObjectOfType<OneVsOneRoomListController>(true);
        if (list != null && list.IsLobbyVisible)
            list.Refresh(showLoading: false);
        onProceed?.Invoke();
    }
}
