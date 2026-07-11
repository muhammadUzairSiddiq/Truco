using ExitGames.Client.Photon;
using Photon.Realtime;
using UnityEngine;

/// <summary>Truco “retos” use <see cref="Photon.Pun.PhotonNetwork.RaiseEvent"/> with <see cref="RaiseEventOptions.Default"/> (receivers = Others).
/// Audio: local plays immediately via <see cref="TrucoGameplayAudio.PlayLocalRaise"/>; remote gets the same event in <see cref="GameManager.OnEvent"/> and <see cref="TrucoGameplayAudio.PlayFromPhotonEvent"/> so SFX stay aligned with UI/text on all clients.</summary>
public static class TrucoPunChallenges
{
    public static bool IsChallengeEventCode(byte code) =>
        code >= UIMANAGER.TRUCO_CHALLENGE && code <= UIMANAGER.MAZO_CHALLENGE;

    /// <summary>Raise to Others only — UI sync on remote clients (sender already updated locally).</summary>
    public static void RaiseToOthers(byte eventCode, object customContent = null)
    {
        Photon.Pun.PhotonNetwork.RaiseEvent(
            eventCode,
            customContent,
            new RaiseEventOptions { Receivers = ReceiverGroup.Others },
            SendOptions.SendReliable);
    }

    /// <summary>Raise to all clients including sender — required when master adjudicates in OnEvent.</summary>
    public static void RaiseToAll(byte eventCode, object customContent = null)
    {
        Photon.Pun.PhotonNetwork.RaiseEvent(
            eventCode,
            customContent,
            new RaiseEventOptions { Receivers = ReceiverGroup.All },
            SendOptions.SendReliable);
    }
}
