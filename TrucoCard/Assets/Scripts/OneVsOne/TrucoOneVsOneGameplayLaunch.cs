using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Loads the Gameplay scene once both Photon clients are in the 1v1 room.</summary>
public static class TrucoOneVsOneGameplayLaunch
{
    public static bool IsGameplaySceneActive =>
        SceneManager.GetActiveScene().name == "Gameplay";

    public static void LoadFromCurrentRoom()
    {
        if (!PhotonNetwork.InRoom) return;
        if (IsGameplaySceneActive) return;
        TrucoMatchProgress.ResetForNewMatch();
        if (!PhotonNetwork.IsMasterClient)
        {
            TrucoDebugLog.Always(TrucoDebugLog.Category.Gameplay,
                "Guest waiting for master to load Gameplay (AutomaticallySyncScene).");
            return;
        }
        TrucoDebugLog.Always(TrucoDebugLog.Category.Gameplay,
            "Host loading Gameplay scene match=" + (OneVsOneMatchSession.CurrentMatchId ?? "?"));
        TrucoSceneTransition.GoPhoton("Gameplay");
    }
}
