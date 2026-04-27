using Photon.Pun;
using UnityEngine;

/// <summary>
/// Al volver a MainMenu desde Gameplay, los clientes a menudo siguen <see cref="PhotonNetwork.InRoom"/> (la escena no hace LeaveRoom).
/// Eso mantiene el contador "vivo" y la lista mira datos incorrectos. Marcamos al salir de Gameplay y al entrar a MainMenu hacemos LeaveRoom + limpieza.
/// Además avisamos al backend con <c>POST /matches/:id/leave</c> para sacar al jugador del match en DB (evita "Match is full" fantasma).
/// </summary>
public static class TrucoReturnFromGameplayCleanup
{
    static bool _pending;

    public static void MarkLeavingGameplay()
    {
        if (SpectatorContext.IsSpectator) return;
        _pending = true;
    }

    public static void ConsumeIfNeeded()
    {
        if (!_pending) return;
        _pending = false;
        string matchId = OneVsOneMatchSession.CurrentMatchId;
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom(false);
        if (!string.IsNullOrEmpty(matchId))
            _ = ApiController.TryNotifyPlayerLeftMatch1v1(matchId);
        OneVsOneMatchSession.Clear();
        if (OneVsOnePhotonFlow.Instance != null) OneVsOnePhotonFlow.Instance.ResetPurpose();
        if (PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InRoom && !PhotonNetwork.InLobby)
            PhotonNetwork.JoinLobby();
    }
}
