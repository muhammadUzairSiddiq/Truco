using Photon.Pun;
using UnityEngine;

/// <summary>
/// Al volver a MainMenu desde Gameplay, los clientes a menudo siguen <see cref="PhotonNetwork.InRoom"/> (la escena no hace LeaveRoom).
/// Eso mantiene el contador "vivo" y la lista mira datos incorrectos. Marcamos al salir de Gameplay y al entrar a MainMenu hacemos LeaveRoom + limpieza.
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
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom(false);
        OneVsOneMatchSession.Clear();
        if (OneVsOnePhotonFlow.Instance != null) OneVsOnePhotonFlow.Instance.ResetPurpose();
    }
}
