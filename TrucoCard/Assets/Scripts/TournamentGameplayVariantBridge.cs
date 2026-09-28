using MH.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;

/// <summary>
/// Applies tournament-only table configuration before the existing Gameplay scene deals cards.
/// It deliberately does not own, duplicate, or modify any Truco rule; GameManager and UIMANAGER
/// remain the single gameplay implementation used by validated 1v1 tables.
/// </summary>
public static class TournamentGameplayVariantBridge
{
    const string GameplayScene = "Gameplay";

    public static void ApplyTournamentVariant()
    {
        if (SceneManager.GetActiveScene().name != GameplayScene) return;
        var request = MultiplayerController.CurrentTournamentRequest;
        if (request != null)
        {
            OneVsOneMatchSession.SetWithFlor(request.WithFlor);
            OneVsOneMatchSession.SetTargetScore(request.TargetScore);
        }
        if (Photon.Pun.PhotonNetwork.InRoom && Photon.Pun.PhotonNetwork.CurrentRoom != null)
        {
            var props = Photon.Pun.PhotonNetwork.CurrentRoom.CustomProperties;
            if (props != null && props.TryGetValue("withFlor", out object florObj))
                OneVsOneMatchSession.SetWithFlor(Convert.ToBoolean(florObj));
            if (props != null && props.TryGetValue("targetScore", out object scoreObj))
                OneVsOneMatchSession.SetTargetScore(Convert.ToInt32(scoreObj));
        }
        UIMANAGER.Instance?.ApplyTableVariantUi();
        if (request == null) return;
        TrucoDebugLog.Log(TrucoDebugLog.Category.OneVsOne,
            "Tournament table variant applied: flor=" + OneVsOneMatchSession.WithFlor
            + " points=" + OneVsOneMatchSession.TargetScore
            + " tournament=" + request.TournamentId);
    }
}
